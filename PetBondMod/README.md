# Pet Bond Mod – build and packaging guide

This repository already contains the SMAPI entry point, manifest, and i18n strings. Follow these steps to turn it into the `PetBondMod.dll` you can drop into Stardew Valley's `Mods` folder.

## Prerequisites
- **.NET 5 SDK** installed (matching the `net5.0` target in `PetBondMod.csproj`).
- **SMAPI + Stardew Valley** installed on the same machine. The build helper package `Pathoschild.Stardew.ModBuildConfig` will try to locate the game/SMAPI automatically; if it cannot, you'll point it to the install path in the build command.

## Build the DLL
From the repo root, run:

```bash
# Restore NuGet packages and build
 dotnet build PetBondMod/PetBondMod.csproj -c Release
```

The compiled DLL will land at `PetBondMod/bin/Release/net5.0/PetBondMod.dll` alongside the copied `manifest.json` and `i18n/` folders. Skill and profession icons are generated at runtime, so no binary art assets are required.

### If the game path isn't detected automatically
Add a `GamePath` property pointing to your Stardew install (the folder that contains `Stardew Valley.exe`):

```bash
dotnet build PetBondMod/PetBondMod.csproj -c Release -p:GamePath="/path/to/Stardew Valley"
```

On Windows PowerShell, escape the space as ``"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Stardew Valley"``.

## Install and test in-game
1. Create a new folder in `Stardew Valley/Mods` named `PetBondMod` (or reuse the existing one if you're updating).
2. Copy the build output from `PetBondMod/bin/Release/net5.0/` into that folder. It should include:
   - `PetBondMod.dll`
   - `manifest.json`
   - `i18n/` directory (copied during the build).
3. Ensure **SpaceCore** is installed in `Mods` (it's a required dependency listed in `manifest.json`).
4. Launch Stardew Valley via SMAPI. You should see the Pet Bond skill on the SpaceCore skill page and XP toasts when pet interactions occur.

## Troubleshooting tips
- If `dotnet build` complains about missing SMAPI references, double-check that the SMAPI installer ran on this machine; ModBuildConfig reads SMAPI's reference assemblies from the game directory.
- If you're on Linux or macOS via Steam/Proton, pass `-p:GamePath="/path/to/steamapps/common/Stardew Valley"` explicitly.
- Rebuild after updating localization (or any future assets you add); the `CopyToOutputDirectory` settings in the project file ensure the fresh files land next to the DLL.
