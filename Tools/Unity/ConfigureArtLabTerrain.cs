using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated v9 study. Keep all source assets and the playable Sandbox unchanged.
public static class ConfigureArtLabTerrain
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV8Lighting.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV9Terrain.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV9Terrain";
    private const string MeshPath = Output + "/TerrainRelief.asset";
    private const int NX = 200, NZ = 168;
    private const float HalfX = 250f, HalfZ = 210f;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null) throw new InvalidOperationException("Missing ArtLab v8.");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV9Terrain");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Cannot create terrain output folder.");

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy ArtLab v8.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.name == "ArtLab V8 Lighting");
        var terrain = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Terrain mesh");
        var nature = root.transform.Cast<Transform>().Single(t => t.name == "Nature studies / layered crowns");
        var forest = nature.Find("B - Forest masses");
        var fixedArt = root.transform.Find("Fixed art references");
        var preview = root.GetComponent<ArtLabDirectionalLightingPreview>();
        if (forest == null || fixedArt == null || preview == null || terrain.sharedMesh == null)
            throw new InvalidOperationException("Unexpected v8 hierarchy.");

        var source = terrain.sharedMesh;
        var oldVertices = source.vertices;
        ValidateGrid(oldVertices, terrain.transform);
        var vertices = (Vector3[])oldVertices.Clone();
        float maxDelta = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            v.y += Relief(v.x, v.z);
            maxDelta = Mathf.Max(maxDelta, Mathf.Abs(v.y - oldVertices[i].y));
            vertices[i] = v;
        }
        var generated = UnityEngine.Object.Instantiate(source);
        generated.name = "TerrainRelief";
        generated.vertices = vertices;
        generated.RecalculateNormals();
        generated.RecalculateTangents();
        generated.RecalculateBounds();
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(generated, MeshPath);
            saved = generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, saved);
            EditorUtility.SetDirty(saved);
            UnityEngine.Object.DestroyImmediate(generated);
        }
        terrain.sharedMesh = saved;
        PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);

        // Preserve each object's original ground clearance using the actual rendered triangles.
        // V5 stores individual prefab instances under four woodland groups.
        int moved = 0;
        void Follow(Transform t)
        {
            var p = t.position;
            p.y += Sample(vertices, p.x, p.z) - Sample(oldVertices, p.x, p.z);
            t.position = p;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            moved++;
        }
        foreach (Transform group in forest)
        {
            if (group.name == "Fixed object shadows" || group.name == "Continuous woodland shade") continue;
            foreach (Transform tree in group) Follow(tree);
        }
        foreach (Transform prop in nature)
            if (prop.name == "Rock") Follow(prop);
        foreach (Transform model in fixedArt)
            if (model.name.StartsWith("Sentinel", StringComparison.Ordinal)) Follow(model);
        // Aircraft are airborne and retain their original world-space altitude.
        preview.ConfigureTerrainStudy();
        EditorUtility.SetDirty(preview);
        root.name = "ArtLab V9 Terrain";
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save v9.");
        Selection.activeGameObject = root;
        return $"ArtLab v9 ready: max relief delta {maxDelta:F1}, {moved} props re-grounded. 2/3: lighting, G/H unchanged. V8 and Sandbox intact.";
    }

    private static float Smooth(float lo, float hi, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, hi, t));
    private static float Hill(float x, float z, float cx, float cz, float rx, float rz)
    {
        float u = (x - cx) / rx, v = (z - cz) / rz;
        return Mathf.Exp(-2f * (u * u + v * v));
    }

    private static float Relief(float x, float z)
    {
        // Large authored landforms, visible from the static gameplay-distance camera.
        float h = 14f * Hill(x,z,-132,12,52,80)
            + 12f * Hill(x,z,63,-76,39,50)
            + 13f * Hill(x,z,148,12,43,70)
            + 8f * Hill(x,z,56,48,34,32)
            - 4f * Hill(x,z,47,-12,30,38);
        // Same protection region as V2, with a wider smooth transition on the new relief.
        float outside = Mathf.Max(Mathf.Abs(x + 43f) - 51f, Mathf.Abs(z + 7f) - 76f);
        float road = Smooth(8f, 23f, Mathf.Abs(z - 78f));
        float river = 102f + 9.5f * Mathf.Sin(z * .025f + .8f) + 4.2f * Mathf.Sin(z * .052f - 1.1f);
        float width = 11.8f + 2f * Mathf.Sin(z * .031f + 1.4f) + 1.1f * Mathf.Sin(z * .084f);
        float shore = Mathf.Abs(x - river) - width;
        // Leave existing riverbed, water and bridge geometry untouched.
        float mask = Smooth(0f, 25f, outside) * road * Smooth(7f, 24f, shore);
        // Keep the static aircraft's reference area near its existing ground height.
        float aircraft = Mathf.Min(Smooth(7f, 25f, Vector2.Distance(new Vector2(x,z), new Vector2(24,19))),
            Smooth(7f, 25f, Vector2.Distance(new Vector2(x,z), new Vector2(45,-24))));
        return h * mask * aircraft;
    }

    private static void ValidateGrid(Vector3[] v, Transform t)
    {
        if (v.Length != (NX+1)*(NZ+1) || t.position.sqrMagnitude > .0001f ||
            Quaternion.Angle(t.rotation, Quaternion.identity) > .01f || (t.lossyScale-Vector3.one).sqrMagnitude > .0001f)
            throw new InvalidOperationException("Expected the original V2 terrain grid at world origin.");
        for (int z=0; z<=NZ; z++) for (int x=0; x<=NX; x++)
        {
            var p = v[z*(NX+1)+x];
            if (Mathf.Abs(p.x - Mathf.Lerp(-HalfX,HalfX,x/(float)NX)) > .01f ||
                Mathf.Abs(p.z - Mathf.Lerp(-HalfZ,HalfZ,z/(float)NZ)) > .01f)
                throw new InvalidOperationException("Unexpected terrain vertex order.");
        }
    }

    private static float Sample(Vector3[] v, float x, float z)
    {
        float gx = Mathf.Clamp((x + HalfX) / (2f*HalfX) * NX, 0f, NX);
        float gz = Mathf.Clamp((z + HalfZ) / (2f*HalfZ) * NZ, 0f, NZ);
        int ix = Mathf.Min(Mathf.FloorToInt(gx), NX-1), iz = Mathf.Min(Mathf.FloorToInt(gz), NZ-1);
        float u = gx-ix, w = gz-iz;
        int a = iz*(NX+1)+ix, b = a+NX+1;
        // Matches V2 indices: (a,b,a+1), (a+1,b,b+1).
        return u+w <= 1f ? v[a].y*(1f-u-w)+v[a+1].y*u+v[b].y*w
            : v[b+1].y*(u+w-1f)+v[a+1].y*(1f-w)+v[b].y*(1f-u);
    }
}
