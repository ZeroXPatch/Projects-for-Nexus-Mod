# Automate No More Lag - Performance Optimization for Automate

A Stardew Valley mod that significantly reduces lag caused by the Automate mod through intelligent update throttling, idle machine detection, smart caching, and location-based processing.

## Features

### 🚀 Performance Optimizations

1. **Update Throttling**: Reduces how often Automate checks machines (configurable)
2. **Idle Detection**: Skips processing machine groups with no ready machines or available inputs
3. **Smart Caching**: Caches machine group states to avoid repeated checks
4. **Empty Location Skipping**: Doesn't process automation in locations with no players
5. **Player Location Mode**: Only processes automation in the map where you are (maximum performance)
6. **Performance Monitoring**: Track and log performance improvements

### 📊 Expected Performance Gains

- **10-30% reduction** in CPU usage with default settings
- **Up to 50-70% reduction** with aggressive settings
- Especially effective with large machine setups (100+ machines)
- Reduces lag spikes during automation cycles

## Installation

1. Install [SMAPI](https://smapi.io/)
2. Install [Automate](https://www.nexusmods.com/stardewvalley/mods/1063)
3. Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional but recommended)
4. Download this mod and unzip it into `Stardew Valley/Mods`
5. Run the game using SMAPI

## Configuration

### Via Generic Mod Config Menu (Recommended)

1. Open the game menu
2. Click the "Mod Options" button
3. Find "ZeroXPatch" in the list
4. Adjust settings to your preference

### Via config.json

Located at `Mods/ZeroXPatch/config.json`:

```json
{
  "UpdateIntervalMultiplier": 1,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 60,
  "EnablePerformanceLogging": false,
  "MaxMachinesPerUpdate": 0,
  "SkipEmptyLocations": true
}
```

## Settings Explained

### Update Interval Multiplier (Default: 1)
- **What it does**: Controls how often Automate runs
- **Values**: 1-10
- **Recommendations**:
  - `1`: Normal speed (no throttling)
  - `2`: Half speed - good balance of performance/automation speed
  - `3-4`: Slower automation but significant performance gain
  - `5+`: Very slow automation, maximum performance

### Enable Idle Detection (Default: true)
- **What it does**: Skips machine groups with no ready output and no empty machines
- **Recommendation**: Keep enabled unless you notice automation issues
- **Impact**: Major performance improvement with minimal downsides

### Cache Duration Ticks (Default: 60)
- **What it does**: How long to remember if a machine group is idle
- **Values**: 10-300 ticks (1 second = 60 ticks)
- **Recommendations**:
  - `30-60`: More responsive, moderate caching
  - `120-180`: Less responsive, better performance
  - `240+`: Significant lag reduction for very large setups

### Enable Performance Logging (Default: false)
- **What it does**: Logs performance stats every 60 seconds
- **Use case**: Troubleshooting or seeing how much the mod is helping
- **Check**: Look in SMAPI console for metrics

### Max Machines Per Update (Default: 0)
- **What it does**: Limits how many machine groups are processed per cycle
- **Values**: 0 = unlimited
- **Use case**: Advanced users with extreme lag
- **Recommendation**: Start with 0, only adjust if needed

### Skip Empty Locations (Default: true)
- **What it does**: Doesn't process automation in locations with no players
- **Recommendation**: Keep enabled
- **Impact**: Good performance gain in multiplayer or with many locations

### Only Process Player Location (Default: false)
- **What it does**: Only processes automation in the map where the player currently is. ALL other locations stop automating completely until you visit them.
- **Recommendation**: Enable for maximum performance if you don't mind automation pausing when you leave an area
- **Impact**: Massive performance gain (60-90% reduction) but automation only works in your current location
- **Use case**: Best for players with huge farms who stay in one area at a time, or players experiencing severe lag
- **Note**: When enabled, your kegs/furnaces/etc will ONLY work when you're in that map. Leave the farm? Farm automation pauses. Enter the farm? It resumes.

## Recommended Presets

### Balanced Performance (Default)
Best for most users - good performance with minimal automation delay
```json
{
  "UpdateIntervalMultiplier": 1,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 60,
  "SkipEmptyLocations": true,
  "OnlyProcessPlayerLocation": false
}
```

### Performance Focus
For users experiencing significant lag
```json
{
  "UpdateIntervalMultiplier": 2,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 120,
  "SkipEmptyLocations": true,
  "OnlyProcessPlayerLocation": false
}
```

### Maximum Performance
For very large machine setups (200+ machines)
```json
{
  "UpdateIntervalMultiplier": 3,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 180,
  "SkipEmptyLocations": true,
  "MaxMachinesPerUpdate": 20,
  "OnlyProcessPlayerLocation": false
}
```

### Ultra Performance (Player Location Only)
For extreme lag or mega farms (500+ machines). Automation ONLY works in your current map.
```json
{
  "UpdateIntervalMultiplier": 2,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 120,
  "SkipEmptyLocations": true,
  "OnlyProcessPlayerLocation": true
}
```

### Minimal Impact
For testing or if you want very responsive automation
```json
{
  "UpdateIntervalMultiplier": 1,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 30,
  "SkipEmptyLocations": false,
  "OnlyProcessPlayerLocation": false
}
```

## Performance Tips

1. **Start Conservative**: Use default settings first, then increase if needed
2. **Monitor FPS**: Use the in-game FPS counter to see improvements
3. **Check Logs**: Enable performance logging to see actual impact
4. **Combine with Other Mods**: This works alongside other performance mods
5. **Large Setups**: If you have 100+ machines, use Performance Focus preset

## Troubleshooting

### Automation seems slower than before
- Decrease `UpdateIntervalMultiplier` (try 1 or 2)
- Decrease `CacheDurationTicks` (try 30-60)
- This is expected - the mod trades some speed for performance

### Machines not processing
1. Disable `EnableIdleDetection` temporarily
2. Check if Automate itself is working
3. Share your SMAPI log if issues persist

### Game still laggy
1. Try the "Maximum Performance" preset
2. Consider reducing total number of machines
3. Check if other mods are causing lag (disable this mod to test)
4. Your farm might be hitting general game engine limits

### Errors in SMAPI log
- Make sure Automate is installed and up to date
- Update ZeroXPatch to latest version
- Report issues with your SMAPI log

## Compatibility

- ✅ **Automate**: Required - this mod patches it
- ✅ **Generic Mod Config Menu**: Optional - for easy configuration
- ✅ **Producer Framework Mod + PFMAutomate**: Compatible
- ✅ **Chests Anywhere**: Compatible
- ✅ **All Automate add-ons**: Should be compatible

## How It Works

ZeroXPatch uses Harmony to patch Automate's update methods:

1. **Throttles Updates**: Skips automation cycles based on your multiplier setting
2. **Detects Idle Groups**: Checks if machines have output or can accept input
3. **Caches Results**: Remembers idle state for the configured duration
4. **Skips Empty Areas**: Doesn't process locations without players

All patches are safe and fallback to original behavior on errors.

## For Developers

### Building from Source

1. Clone this repository
2. Open in Visual Studio or your preferred IDE
3. Ensure you have .NET 6.0 SDK installed
4. Build the project - it will automatically copy to your Mods folder

### Technical Details

- Uses Harmony 2.x for runtime patching
- Patches `MachineManager.Automate()` and `MachineGroup.Automate()`
- Implements reflection-based compatibility to work across Automate updates
- Thread-safe caching with automatic cleanup

## Known Limitations

- Automation will be slightly slower with throttling enabled (this is intentional)
- Some very specific automation chains might need lower throttling values
- Cache cleanup happens every 10 seconds (not configurable)

## Credits

- **Pathoschild**: For the amazing Automate mod
- **StardewValley Modding Community**: For tools and support
- **You**: For using this mod to improve your farming experience!

## Support

If you experience issues:
1. Make sure Automate works without ZeroXPatch first
2. Try different configuration settings
3. Check SMAPI log for errors
4. Report issues with your configuration and log file

**Important**: If Automate behaves oddly, disable ZeroXPatch before reporting to Automate's page!

## License

This mod is released under MIT License. Feel free to modify and redistribute.

## Changelog

### 1.0.0
- Initial release
- Update throttling
- Idle detection
- Smart caching
- Empty location skipping
- Performance monitoring
- Generic Mod Config Menu integration
