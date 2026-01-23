using System;
using System.Collections.Generic;
using StardewValley;

namespace CraftAnywhere
{
    public interface IBetterCraftingApi
    {
        /// <summary>
        /// This event is fired whenever a new Better Crafting menu is opened,
        /// allowing other mods to manipulate the list of containers. This
        /// version of the event doesn't include a reference to the menu.
        /// </summary>
        event Action<ISimplePopulateContainersEvent>? MenuSimplePopulateContainers;
    }

    public interface ISimplePopulateContainersEvent
    {
        /// <summary>
        /// A list of all the containers this menu should draw items from.
        /// </summary>
        IList<Tuple<object, GameLocation?>> Containers { get; }

        /// <summary>
        /// Set this to true to prevent Better Crafting from running its
        /// own container discovery logic, if you so desire.
        /// </summary>
        bool DisableDiscovery { get; set; }
    }
}