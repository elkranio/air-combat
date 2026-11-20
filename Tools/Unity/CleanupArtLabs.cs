using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time migration of the accepted V13. All asset moves preserve GUIDs.
// Preview and full dependency preflight before deleting anything. Never regenerate the map here.
public static class CleanupArtLabs
{
    private const string Scenes = "Assets/_Project/Scenes/";
    private const string Generated = "Assets/_Project/Generated/";
    private const string Frontier = Scenes + "ScorchedFrontier.unity";
    private const string Materials = "Assets/_Project/Art/Aircraft/FrontierMaterials";
    private static readonly (string from,string to)[] Moves = {
        (Scenes+"ArtLabV13ScorchedFrontier.unity",Frontier),
        (Generated+"ArtLabV13ScorchedFrontier",Generated+"ScorchedFrontier"),
        (Generated+"ArtLabV8Lighting/ArtLabLighting_URP.asset","Assets/Settings/ScorchedFrontier_URP.asset"),
        (Generated+"ArtLabV11Readability/V11_Kestrel_Hull.mat",Materials+"/Kestrel_Hull.mat"),
        (Generated+"ArtLabV11Readability/V11_Kestrel_Upper.mat",Materials+"/Kestrel_Upper.mat"),
        (Generated+"ArtLabV11Readability/V11_Kestrel_Panels.mat",Materials+"/Kestrel_Panels.mat")
    };
    private static readonly string[] OldNames = {"ArtLab","ArtLabV2","ArtLabV3","ArtLabV3AntiTiling","ArtLabV4Trees",
        "ArtLabV5Forest","ArtLabV6Canopy","ArtLabV7Hybrid","ArtLabV8Lighting","ArtLabV9Terrain","ArtLabV10LandscapeFlight",
        "ArtLabV11Readability","ArtLabV12FocusStudy"};
    private static readonly string[] OldScripts = {"ArtLabPalettePreview","ArtLabLightingPreview","ArtLabDirectionalLightingPreview",
        "ArtLabForestInstances","ArtLabFocusStudy"};
    private static readonly string[] OldTools = {"ConfigureArtLab","ConfigureArtLabV2","ConfigureArtLabGround","ConfigureArtLabTrees",
        "ConfigureArtLabForest","ConfigureArtLabForestMasses","ConfigureArtLabForestHybrid","ConfigureArtLabLighting",
        "ConfigureArtLabTerrain","ConfigureArtLabLandscapeFlight","ConfigureArtLabReadability","ConfigureArtLabFocusStudy"};
    private static IEnumerable<string> Retired => OldNames.Select(n=>Scenes+n+".unity")
        .Concat(OldNames.Select(n=>Generated+n))
        .Concat(OldScripts.Select(n=>"Assets/_Project/Scripts/Visual/"+n+".cs"))
        .Concat(new[]{"Assets/_Project/Shaders/ArtLabAircraftOutline.shader"});
    private static bool Exists(string p)=>File.Exists(p)||Directory.Exists(p);
    private static bool Inside(string p,string root)=>p==root||p.StartsWith(root+"/",StringComparison.Ordinal);
    private static bool IsRetired(string path)=>Retired.Any(p=>Inside(path,p))&&!Moves.Any(m=>Inside(path,m.from)||Inside(path,m.to));
    private static void CheckEditor()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save current scene changes first.");
        if(!Exists(Scenes+"Sandbox.unity"))throw new InvalidOperationException("Sandbox is missing.");
        foreach(var m in Moves)
        {
            bool from=Exists(m.from),to=Exists(m.to);
            if(from==to)throw new InvalidOperationException("Expected exactly one source or destination for: "+m.from+" -> "+m.to);
        }
    }
    private static void CheckDependencies()
    {
        // Do not silently remove any configured build scene.
        foreach(var s in EditorBuildSettings.scenes)
            if(IsRetired(s.path))throw new InvalidOperationException("Retired scene still listed in build settings: "+s.path);
        var blockers=new List<string>();
        foreach(var path in AssetDatabase.GetAllAssetPaths())
        {
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||IsRetired(path)||AssetDatabase.IsValidFolder(path))continue;
            var bad=AssetDatabase.GetDependencies(path,true).Where(IsRetired).Distinct().ToArray();
            if(bad.Length>0)blockers.Add(path+" -> "+string.Join(", ",bad));
        }
        if(blockers.Count>0)throw new InvalidOperationException("Retained assets still depend on retired ArtLabs. Nothing deleted.\n"+string.Join("\n",blockers.Take(30)));
    }
    public static string Preview()
    {
        CheckEditor();CheckDependencies();
        var pending=Moves.Where(m=>Exists(m.from)).ToArray();
        var paths=Retired.Where(Exists).Distinct().ToArray();
        var tools=OldTools.Select(n=>"Tools/Unity/"+n+".cs").Where(File.Exists).ToArray();
        long bytes=0;
        foreach(var p in paths)
        {
            if(Directory.Exists(p))foreach(var f in Directory.GetFiles(p,"*",SearchOption.AllDirectories))
            {if(!IsRetired(f.Replace('\\','/')))continue;bytes+=new FileInfo(f).Length;}
            else if(File.Exists(p))bytes+=new FileInfo(p).Length;
        }
        return $"Preflight OK. {pending.Length} GUID-preserving moves; {paths.Length} retired asset roots/files; {tools.Length} old editor tools; approx {bytes/1048576d:F1} MiB removed. Sandbox and source models retained. No changes made.";
    }
    public static string Configure()
    {
        var plan=Preview();
        // Close retired scenes before moving/removing assets; no saving or rewriting Sandbox.
        EditorSceneManager.OpenScene(Scenes+"Sandbox.unity",OpenSceneMode.Single);
        if(!AssetDatabase.IsValidFolder(Materials))AssetDatabase.CreateFolder("Assets/_Project/Art/Aircraft","FrontierMaterials");
        foreach(var m in Moves)
        {
            if(!Exists(m.from))continue;
            var error=AssetDatabase.MoveAsset(m.from,m.to);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException("Move failed: "+m.from+" -> "+m.to+": "+error);
        }
        // Recheck with actual migrated paths. Do not delete on a dependency failure.
        CheckDependencies();
        // Delete scenes before their generated assets and scripts. Assets are explicitly allowlisted.
        int deleted=0;
        foreach(var path in Retired.Where(Exists).Distinct().ToArray())
        {
            if(!AssetDatabase.DeleteAsset(path))throw new InvalidOperationException("Could not delete "+path);
            deleted++;
        }
        foreach(var n in OldTools)
        {
            string path="Tools/Unity/"+n+".cs";
            if(File.Exists(path))File.Delete(path);
            if(File.Exists(path+".meta"))File.Delete(path+".meta");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Frontier,OpenSceneMode.Single);
        AssetDatabase.Refresh();
        return plan+" Applied: "+deleted+" retired roots/files deleted; ScorchedFrontier opened. Review git status and playtest both scenes before committing.";
    }
}
