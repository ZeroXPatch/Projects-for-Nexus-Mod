using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace MassAnimalMover
{
    public static class AnimalManager
    {
        public static string TransferAnimals(Building sourceParams, Building destParams, List<long> animalIdsToMove)
        {
            if (sourceParams == null || destParams == null) return "Building not found.";
            if (sourceParams == destParams) return "Cannot move to same building.";

            AnimalHouse destIndoors = destParams.GetIndoors() as AnimalHouse;
            AnimalHouse sourceIndoors = sourceParams.GetIndoors() as AnimalHouse;

            if (destIndoors == null) return "Destination is not an animal building.";

            // UPDATED: Get Parent Locations dynamically (e.g., Farm, IslandWest, CustomSVE)
            GameLocation sourceParent = sourceParams.GetParentLocation();
            GameLocation destParent = destParams.GetParentLocation();

            if (sourceParent == null || destParent == null) return "Could not determine location.";

            // 1. Capacity Check
            int capacity = destParams.maxOccupants.Value;
            int destCurrentCount = GetAnimalCount(destParams);

            if (destCurrentCount + animalIdsToMove.Count > capacity)
                return $"Not enough space! Slots left: {capacity - destCurrentCount}";

            int movedCount = 0;

            foreach (long animalId in animalIdsToMove)
            {
                FarmAnimal animal = null;

                // --- FIND ANIMAL ---
                // 1. Check Source Indoors
                if (sourceIndoors != null && sourceIndoors.animals.TryGetValue(animalId, out FarmAnimal insideAnimal))
                    animal = insideAnimal;
                // 2. Check Source Outdoors (Parent Location)
                else if (sourceParent.animals.TryGetValue(animalId, out FarmAnimal outsideAnimal))
                    animal = outsideAnimal;

                if (animal == null)
                {
                    Console.WriteLine($"[MassAnimalMover] Could not find animal with ID {animalId}");
                    continue;
                }

                // --- REMOVE FROM OLD LOCATION ---
                // Try removing from both possible states to be safe
                if (sourceIndoors != null && sourceIndoors.animals.ContainsKey(animalId))
                    sourceIndoors.animals.Remove(animalId);

                if (sourceParent.animals.ContainsKey(animalId))
                    sourceParent.animals.Remove(animalId);

                // --- ADD TO NEW LOCATION ---
                // Always add to the INDOORS of the destination.
                // This is safer than trying to put them outside at a calculated coordinate on a different map.
                destIndoors.animals.Add(animalId, animal);

                // --- UPDATE DATA ---
                animal.home = destParams;

                // --- TELEPORT VISUALS ---
                // Force them inside to preventing floating in void on map change
                animal.currentLocation = destIndoors;
                animal.Position = new Vector2(100, 100); // Standard "inside door" coordinate

                movedCount++;
            }

            return $"Moved {movedCount} animals.";
        }

        private static int GetAnimalCount(Building b)
        {
            int count = 0;
            // Count animals inside
            if (b.GetIndoors() is AnimalHouse indoors)
                count += indoors.animals.Count();

            // Count animals outside in the correct location
            GameLocation parent = b.GetParentLocation();
            if (parent != null)
            {
                foreach (var a in parent.animals.Values)
                {
                    if (a.home == b) count++;
                }
            }
            return count;
        }
    }
}