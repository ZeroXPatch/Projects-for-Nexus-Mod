using System.Collections.Generic;

namespace OopsAllGeorge;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Chance per day to trigger "George Day". Percent from 0 to 100.</summary>
    public float ChancePercent { get; set; } = 1.0f;

    /// <summary>Replace NPC character sprites with George's sprite on George Day.</summary>
    public bool ReplaceCharacterSprites { get; set; } = true;

    /// <summary>Replace NPC portraits with George's portrait on George Day.</summary>
    public bool ReplacePortraits { get; set; } = true;

    /// <summary>Also replace Characters/&lt;Name&gt;_Beach where applicable.</summary>
    public bool ReplaceBeachSpritesToo { get; set; } = true;

    /// <summary>NPC internal names to exclude (case-insensitive). Default excludes George so he's "normal George".</summary>
    public List<string> ExcludeNpcNames { get; set; } = new() { "George" };

    /// <summary>If true, shows a message when George Day begins.</summary>
    public bool ShowStartMessage { get; set; } = true;

    /// <summary>The message displayed when George Day triggers.</summary>
    public string StartMessage { get; set; } = "A strange wheelchair energy fills the valley...";
}
