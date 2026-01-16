using System;

namespace PetIlluminator;

public class ModConfig
{
    public bool Enabled { get; set; } = true;

    // Default is now "Normal"
    public string Preset { get; set; } = "Normal";

    public float LightRadius { get; set; } = 1.0f;

    // New option: If true, waits 2 hours after sunset. If false, starts at sunset.
    public bool UseLateActivation { get; set; } = true;

    public int CustomRed { get; set; } = 255;

    public int CustomGreen { get; set; } = 255;

    public int CustomBlue { get; set; } = 255;
}