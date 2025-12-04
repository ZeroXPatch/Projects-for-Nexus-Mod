using StardewValley;

namespace LazySprinkler
{
    internal static class CropExtensions
    {
        /// <summary>
        /// Lightweight "is this ready to pick?" check so the mod doesn’t
        /// speed up already-harvestable crops.
        /// </summary>
        public static bool readyForHarvest(this Crop crop)
        {
            if (crop is null)
                return false;

            if (crop.phaseDays is null || crop.phaseDays.Count == 0)
                return false;

            int lastPhaseIndex = crop.phaseDays.Count - 1;

            // not in last phase yet
            if (crop.currentPhase.Value < lastPhaseIndex)
                return false;

            int lastPhaseLength = crop.phaseDays[lastPhaseIndex];

            // treat crops in last phase as ready once they’ve finished that phase
            return crop.dayOfCurrentPhase.Value >= lastPhaseLength - 1
                   || crop.fullyGrown.Value;   // <-- note .Value here
        }
    }
}
