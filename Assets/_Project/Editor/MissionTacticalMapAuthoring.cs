using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One explicit binding command for the current authored marker hierarchy.
    // Future missions can configure TacticalMapView directly in the inspector.
    internal static class MissionTacticalMapAuthoring
    {
        [MenuItem("Air Combat/Missions/Install Tactical Map")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                Scene scene=SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name!="Mission01_FirstSortie") throw new InvalidOperationException("Open Mission01_FirstSortie.");
                string guid=AssetDatabase.AssetPathToGUID(scene.path);
                var data=AssetDatabase.FindAssets("t:MissionMapData",new[]{"Assets/_Project/Generated/MissionMaps"})
                    .Select(g=>AssetDatabase.LoadAssetAtPath<MissionMapData>(AssetDatabase.GUIDToAssetPath(g)))
                    .SingleOrDefault(d=>d!=null && d.SourceSceneGuid==guid);
                if (data==null || data.Geography==null) throw new InvalidOperationException("Bake the current mission map first.");
                var roots=scene.GetRootGameObjects();
                var launch=roots.SelectMany(g=>g.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var player=new SerializedObject(launch).FindProperty("flight").objectReferenceValue as FlightController;
                if (player==null) throw new InvalidOperationException("Missing mission player.");
                var plan=roots.Single(g=>g.name=="Mission01 / Objectives").transform.Find("M01 / Rock layout v3 (visual only)");
                if (plan==null) throw new InvalidOperationException("Missing current v3 objective plan.");
                var samRoot=plan.Find("01 / SAM sites - visual only");
                var reconRoot=plan.Find("02 / Recon zone - empty markers");
                var extra=plan.Find("03 / Optional objective - empty marker");
                if(samRoot==null || reconRoot==null || extra==null) throw new InvalidOperationException("Incomplete mission markers.");
                var sites=samRoot.Cast<Transform>().Select(t=>{
                    var match=Regex.Match(t.name,@"^(SAM [A-Z]) - visual \(R([0-9]+)\)$");
                    if(!match.Success) throw new InvalidOperationException("Cannot read planned radius from "+t.name);
                    return new TacticalMapView.PlannedSam{site=t,label=match.Groups[1].Value,radius=float.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)};
                }).ToArray();
                var targets=reconRoot.Cast<Transform>().ToArray();
                if(sites.Length!=6 || targets.Length!=3) throw new InvalidOperationException("Unexpected marker count; inspect scene.");
                var existing=roots.SelectMany(g=>g.GetComponentsInChildren<TacticalMapView>(true)).ToArray();
                if(existing.Length>1) throw new InvalidOperationException("Multiple tactical map views.");
                var view=existing.Length==1?existing[0]:Undo.AddComponent<TacticalMapView>(launch.gameObject);
                Undo.RecordObject(view,"Bind tactical map");
                view.ConfigureForAuthoring(data,player.transform,sites,targets,extra,"FIRST SORTIE");
                EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorSceneManager.MarkSceneDirty(scene); Selection.activeObject=view;
                Debug.Log("MAP-02: tactical map installed. Tab opens/closes in Play Mode. Save scene after review.");
            }
            catch(Exception ex){Debug.LogError("Install Tactical Map: "+ex.Message);}
        }
    }
}
