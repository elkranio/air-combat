#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // Read-only audit: no import, scene modification or asset deletion.
    public static class AssetDependencyAudit
    {
        [MenuItem("Air Combat/Tools/Audit unreferenced art assets")]
        private static void Audit()
        {
            var all = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)).ToArray();
            // Keep every scene and prefab, including disabled labs and assets not
            // yet instantiated. Resources can be loaded by name at runtime.
            var roots = all.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("/Resources/") || p.StartsWith("Assets/Settings/", StringComparison.Ordinal) ||
                p.StartsWith("Assets/_Project/Settings/", StringComparison.Ordinal) ||
                p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".shadervariants", StringComparison.OrdinalIgnoreCase)).ToArray();
            var used = new HashSet<string>(AssetDatabase.GetDependencies(roots, true), StringComparer.Ordinal);
            foreach (string root in roots) used.Add(root);
            var candidates = all.Where(p => (p.StartsWith("Assets/_Project/Art/", StringComparison.Ordinal) ||
                p.StartsWith("Assets/_Project/Generated/", StringComparison.Ordinal) ||
                p.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal)) && !used.Contains(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string dir = "Temp";
            Directory.CreateDirectory(dir);
            string report = Path.Combine(dir, "AirCombat-asset-audit.txt");
            File.WriteAllLines(report, new[] {
                "Candidate list only — NOT safe-to-delete proof.",
                "All scenes, all prefabs, Resources, project render settings and shaders are protected roots.",
                "Dynamic path loads, imported model sidecars, licenses and future authoring sources still need manual review.",
                $"Roots: {roots.Length}; protected dependency paths: {used.Count}; candidates: {candidates.Length}", ""
            }.Concat(candidates));
            Debug.Log($"Air Combat asset audit: {candidates.Length} candidates. Review {report}. Nothing deleted.");
        }
    }
}
#endif
