# Automate No More Lag - Installation Guide

## Quick Start

### Prerequisites
1. **Stardew Valley** (PC version via Steam, GOG, or Windows Store)
2. **SMAPI 4.0.0+** - [Download here](https://smapi.io/)
3. **Automate mod** - [Download from Nexus](https://www.nexusmods.com/stardewvalley/mods/1063)

### Optional but Recommended
- **Generic Mod Config Menu** - [Download here](https://www.nexusmods.com/stardewvalley/mods/5098)

## Step-by-Step Installation

### 1. Install SMAPI
If you haven't already:
- Download SMAPI from https://smapi.io/
- Extract the download
- Run `install on Windows.bat` (or appropriate file for your OS)
- Follow the on-screen instructions

### 2. Install Automate
- Download Automate from Nexus Mods
- Extract the `Automate` folder
- Place it in your `Stardew Valley/Mods` folder
- The path should look like: `Stardew Valley/Mods/Automate/`

### 3. Install ZeroXPatch
- Extract this mod's folder
- Place the `ZeroXPatch` folder in your `Stardew Valley/Mods` folder
- The path should look like: `Stardew Valley/Mods/ZeroXPatch/`

### 4. (Optional) Install Generic Mod Config Menu
- Download Generic Mod Config Menu from Nexus
- Extract and place in your Mods folder
- This allows you to configure ZeroXPatch in-game

### 5. Launch the Game
- Run Stardew Valley through SMAPI (not the regular launcher)
- Look for green text in the SMAPI console confirming ZeroXPatch loaded
- You should see: "Successfully patched Automate for performance improvements"

## Verifying Installation

### Check SMAPI Console
When the game starts, you should see:
```
[ZeroXPatch] Successfully patched Automate for performance improvements.
[ZeroXPatch] ZeroXPatch is active. If you experience issues with Automate...
```

### In-Game Check
1. Open the game menu
2. If you have Generic Mod Config Menu, click "Mod Options"
3. Look for "ZeroXPatch" in the list
4. If it's there, installation was successful!

## Folder Structure

Your Mods folder should look like this:
```
Stardew Valley/
└── Mods/
    ├── Automate/
    │   ├── Automate.dll
    │   ├── manifest.json
    │   └── ...
    ├── ZeroXPatch/
    │   ├── ZeroXPatch.dll
    │   ├── manifest.json
    │   ├── config.json
    │   └── README.md
    └── (other mods)
```

## Configuration

### First Time Setup
The mod works out-of-the-box with sensible defaults. However, you can customize it:

**Option 1: Generic Mod Config Menu (Easy)**
1. Open game menu
2. Click "Mod Options"
3. Select "ZeroXPatch"
4. Adjust sliders and toggles
5. Changes save automatically

**Option 2: Edit config.json (Advanced)**
1. Navigate to `Stardew Valley/Mods/ZeroXPatch/`
2. Open `config.json` in a text editor
3. Modify values (see README.md for explanations)
4. Save the file
5. Restart the game

## Testing Performance Improvements

### Before/After Comparison
1. **Before**: Note your FPS and any lag when automating many machines
2. **Install ZeroXPatch** as described above
3. **After**: Compare FPS and responsiveness

### Monitor Performance
Enable performance logging:
1. Set `"EnablePerformanceLogging": true` in config.json
2. Play for 2-3 in-game days
3. Check SMAPI console for performance metrics
4. You'll see stats like "Avg Processing Time" and "Total Time Saved"

## Common Issues

### "ZeroXPatch failed to load"
- Make sure Automate is installed first
- Check that SMAPI is up to date (4.0.0+)
- Verify all files are in the correct folders

### "Could not find Automate assembly"
- Automate must be installed and loaded before ZeroXPatch
- Check SMAPI console to see if Automate loaded successfully
- Try reinstalling Automate

### Automation stopped working
1. Disable ZeroXPatch temporarily (rename the folder)
2. Check if Automate works alone
3. If yes, adjust ZeroXPatch settings (lower UpdateIntervalMultiplier)
4. If no, issue is with Automate, not ZeroXPatch

### Game crashes on startup
- Check SMAPI log for errors
- Ensure you're using compatible versions
- Try updating all mods

## Performance Tuning

### If you have minor lag:
Use default settings or "Balanced Performance" preset

### If you have significant lag:
1. Start with "Performance Focus" preset
2. Monitor improvements
3. Adjust further if needed

### If you have massive lag (500+ machines):
1. Use "Maximum Performance" preset
2. Consider reducing machine count
3. May need to accept slower automation

## Getting Help

### Before Asking for Help
1. Check this guide thoroughly
2. Review the main README.md
3. Check SMAPI console for error messages
4. Try disabling ZeroXPatch to isolate the issue

### Where to Get Help
- SMAPI log: Upload to https://smapi.io/log
- Stardew Valley modding Discord
- Nexus Mods page (if published there)
- GitHub issues (if repository is public)

### Important Note
**If Automate behaves unexpectedly, test with ZeroXPatch disabled before reporting to Automate's page!**

## Updating the Mod

1. Download the new version
2. Delete the old `ZeroXPatch` folder from Mods
3. Extract and place the new version
4. Your config.json will be recreated with defaults
5. Reconfigure if needed (or backup config.json first)

## Uninstalling

To remove ZeroXPatch:
1. Close the game
2. Navigate to `Stardew Valley/Mods/`
3. Delete the `ZeroXPatch` folder
4. Automate will work normally again

## Tips for Best Results

1. **Start Conservative**: Use default settings first
2. **Measure Impact**: Enable performance logging to see actual improvements
3. **Adjust Gradually**: Change one setting at a time
4. **Monitor FPS**: Watch your frame rate to gauge improvements
5. **Balance Speed vs Performance**: Higher performance = slower automation

## Advanced: Building from Source

For developers who want to compile the mod:

### Requirements
- Visual Studio 2022 or later
- .NET 6.0 SDK
- Stardew Valley installed

### Build Steps
1. Clone or download source code
2. Open `ZeroXPatch.csproj` in Visual Studio
3. Ensure NuGet packages restore correctly
4. Build the project (Ctrl+Shift+B)
5. Mod will auto-copy to your Mods folder (if configured)

The ModBuildConfig package handles most build configuration automatically.

---

**Enjoy lag-free farming with ZeroXPatch! 🚜🌾**
