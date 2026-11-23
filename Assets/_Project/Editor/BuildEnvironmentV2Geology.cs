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

// V2A2: comparative geology pass. Existing V2A terrain, scene and GUIDs stay untouched.
// B changes both geometric relief and the matching terrain albedo in the new scene.
public static class BuildEnvironmentV2Geology
{
    private const string Source = "Assets/_Project/Scenes/FrontierV2Ground.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierV2Geology.unity";
    private const string Output = "Assets/_Project/Generated/EnvironmentV2";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int Cells = 240;
    private const int Side = Cells + 1;
    private const int TextureSize = 2048;
    private const float Half = 600f;
    private const float Step = 2f * Half / Cells;

    [MenuItem("Air Combat/Art V2/V2A2 Create Geology Comparison")]
    private static void MenuCreate()
    {
        try { Debug.Log(Create()); }
        catch (Exception e) { Debug.LogError("V2A2: " + e); }
    }

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before authoring V2A2.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
            throw new InvalidOperationException("Missing the accepted V2A scene: " + Source);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null)
            throw new InvalidOperationException("V2A2 scene already exists; refusing to overwrite it.");
        foreach (var filename in new[] { "V2A2_Moderate.asset", "V2A2_Defined.asset",
            "V2A2_Moderate.mat", "V2A2_Defined.mat",
            "V2A2_ModerateColor.png", "V2A2_DefinedColor.png" })
            if (File.Exists(Path.GetFullPath(Output + "/" + filename)))
                throw new InvalidOperationException("V2A2 output already exists: " + filename);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        if (pipeline == null) throw new InvalidOperationException("Missing approved ArtLab URP pipeline.");

        // Copy; never edit the accepted V2A study, V17 reference or combat labs.
        var sourceScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(sourceScene, Destination, true))
            throw new InvalidOperationException("Could not copy V2A study scene.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var terrain = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
            .Single(f => f.name == "V2A terrain - 1200 x 1200");
        var surface = terrain.GetComponent<MeshRenderer>();
        var collider = terrain.GetComponent<MeshCollider>();
        var baseline = terrain.sharedMesh;
        var originalMaterial = surface != null ? surface.sharedMaterial : null;
        if (collider == null || baseline == null || originalMaterial == null ||
            baseline.vertexCount != Side * Side || baseline.uv.Length != Side * Side ||
            baseline.triangles.Length != Cells * Cells * 6)
            throw new InvalidOperationException("V2A ground structure changed. Inspect it before continuing.");

        var moderate = MakeGround(baseline, .75f, "Moderate");
        var defined = MakeGround(baseline, 1.3f, "Defined");
        AssetDatabase.CreateAsset(moderate.Mesh, Output + "/V2A2_Moderate.asset");
        AssetDatabase.CreateAsset(defined.Mesh, Output + "/V2A2_Defined.asset");
        Material moderateMaterial = MakeMaterial(originalMaterial, moderate, "Moderate", .75f);
        Material definedMaterial = MakeMaterial(originalMaterial, defined, "Defined", 1.3f);

        var artRoot = scene.GetRootGameObjects()
            .Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        var test = artRoot.AddComponent<EnvironmentV2GeologyComparison>();
        test.Configure(terrain, surface, collider,
            new[] { baseline, moderate.Mesh, defined.Mesh },
            new[] { originalMaterial, moderateMaterial, definedMaterial });
        // Defined variant is the initial Play Mode view; the original stays on B.
        terrain.sharedMesh = defined.Mesh;
        surface.sharedMaterial = definedMaterial;
        collider.sharedMesh = defined.Mesh;
        terrain.name = "V2A terrain - 1200 x 1200"; // Preserve authoring locator in copied scene.

        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        artRoot.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform,
            camera, player.GetComponent<FlightController>(), player.GetComponent<Afterburner>(), Half,
            "V2A2 / geological definition (B toggles)");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Cannot save new V2A2 scene.");
        Selection.activeGameObject = terrain.gameObject;
        return "V2A2 ready: FrontierV2Geology.unity. B toggles original / moderate / defined, " +
            "H toggles legend. Camera, lighting and accepted V2A assets unchanged.";
    }

    private sealed class Ground
    {
        public Mesh Mesh;
        public float[] Channels;
        public Vector3[] Vertices;
    }

    private static Ground MakeGround(Mesh original, float strength, string name)
    {
        var source = original.vertices;
        var vertices = (Vector3[])source.Clone();
        var channels = new float[vertices.Length];
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        for (int i = 0; i < vertices.Length; i++)
        {
            float x = source[i].x, z = source[i].z;
            float cut, bank;
            Relief(x, z, out cut, out bank);
            // Protect the Y=48 flight study from raised mesas while carving the plain.
            float added = bank * Mathf.Clamp01((38f - source[i].y) / 11f) - cut;
            vertices[i].y = Mathf.Clamp(source[i].y + added * strength, -4f, 37f);
            channels[i] = Mathf.Clamp01(cut / 7.5f);
            low = Mathf.Min(low, vertices[i].y);
            high = Mathf.Max(high, vertices[i].y);
        }
        var mesh = new Mesh { name = "V2A2 / " + name + " directional erosion" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = original.uv;
        mesh.triangles = original.triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Debug.Log($"V2A2 {name}: {mesh.vertexCount} vertices, height {low:F1}..{high:F1}, strength {strength:F2}");
        return new Ground { Mesh = mesh, Channels = channels, Vertices = vertices };
    }

    private static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Noise(float x, float z, float f, float ox, float oz) =>
        Mathf.PerlinNoise(x * f + ox, z * f + oz);

    private static float Channel(float x, float z, float baseX, float slope,
        float phase, float width, float depth, out float bank)
    {
        // Longitudinal drainage with low-frequency asymmetric bends.
        float path = baseX + slope * z + 24f * Mathf.Sin(z * .008f + phase)
            + 9f * Mathf.Sin(z * .017f + phase * 2.1f);
        float d = Mathf.Abs(x - path);
        float trench = (1f - Smooth(width * .4f, width * 1.55f, d)) * depth;
        // Shallow adjoining shelf: not a periodic contour line around every gully.
        bank = (Smooth(width * .9f, width * 1.8f, d) -
            Smooth(width * 2.25f, width * 3.3f, d)) * 2.1f;
        return trench;
    }

    private static void Relief(float x, float z, out float cut, out float bank)
    {
        // Preserve open approaches around the starting aircraft but provide
        // readable, distinct erosion formations across the flight corridor.
        float southNorth = Smooth(-545f, -385f, z) * (1f - Smooth(415f, 580f, z));
        float westEast = Smooth(-540f, -390f, x) * (1f - Smooth(400f, 545f, x));
        float envelope = southNorth * westEast;
        float b0, b1, b2;
        float c0 = Channel(x, z, -215f, .11f, .4f, 21f, 5.6f, out b0);
        float c1 = Channel(x, z, -18f, -.06f, 1.9f, 30f, 6.6f, out b1);
        float c2 = Channel(x, z, 202f, .085f, 2.7f, 26f, 5.1f, out b2);
        // Different start/end elevations: no three parallel, full-map trenches.
        float westReach = Smooth(-540f, -405f, z) * (1f - Smooth(-75f, 90f, z));
        float eastReach = Smooth(-120f, 115f, z) * (1f - Smooth(340f, 520f, z));
        c0 *= westReach; b0 *= westReach;
        c2 *= eastReach; b2 *= eastReach;
        // Taper isolated tributaries; avoid a tiled field of identical valleys.
        float tributaryA = 4.2f * (1f - Smooth(13f, 33f,
            Mathf.Abs(x - (-120f + (z - 60f) * .53f)))) *
            Smooth(-190f, -40f, z) * (1f - Smooth(180f, 285f, z));
        float tributaryB = 3.9f * (1f - Smooth(11f, 32f,
            Mathf.Abs(x - (128f - (z + 120f) * .48f)))) *
            Smooth(-340f, -240f, z) * (1f - Smooth(35f, 150f, z));
        cut = (c0 + c1 + c2 + tributaryA + tributaryB) * envelope;
        // Larger, non-repeating banks that survive gameplay-scale mip filtering.
        float shelf = 2.8f * (1f - Smooth(25f, 110f,
            Mathf.Abs(x - (-295f + .10f * z)))) *
            Smooth(-350f, -140f, z) * (1f - Smooth(130f, 345f, z));
        float fractured = 1.65f * (Noise(x, z, .017f, 59f, 34f) - .5f);
        bank = (b0 + b1 + b2 + shelf + fractured) * envelope;
    }

    // Match color/slope/channel masks to the ACTUAL triangulated game mesh.
    // Never derive a fake light direction or bake a sun shadow into albedo.
    private static void Sample(Ground ground, float x, float z,
        out float height, out float slope, out float channel)
    {
        float gx = Mathf.Clamp((x + Half) / Step, 0f, Cells - .00001f);
        float gz = Mathf.Clamp((z + Half) / Step, 0f, Cells - .00001f);
        int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
        float u = gx - ix, v = gz - iz;
        int a = iz * Side + ix, b = a + Side;
        var verts = ground.Vertices;
        float h00 = verts[a].y, h10 = verts[a + 1].y;
        float h01 = verts[b].y, h11 = verts[b + 1].y;
        height = Mathf.Lerp(Mathf.Lerp(h00, h10, u), Mathf.Lerp(h01, h11, u), v);
        float dx = Mathf.Lerp(h10 - h00, h11 - h01, v) / Step;
        float dz = Mathf.Lerp(h01 - h00, h11 - h10, u) / Step;
        slope = Mathf.Sqrt(dx * dx + dz * dz);
        var flow = ground.Channels;
        channel = Mathf.Lerp(Mathf.Lerp(flow[a], flow[a + 1], u),
            Mathf.Lerp(flow[b], flow[b + 1], u), v);
    }

    private static Material MakeMaterial(Material original, Ground ground,
        string label, float strength)
    {
        var colors = new Color32[TextureSize * TextureSize];
        Color dust = new Color32(185, 153, 117, 255);
        Color pale = new Color32(207, 173, 128, 255);
        Color ochre = new Color32(156, 115, 84, 255);
        Color stone = new Color32(131, 116, 103, 255);
        Color wash = new Color32(147, 113, 86, 255);
        for (int iz = 0; iz < TextureSize; iz++)
        for (int ix = 0; ix < TextureSize; ix++)
        {
            float x = (ix + .5f) * (2f * Half / TextureSize) - Half;
            float z = (iz + .5f) * (2f * Half / TextureSize) - Half;
            Sample(ground, x, z, out float h, out float slope, out float channel);
            float broad = Noise(x, z, .006f, 73f, 25f);
            float medium = Noise(x, z, .027f, 22f, 67f);
            Color c = Color.Lerp(dust, pale, Smooth(.26f, .74f, broad));
            c = Color.Lerp(c, ochre, Smooth(.48f, .81f, medium) * .28f);
            c = Color.Lerp(c, wash, channel * Mathf.Lerp(.28f, .47f, strength / 1.3f));
            c = Color.Lerp(c, stone, Smooth(.09f, .32f, slope) * .82f);
            c = Color.Lerp(c, pale, Smooth(19f, 34f, h) * .12f);
            c.a = 1f;
            colors[iz * TextureSize + ix] = c;
        }
        var image = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
        byte[] png;
        try { image.SetPixels32(colors); image.Apply(false, false); png = ImageConversion.EncodeToPNG(image); }
        finally { Object.DestroyImmediate(image); }
        string path = Output + "/V2A2_" + label + "Color.png";
        File.WriteAllBytes(Path.GetFullPath(path), png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Texture import failed: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 6;
        importer.maxTextureSize = TextureSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("Missing imported texture " + path);
        var material = new Material(original) { name = "V2A2 / " + label + " geology" };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        string materialPath = Output + "/V2A2_" + label + ".mat";
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }
}
