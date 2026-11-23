using System;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// V2A: an isolated, reproducible ground/composition experiment.
// Copies the accepted V17 flight/camera setup, never edits the source scene.
public static class BuildEnvironmentV2Ground
{
    private const string Source = "Assets/_Project/Scenes/FrontierStrata.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierV2Ground.unity";
    private const string Output = "Assets/_Project/Generated/EnvironmentV2";
    private const string PipelinePath = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int Cells = 240;
    private const int TextureSize = 2048;
    private const float Half = 600f;
    private const float Spacing = Half * 2f / Cells;

    [MenuItem("Air Combat/Art V2/V2A Create Ground Study")]
    private static void CreateFromMenu()
    {
        try { Debug.Log(Create()); }
        catch (Exception e) { Debug.LogError("V2A: " + e); }
    }

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before creating the study.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save all open scenes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
            throw new InvalidOperationException("Missing V17 source scene: " + Source);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null)
            throw new InvalidOperationException("V2A scene already exists. Do not overwrite an authored study.");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null) throw new InvalidOperationException("Missing ArtLab URP asset.");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
        EnsureFolder(Output);

        // SaveScene(..., saveAsCopy:true) preserves V17 byte-for-byte.
        var sourceScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(sourceScene, Destination, true))
            throw new InvalidOperationException("Failed to copy FrontierStrata.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var cameraOwner = scene.GetRootGameObjects().Single(g => g.name == "Main Camera");
        var lightOwner = scene.GetRootGameObjects().Single(g => g.name == "Directional Light");
        var camera = cameraOwner.GetComponent<Camera>();
        var flight = player.GetComponent<FlightController>();
        var boost = player.GetComponent<Afterburner>();
        var sun = lightOwner.GetComponent<Light>();
        if (camera == null || flight == null || boost == null || sun == null)
            throw new InvalidOperationException("Unexpected V17 flight/light setup. Inspect source instead of guessing.");

        // Old kit, roads, ground and variant scripts belong to the V17 art root.
        // They are deleted ONLY from this freshly copied, isolated study.
        foreach (var root in scene.GetRootGameObjects())
            if (root != player && root != cameraOwner && root != lightOwner)
                Object.DestroyImmediate(root);

        var terrainMesh = BuildMesh();
        string meshPath = Output + "/V2A_Terrain.asset";
        AssetDatabase.CreateAsset(terrainMesh, meshPath);
        var texture = BuildAlbedo(terrainMesh.vertices);
        var ground = new Material(shader) { name = "V2A dry frontier / ground" };
        ground.SetColor("_BaseColor", Color.white);
        ground.SetFloat("_Metallic", 0f);
        ground.SetFloat("_Smoothness", .045f);
        ground.SetTexture("_BaseMap", texture);
        string materialPath = Output + "/V2A_Ground.mat";
        AssetDatabase.CreateAsset(ground, materialPath);

        var rootObject = new GameObject("Art V2 / Ground study (isolated)");
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        var terrain = new GameObject("V2A terrain - 1200 x 1200");
        terrain.transform.SetParent(rootObject.transform, false);
        terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
        var renderer = terrain.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ground;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        terrain.AddComponent<MeshCollider>().sharedMesh = terrainMesh;

        // Keep the V17 real flight controls and camera. Only reposition to a
        // broad open basin; the camera offset/angle is preserved for honest A/B.
        Vector3 cameraOffset = camera.transform.position - player.transform.position;
        player.transform.position = new Vector3(-200f, 48f, -230f);
        camera.transform.position = player.transform.position + cameraOffset;
        camera.orthographic = true;
        camera.orthographicSize = 80f;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 650f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.59f, .52f, .44f);

        sun.transform.rotation = Quaternion.Euler(44f, -38f, 0f);
        sun.intensity = 1.28f;
        sun.color = new Color(1f, .943f, .858f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .78f;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.38f, .40f, .43f);
        RenderSettings.fog = false; // V2E will compare atmosphere separately.
        RenderSettings.skybox = null;

        rootObject.AddComponent<ArtLabLandscapeFlight>().Configure(pipeline,
            player.transform, camera, flight, boost, Half, "V2A / dry frontier ground only");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save new V2A study scene.");
        Selection.activeGameObject = terrain;
        return "V2A built: FrontierV2Ground.unity (1200x1200, 240x240 terrain, 2048 albedo). " +
            "Play the scene, compare at pitch 90/75/65 and size 80. V17/gameplay are untouched.";
    }

    private static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Noise(float x, float z, float f, float ox, float oz) =>
        Mathf.PerlinNoise(x * f + ox, z * f + oz);

    private static float Mesa(float x, float z, float cx, float cz, float rx, float rz)
    {
        float dx = (x - cx) / rx;
        float dz = (z - cz) / rz;
        // Slightly irregular finite shoulders, not a repeating field of hills.
        float irregular = (Noise(x, z, .013f, 91f, 34f) - .5f) * .13f;
        float distance = Mathf.Sqrt(dx * dx + dz * dz) + irregular;
        return 1f - Smooth(.68f, 1.06f, distance);
    }

    private static float Height(float x, float z)
    {
        // Two long geological walls frame an accessible central flight corridor.
        // No painted sunlight or regular ridged-noise 'tumour' texture.
        float warpX = (Noise(x, z, .0062f, 31f, 72f) - .5f) * 23f;
        float wx = x + warpX;
        float leftEdge = -292f + 54f * Mathf.Sin(z * .0042f + .75f);
        float rightEdge = 292f + 45f * Mathf.Sin(z * .0046f + 1.1f);
        float left = Smooth(9f, 156f, leftEdge - wx) * 25f;
        float right = Smooth(2f, 165f, wx - rightEdge) * 24f;
        float westernShelf = Mesa(wx, z, -355f, 175f, 205f, 115f) * 6f;
        float easternShelf = Mesa(wx, z, 365f, -185f, 150f, 230f) * 7f;
        float basin = 2.3f + (Noise(x, z, .0038f, 55f, 18f) - .5f) * 3.1f;
        float gullyX = -38f + .16f * z + 30f * Mathf.Sin(z * .006f + .4f);
        float gullyDistance = Mathf.Abs(x - gullyX);
        float gully = (1f - Smooth(10f, 39f, gullyDistance)) * 2.3f;
        float h = basin + left + right + westernShelf + easternShelf - gully;
        return Mathf.Clamp(h, -2f, 35f); // Jet visual study flies at y=48.
    }

    private static Mesh BuildMesh()
    {
        var vertices = new Vector3[(Cells + 1) * (Cells + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[Cells * Cells * 6];
        for (int z = 0; z <= Cells; z++)
        for (int x = 0; x <= Cells; x++)
        {
            float worldX = x * Spacing - Half, worldZ = z * Spacing - Half;
            int index = z * (Cells + 1) + x;
            vertices[index] = new Vector3(worldX, Height(worldX, worldZ), worldZ);
            uv[index] = new Vector2(x / (float)Cells, z / (float)Cells);
        }
        int t = 0;
        for (int z = 0; z < Cells; z++)
        for (int x = 0; x < Cells; x++)
        {
            int a = z * (Cells + 1) + x, b = a + Cells + 1;
            triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
            triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
        }
        var mesh = new Mesh { name = "V2A / sculpted frontier ground" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Sample the ACTUAL authored mesh, not repeated expensive/noisy analytic
    // height evaluations for each of the four million albedo texels.
    private static void SampleHeightSlope(Vector3[] vertices, float x, float z,
        out float height, out float slope)
    {
        float gx = Mathf.Clamp((x + Half) / Spacing, 0f, Cells - .00001f);
        float gz = Mathf.Clamp((z + Half) / Spacing, 0f, Cells - .00001f);
        int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
        float u = gx - ix, v = gz - iz;
        int a = iz * (Cells + 1) + ix, b = a + Cells + 1;
        float h00 = vertices[a].y, h10 = vertices[a + 1].y;
        float h01 = vertices[b].y, h11 = vertices[b + 1].y;
        height = Mathf.Lerp(Mathf.Lerp(h00, h10, u), Mathf.Lerp(h01, h11, u), v);
        float dx = Mathf.Lerp(h10 - h00, h11 - h01, v) / Spacing;
        float dz = Mathf.Lerp(h01 - h00, h11 - h10, u) / Spacing;
        slope = Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static Texture2D BuildAlbedo(Vector3[] vertices)
    {
        var colors = new Color32[TextureSize * TextureSize];
        var dust = new Color32(181, 152, 119, 255);
        var pale = new Color32(201, 172, 133, 255);
        var ochre = new Color32(160, 126, 98, 255);
        var stone = new Color32(133, 120, 109, 255);
        for (int iz = 0; iz < TextureSize; iz++)
        for (int ix = 0; ix < TextureSize; ix++)
        {
            float x = (ix + .5f) / TextureSize * 2f * Half - Half;
            float z = (iz + .5f) / TextureSize * 2f * Half - Half;
            float broad = Noise(x, z, .0038f, 88f, 41f);
            float medium = Noise(x, z, .012f, 53f, 26f);
            float fine = Noise(x, z, .077f, 12f, 68f);
            SampleHeightSlope(vertices, x, z, out float height, out float slope);
            Color c = Color.Lerp(dust, pale, Smooth(.27f, .75f, broad));
            c = Color.Lerp(c, ochre, Smooth(.42f, .78f, medium) * .28f);
            c = Color.Lerp(c, stone, Smooth(.12f, .47f, slope) * .75f);
            c = Color.Lerp(c, pale, Smooth(17f, 33f, height) * .14f);
            c *= Mathf.Lerp(.972f, 1.028f, fine);
            c.a = 1f;
            colors[iz * TextureSize + ix] = c;
        }
        var source = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
        byte[] bytes;
        try
        {
            source.SetPixels32(colors);
            source.Apply(false, false);
            bytes = ImageConversion.EncodeToPNG(source);
        }
        finally { Object.DestroyImmediate(source); }
        string path = Output + "/V2A_GroundColor.png";
        File.WriteAllBytes(Path.GetFullPath(path), bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Failed importing " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = TextureSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException("Missing imported ground texture.");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int split = folder.LastIndexOf('/');
        if (split < 0) throw new InvalidOperationException("Invalid asset folder: " + folder);
        EnsureFolder(folder.Substring(0, split));
        AssetDatabase.CreateFolder(folder.Substring(0, split), folder.Substring(split + 1));
    }
}
