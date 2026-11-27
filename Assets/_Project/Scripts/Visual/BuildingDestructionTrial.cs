using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // PG07 visual prototype for PG02. Ground damage uses the existing NURS
    // receiver contract; all rubble and fire are presentation, not hitboxes.
    [DisallowMultipleComponent]
    public sealed class BuildingDestructionTrial : DamageableTarget
    {
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public bool IsDestroyed => destroyed;
        public string Status => destroyed ? "PG07 / DESTROYED - FIRE ACTIVE / R TO RETRY" :
            "PG07 / CONTROL BUILDING / RMB NURS / R TO RETRY";

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

        public bool Initialize(int plumeVariant = 0)
        {
            if (ready) return true;
            intact = transform.Find("Visual");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var effect = Shader.Find("AirCombat/SandboxEffect");
            if (intact == null || lit == null || unlit == null || effect == null)
            {
                Debug.LogError("PG07: missing PG02 Visual or destruction shaders.", this);
                return false;
            }
            hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, 3.7f, -.5f);
            hitbox.size = new Vector3(26f, 7.4f, 17f);
            block = MakeBlock();
            puff = MakePuff();
            blastMaterial = RocketImpactFx.CreateMaterial(effect, out blastMask);
            wreck = new GameObject("PG07 / persistent ruin").transform;
            wreck.SetParent(transform, false);
            BuildRuin(lit, unlit, plumeVariant);
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
            foreach (var d in debris) d.Node.gameObject.SetActive(false);
            wreck.gameObject.SetActive(false);
            intact.gameObject.SetActive(true);
            hitbox.enabled = true;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            // One successful blast is enough for this presentation trial.
            if (!ready || destroyed || !isActiveAndEnabled || damage <= 0) return false;
            destroyed = true;
            age = 0f;
            hitbox.enabled = false;
            intact.gameObject.SetActive(false);
            wreck.gameObject.SetActive(true);
            TickPresentation();
            burst = RocketImpactFx.Spawn(transform.TransformPoint(new Vector3(-2f, 4f, 0f)), blastMaterial, 13f);
            burst.transform.SetParent(transform, true);
            return true;
        }

        private void BuildRuin(Shader lit, Shader unlit, int plumeVariant)
        {
            var concrete = Paint(lit, "PG07 concrete", new Color32(127, 130, 119, 255));
            var wall = Paint(lit, "PG07 damaged wall", new Color32(137, 138, 126, 255));
            var scorched = Paint(lit, "PG07 char", new Color32(51, 51, 47, 255));
            var roof = Paint(lit, "PG07 roof rubble", new Color32(75, 83, 82, 255));
            var smoke = new[] {
                Paint(lit, "PG07 smoke dark", new Color32(52, 53, 54, 255)),
                Paint(lit, "PG07 smoke medium", new Color32(65, 66, 65, 255)),
                Paint(lit, "PG07 smoke outer", new Color32(80, 80, 77, 255))
            };
            var flame = new[] {
                Paint(unlit, "PG07 ember orange", new Color32(231, 79, 20, 255)),
                Paint(unlit, "PG07 flame", new Color32(255, 148, 35, 255)),
                Paint(unlit, "PG07 flame core", new Color32(255, 222, 102, 255))
            };
            // Match the approved PG02 footprint. Keep a broken perimeter and
            // two fallen roof masses; the silhouette must not read as intact.
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
            // Variant zero keeps PG07B exactly. Change only the plume random stream
            // on the other buildings, not the accepted rubble or thrown debris.
            if (plumeVariant != 0)
                rng = new System.Random(unchecked(7402 + plumeVariant * 7919));
            // Bounded pool; no per-frame allocations or repeated instantiation.
            for (int i=0; i<10; i++)
            {
                bool primary = i%2 == 0;
                float focusScale = primary ? 1.65f : 1.42f;
                Vector3 origin = primary ? new Vector3(-4f,2.2f,-.7f) : new Vector3(2.7f,1.8f,3.2f);
                origin += new Vector3(Range(rng,-1.2f,1.2f)*focusScale,0,Range(rng,-1f,1f)*focusScale);
                AddPlume(flame[i%3], origin, new Vector3(Range(rng,.35f,.95f),0,Range(rng,.1f,.6f)), i*.085f,
                    Range(rng,.65f,1.15f),Range(rng,2.3f,3.5f)*focusScale,Range(rng,3.2f,5.0f)*focusScale,true,rng);
            }
            for (int i=0; i<15; i++)
            {
                Vector3 origin = new Vector3(-2.6f+Range(rng,-2f,2f),4.2f,Range(rng,-1.8f,1.8f));
                AddPlume(smoke[i%3],origin,new Vector3(Range(rng,7f,14f),0,Range(rng,1f,6f)),i*.22f+Range(rng,0f,.12f),
                    Range(rng,3.1f,5.0f),Range(rng,4.2f,6.0f),Range(rng,19f,31f),false,rng);
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

        private void AddPlume(Material material,Vector3 origin,Vector3 drift,float delay,float lifetime,float size,float rise,bool fire,System.Random rng)
        {
            var t=Piece(fire ? "Faceted flame" : "Faceted smoke",puff,material,origin,Vector3.one,shadow:false);
            plume.Add(new PlumePiece { Node=t,Origin=origin,Drift=drift,Delay=delay,Lifetime=lifetime,Size=size,Rise=rise,Fire=fire,
                Spin=Range(rng,-55f,55f),Aspect=fire ? Range(rng,1.75f,2.1f) : Range(rng,.88f,1.05f),
                Expansion=fire ? .45f : Range(rng,1.4f,1.85f),
                Rotation=fire
                    ? Quaternion.Euler(Range(rng,-12f,12f),Range(rng,0f,360f),Range(rng,-12f,12f))
                    : Quaternion.Euler(Range(rng,0f,360f),Range(rng,0f,360f),Range(rng,0f,360f)) });
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
            if (block != null) Destroy(block);
            if (puff != null) Destroy(puff);
            if (blastMaterial != null) Destroy(blastMaterial);
            if (blastMask != null) Destroy(blastMask);
            foreach (var m in materials) if (m != null) Destroy(m);
        }
    }
}
