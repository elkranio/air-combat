using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-time scene authoring, never a runtime generator.
    internal static class Mission01FirstSortie
    {
        private const string MainScene = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string MissionScene = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string ArtParent = "Assets/_Project/Art/Environment";
        private const string ArtFolder = ArtParent + "/Mission01_FirstSortie";
        private const string DataPath = ArtFolder + "/Mission01_Terrain.asset";
        private const string MeshPath = ArtFolder + "/Mission01_Facets.asset";
        private const string MaterialPath = ArtFolder + "/Mission01_Sand.mat";
        private const float MapSize = 2000f;
        private const int Cells = 70; // Same nominal ~28.6-unit facets as TERRAIN-01.

        [MenuItem("Air Combat/Missions/Create or Open Mission 01 - First Sortie")]
        private static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MissionScene) != null)
            {
                EditorSceneManager.OpenScene(MissionScene, OpenSceneMode.Single);
                return; // Never overwrite the authored mission.
            }
            if (AssetDatabase.IsValidFolder(ArtFolder) || !AssetDatabase.IsValidFolder(ArtParent) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
            {
                Debug.LogError("MISSION-01: source scene or art folder state is unexpected. Nothing created.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("MISSION-01: URP Lit shader not found.");
                return;
            }
            if (!AssetDatabase.CopyAsset(MainScene, MissionScene))
            {
                Debug.LogError("MISSION-01: could not copy the source scene.");
                return;
            }

            bool createdArt = false;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(MissionScene, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var session = roots.Select(r => r.GetComponent<E1MapFlightSession>())
                    .FirstOrDefault(s => s != null);
                var lighting = roots.FirstOrDefault(r => r.name == "E1 / CombatLightingRig");
                if (session == null || lighting == null || lighting.GetComponentInChildren<Light>(true) == null)
                    throw new InvalidOperationException("Flight session or lighting rig missing.");
                var serialized = new SerializedObject(session);
                var jet = serialized.FindProperty("player").objectReferenceValue as JetAssembly;
                var rig = serialized.FindProperty("cameraRig").objectReferenceValue as CombatCameraRig;
                if (jet == null || rig == null || jet.GetComponent<FlightController>() == null)
                    throw new InvalidOperationException("Player or camera missing.");
                var trigger = jet.GetComponent<PlayerWeaponTrigger>();
                if (trigger == null) throw new InvalidOperationException("Player trigger missing.");
                var playerRoot = jet.transform.root.gameObject;
                var cameraRoot = rig.transform.root.gameObject;

                // Keep the tested camera/lighting and temporary flight preview, but no encounter.
                serialized.FindProperty("combatHud").objectReferenceValue = null;
                serialized.FindProperty("airContacts").arraySize = 0;
                serialized.FindProperty("altitudeComparison").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var encounter = session.GetComponent<Air01Encounter>();
                if (encounter != null) UnityEngine.Object.DestroyImmediate(encounter);
                trigger.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
                foreach (var root in roots)
                    if (root != session.gameObject && root != playerRoot && root != cameraRoot && root != lighting)
                        UnityEngine.Object.DestroyImmediate(root);

                playerRoot.transform.SetPositionAndRotation(new Vector3(-700f, 78f, -700f),
                    Quaternion.Euler(0f, 45f, 0f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerRoot.transform);

                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "Mission01_FirstSortie")))
                    throw new InvalidOperationException("Could not create mission art folder.");
                createdArt = true;
                var data = new TerrainData { heightmapResolution = 257,
                    size = new Vector3(MapSize, 180f, MapSize) };
                data.SetHeights(0, 0, new float[257, 257]);
                AssetDatabase.CreateAsset(data, DataPath);
                var sourceObject = Terrain.CreateTerrainGameObject(data);
                sourceObject.name = "Mission01 / Editable terrain";
                sourceObject.transform.position = new Vector3(-MapSize * .5f, 0f, -MapSize * .5f);
                var source = sourceObject.GetComponent<Terrain>();
                source.allowAutoConnect = false;
                source.drawTreesAndFoliage = false;
                source.drawHeightmap = false;
                var collider = sourceObject.GetComponent<TerrainCollider>();
                if (collider != null) collider.enabled = false;

                var material = new Material(shader) { name = "Mission01 Sand" };
                material.SetColor("_BaseColor", new Color32(184, 169, 147, 255));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(material, MaterialPath);
                var mesh = new Mesh { name = "Mission01 Facets" };
                Terrain01Playground.FillMesh(mesh, data, Cells);
                AssetDatabase.CreateAsset(mesh, MeshPath);
                var surface = new GameObject("Mission01 / Faceted surface", typeof(MeshFilter), typeof(MeshRenderer));
                surface.transform.position = sourceObject.transform.position;
                surface.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surface.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                // Scene-neutral anchors for future airfield and mission authoring; no layout yet.
                new GameObject("Mission01 / Airfield").transform.position = new Vector3(-700f, 0f, -700f);
                new GameObject("Mission01 / Objectives");
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save mission scene.");
                Selection.activeGameObject = sourceObject;
                Debug.Log("MISSION-01A ready: flat 2 km map; temporary Kestrel free-flight preview.");
            }
            catch (Exception ex)
            {
                // Only remove this invocation's new assets. The original is never saved or modified.
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(MissionScene);
                if (createdArt) AssetDatabase.DeleteAsset(ArtFolder);
                Debug.LogError("MISSION-01: creation failed: " + ex);
            }
        }
    }
}
