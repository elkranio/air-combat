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

        [MenuItem("Air Combat/Tools/Audit scene reachability (CLEAN-03C)")]
        private static void AuditSceneReachability()
        {
            var all = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !AssetDatabase.IsValidFolder(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var prefabs = all.Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
            var roots = all.Where(p => !IsReviewScope(p) || IsProtected(p)).ToArray();
            var live = DependencyClosure(roots);
            var withPrefabs = DependencyClosure(roots.Concat(prefabs).Distinct().ToArray());
            var unused = all.Where(p => IsReviewScope(p) && !live.Contains(p)).ToArray();
            var unusedPrefabs = unused.Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
            var prefabOnly = unused.Where(p => !p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && withPrefabs.Contains(p)).ToArray();
            var orphan = unused.Where(p => !withPrefabs.Contains(p)).ToArray();
            var report = new List<string> {
                "CLEAN-03C - scene reachability audit (READ ONLY; NOT deletion proof)",
                "All scenes (including disabled), assets outside review scope, Resources, StreamingAssets, shaders and settings are roots.",
                "Review scope: _Project/Art, _Project/Generated, _Project/Prefabs, ThirdParty.",
                "Review future content, model sidecars, licenses, dynamic loads and authoring inputs before deletion.",
                $"Imported paths: {all.Length}; roots: {roots.Length}; protected dependency paths: {live.Count}",
                $"Unreachable prefabs: {unusedPrefabs.Length}; prefab-only assets: {prefabOnly.Length}; orphan candidates: {orphan.Length}",
                "", "SCENE ROOTS"
            };
            report.AddRange(roots.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)));
            var reverse = unused.ToDictionary(p => p, p => new List<string>(), StringComparer.Ordinal);
            foreach (string source in all)
                foreach (string dep in AssetDatabase.GetDependencies(source, false))
                    if (dep != source && reverse.TryGetValue(dep, out var owners)) owners.Add(source);
            AppendSection(report, "A / UNREACHABLE PREFABS", unusedPrefabs, reverse);
            AppendSection(report, "B / ASSETS RETAINED ONLY BY UNREACHABLE PREFABS", prefabOnly, reverse);
            AppendSection(report, "C / ASSETS OUTSIDE ALL SCENE AND PREFAB DEPENDENCY CLOSURES", orphan, reverse);
            Directory.CreateDirectory("Temp");
            const string path = "Temp/AirCombat-scene-reachability.txt";
            File.WriteAllLines(path, report);
            Debug.Log($"CLEAN-03C: {unusedPrefabs.Length} unreachable prefabs, {prefabOnly.Length} prefab-only assets, {orphan.Length} orphan candidates. Report: {path}. Nothing deleted.");
        }

        private static bool IsReviewScope(string path) =>
            path.StartsWith("Assets/_Project/Art/", StringComparison.Ordinal) ||
            path.StartsWith("Assets/_Project/Generated/", StringComparison.Ordinal) ||
            path.StartsWith("Assets/_Project/Prefabs/", StringComparison.Ordinal) ||
            path.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal);

        private static bool IsProtected(string path) =>
            path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("/StreamingAssets/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("/Settings/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".shadervariants", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase);

        private static HashSet<string> DependencyClosure(string[] roots)
        {
            var result = new HashSet<string>(roots, StringComparer.Ordinal);
            if (roots.Length > 0) result.UnionWith(AssetDatabase.GetDependencies(roots, true));
            return result;
        }

        private static void AppendSection(List<string> report, string title, string[] paths,
            Dictionary<string, List<string>> reverse)
        {
            report.Add("");
            report.Add(title + " (" + paths.Length + ")");
            foreach (string path in paths)
            {
                long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                report.Add($"{path} | {bytes} bytes | guid={AssetDatabase.AssetPathToGUID(path)}");
                foreach (string owner in reverse[path].Distinct().OrderBy(p => p, StringComparer.Ordinal))
                    report.Add("    <- " + owner);
            }
        }
    }
}
#endif
