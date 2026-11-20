using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time consolidation after V18. No regeneration, material edits or scene serialization.
public static class CleanupFrontierStudies
{
    private const string Scenes = "Assets/_Project/Scenes/";
    private const string Gen = "Assets/_Project/Generated/";
    private const string Surface = Gen + "FrontierSurfacePass";
    private const string Escarpments = Gen + "FrontierEscarpments";
    private const string Strata = Gen + "FrontierStrata";
    private const string OldArt = "Assets/_Project/Art/Environment/FrontierEscarpments";
    private const string Active = Scenes + "FrontierStrata.unity";
    private static readonly string[] KeepScenes = { Scenes+"Sandbox.unity", Scenes+"ScorchedFrontier.unity", Active, Scenes+"FrontierBakeTruth.unity" };
    private static readonly string[] RetiredScenes = { Scenes+"FrontierHeroShowcase.unity", Scenes+"FrontierSurfacePass.unity", Scenes+"FrontierEscarpments.unity" };
    private static readonly (string from, string to)[] Moves = new[]{
        "DustShoulder.mat", "Industrial_spur_UV.asset", "Industrial_spur_shoulder_UV.asset",
        "Main_highway_UV.asset", "Main_highway_shoulder_UV.asset", "RoadShoulder.png",
        "WornAsphalt.mat", "WornAsphalt.png"
    }.Select(n=>(Surface+"/"+n,Strata+"/"+n)).Concat(new[]{
        (Escarpments+"/Ground.mat",Strata+"/Ground.mat"),
        (Escarpments+"/GroundRetouched.png",Strata+"/GroundRetouched.png")
    }).ToArray();
    private static readonly string[] RetiredAssets = RetiredScenes.Concat(new[]{
        Surface+"/Ground.mat", Surface+"/GroundSurface.png", Escarpments+"/Terrain.asset", Escarpments+"/Sandstone.mat",
        OldArt+"/BrokenMesa.fbx", OldArt+"/EastEscarpment.fbx", OldArt+"/SplitRidge.fbx",
        OldArt+"/WesternButte.fbx", OldArt+"/EscarpmentPalette.png"
    }).ToArray();
    private static readonly string[] RetiredFolders = { Surface, Escarpments, OldArt };
    private static readonly string[] RetiredFiles = {
        "Tools/Unity/CleanupArtLabs.cs", "Tools/Unity/ConfigureFrontierHeroKit.cs", "Tools/Unity/ConfigureFrontierSurfacePass.cs",
        "Tools/Unity/ConfigureFrontierEscarpments.cs", "Tools/Unity/ConfigureFrontierStrata.cs",
        "Tools/Blender/generate_frontier_escarpments.py", "Resources/Blender/FrontierEscarpments.blend", "Resources/Blender/FrontierEscarpments.blend1"
    };
    private static bool Exists(string p)=>File.Exists(p)||Directory.Exists(p);
    private static string Normalize(string p)=>p.Replace('\\','/');
    private static bool IsRetired(string p)=>RetiredAssets.Contains(p)||RetiredFolders.Contains(p);

