#if UNITY_EDITOR
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

// V2A3: correction for the 5-unit grid and parallel banded gullies in V2A2.
// New isolated scene from accepted V2A, not a destructive rewrite of V2A2.
public static class BuildEnvironmentV2Erosion
{
    private const string Source = "Assets/_Project/Scenes/FrontierV2Ground.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierV2Erosion.unity";
    private const string Output = "Assets/_Project/Generated/EnvironmentV2";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int OriginalCells = 240;
    private const int Cells = 600; // 2m samples vs the visible 5m V2A/V2A2 stair-steps.
    private const int TextureSize = 2048;
    private const float Half = 600f;
    private const float Step = Half * 2f / Cells;

    private struct Drain
    {
        public Vector2[] Path;
        public float Width;
        public float Depth;
        public float Bank;
        public Drain(float width, float depth, float bank, params Vector2[] path)
        { Width = width; Depth = depth; Bank = bank; Path = path; }
    }

    // One watershed with finite, diagonal tributaries. Unlike V2A2 there are
    // no three evenly-spaced, north-south, map-length trenches.
    private static readonly Drain[] Drains =
    {
        new Drain(16f, 5.1f, 1.0f,
            new Vector2(-300,-580), new Vector2(-240,-455), new Vector2(-171,-365),
            new Vector2(-119,-274), new Vector2(-105,-165), new Vector2(-52,-45),
            new Vector2(-16,70), new Vector2(62,212), new Vector2(76,340),
            new Vector2(146,545)),
        new Drain(11f, 3.7f, .7f,
            new Vector2(-490,-395), new Vector2(-410,-350), new Vector2(-324,-306),
            new Vector2(-250,-283), new Vector2(-119,-274)),
        new Drain(10f, 3.2f, .6f,
            new Vector2(105,-495), new Vector2(44,-409), new Vector2(-7,-322),
            new Vector2(-62,-288), new Vector2(-119,-274)),
        new Drain(10f, 3.2f, .7f,
            new Vector2(-465,205), new Vector2(-355,165), new Vector2(-254,123),
            new Vector2(-134,94), new Vector2(-16,70)),
        new Drain(11f, 3.9f, .8f,
            new Vector2(345,330), new Vector2(244,303), new Vector2(153,262),
            new Vector2(101,234), new Vector2(62,212))
    };

