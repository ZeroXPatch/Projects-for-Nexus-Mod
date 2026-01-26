namespace FasterMenuLoad
{
    public class ModConfig
    {
        public bool LazyLoadSkills { get; set; } = true;    // Tab 1
        public bool LazyLoadSocial { get; set; } = true;    // Tab 2
        // Tab 3 is Map (We usually skip this as it's fast/essential)
        public bool LazyLoadCrafting { get; set; } = false;  // Tab 4
        public bool LazyLoadAnimals { get; set; } = true;   // Tab 5 (New in 1.6)
        public bool LazyLoadPowers { get; set; } = false;    // Tab 6 (New in 1.6)
        public bool LazyLoadCollections { get; set; } = true; // Tab 7

        // Debug option
        public bool EnableDebugLogging { get; set; } = false;
    }
}