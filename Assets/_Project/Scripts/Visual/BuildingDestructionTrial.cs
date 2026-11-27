using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // PG07/08 persistent prop ruins and pooled cosmetic effects.
    [DisallowMultipleComponent]
    public sealed class BuildingDestructionTrial : DamageableTarget
    {
        public enum RuinProfile { ControlBuilding, CompactProp, LongProp }
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public bool IsDestroyed => destroyed;
        public int FireFocusCount { get; private set; }
        public string Status => destroyed ? $"PG07 / DESTROYED - {FireFocusCount} FIRE FOCI / R TO RETRY" :
            "PG07 / CONTROL BUILDING / RMB NURS / R TO RETRY";

        // PG02-local sockets on the exposed floor, broken roof and annex.
        // Choose without replacement; jitter stays within each socket's zone.
        private static readonly Vector3[] FireSockets = {
            new Vector3(-8f,3.4f,-3.8f), new Vector3(-3.2f,1.8f,-1.1f),
            new Vector3(-6.5f,1.7f,3.7f), new Vector3(2.7f,2.2f,3.2f),
            new Vector3(2.8f,1.5f,-5.3f), new Vector3(9.1f,4.2f,-2.2f)
        };
        private static readonly Vector3[] CompactSockets = {
            new Vector3(-6f,2.7f,-2.5f), new Vector3(-5f,2.1f,3.8f),
            new Vector3(-.5f,3.9f,-.5f), new Vector3(3.8f,3.4f,2.2f),
            new Vector3(5.6f,1.9f,-4f), new Vector3(7.6f,1.8f,4.3f)
        };
        private static readonly Vector3[] LongSockets = {
            new Vector3(-9f,2.4f,-2.7f), new Vector3(-7f,2.0f,4f),
            new Vector3(-2.6f,3.5f,-.5f), new Vector3(3.5f,3.9f,.5f),
            new Vector3(7f,2.0f,-4f), new Vector3(9f,2.5f,3.5f)
        };
        private RuinProfile profile;
        private bool sharedYard, allowWeaponHits;
        private Vector3[] activeSockets = FireSockets;
        private float effectScale = 1f;
        private const int FlameBudget = 12, SmokeBudget = 18;
        private readonly int[] socketOrder = new int[FireSockets.Length];
        private readonly Vector3[] focusOrigins = new Vector3[4];
        private readonly float[] focusStrengths = new float[4];
        private System.Random plumeRandom;
        private Material[] flameMaterials, smokeMaterials;

        private Transform intact, wreck;
        private BoxCollider hitbox;
        private bool ready, destroyed;
        private float age;
        private GameObject burst;
        private Mesh block, puff;
        private Material blastMaterial;
        private Texture2D blastMask;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<PlumePiece> plume = new List<PlumePiece>();
        private readonly List<DebrisPiece> debris = new List<DebrisPiece>();

        private sealed class PlumePiece
        {
            public Transform Node;
            public MeshRenderer Renderer;
            public Vector3 Origin, Drift;
            public float Delay, Lifetime, Size, Rise, Spin, Aspect, Expansion;
            public bool Fire;
            public Quaternion Rotation;
        }

        private sealed class DebrisPiece
        {
            public Transform Node;
            public Vector3 Origin, Velocity, Spin;
            public float FlightTime, RestY;
            public Quaternion Rotation;
        }

        public bool Initialize(int plumeVariant = 0, RuinProfile ruinProfile = RuinProfile.ControlBuilding,
            bool useSharedYard = false, bool installWeaponHitbox = true)
        {
            if (ready) return true;
            profile = ruinProfile;
            sharedYard = useSharedYard;
            allowWeaponHits = installWeaponHitbox;
            activeSockets = profile == RuinProfile.CompactProp ? CompactSockets :
                profile == RuinProfile.LongProp ? LongSockets : FireSockets;
            effectScale = profile == RuinProfile.CompactProp ? .82f :
                profile == RuinProfile.LongProp ? .92f : 1f;
            intact = transform.Find("Visual");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var effect = Shader.Find("AirCombat/SandboxEffect");
            if (intact == null || lit == null || unlit == null || effect == null)
            {
                Debug.LogError("PG08: missing Visual or destruction shaders.", this);
                return false;
            }
            // The protected M03 props are explicit game targets, not targets for
            // player weapons. Sandbox keeps its existing hitbox behavior.
            if (allowWeaponHits)
            {
                hitbox = gameObject.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0f, 3.7f, -.5f);
                hitbox.size = new Vector3(26f, 7.4f, 17f);
            }
            block = MakeBlock();
            puff = MakePuff();
            blastMaterial = RocketImpactFx.CreateMaterial(effect, out blastMask);
            wreck = new GameObject("PG07 / persistent ruin").transform;
            wreck.SetParent(transform, false);
            // Independent cosmetic RNG; never alter Unity's gameplay random stream.
            plumeRandom = new System.Random(unchecked(System.Guid.NewGuid().GetHashCode() + plumeVariant * 7919));
            BuildRuin(lit, unlit);
            ready = true;
            ResetTarget();
            return true;
        }

        public void ResetTarget()
        {
            if (!ready) return;
            destroyed = false;
            age = 0f;
            ClearBurst();
            foreach (var p in plume) p.Node.gameObject.SetActive(false);
            ConfigureFireLayout();
            foreach (var d in debris) d.Node.gameObject.SetActive(false);
            wreck.gameObject.SetActive(false);
            intact.gameObject.SetActive(true);
            if (hitbox != null) hitbox.enabled = true;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            // One successful blast is enough for this presentation trial.
            if (!ready || destroyed || !isActiveAndEnabled || damage <= 0) return false;
            destroyed = true;
            age = 0f;
            if (hitbox != null) hitbox.enabled = false;
            intact.gameObject.SetActive(false);
            wreck.gameObject.SetActive(true);
            TickPresentation();
            burst = RocketImpactFx.Spawn(transform.TransformPoint(new Vector3(-2f, 4f, 0f)), blastMaterial, 13f);
            burst.transform.SetParent(transform, true);
            return true;
        }

        private void BuildRuin(Shader lit, Shader unlit)
        {
            var concrete = Paint(lit, "PG07 concrete", new Color32(127, 130, 119, 255));
            var wall = Paint(lit, "PG07 damaged wall", new Color32(137, 138, 126, 255));
            var scorched = Paint(lit, "PG07 char", new Color32(51, 51, 47, 255));
            var roof = Paint(lit, "PG07 roof rubble", new Color32(75, 83, 82, 255));
            smokeMaterials = new[] {
                Paint(lit, "PG07 smoke dark", new Color32(52, 53, 54, 255)),
                Paint(lit, "PG07 smoke medium", new Color32(65, 66, 65, 255)),
                Paint(lit, "PG07 smoke outer", new Color32(80, 80, 77, 255))
            };
            flameMaterials = new[] {
                Paint(unlit, "PG07 ember orange", new Color32(231, 79, 20, 255)),
                Paint(unlit, "PG07 flame", new Color32(255, 148, 35, 255)),
                Paint(unlit, "PG07 flame core", new Color32(255, 222, 102, 255))
            };
            if (profile != RuinProfile.ControlBuilding)
            {
                BuildEquipmentRuin(concrete, wall, scorched, roof);
                for (int i=0; i<FlameBudget; i++) CreatePlume(true);
                for (int i=0; i<SmokeBudget; i++) CreatePlume(false);
                return;
            }
            // Match the approved PG02 footprint. In PG06 the broad slab is
            // intentionally hidden; do not resurrect it under the wreck.
            if (!sharedYard)
                Piece("Foundation", block, concrete, new Vector3(0,.145f,0), new Vector3(27.57f,.29f,19.28f));
            Piece("Charred floor", block, scorched, new Vector3(-3,.40f,-.65f), new Vector3(18.2f,.25f,14f));
            Piece("Rear wall left", block, wall, new Vector3(-8.5f,2.15f,-7.3f), new Vector3(7.0f,3.7f,.9f));
            Piece("Rear wall stump", block, scorched, new Vector3(-.2f,1.25f,-7.3f), new Vector3(5.0f,1.9f,.9f));
            Piece("Left wall", block, wall, new Vector3(-12.1f,2.4f,-3.65f), new Vector3(.9f,4.2f,6.1f));
            Piece("Left broken wall", block, wall, new Vector3(-12.0f,1.4f,3.2f), new Vector3(.9f,2.2f,4.8f), new Vector3(0,0,-7));
            Piece("Front wall stump", block, wall, new Vector3(-8.1f,1.6f,6.35f), new Vector3(6.1f,2.6f,.8f));
            Piece("Front broken pier", block, scorched, new Vector3(3.9f,1.55f,6.3f), new Vector3(1.6f,2.5f,.95f), new Vector3(0,0,12));
            Piece("Annex damaged mass", block, wall, new Vector3(9.1f,1.8f,-2.2f), new Vector3(6.7f,3f,9.7f));
            Piece("Annex fallen cap", block, roof, new Vector3(9.3f,3.75f,-2.5f), new Vector3(6.9f,.65f,8.9f), new Vector3(7,4,-9));
            Piece("Collapsed roof left", block, roof, new Vector3(-7.4f,2.65f,-2.2f), new Vector3(7.5f,.8f,8.1f), new Vector3(17,-12,19));
            Piece("Collapsed roof right", block, roof, new Vector3(1.6f,1.6f,2.1f), new Vector3(6.5f,.75f,5.4f), new Vector3(-10,23,-12));

            var rng = new System.Random(7402);
            for (int i=0; i<12; i++)
            {
                float x = Range(rng,-8.5f,5.2f), z = Range(rng,-5.1f,5.2f);
                Piece("Settled rubble", block, i%3 == 0 ? scorched : wall,
                    new Vector3(x,.73f,z), new Vector3(Range(rng,1.1f,2.7f),Range(rng,.45f,.9f),Range(rng,1.0f,2.4f)),
                    new Vector3(Range(rng,-18f,18f),Range(rng,0f,360f),Range(rng,-16f,16f)));
            }
            for (int i=0; i<9; i++)
            {
                Vector3 start = new Vector3(Range(rng,-7f,3f),Range(rng,3.8f,5.7f),Range(rng,-4f,4f));
                float angle = i * Mathf.PI * 2f / 9f + Range(rng,-.2f,.2f);
                Vector3 v = new Vector3(Mathf.Cos(angle)*Range(rng,8f,15f),Range(rng,10f,19f),Mathf.Sin(angle)*Range(rng,8f,15f));
                Vector3 size = new Vector3(Range(rng,1.1f,2.2f),Range(rng,.3f,.65f),Range(rng,.9f,1.9f));
                float restY = Mathf.Max(size.x,Mathf.Max(size.y,size.z))*.5f;
                var t = Piece("Thrown rubble", block, i%2 == 0 ? roof : wall, start, size);
                const float gravity = 35f;
                float flight = (v.y + Mathf.Sqrt(v.y*v.y + 2f*gravity*(start.y-restY)))/gravity;
                debris.Add(new DebrisPiece { Node=t, Origin=start, Velocity=v, RestY=restY, FlightTime=flight,
                    Spin=new Vector3(Range(rng,-280f,280f),Range(rng,-280f,280f),Range(rng,-280f,280f)),
                    Rotation=Quaternion.Euler(0,Range(rng,0f,360f),0) });
            }
            // A fixed pool, irrespective of how many sockets are burning.
            for (int i=0; i<FlameBudget; i++) CreatePlume(true);
            for (int i=0; i<SmokeBudget; i++) CreatePlume(false);
        }

        private void BuildEquipmentRuin(Material concrete, Material wall, Material scorched, Material roof)
        {
            // Abstract low-poly wreck silhouettes; preserve the authored small
            // footing (including PG06's hidden display-slab renderer override).
            var foundation = intact.Find("Foundation");
            if (foundation != null)
            {
                var retained = Instantiate(foundation.gameObject, wreck, false);
                retained.name = "Retained footing";
            }
            else
                Piece("Footing",block,concrete,new Vector3(0,.45f,0),new Vector3(14f,.6f,9f));

            bool elongated = profile == RuinProfile.LongProp;
            float width = elongated ? 19f : 15f;
            Piece("Charred mass",block,scorched,new Vector3(0,1.1f,0),new Vector3(width,1.6f,9f));
            if (elongated)
            {
                Piece("Broken left mass",block,roof,new Vector3(-5.1f,2.4f,-.5f),new Vector3(7.7f,3.2f,7.2f),new Vector3(8,0,12));
                Piece("Broken right mass",block,wall,new Vector3(4.8f,2.6f,.6f),new Vector3(7.5f,3.6f,7f),new Vector3(-9,4,-15));
                Piece("Fallen long panel",block,roof,new Vector3(-3.6f,1.5f,4.3f),new Vector3(8.6f,.55f,3f),new Vector3(13,-8,6));
                Piece("Detached panel",block,roof,new Vector3(7.9f,1.2f,-4.6f),new Vector3(5f,.65f,3.1f),new Vector3(-8,17,-5));
            }
            else
            {
                Piece("Collapsed central mass",block,roof,new Vector3(-2f,2.5f,-.5f),new Vector3(10.5f,3.7f,7.5f),new Vector3(12,-6,14));
                Piece("Separated block",block,wall,new Vector3(5.7f,1.9f,2.5f),new Vector3(5.2f,2.7f,5.8f),new Vector3(-10,12,-13));
                Piece("Fallen panel",block,roof,new Vector3(-4.7f,1.2f,4.5f),new Vector3(6f,.65f,3.5f),new Vector3(14,-12,8));
            }
            var rng = new System.Random(elongated ? 803 : 801);
            for (int i=0; i<9; i++)
            {
                float x = Range(rng,-width*.5f,width*.5f);
                float z = Range(rng,-5.8f,5.8f);
                Piece("Settled scrap",block,i%3 == 0 ? wall : roof,
                    new Vector3(x,.85f,z),new Vector3(Range(rng,.8f,2.2f),Range(rng,.3f,.85f),Range(rng,.7f,1.8f)),
                    new Vector3(Range(rng,-20f,20f),Range(rng,0f,360f),Range(rng,-20f,20f)));
            }
            for (int i=0; i<7; i++)
            {
                var start = new Vector3(Range(rng,-width*.35f,width*.35f),Range(rng,3f,4.8f),Range(rng,-3f,3f));
                float angle = i * Mathf.PI * 2f / 7f + Range(rng,-.35f,.35f);
                var velocity = new Vector3(Mathf.Cos(angle)*Range(rng,5f,11f),Range(rng,8f,15f),Mathf.Sin(angle)*Range(rng,5f,11f));
                var size = new Vector3(Range(rng,.8f,1.9f),Range(rng,.3f,.6f),Range(rng,.7f,1.5f));
                float restY = Mathf.Max(size.x,Mathf.Max(size.y,size.z))*.5f + (sharedYard ? .27f : 0f);
                var t = Piece("Thrown scrap",block,i%2 == 0 ? roof : wall,start,size);
                float flight = (velocity.y + Mathf.Sqrt(velocity.y*velocity.y + 70f*(start.y-restY)))/35f;
                debris.Add(new DebrisPiece { Node=t, Origin=start, Velocity=velocity, RestY=restY, FlightTime=flight,
                    Spin=new Vector3(Range(rng,-220f,220f),Range(rng,-220f,220f),Range(rng,-220f,220f)),
                    Rotation=Quaternion.Euler(0,Range(rng,0f,360f),0) });
            }
        }

        private void ConfigureFireLayout()
        {
            var rng = plumeRandom;
            FireFocusCount = rng.Next(2,5);
            for (int i=0; i<socketOrder.Length; i++) socketOrder[i] = i;
            for (int i=0; i<FireFocusCount; i++)
            {
                int j = rng.Next(i,socketOrder.Length);
                int swap = socketOrder[i]; socketOrder[i] = socketOrder[j]; socketOrder[j] = swap;
                focusOrigins[i] = activeSockets[socketOrder[i]] +
                    new Vector3(Range(rng,-.65f,.65f),0,Range(rng,-.65f,.65f));
                // One strong focal point, smaller secondary fires.
                focusStrengths[i] = i == 0 ? Range(rng,1.4f,1.7f) : Range(rng,.95f,1.4f);
            }
            float densityScale = FireFocusCount == 2 ? 1f : FireFocusCount == 3 ? .91f : .82f;
            for (int i=0; i<plume.Count; i++)
            {
                var p = plume[i];
                int slot = p.Fire ? i : i-FlameBudget;
                int focus = slot % FireFocusCount;
                int layer = slot / FireFocusCount;
                float strength = focusStrengths[focus] * densityScale * effectScale;
                Vector3 centre = focusOrigins[focus];
                var palette = p.Fire ? flameMaterials : smokeMaterials;
                p.Renderer.sharedMaterial = palette[(layer+focus)%palette.Length];
                if (p.Fire)
                {
                    Vector3 origin = centre + new Vector3(Range(rng,-.85f,.85f)*strength,0,Range(rng,-.75f,.75f)*strength);
                    ConfigurePlume(p,origin,new Vector3(Range(rng,.35f,.95f),0,Range(rng,.1f,.6f)),
                        layer*.13f+Range(rng,0f,.09f),Range(rng,.65f,1.15f),
                        Range(rng,2.3f,3.5f)*strength,Range(rng,3.2f,5.0f)*strength,rng);
                }
                else
                {
                    // Smoke starts above its own fire; all columns share the
                    // same general wind, but differ in lift, bulk and timing.
                    float smokeScale = Mathf.Lerp(.70f,1.12f,Mathf.InverseLerp(.8f,1.7f,strength));
                    Vector3 origin = centre + new Vector3(Range(rng,-.75f,.75f),1.5f,Range(rng,-.75f,.75f));
                    ConfigurePlume(p,origin,new Vector3(Range(rng,7f,14f),0,Range(rng,1f,6f)),
                        layer*.38f+Range(rng,.12f,.38f),Range(rng,3.1f,5.0f),
                        Range(rng,4.2f,6.0f)*smokeScale,Range(rng,19f,31f)*smokeScale,rng);
                }
            }
        }

        private Material Paint(Shader shader, string name, Color color)
        {
            var m = new Material(shader) { name=name };
            m.SetColor("_BaseColor",color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",.04f);
            materials.Add(m);
            return m;
        }

        private Transform Piece(string name, Mesh mesh, Material material, Vector3 pos, Vector3 scale,
            Vector3 euler = default, bool shadow = true)
        {
            var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(wreck,false);
            go.transform.localPosition=pos;
            go.transform.localRotation=Quaternion.Euler(euler);
            go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            if (!shadow) { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; }
            return go.transform;
        }

        private void CreatePlume(bool fire)
        {
            var material = fire ? flameMaterials[0] : smokeMaterials[0];
            var t = Piece(fire ? "Faceted flame" : "Faceted smoke",puff,material,Vector3.zero,Vector3.one,shadow:false);
            t.gameObject.SetActive(false);
            plume.Add(new PlumePiece { Node=t, Renderer=t.GetComponent<MeshRenderer>(), Fire=fire });
        }

        private static void ConfigurePlume(PlumePiece p,Vector3 origin,Vector3 drift,float delay,float lifetime,float size,float rise,System.Random rng)
        {
            p.Origin=origin; p.Drift=drift; p.Delay=delay; p.Lifetime=lifetime; p.Size=size; p.Rise=rise;
            p.Spin=Range(rng,-55f,55f);
            p.Aspect=p.Fire ? Range(rng,1.75f,2.1f) : Range(rng,.88f,1.05f);
            p.Expansion=p.Fire ? .45f : Range(rng,1.4f,1.85f);
            p.Rotation=p.Fire
                ? Quaternion.Euler(Range(rng,-12f,12f),Range(rng,0f,360f),Range(rng,-12f,12f))
                : Quaternion.Euler(Range(rng,0f,360f),Range(rng,0f,360f),Range(rng,0f,360f));
        }

        private void Update()
        {
            if (!destroyed) return;
            age += Time.deltaTime;
            TickPresentation();
        }

        private void TickPresentation()
        {
            foreach (var p in plume)
            {
                if (age < p.Delay) { p.Node.gameObject.SetActive(false); continue; }
                float t=Mathf.Repeat(age-p.Delay,p.Lifetime)/p.Lifetime;
                float envelope=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(t/.12f)) *
                    (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.72f,1f,t)));
                float scale=p.Size * envelope * Mathf.Lerp(p.Fire ? 1f : .65f,p.Expansion,t);
                p.Node.gameObject.SetActive(scale>.015f);
                p.Node.localPosition=p.Origin+p.Drift*t+Vector3.up*(p.Rise*t);
                p.Node.localRotation=p.Fire
                    ? Quaternion.AngleAxis(p.Spin*t,Vector3.up)*p.Rotation
                    : Quaternion.Euler(p.Spin*t*.32f,p.Spin*t,-p.Spin*t*.23f)*p.Rotation;
                p.Node.localScale=new Vector3(scale,scale*p.Aspect,scale);
            }
            foreach (var d in debris)
            {
                d.Node.gameObject.SetActive(true);
                float t=Mathf.Min(age,d.FlightTime);
                Vector3 pos=d.Origin+d.Velocity*t+Vector3.down*(17.5f*t*t);
                pos.y=Mathf.Max(pos.y,d.RestY);
                d.Node.localPosition=pos;
                d.Node.localRotation=d.Rotation*Quaternion.Euler(d.Spin*t);
            }
        }

        private static float Range(System.Random rng,float a,float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());

        private static Mesh MakeBlock()
        {
            var v = new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
            return FlatMesh("PG07 block",v,new[]{0,1,2,0,2,3,4,6,5,4,7,6,0,3,7,0,7,4,1,5,6,1,6,2,0,4,5,0,5,1,3,2,6,3,6,7});
        }

        private static Mesh MakePuff()
        {
            float g=(1f+Mathf.Sqrt(5f))*.5f;
            var v=new[]{new Vector3(-1,g,0),new Vector3(1,g,0),new Vector3(-1,-g,0),new Vector3(1,-g,0),
                new Vector3(0,-1,g),new Vector3(0,1,g),new Vector3(0,-1,-g),new Vector3(0,1,-g),
                new Vector3(g,0,-1),new Vector3(g,0,1),new Vector3(-g,0,-1),new Vector3(-g,0,1)};
            for(int i=0;i<v.Length;i++) v[i]=v[i].normalized*.5f;
            var mesh = FlatMesh("PG07 faceted puff",v,new[]{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,
                1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,
                4,9,5,2,4,11,6,2,10,8,6,7,9,8,1});
            // Preserve the low-poly silhouette but soften lighting across faces.
            // Flame uses the same mesh with an unlit material; rubble uses block.
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (int i=0; i<normals.Length; i++)
                normals[i] = Vector3.Slerp(normals[i],vertices[i].normalized,.55f).normalized;
            mesh.normals = normals;
            return mesh;
        }

        private static Mesh FlatMesh(string name,Vector3[] source,int[] faces)
        {
            var vertices=new Vector3[faces.Length]; var indices=new int[faces.Length];
            for(int i=0;i<faces.Length;i+=3)
            {
                Vector3 a=source[faces[i]],b=source[faces[i+1]],c=source[faces[i+2]];
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c)<0f) { Vector3 tmp=b;b=c;c=tmp; }
                vertices[i]=a;vertices[i+1]=b;vertices[i+2]=c;
                indices[i]=i;indices[i+1]=i+1;indices[i+2]=i+2;
            }
            var mesh=new Mesh { name=name,vertices=vertices,triangles=indices };
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private void ClearBurst()
        {
            if (burst != null) { burst.SetActive(false); Destroy(burst); }
            burst=null;
        }

        private void OnDestroy()
        {
            ClearBurst();
            if (wreck != null) Destroy(wreck.gameObject);
            if (hitbox != null) Destroy(hitbox);
            if (block != null) Destroy(block);
            if (puff != null) Destroy(puff);
            if (blastMaterial != null) Destroy(blastMaterial);
            if (blastMask != null) Destroy(blastMask);
            foreach (var m in materials) if (m != null) Destroy(m);
        }
    }
}
