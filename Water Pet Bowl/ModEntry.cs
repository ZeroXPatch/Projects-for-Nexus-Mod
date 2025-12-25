using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace AutoFillPetBowl;

internal sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            this.ModManifest,
            reset: () => this.Config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.Config)
        );

        gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.Enabled,
            setValue: v => this.Config.Enabled = v,
            name: () => this.Helper.Translation.Get("gmcm.enabled.name"),
            tooltip: () => this.Helper.Translation.Get("gmcm.enabled.desc")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.OnlyFillIfEmpty,
            setValue: v => this.Config.OnlyFillIfEmpty = v,
            name: () => this.Helper.Translation.Get("gmcm.onlyIfEmpty.name"),
            tooltip: () => this.Helper.Translation.Get("gmcm.onlyIfEmpty.desc")
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.Config.FillDurationDays,
            setValue: v => this.Config.FillDurationDays = Math.Max(1, v),
            name: () => this.Helper.Translation.Get("gmcm.fillDuration.name"),
            tooltip: () => this.Helper.Translation.Get("gmcm.fillDuration.desc"),
            min: 1,
            max: 14,
            interval: 1
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.ShowHudMessage,
            setValue: v => this.Config.ShowHudMessage = v,
            name: () => this.Helper.Translation.Get("gmcm.showMessage.name"),
            tooltip: () => this.Helper.Translation.Get("gmcm.showMessage.desc")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.DebugLogging,
            setValue: v => this.Config.DebugLogging = v,
            name: () => this.Helper.Translation.Get("gmcm.debug.name"),
            tooltip: () => this.Helper.Translation.Get("gmcm.debug.desc")
        );
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
        if (!Context.IsMainPlayer)
            return;
        if (Game1.player is null)
            return;
        if (!this.Config.Enabled)
            return;

        int filledCount = 0;

        foreach (GameLocation loc in Game1.locations)
        {
            foreach (Building building in GetBuildingsFromLocation(loc))
            {
                if (!IsPetBowlBuilding(building))
                    continue;

                if (this.Config.OnlyFillIfEmpty && IsBowlFilled(building))
                {
                    this.Debug($"Skip filled bowl at {loc.NameOrUniqueName} (buildingId={TryGetBuildingId(building)}).");
                    continue;
                }

                if (TryFillBowl(building, this.Config.FillDurationDays))
                    filledCount++;
            }
        }

        if (this.Config.ShowHudMessage && filledCount > 0)
        {
            string msg = string.Format(this.Helper.Translation.Get("hud.filled"), filledCount);
            Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
        }

        this.Debug($"DayStarted: filled {filledCount} pet bowl(s).");
    }

    /// <summary>
    /// Get "buildings" from any location without referencing BuildableGameLocation.
    /// Works for Farm and other buildable locations (including custom maps) if they expose a buildings field/property.
    /// </summary>
    private static IEnumerable<Building> GetBuildingsFromLocation(GameLocation location)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // property (some versions)
        var prop = location.GetType().GetProperty("buildings", flags);
        object? value = prop?.GetValue(location);

        // field (common)
        if (value is null)
        {
            var field = location.GetType().GetField("buildings", flags);
            value = field?.GetValue(location);
        }

        if (value is null)
            return Enumerable.Empty<Building>();

        if (value is IEnumerable<Building> typed)
            return typed;

        if (value is IEnumerable enumerable)
            return enumerable.Cast<object>().OfType<Building>();

        return Enumerable.Empty<Building>();
    }

    /// <summary>Detect pet bowl buildings by class name or buildingType string.</summary>
    private static bool IsPetBowlBuilding(Building building)
    {
        // Most reliable: type name
        if (string.Equals(building.GetType().Name, "PetBowl", StringComparison.OrdinalIgnoreCase))
            return true;

        // Fallback: buildingType net field (string)
        // (In many cases this will be "Pet Bowl" or similar.)
        try
        {
            string? bt = building.buildingType?.Value;
            if (!string.IsNullOrEmpty(bt) && bt.Contains("pet", StringComparison.OrdinalIgnoreCase) && bt.Contains("bowl", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // ignore
        }

        return false;
    }

    /// <summary>Best-effort check whether the bowl is already filled.</summary>
    private static bool IsBowlFilled(Building bowl)
    {
        // bool-ish members like watered/filled/full
        if (TryGetLikelyBool(bowl, out bool watered) && watered)
            return true;

        // int-ish members like days remaining
        if (TryGetLikelyInt(bowl, out int daysRemaining) && daysRemaining > 0)
            return true;

        return false;
    }

    /// <summary>Try fill using reflection: call a method if it exists, else set likely fields.</summary>
    private bool TryFillBowl(Building bowl, int durationDays)
    {
        try
        {
            if (TryInvokeFillMethod(bowl, durationDays))
                return true;

            bool changed = false;
            changed |= SetLikelyBoolMembers(bowl, value: true);
            changed |= SetLikelyIntMembers(bowl, value: Math.Max(1, durationDays));

            if (!changed)
                this.Debug($"No fill method/fields found for {bowl.GetType().FullName} (id={TryGetBuildingId(bowl)}).");

            return changed;
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to fill pet bowl ({bowl.GetType().FullName}).\n{ex}", LogLevel.Warn);
            return false;
        }
    }

    private bool TryInvokeFillMethod(Building bowl, int durationDays)
    {
        string[] preferredNames =
        {
            "FillBowl", "Fill", "WaterBowl", "Water", "Refill", "RefillBowl", "OnWatered", "SetWatered"
        };

        var methods = bowl.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // exact name matches first
        foreach (string name in preferredNames)
        {
            var m = methods.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (TryInvokeCandidate(m, bowl, durationDays))
                return true;
        }

        // heuristic matches
        foreach (var m in methods)
        {
            string n = m.Name.ToLowerInvariant();
            if (!n.Contains("water") && !n.Contains("fill") && !n.Contains("refill"))
                continue;

            if (TryInvokeCandidate(m, bowl, durationDays))
                return true;
        }

        return false;
    }

    private static bool TryInvokeCandidate(MethodInfo? method, Building bowl, int durationDays)
    {
        if (method is null)
            return false;

        try
        {
            var pars = method.GetParameters();

            if (pars.Length == 0)
            {
                method.Invoke(bowl, null);
                return true;
            }

            if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
            {
                method.Invoke(bowl, new object[] { durationDays });
                return true;
            }

            if (pars.Length == 1 && pars[0].ParameterType == typeof(bool))
            {
                method.Invoke(bowl, new object[] { true });
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetLikelyBool(object instance, out bool value)
    {
        value = false;

        foreach (var member in GetAllReadableMembers(instance))
        {
            string name = member.Name.ToLowerInvariant();
            if (!LooksLikeFillBoolName(name))
                continue;

            if (TryReadBoolMember(instance, member, out value))
                return true;
        }

        return false;
    }

    private static bool TryGetLikelyInt(object instance, out int value)
    {
        value = 0;

        foreach (var member in GetAllReadableMembers(instance))
        {
            string name = member.Name.ToLowerInvariant();
            if (!LooksLikeFillIntName(name))
                continue;

            if (TryReadIntMember(instance, member, out value))
                return true;
        }

        return false;
    }

    private static bool SetLikelyBoolMembers(object instance, bool value)
    {
        bool changed = false;

        foreach (var member in GetAllWritableMembers(instance))
        {
            string name = member.Name.ToLowerInvariant();
            if (!LooksLikeFillBoolName(name))
                continue;

            changed |= TryWriteBoolMember(instance, member, value);
        }

        return changed;
    }

    private static bool SetLikelyIntMembers(object instance, int value)
    {
        bool changed = false;

        foreach (var member in GetAllWritableMembers(instance))
        {
            string name = member.Name.ToLowerInvariant();
            if (!LooksLikeFillIntName(name))
                continue;

            changed |= TryWriteIntMember(instance, member, value);
        }

        return changed;
    }

    private static bool LooksLikeFillBoolName(string name)
    {
        if (name.Contains("tile"))
            return false;

        return name.Contains("watered")
            || name.Contains("iswatered")
            || name.Contains("filled")
            || name.Contains("isfilled")
            || (name.Contains("water") && (name.Contains("has") || name.Contains("full")));
    }

    private static bool LooksLikeFillIntName(string name)
    {
        if (name.Contains("tile") || name.Contains("offset"))
            return false;

        return (name.Contains("day") && (name.Contains("water") || name.Contains("fill")))
            || name.Contains("daysremaining")
            || name.Contains("daysleft")
            || name.Contains("waterleft")
            || name.Contains("remainingwater")
            || name.Contains("fillduration")
            || name.Contains("waterduration");
    }

    private readonly record struct MemberAccessor(string Name, Type Type, Func<object, object?> Getter, Action<object, object?>? Setter);

    private static IEnumerable<MemberAccessor> GetAllReadableMembers(object instance)
    {
        Type t = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var p in t.GetProperties(flags))
        {
            if (!p.CanRead)
                continue;

            yield return new MemberAccessor(
                p.Name,
                p.PropertyType,
                Getter: obj => p.GetValue(obj),
                Setter: p.CanWrite ? (obj, val) => p.SetValue(obj, val) : null
            );
        }

        foreach (var f in t.GetFields(flags))
        {
            yield return new MemberAccessor(
                f.Name,
                f.FieldType,
                Getter: obj => f.GetValue(obj),
                Setter: (obj, val) => f.SetValue(obj, val)
            );
        }
    }

    private static IEnumerable<MemberAccessor> GetAllWritableMembers(object instance)
        => GetAllReadableMembers(instance).Where(m => m.Setter is not null);

    private static bool TryReadBoolMember(object instance, MemberAccessor member, out bool value)
    {
        value = false;

        object? raw = member.Getter(instance);
        if (raw is null)
            return false;

        if (raw is bool b)
        {
            value = b;
            return true;
        }

        if (raw is NetBool nb)
        {
            value = nb.Value;
            return true;
        }

        return false;
    }

    private static bool TryReadIntMember(object instance, MemberAccessor member, out int value)
    {
        value = 0;

        object? raw = member.Getter(instance);
        if (raw is null)
            return false;

        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is NetInt ni)
        {
            value = ni.Value;
            return true;
        }

        return false;
    }

    private static bool TryWriteBoolMember(object instance, MemberAccessor member, bool value)
    {
        if (member.Setter is null)
            return false;

        if (member.Type == typeof(bool))
        {
            member.Setter(instance, value);
            return true;
        }

        object? raw = member.Getter(instance);
        if (raw is NetBool nb)
        {
            nb.Value = value;
            return true;
        }

        return false;
    }

    private static bool TryWriteIntMember(object instance, MemberAccessor member, int value)
    {
        if (member.Setter is null)
            return false;

        if (member.Type == typeof(int))
        {
            member.Setter(instance, value);
            return true;
        }

        object? raw = member.Getter(instance);
        if (raw is NetInt ni)
        {
            ni.Value = value;
            return true;
        }

        return false;
    }

    private static string TryGetBuildingId(Building building)
    {
        try
        {
            // many buildings have an id net field
            return building.id?.Value.ToString() ?? "?";
        }
        catch
        {
            return "?";
        }
    }

    private void Debug(string message)
    {
        if (this.Config.DebugLogging)
            this.Monitor.Log(message, LogLevel.Debug);
    }
}
