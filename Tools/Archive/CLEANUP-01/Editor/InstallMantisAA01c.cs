#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    public static class InstallMantisAA01c
    {
        [MenuItem("Air Combat/Mission/AA-01c - Arm Mantis patrol")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/ArcadeDesertE1.unity");
            if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("Open ArcadeDesertE1 first."); return; }
            var units = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GroundPatrolMotor>(true))
                .Where(x => x.gameObject.name.Contains("Mantis")).ToArray();
            if (units.Length == 0) { Debug.LogError("Install Mantis patrol first."); return; }
            foreach (var unit in units)
            {
                var all = unit.GetComponentsInChildren<Transform>(true);
                Transform yaw = all.FirstOrDefault(x => x.name == "TurretYaw");
                Transform pitch = all.FirstOrDefault(x => x.name == "GunPitch");
                Transform left = all.FirstOrDefault(x => x.name == "Muzzle_L");
                Transform right = all.FirstOrDefault(x => x.name == "Muzzle_R");
                if (yaw == null || pitch == null || left == null || right == null)
                { Debug.LogError("Mantis gun sockets missing.", unit); continue; }
                var gun = unit.GetComponent<MantisGun>();
                if (gun == null) gun = Undo.AddComponent<MantisGun>(unit.gameObject);
                Undo.RecordObject(gun, "Bind Mantis gun sockets");
                gun.ConfigureArt(yaw, pitch, left, right);
                EditorUtility.SetDirty(gun);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = unit.gameObject;
            }
            Debug.Log("AA-01c: Mantis guns equipped. Save scene; play in Ground Nurs Test mode.");
        }
    }
}
#endif
