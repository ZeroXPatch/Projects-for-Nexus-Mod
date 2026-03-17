# Player Location Mode - Quick Reference

## What is "Only Process Player Location"?

This new feature in Automate No More Lag allows you to **completely stop** automation in all locations except the one you're currently in. When enabled, machines will ONLY process when you're physically present in that location.

## How It Works

**Enabled (true)**:
- You're on your farm → Farm machines work ✅
- You leave to town → Farm machines PAUSE ⏸️
- You return to farm → Farm machines RESUME ✅
- You go to Ginger Island → Island machines work, farm paused ✅/⏸️

**Disabled (false)**:
- All locations process normally (subject to other settings)

## When to Use It

### ✅ Good Use Cases:
- **Severe lag** with 300+ machines and you need maximum performance
- **Single location focus** - you spend most time in one area
- **Mega farms** where you organize work by location
- **Testing** - see if location-based processing helps your specific setup
- **Low-end hardware** struggling with large automation setups

### ❌ Not Recommended For:
- Players who want "set and forget" automation everywhere
- Multiplayer farms where different players are in different locations
- If you frequently switch between locations
- Small to medium farms (<100 machines) - other settings will suffice

## Performance Impact

**Expected Improvement:**
- 60-90% CPU reduction for automation (most dramatic of all settings)
- Near-zero lag from machines in unvisited locations
- Instant response when entering a location with machines

**Trade-off:**
- Machines only work when you're there
- Must visit each location to let automation run
- Not suitable for passive income strategies

## Configuration

### Via Generic Mod Config Menu:
1. Open game menu
2. Go to Mod Options
3. Find "Automate No More Lag"
4. Toggle "Only Process Player Location" ON

### Via config.json:
```json
{
  "OnlyProcessPlayerLocation": true
}
```

## Combining with Other Settings

### Recommended Combo (Ultra Performance):
```json
{
  "UpdateIntervalMultiplier": 2,
  "EnableIdleDetection": true,
  "CacheDurationTicks": 120,
  "SkipEmptyLocations": true,
  "OnlyProcessPlayerLocation": true
}
```

This gives maximum performance while still allowing automation to work efficiently when you're present.

## Technical Details

- Checks `Game1.player.currentLocation` against machine group location
- Happens before any other processing (most efficient check)
- Completely skips idle detection, caching, and machine scanning for non-player locations
- Zero CPU spent on locations you're not in

## Comparison with "Skip Empty Locations"

| Feature | Skip Empty Locations | Only Process Player Location |
|---------|---------------------|------------------------------|
| What it does | Skips locations with NO players | Only processes YOUR current location |
| Multiplayer | Works with any player | Only your location |
| Performance | Good | Excellent |
| Automation coverage | All locations with players | Only where you are |
| Recommended for | All players | Severe lag cases |

## Example Scenarios

### Scenario 1: Large Farm Setup
- 300 kegs in basement
- 100 furnaces in cave
- You're fishing in town
- **With feature ON**: 0 CPU for machines (you're not there)
- **With feature OFF**: Normal processing (potential lag)

### Scenario 2: Ginger Island Farm
- Machines on main farm and island
- You're on island for the day
- **With feature ON**: Island works, main farm paused
- **With feature OFF**: Both locations process

### Scenario 3: Multiplayer
- You're in your cabin area
- Friend is on main farm
- **With feature ON**: Only YOUR location processes
- **Skip Empty Locations**: Both locations process (players in both)

## Tips

1. **Test it first**: Try with feature enabled for one in-game day
2. **Check your workflow**: Do you stay in one place or move around a lot?
3. **Combine wisely**: Use with other performance settings for best results
4. **Monitor the difference**: Enable performance logging to see impact
5. **Adjust as needed**: You can toggle this on/off anytime

## Troubleshooting

**Q: My machines aren't processing!**
A: Make sure you're in the same location. This is expected behavior with this feature.

**Q: Can I make it work for specific locations only?**
A: No, it's all-or-nothing. Either player location only, or all locations.

**Q: Does it work in multiplayer?**
A: Yes, but only YOUR location processes. Other players' locations won't process for you.

**Q: Will machines "catch up" when I return?**
A: No, they're paused. Time doesn't pass for machines when you're away with this setting.

**Q: Can I combine this with other performance settings?**
A: Yes! Recommended to use all settings together for maximum effect.

---

**Remember**: This is the most aggressive performance option. Use it when you need maximum lag reduction and don't mind location-based automation!
