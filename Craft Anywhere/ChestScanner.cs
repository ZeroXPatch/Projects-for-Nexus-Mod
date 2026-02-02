using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;

namespace CraftAnywhere
{
    internal class ChestScanner
    {
        private const string AutoGrabberId = "(BC)165";

        private readonly IModHelper Helper;
        private readonly IMonitor Monitor;

        public ChestScanner(IModHelper helper, IMonitor monitor)
        {
            Helper = helper;
            Monitor = monitor;
        }

        public List<Chest> GetAllChests()
        {
            List<Chest> foundChests = new List<Chest>();
            HashSet<GameLocation> scanned = new HashSet<GameLocation>();

            // Pass 1: scan everything already in Game1.locations
            foreach (GameLocation location in Game1.locations)
            {
                ScanLocation(location, foundChests, scanned);
            }

            // Pass 2: find building interiors that may not be in Game1.locations yet
            // (unvisited sheds, barns, coops, etc.)
            ScanAllBuildings(foundChests, scanned);

            return foundChests;
        }

        /// <summary>
        /// Scans all buildings in the game world for their interiors.
        /// </summary>
        private void ScanAllBuildings(List<Chest> foundChests, HashSet<GameLocation> scanned)
        {
            // Scan all locations for buildings
            foreach (GameLocation location in Game1.locations)
            {
                ScanBuildingsInLocation(location, foundChests, scanned);
            }
        }

        /// <summary>
        /// Scans all buildings in a specific location.
        /// </summary>
        private void ScanBuildingsInLocation(GameLocation location, List<Chest> foundChests, HashSet<GameLocation> scanned)
        {
            if (location.buildings == null)
                return;

            foreach (Building building in location.buildings)
            {
                // Scan the building's interior
                TryScanBuildingInterior(building, foundChests, scanned);

                // Recursively scan buildings within buildings (e.g., buildings on the island farm)
                GameLocation? interior = building.GetIndoors();
                if (interior != null && interior.buildings != null && interior.buildings.Count > 0)
                {
                    ScanBuildingsInLocation(interior, foundChests, scanned);
                }
            }
        }

        /// <summary>
        /// Resolves and scans a building's interior location.
        /// </summary>
        private void TryScanBuildingInterior(Building building, List<Chest> foundChests, HashSet<GameLocation> scanned)
        {
            try
            {
                // Use the public GetIndoors() method to get the interior location
                GameLocation? interior = building.GetIndoors();

                if (interior == null)
                    return; // building has no interior

                // The interior location should have its objects loaded automatically
                // when accessed via GetIndoors(), but we'll scan it regardless
                ScanLocation(interior, foundChests, scanned);
            }
            catch (Exception ex)
            {
                Monitor.LogOnce($"Failed to scan building interior '{building.buildingType.Value}': {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>
        /// Scans a single GameLocation for player chests, auto-grabbers, building
        /// output chests, and fridges. The scanned HashSet prevents any location
        /// from being processed twice.
        /// </summary>
        private void ScanLocation(GameLocation location, List<Chest> foundChests, HashSet<GameLocation> scanned)
        {
            if (!scanned.Add(location))
                return;

            var objects = location.objects;
            if (objects == null)
                return;

            // 1. Scan Objects
            foreach (StardewValley.Object obj in objects.Values)
            {
                if (obj is Chest chest)
                {
                    if (chest.playerChest.Value)
                        foundChests.Add(chest);
                }
                else if (obj.QualifiedItemId == AutoGrabberId && obj.heldObject.Value is Chest grabberChest)
                {
                    foundChests.Add(grabberChest);
                }
            }

            // 2. Scan Buildings (output chests like Junimo Huts)
            if (location.buildings != null)
            {
                foreach (Building building in location.buildings)
                {
                    if (building is JunimoHut hut)
                    {
                        foundChests.Add(hut.GetOutputChest());
                    }
                    else
                    {
                        Chest? output = building.GetBuildingChest("Output");
                        if (output != null)
                            foundChests.Add(output);
                    }
                }
            }

            // 3. Scan Fridges
            if (location is FarmHouse house)
            {
                if (house.fridge.Value != null && house.fridgePosition != Point.Zero)
                    foundChests.Add(house.fridge.Value);
            }
            else if (location is IslandFarmHouse islandHouse)
            {
                if (islandHouse.fridge.Value != null)
                    foundChests.Add(islandHouse.fridge.Value);
            }
        }
    }
}