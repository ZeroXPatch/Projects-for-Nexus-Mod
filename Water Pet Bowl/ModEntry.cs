using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace AutoFillPetBowl;

public class ModEntry : Mod
{
    // The 'null!' tells the compiler "trust me, this won't be null when I use it"
    private ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!this.Config.Enabled)
            return;

        Farm farm = Game1.getFarm();
        if (farm is null) return;

        int bowlsFilled = 0;

        // In 1.6, Pet Bowls are buildings. We iterate the farm's building list.
        foreach (Building building in farm.buildings)
        {
            if (building is PetBowl bowl)
            {
                if (!bowl.watered.Value)
                {
                    bowl.watered.Value = true;
                    bowlsFilled++;
                }
            }
        }

        if (this.Config.EnableLogging && bowlsFilled > 0)
        {
            this.Monitor.Log($"Filled {bowlsFilled} pet bowl(s) with water.", LogLevel.Debug);
        }
    }
}