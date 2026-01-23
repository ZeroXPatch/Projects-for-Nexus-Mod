using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;

namespace CraftAnywhere
{
    internal class ChestScanner
    {
        private const string AutoGrabberId = "(BC)165";

        public List<Chest> GetAllChests()
        {
            List<Chest> foundChests = new List<Chest>();

            foreach (GameLocation location in Game1.locations)
            {
                // 1. Scan Objects
                foreach (StardewValley.Object obj in location.objects.Values)
                {
                    // Check if it is a Chest. 
                    // Note: Big Chests and Stone Chests are instances of Chest.
                    if (obj is Chest chest)
                    {
                        // Ensure it's a player chest (not a treasure chest in the mines)
                        if (chest.playerChest.Value)
                        {
                            foundChests.Add(chest);
                        }
                    }
                    // Check for Auto-Grabbers
                    else if (obj.QualifiedItemId == AutoGrabberId && obj.heldObject.Value is Chest grabberChest)
                    {
                        foundChests.Add(grabberChest);
                    }
                }

                // 2. Scan Buildings
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
                        {
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

            return foundChests;
        }
    }
}