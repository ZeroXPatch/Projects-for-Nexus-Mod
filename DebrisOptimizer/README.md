# Debris Performance Optimizer

**Author:** ZeroXPatch

## Description
A Stardew Valley SMAPI mod that improves game performance when there's a lot of debris on screen from high drop rate mods. **Important:** This mod only hides excess debris visually - you still receive ALL items! It won't reduce your actual drops.

## The Problem
When using custom drop rate mods (2x, 5x, 10x, etc.), cutting trees and mining rocks can create hundreds of debris items on screen, causing severe stuttering and performance drops.

## The Solution
This mod hides excess debris visually while ensuring you still collect everything. The debris items remain in the game world and will be automatically collected - they're just not rendered to save performance.

## Features
- ✅ **Visual hiding only** - You get ALL items, guaranteed!
- ✅ **Generic Mod Config Menu integration** - Easy in-game configuration
- ✅ **Distance-based hiding** - Keeps debris near you visible, hides distant debris
- ✅ **Physics optimization** - Freezes physics for far-away debris to save CPU
- ✅ **Debug overlay** - Monitor how many debris items are visible vs hidden

## Installation
1. Install [SMAPI](https://smapi.io/)
2. Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional but recommended)
3. Download this mod
4. Extract the `DebrisOptimizer` folder into your `Stardew Valley/Mods` folder
5. Run the game using SMAPI

## Configuration (via Generic Mod Config Menu)

### Debris Display Settings
- **Max Visible Debris** (default: 150)
  - How many debris items to show on screen
  - Lower = better performance, higher = more visual feedback
  - Range: 1-500

- **Enable Debris Hiding** (default: ON)
  - Toggle the visual hiding system
  - Turn OFF if you want to see all debris regardless of performance

### Performance Settings
- **Physics Distance** (default: 800 pixels)
  - Distance at which debris physics are frozen
  - Lower = better performance, higher = more realistic movement
  - Range: 200-2000

- **Disable Distant Physics** (default: ON)
  - Freeze physics calculations for far-away debris
  - Significantly reduces CPU usage with many debris items

### Debug Options
- **Show Debug Overlay** (default: OFF)
  - Display on-screen counter: "Debris: X/Y visible (Z hidden)"
  - Useful for testing and monitoring

## How It Works

1. **Debris Tracking**: The mod continuously monitors all debris in your current location
2. **Distance Sorting**: Debris is sorted by distance from your player
3. **Visual Hiding**: The furthest debris beyond your limit are hidden using Harmony patches
4. **Collection Works Normally**: Hidden debris can still be collected automatically when you get near them
5. **Physics Optimization**: Distant debris have their physics frozen to reduce CPU load

### Example with Max Visible Debris = 150:
- You chop down trees with a 10x drop rate mod
- 500 wood debris spawn
- The mod shows the closest 150 debris
- The other 350 are hidden but still exist in the game
- You'll collect all 500 wood as normal!

## Recommended Settings

### For 2x-3x Drop Rates:
- Max Visible Debris: 150-200
- Physics Distance: 800

### For 5x-10x Drop Rates:
- Max Visible Debris: 75-150
- Physics Distance: 600

### For 10x+ Extreme Drop Rates:
- Max Visible Debris: 50-75
- Physics Distance: 400

### Potato PC / Heavy Performance Issues:
- Max Visible Debris: 25-50
- Physics Distance: 300
- Disable Distant Physics: ON

## FAQ

**Q: Will I lose items if debris is hidden?**
A: No! The debris still exists in the game world and will be collected normally. It's only hidden visually.

**Q: Why do I sometimes see debris pop in/out?**
A: The mod dynamically shows/hides debris based on distance. Items closer to you become visible, far items get hidden. This is normal and helps performance.

**Q: Can I set Max Visible Debris to 1?**
A: Yes, but it's not recommended. You'll still get all items, but you'll only see 1 debris piece at a time which might feel weird. Try 25-50 instead.

**Q: Does this work with other performance mods?**
A: Yes! This mod focuses specifically on debris rendering and should be compatible with most other performance mods.

**Q: The game still lags with my extreme drop rates!**
A: Try lowering Max Visible Debris to 25 or less, and reduce Physics Distance to 300. Also consider whether your drop rate mod might be too extreme for your hardware.

## Compatibility
- Requires SMAPI 4.0.0 or later
- Compatible with Stardew Valley 1.6+
- Works with any drop rate modification mods
- Generic Mod Config Menu integration (optional)
- Should be compatible with most other mods

## Technical Details
This mod uses Harmony patching to intercept the debris rendering code. When debris is marked as "hidden", the draw method returns early, skipping the rendering while leaving all game logic intact. This ensures items are still collectible while improving visual performance.

## Building from Source
1. Install .NET 6.0 SDK
2. Navigate to the mod directory
3. Run `dotnet build`
4. The compiled mod will be in `bin/Debug/net6.0/`

## Troubleshooting

**Still experiencing stuttering?**
1. Lower Max Visible Debris to 50 or less
2. Reduce Physics Distance to 300-400
3. Make sure "Disable Distant Physics" is ON
4. Check if other mods are causing performance issues

**Items not being collected?**
1. This shouldn't happen - debris is never deleted, only hidden
2. Try disabling the mod temporarily to see if it's a different issue
3. Report the bug with your SMAPI log

**Config menu not showing?**
1. Make sure Generic Mod Config Menu is installed
2. Check SMAPI log for errors
3. Try reinstalling both mods

## Credits
Created by ZeroXPatch in response to performance issues when using high drop rate mods.

## License
This mod is provided as-is for personal use.