    [MenuItem("Air Combat/Art V2/V2A3 Create Erosion Study")]
    private static void MenuCreate()
    {
        try { Debug.Log(Create()); }
        catch (Exception e) { Debug.LogError("V2A3: " + e); }
    }

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring V2A3.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before authoring V2A3.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
            throw new InvalidOperationException("Missing green V2A study: " + Source);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null)
            throw new InvalidOperationException("V2A3 destination already exists. Refusing to overwrite it.");
        foreach (var file in new[] {"V2A3_Erosion.asset", "V2A3_ErosionColor.png", "V2A3_Erosion.mat"})
            if (File.Exists(Path.GetFullPath(Output + "/" + file)))
                throw new InvalidOperationException("V2A3 asset already exists: " + file);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        if (pipeline == null) throw new InvalidOperationException("Missing ArtLab URP pipeline.");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP Lit shader.");

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true))
            throw new InvalidOperationException("Could not copy V2A scene.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
            .Single(f => f.name == "V2A terrain - 1200 x 1200");
        var renderer = terrain.GetComponent<MeshRenderer>();
        var collider = terrain.GetComponent<MeshCollider>();
        var baseline = terrain.sharedMesh;
        Material originalMaterial = renderer != null ? renderer.sharedMaterial : null;
        if (baseline == null || collider == null || originalMaterial == null ||
            !baseline.isReadable || baseline.vertexCount != (OriginalCells + 1) * (OriginalCells + 1))
            throw new InvalidOperationException("Unexpected V2A terrain; original study must be unchanged.");

        Mesh upgraded = GenerateMesh(baseline);
        AssetDatabase.CreateAsset(upgraded, Output + "/V2A3_Erosion.asset");
        var texture = GenerateAlbedo(upgraded);
        var material = new Material(originalMaterial) { name = "V2A3 / directional watershed" };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .065f);
        AssetDatabase.CreateAsset(material, Output + "/V2A3_Erosion.mat");

        var artRoot = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        artRoot.AddComponent<EnvironmentV2ErosionPreview>().Configure(
            terrain, renderer, collider, baseline, originalMaterial, upgraded, material);
        terrain.sharedMesh = upgraded;
        renderer.sharedMaterial = material;
        collider.sharedMesh = upgraded;

        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        artRoot.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform,
            camera, player.GetComponent<FlightController>(), player.GetComponent<Afterburner>(), Half,
            "V2A3 / branched watershed (B compare)");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save V2A3 scene.");
        Selection.activeGameObject = terrain.gameObject;
        return "V2A3 created: FrontierV2Erosion.unity, 600x600 terrain / 2048 color. " +
            "B: original vs branched watershed. V2A, V2A2 and combat scenes untouched.";
    }

    private static float Smooth(float a, float b, float value)
    {
        float t = Mathf.Clamp01((value - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Noise(float x, float z, float frequency, float ox, float oz) =>
        Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz);

    private static float OriginalHeight(Vector3[] original, float x, float z)
    {
        const int side = OriginalCells + 1;
        const float step = 1200f / OriginalCells;
        float gx = Mathf.Clamp((x + Half) / step, 0, OriginalCells - .0001f);
        float gz = Mathf.Clamp((z + Half) / step, 0, OriginalCells - .0001f);
        int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
        float u = gx - ix, v = gz - iz;
        int a = iz * side + ix;
        float h00 = original[a].y, h10 = original[a + 1].y;
        float h01 = original[a + side].y, h11 = original[a + side + 1].y;
        // V2A mesh consists of two triangles per cell. Continuous bicubic-like
        // smoothing isn't appropriate for cliff edges; bilinear is enough here.
        return Mathf.Lerp(Mathf.Lerp(h00, h10, u), Mathf.Lerp(h01, h11, u), v);
    }

    private static float DistanceToDrain(Drain drain, float x, float z, out float along)
    {
        Vector2 p = new Vector2(x, z);
        float best = float.MaxValue;
        float bestAlong = 0f;
        float sum = 0f;
        float length = 0f;
        for (int i = 0; i < drain.Path.Length - 1; i++)
            length += Vector2.Distance(drain.Path[i], drain.Path[i + 1]);
        for (int i = 0; i < drain.Path.Length - 1; i++)
        {
            Vector2 a = drain.Path[i], d = drain.Path[i + 1] - a;
            float segment = d.magnitude;
            float t = segment > .0001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / (segment * segment)) : 0;
            float distanceSquared = (p - (a + d * t)).sqrMagnitude;
            if (distanceSquared < best)
            {
                best = distanceSquared;
                bestAlong = (sum + segment * t) / Mathf.Max(1f, length);
            }
            sum += segment;
        }
        along = bestAlong;
        return Mathf.Sqrt(best);
    }

    private static float Height(Vector3[] baseVertices, float x, float z, out float erosion)
    {
        float baseline = OriginalHeight(baseVertices, x, z);
        // Do not build a periodic 'comb': just slightly distort the watercourse
        // distances and create broader nonuniform upland surfaces.
        float warpX = (Noise(x, z, .013f, 65f, 92f) - .5f) * 10f;
        float warpZ = (Noise(x, z, .011f, 31f, 74f) - .5f) * 10f;
        float relief = 0f;
        float bank = 0f;
        erosion = 0f;
        for (int i = 0; i < Drains.Length; i++)
        {
            var drain = Drains[i];
            float progress;
            float distance = DistanceToDrain(drain, x + warpX, z + warpZ, out progress);
            float taper = Smooth(0f, .085f, progress) * (1f - Smooth(.92f, 1f, progress));
            float bed = Mathf.Exp(-.5f * Mathf.Pow(distance / drain.Width, 2f));
            // Upper banks carry geometry rather than just painted dark lines.
            float shoulder = Mathf.Exp(-.5f * Mathf.Pow((distance - drain.Width * 2.15f) / (drain.Width * .95f), 2f));
            float weight = taper * (i == 0 ? 1f : .84f);
            relief += bed * drain.Depth * weight;
            bank += shoulder * drain.Bank * weight;
            erosion = Mathf.Max(erosion, bed * weight);
        }
        float upland = (Noise(x, z, .016f, 92f, 34f) - .5f) * 3.3f
            + (Noise(x, z, .048f, 11f, 67f) - .5f) * 1.0f;
        // Extra local tilted masses, never a blanket filter over the whole map.
        float flankWest = Smooth(-95f, 45f, -x - 175f) * .7f;
        float flankEast = Smooth(70f, 235f, x) * .65f;
        float height = baseline + upland * (0.30f + flankWest + flankEast) + bank - relief;
        return Mathf.Clamp(height, -6f, 38f); // Maintain Y=48 aircraft clearance.
    }

    private static Mesh GenerateMesh(Mesh original)
    {
        Vector3[] source = original.vertices;
        var verts = new Vector3[(Cells + 1) * (Cells + 1)];
        var uv = new Vector2[verts.Length];
        var tris = new int[Cells * Cells * 6];
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int iz = 0; iz <= Cells; iz++)
        for (int ix = 0; ix <= Cells; ix++)
        {
            float x = ix * Step - Half, z = iz * Step - Half;
            int index = iz * (Cells + 1) + ix;
            verts[index] = new Vector3(x, Height(source, x, z, out _), z);
            uv[index] = new Vector2((float)ix / Cells, (float)iz / Cells);
            minY = Mathf.Min(minY, verts[index].y);
            maxY = Mathf.Max(maxY, verts[index].y);
        }
        int n = 0;
        for (int iz = 0; iz < Cells; iz++)
        for (int ix = 0; ix < Cells; ix++)
        {
            int a = iz * (Cells + 1) + ix, b = a + Cells + 1;
            tris[n++] = a; tris[n++] = b; tris[n++] = a + 1;
            tris[n++] = a + 1; tris[n++] = b; tris[n++] = b + 1;
        }
        var mesh = new Mesh { name = "V2A3 / branched watershed / 2m mesh" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Debug.Log($"V2A3: {mesh.vertexCount} vertices, height {minY:F1}..{maxY:F1}; " +
            "no slope bands from 5m vertices.");
        return mesh;
    }

    private static void SampleSurface(Vector3[] vertices, Vector3[] normals, float x, float z,
        out float height, out float slope)
    {
        int side = Cells + 1;
        float fx = Mathf.Clamp((x + Half) / Step, 0, Cells - .00001f);
        float fz = Mathf.Clamp((z + Half) / Step, 0, Cells - .00001f);
        int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
        float u = fx - ix, v = fz - iz;
        int a = iz * side + ix, b = a + side;
        height = Mathf.Lerp(Mathf.Lerp(vertices[a].y, vertices[a + 1].y, u),
            Mathf.Lerp(vertices[b].y, vertices[b + 1].y, u), v);
        Vector3 normal = Vector3.Lerp(Vector3.Lerp(normals[a], normals[a + 1], u),
            Vector3.Lerp(normals[b], normals[b + 1], u), v).normalized;
        slope = Mathf.Sqrt(normal.x * normal.x + normal.z * normal.z);
    }

    private static Texture2D GenerateAlbedo(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var pixels = new Color32[TextureSize * TextureSize];
        Color dust = new Color32(185, 155, 121, 255);
        Color light = new Color32(204, 174, 139, 255);
        Color ochre = new Color32(159, 124, 94, 255);
        Color stone = new Color32(135, 122, 109, 255);
        Color floor = new Color32(168, 137, 102, 255);
        for (int iz = 0; iz < TextureSize; iz++)
        for (int ix = 0; ix < TextureSize; ix++)
        {
            float x = (ix + .5f) / TextureSize * 1200f - Half;
            float z = (iz + .5f) / TextureSize * 1200f - Half;
            SampleSurface(verts, normals, x, z, out float h, out float slope);
            float broad = Noise(x, z, .0055f, 84f, 28f);
            float medium = Noise(x, z, .039f, 26f, 77f);
            float grain = Noise(x, z, .17f, 91f, 36f);
            Color c = Color.Lerp(dust, light, Smooth(.26f, .77f, broad));
            c = Color.Lerp(c, ochre, Smooth(.39f, .79f, medium) * .24f);
            // Smooth blended material response, not quantized 5m slope steps.
            c = Color.Lerp(c, stone, Smooth(.13f, .59f, slope) * .40f);
            c = Color.Lerp(c, floor, Smooth(-4f, 4f, -h) * .09f);
            c *= Mathf.Lerp(.967f, 1.033f, grain);
            c.a = 1f;
            pixels[iz * TextureSize + ix] = c;
        }
        var image = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
        byte[] bytes;
        try { image.SetPixels32(pixels); image.Apply(false, false); bytes = ImageConversion.EncodeToPNG(image); }
        finally { Object.DestroyImmediate(image); }
        string path = Output + "/V2A3_ErosionColor.png";
        File.WriteAllBytes(Path.GetFullPath(path), bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) throw new InvalidOperationException("V2A3: TextureImporter missing.");
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.mipmapEnabled = true;
        imp.isReadable = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 6;
        imp.maxTextureSize = TextureSize;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException("V2A3: Imported ground texture missing.");
    }
}
#endif
