// Decompiled with JetBrains decompiler
// Type: PetIlluminator.IGenericModConfigMenuApi
// Assembly: PetIlluminator, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1DA55A12-7DF5-4B54-BF79-8B48FC03EAD8
// Assembly location: C:\GOG Games\Stardew Valley\Mods\PetIlluminator\PetIlluminator.dll

using StardewModdingAPI;
using System;

#nullable enable
namespace PetIlluminator;

public interface IGenericModConfigMenuApi
{
  void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

  void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

  void AddParagraph(IManifest mod, Func<string> text);

  void AddBoolOption(
    IManifest mod,
    Func<bool> getValue,
    Action<bool> setValue,
    Func<string> name,
    Func<string>? tooltip = null,
    string? fieldId = null);

  void AddNumberOption(
    IManifest mod,
    Func<int> getValue,
    Action<int> setValue,
    Func<string> name,
    Func<string>? tooltip = null,
    int? min = null,
    int? max = null,
    int? interval = null,
    Func<int, string>? formatValue = null,
    string? fieldId = null);

  void AddNumberOption(
    IManifest mod,
    Func<float> getValue,
    Action<float> setValue,
    Func<string> name,
    Func<string>? tooltip = null,
    float? min = null,
    float? max = null,
    float? interval = null,
    Func<float, string>? formatValue = null,
    string? fieldId = null);

  void AddTextOption(
    IManifest mod,
    Func<string> getValue,
    Action<string> setValue,
    Func<string> name,
    Func<string>? tooltip = null,
    string[]? allowedValues = null,
    Func<string, string>? formatAllowedValue = null,
    string? fieldId = null);
}
