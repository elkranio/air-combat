using System;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // In-place component upgrade, not a model/scene generator. Existing
    // prefab instances inherit the new target and collider automatically.
    internal static class BD02BuildingAuthoring
    {
        private const string Folder = "Assets/_Project/Prefabs/Environment/";
        private static readonly string[] Names = { "BD01_SupplyWarehouse", "WH02_EquipmentShelter", "WH03_SupplyShelter" };
        private static readonly Vector3[] Sizes = { new Vector3(18,9,28), new Vector3(14,6,20), new Vector3(16,7,22) };
        private static readonly int[] Counts = { 8,8,9 };

        [MenuItem("Air Combat/Missions/M02 - Enable BD02 Building Destruction")]
        private static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int saved = 0;
            try
            {
                // Preflight all three assets before touching any of them.
                for (int i = 0; i < Names.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Names[i] + ".prefab");
                    Validate(prefab, i);
                }
                for (int i = 0; i < Names.Length; i++)
                {
                    string path = Folder + Names[i] + ".prefab";
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var box = root.GetComponent<BoxCollider>();
                        if (box == null) box = root.AddComponent<BoxCollider>();
                        box.center = Vector3.up * (Sizes[i].y * .5f);
                        box.size = Sizes[i];
                        box.isTrigger = false;
                        box.enabled = true;
                        if (root.GetComponent<DepotBuildingTarget>() == null) root.AddComponent<DepotBuildingTarget>();
                        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                        if (!success) throw new InvalidOperationException("Could not save " + path);
                        saved++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                AssetDatabase.SaveAssets();
                Debug.Log("BD02: three reusable ground targets upgraded. Disable Carryall Firing Range, enable Start Airborne, then Play M02.");
            }
            catch (Exception ex)
            { Debug.LogError($"BD02: {saved}/3 prefab assets saved. Review changes before retry. {ex}"); }
        }

        private static void Validate(GameObject prefab, int index)
        {
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + Names[index]);
            var visual = prefab.transform.Find("Visual");
            if (visual == null || visual.childCount != Counts[index])
                throw new InvalidOperationException("Unexpected visual assemblies: " + Names[index]);
            foreach (Transform child in visual)
                if (child.GetComponent<MeshFilter>() == null || child.GetComponent<MeshRenderer>() == null)
                    throw new InvalidOperationException("Unexpected mesh hierarchy: " + Names[index]);
            var box = prefab.GetComponent<BoxCollider>();
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != (box != null ? 1 : 0))
                throw new InvalidOperationException("Unexpected colliders: " + Names[index]);
            foreach (var target in prefab.GetComponentsInChildren<DamageableTarget>(true))
                if (!(target is DepotBuildingTarget) || target.gameObject != prefab)
                    throw new InvalidOperationException("Unexpected target receiver: " + Names[index]);
        }
    }
}
