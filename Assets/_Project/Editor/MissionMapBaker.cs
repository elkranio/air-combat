using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // MAP-01: explicit static bake. No scene edits, runtime rendering or
    // hard-coded mission coordinates. Active marked rock solids only.
    internal sealed class MissionMapBaker : EditorWindow
    {
        private const string OutputFolder = "Assets/_Project/Generated/MissionMaps";
        private const int Resolution = 1024;
        private static readonly Color32 Sand = new Color32(153, 139, 115, 255);
        private static readonly Color32 Grid = new Color32(142, 129, 108, 255);
        private static readonly Color32 Meadow = new Color32(112, 132, 98, 255);
        private static readonly Color32 MeadowGrid = new Color32(100, 120, 89, 255);
        private static readonly Color32 Stone = new Color32(78, 72, 61, 255);
        private static readonly Color32 Contour = new Color32(193, 178, 147, 255);
        [SerializeField] private Renderer ground;
        [SerializeField] private Transform airfield;
        [SerializeField] private float altitude = 78f;
        [SerializeField] private string boundScene;
        private MissionMapData data;
        private string status;

        [MenuItem("Air Combat/Missions/Bake Mission Map")]
        private static void Open()
        {
            var window = GetWindow<MissionMapBaker>("Mission Map Baker");
            window.minSize = new Vector2(430, 510);
            window.BindScene(true);
            window.Show();
        }

        private void OnEnable() => BindScene(false);
        private void OnFocus() => BindScene(false);

        private void BindScene(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)) return;
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (!force && boundScene == guid) return;
            boundScene = guid; status = null;
            data = AssetDatabase.LoadAssetAtPath<MissionMapData>(AssetPath(scene));
            if (!MissionAutoMap.TryResolve(scene, out ground, out airfield, out altitude))
                altitude = data != null ? data.SectionAltitude : 78f;
            // The manual window still permits explicit ground/runway selection.
        }

        private static string AssetPath(Scene scene)
        {
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            return $"{OutputFolder}/{scene.name}_{guid.Substring(0, 8)}.asset";
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Exit Play Mode before baking.", MessageType.Info); return; }
            BindScene(false);
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            { EditorGUILayout.HelpBox("Save and open a mission scene first.", MessageType.Info); return; }
            EditorGUILayout.LabelField("STATIC GEOGRAPHY / NORTH UP", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene", scene.name);
            ground = (Renderer)EditorGUILayout.ObjectField("Ground bounds", ground, typeof(Renderer), true);
            airfield = (Transform)EditorGUILayout.ObjectField("Airfield / runway", airfield, typeof(Transform), true);
            altitude = EditorGUILayout.FloatField("Cruise section Y", altitude);
            using (new EditorGUI.DisabledScope(ground == null || !scene.isLoaded || string.IsNullOrEmpty(scene.path)))
                if (GUILayout.Button("Bake / refresh static map", GUILayout.Height(28)))
                {
                    try { Bake(scene); status = "Baked. No scene objects changed."; }
                    catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
                }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            if (data == null || data.Geography == null) return;
            EditorGUILayout.LabelField($"{data.WorldBounds.width:0} × {data.WorldBounds.height:0}  |  Y{data.SectionAltitude:0.##}  |  {data.ObstacleCount} solids");
            EditorGUILayout.LabelField($"{data.SectionTriangleCount} union triangles  |  {data.Geography.width} × {data.Geography.height} px");
            if (GUILayout.Button("Select baked map asset")) Selection.activeObject = data;
            float availableW = Mathf.Max(1, position.width - 24);
            float availableH = Mathf.Max(1, position.height - 240);
            float ratio = data.WorldBounds.width / data.WorldBounds.height;
            float w = Mathf.Min(availableW, availableH * ratio), h = w / ratio;
            var slot = GUILayoutUtility.GetRect(availableW, h);
            var mapRect = new Rect(slot.center.x - w * .5f, slot.y, w, h);
            GUI.DrawTexture(mapRect, data.Geography, ScaleMode.StretchToFill, false);
            GUI.BeginGroup(mapRect);
            if (data.HasAirfield)
            {
                Vector2 uv = data.WorldToUV(data.AirfieldPosition);
                if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
                {
                    var p = new Vector2(uv.x * w, (1 - uv.y) * h);
                    var f = data.AirfieldForward;
                    var d = new Vector2(f.x, -f.y).normalized;
                    if (d.sqrMagnitude < .01f) d = Vector2.up;
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(.83f, .96f, .95f) } };
                    Matrix4x4 old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, p);
                    EditorGUI.DrawRect(new Rect(p.x - 10, p.y - 2, 20, 4), new Color(.78f, .95f, .95f));
                    GUI.matrix = old;
                    GUI.Label(new Rect(p.x + 9, p.y - 9, 85, 18), "AIRFIELD", style);
                }
            }
            GUI.Label(new Rect(w - 35, 4, 31, 21), "N ↑", EditorStyles.whiteBoldLabel);
            GUI.EndGroup();
            EditorGUILayout.LabelField("Rock silhouettes = blocked at cruise altitude. No tactical overlays yet.", EditorStyles.wordWrappedMiniLabel);
        }

        // Explicit authoring entry point; shares the same raster/asset pipeline as the window.
        internal static MissionMapData BakeForRouteTrial(Scene scene, Renderer surface, Transform runway, float sectionAltitude = 78f)
        {
            var baker = CreateInstance<MissionMapBaker>();
            try
            {
                baker.ground = surface; baker.airfield = runway; baker.altitude = sectionAltitude;
                baker.Bake(scene);
                return baker.data;
            }
            finally { DestroyImmediate(baker); }
        }

        private void Bake(Scene scene)
        {
            if (ground.gameObject.scene != scene || (airfield != null && airfield.gameObject.scene != scene))
                throw new InvalidOperationException("Select references from the active scene.");
            var mission03 = scene.GetRootGameObjects().SelectMany(g =>
                g.GetComponentsInChildren<Mission03Flight>(true)).SingleOrDefault();
            if (mission03 != null) altitude = mission03.CruiseAltitude;
            if (float.IsNaN(altitude) || float.IsInfinity(altitude)) throw new InvalidOperationException("Invalid altitude.");
            var b = ground.bounds;
            var world = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            // For campaign scenes the operation rectangle, not the original
            // ground mesh or the decorative surround, defines the map extent.
            var launch = scene.GetRootGameObjects().SelectMany(g =>
                g.GetComponentsInChildren<Mission01LaunchSequence>(true)).SingleOrDefault();
            if (launch != null) world = launch.OperationBounds;
            var routeTrial = scene.GetRootGameObjects().SelectMany(g =>
                g.GetComponentsInChildren<Mission02RouteTrial>(true)).SingleOrDefault();
            if (routeTrial != null) world = routeTrial.OperationBounds;
            if (mission03 != null) world = mission03.OperationBounds;
            if (world.width <= .01f || world.height <= .01f) throw new InvalidOperationException("Map has no X/Z extent.");
            if (!TerrainSectionUnion.TryBakeForAuthoring(scene, altitude, out var edges, out var fill, out int count))
                throw new InvalidOperationException("Terrain union failed. Check readable closed rock meshes; previous bake retained.");
            if (count > 0 && fill.Length == 0)
                Debug.LogWarning("Map bake: no marked solids cross the requested altitude.");
            int width = Mathf.Max(1, Mathf.RoundToInt(Resolution * world.width / Mathf.Max(world.width, world.height)));
            int height = Mathf.Max(1, Mathf.RoundToInt(Resolution * world.height / Mathf.Max(world.width, world.height)));
            var pixels = Raster(world, width, height, edges, fill, mission03 != null);
            EnsureFolder(OutputFolder);
            string path = AssetPath(scene);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is MissionMapData)) throw new InvalidOperationException("Map output path occupied.");
            var target = existing as MissionMapData;
            if (target == null)
            {
                target = CreateInstance<MissionMapData>();
                AssetDatabase.CreateAsset(target, path);
            }
            var texture = target.Geography;
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = "Static Geography";
                AssetDatabase.AddObjectToAsset(texture, target);
            }
            else if ((texture.width != width || texture.height != height) && !texture.Reinitialize(width, height, TextureFormat.RGBA32, false))
                throw new InvalidOperationException("Cannot resize map texture.");
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels); texture.Apply(false, false);
            Vector3 a = airfield != null ? airfield.position : Vector3.zero;
            Vector3 f = airfield != null ? airfield.forward : Vector3.forward;
            target.SetBake(AssetDatabase.AssetPathToGUID(scene.path), world, altitude, texture, count, fill.Length / 3,
                airfield != null, new Vector2(a.x, a.z), new Vector2(f.x, f.z).normalized);
            EditorUtility.SetDirty(texture); EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
            data = target;
            Debug.Log($"Mission map baked: {count} marked solids, {fill.Length / 3} triangles, {width}x{height}. {path}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // Texture coordinates have bottom-left origin. Pixel-center coverage;
        // scanline fill of the shared union triangles, clipped to map extent.
        private static Color32[] Raster(Rect world, int w, int h, Vector2[] edges, Vector2[] fill, bool green)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float gx = world.xMin + (x + .5f) * world.width / w;
                    float gz = world.yMin + (y + .5f) * world.height / h;
                    bool line = Mathf.Abs(gx / 400f - Mathf.Round(gx / 400f)) < world.width / w / 800f ||
                                Mathf.Abs(gz / 400f - Mathf.Round(gz / 400f)) < world.height / h / 800f;
                    pixels[y * w + x] = green ? (line ? MeadowGrid : Meadow) : (line ? Grid : Sand);
                }
            Vector2 Pixel(Vector2 v) => Vector2.Scale(MissionMapData.ToUV(world, v), new Vector2(w, h));
            for (int i = 0; i < fill.Length; i += 3)
                FillTriangle(pixels, w, h, Pixel(fill[i]), Pixel(fill[i + 1]), Pixel(fill[i + 2]));
            for (int i = 0; i < edges.Length; i += 2)
                DrawLine(pixels, w, h, Pixel(edges[i]), Pixel(edges[i + 1]));
            return pixels;
        }

        private static void FillTriangle(Color32[] pixels, int w, int h, Vector2 a, Vector2 b, Vector2 c)
        {
            if (Mathf.Abs(Cross(b - a, c - a)) < 1e-7f) return;
            int y0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - .5f));
            int y1 = Mathf.Min(h - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - .5f));
            for (int y = y0; y <= y1; y++)
            {
                float scan = y + .5f, lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                Span(a, b); Span(b, c); Span(c, a);
                if (lo > hi) continue;
                int x0 = Mathf.Max(0, Mathf.CeilToInt(lo - .5f));
                int x1 = Mathf.Min(w - 1, Mathf.FloorToInt(hi - .5f));
                for (int x = x0; x <= x1; x++) pixels[y * w + x] = Stone;
                void Span(Vector2 p, Vector2 q)
                {
                    if (p.y > q.y) { var t = p; p = q; q = t; }
                    if (p.y == q.y || scan < p.y || scan > q.y) return;
                    float x = Mathf.LerpUnclamped(p.x, q.x, (scan - p.y) / (q.y - p.y));
                    lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x);
                }
            }
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // Liang-Barsky segment clipping. The southern extension must not draw
        // outside the image or introduce an artificial cap along the map edge.
        private static void DrawLine(Color32[] pixels, int w, int h, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float lo = 0, hi = 1;
            if (!Clip(-d.x, a.x, ref lo, ref hi) || !Clip(d.x, w - a.x, ref lo, ref hi) ||
                !Clip(-d.y, a.y, ref lo, ref hi) || !Clip(d.y, h - a.y, ref lo, ref hi)) return;
            Vector2 p = a + lo * d, q = a + hi * d;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(q.x - p.x), Mathf.Abs(q.y - p.y))));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 v = Vector2.LerpUnclamped(p, q, i / (float)steps);
                int x = Mathf.Clamp(Mathf.FloorToInt(v.x), 0, w - 1), y = Mathf.Clamp(Mathf.FloorToInt(v.y), 0, h - 1);
                pixels[y * w + x] = Contour;
            }
        }
        private static bool Clip(float p, float q, ref float lo, ref float hi)
        {
            if (Mathf.Abs(p) < 1e-7f) return q >= 0;
            float t = q / p;
            if (p < 0) lo = Mathf.Max(lo, t); else hi = Mathf.Min(hi, t);
            return lo <= hi;
        }
    }
}
