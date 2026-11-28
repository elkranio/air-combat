using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AirCombat
{
    // MAP-02: runtime view of a baked geography asset. The prototype markers
    // are explicit scene references, not a second source of terrain geometry.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class TacticalMapView : MonoBehaviour
    {
        [Serializable] public struct PlannedSam
        {
            public Transform site;
            [HideInInspector, Min(0f)] public float radius; // Legacy planning fallback only.
            public string label;
            public SurfaceToAirLauncher Launcher => site != null ? site.GetComponent<SurfaceToAirLauncher>() : null;
            public float Radius => Launcher != null ? Launcher.RadarThreatRadius : radius;
            public bool Visible => site != null && site.gameObject.activeInHierarchy &&
                (Launcher == null || Launcher.IsRadarVisible);
        }
        [SerializeField] private MissionMapData map;
        [SerializeField] private bool autoBakeMap = true;
        [SerializeField] private Transform player;
        [SerializeField] private string missionTitle = "FIRST SORTIE";
        [SerializeField] private PlannedSam[] plannedSam = Array.Empty<PlannedSam>();
        [SerializeField] private Transform[] recon = Array.Empty<Transform>();
        [SerializeField] private Transform optionalTarget;
        private Texture2D ring, arrow;
        private Canvas canvas;
        private RectTransform frame, drawParent;
        private Font font;
        private int screenWidth, screenHeight;
        // A snapshot of the same ground-plane axes as the combat radar.
        private Vector2 mapRight, mapUp;
        private float mapScale;
        private bool cameraUp;
        private MissionBoundaryGuard operationBoundary;
        private Mission02RouteTrial routeTrial;
        private readonly List<RadarDetection> airContacts = new List<RadarDetection>();
        private ObjectiveProgress primaryProgress;
        private static readonly Color Ink = new Color(.80f,.89f,.87f);
        private static readonly Color Muted = new Color(.53f,.66f,.65f);
        private static readonly Color Cyan = new Color(.48f,.92f,.94f);
        private static readonly Color Red = new Color(.86f,.36f,.30f);
        private static readonly Color Gold = new Color(.97f,.74f,.36f);

        private bool IsOpen => MissionPause.IsOwnedBy(this, MissionPause.Reason.TacticalMap);
        private bool HasMap => map != null && map.Geography != null && map.WorldBounds.width > 0 && map.WorldBounds.height > 0 && player != null;

#if UNITY_EDITOR
        public bool AutoBakeMap => autoBakeMap;
        public MissionMapData GeographyData => map;
        public void SetGeographyForAuthoring(MissionMapData data) => map = data;

        public void ConfigureForAuthoring(MissionMapData data, Transform aircraft, PlannedSam[] sites,
            Transform[] targets, Transform extra, string title)
        {
            map = data; player = aircraft; plannedSam = sites; recon = targets; optionalTarget = extra; missionTitle = title;
        }
#endif
        private string buildStage = "not started";
        private void Update()
        {
            var keys = Keyboard.current;
            if (Application.isFocused && keys != null && keys.tabKey.wasPressedThisFrame)
            {
                if (IsOpen) Close(); else TryOpen();
                return;
            }
            if (IsOpen && (!HasMap || (Application.isFocused && keys != null && keys.escapeKey.wasPressedThisFrame)))
            { Close(); return; }
            if (IsOpen) FitFrame();
        }
        private void TryOpen()
        {
            if (!HasMap)
            {
                Debug.LogWarning("Tactical map: missing geography or player reference.", this);
                return;
            }
            if (!MissionPause.CanOpenTacticalMap) return;
            try
            {
                BuildCanvas();
            }
            catch (Exception ex)
            {
                Debug.LogError("Tactical map: build failed at " + buildStage + ": " + ex, this);
                DisposeCanvas();
                return;
            }
            if (!MissionPause.TryAcquire(this, MissionPause.Reason.TacticalMap))
                DisposeCanvas();
        }
        private void Close()
        {
            if (!IsOpen) return;
            MissionPause.Release(this);
            DisposeCanvas();
        }
        private void DisposeCanvas()
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
            canvas = null; frame = null; drawParent = null;
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            if (ring != null) Destroy(ring);
            if (arrow != null) Destroy(arrow);
        }

        // Screen-space overlay uses the same uGUI render path as the combat HUD.
        // Construct once on open (positions are a snapshot of the paused world).
        private void BuildCanvas()
        {
            DisposeCanvas();
            routeTrial = GetComponent<Mission02RouteTrial>();
            operationBoundary = ResolveOperationBoundary();
            buildStage = "font and textures";
            EnsureGraphics();
            buildStage = "canvas root";
            var go = new GameObject("Tactical Map Canvas", typeof(RectTransform), typeof(Canvas));
            canvas = go.GetComponent<Canvas>();
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var root = (RectTransform)go.transform;
            drawParent = root;
            var bg = CreateRect("Backdrop", new Rect(0,0,0,0));
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var background = bg.gameObject.AddComponent<RawImage>();
            background.texture = Texture2D.whiteTexture;
            background.color = new Color(.047f,.075f,.085f);
            background.raycastTarget = false;
            frame = CreateRect("Map layout", new Rect(0,0,1120,760));
            frame.anchorMin = frame.anchorMax = new Vector2(.5f,.5f);
            frame.pivot = new Vector2(.5f,.5f);
            frame.anchoredPosition = Vector2.zero;
            screenWidth = screenHeight = -1;
            FitFrame();
            drawParent = frame;
            buildStage = "header and layout";
            Text(new Rect(28,19,780,36), "OPERATION / " + missionTitle, 27, Ink, true);
            Text(new Rect(855,25,237,28), "TACTICAL MAP  /  PAUSED", 15, Cyan, false, TextAnchor.MiddleRight);
            Solid(new Rect(28,66,1064,1), new Color(.25f,.38f,.39f));
            Rect area = new Rect(28,88,660,620);
            buildStage = "geography and markers";
            var panel = CreateRect("Geography and markers", area);
            panel.gameObject.AddComponent<RectMask2D>();
            drawParent = panel;
            DrawGeography(new Rect(0,0,area.width,area.height));
            DrawOperationBoundary(new Rect(0,0,area.width,area.height));
            DrawMap(new Rect(0,0,area.width,area.height));
            drawParent = frame;
            buildStage = "mission data and footer";
            DrawMissionData(new Rect(718,90,374,618));
            Text(new Rect(28,721,660,26), (cameraUp ? "CAMERA UP" : "NORTH UP")+"   /   CRUISE Y"+map.SectionAltitude.ToString("0")+"   /   "+map.WorldBounds.width.ToString("0")+" × "+map.WorldBounds.height.ToString("0"), 14, Muted);
            Text(new Rect(718,721,374,26), "[ TAB ] CLOSE MAP", 18, Cyan, true, TextAnchor.MiddleRight);
        }
        private void FitFrame()
        {
            if (frame == null || (screenWidth == Screen.width && screenHeight == Screen.height)) return;
            screenWidth = Screen.width; screenHeight = Screen.height;
            float scale = Mathf.Min(screenWidth / 1120f, screenHeight / 760f);
            frame.localScale = new Vector3(scale,scale,1f);
        }
        private RectTransform CreateRect(string name, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var t = (RectTransform)go.transform;
            t.SetParent(drawParent,false);
            t.anchorMin = t.anchorMax = new Vector2(0,1);
            t.pivot = new Vector2(0,1);
            t.sizeDelta = rect.size;
            t.anchoredPosition = new Vector2(rect.x,-rect.y);
            return t;
        }
        private RectTransform Texture(Rect r, UnityEngine.Texture texture, Color color)
        {
            var t = CreateRect("Map graphic",r);
            var image = t.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.color = color; image.raycastTarget = false;
            return t;
        }

        // Read the current mission camera, not a hard-coded yaw or aircraft heading.
        private Camera ResolveCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var rig in root.GetComponentsInChildren<ArcadeCameraFollow>())
                {
                    var camera = rig.GetComponent<Camera>();
                    if (rig.isActiveAndEnabled && camera != null && camera.isActiveAndEnabled)
                        return camera;
                }
            return null; // Non-oblique scenes stay north-up, like the radar.
        }
        private Vector2 Display(Vector2 xz) => RadarHud.RelativeOnDisplay(xz,mapRight,mapUp);
        private Vector2 Point(Rect rect, Vector2 uv)
        {
            Vector2 d = Display(Vector2.Scale(uv-new Vector2(.5f,.5f),map.WorldBounds.size))*mapScale;
            return rect.center + new Vector2(d.x,-d.y);
        }
        private Vector2 Point(Rect rect, Transform t) => Point(rect,map.WorldToUV(t.position));
        private static bool Inside(Vector2 uv) => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;

        private void DrawGeography(Rect area)
        {
            Camera camera = ResolveCamera();
            cameraUp = camera != null;
            RadarHud.GetDisplayAxes(camera,out mapRight,out mapUp);
            Vector2 world = map.WorldBounds.size;
            // Fixed-yaw, unrolled camera: fit the entire rotated rectangle,
            // preserving world scale in both axes (including non-square maps).
            float spanX = Mathf.Abs(mapRight.x)*world.x + Mathf.Abs(mapRight.y)*world.y;
            float spanY = Mathf.Abs(mapUp.x)*world.x + Mathf.Abs(mapUp.y)*world.y;
            mapScale = Mathf.Min((area.width-16)/spanX,(area.height-16)/spanY);
            float w=world.x*mapScale, h=world.y*mapScale;
            var panel=drawParent;
            var layer=CreateRect("Rotated static geography and coverage",new Rect(0,0,w,h));
            Vector2 east=Display(Vector2.right);
            Rotate(layer,area.center,Mathf.Atan2(east.y,east.x)*Mathf.Rad2Deg);
            drawParent=layer;
            Solid(new Rect(-2,-2,w+4,h+4),new Color(.31f,.46f,.46f));
            Texture(new Rect(0,0,w,h),map.Geography,Color.white);
            if (plannedSam != null) foreach (var sam in plannedSam)
            {
                if (!sam.Visible || sam.Radius <= 0f) continue;
                Vector2 uv=map.WorldToUV(sam.site.position);
                Vector2 p=new Vector2(uv.x*w,(1-uv.y)*h);
                float r=sam.Radius*mapScale;
                DrawClippedRing(new Rect(p.x-r,p.y-r,r*2,r*2),w,h,Red);
            }
            var mission03 = GetComponent<Mission03Flight>();
            if (mission03 != null) for (int i = 0; i < mission03.FriendlySiteCount; i++)
            {
                var site = mission03.FriendlySiteAt(i);
                float radius = mission03.FriendlyMapRadiusAt(i);
                if (site == null || radius <= 0f) continue;
                Vector2 uv = map.WorldToUV(site.position);
                Vector2 p = new Vector2(uv.x*w,(1-uv.y)*h);
                float r = radius*mapScale;
                DrawClippedRing(new Rect(p.x-r,p.y-r,r*2,r*2),w,h,Cyan);
            }
            drawParent=panel;
        }
        // Crop in original map coordinates before rotating the layer. Unlike
        // a screen-aligned RectMask, this clips to the actual mission boundary.
        private void DrawClippedRing(Rect full,float w,float h,Color tint)
        {
            float x0=Mathf.Max(0,full.xMin), x1=Mathf.Min(w,full.xMax);
            float y0=Mathf.Max(0,full.yMin), y1=Mathf.Min(h,full.yMax);
            if (x1<=x0 || y1<=y0) return;
            var clipped=Rect.MinMaxRect(x0,y0,x1,y1);
            var graphic=Texture(clipped,ring,tint).GetComponent<RawImage>();
            graphic.uvRect=new Rect((x0-full.xMin)/full.width,(full.yMax-y1)/full.height,
                clipped.width/full.width,clipped.height/full.height);
        }

        private MissionBoundaryGuard ResolveOperationBoundary()
        {
            primaryProgress = default;
            if (routeTrial != null) return routeTrial.Boundary;
            var mission03 = GetComponent<Mission03Flight>();
            if (mission03 != null) return mission03.Boundary;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var launch in root.GetComponentsInChildren<Mission01LaunchSequence>())
                    if (launch.isActiveAndEnabled && launch.Boundary != null)
                    {
                        primaryProgress = launch.PrimaryProgress;
                        return launch.Boundary;
                    }
            return null;
        }

        private void DrawOperationBoundary(Rect area)
        {
            if (operationBoundary == null) return;
            Rect outer = operationBoundary.Bounds;
            DrawBoundaryContour(area, outer, 2.5f, RadarHud.BoundaryInk);
        }

        // Clip in world X/Z before projection. If the operation is changed in
        // the Inspector, never fabricate a border at the baked geography edge.
        private void DrawBoundaryContour(Rect area, Rect bounds, float width, Color tint)
        {
            Rect extent = map.WorldBounds;
            Vector2 Project(float x, float z) => Point(area, map.WorldToUV(new Vector2(x,z)));
            void Horizontal(float z)
            {
                float a = Mathf.Max(bounds.xMin, extent.xMin), b = Mathf.Min(bounds.xMax, extent.xMax);
                if (z >= extent.yMin && z <= extent.yMax && b > a)
                    Stroke(Project(a,z), Project(b,z), width, tint);
            }
            void Vertical(float x)
            {
                float a = Mathf.Max(bounds.yMin, extent.yMin), b = Mathf.Min(bounds.yMax, extent.yMax);
                if (x >= extent.xMin && x <= extent.xMax && b > a)
                    Stroke(Project(x,a), Project(x,b), width, tint);
            }
            Horizontal(bounds.yMin); Horizontal(bounds.yMax);
            Vertical(bounds.xMin); Vertical(bounds.xMax);
        }

        private void DrawMap(Rect rect)
        {
            if (routeTrial != null) DrawRouteTrial(rect);
            var mission03 = GetComponent<Mission03Flight>();
            if (mission03 != null && mission03.PowerComplex != null)
            {
                Vector3 center = Vector3.zero;
                int count = 0;
                foreach (var target in mission03.PowerComplex.Targets)
                {
                    if (target == null) continue;
                    center += target.transform.position;
                    count++;
                }
                if (count > 0)
                {
                    Vector2 complexUv = map.WorldToUV(center / count);
                    if (Inside(complexUv))
                    {
                        Vector2 p = Point(rect, complexUv);
                        Diamond(p, 8f, Cyan);
                        Text(new Rect(p.x+12,p.y-10,144,20),"POWER COMPLEX",12,Cyan,true);
                    }
                }
            }
            if (mission03 != null) for (int i = 0; i < mission03.FriendlySiteCount; i++)
            {
                var site = mission03.FriendlySiteAt(i);
                if (site == null || !Inside(map.WorldToUV(site.position))) continue;
                Vector2 p = Point(rect,site);
                Rotate(Texture(new Rect(0,0,14,14),arrow,Cyan),p,0f);
                Text(new Rect(p.x+10,p.y-10,100,20),$"SAM {i + 2:00}",12,Cyan,true);
            }
            if (map.HasAirfield && Inside(map.WorldToUV(map.AirfieldPosition)))
            {
                Vector2 p = Point(rect,map.WorldToUV(map.AirfieldPosition));
                Vector2 f = Display(map.AirfieldForward).normalized;
                Stroke(p-new Vector2(f.x,-f.y)*12,p+new Vector2(f.x,-f.y)*12,5,Cyan);
                Text(new Rect(p.x+12,p.y-10,96,20),"AIRFIELD",12,Ink,true);
            }
            if (plannedSam != null) foreach (var sam in plannedSam)
            {
                if (!sam.Visible || !Inside(map.WorldToUV(sam.site.position))) continue;
                Vector2 p=Point(rect,sam.site);
                Solid(new Rect(p.x-4,p.y-4,8,8),Red);
                Text(new Rect(p.x+7,p.y-10,54,20),sam.label,12,Ink,true);
            }
            if (recon != null) for (int i=0;i<recon.Length;i++)
                if (recon[i]!=null && Inside(map.WorldToUV(recon[i].position)))
                {
                    var target = recon[i].GetComponent<IObjectiveTarget>();
                    if (target == null || ObjectiveStatusText.IsActive(target.ObjectiveState)) Diamond(Point(rect,recon[i]),5,Ink);
                }
            if (optionalTarget != null && Inside(map.WorldToUV(optionalTarget.position)))
            {
                var target = optionalTarget.GetComponent<IObjectiveTarget>();
                if (target == null || ObjectiveStatusText.IsActive(target.ObjectiveState)) Diamond(Point(rect,optionalTarget),7,Gold);
            }
            if (GetComponent<Mission03Flight>() != null) DrawAirContacts(rect);
            Vector2 uv=map.WorldToUV(player.position);
            Vector2 pos=Point(rect,new Vector2(Mathf.Clamp(uv.x,.025f,.975f),Mathf.Clamp(uv.y,.025f,.975f)));
            Vector3 heading=player.forward;
            Vector2 projected=Display(new Vector2(heading.x,heading.z));
            float angle=Mathf.Atan2(projected.x,projected.y)*Mathf.Rad2Deg;
            Rotate(Texture(new Rect(pos.x-11,pos.y-11,22,22),arrow,Cyan),pos,-angle);
            Text(new Rect(pos.x+13,pos.y-11,96,22),Inside(uv)?"YOU":"OUTSIDE",12,Cyan,true);
            Vector2 north=Display(Vector2.up);
            Vector2 compass=new Vector2(rect.width-35,27);
            Rotate(Texture(new Rect(0,0,19,19),arrow,Ink),compass,
                -Mathf.Atan2(north.x,north.y)*Mathf.Rad2Deg);
            Text(new Rect(compass.x-10,compass.y+12,20,20),"N",14,Ink,true,TextAnchor.MiddleCenter);
        }

        // Snapshot the registered airborne contacts on each map open. Radar.Sample
        // includes aircraft outside sensor range; no new detection rules are needed.
        private void DrawAirContacts(Rect rect)
        {
            var radar = player.GetComponent<AircraftRadar>();
            if (radar == null) return;
            radar.Sample(airContacts);
            foreach (var contact in airContacts)
            {
                if (contact.Kind != RadarContactType.Aircraft) continue;
                Vector2 uv = map.WorldToUV(contact.WorldPosition);
                if (!Inside(uv)) continue;
                Vector2 p = Point(rect, uv);
                Vector2 heading = Display(contact.Heading);
                if (heading.sqrMagnitude < .0001f) { Diamond(p,4f,Red); continue; }
                float angle = Mathf.Atan2(heading.x,heading.y)*Mathf.Rad2Deg;
                Rotate(Texture(new Rect(0,0,11,11),arrow,Red),p,-angle);
            }
        }

        private void DrawRouteTrial(Rect rect)
        {
            // Use the authored world-space guide geometry, even if G hides it in flight.
            if (routeTrial.RouteGuide != null)
                foreach (var line in routeTrial.RouteGuide.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (line.loop || line.positionCount < 2) continue;
                    Color tint = line.name.StartsWith("A to B") ? Red :
                        (line.name == "S to C" || line.name == "C1 to C2") ? Gold : Cyan;
                    for (int i = 1; i < line.positionCount; i++)
                    {
                        Vector3 a = line.GetPosition(i-1), b = line.GetPosition(i);
                        if (!line.useWorldSpace) { a = line.transform.TransformPoint(a); b = line.transform.TransformPoint(b); }
                        Vector2 u = map.WorldToUV(a), v = map.WorldToUV(b);
                        if (Inside(u) && Inside(v)) Stroke(Point(rect,u), Point(rect,v), 2f, tint);
                    }
                }
            Mark(routeTrial.PointA, "A", Red);
            Mark(routeTrial.PointB, "B", Red);
            Mark(routeTrial.PointC, routeTrial.HasSecondPoint ? "C1" : "C", Gold);
            if (routeTrial.HasSecondPoint) Mark(routeTrial.PointC2, "C2", Gold);
            for (int i = 0; i < ConvoyMarchPreview.Count; i++)
                if (routeTrial.TryGetPicketPosition(i, out Vector3 world))
                {
                    Vector2 uv = map.WorldToUV(world);
                    if (!Inside(uv)) continue;
                    Vector2 p = Point(rect, uv);
                    Solid(new Rect(p.x-3,p.y-3,6,6), new Color(.99f,.26f,.18f));
                }
            for (int i = 0; i < 2; i++)
                if (routeTrial.TryGetWardenPosition(i, out Vector3 position))
                {
                    Vector2 uv = map.WorldToUV(position);
                    if (Inside(uv)) Diamond(Point(rect, uv), 5f, new Color(1f,.64f,.35f));
                }
            void Mark(Vector3 world, string name, Color tint)
            {
                Vector2 uv = map.WorldToUV(world);
                if (!Inside(uv)) return;
                Vector2 p = Point(rect, uv);
                Diamond(p, 8f, tint);
                Text(new Rect(p.x+10,p.y-12,32,24),name,18,Ink,true);
            }
        }

        private void DrawRouteData(Rect rect)
        {
            Solid(rect,new Color(.075f,.115f,.13f));
            float x=rect.x+20, w=rect.width-40, y=rect.y+15;
            Text(new Rect(x,y,w,30),"ROUTE TRIAL / M02",20,Ink,true);
            Text(new Rect(x,y+40,w,25),"A — Convoy entry",18,Red,true);
            Text(new Rect(x,y+73,w,25),"B — Supply depot",18,Red,true);
            Text(new Rect(x,y+106,w,25),routeTrial.HasSecondPoint ? "C1 + C2 — One optional task" : "C — Optional objective",18,Gold,true);
            Solid(new Rect(x,y+148,w,1),new Color(.23f,.34f,.35f));
            Legend(x,y+172,Cyan,"Direct / return route and player");
            Legend(x,y+207,Gold,"Optional canyon route");
            Legend(x,y+242,Red,"Convoy route / live Pickets");
            Legend(x,y+277,new Color(.28f,.26f,.22f),"Terrain obstruction");
            Legend(x,y+312,RadarHud.BoundaryInk,"Operation boundary");
            Text(new Rect(x,y+355,w,22),"DEPLOYMENT TRIAL / SAM FIRE OFF",14,Gold,true);
            Text(new Rect(x,y+378,w,22),routeTrial.ConvoyStatus,12,Ink);
            Text(new Rect(x,y+405,w,22),routeTrial.DeploymentStatus,12,Ink);
            Text(new Rect(x,y+432,w,22),routeTrial.EscortStatus,14,Ink);
            Text(new Rect(x,y+460,w,25),"Map pauses flight and timing.",14,Muted);
        }

        private void DrawMissionData(Rect rect)
        {
            if (routeTrial != null) { DrawRouteData(rect); return; }
            if (GetComponent<Mission03Flight>() != null)
            {
                Solid(rect,new Color(.075f,.115f,.13f));
                float left=rect.x+20, top=rect.y+15, width=rect.width-40;
                Text(new Rect(left,top,width,30),"M03 / FLIGHT FOUNDATION",20,Ink,true);
                Text(new Rect(left,top+48,width,25),"Primary: protect the complex",18,Cyan,true);
                Text(new Rect(left,top+82,width,25),"Secondary: both interceptors",18,Gold,true);
                Text(new Rect(left,top+120,width,24),"Objectives not installed yet.",14,Muted);
                Solid(new Rect(left,top+160,width,1),new Color(.23f,.34f,.35f));
                Legend(left,top+186,Cyan,"Player / friendly airfield");
                Legend(left,top+221,RadarHud.BoundaryInk,"Operation boundary");
                Legend(left,top+256,new Color(.28f,.26f,.22f),"Terrain obstruction");
                Legend(left,top+291,Red,"Airborne hostile contacts");
                Legend(left,top+326,Cyan,"Friendly SAM / decorative zone");
                Legend(left,top+361,Cyan,"Protected power complex");
                Text(new Rect(left,top+416,width,25),"Map pauses flight and timing.",14,Muted);
                return;
            }
            Solid(rect,new Color(.075f,.115f,.13f));
            float x=rect.x+20, w=rect.width-40, y=rect.y+15;
            Text(new Rect(x,y,w,30),"MISSION DATA",20,Ink,true);
            Text(new Rect(x,y+36,w,24),"PRIMARY OBJECTIVE",15,Cyan,true);
            Text(new Rect(x,y+66,w,24),"Destroy reconnaissance units",18,Ink);
            bool reconLive = primaryProgress.IsReady;
            Text(new Rect(x,y+94,w,22),reconLive ? $"Destroyed: {primaryProgress.Destroyed} / {primaryProgress.Total}" : "Planned targets: "+(recon==null?0:recon.Length),14,Muted);
            Solid(new Rect(x,y+126,w,1),new Color(.23f,.34f,.35f));
            Text(new Rect(x,y+142,w,24),"SECONDARY OBJECTIVE",15,Gold,true);
            var optional = optionalTarget != null ? optionalTarget.GetComponent<IObjectiveTarget>() : null;
            Text(new Rect(x,y+171,w,24),optional != null ? "Destroy Overlord" : "Destroy optional target",18,Ink);
            Text(new Rect(x,y+198,w,22),optional != null ? ObjectiveStatusText.For(optional.ObjectiveState) : "Optional / planned",14,Muted);
            Solid(new Rect(x,y+230,w,1),new Color(.23f,.34f,.35f));
            Text(new Rect(x,y+244,w,26),"LEGEND",16,Ink,true);
            Legend(x,y+282,new Color(.28f,.26f,.22f),"Terrain obstruction");
            bool linked = plannedSam != null && plannedSam.Length > 0 &&
                Array.TrueForAll(plannedSam, sam => sam.Launcher != null);
            Legend(x,y+315,Red,linked ? "SAM coverage" : "Planned SAM coverage");
            Legend(x,y+348,Ink,"Recon target marker");
            Legend(x,y+381,Gold,"Optional target marker");
            Legend(x,y+414,Cyan,"Player / airfield");
            float footer = 454f;
            if (operationBoundary != null)
            {
                Legend(x,y+447,RadarHud.BoundaryInk,"Operation boundary");
                footer = 487f;
            }
            Solid(new Rect(x,y+footer,w,1),new Color(.23f,.34f,.35f));
            bool armed = linked && Array.TrueForAll(plannedSam, sam => sam.Launcher.IsConfigured);
            Text(new Rect(x,y+footer+15,w,25),linked ? "MISSION PROTOTYPE" : "PLANNING DATA / NOT LIVE",14,Gold,true);
            Text(new Rect(x,y+footer+46,w,28),armed ? (reconLive ? (optional != null ? "SAMs and objectives live." : "SAMs and recon live; optional pending.") : "SAMs live; objectives pending.") : linked ? "SAM sites linked; combat pending." : "SAMs and objectives are placeholders.",14,Muted);
        }
        private void Legend(float x,float y,Color c,string text)
        { Solid(new Rect(x,y+5,15,15),c); Text(new Rect(x+25,y,300,25),text,15,Ink); }
        private void Text(Rect r,string value,int size,Color c,bool bold=false,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var t = CreateRect("Label: " + value,r).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = alignment; t.color = c; t.text = value;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }
        private void Solid(Rect r,Color c) => Texture(r,Texture2D.whiteTexture,c);
        private static void Rotate(RectTransform t,Vector2 center,float angle)
        {
            t.pivot = new Vector2(.5f,.5f);
            t.anchoredPosition = new Vector2(center.x,-center.y);
            t.localRotation = Quaternion.Euler(0,0,angle);
        }
        private void Stroke(Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 d=b-a, mid=(a+b)*.5f;
            Rotate(Texture(new Rect(0,0,d.magnitude,width),Texture2D.whiteTexture,c),mid,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
        }
        private void Diamond(Vector2 p,float size,Color c)
        {
            Rotate(Texture(new Rect(0,0,size*1.4f,size*1.4f),Texture2D.whiteTexture,c),p,45);
        }
        private void EnsureGraphics()
        {
            if (font==null) font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font==null) throw new InvalidOperationException("Tactical map font unavailable.");
            if (ring==null) ring=Mask(256,(x,y)=>{float r=Mathf.Sqrt(x*x+y*y); return r>.995f?0f:r>.97f?.85f:.19f;});
            if (arrow==null) arrow=Mask(64,(x,y)=> y>-.75f && y<.95f && Mathf.Abs(x)<(.95f-y)*.54f && y>-.28f-Mathf.Abs(x)*.62f?1f:0f);
        }
        private static Texture2D Mask(int size,Func<float,float,float> alpha)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.hideFlags=HideFlags.HideAndDontSave; texture.wrapMode=TextureWrapMode.Clamp; texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*alpha((x+.5f)/size*2-1,(y+.5f)/size*2-1)));
            texture.SetPixels32(pixels); texture.Apply(false,true); return texture;
        }
    }
}
