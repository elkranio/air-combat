#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-shot, dependency-guarded transition from art experiments to the base map.
    // The menu intentionally deletes its own source after a successful Apply.
    internal static class CleanupLegacyProjectB
    {
        private const string Map = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string WeaponLab = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string GroundLab = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string CameraPrefab = "Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab";
        private const string ThisScript = "Assets/_Project/Editor/CleanupLegacyProjectB.cs";
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static readonly string[] OldScenes =
        {
            "FrontierBakeTruth", "FrontierCameraC1", "FrontierCameraCombatC1",
            "FrontierMesaPolish", "FrontierPracticalBake", "FrontierStrata",
            "FrontierTerrainT1", "Sandbox", "ScorchedFrontier"
        };

        // These are candidates, NOT unconditional recursive deletions. Files still
        // referenced by the retained map, labs or gameplay prefabs stay untouched.
        private static readonly string[] OldAssetFolders =
        {
            "Assets/_Project/Generated/Battlefield",
            "Assets/_Project/Generated/CliffReference",
            "Assets/_Project/Generated/FrontierBakeTruth",
            "Assets/_Project/Generated/FrontierMesaPolish",
            "Assets/_Project/Generated/FrontierPracticalBake",
            "Assets/_Project/Generated/FrontierStrata",
            "Assets/_Project/Generated/RockKitR1",
            "Assets/_Project/Generated/ScorchedFrontier",
            "Assets/_Project/Generated/TerrainT1",
            "Assets/_Project/Generated/WeaponLab",
            "Assets/_Project/Art/Environment/FrontierBakeTruth",
            "Assets/_Project/Art/Environment/FrontierHeroKit",
            "Assets/_Project/Art/Environment/FrontierMesaPolish",
            "Assets/_Project/Art/Environment/FrontierPracticalBake",
            "Assets/_Project/Art/Environment/RockKitR1",
            "Assets/_Project/Art/Nature/ForestHybrid",
            "Assets/_Project/Art/Nature/ForestMasses",
            "Assets/_Project/Art/Nature/Trees",
            "Assets/_Project/Art/Terrain/GroundSet",
            "Assets/ThirdParty/Kenney/CityKitIndustrial",
            "Assets/ThirdParty/Kenney/CityKitSuburban",
            "Assets/ThirdParty/Kenney/NatureKit",
            "Assets/ThirdParty/Kenney/Skyboxes"
        };

        private static readonly string[] OldScripts =
        {
            "Assets/_Project/Editor/AssembleRockKitR1.cs",
            "Assets/_Project/Editor/BuildArcadeMapE1.cs",
            "Assets/_Project/Editor/BuildCameraC1CombatStudy.cs",
            "Assets/_Project/Editor/BuildCameraC1Study.cs",
            "Assets/_Project/Editor/BuildCliffPackReferenceStudy.cs",
            "Assets/_Project/Editor/BuildTerrainT1Study.cs",
            "Assets/_Project/Editor/InstallArcadeShadowProfileE1D.cs",
            "Assets/_Project/Editor/PrepareArcadeCameraPrefab.cs",
            "Assets/_Project/Editor/UpgradeArcadeMapE1B.cs",
            "Assets/_Project/Editor/UpgradeArcadeMapE1C.cs",
            "Assets/_Project/Scripts/Camera/HeadingLockedObliqueCameraStudy.cs",
            "Assets/_Project/Scripts/Scenes/C1AirTargetSession.cs",
            "Assets/_Project/Scripts/Visual/ArtLabLandscapeFlight.cs",
            "Assets/_Project/Scripts/Visual/AircraftGroundShadow.cs",
            "Assets/_Project/Scripts/Visual/FrontierBakeTruthPreview.cs",
            "Assets/_Project/Scripts/Visual/FrontierHeroBakePreview.cs",
            "Assets/_Project/Scripts/Visual/FrontierMesaPolishPreview.cs",
            "Assets/_Project/Scripts/Visual/FrontierPracticalBakePreview.cs",
            "Assets/_Project/Scripts/Visual/TerrainT1HeightCollision.cs",
            "Assets/_Project/Scripts/Sandbox/Landscape/LandscapeChunkBuilder.cs",
            "Assets/_Project/Scripts/Sandbox/Landscape/LandscapeGroundPaint.cs",
            "Assets/_Project/Scripts/Sandbox/Landscape/LandscapePalette.cs",
            "Assets/_Project/Scripts/Sandbox/Landscape/LandscapePropSpawner.cs",
            "Assets/_Project/Scripts/Sandbox/Landscape/ProceduralLandscape.cs",
            "Assets/_Project/Scripts/Sandbox/SandboxSurface.cs"
        };

        // Old E1/E1b rocks are removed from the scene FIRST, after which their
        // unused standalone assets can be identified safely by the dependency scan.
        private static readonly string[] RetiredE1Files =
        {
            "Assets/_Project/Art/Terrain/SandboxGrid.mat",
            "Assets/_Project/Art/Terrain/SandboxGround.mat",
            "Assets/_Project/Art/Terrain/SkyboxDay.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Ground.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Ground.terrainlayer",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_GroundColor.png",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Kestrel_Hull.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Kestrel_Panels.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Kestrel_Upper.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Rock_1.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Rock_2.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B/E1B_Rock_3.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Models/Chunk_Broad.obj",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Models/Chunk_Ledge.obj",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Models/Chunk_Upright.obj",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Rock_A_Low.asset",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Rock_B_Upright.asset",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/Rock_C_Slab.asset",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Rock1.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Rock2.mat",
            "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Rock3.mat"
        };

        private static readonly string[] OldRootFiles =
        {
            "ENVIRONMENT_ART_RESTART.md", "Tools/Unity/ARTLAB.md",
            "Docs/Production/CLEANUP_A.md"
        };

        private static readonly string[] OldRawFiles =
        {
            "Resources/Blender/ForestHybridPack.blend",
            "Resources/Blender/ForestMassPack.blend",
            "Resources/Blender/FrontierBakeTruth.blend",
            "Resources/Blender/FrontierHeroKit.blend",
            "Resources/Blender/FrontierMesaPolish.blend",
            "Resources/Blender/FrontierPracticalBake.blend",
            "Resources/Blender/FrontierStrata.blend",
            "Resources/Blender/FrontierStrata.json",
            "Resources/Blender/TreePack.blend",
            "Tools/Blender/generate_bake_truth_test.py",
            "Tools/Blender/generate_forest_hybrid_pack.py",
            "Tools/Blender/generate_forest_mass_pack.py",
            "Tools/Blender/generate_frontier_hero_kit.py",
            "Tools/Blender/generate_frontier_mesa_polish.py",
            "Tools/Blender/generate_frontier_practical_bake.py",
            "Tools/Blender/generate_frontier_strata.py",
            "Tools/Blender/generate_rockkit_r1.py",
            "Tools/Blender/generate_tree_pack.py",
            "Tools/Unity/CleanupFrontierStudies.cs",
            "Tools/Unity/ConfigureArtLabScorchedFrontier.cs",
            "Tools/Unity/ConfigureFrontierBakeTruth.cs",
            "Tools/Unity/ConfigureFrontierMesaPolish.cs",
            "Tools/Unity/ConfigureFrontierPracticalBake.cs",
            "Tools/Unity/ConfigureSandboxAfterburner.cs",
            "Tools/Unity/ConfigureSandboxAircraftShadows.cs",
            "Tools/Unity/ConfigureSandboxBattlefield.cs",
            "Tools/Unity/ConfigureSandboxFlight.cs",
            "Tools/Unity/ConfigureSandboxKenneyLandscape.cs",
            "Tools/Unity/ConfigureSandboxKestrel.cs",
            "Tools/Unity/ConfigureSandboxLandscape.cs",
            "Tools/Unity/ConfigureSandboxMissiles.cs",
            "Tools/Unity/ConfigureSandboxTurrets.cs",
            "Tools/Unity/MigrateArtLayout.cs"
        };

        private sealed class Audit
        {
            public readonly List<string> Delete = new List<string>();
            public readonly List<string> Retain = new List<string>();
        }

        [MenuItem("Air Combat/Maintenance/Cleanup B - Preview")]
        private static void Preview()
        {
            try
            {
                EnsureReady();
                var audit = BuildAudit();
                Debug.Log($"Cleanup B PREVIEW: {audit.Delete.Count} deletable Unity assets; " +
                    $"{audit.Retain.Count} protected references. No changes made.\n" +
                    "Delete candidates:\n" + string.Join("\n", audit.Delete) +
                    "\nProtected (will NOT delete):\n" + string.Join("\n", audit.Retain));
                EditorUtility.DisplayDialog("Cleanup B preview",
                    $"{audit.Delete.Count} deletable old Unity assets. " +
                    $"{audit.Retain.Count} still referenced and protected. " +
                    "See full paths in Unity Console. No files changed.", "OK");
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Air Combat/Maintenance/Cleanup B - Apply")]
        private static void Apply()
        {
            try
            {
                EnsureReady();
                var audit = BuildAudit();
                if (!EditorUtility.DisplayDialog("Cleanup B — delete retired experiments?",
                    $"Delete up to {audit.Delete.Count} Unity assets plus legacy source/docs, " +
                    "and remove disabled E1/E1b rock groups.\n\n" +
                    "Main map, two combat regression scenes, and referenced prefabs are protected. " +
                    "All removals stay recoverable from Git. Save/commit green first.",
                    "Apply cleanup", "Cancel")) return;

                var scene = EditorSceneManager.OpenScene(Map, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                int removedRoots = 0;
                foreach (string name in new[]
                {
                    "E1 / Sparse rock groups - move freely", "E1B / Chunky formations"
                })
                {
                    var root = roots.FirstOrDefault(x => x.name == name);
                    if (root == null) continue;
                    if (root.activeSelf)
                        throw new InvalidOperationException("Legacy rock group was reactivated: " + name);
                    UnityEngine.Object.DestroyImmediate(root);
                    removedRoots++;
                }
                if (removedRoots > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Unable to save cleaned main map.");
                }

                // Inactive rock groups no longer keep obsolete meshes/materials alive.
                audit = BuildAudit();
                int removed = 0;
                bool selfDeleted = false;
                // Old source scripts are deleted below. Prevent an automatic
                // script recompile/domain reload half-way through the operation.
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (string path in audit.Delete)
                        {
                            if (AssetDatabase.DeleteAsset(path)) removed++;
                            else Debug.LogWarning("Cleanup B could not remove: " + path);
                        }
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                    PruneEmptyFolders();

                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(Map)))
                        throw new InvalidOperationException("Main map GUID vanished! Stop and inspect repository.");
                    EditorBuildSettings.scenes = new[]
                    {
                        new EditorBuildSettingsScene(Map, true),
                        new EditorBuildSettingsScene(WeaponLab, false),
                        new EditorBuildSettingsScene(GroundLab, false)
                    };
                    AssetDatabase.SaveAssets();

                    ReplaceCurrentDocs();
                    DeleteOldDocsAndRawSources(audit.Retain);

                    // Last: the one-shot cleanup tool removes its own menu.
                    selfDeleted = AssetDatabase.DeleteAsset(ThisScript);
                }
                finally { AssetDatabase.AllowAutoRefresh(); }
                AssetDatabase.Refresh();
                Debug.Log($"Cleanup B applied: {removed} old Unity assets, {removedRoots} disabled rock roots. " +
                    "Current build scene: ArcadeDesertE1. One-shot editor command removed: " + selfDeleted +
                    ". Check git status; test map, WeaponLab and GroundAttackLab before green.");
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private static void EnsureReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Unity Play Mode before Cleanup B.");
            foreach (Scene scene in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt))
                if (scene.isDirty)
                    throw new InvalidOperationException("Save/discard unsaved scenes first: " + scene.name);
            string docs = Path.Combine(ProjectRoot, "Docs/Production/CleanupBFinal");
            foreach (string name in new[]
            {
                "README.md", "HANDOFF.md", "GAMEPLAY_ARCHITECTURE.md",
                "ART_PIPELINE.md", "ART_AUTHORING.md", "THIRD_PARTY_ASSETS.md",
                "GAMEPLAY_COMBAT.md", "LIGHTING_L1.md"
            })
                if (!File.Exists(Path.Combine(docs, name)))
                    throw new InvalidOperationException("Missing cleanup document template: " + name);
            foreach (string path in new[] { Map, WeaponLab, GroundLab, CameraPrefab })
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                    throw new InvalidOperationException("Required kept asset not found: " + path);
            var mapScene = EditorSceneManager.OpenScene(Map, OpenSceneMode.Single);
            var camera = mapScene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<ArcadeCameraFollow>(true)).FirstOrDefault();
            if (camera == null || camera.GetComponent<CombatCameraRig>() == null)
                throw new InvalidOperationException("Cleanup A camera migration was not committed in main map.");
            if (mapScene.GetRootGameObjects().All(x => x.GetComponentInChildren<E1MapFlightSession>(true) == null))
                throw new InvalidOperationException("Main map flight session missing.");
        }

        private static Audit BuildAudit()
        {
            var activeRoots = new List<string> { Map, WeaponLab, GroundLab, CameraPrefab };
            activeRoots.AddRange(AssetDatabase.GetAllAssetPaths()
                .Where(x => x.StartsWith("Assets/_Project/Prefabs/", StringComparison.Ordinal) &&
                            x.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)));
            // Avoid deleting working legacy camera dependencies required by the
            // two labs. Source references are checked separately below.
            var protectedAssets = new HashSet<string>(
                AssetDatabase.GetDependencies(activeRoots.Distinct().ToArray(), true),
                StringComparer.OrdinalIgnoreCase);
            foreach (string root in activeRoots) protectedAssets.Add(root);

            var audit = new Audit();
            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(x => x.StartsWith("Assets/", StringComparison.Ordinal) &&
                            !AssetDatabase.IsValidFolder(x)).Distinct().ToArray();
            foreach (string path in paths)
            {
                bool oldScene = OldScenes.Any(n =>
                    path == "Assets/_Project/Scenes/" + n + ".unity");
                bool oldFolder = OldAssetFolders.Any(f => path.StartsWith(f + "/", StringComparison.Ordinal));
                bool oldE1 = RetiredE1Files.Contains(path);
                bool oldEditor = OldScripts.Contains(path);
                if (!(oldScene || oldFolder || oldE1 || oldEditor)) continue;
                bool licenseForKeptAsset = path.EndsWith("/License.txt", StringComparison.OrdinalIgnoreCase) &&
                    protectedAssets.Any(dep => dep.StartsWith(path.Substring(0, path.LastIndexOf('/') + 1),
                        StringComparison.OrdinalIgnoreCase));
                if (protectedAssets.Contains(path) || licenseForKeptAsset) audit.Retain.Add(path);
                else audit.Delete.Add(path);
            }
            audit.Delete.Sort(StringComparer.Ordinal);
            audit.Retain.Sort(StringComparer.Ordinal);
            return audit;
        }

        private static void PruneEmptyFolders()
        {
            var standalone = new[]
            {
                "Assets/_Project/Art/Environment/ArcadeDesertE1/E1B",
                "Assets/_Project/Art/Environment/ArcadeDesertE1/Models",
                "Assets/_Project/Scripts/Sandbox",
                "Assets/_Project/Art/Nature"
            };
            // Remove empty nested subfolders too (e.g. Kenney/Textures and
            // old ScorchedFrontier/Modules) while preserving nonempty branches.
            var folders = AssetDatabase.GetAllAssetPaths()
                .Where(AssetDatabase.IsValidFolder)
                .Where(p => OldAssetFolders.Any(root => p == root ||
                             p.StartsWith(root + "/", StringComparison.Ordinal)) ||
                            standalone.Any(root => p == root ||
                             p.StartsWith(root + "/", StringComparison.Ordinal)))
                .OrderByDescending(x => x.Length).ToArray();
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                if (!AssetDatabase.GetAllAssetPaths().Any(p =>
                    p.StartsWith(folder + "/", StringComparison.Ordinal)))
                    AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void ReplaceCurrentDocs()
        {
            var finalDir = Path.Combine(ProjectRoot, "Docs/Production/CleanupBFinal");
            foreach (string name in new[]
            {
                "README.md", "HANDOFF.md", "GAMEPLAY_ARCHITECTURE.md",
                "ART_PIPELINE.md", "ART_AUTHORING.md", "THIRD_PARTY_ASSETS.md",
                "GAMEPLAY_COMBAT.md", "LIGHTING_L1.md"
            })
            {
                string template = Path.Combine(finalDir, name);
                if (!File.Exists(template))
                    throw new InvalidOperationException("Missing Cleanup B documentation template: " + template);
                File.Copy(template, Path.Combine(ProjectRoot, name), true);
                File.Delete(template);
            }
            if (Directory.Exists(finalDir) && !Directory.EnumerateFileSystemEntries(finalDir).Any())
                Directory.Delete(finalDir);
        }

        private static void DeleteOldDocsAndRawSources(List<string> keptAssets)
        {
            string root = ProjectRoot;
            var files = OldRootFiles.Concat(OldRawFiles);
            foreach (string relative in files)
            {
                // Keep editable source + generator when an existing combat prefab
                // still depends on a matching historical imported-art family.
                string family = relative.Contains("FrontierHeroKit") ? "FrontierHeroKit" :
                    relative.Contains("FrontierStrata") ? "FrontierStrata" :
                    relative.Contains("FrontierBakeTruth") || relative.Contains("bake_truth") ? "FrontierBakeTruth" :
                    relative.Contains("FrontierMesaPolish") || relative.Contains("mesa_polish") ? "FrontierMesaPolish" :
                    relative.Contains("FrontierPracticalBake") || relative.Contains("practical_bake") ? "FrontierPracticalBake" :
                    relative.Contains("ForestHybrid") || relative.Contains("forest_hybrid") ? "ForestHybrid" :
                    relative.Contains("ForestMass") || relative.Contains("forest_mass") ? "ForestMasses" :
                    relative.Contains("TreePack") || relative.Contains("tree_pack") ? "Trees" : "";
                if (!string.IsNullOrEmpty(family) &&
                    keptAssets.Any(a => a.Contains("/" + family + "/"))) continue;
                string full = Path.Combine(root, relative);
                if (File.Exists(full)) File.Delete(full);
            }
            foreach (string directory in new[] { "Docs/Art", "Docs/Archive", "Tools/Blender/__pycache__" })
            {
                string full = Path.Combine(root, directory);
                if (Directory.Exists(full)) Directory.Delete(full, true);
            }
        }
    }
}
#endif
