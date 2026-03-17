# Loading Progress Bar

A simple Stardew Valley mod that shows a visual progress bar (0-100%) during day-to-day transitions so you know the game is still working and hasn't frozen.

## What It Does

When you go to sleep and the day transitions:
- Shows a progress bar at the bottom of the screen
- Displays current status (Saving, Processing, etc.)
- Shows percentage (0% to 100%)
- Lets you know the game is working, not frozen

## Installation

1. Install [SMAPI](https://smapi.io/)
2. Download this mod
3. Extract to `Stardew Valley/Mods` folder
4. Run the game through SMAPI

## Configuration

Edit `config.json` in the mod folder to customize:

```json
{
  "ShowProgressBar": true,      // Enable/disable the bar
  "BarWidth": 500,              // Width in pixels
  "BarHeight": 40,              // Height in pixels
  "ShowPercentage": true,       // Show percentage number
  "ShowMessage": true           // Show status message
}
```

## How It Looks

```
┌────────────────────────────────────────┐
│ Saving game...                     45% │
│ ████████████░░░░░░░░░░░░░░░░░░░░       │
└────────────────────────────────────────┘
```

The bar changes color as it progresses:
- Yellow (0-33%)
- Yellow-Green (33-66%)
- Green (66-100%)

## Progress Stages

1. **0% - Preparing to save**
2. **25% - Saving game**
3. **60% - Processing new day**
4. **100% - Complete!**

## Benefits

- No more wondering if the game is frozen
- Especially helpful with heavily modded games (300+ mods)
- Visual feedback during long transitions
- Peace of mind

## Compatibility

✅ Works with all mods  
✅ Safe to add/remove anytime  
✅ No save file changes  
✅ Multiplayer compatible  

## Building From Source

Requirements:
- Visual Studio 2022 or VS Code
- .NET 6.0 SDK
- SMAPI installed

Steps:
```bash
dotnet build
```

The mod will automatically copy to your Mods folder.

## License

MIT License - Free to use and modify
