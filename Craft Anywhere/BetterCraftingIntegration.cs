using System;
using System.Collections.Generic;
using StardewValley;

namespace CraftAnywhere
{
    public interface IBetterCraftingApi
    {
        /// <summary>
        /// This event is raised when Better Crafting wants to populate the list of additional
        /// containers for crafting. Use event subscription (+=) syntax.
        /// </summary>
        event Action<ISimplePopulateContainersEvent>? MenuSimplePopulateContainers;
    }

    public interface ISimplePopulateContainersEvent
    {
        /// <summary>
        /// The list of additional containers to add items from.
        /// Each entry is a Tuple of (object, GameLocation?) where object is typically a Chest.
        /// </summary>
        IList<Tuple<object, GameLocation?>> Containers { get; }

        /// <summary>
        /// Set this to true to prevent Better Crafting from running its
        /// own container discovery logic, if you so desire.
        /// </summary>
        bool DisableDiscovery { get; set; }
    }
}