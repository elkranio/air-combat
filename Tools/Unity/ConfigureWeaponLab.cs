using System;
using System.Collections.Generic;
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

// Copy the accepted flight presentation into a deliberately large, isolated cannon test arena.
// No writes to Sandbox, art-study scenes, global RP settings or shared aircraft assets.
public static class ConfigureWeaponLab
{
    private const string Source = "Assets/_Project/Scenes/FrontierMesaPolish.unity";
    private const string Destination = "Assets/_Project/Scenes/WeaponLab.unity";
    private const string Output = "Assets/_Project/Generated/WeaponLab";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const string Audio = "Assets/_Project/Art/Weapons/Audio/AutocannonShot.wav";
    private const float HalfExtent = 6000f;
    private const int Grid = 192;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before building Weapon Lab.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save all open scenes before building Weapon Lab.");

        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        var shot = Require<AudioClip>(Audio);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (lit == null || unlit == null) throw new InvalidOperationException("URP Lit/Unlit shader unavailable.");
        EnsureFolder(Output);

        var source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(source, Destination, true))
            throw new InvalidOperationException("Failed to copy V20 into WeaponLab.unity.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var player = Root(scene, "Player");
        var cameraRoot = Root(scene, "Main Camera");
        var lightRoot = Root(scene, "Directional Light");
        var flight = player.GetComponent<FlightController>();
        var afterburner = player.GetComponent<Afterburner>();
        var camera = cameraRoot.GetComponent<Camera>();
        if (flight == null || afterburner == null || camera == null ||
            cameraRoot.GetComponent<TopDownCameraFollow>() == null ||
            lightRoot.GetComponent<Light>() == null)
            throw new InvalidOperationException("V20 flight/camera/sun layout is different from expected.");

        // Remove terrain studies, settlements and their preview scripts from the COPIED scene only.
        foreach (var go in scene.GetRootGameObjects())
            if (go != player && go != cameraRoot && go != lightRoot) Object.DestroyImmediate(go);
        var offset = camera.transform.position - player.transform.position;
        player.transform.SetPositionAndRotation(new Vector3(0f, 48f, -800f), Quaternion.identity);
        camera.transform.position = player.transform.position + offset;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 320f);

        var root = new GameObject("Weapon Lab / range and passive targets");
        var groundMat = MaterialAsset(Output + "/TrainingGround.mat", lit,
            new Color32(255, 255, 255, 255), Require<Texture2D>(MakeGroundTexture()));
        var ground = new GameObject("12 km practice ground", typeof(MeshFilter), typeof(MeshRenderer));
        ground.transform.SetParent(root.transform, false);
        ground.GetComponent<MeshFilter>().sharedMesh = SaveAsset(Output + "/TrainingTerrain.asset", MakeGroundMesh());
        var groundRenderer = ground.GetComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = groundMat;
        groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        groundRenderer.receiveShadows = true;

        var tracer = MaterialAsset(Output + "/AutocannonTracer.mat", unlit,
            new Color32(255, 221, 130, 255), null);
        var body = MaterialAsset(Output + "/DummyBody.mat", lit,
            new Color32(134, 172, 173, 255), null);
        var stripes = MaterialAsset(Output + "/DummyStripe.mat", lit,
            new Color32(229, 176, 95, 255), null);

        var gun = player.GetComponent<WeaponLabCannon>();
        if (gun == null) gun = player.AddComponent<WeaponLabCannon>();
        gun.Configure(tracer, shot);

        var targets = new Transform[7];
        var group = new GameObject("Floating static shooting dummies");
        group.transform.SetParent(root.transform, false);
        for (int i = 0; i < targets.Length; i++)
            targets[i] = MakeDummy(group.transform, i, body, stripes);

        var session = root.AddComponent<WeaponLabSession>();
        session.Configure(pipeline, player.transform, flight, afterburner, gun, targets, HalfExtent);
        EditorUtility.SetDirty(session);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save WeaponLab scene.");
        Selection.activeGameObject = player;
        return "WeaponLab ready: independent 12 x 12 km field, seven recycling passive dummies, " +
            "LMB forward autocannon (infinite ammo), T toggles heat, R reset. Sandbox and V20 untouched.";
    }

    private static Transform MakeDummy(Transform parent, int index, Material body, Material stripe)
    {
        var go = new GameObject("Passive Drone " + (index + 1).ToString("00"));
        go.transform.SetParent(parent, false);
        go.AddComponent<SphereCollider>().radius = 5f;
        var parts = new List<Renderer>
        {
            Box(go.transform, "Central hull", new Vector3(0, 0, 0), new Vector3(3.5f, 1.4f, 3.6f), body),
            Box(go.transform, "Right/left wing", new Vector3(0, -.12f, 0), new Vector3(9.4f, .65f, 1.3f), stripe),
            Box(go.transform, "Tall target fin", new Vector3(0, 1.15f, -.85f), new Vector3(.65f, 2.9f, 1.1f), body)
        };
        go.AddComponent<WeaponLabTarget>().Configure(parts.ToArray());
        return go.transform;
    }

    private static Renderer Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        return renderer;
    }

    private static Mesh MakeGroundMesh()
    {
        int side = Grid + 1;
        var verts = new Vector3[side * side];
        var uv = new Vector2[verts.Length];
        var tris = new int[Grid * Grid * 6];
        for (int z = 0; z <= Grid; z++)
        for (int x = 0; x <= Grid; x++)
        {
            float px = Mathf.Lerp(-HalfExtent, HalfExtent, x / (float)Grid);
            float pz = Mathf.Lerp(-HalfExtent, HalfExtent, z / (float)Grid);
            // Small, slow undulations; never a mountain that might obscure target practice.
            float broad = Mathf.PerlinNoise(px * .00032f + 31f, pz * .00032f + 47f);
            float mid = Mathf.PerlinNoise(px * .0011f + 92f, pz * .0011f + 21f);
            float h = (broad - .5f) * 11f + (mid - .5f) * 2.6f;
            int i = z * side + x;
            verts[i] = new Vector3(px, h, pz);
            uv[i] = new Vector2(x / (float)Grid * 15f, z / (float)Grid * 15f);
        }
        int n = 0;
        for (int z = 0; z < Grid; z++)
        for (int x = 0; x < Grid; x++)
        {
            int i = z * side + x;
            tris[n++] = i; tris[n++] = i + side; tris[n++] = i + 1;
            tris[n++] = i + 1; tris[n++] = i + side; tris[n++] = i + side + 1;
        }
        var mesh = new Mesh { name = "Weapon Lab / broad practice ground", vertices = verts, uv = uv, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static string MakeGroundTexture()
    {
        const int size = 512;
        var colors = new Color32[size * size];
        Color clay = new Color32(166, 125, 91, 255);
        Color dust = new Color32(191, 151, 109, 255);
        Color dark = new Color32(134, 99, 78, 255);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Seamless periodic multi-octave noise; suitable for repeat wrapping.
            float u = 2f * Mathf.PI * x / size, v = 2f * Mathf.PI * y / size;
            float a = 0.5f + 0.25f * Mathf.Sin(u * 3f + Mathf.Cos(v * 2f)) + .17f * Mathf.Sin(v * 5f - u * 2f);
            float b = 0.5f + 0.25f * Mathf.Sin(u * 13f + v * 7f) + .18f * Mathf.Cos(v * 11f - u * 9f);
            Color c = Color.Lerp(clay, dust, Mathf.Clamp01(a));
            c = Color.Lerp(c, dark, Mathf.Clamp01((b - .58f) * .35f));
            colors[y * size + x] = c;
        }
        var temp = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        byte[] bytes;
        try { temp.SetPixels32(colors); temp.Apply(false, false); bytes = ImageConversion.EncodeToPNG(temp); }
        finally { Object.DestroyImmediate(temp); }
        string path = Output + "/TrainingGround.png";
        File.WriteAllBytes(Path.GetFullPath(path), bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) throw new InvalidOperationException("Could not import ground PNG.");
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.mipmapEnabled = true;
        imp.isReadable = false;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Trilinear;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.SaveAndReimport();
        return path;
    }

    private static Material MaterialAsset(string path, Shader shader, Color color, Texture2D texture)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .06f);
        if (texture != null) material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T SaveAsset<T>(string path, T asset) where T : Object
    {
        var old = AssetDatabase.LoadAssetAtPath<T>(path);
        if (old == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
        EditorUtility.CopySerialized(asset, old);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(old);
        return old;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) throw new InvalidOperationException("Missing parent folder: " + parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static T Require<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().Single(go => go.name == name);
}
