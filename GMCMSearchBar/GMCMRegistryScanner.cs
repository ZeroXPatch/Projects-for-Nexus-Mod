// GMCMRegistryScanner.cs
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
        private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

        public static List<IManifest> GetRegisteredModsOrFallback(IModHelper helper, object gmcmApiObj, IMonitor monitor, IManifest selfManifest)
        {
            var reflected = TryGetRegisteredModsViaReflection(helper, gmcmApiObj, monitor, selfManifest);
            if (reflected.Count > 0)
                return CleanSort(reflected, selfManifest);

            // Fallback: loaded mods only (still won't perfectly match GMCM, but avoids listing every content pack).
            monitor.Log("Couldn't locate GMCM registry via reflection; falling back to loaded mods.", LogLevel.Warn);

            var fallback = helper.ModRegistry
                .GetAll()
                .Select(m => m.Manifest)
                .Where(m => m is not null)
                .Where(m => helper.ModRegistry.IsLoaded(m.UniqueID))
                .ToList();

            return CleanSort(fallback, selfManifest);
        }

        private static List<IManifest> CleanSort(IEnumerable<IManifest> manifests, IManifest selfManifest)
        {
            return manifests
                .Where(m => m is not null)
                .Where(m => !m.UniqueID.Equals(selfManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
                .Where(m => !m.UniqueID.Equals(GmcmUniqueId, StringComparison.OrdinalIgnoreCase))
                .GroupBy(m => m.UniqueID, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<IManifest> TryGetRegisteredModsViaReflection(IModHelper helper, object gmcmApiObj, IMonitor monitor, IManifest selfManifest)
        {
            try
            {
                var results = new List<IManifest>();

                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var queue = new Queue<(object Obj, int Depth)>();
                queue.Enqueue((gmcmApiObj, 0));

                const int maxDepth = 7;
                int nodesProcessed = 0;
                const int maxNodes = 20000;

                void AddManifest(IManifest m)
                {
                    // do NOT filter out content packs here:
                    // many GMCM entries are content packs registered by a framework mod.
                    if (m.UniqueID.Equals(selfManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
                        return;

                    if (m.UniqueID.Equals(GmcmUniqueId, StringComparison.OrdinalIgnoreCase))
                        return;

                    results.Add(m);
                }

                void TryAddByUidString(string? maybeUid)
                {
                    if (string.IsNullOrWhiteSpace(maybeUid))
                        return;

                    // basic heuristic: mod IDs usually look like "author.modname"
                    if (!maybeUid.Contains('.') || maybeUid.Contains(' '))
                        return;

                    try
                    {
                        var info = helper.ModRegistry.Get(maybeUid);
                        if (info?.Manifest is not null && helper.ModRegistry.IsLoaded(info.Manifest.UniqueID))
                            AddManifest(info.Manifest);
                    }
                    catch { }
                }

                bool ShouldReflectInto(Type t)
                {
                    // GMCM types are in this namespace in most versions:
                    if (t.Namespace?.StartsWith("GenericModConfigMenu", StringComparison.Ordinal) == true)
                        return true;

                    // Some builds put internals elsewhere; assembly name is usually "GenericModConfigMenu"
                    string asm = t.Assembly.GetName().Name ?? "";
                    if (asm.IndexOf("GenericModConfigMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    return false;
                }

                bool TryExtractManifestShallow(object obj, out IManifest manifest)
                {
                    manifest = null!;

                    if (obj is IManifest m)
                    {
                        manifest = m;
                        return true;
                    }

                    if (obj is IModInfo info && info.Manifest is not null)
                    {
                        manifest = info.Manifest;
                        return true;
                    }

                    if (obj is string s)
                    {
                        TryAddByUidString(s);
                        return false;
                    }

                    Type t = obj.GetType();

                    // look for a direct Manifest/Mod/Owner field/property
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    // properties first
                    foreach (var p in t.GetProperties(flags))
                    {
                        if (p.GetIndexParameters().Length > 0)
                            continue;

                        object? v;
                        try { v = p.GetValue(obj); } catch { continue; }
                        if (v is null) continue;

                        if (v is IManifest pm)
                        {
                            manifest = pm;
                            return true;
                        }

                        if (v is IModInfo pi && pi.Manifest is not null)
                        {
                            manifest = pi.Manifest;
                            return true;
                        }

                        if (v is string ps)
                            TryAddByUidString(ps);
                    }

                    // then fields
                    foreach (var f in t.GetFields(flags))
                    {
                        object? v;
                        try { v = f.GetValue(obj); } catch { continue; }
                        if (v is null) continue;

                        if (v is IManifest fm)
                        {
                            manifest = fm;
                            return true;
                        }

                        if (v is IModInfo fi && fi.Manifest is not null)
                        {
                            manifest = fi.Manifest;
                            return true;
                        }

                        if (v is string fs)
                            TryAddByUidString(fs);
                    }

                    return false;
                }

                void EnqueueCandidate(object? candidate, int depth)
                {
                    if (candidate is null)
                        return;

                    if (candidate is IManifest cm)
                    {
                        AddManifest(cm);
                        return;
                    }

                    if (candidate is IModInfo ci && ci.Manifest is not null)
                    {
                        AddManifest(ci.Manifest);
                        return;
                    }

                    if (candidate is string cs)
                    {
                        TryAddByUidString(cs);
                        return;
                    }

                    // Shallow extraction helps when dictionary values are GMCM config objects.
                    if (TryExtractManifestShallow(candidate, out var extracted))
                    {
                        AddManifest(extracted);
                        return;
                    }

                    if (depth > maxDepth)
                        return;

                    Type t = candidate.GetType();

                    // Always allow containers (registry is usually inside System collections).
                    if (candidate is IDictionary || (candidate is IEnumerable && candidate is not string))
                    {
                        queue.Enqueue((candidate, depth));
                        return;
                    }

                    // Only reflect into GMCM-owned objects to avoid crawling the whole game/mod world.
                    if (ShouldReflectInto(t))
                        queue.Enqueue((candidate, depth));
                }

                while (queue.Count > 0)
                {
                    if (nodesProcessed++ > maxNodes)
                        break;

                    var (obj, depth) = queue.Dequeue();
                    if (obj is null)
                        continue;

                    if (!visited.Add(obj))
                        continue;

                    // If this is a manifest/modinfo, record it.
                    if (obj is IManifest directManifest)
                    {
                        AddManifest(directManifest);
                        continue;
                    }

                    if (obj is IModInfo directInfo && directInfo.Manifest is not null)
                    {
                        AddManifest(directInfo.Manifest);
                        continue;
                    }

                    if (obj is string s)
                    {
                        TryAddByUidString(s);
                        continue;
                    }

                    // containers: scan contents regardless of namespace
                    if (obj is IDictionary dict)
                    {
                        foreach (DictionaryEntry entry in dict)
                        {
                            EnqueueCandidate(entry.Key, depth + 1);
                            EnqueueCandidate(entry.Value, depth + 1);

                            // Sometimes key/value is a complex object with Manifest/UniqueID inside
                            if (entry.Key is not null && TryExtractManifestShallow(entry.Key, out var mk))
                                AddManifest(mk);
                            if (entry.Value is not null && TryExtractManifestShallow(entry.Value, out var mv))
                                AddManifest(mv);
                        }
                        continue;
                    }

                    if (obj is IEnumerable enumerable && obj is not string)
                    {
                        foreach (object? item in enumerable)
                            EnqueueCandidate(item, depth + 1);

                        continue;
                    }

                    if (depth >= maxDepth)
                        continue;

                    Type t = obj.GetType();
                    if (!ShouldReflectInto(t) && obj != gmcmApiObj)
                        continue;

                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    foreach (var field in t.GetFields(flags))
                    {
                        object? v;
                        try { v = field.GetValue(obj); } catch { continue; }
                        EnqueueCandidate(v, depth + 1);
                    }

                    foreach (var prop in t.GetProperties(flags))
                    {
                        if (prop.GetIndexParameters().Length > 0)
                            continue;

                        object? v;
                        try { v = prop.GetValue(obj); } catch { continue; }
                        EnqueueCandidate(v, depth + 1);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                monitor.Log($"GMCM registry reflection failed.\n{ex}", LogLevel.Trace);
                return new List<IManifest>();
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            private ReferenceEqualityComparer() { }

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
