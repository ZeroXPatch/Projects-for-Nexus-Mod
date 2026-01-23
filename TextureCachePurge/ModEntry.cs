private void PerformCacheClear(string type)
{
    try
    {
        this.Helper.GameContent.InvalidateCache(asset =>
        {
            if (asset.DataType != typeof(Texture2D)) return false;

            string name = asset.Name.Name;
            if (name.StartsWith("LooseSprites/Cursors") ||
                name.StartsWith("LooseSprites/font") ||
                name.StartsWith("LooseSprites/ControllerMaps"))
            {
                return false;
            }

            return true;
        });

        // --- RESTORED LOGIC ---
        // We always run GC.Collect() because that's the whole point of the mod.
        GC.Collect();

        // If "Aggressive Memory Clean" is ON (Default), we force the game to wait.
        // This makes the RAM number drop instantly but causes a momentary freeze.
        if (this.Config.ForceGarbageCollection)
        {
            GC.WaitForPendingFinalizers();
        }

        if (type == "Manual" || type == "Auto-RAM")
            Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.purged"), 2));
    }
    catch (Exception ex)
    {
        this.Monitor.Log($"Cache purge failed: {ex.Message}", LogLevel.Error);
    }
}