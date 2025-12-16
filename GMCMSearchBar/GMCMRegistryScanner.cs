using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace GMCMSearchBar
{
    internal static class GMCMRegistryScanner
    {
        public static List<IManifest> GetRegisteredModsOrFallback(
            IModHelper helper,
            object gmcmApiObj,
            IMonitor monitor,
            IManifest selfManifest,
            bool includeContentPacks
        )
        {
            // Try reflection first (best quality: only GMCM-registered entries)
            var reflected = TryGetRegisteredModsViaReflection(helper, gmcmApiObj, monitor, includeContentPacks);

            if (reflected.Count > 0)
                return CleanSort(reflected, selfManifest);

            // Fallback: show everything (then entries that fail to open will get removed by SearchMenu)
            monitor.Log("Couldn't locate GMCM registry via reflection; falling back to all loaded mods.", LogLevel.Warn);

            var fallback = helper.ModRegistry
                .GetAll()
                .Select(m => m.Manifest)
                .Where(m => m is not null)
                .Where(m => includeContentPacks || !helper.ModRegistry.Get(m.UniqueID)?.IsContentPack == true)
                .ToList();

            return CleanSort(fallback, selfManifest);
        }

        private static List<IManifest> TryGetRegisteredModsViaReflection(IModHelper helper, object gmcmApiObj, IMonitor monitor, bool includeContentPacks)
        {
            try
            {
                var best = new Candidate();

                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var queue = new Queue<(object Obj, int Depth)>();

                void Enqueue(object? obj, int depth)
                {
                    if (obj is null) return;
                    if (depth > 4) return;
                    if (obj is string) return;

                    if (visited.Add(obj))
                        queue.Enqueue((obj, depth));
                }

                Enqueue(gmcmApiObj, 0);

                while (queue.Count > 0)
                {
                    var (obj, depth) = queue.Dequeue();

                    // dictionary candidates often contain the registration state
                    if (TryExtractManyManifests(helper, obj, out var manifests, includeContentPacks))
                    {
                        var list = manifests.DistinctBy(m => m.UniqueID).ToList();
                        if (list.Count > best.Manifests.Count)
                            best = new Candidate(list);
                    }

                    // walk fields + properties
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    foreach (var f in obj.GetType().GetFields(flags))
                    {
                        object? v;
                        try { v = f.GetValue(obj); } catch { continue; }
                        if (v is null) continue;
                        if (IsLeaf(v)) continue;
                        Enqueue(v, depth + 1);
                    }

                    foreach (var p in obj.GetType().GetProperties(flags))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue;

                        object? v;
                        try { v = p.GetValue(obj); } catch { continue; }
                        if (v is null) continue;
                        if (IsLeaf(v)) continue;
                        Enqueue(v, depth + 1);
                    }
                }

                return best.Manifests;
            }
            catch (Exception ex)
            {
                monitor.Log($"GMCM registry reflection failed: {ex}", LogLevel.Trace);
                return new List<IManifest>();
            }
        }

        private static bool TryExtractManyManifests(IModHelper helper, object obj, out IEnumerable<IManifest> manifests, bool includeContentPacks)
        {
            var results = new List<IManifest>();

            // 1) IDictionary (non-generic)
            if (obj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    AddIfFound(helper, entry.Key, results, includeContentPacks);
                    AddIfFound(helper, entry.Value, results, includeContentPacks);

                    if (entry.Key is string uidKey)
                        AddUid(helper, uidKey, results, includeContentPacks);

                    if (entry.Value is string uidVal)
                        AddUid(helper, uidVal, results, includeContentPacks);
                }
            }

            // 2) IEnumerable of anything
            if (obj is IEnumerable enumerable && obj is not string)
            {
                foreach (var item in enumerable)
                    AddIfFound(helper, item, results, includeContentPacks);
            }

            // 3) object might itself contain a manifest
            AddIfFound(helper, obj, results, includeContentPacks);

            results = results
                .Where(m => m is not null)
                .DistinctBy(m => m.UniqueID)
                .ToList();

            manifests = results;
            return results.Count > 0;
        }

        private static void AddIfFound(IModHelper helper, object? obj, List<IManifest> results, bool includeContentPacks)
        {
            if (TryExtractManifest(helper, obj, out var m, includeContentPacks))
                results.Add(m);
        }

        private static void AddUid(IModHelper helper, string uid, List<IManifest> results, bool includeContentPacks)
        {
            var info = helper.ModRegistry.Get(uid);
            if (info?.Manifest is null)
                return;

            if (!includeContentPacks && info.IsContentPack)
                return;

            results.Add(info.Manifest);
        }

        private static bool TryExtractManifest(IModHelper helper, object? obj, out IManifest manifest, bool includeContentPacks)
        {
            manifest = null!;

            if (obj is null)
                return false;

            if (obj is IManifest m)
            {
                if (!includeContentPacks)
                {
                    var info = helper.ModRegistry.Get(m.UniqueID);
                    if (info?.IsContentPack == true)
                        return false;
                }

                manifest = m;
                return true;
            }

            var t = obj.GetType();

            // common pattern: something.Manifest
            var prop = t.GetProperty("Manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop is not null && typeof(IManifest).IsAssignableFrom(prop.PropertyType) && prop.CanRead)
            {
                try
                {
                    var v = prop.GetValue(obj) as IManifest;
                    if (v is not null)
                    {
                        if (!includeContentPacks && helper.ModRegistry.Get(v.UniqueID)?.IsContentPack == true)
                            return false;

                        manifest = v;
                        return true;
                    }
                }
                catch { }
            }

            // sometimes only UniqueID/ModID exists
            foreach (var name in new[] { "UniqueID", "UniqueId", "ModID", "ModId", "Id", "ID" })
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p is not null && p.PropertyType == typeof(string) && p.CanRead)
                {
                    try
                    {
                        var uid = p.GetValue(obj) as string;
                        if (!string.IsNullOrWhiteSpace(uid))
                        {
                            var info = helper.ModRegistry.Get(uid);
                            if (info?.Manifest is null)
                                break;

                            if (!includeContentPacks && info.IsContentPack)
                                return false;

                            manifest = info.Manifest;
                            return true;
                        }
                    }
                    catch { }
                }
            }

            return false;
        }

        private static List<IManifest> CleanSort(IEnumerable<IManifest> manifests, IManifest selfManifest)
        {
            return manifests
                .Where(m => m is not null)
                .Where(m => !string.Equals(m.UniqueID, selfManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(m => m.UniqueID)
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(m => m.UniqueID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsLeaf(object v)
        {
            return v is string
                   || v.GetType().IsPrimitive
                   || v is decimal;
        }

        private sealed class Candidate
        {
            public List<IManifest> Manifests { get; }

            public Candidate() : this(new List<IManifest>()) { }
            public Candidate(List<IManifest> manifests) => this.Manifests = manifests;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