    private static void CheckEditor()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes first.");
        foreach(var p in KeepScenes)if(AssetDatabase.LoadAssetAtPath<SceneAsset>(p)==null)
            throw new InvalidOperationException("Required retained scene missing: "+p);
        if(!AssetDatabase.IsValidFolder(Strata))throw new InvalidOperationException("Missing active V17 output.");
        foreach(var m in Moves)
        {
            bool from=File.Exists(m.from),to=File.Exists(m.to);
            if(from==to)throw new InvalidOperationException("Expected exactly one copy of "+m.from+" or "+m.to);
            string p=from?m.from:m.to;
            if(!File.Exists(p+".meta")||string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p)))
                throw new InvalidOperationException("Missing import/GUID: "+p);
            if(File.Exists((from?m.to:m.from)+".meta"))throw new InvalidOperationException("Unexpected orphan meta for move: "+m.from);
        }
        var allowed=new HashSet<string>(RetiredAssets.Concat(Moves.Select(m=>m.from)),StringComparer.Ordinal);
        // Never recursively delete a directory containing an unexpected file or nested directory.
        foreach(var folder in RetiredFolders.Where(Directory.Exists))
        {
            if(Directory.GetDirectories(folder).Length!=0)throw new InvalidOperationException("Unexpected subfolder in "+folder);
            foreach(var f in Directory.GetFiles(folder).Select(Normalize))
            {
                string asset=f.EndsWith(".meta",StringComparison.Ordinal)?f.Substring(0,f.Length-5):f;
                if(!allowed.Contains(asset))throw new InvalidOperationException("Unexpected file in retired folder: "+f);
            }
        }
        foreach(var b in EditorBuildSettings.scenes)if(IsRetired(b.path))
            throw new InvalidOperationException("Build settings reference retired scene: "+b.path);
    }
    private static void CheckDependencies()
    {
        // Direct dependencies of *every* retained asset are sufficient: any indirect path
        // into the delete set must have a first direct edge from a retained asset.
        // This includes the old paths of assets about to be moved, because they survive.
        var blockers=new List<string>();
        foreach(var p in AssetDatabase.GetAllAssetPaths())
        {
            if(!p.StartsWith("Assets/",StringComparison.Ordinal)||IsRetired(p)||AssetDatabase.IsValidFolder(p))continue;
            foreach(var d in AssetDatabase.GetDependencies(p,false))if(IsRetired(d))blockers.Add(p+" -> "+d);
        }
        if(blockers.Count!=0)throw new InvalidOperationException("Retained dependencies would break. No deletion performed.\n"+string.Join("\n",blockers.Take(30)));
    }
    private static long Bytes(IEnumerable<string> paths)=>paths.Where(File.Exists).Sum(p=>new FileInfo(p).Length);
    public static string Preview()
    {
        CheckEditor();CheckDependencies();
        var deletes=RetiredAssets.Concat(RetiredFiles).Where(File.Exists).ToArray();
        return $"Preflight OK: {Moves.Count(m=>File.Exists(m.from))} GUID-preserving moves, {deletes.Length} retired files, {Bytes(deletes)/1048576d:F1} MiB removed (excluding meta). Keeps Sandbox, V13, V17, V18, hero kit and V17 source. No changes made.";
    }
    public static string Configure()
    {
        string plan=Preview();
        // Close obsolete scenes, open V17 without saving or modifying any scene.
        EditorSceneManager.OpenScene(Active,OpenSceneMode.Single);
        foreach(var m in Moves)
        {
            if(!File.Exists(m.from))continue;
            string guid=AssetDatabase.AssetPathToGUID(m.from);
            string error=AssetDatabase.MoveAsset(m.from,m.to);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException("Move failed: "+m.from+": "+error);
            if(AssetDatabase.AssetPathToGUID(m.to)!=guid)throw new InvalidOperationException("Unexpected GUID change: "+m.to);
        }
        // Recheck the actual state after moves; still no deletions if conflicts appear.
        CheckEditor();CheckDependencies();
        foreach(var p in RetiredAssets)
        {
            if(!Exists(p))continue;
            if(!AssetDatabase.DeleteAsset(p))throw new InvalidOperationException("Cannot delete "+p);
        }
        foreach(var folder in RetiredFolders)
        {
            if(!Directory.Exists(folder))continue;
            if(Directory.GetFileSystemEntries(folder).Length!=0)throw new InvalidOperationException("Retired folder not empty: "+folder);
            if(!AssetDatabase.DeleteAsset(folder))throw new InvalidOperationException("Cannot delete empty folder: "+folder);
        }
        foreach(var p in RetiredFiles)
        {
            if(File.Exists(p))File.Delete(p);
            if(File.Exists(p+".meta"))File.Delete(p+".meta");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Resolve moved references via unchanged GUIDs in a fresh scene load.
        EditorSceneManager.OpenScene(Active,OpenSceneMode.Single);
        return plan+" Applied. V17 reopened; inspect git status and Play Mode V17/V18/Sandbox before committing. No regeneration performed.";
    }
}
