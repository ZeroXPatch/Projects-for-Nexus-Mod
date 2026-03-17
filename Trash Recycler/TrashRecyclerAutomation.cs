using System;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace TrashToTreasure
{
    /// <summary>Factory for creating Trash Recycler machine instances.</summary>
    /// <remarks>Uses duck typing to match Automate's IAutomationFactory interface.</remarks>
    public class TrashRecyclerFactory
    {
        private readonly string _machineId;
        private readonly ModConfig _config;
        private readonly Func<Item> _getRandomItem;

        public TrashRecyclerFactory(string machineId, ModConfig config, Func<Item> getRandomItem)
        {
            _machineId = machineId;
            _config = config;
            _getRandomItem = getRandomItem;
        }

        /// <summary>Get a machine instance for the given object.</summary>
        public object GetFor(SObject obj, GameLocation location, in Vector2 tile)
        {
            if (obj?.QualifiedItemId == $"(O){_machineId}")
                return new TrashRecyclerMachine(obj, _config, _getRandomItem);

            return null;
        }

        /// <summary>Get a machine instance for the given terrain feature.</summary>
        public object GetFor(TerrainFeature feature, GameLocation location, in Vector2 tile)
        {
            return null;
        }

        /// <summary>Get a machine instance for the given building.</summary>
        public object GetFor(Building building, GameLocation location, in Vector2 tile)
        {
            return null;
        }
    }

    /// <summary>Automate implementation for the Trash Recycler machine.</summary>
    /// <remarks>Uses duck typing to match Automate's IAutomatable interface.</remarks>
    public class TrashRecyclerMachine
    {
        private readonly SObject _machine;
        private readonly ModConfig _config;
        private readonly Func<Item> _getRandomItem;

        public TrashRecyclerMachine(SObject machine, ModConfig config, Func<Item> getRandomItem)
        {
            _machine = machine;
            _config = config;
            _getRandomItem = getRandomItem;
        }

        /// <summary>The machine's type ID.</summary>
        public string MachineTypeID => "ZeroXPatch.TrashToTreasure_Machine";

        /// <summary>Get the machine's processing state.</summary>
        public MachineState GetState()
        {
            var state = _machine.heldObject.Value == null
                ? MachineState.Empty
                : (_machine.MinutesUntilReady <= 0 ? MachineState.Done : MachineState.Processing);

            return state;
        }

        /// <summary>Get the machine's output.</summary>
        public ITrackedStack GetOutput()
        {
            if (_machine.heldObject.Value != null && _machine.MinutesUntilReady <= 0)
            {
                return new TrackedItem(_machine.heldObject.Value, onReduced: _ =>
                {
                    _machine.heldObject.Value = null;
                    _machine.MinutesUntilReady = -1;
                });
            }

            return null;
        }

        /// <summary>Provide input to the machine.</summary>
        public bool SetInput(IStorage input)
        {
            if (input == null)
                return false;

            // Accept trash (item ID 168)
            var trash = input.GetIngredient(
                predicate: item => item != null && (item.QualifiedItemId == "(O)168" || item.ParentSheetIndex == 168),
                count: 1,
                getKey: null
            );

            if (trash == null)
                return false;

            // Get random output item
            Item outputItem = _getRandomItem();

            if (outputItem == null)
                return false;

            // Set up the machine
            _machine.heldObject.Value = (SObject)outputItem;
            _machine.MinutesUntilReady = (int)(_config.ProcessTimeHours * 60);

            // Consume the trash
            trash.Reduce(1);

            return true;
        }
    }

    /// <summary>Tracked item stack implementation.</summary>
    public class TrackedItem : ITrackedStack
    {
        private readonly Action<int>? _onReduced;

        public TrackedItem(Item item, Action<int>? onReduced = null)
        {
            Item = item;
            Count = item.Stack;
            _onReduced = onReduced;
        }

        public Item Item { get; }
        public int Count { get; private set; }

        public void Reduce(int count)
        {
            Count -= count;
            _onReduced?.Invoke(count);
        }

        public void Take()
        {
            Reduce(Count);
        }
    }

    // These interfaces are used for duck typing - they define the contract without explicit implementation
    public interface ITrackedStack
    {
        Item Item { get; }
        int Count { get; }
        void Reduce(int count);
        void Take();
    }

    public interface IStorage
    {
        ITrackedStack GetIngredient(Predicate<Item> predicate, int count = 1, Func<Item, string>? getKey = null);
        void Push(ITrackedStack stack);
    }

    public enum MachineState
    {
        Empty,
        Processing,
        Done
    }
}