#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // C1b is a combat proof-of-feel, not a new gameplay implementation.
    // It copies the accepted C1 environment and wires existing combat prefabs.
    public static class BuildCameraC1CombatStudy
    {
        private const string Source = "Assets/_Project/Scenes/FrontierCameraC1.unity";
        private const string Destination = "Assets/_Project/Scenes/FrontierCameraCombatC1.unity";
        private const string Prefabs = "Assets/_Project/Prefabs/";

        [MenuItem("Air Combat/Art/C1b - Create 3-4 Camera Combat Study")]
        private static void BuildFromMenu()
        {
            try { Debug.Log(Build()); }
            catch (Exception exception) { Debug.LogError("C1b combat: " + exception); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null ||
                System.IO.File.Exists(Destination))
                throw new InvalidOperationException("C1b already exists; never overwrite a playtested scene.");

            // Validate BEFORE opening/saving anything, so missing assets cannot
            // leave an accidental half-authored scene.
            Require<SceneAsset>(Source);
            var playerPrefab = Require<GameObject>(Prefabs + "Aircraft/PlayerJet.prefab");
            var enemyPrefab = Require<GameObject>(Prefabs + "Aircraft/EnemyJet.prefab");
            var aaPrefab = Require<GameObject>(Prefabs + "Enemies/AA1Autocannon.prefab");
            var samPrefab = Require<GameObject>(Prefabs + "Enemies/SAM1Launcher.prefab");
            var depotPrefab = Require<GameObject>(Prefabs + "Targets/MilitaryDepot.prefab");
            var hudPrefab = Require<GameObject>(Prefabs + "UI/CombatHUD.prefab");
            var missilePrefab = Require<GameObject>("Assets/_Project/Art/Weapons/Missile.prefab");
            var missileImpact = Require<Material>("Assets/_Project/Art/VFX/MissileEffect.mat");
            var pipeline = Require<UniversalRenderPipelineAsset>("Assets/Settings/ScorchedFrontier_URP.asset");

            var source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, Destination, true))
                throw new InvalidOperationException("Could not copy C1 source scene.");
            var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var oldPlayer = roots.Single(x => x.name == "Player");
            var cameraRoot = roots.Single(x => x.name == "Main Camera");
            var camera = cameraRoot.GetComponent<Camera>();
            var headingCamera = cameraRoot.GetComponent<HeadingLockedObliqueCameraStudy>();
            var study = roots.SelectMany(x => x.GetComponentsInChildren<ArtLabLandscapeFlight>(true)).Single();
            // V19 preview cycles roof/rock materials on B. C1b reserves B for
            // the fighter's evasion toggle, and the scene already stores mode 3
            // (AO + normal) on its renderers; freeze those material choices.
            foreach (var preview in roots.SelectMany(x =>
                         x.GetComponentsInChildren<FrontierPracticalBakePreview>(true)))
                preview.enabled = false;
            var terrain = roots.SelectMany(x => x.GetComponentsInChildren<MeshFilter>(true))
                .Single(x => x.name == "Scorched terrain");
            if (camera == null || headingCamera == null || terrain.sharedMesh == null)
                throw new InvalidOperationException("C1 camera or V19 terrain changed; refusing blind authoring.");

            // V19 visual study has zero colliders. Without this, bomb/NURS rays
            // pass through the entire ground even though the picture looks right.
            var groundCollider = terrain.GetComponent<MeshCollider>();
            if (groundCollider == null) groundCollider = terrain.gameObject.AddComponent<MeshCollider>();
            groundCollider.sharedMesh = terrain.sharedMesh;
            groundCollider.convex = false;

            Object.DestroyImmediate(oldPlayer);
            var player = Spawn(playerPrefab, scene, "C1b / PlayerJet", new Vector3(82f, 48f, -45f));
            var jet = Need<JetAssembly>(player);
            var flight = Need<FlightController>(player);
            var afterburner = Need<Afterburner>(player);
            if (!jet.IsConfigured) throw new InvalidOperationException("PlayerJet chassis sockets missing.");

            // In this CLONED art scene the original camera already has Camera,
            // TopDownCameraFollow, TargetLock and HeadingLockedObliqueCameraStudy.
            // Adding the compositional rig lets the existing GroundAttackLabSession
            // bind the same gun/camera/lock contracts as in its native test scene.
            var cameraRig = cameraRoot.GetComponent<CombatCameraRig>();
            if (cameraRig == null) cameraRig = cameraRoot.AddComponent<CombatCameraRig>();
            headingCamera.Configure(player.transform);
            headingCamera.SetPreferredPitch(50f);
            headingCamera.SetCombatHotkeys(true); // 1-6 must remain weapon slots.
            var follow = Need<TopDownCameraFollow>(cameraRoot);
            follow.enabled = false;
            var lead = cameraRoot.GetComponent<CameraLookAhead>();
            if (lead != null) lead.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 80f;
            study.Configure(pipeline, player.transform, camera, flight, afterburner, 600f,
                "C1b / COMBAT ON V19 MAP");
            study.SetStudyLegend("C1b / COMBAT - CAMERA 50 DEG",
                "C: camera A/B   J/K/L: 50/58/65   H: hide");
            study.SetControlsLegend("A/D steer  W boost  Q/E break  1-6 guns  LMB fire  R reset");

            // 4 depots are a *current GroundAttackLabSession contract*, not a
            // cosmetic count. Keep them independent with their original prefabs.
            var depotGroup = new GameObject("Military Depots - G2");
            if (depotGroup.scene != scene) SceneManager.MoveGameObjectToScene(depotGroup, scene);
            Vector3[] depotPositions =
            {
                new Vector3(170, 0, 170), new Vector3(-100, 0, 115),
                new Vector3(240, 0, -115), new Vector3(-180, 0, -85)
            };
            for (int i = 0; i < depotPositions.Length; i++)
            {
                var depot = Spawn(depotPrefab, scene, "C1b / Depot " + (i + 1), depotPositions[i]);
                depot.transform.SetParent(depotGroup.transform, true);
            }

            // One of each ALREADY-AUTHORED ground defense. Both are within the
            // 460-unit radar horizon and SAM starts inside its 350-unit radius.
            var aa = Spawn(aaPrefab, scene, "C1b / AA1", new Vector3(145, 0, 85))
                .GetComponent<AntiAircraftTurret>();
            var sam = Spawn(samPrefab, scene, "C1b / SAM1", new Vector3(-75, 0, 180))
                .GetComponent<SurfaceToAirLauncher>();
            if (aa == null || sam == null) throw new InvalidOperationException("Site prefab components missing.");

            var hud = Need<CombatHud>(Spawn(hudPrefab, scene, "C1b / CombatHUD", Vector3.zero));
            // The source Canvas prefab's RectTransform has zero scale; its
            // existing GroundAttackLab scene instance overrides it to one.
            hud.transform.localScale = Vector3.one;
            var groundSessionRoot = new GameObject("C1b / Ground-combat session");
            if (groundSessionRoot.scene != scene) SceneManager.MoveGameObjectToScene(groundSessionRoot, scene);
            var groundSession = groundSessionRoot.AddComponent<GroundAttackLabSession>();
            groundSession.Configure(jet, cameraRig);
            groundSession.ConfigureAntiAircraft(aa);
            groundSession.ConfigureMissileTower(sam);
            groundSession.ConfigureHud(hud);

            // Air target is needed to test actual cursor lock and slot 3 launch;
            // the ground session intentionally does not own fighter AI.
            var enemy = Spawn(enemyPrefab, scene, "C1b / Enemy fighter", new Vector3(98, 48, 47));
            var fighter = Need<WeaponLabEnemy>(enemy);
            var airSessionRoot = new GameObject("C1b / Air-target adapter");
            if (airSessionRoot.scene != scene) SceneManager.MoveGameObjectToScene(airSessionRoot, scene);
            airSessionRoot.AddComponent<C1AirTargetSession>().Configure(player.transform,
                fighter, cameraRig, Need<MissileProjectile>(missilePrefab), missileImpact);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save combat camera study.");
            Selection.activeGameObject = player;
            return "C1b ready: " + Destination +
                " | Open/Play; C A/B; 1-6 guns; AA1/SAM1; enemy fighter and heading-up radar.";
        }

        private static GameObject Spawn(GameObject prefab, Scene scene, string name, Vector3 position)
        {
            var spawned = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (spawned == null) throw new InvalidOperationException("Cannot instantiate " + prefab.name);
            spawned.name = name;
            spawned.transform.SetPositionAndRotation(position, Quaternion.identity);
            return spawned;
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("Missing asset: " + path);

        private static T Need<T>(GameObject go) where T : Component =>
            go.GetComponent<T>() ??
            throw new InvalidOperationException("Missing component " + typeof(T).Name + " on " + go.name);
    }
}
#endif
