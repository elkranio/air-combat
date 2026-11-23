#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // E1c is a non-destructive edit of the already committed E1b scene.
    // The single giant plateau is a real, editable OBJ source, not a scaled rock.
    // Never rerun E1/E1b or touch shared player/lighting/combat prefabs.
    public static class UpgradeArcadeMapE1C
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ArtRoot = "Assets/_Project/Art/Environment/ArcadeDesertE1";
        private const string Output = ArtRoot + "/E1C";
        private const string PlateauObj = ArtRoot + "/Models/MainPlateau_E1C.obj";
        private const string RockParent = "E1B / Chunky formations";
        private const string PlateauName = "E1C / Main plateau - move or remodel";
        private const float TestAltitude = 150f;

        private static readonly string[] PaintNames =
            { "Kestrel_Hull", "Kestrel_Upper", "Kestrel_Panels" };
        private static readonly string[] PaintColors =
            { "#31536B", "#19364B", "#E07940" };

        [MenuItem("Air Combat/Art/E1c - Install Big Plateau + Altitude Test")]
        private static void Menu()
        {
            try { Debug.Log(Build()); }
            catch (Exception error) { Debug.LogError("E1c upgrade failed: " + error); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before E1c.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before E1c.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("E1 scene missing; do not rerun its builder.");
            if (AssetDatabase.IsValidFolder(Output) || Directory.Exists(Output))
                throw new InvalidOperationException("E1c already installed (or interrupted). Inspect scene/" + Output + " first; do not overwrite.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlateauObj);
            var modelMesh = model != null ? model.GetComponentInChildren<MeshFilter>(true)?.sharedMesh : null;
            if (modelMesh == null)
                throw new InvalidOperationException("Missing imported E1c OBJ: " + PlateauObj);
            Vector3 bounds = modelMesh.bounds.size;
            if (bounds.x < 350f || bounds.x > 520f || bounds.z < 210f || bounds.z > 380f ||
                bounds.y < 20f || bounds.y > 45f)
                throw new InvalidOperationException("Plateau OBJ bounds not in world units: " + bounds);
            var sourcePaint = PaintNames.Select(name => Require<Material>(
                ArtRoot + "/E1B/E1B_" + name + ".mat")).ToArray();
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
                throw new InvalidOperationException("URP/Lit shader unavailable.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var terrainRoot = roots.Single(x => x.name == "E1 / Terrain - sculpt here later");
            var oldSmallRocks = roots.Single(x => x.name == "E1 / Sparse rock groups - move freely");
            var oldOutcrops = roots.Single(x => x.name == RockParent);
            var jet = roots.Single(x => x.name == "E1 / PlayerJet (current model)");
            var camera = roots.Single(x => x.name == "E1 / Main Camera");
            var session = roots.SelectMany(x => x.GetComponentsInChildren<E1MapFlightSession>(true)).Single();
            var terrain = terrainRoot.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null || terrain.GetComponent<TerrainCollider>() == null)
                throw new InvalidOperationException("E1 terrain or collider missing; inspect the authored scene.");
            if (roots.Any(x => x.name == PlateauName))
                throw new InvalidOperationException("E1c already exists in this scene.");
            var renderers = jet.GetComponentsInChildren<Renderer>(true);
            foreach (Material mat in sourcePaint)
                if (!renderers.Any(r => r.sharedMaterials.Contains(mat)))
                    throw new InvalidOperationException("E1b Kestrel material missing from player: " + mat.name);

            // New materials only. Do not recolor the approved muted TerrainLayer
            // or modify shared aircraft chassis materials and other scenes.
            AssetDatabase.CreateFolder(ArtRoot, "E1C");
            var cliffMat = CreateMat("E1C_Plateau", "#968A7A", 0.02f);
            var newPaint = new Material[sourcePaint.Length];
            for (int i = 0; i < newPaint.Length; i++)
            {
                var material = new Material(sourcePaint[i]) { name = "E1C / " + PaintNames[i] };
                material.SetColor("_BaseColor", Parse(PaintColors[i]));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", .12f);
                AssetDatabase.CreateAsset(material, Output + "/E1C_" + PaintNames[i] + ".mat");
                newPaint[i] = material;
            }

            // Keep the old experiments in their original scene roots but stop
            // them rendering. No tiny rocks or toy-sized outcrops in this pass.
            oldSmallRocks.SetActive(false);
            oldOutcrops.SetActive(false);

            var plateau = new GameObject(PlateauName);
            SceneManager.MoveGameObjectToScene(plateau, scene);
            var position = new Vector3(110f, 0f, 135f);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            plateau.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, -11f, 0f));
            plateau.AddComponent<MeshFilter>().sharedMesh = modelMesh;
            var renderer = plateau.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cliffMat;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            plateau.AddComponent<MeshCollider>().sharedMesh = modelMesh;

            // Spawn initially at 150. Height comparison is a scene-only runtime
            // option; it does not change flight altitude in either combat lab.
            float deltaHeight = TestAltitude - jet.transform.position.y;
            jet.transform.position += Vector3.up * deltaHeight;
            camera.transform.position += Vector3.up * deltaHeight;
            PrefabUtility.RecordPrefabInstancePropertyModifications(jet.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            session.EnableAltitudeComparison();
            EditorUtility.SetDirty(session);

            int slots = 0;
            foreach (var part in renderers)
            {
                var paint = part.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < paint.Length; i++)
                for (int j = 0; j < sourcePaint.Length; j++)
                {
                    if (paint[i] != sourcePaint[j]) continue;
                    paint[i] = newPaint[j];
                    changed = true;
                    slots++;
                    break;
                }
                if (!changed) continue;
                part.sharedMaterials = paint;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save E1c map scene.");
            Selection.activeGameObject = plateau;
            return $"E1c ready in {ScenePath}: one 430x275m/30m plateau, {slots} new aircraft paint slots, Y150 start. " +
                   "Play: 7/8/9 compare Y78/Y110/Y150; R reset. Do not rerun any E1 builders.";
        }

        private static Material CreateMat(string name, string html, float smoothness)
        {
            var result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            result.SetColor("_BaseColor", Parse(html));
            result.SetFloat("_Metallic", 0f);
            result.SetFloat("_Smoothness", smoothness);
            result.enableInstancing = true;
            AssetDatabase.CreateAsset(result, Output + "/" + name + ".mat");
            return result;
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("Missing asset: " + path);

        private static Color Parse(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out Color color))
                throw new ArgumentException("Invalid color " + html);
            return color;
        }
    }
}
#endif
