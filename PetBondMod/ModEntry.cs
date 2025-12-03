using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using SpaceCore;
using SpaceCore.Skills;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Buffs;
using StardewValley.Locations;

namespace PetBondMod
{
    /// <summary>
    /// Main SMAPI entry point for the Pet Bond skill mod. Registers the SpaceCore skill and
    /// starts lightweight XP hooks (petting once per day, filling the bowl, and pet friendship
    /// milestones). This file intentionally keeps the footprint small so future features
    /// (treats, profession effects) can be layered without reworking the bootstrap code.
    /// </summary>
    public class ModEntry : Mod
    {
        internal static IMonitor Log = null!;
        internal static IModHelper ModHelper = null!;

        private readonly DailyXpTracker _dailyXp = new();
        private readonly PetCareWatcher _petCare = new();
        private readonly HeartMilestoneTracker _heartMilestones = new();
        private readonly PetBondFeatureManager _features = new();

        public override void Entry(IModHelper helper)
        {
            Log = this.Monitor;
            ModHelper = helper;

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.Input.ButtonReleased += this.OnButtonReleased;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            Skills.RegisterSkill(new PetBondSkill(ModHelper));
            Log.Log("Registered Pet Bond skill via SpaceCore.", LogLevel.Info);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _dailyXp.Reset();
            _petCare.Reset();
            _heartMilestones.Reload(Game1.player);
            _features.OnDayStarted(Game1.player);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            _petCare.Update(Game1.player);
            _heartMilestones.Update(Game1.player);
            _features.OnUpdate(Game1.player);
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            _features.OnTimeChanged(Game1.player, e.NewTime);
        }

        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
                return;

            var farmer = Game1.player;
            Vector2 grabTile = farmer.GetGrabTile();
            Pet? pet = Game1.currentLocation?.characters?.OfType<Pet>()
                .FirstOrDefault(p => p.Tile == grabTile);

            if (pet != null && _dailyXp.TryMarkPetted())
            {
                GrantXp(farmer, PetBondSkill.Xp.Petting, "hud.xp.petted");
                _features.OnPetInteracted(farmer, pet);
            }
            else if (pet != null)
            {
                _features.OnPetInteracted(farmer, pet);
            }
        }

