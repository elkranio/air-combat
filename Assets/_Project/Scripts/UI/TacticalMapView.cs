using System;
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
            [Min(0f)] public float radius;
            public string label;
        }
        [SerializeField] private MissionMapData map;
        [SerializeField] private Transform player;
        [SerializeField] private string missionTitle = "FIRST SORTIE";
        [SerializeField] private PlannedSam[] plannedSam = Array.Empty<PlannedSam>();
        [SerializeField] private Transform[] recon = Array.Empty<Transform>();
        [SerializeField] private Transform optionalTarget;
        private static TacticalMapView active;
        private float previousTimeScale;
        private bool previousAudioPause;
        private Texture2D ring, arrow;
        private Canvas canvas;
        private RectTransform frame, drawParent;
        private Font font;
        private int screenWidth, screenHeight;
        private static readonly Color Ink = new Color(.80f,.89f,.87f);
        private static readonly Color Muted = new Color(.53f,.66f,.65f);
        private static readonly Color Cyan = new Color(.48f,.92f,.94f);
        private static readonly Color Red = new Color(.86f,.36f,.30f);
        private static readonly Color Gold = new Color(.97f,.74f,.36f);

        public static bool IsOpenFor(Scene scene) => active != null && active.gameObject.scene == scene;
        private bool IsOpen => active == this;
        private bool HasMap => map != null && map.Geography != null && map.WorldBounds.width > 0 && map.WorldBounds.height > 0 && player != null;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(MissionMapData data, Transform aircraft, PlannedSam[] sites,
            Transform[] targets, Transform extra, string title)
        {
            map = data; player = aircraft; plannedSam = sites; recon = targets; optionalTarget = extra; missionTitle = title;
        }
