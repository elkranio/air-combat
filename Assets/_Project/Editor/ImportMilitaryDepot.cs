#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat.EditorTools
{
    // G2 asset import/placement only. Requires the actual Blender FBX and Cycles maps.
    // Never regenerates a combat scene, changes gameplay, or mutates other prefabs.
    internal static class ImportMilitaryDepot
    {
        private const string ScenePath = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string Source = "Assets/_Project/Art/Environment/MilitaryDepot";
        private const string Fbx = Source + "/MilitaryDepot.fbx";
        private const string Ready = Source + "/MilitaryDepot.ready.txt";
        private const string MaterialFolder = Source + "/Materials";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Targets";
        private const string PrefabPath = PrefabFolder + "/MilitaryDepot.prefab";
        private const string TargetRoot = "Military Depots - G2";
        private const string Menu = "Air Combat/Ground Attack/Import Military Depot G2";
        private static readonly string[] Groups =
        {
            "Depot_Concrete", "Depot_DarkSteel", "Depot_Steel", "Depot_Fuel",
            "Depot_Crates", "Depot_Barricade", "Depot_Warning", "Roof_BakedPanel"
        };

        [MenuItem(Menu)]
        private static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("G2: Stop Play Mode before import."); return; }
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("G2: Open and save a CLEAN GroundAttackLab.unity first."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
                AssetDatabase.IsValidFolder(MaterialFolder) || GameObject.Find(TargetRoot) != null)
            { Debug.LogError("G2: Assets or scene targets already exist. Refusing overwrite/duplicate."); return; }
            if (!File.Exists(Ready) || File.ReadAllLines(Ready).FirstOrDefault() != "G2_MILITARY_DEPOT_1")
            { Debug.LogError("G2: Missing completed Cycles bake marker. Run Blender generator first."); return; }
            if (AssetImporter.GetAtPath(Fbx) is not ModelImporter)
            { Debug.LogError("G2: FBX not imported. Finish Blender generator and Unity refresh first."); return; }
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
            { Debug.LogError("G2: URP Lit shader missing."); return; }

            var created = new List<string>();
            try
            {
                var model = ImportModel();
                var baseColor = ImportMap("DepotRoof_BaseColor", false, true);
                var normal = ImportMap("DepotRoof_Normal", true, false);
                var ao = ImportMap("DepotRoof_AO", false, false);
                if (model == null || baseColor == null || normal == null || ao == null)
                    throw new InvalidOperationException("G2: Missing FBX or one of the roof textures.");
                EnsureFolder(MaterialFolder);
                EnsureFolder(PrefabFolder);
                var mats = CreateMaterials(baseColor, normal, ao, created);

                var root = new GameObject("MilitaryDepot");
                try
                {
                    var embedded = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
                    if (embedded == null) throw new InvalidOperationException("G2: Failed to instantiate FBX.");
                    embedded.transform.SetParent(root.transform, false);
                    embedded.transform.localPosition = Vector3.zero;
                    embedded.transform.localRotation = Quaternion.identity;
                    embedded.transform.localScale = Vector3.one;
                    embedded.name = "Depot Mesh";

                    var renderers = embedded.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length != Groups.Length)
                        throw new InvalidOperationException("G2: Expected 8 low-poly render groups, found " + renderers.Length);
                    foreach (string name in Groups)
                    {
                        var matches = renderers.Where(r => r.gameObject.name == name).ToArray();
                        if (matches.Length != 1) throw new InvalidOperationException("G2: Missing/duplicate render group " + name);
                        var renderer = matches[0];
                        renderer.sharedMaterial = mats[name];
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    var roof = renderers.Single(r => r.name == "Roof_BakedPanel");
                    var filter = roof.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null || mesh.triangles.Length != 6 ||
                        mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
                        throw new InvalidOperationException("G2: Roof needs a two-triangle UV/Mikk-tangent bake target.");
                    if (Vector3.Dot(roof.transform.TransformDirection(mesh.normals[0]), Vector3.up) < .96f)
                        throw new InvalidOperationException("G2: Roof normals are not world-up (FBX axis mismatch).");
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                    if (bounds.size.x < 72 || bounds.size.x > 82 ||
                        bounds.size.z < 49 || bounds.size.z > 59 ||
                        bounds.size.y < 9 || bounds.size.y > 13 ||
                        Mathf.Abs(bounds.min.y) > .25f)
                        throw new InvalidOperationException("G2: Invalid FBX size/axis in metres: " + bounds);
                    // Target collider is structural only; G3 will own HP, damage and explosion logic.
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0f, 5.2f, 0f);
                    collider.size = new Vector3(76f, 10.4f, 54f);
                    bool ok;
                    var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out ok);
                    if (!ok || saved == null) throw new InvalidOperationException("G2: Failed to save depot prefab.");
                    created.Add(PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("G2: Prefab not found after save.");
                var group = new GameObject(TargetRoot);
                // The first target is directly ahead of G1's +Z player spawn.
                Vector3[] positions =
                {
                    new Vector3(0, 0, 250),
                    new Vector3(-310, 0, 625),
                    new Vector3(430, 0, 950),
                    new Vector3(-220, 0, 1300)
                };
                float[] headings = { 0f, 38f, -28f, 67f };
                for (int i = 0; i < positions.Length; i++)
                {
                    var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                    if (instance == null) throw new InvalidOperationException("G2: Could not place target " + i);
                    instance.name = "Military Depot " + (i + 1);
                    instance.transform.SetParent(group.transform, true);
                    instance.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0, headings[i], 0));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("G2: Could not save GroundAttackLab.");
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("G2 MilitaryDepot: actual FBX + Cycles roof normal/AO imported; prefab created; " +
                    "4 instances saved in GroundAttackLab. WeaponLab/Sandbox unchanged.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Only clean up our own generated asset references; never delete source FBX/maps.
                // The scene stays at its last on-disk state unless its save already succeeded.
                foreach (string path in created.AsEnumerable().Reverse())
                    AssetDatabase.DeleteAsset(path);
                if (SceneManager.GetActiveScene().path == ScenePath)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.LogError("G2 import aborted. Inspect the error; Blender FBX/maps are preserved.");
            }
        }

        private static GameObject ImportModel()
        {
            var importer = AssetImporter.GetAtPath(Fbx) as ModelImporter
                ?? throw new InvalidOperationException("G2: Missing model importer.");
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        }

        private static Texture2D ImportMap(string name, bool isNormal, bool isSrgb)
        {
            var path = Source + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("G2: Missing map importer " + path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = isSrgb;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Dictionary<string, Material> CreateMaterials(Texture2D baseColor,
            Texture2D normal, Texture2D ao, List<string> created)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var color = new Dictionary<string, Color>
            {
                { "Depot_Concrete", new Color(.48f,.46f,.41f) },
                { "Depot_DarkSteel", new Color(.22f,.26f,.26f) },
                { "Depot_Steel", new Color(.37f,.41f,.39f) },
                { "Depot_Fuel", new Color(.35f,.42f,.35f) },
                { "Depot_Crates", new Color(.46f,.36f,.26f) },
                { "Depot_Barricade", new Color(.55f,.51f,.42f) },
                { "Depot_Warning", new Color(.62f,.43f,.18f) },
                { "Roof_BakedPanel", Color.white }
            };
            var mats = new Dictionary<string, Material>();
            foreach (string name in Groups)
            {
                var mat = new Material(shader) { name = "G2_" + name };
                mat.SetColor("_BaseColor", color[name]);
                mat.SetFloat("_Metallic", name.Contains("Steel") ? .17f : 0f);
                mat.SetFloat("_Smoothness", .10f);
                if (name == "Roof_BakedPanel")
                {
                    mat.SetTexture("_BaseMap", baseColor);
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.SetTexture("_OcclusionMap", ao);
                    mat.SetFloat("_OcclusionStrength", .65f);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }
                string path = MaterialFolder + "/" + mat.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                    throw new InvalidOperationException("G2: Material already exists: " + path);
                AssetDatabase.CreateAsset(mat, path);
                created.Add(path);
                mats.Add(name, mat);
            }
            // Simple A/B reference, same mesh/color/lighting but without the baked maps.
            var baseOnly = new Material(mats["Roof_BakedPanel"]) { name = "G2_Roof_BaseOnly_AB" };
            baseOnly.SetTexture("_BumpMap", null);
            baseOnly.SetTexture("_OcclusionMap", null);
            baseOnly.DisableKeyword("_NORMALMAP");
            baseOnly.DisableKeyword("_OCCLUSIONMAP");
            string basePath = MaterialFolder + "/" + baseOnly.name + ".mat";
            AssetDatabase.CreateAsset(baseOnly, basePath);
            created.Add(basePath);
            return mats;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