        internal static void GrantXp(Farmer farmer, int amount, string translationKey, params object[]? args)
        {
            Skills.AddExperience(farmer, PetBondSkill.SkillId, amount);
            Log.Log($"Granted {amount} XP ({translationKey}).", LogLevel.Trace);

            var withAmount = new List<object?> { amount };
            if (args != null)
                withAmount.AddRange(args);

            string text = ModHelper.Translation.Get(translationKey, withAmount.ToArray()!);
            Game1.addHUDMessage(new HUDMessage(text) { noIcon = true });
        }
    }

    internal sealed class PetBondSkill : Skill
    {
        public const string SkillId = "rhapsody.PetBond";

        private readonly IModHelper _helper;

        public PetBondSkill(IModHelper helper) : base(SkillId)
        {
            _helper = helper;
            this.Icon = IconFactory.CreateSkillIcon();
            this.SkillsPageIcon = IconFactory.CreatePageIcon();
            this.ExperienceBarColor = Color.HotPink;
            this.ExtraLevelUpInfo = BuildExtraLevelInfo();
            this.Professions = BuildProfessions();
        }

        public static class Xp
        {
            public const int Petting = 10;
            public const int WaterBowl = 5;
            public const int HeartOne = 20;
            public const int HeartThree = 40;
            public const int HeartFive = 80;
            public const int PetJob = 8;
            public const int Cleanup = 6;
        }

        public override string GetName() => _helper.Translation.Get("skill.name");

        public override string GetDescription() => _helper.Translation.Get("skill.description");

        private IList<string> BuildExtraLevelInfo()
        {
            // Placeholder text so the skill renders in SpaceCore menus. Replace with localized
            // summaries from PetBondSkill.md when content is ready.
            return new List<string>
            {
                _helper.Translation.Get("skill.level1"),
                _helper.Translation.Get("skill.level2"),
                _helper.Translation.Get("skill.level3"),
                _helper.Translation.Get("skill.level4"),
                _helper.Translation.Get("skill.level5"),
                _helper.Translation.Get("skill.level6"),
                _helper.Translation.Get("skill.level7"),
                _helper.Translation.Get("skill.level8"),
                _helper.Translation.Get("skill.level9"),
                _helper.Translation.Get("skill.level10"),
            };
        }

        private List<Profession> BuildProfessions()
        {
            // Uses constants to keep IDs stable for save data. These map to the design doc:
            // 5: Trail Scout / Home Guardian
            // 10: Pathfinder / Treasure Sniffer (Trail branch), Warden's Companion / Farm Sentinel (Guardian branch)
            var list = new List<Profession>();

            var trailScout = new Profession(ProfessionIds.TrailScout, _helper.Translation.Get("profession.trailscout.name"), _helper.Translation.Get("profession.trailscout.desc"));
            var homeGuardian = new Profession(ProfessionIds.HomeGuardian, _helper.Translation.Get("profession.homeguardian.name"), _helper.Translation.Get("profession.homeguardian.desc"));
            trailScout.Icon = IconFactory.CreateProfessionIcon(new Color(107, 173, 102), new Color(65, 115, 62));
            homeGuardian.Icon = IconFactory.CreateProfessionIcon(new Color(159, 136, 192), new Color(109, 86, 146));

            var pathfinder = new Profession(ProfessionIds.Pathfinder, _helper.Translation.Get("profession.pathfinder.name"), _helper.Translation.Get("profession.pathfinder.desc"));
            var treasureSniffer = new Profession(ProfessionIds.TreasureSniffer, _helper.Translation.Get("profession.treasuresniffer.name"), _helper.Translation.Get("profession.treasuresniffer.desc"));
            var wardensCompanion = new Profession(ProfessionIds.WardensCompanion, _helper.Translation.Get("profession.wardenscompanion.name"), _helper.Translation.Get("profession.wardenscompanion.desc"));
            var farmSentinel = new Profession(ProfessionIds.FarmSentinel, _helper.Translation.Get("profession.farmsentinel.name"), _helper.Translation.Get("profession.farmsentinel.desc"));

            pathfinder.Icon = IconFactory.CreateProfessionIcon(new Color(76, 159, 179), new Color(48, 108, 126));
            treasureSniffer.Icon = IconFactory.CreateProfessionIcon(new Color(193, 167, 83), new Color(143, 118, 37));
            wardensCompanion.Icon = IconFactory.CreateProfessionIcon(new Color(196, 111, 119), new Color(154, 61, 69));
            farmSentinel.Icon = IconFactory.CreateProfessionIcon(new Color(138, 188, 207), new Color(88, 138, 157));

            list.Add(trailScout);
            list.Add(homeGuardian);
            list.Add(pathfinder);
            list.Add(treasureSniffer);
            list.Add(wardensCompanion);
            list.Add(farmSentinel);

            return list;
        }

        public static class ProfessionIds
        {
            public const int TrailScout = 1_00;
            public const int HomeGuardian = 1_01;
            public const int Pathfinder = 2_00;
            public const int TreasureSniffer = 2_01;
            public const int WardensCompanion = 2_02;
            public const int FarmSentinel = 2_03;
        }
    }

    /// <summary>
    /// Tracks once-per-day XP sources so we don't double-award from repeated petting clicks.
    /// </summary>
    internal sealed class DailyXpTracker
    {
        private int _lastDay = -1;
        private bool _pettedToday;

        public void Reset()
        {
            _lastDay = Game1.dayOfMonth;
            _pettedToday = false;
        }

        public bool TryMarkPetted()
        {
            this.EnsureDay();
            if (_pettedToday)
                return false;

            _pettedToday = true;
            return true;
        }

        private void EnsureDay()
        {
            if (_lastDay == Game1.dayOfMonth)
                return;

            this.Reset();
        }
    }

    /// <summary>
    /// Watches pet-care actions that don't have dedicated events yet (like filling the water bowl)
    /// and grants XP when the state changes.
    /// </summary>
    internal sealed class PetCareWatcher
    {
        private bool _bowlFilledToday;

        public void Reset()
        {
            _bowlFilledToday = false;
        }

        public void Update(Farmer farmer)
        {
            if (_bowlFilledToday)
                return;

            Farm? farm = Game1.getFarm();
            if (farm != null && FarmStateHelpers.IsPetBowlWatered(farm))
            {
                _bowlFilledToday = true;
                ModEntry.GrantXp(farmer, PetBondSkill.Xp.WaterBowl, "hud.xp.bowl");
            }
        }
    }

    /// <summary>
    /// Tracks one-time XP awards when the pet reaches heart thresholds. Data persists per save.
    /// </summary>
    internal sealed class HeartMilestoneTracker
    {
        private const string DataKey = "rhapsody.PetBond/HeartMilestones";

        private readonly HashSet<int> _awardedHearts = new();
        private int _lastHearts;

        public void Reload(Farmer farmer)
        {
            _awardedHearts.Clear();
            _lastHearts = 0;

            if (!farmer.modData.TryGetValue(DataKey, out string? stored) || string.IsNullOrWhiteSpace(stored))
                return;

            foreach (string chunk in stored.Split(','))
            {
                if (int.TryParse(chunk, out int value))
                    _awardedHearts.Add(value);
            }
        }

        public void Update(Farmer farmer)
        {
            Pet? pet = Utility.GetPet(farmer);
            if (pet is null)
                return;

            int hearts = Math.Max(0, pet.friendshipTowardFarmer.Value / 200);
            if (hearts == _lastHearts)
                return;

            _lastHearts = hearts;
            GrantIfReady(farmer, hearts, 1, PetBondSkill.Xp.HeartOne);
            GrantIfReady(farmer, hearts, 3, PetBondSkill.Xp.HeartThree);
            GrantIfReady(farmer, hearts, 5, PetBondSkill.Xp.HeartFive);
        }

        private void GrantIfReady(Farmer farmer, int currentHearts, int target, int xp)
        {
            if (currentHearts < target || _awardedHearts.Contains(target))
                return;

            _awardedHearts.Add(target);
            SaveState(farmer);

            string suffix = target == 1 ? string.Empty : "s";
            ModEntry.GrantXp(farmer, xp, "hud.xp.heart", target, suffix);
        }

        private void SaveState(Farmer farmer)
        {
            farmer.modData[DataKey] = string.Join(",", _awardedHearts.OrderBy(h => h));
        }
    }

    /// <summary>
    /// Implements the bulk of the skill fantasy: buffs, daily pet jobs, profession perks,
    /// and proximity-based effects.
    /// </summary>
    internal sealed class PetBondFeatureManager
    {
        private readonly Random _rng = new();

        private bool _debuffClearedToday;
        private bool _emotionalSupportActive;
        private int _minutesNearPet;

        private int _lastDayApplied = -1;
        private bool _preMineBuffUsed;

        public void OnDayStarted(Farmer farmer)
        {
            _debuffClearedToday = false;
            _emotionalSupportActive = false;
            _minutesNearPet = 0;
            _preMineBuffUsed = false;
            _lastDayApplied = Game1.dayOfMonth;

            ApplyComfortedBuff(farmer);
            ApplyCozyMorning(farmer);
            BoostPetSpeed();

            RunDailyForageJob(farmer);
            RunNightlyCleanup(farmer);
            RunRareGuardianGift(farmer);
        }

        public void OnUpdate(Farmer farmer)
        {
            ApplyGuardianDefense(farmer);
            RemoveExpiredDefenseBuffs(farmer);
        }

        public void OnTimeChanged(Farmer farmer, int newTime)
        {
            TrackEmotionalSupport(farmer, newTime);
            RemoveExpiredBuffs(newTime);
        }

        public void OnPetInteracted(Farmer farmer, Pet pet)
        {
            if (Game1.timeOfDay < 610)
                _preMineBuffUsed = false;

            ApplyEmotionalSupportOnDemand(farmer);
            ClearNegativeDebuff(farmer);
            ApplyProfessionPettingBuffs(farmer);
        }

        private void ApplyComfortedBuff(Farmer farmer)
        {
            if (GetSkillLevel(farmer) < 1)
                return;

            Pet? pet = Utility.GetPet(farmer);
            if (pet is null)
                return;

            // Flavor: if pet was near its bowl/bed on the farm, tiny chance of a comfort buff.
            if (_rng.NextDouble() <= 0.05)
            {
                var buff = CreateBuff(120, effects => effects.LuckLevel.Value += 1, "petbond-comforted", "Comforted");
                Game1.player.applyBuff(buff);
            }
        }

        private void ApplyCozyMorning(Farmer farmer)
        {
            if (GetSkillLevel(farmer) < 8)
                return;

            Farm? farm = Game1.getFarm();
            if (farm == null || !Game1.isRaining || !FarmStateHelpers.IsPetBowlWatered(farm))
                return;

            var buff = CreateBuff(240, effects =>
            {
                effects.FarmingLevel.Value += 1;
                effects.LuckLevel.Value += 1;
            }, "petbond-cozy", "Cozy Morning");
            Game1.player.applyBuff(buff);
        }

        private void BoostPetSpeed()
        {
            if (GetSkillLevel(Game1.player) < 2)
                return;

            try
            {
                foreach (Pet pet in Game1.locations.SelectMany(l => l.characters.OfType<Pet>()))
                {
                    pet.speed = Math.Max(pet.speed + 1, 5);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Log.Log($"Failed to boost pet speed: {ex}", LogLevel.Trace);
            }
        }

        private void TrackEmotionalSupport(Farmer farmer, int newTime)
        {
            if (GetSkillLevel(farmer) < 3)
                return;

            Pet? pet = Utility.GetPet(farmer);
            if (pet is null || Game1.currentLocation != pet.currentLocation)
                return;

            if (Vector2.DistanceSquared(pet.Tile, farmer.Tile) <= 9)
            {
                _minutesNearPet += 10;
                if (_minutesNearPet >= 120 && !_emotionalSupportActive)
                {
                    _emotionalSupportActive = true;
                    int remainingMinutes = Math.Max(10, GetRemainingMinutes(newTime));
                    var buff = CreateBuff(remainingMinutes, effects => effects.DefenseLevel.Value += 1, "petbond-emotional", "Emotional Support");
                    Game1.player.applyBuff(buff);
                }
            }
        }

        private void ApplyEmotionalSupportOnDemand(Farmer farmer)
        {
            if (_emotionalSupportActive || GetSkillLevel(farmer) < 3)
                return;

            // If the player interacts with the pet after spending time nearby but before the timer triggered,
            // grant the buff immediately for the rest of the day.
            if (_minutesNearPet >= 60)
            {
                int remainingMinutes = Math.Max(10, GetRemainingMinutes(Game1.timeOfDay));
                var buff = CreateBuff(remainingMinutes, effects => effects.DefenseLevel.Value += 1, "petbond-emotional", "Emotional Support");
                Game1.player.applyBuff(buff);
                _emotionalSupportActive = true;
            }
        }

        private void ClearNegativeDebuff(Farmer farmer)
        {
            if (_debuffClearedToday || GetSkillLevel(farmer) < 9)
                return;

            bool hadDebuff = false;

            if (farmer.exhausted.Value)
            {
                farmer.exhausted.Value = false;
                hadDebuff = true;
            }

            if (hadDebuff)
            {
                _debuffClearedToday = true;
                Game1.addHUDMessage(new HUDMessage(ModEntry.ModHelper.Translation.Get("hud.cleanse")) { noIcon = true });
            }
        }

        private void ApplyGuardianDefense(Farmer farmer)
        {
            if (Game1.timeOfDay < 600)
                return;

            if (Game1.currentLocation is not Farm || !IsPetOutside())
            {
                RemoveDefenseBuffs();
                return;
            }

            int amount = 0;
            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.HomeGuardian))
                amount = 1;
            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.WardensCompanion))
                amount = 2;
            if (amount > 0)
            {
                var buff = CreateBuff(GetRemainingMinutes(Game1.timeOfDay), effects => effects.DefenseLevel.Value += amount, "petbond-guardian", "Guardian Defense");
                Game1.player.applyBuff(buff);
            }
            else
            {
                RemoveDefenseBuffs();
            }
        }

        private void RemoveDefenseBuffs()
        {
            Game1.player.buffs.Remove("petbond-guardian");
        }

        private void RemoveExpiredDefenseBuffs(Farmer farmer)
        {
            if (Game1.currentLocation is not Farm && !farmer.professions.Contains(PetBondSkill.ProfessionIds.HomeGuardian))
                RemoveDefenseBuffs();
        }

        private void RunDailyForageJob(Farmer farmer)
        {
            if (!farmer.professions.Contains(PetBondSkill.ProfessionIds.TrailScout))
                return;

            double chance = 0.15;
            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.Pathfinder))
                chance = 0.30;
            else if (farmer.professions.Contains(PetBondSkill.ProfessionIds.TreasureSniffer))
                chance = 0.20;

            if (_rng.NextDouble() > chance)
                return;

            Item? item = CreateForageItem(farmer);
            if (item == null)
                return;

            Farm? farm = Game1.getFarm();
            Vector2 dropTile = farm != null ? FarmStateHelpers.GetPetBowlPosition(farm) ?? new Vector2(64f, 15f) : new Vector2(64f, 15f);
            Game1.createItemDebris(item, dropTile * 64f, -1, farm);
            ModEntry.GrantXp(farmer, PetBondSkill.Xp.PetJob, "hud.xp.forage", item.DisplayName);
        }

        private void RunNightlyCleanup(Farmer farmer)
        {
            if (!farmer.professions.Contains(PetBondSkill.ProfessionIds.HomeGuardian))
                return;

            Farm? farm = Game1.getFarm();
            if (farm == null)
                return;

            int min = farmer.professions.Contains(PetBondSkill.ProfessionIds.FarmSentinel) ? 2 : 0;
            int max = farmer.professions.Contains(PetBondSkill.ProfessionIds.FarmSentinel) ? 4 : 2;
            int cleanupCount = _rng.Next(min, max + 1);

            var debris = farm.objects.Pairs
                .Where(pair => IsDebris(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(_ => _rng.Next())
                .Take(cleanupCount)
                .ToList();

            foreach (Vector2 tile in debris)
                farm.objects.Remove(tile);

            if (debris.Count > 0)
                ModEntry.GrantXp(farmer, PetBondSkill.Xp.Cleanup, "hud.xp.cleanup", debris.Count);
        }

        private void RunRareGuardianGift(Farmer farmer)
        {
            if (!farmer.professions.Contains(PetBondSkill.ProfessionIds.WardensCompanion))
                return;

            if (_rng.NextDouble() > 0.08)
                return;

            Item gift = CreateGuardianGift();
            Farm farm = Game1.getFarm();
            Vector2 dropTile = FarmStateHelpers.GetPetBowlPosition(farm) ?? new Vector2(64f, 15f);
            Game1.createItemDebris(gift, dropTile * 64f, -1, farm);
            Game1.addHUDMessage(new HUDMessage(ModEntry.ModHelper.Translation.Get("hud.guardianGift", new { item = gift.DisplayName })) { noIcon = true });
        }

        private void ApplyProfessionPettingBuffs(Farmer farmer)
        {
            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.TrailScout) && Game1.currentLocation is Farm)
            {
                var buff = CreateBuff(180, effects => effects.ForagingLevel.Value += 1, "petbond-trailscout", "Trail Scout");
                Game1.player.applyBuff(buff);
            }

            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.TreasureSniffer) && !_preMineBuffUsed && Game1.currentLocation is MineShaft)
            {
                var buff = CreateBuff(600, effects =>
                {
                    effects.LuckLevel.Value += 1;
                    effects.MiningLevel.Value += 1;
                }, "petbond-sniffer", "Treasure Sniffer");
                Game1.player.applyBuff(buff);
                _preMineBuffUsed = true;
            }

            if (farmer.professions.Contains(PetBondSkill.ProfessionIds.WardensCompanion) && Game1.currentLocation is MineShaft shaft && shaft.mineLevel <= 5)
            {
                var buff = CreateBuff(300, effects => effects.DefenseLevel.Value += 1, "petbond-warden", "Guardian Defense");
                Game1.player.applyBuff(buff);
            }
        }

        private Item? CreateForageItem(Farmer farmer)
        {
            bool treasure = farmer.professions.Contains(PetBondSkill.ProfessionIds.TreasureSniffer);
            if (treasure)
            {
                double roll = _rng.NextDouble();
                if (roll < 0.05)
                    return new StardewValley.Object(275, 1); // Artifact trove
                if (roll < 0.10)
                    return new StardewValley.Object(749, 1); // Omni geode
                if (roll < 0.20)
                    return new StardewValley.Object(535, 1); // Geode
            }

            bool mineralsAllowed = farmer.professions.Contains(PetBondSkill.ProfessionIds.Pathfinder);
            if (mineralsAllowed && _rng.NextDouble() < 0.25)
            {
                int[] mineralIds = { 80, 86, 535, 536 }; // Quartz, Earth Crystal, geodes
                return new StardewValley.Object(this.Choose(mineralIds), 1);
            }

            string season = Game1.currentSeason;
            int[] forageIds = season switch
            {
                "spring" => new[] { 16, 18, 20, 22 },
                "summer" => new[] { 398, 396, 402, 257 },
                "fall" => new[] { 404, 406, 408, 410 },
                _ => new[] { 412, 414, 283, 281 }
            };

            return new StardewValley.Object(this.Choose(forageIds), 1);
        }

        private Item CreateGuardianGift()
        {
            int[] giftIds = { 709, 335, 338, 766, 388 };
            return new StardewValley.Object(this.Choose(giftIds), _rng.Next(1, 4));
        }

        private void RemoveExpiredBuffs(int currentTime)
        {
            // Buff expiry is handled by the game's buff manager; no manual cleanup needed here.
        }

        private static bool IsDebris(StardewValley.Object obj)
        {
            int id = obj.ParentSheetIndex;
            return obj.Name is "Twig" or "Stone" or "Weeds" || (id >= 0 && id <= 23) || id is 294 or 295 or 297 || id == 343;
        }

        private bool IsPetOutside()
        {
            Pet? pet = Utility.GetPet(Game1.player);
            return pet?.currentLocation is Farm;
        }

        private Buff CreateBuff(int minutes, Action<BuffEffects> effectBuilder, string id, string description)
        {
            double durationMultiplier = GetDurationMultiplier(Game1.player);
            int adjustedMinutes = Math.Max(1, (int)Math.Round(minutes * durationMultiplier));

            var effects = new BuffEffects();
            effectBuilder(effects);

            var buff = new Buff(id, source: "Pet Bond", displaySource: "Pet Bond")
            {
                description = description,
                millisecondsDuration = adjustedMinutes * 700,
                effects = effects
            };
            return buff;
        }

        private double GetDurationMultiplier(Farmer farmer)
        {
            return GetSkillLevel(farmer) >= 7 ? 1.25 : 1.0;
        }

        private static int GetSkillLevel(Farmer farmer)
        {
            return Skills.GetSkillLevel(farmer, PetBondSkill.SkillId);
        }

        private static int GetRemainingMinutes(int timeOfDay)
        {
            int hour = timeOfDay / 100;
            int minute = timeOfDay % 100;
            int remainingHours = Math.Max(0, 26 - hour); // allow past midnight buffer
            return remainingHours * 60 - minute;
        }

        private T Choose<T>(IList<T> source)
        {
            return source[_rng.Next(source.Count)];
        }
    }

    internal static class IconFactory
    {
        private const int IconSize = 16;

        public static Texture2D CreateSkillIcon()
        {
            return CreateCheckeredTexture(new Color(84, 167, 226), new Color(45, 112, 171));
        }

        public static Texture2D CreatePageIcon()
        {
            return CreateDiagonalTexture(new Color(139, 201, 232), new Color(74, 132, 187));
        }

        public static Texture2D CreateProfessionIcon(Color primary, Color secondary)
        {
            return CreateDiagonalTexture(primary, secondary);
        }

        private static Texture2D CreateCheckeredTexture(Color light, Color dark)
        {
            return CreateTexture((x, y) => ((x / 4 + y / 4) % 2 == 0) ? light : dark);
        }

        private static Texture2D CreateDiagonalTexture(Color primary, Color secondary)
        {
            return CreateTexture((x, y) => x >= y ? primary : secondary);
        }

        private static Texture2D CreateTexture(Func<int, int, Color> selector)
        {
            var device = Game1.graphics?.GraphicsDevice ?? Game1.game1.GraphicsDevice;
            var texture = new Texture2D(device, IconSize, IconSize);

            var data = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    data[y * IconSize + x] = selector(x, y);
                }
            }

            texture.SetData(data);
            return texture;
        }
    }

    internal static class FarmStateHelpers
    {
        public static bool IsPetBowlWatered(Farm farm)
        {
            var property = farm.GetType().GetProperty("wasPetBowlWatered") ?? farm.GetType().GetProperty("petBowlWatered");
            if (property?.GetValue(farm) is Netcode.NetBool netBool)
                return netBool.Value;
            if (property?.GetValue(farm) is bool rawBool)
                return rawBool;
            return false;
        }

        public static Vector2? GetPetBowlPosition(Farm farm)
        {
            var property = farm.GetType().GetProperty("petBowlPosition");
            if (property?.GetValue(farm) is Netcode.NetVector2 netVec)
                return netVec.Value;
            if (property?.GetValue(farm) is Vector2 vector)
                return vector;
            return null;
        }
    }
}