#endif
        private void Update()
        {
            var keys = Keyboard.current;
            if (!Application.isFocused || keys == null) return;
            if (IsOpen && (!HasMap || keys.tabKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
            { Close(); return; }
            if (keys.tabKey.wasPressedThisFrame && HasMap && active == null && Time.timeScale > 0f)
            {
                try { BuildCanvas(); }
                catch (Exception ex)
                {
                    DisposeCanvas();
                    Debug.LogException(ex, this);
                    return;
                }
                previousTimeScale = Time.timeScale;
                previousAudioPause = AudioListener.pause;
                active = this;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            if (IsOpen) FitFrame();
        }
        private void Close()
        {
            if (!IsOpen) return;
            active = null;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
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
            EnsureGraphics();
            var go = new GameObject("Tactical Map Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            canvas = go.GetComponent<Canvas>();
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
            Text(new Rect(28,19,780,36), "OPERATION / " + missionTitle, 27, Ink, true);
            Text(new Rect(855,25,237,28), "TACTICAL MAP  /  PAUSED", 15, Cyan, false, TextAnchor.MiddleRight);
            Solid(new Rect(28,66,1064,1), new Color(.25f,.38f,.39f));
            Rect area = new Rect(28,88,660,620);
            float ratio = map.WorldBounds.width/map.WorldBounds.height;
            float w = Mathf.Min(area.width,area.height*ratio), h = w/ratio;
            Rect panel = new Rect(area.center.x-w*.5f,area.center.y-h*.5f,w,h);
            Solid(new Rect(panel.x-2,panel.y-2,panel.width+4,panel.height+4), new Color(.31f,.46f,.46f));
            drawParent = CreateRect("Geography and markers", panel);
            drawParent.gameObject.AddComponent<RectMask2D>();
            Texture(new Rect(0,0,w,h),map.Geography,Color.white);
            DrawMap(new Rect(0,0,w,h));
            drawParent = frame;
            DrawMissionData(new Rect(718,90,374,618));
            Text(new Rect(28,721,660,26), "NORTH UP   /   CRUISE Y"+map.SectionAltitude.ToString("0")+"   /   "+map.WorldBounds.width.ToString("0")+" × "+map.WorldBounds.height.ToString("0"), 14, Muted);
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

        private Vector2 Point(Rect rect, Vector2 uv) => new Vector2(rect.x+uv.x*rect.width,rect.y+(1-uv.y)*rect.height);
        private Vector2 Point(Rect rect, Transform t) => Point(rect,map.WorldToUV(t.position));
        private static bool Inside(Vector2 uv) => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        private void DrawMap(Rect rect)
        {
            if (plannedSam != null) foreach (var sam in plannedSam)
            {
                if (sam.site == null || sam.radius <= 0f) continue;
                Vector2 p = Point(rect,sam.site);
                float rx = sam.radius/map.WorldBounds.width*rect.width;
                float ry = sam.radius/map.WorldBounds.height*rect.height;
                Texture(new Rect(p.x-rx,p.y-ry,rx*2,ry*2),ring,Red);
            }
            if (map.HasAirfield && Inside(map.WorldToUV(map.AirfieldPosition)))
            {
                Vector2 p = Point(rect,map.WorldToUV(map.AirfieldPosition));
                Vector2 f = map.AirfieldForward.normalized;
                Stroke(p-new Vector2(f.x,-f.y)*12,p+new Vector2(f.x,-f.y)*12,5,Cyan);
                Text(new Rect(p.x+12,p.y-10,96,20),"AIRFIELD",12,Ink,true);
            }
            if (plannedSam != null) foreach (var sam in plannedSam)
            {
                if (sam.site == null || !Inside(map.WorldToUV(sam.site.position))) continue;
                Vector2 p=Point(rect,sam.site);
                Solid(new Rect(p.x-4,p.y-4,8,8),Red);
                Text(new Rect(p.x+7,p.y-10,54,20),sam.label,12,Ink,true);
            }
            if (recon != null) for (int i=0;i<recon.Length;i++)
                if (recon[i]!=null && Inside(map.WorldToUV(recon[i].position))) Diamond(Point(rect,recon[i]),5,Ink);
            if (optionalTarget != null && Inside(map.WorldToUV(optionalTarget.position))) Diamond(Point(rect,optionalTarget),7,Gold);
            Vector2 uv=map.WorldToUV(player.position);
            Vector2 pos=Point(rect,new Vector2(Mathf.Clamp(uv.x,.025f,.975f),Mathf.Clamp(uv.y,.025f,.975f)));
            Vector3 heading=player.forward;
            float angle=Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg;
            Rotate(Texture(new Rect(pos.x-11,pos.y-11,22,22),arrow,Cyan),pos,-angle);
            Text(new Rect(pos.x+13,pos.y-11,96,22),Inside(uv)?"YOU":"OUTSIDE",12,Cyan,true);
            Text(new Rect(rect.width-47,7,38,25),"N ↑",18,Ink,true);
        }
        private void DrawMissionData(Rect rect)
        {
            Solid(rect,new Color(.075f,.115f,.13f));
            float x=rect.x+20, w=rect.width-40, y=rect.y+15;
            Text(new Rect(x,y,w,30),"MISSION DATA",20,Ink,true);
            Text(new Rect(x,y+36,w,24),"PRIMARY OBJECTIVE",15,Cyan,true);
            Text(new Rect(x,y+66,w,24),"Destroy reconnaissance units",18,Ink);
            Text(new Rect(x,y+94,w,22),"Planned targets: "+(recon==null?0:recon.Length),14,Muted);
            Solid(new Rect(x,y+126,w,1),new Color(.23f,.34f,.35f));
            Text(new Rect(x,y+142,w,24),"SECONDARY OBJECTIVE",15,Gold,true);
            Text(new Rect(x,y+171,w,24),"Destroy optional target",18,Ink);
            Text(new Rect(x,y+198,w,22),"Optional / planned",14,Muted);
            Solid(new Rect(x,y+230,w,1),new Color(.23f,.34f,.35f));
            Text(new Rect(x,y+244,w,26),"LEGEND",16,Ink,true);
            Legend(x,y+282,new Color(.28f,.26f,.22f),"Terrain obstruction");
            Legend(x,y+315,Red,"Planned SAM coverage");
            Legend(x,y+348,Ink,"Recon target marker");
            Legend(x,y+381,Gold,"Optional target marker");
            Legend(x,y+414,Cyan,"Player / airfield");
            Solid(new Rect(x,y+454,w,1),new Color(.23f,.34f,.35f));
            Text(new Rect(x,y+469,w,25),"PLANNING DATA / NOT LIVE",14,Gold,true);
            Text(new Rect(x,y+500,w,28),"SAMs and objectives are placeholders.",14,Muted);
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
