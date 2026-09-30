using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[BepInPlugin("codex.longyin.trainer", "LongYin Trainer", "0.4.29.5")]
public sealed class LongYinTrainerPlugin : BasePlugin
{
    private sealed class SkillTalentState
    {
        public bool Eligible;
        public HeroData? Hero;
        public KungfuSkillLvData? Skill;
        public int LevelBefore;
        public int Tier;
    }

    internal static ManualLogSource Logger = null!;
    private Harmony? _harmony;
    internal static ConfigEntry<KeyCode> ToggleKey = null!;
    internal static ConfigEntry<bool> LockEquipmentCraftValue = null!;
    internal static ConfigEntry<bool> LockMedicineCraftValue = null!;
    internal static ConfigEntry<bool> LockFoodCraftValue = null!;
    internal static ConfigEntry<float> EquipmentCraftValue = null!;
    internal static ConfigEntry<float> MedicineCraftValue = null!;
    internal static ConfigEntry<float> FoodCraftValue = null!;
    internal static ConfigEntry<bool> LockCraftTime = null!;
    internal static ConfigEntry<int> CraftTimeValue = null!;
    internal static ConfigEntry<bool> InstantCraft = null!;
    internal static ConfigEntry<int> MinimumCraftRarity = null!;
    internal static ConfigEntry<int> PlayerTalentSlotLimit = null!;
    internal static ConfigEntry<int> StartTalentSlotLimit = null!;
    internal static ConfigEntry<bool> UnlockInheritedTalents = null!;
    internal static ConfigEntry<bool> UnlockAdvancedStartTalents = null!;
    internal static ConfigEntry<bool> LockExploreStamina = null!;
    internal static ConfigEntry<bool> FreezeDate = null!;
    internal static ConfigEntry<int> BookExpMultiplier = null!;
    internal static ConfigEntry<int> BattleSkillExpMultiplier = null!;
    internal static ConfigEntry<int> CreationPointMultiplier = null!;
    internal static ConfigEntry<float> BattleSpeedMultiplier = null!;
    internal static ConfigEntry<float> HorseBaseSpeedMultiplier = null!;
    internal static ConfigEntry<float> HorseSprintSpeedMultiplier = null!;
    internal static ConfigEntry<float> HorseSprintDurationMultiplier = null!;
    internal static ConfigEntry<float> HorseSprintCooldownMultiplier = null!;
    internal static ConfigEntry<float> HorseStaminaMultiplier = null!;
    internal static ConfigEntry<bool> SkipStartupIntro = null!;
    internal static ConfigEntry<bool> EnableSkillTalentGrant = null!;
    internal static ConfigEntry<bool> ShowAllBigMapEvents = null!;
    internal static ConfigEntry<int> SkillTalentLevelThreshold = null!;
    internal static ConfigEntry<float> SkillTalentTierPointMultiplier = null!;
    private static bool _insideStartTalentBypass;

    public override void Load()
    {
        Logger = Log;
        ToggleKey = Config.Bind("General", "TogglePanelKey", KeyCode.H, "Hotkey used to open or close the trainer panel.");
        LockEquipmentCraftValue = Config.Bind("Craft", "LockEquipmentValue", false, "Locks the value budget used when crafting equipment.");
        LockMedicineCraftValue = Config.Bind("Craft", "LockMedicineValue", false, "Locks the value budget used when crafting medicine.");
        LockFoodCraftValue = Config.Bind("Craft", "LockFoodValue", false, "Locks the value budget used when cooking food.");
        EquipmentCraftValue = Config.Bind("Craft", "EquipmentValue", 800f, "Target equipment crafting value. Accepts any finite non-negative value; extreme values may overflow the game's own formulas.");
        MedicineCraftValue = Config.Bind("Craft", "MedicineValue", 800f, "Target medicine crafting value. Accepts any finite non-negative value; extreme values may overflow the game's own formulas.");
        FoodCraftValue = Config.Bind("Craft", "FoodValue", 800f, "Target food crafting value. Accepts any finite non-negative value; extreme values may overflow the game's own formulas.");
        InstantCraft = Config.Bind("Craft", "InstantCraft", false, "Sets crafting time to zero days.");
        LockCraftTime = Config.Bind("Craft", "LockCraftTime", InstantCraft.Value, "Locks the crafting time in days.");
        CraftTimeValue = Config.Bind("Craft", "CraftTimeValue", InstantCraft.Value ? 0 : 1, "Crafting time in days. Zero is supported.");
        MinimumCraftRarity = Config.Bind("Craft", "MinimumResultRarity", 0, "Minimum rarity level applied to generated craft choices. 0 disables it.");
        PlayerTalentSlotLimit = Config.Bind("Hero", "TalentSlotLimit", 30, "Minimum number of permanent talent slots available to the player hero.");
        StartTalentSlotLimit = Config.Bind("Hero", "StartTalentSlotLimit", 5, "Talent-slot limit used only while creating a new character. Valid range: 1-1000.");
        UnlockInheritedTalents = Config.Bind("Hero", "UnlockInheritedTalents", false, "Makes ending-linked inherited talents selectable during character creation without unlocking platform achievements.");
        UnlockAdvancedStartTalents = Config.Bind("Hero", "UnlockAdvancedStartTalents", true, "Allows talents in the Advanced category to be selected during character creation regardless of their normal initial-learning requirements.");
        LockExploreStamina = Config.Bind("Extended", "LockExploreStamina", false, "Prevents exploration stamina from decreasing.");
        FreezeDate = Config.Bind("Extended", "FreezeDate", false, "Prevents in-game day progression while enabled.");
        BookExpMultiplier = Config.Bind("Extended", "BookExpMultiplier", 1, "Multiplier for EXP gained by the player from reading books.");
        BattleSkillExpMultiplier = Config.Bind("Extended", "BattleSkillExpMultiplier", 1, "Multiplier for martial-skill EXP gained by the player in battle.");
        CreationPointMultiplier = Config.Bind("Extended", "CreationPointMultiplier", 1, "Multiplier for character-creation attribute and skill point pools.");
        BattleSpeedMultiplier = Config.Bind("Extended", "BattleSpeedMultiplier", 1f, "Multiplier applied after selecting an in-battle speed button.");
        HorseBaseSpeedMultiplier = Config.Bind("Extended", "HorseBaseSpeedMultiplier", 1f, "Multiplier for the player's horse travel speed.");
        HorseSprintSpeedMultiplier = Config.Bind("Extended", "HorseSprintSpeedMultiplier", 1f, "Additional multiplier while the player's horse is sprinting.");
        HorseSprintDurationMultiplier = Config.Bind("Extended", "HorseSprintDurationMultiplier", 1f, "Multiplier for horse sprint duration.");
        HorseSprintCooldownMultiplier = Config.Bind("Extended", "HorseSprintCooldownMultiplier", 1f, "Multiplier for horse sprint cooldown; values below one shorten it.");
        HorseStaminaMultiplier = Config.Bind("Extended", "HorseStaminaMultiplier", 1f, "Scales equipped-horse stamina changes. Values above one reduce both drain and recovery per change.");
        SkipStartupIntro = Config.Bind("Extended", "SkipStartupIntro", false, "Skips the startup logo video on the next game launch.");
        EnableSkillTalentGrant = Config.Bind("Extended", "EnableSkillTalentGrant", false, "Grants talent points when a player martial skill reaches the configured level threshold.");
        ShowAllBigMapEvents = Config.Bind("Extended", "ShowAllBigMapEvents", false, "Shows every generated random-event icon on the big map without changing event discovery state in the save.");
        SkillTalentLevelThreshold = Config.Bind("Extended", "SkillTalentLevelThreshold", 10, "Skill level that triggers the talent-point grant.");
        SkillTalentTierPointMultiplier = Config.Bind("Extended", "SkillTalentTierPointMultiplier", 2f, "Talent points granted per skill rarity tier when the threshold is crossed.");
        // Preserve every in-range value instead of snapping to resource tiers.
        EquipmentCraftValue.Value = SafeCraftValue(EquipmentCraftValue.Value);
        MedicineCraftValue.Value = SafeCraftValue(MedicineCraftValue.Value);
        FoodCraftValue.Value = SafeCraftValue(FoodCraftValue.Value);
        CraftTimeValue.Value = SafeCraftTime(CraftTimeValue.Value);
        PlayerTalentSlotLimit.Value = SafeTalentSlotLimit(PlayerTalentSlotLimit.Value);
        StartTalentSlotLimit.Value = SafeStartTalentSlotLimit(StartTalentSlotLimit.Value);
        BookExpMultiplier.Value = SafeExpMultiplier(BookExpMultiplier.Value);
        BattleSkillExpMultiplier.Value = SafeExpMultiplier(BattleSkillExpMultiplier.Value);
        CreationPointMultiplier.Value = SafeCreationMultiplier(CreationPointMultiplier.Value);
        BattleSpeedMultiplier.Value = SafeSpeedMultiplier(BattleSpeedMultiplier.Value);
        HorseBaseSpeedMultiplier.Value = SafeHorseMultiplier(HorseBaseSpeedMultiplier.Value);
        HorseSprintSpeedMultiplier.Value = SafeHorseMultiplier(HorseSprintSpeedMultiplier.Value);
        HorseSprintDurationMultiplier.Value = SafeHorseMultiplier(HorseSprintDurationMultiplier.Value);
        HorseSprintCooldownMultiplier.Value = SafeHorseMultiplier(HorseSprintCooldownMultiplier.Value);
        HorseStaminaMultiplier.Value = SafeHorseMultiplier(HorseStaminaMultiplier.Value);
        SkillTalentLevelThreshold.Value = SafeSkillLevelThreshold(SkillTalentLevelThreshold.Value);
        SkillTalentTierPointMultiplier.Value = SafeTalentPointMultiplier(SkillTalentTierPointMultiplier.Value);
        Config.Save();
        TrainerBehaviour.InitializeCustomKungfuStorage();
        _harmony = new Harmony("codex.longyin.trainer");
        BuildingCombat.Install(_harmony, Config);
        foreach (var method in typeof(AreaData).GetMethods())
            if (method.Name is "ChangePeople" or "ChangeSafe" or "ChangeSupport" or "ChangeDefence" or "ChangeAreaState" or "ResetAllState")
                _harmony.Patch(method, postfix: new HarmonyMethod(typeof(LongYinTrainerPlugin), nameof(CityStatePostfix)));
        var kungfuLevelCtor = AccessTools.Constructor(typeof(KungfuSkillLvData), new[] { typeof(int) });
        if (kungfuLevelCtor != null)
            _harmony.Patch(kungfuLevelCtor,
                new HarmonyMethod(AccessTools.Method(typeof(LongYinTrainerPlugin), nameof(CustomKungfuLevelCtorPrefix))),
                new HarmonyMethod(AccessTools.Method(typeof(LongYinTrainerPlugin), nameof(CustomKungfuLevelCtorPostfix))));
        else Log.LogWarning("Constructor not found: KungfuSkillLvData(int)");
        Patch(typeof(GameDataController), nameof(GameDataController.Update), Type.EmptyTypes, null, nameof(GameUpdatePostfix));
        Patch(typeof(GameController), nameof(GameController.CountHeroData), new[] { typeof(HeroData) }, nameof(CountHeroDataPrefix), null);
        Patch(typeof(ItemIconController), nameof(ItemIconController.OnClick), Type.EmptyTypes, null, nameof(ItemClickedPostfix));
        Patch(typeof(HeroData), nameof(HeroData.GetMaxTagNum), Type.EmptyTypes, null, nameof(GetMaxTagNumPostfix));
        Patch(typeof(CraftUIController), nameof(CraftUIController.GetCraftFinalValue), Type.EmptyTypes, null, nameof(CraftFinalValuePostfix));
        Patch(typeof(CraftUIController), nameof(CraftUIController.GetCraftTime), Type.EmptyTypes, null, nameof(CraftTimePostfix));
        Patch(typeof(CraftUIController), nameof(CraftUIController.ShowCraftResultChoosePanel), Type.EmptyTypes, null, nameof(CraftResultsPostfix));
        Patch(typeof(ExploreController), nameof(ExploreController.ChangeMoveStep), new[] { typeof(int) }, nameof(ExploreStaminaPrefix), null);
        Patch(typeof(ExploreController), nameof(ExploreController.ChangeMoveStep), new[] { typeof(int), typeof(bool) }, nameof(ExploreStaminaPrefix), null);
        Patch(typeof(HeroData), nameof(HeroData.AddSkillBookExp), new[] { typeof(float), typeof(KungfuSkillLvData), typeof(bool) }, nameof(BookExpPrefix), nameof(SkillTalentPostfix));
        Patch(typeof(HeroData), nameof(HeroData.BattleChangeSkillFightExp), new[] { typeof(float), typeof(KungfuSkillLvData), typeof(bool) }, nameof(BattleSkillExpPrefix), nameof(SkillTalentPostfix));
        Patch(typeof(StartMenuController), nameof(StartMenuController.SetAttriPreset), new[] { typeof(int) }, null, nameof(CreationPointsPostfix));
        Patch(typeof(StartMenuController), nameof(StartMenuController.ResetPlayerAttri), Type.EmptyTypes, null, nameof(CreationPointsPostfix));
        Patch(typeof(StartMenuController), nameof(StartMenuController.Update), Type.EmptyTypes, null, nameof(StartMenuUpdatePostfix));
        Patch(typeof(StartMenuController), nameof(StartMenuController.RefreshTagMenu), Type.EmptyTypes, null, nameof(StartTalentMenuRefreshedPostfix));
        Patch(typeof(StartMenuController), nameof(StartMenuController.StartChooseTagClicked), new[] { typeof(int) }, nameof(StartChooseTalentPrefix), null);
        Patch(typeof(StartMenuController), nameof(StartMenuController.CheckMeetCondition), new[] { typeof(HeroData), typeof(HeroTagDataBase) }, null, nameof(StartTalentConditionPostfix));
        Patch(typeof(HeroData), nameof(HeroData.GetHeroPermanentTagNum), Type.EmptyTypes, null, nameof(StartTalentCountForLimitPostfix));
        Patch(typeof(HeroTagData), nameof(HeroTagData.StartChooseAble), Type.EmptyTypes, null, nameof(StartTalentAvailabilityPostfix));
        Patch(typeof(PlayerPrefDictionary), nameof(PlayerPrefDictionary.ContainsKey), new[] { typeof(string) }, nameof(InheritedTalentPrefContainsPrefix), null);
        Patch(typeof(PlayerPrefDictionary), nameof(PlayerPrefDictionary.GetString), new[] { typeof(string) }, nameof(InheritedTalentPrefStringPrefix), null);
        Patch(typeof(PlayerPrefDictionary), nameof(PlayerPrefDictionary.GetInt), new[] { typeof(string) }, nameof(InheritedTalentPrefIntPrefix), null);
        Patch(typeof(BattleController), nameof(BattleController.BattleTimeScaleButtonClicked), new[] { typeof(GameObject) }, null, nameof(BattleSpeedPostfix));
        Patch(typeof(BattleController), nameof(BattleController.BattleRealEnd), Type.EmptyTypes, null, nameof(CustomKungfuBattleEndedPostfix));
        Patch(typeof(GameController), nameof(GameController.ChangeDay), Type.EmptyTypes, nameof(FreezeDatePrefix), null);
        Patch(typeof(GameController), nameof(GameController.ChangeDay), new[] { typeof(int) }, nameof(FreezeDatePrefix), null);
        Patch(typeof(HorseData), nameof(HorseData.StartSprint), Type.EmptyTypes, null, nameof(HorseSprintPostfix));
        Patch(typeof(HorseData), nameof(HorseData.ChangeNowPower), new[] { typeof(float) }, nameof(HorseStaminaPrefix), null);
        Patch(typeof(HeroData), nameof(HeroData.GetHorseTravelSpeed), Type.EmptyTypes, null, nameof(HorseSpeedPostfix));
        Patch(typeof(HeroData), nameof(HeroData.GetHorseTravelSpeed), new[] { typeof(bool), typeof(bool) }, null, nameof(HorseSpeedWithStatePostfix));
        Patch(typeof(HeroData), nameof(HeroData.GetFinalTravelSpeed), Type.EmptyTypes, null, nameof(TravelSpeedPostfix));
        Patch(typeof(BreakThroughController), nameof(BreakThroughController.StartBreakThrough), new[] { typeof(KungfuSkillLvData), typeof(bool) }, null, nameof(BreakThroughStartedPostfix));
        Patch(typeof(BreakThroughController), nameof(BreakThroughController.BreakThroughChoiceClicked), new[] { typeof(BreakThroughChoiceController) }, nameof(BreakThroughChoicePrefix), null);
        Patch(typeof(GameDataController), nameof(GameDataController.LoadAllGameData), Type.EmptyTypes, null, nameof(CustomKungfuDatabasePostfix));
        Patch(typeof(GameDataController), nameof(GameDataController.Load), new[] { typeof(int) }, nameof(CustomKungfuDatabasePrefix), null);
        Patch(typeof(GameDataController), nameof(GameDataController.GameDataIntoGame), Type.EmptyTypes, nameof(CustomKungfuDatabasePrefix), null);
        Patch(typeof(BuildingUIController), nameof(BuildingUIController.GenerateBuildingButton), Type.EmptyTypes, null, nameof(BuildingButtonsPostfix));
        Patch(typeof(BuildingButtonController), nameof(BuildingButtonController.OnClick), Type.EmptyTypes, nameof(BuildingButtonPrefix), null);
        Patch(typeof(KungfuSkillData), nameof(KungfuSkillData.GetSkillIcon), Type.EmptyTypes, null, nameof(KungfuDataIconPostfix));
        Patch(typeof(KungfuSkillLvData), nameof(KungfuSkillLvData.GetSkillIcon), Type.EmptyTypes, null, nameof(KungfuLevelIconPostfix));
        Patch(typeof(TextureController), nameof(TextureController.LoadAtlasSprite), new[] { typeof(string), typeof(string) }, nameof(CustomIconAtlasPrefix), null);
        Patch(typeof(KungfuSkillLvData), nameof(KungfuSkillLvData.CDTimeTotal), Type.EmptyTypes, nameof(CustomKungfuCooldownPrefix), null);
        Patch(typeof(GameDataController), nameof(GameDataController.GetSkillDataBase), new[] { typeof(int) }, nameof(CustomKungfuDatabaseLookupPrefix), null);
        Patch(typeof(KungfuSkillLvData), nameof(KungfuSkillLvData.DataBase), Type.EmptyTypes, nameof(CustomKungfuLevelDatabasePrefix), null);
        Patch(typeof(SkillIconController), nameof(SkillIconController.Update), Type.EmptyTypes, null, nameof(CustomKungfuSkillIconUpdatePostfix));
        Patch(typeof(QuickDetail), nameof(QuickDetail.RefreshSkillRangeUI), new[] { typeof(KungfuSkillLvData) }, nameof(CustomKungfuQuickRangePrefix), null);
        Patch(typeof(BigMapRandomEventController), nameof(BigMapRandomEventController.Init), Type.EmptyTypes, null, nameof(BigMapEventInitPostfix));
        Patch(typeof(BigMapController), nameof(BigMapController.RecreatAllBigMapRandomEvent), Type.EmptyTypes, nameof(BigMapEventsRecreatedPrefix), nameof(BigMapEventsRecreatedPostfix));
        // Do not attach another permanent frame-by-frame Update hook while the
        // option is disabled.  On IL2CPP this eventually created enough native
        // wrapper churn to reproduce GameAssembly.dll access violations while
        // idling at the title screen.  The setting intentionally takes effect
        // on the next launch, so patch the short intro flow only when requested.
        if (SkipStartupIntro.Value)
        {
            Patch(typeof(EnterSceneController), nameof(EnterSceneController.Start), Type.EmptyTypes, null, nameof(EnterScenePostfix));
            Patch(typeof(EnterSceneController), nameof(EnterSceneController.Update), Type.EmptyTypes, null, nameof(EnterScenePostfix));
        }
        Patch(typeof(BigmapNpcController), nameof(BigmapNpcController.FixedUpdate), Type.EmptyTypes, null, nameof(FullVisionPostfix));
        foreach (var method in typeof(ForceData).GetMethods())
            if (method.Name == "ChangeResource" || method.Name == "CostResource")
                _harmony!.Patch(method, postfix: new HarmonyMethod(typeof(LongYinTrainerPlugin), nameof(ForceResourcePostfix)));
        Log.LogInfo($"LongYin Trainer loaded. Press {ToggleKey.Value} to open/close the panel.");
    }

    private static void ForceResourcePostfix(ForceData __instance) => TrainerBehaviour.EnforceForceLock(__instance);
    private static void CityStatePostfix(AreaData __instance)
    {
        if (CityStateLock.HasLocks) CityStateLock.Enforce(GameController.Instance?.worldData, __instance);
    }
    private static void FullVisionPostfix(BigmapNpcController __instance) => TrainerBehaviour.ApplyNativeFullVision(__instance);

    private void Patch(Type type, string name, Type[] args, string? prefix, string? postfix)
    {
        var target = AccessTools.Method(type, name, args);
        if (target == null)
        {
            Log.LogWarning($"Method not found: {type.Name}.{name}");
            return;
        }
        _harmony!.Patch(target,
            prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(LongYinTrainerPlugin), prefix)),
            postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(LongYinTrainerPlugin), postfix)));
    }

    private static void ItemClickedPostfix(ItemIconController __instance)
    {
        // Run only after the game's NGUI click handler has returned. Never retain
        // its transient ItemData wrapper and never rebuild our page from inside
        // this native callback. Only copy primitive identity and resolve later.
        if (!TrainerBehaviour.Visible || __instance?.itemData == null) return;
        try
        {
            var item = __instance.itemData;
            var owner = HeroDetailController.Instance?.nowShowHero ?? TrainerBehaviour.Player;
            TrainerBehaviour.QueueItemSelectionFromClick(
                owner?.heroID ?? int.MinValue,
                __instance.itemListID,
                item.itemID,
                (int)item.type,
                item.subType,
                item.Pointer);
        }
        catch (Exception ex) { Logger.LogWarning($"Item click capture skipped safely: {ex.Message}"); }
    }

    private static void GameUpdatePostfix() => TrainerBehaviour.Tick();
    private static void OnGuiPostfix() => TrainerBehaviour.Render();

    private static void CustomKungfuDatabasePrefix()
    {
        CityStateLock.Clear();
        TrainerBehaviour.RegisterCustomKungfuDefinitions();
    }
    private static void CustomKungfuDatabasePostfix() => TrainerBehaviour.RegisterCustomKungfuDefinitions();

    private static void CustomKungfuLevelCtorPrefix(ref int __0, out int __state)
    {
        __state = __0;
        if (TrainerBehaviour.TryResolveCustomKungfuBaseDonor(__0, out var donorId)) __0 = donorId;
    }

    private static void CustomKungfuLevelCtorPostfix(KungfuSkillLvData __instance, int __state)
    {
        TrainerBehaviour.RetargetConstructedCustomKungfuLevel(__instance, __state);
    }

    private static void BuildingButtonsPostfix(BuildingUIController __instance)
    {
        TrainerBehaviour.EnsureCustomKungfuBuildingButton(__instance);
    }

    private static bool BuildingButtonPrefix(BuildingButtonController __instance)
    {
        if (!TrainerBehaviour.IsCustomKungfuBuildingChoice(__instance?.areaBuildingChoice)) return true;
        TrainerBehaviour.OpenCustomKungfuEditor();
        return false;
    }

    private static void KungfuDataIconPostfix(KungfuSkillData __instance, ref string __result)
    {
        TrainerBehaviour.ReplaceCustomKungfuIcon(__instance?.skillID ?? int.MinValue, ref __result);
    }

    private static void KungfuLevelIconPostfix(KungfuSkillLvData __instance, ref string __result)
    {
        TrainerBehaviour.ReplaceCustomKungfuIcon(__instance?.skillID ?? int.MinValue, ref __result);
    }

    private static bool CustomKungfuDatabaseLookupPrefix(int skillID, ref KungfuSkillData __result)
    {
        if (!TrainerBehaviour.TryGetRuntimeCustomKungfuDefinition(skillID, out var definition)) return true;
        __result = definition!;
        return false;
    }

    private static bool CustomKungfuLevelDatabasePrefix(KungfuSkillLvData __instance, ref KungfuSkillData __result)
    {
        if (__instance == null || !TrainerBehaviour.TryGetRuntimeCustomKungfuDefinition(__instance.skillID, out var definition)) return true;
        __result = definition!;
        return false;
    }

    private static void CustomKungfuSkillIconUpdatePostfix(SkillIconController __instance)
        => TrainerBehaviour.RefreshCustomKungfuSkillIcon(__instance);

    private static bool CustomIconAtlasPrefix(string __0, string __1, ref Sprite __result)
    {
        if (__0 != "IconAtlas" || !TrainerBehaviour.TryResolveUploadedIcon(__1, out var sprite)) return true;
        __result = sprite!;
        return false;
    }

    private static bool CustomKungfuQuickRangePrefix(QuickDetail __instance, KungfuSkillLvData __0)
    {
        if (!TrainerBehaviour.ShouldSkipCustomKungfuRangePreview(__0)) return true;
        try
        {
            if (__instance?.skillRangeUI != null) __instance.skillRangeUI.SetActive(false);
        }
        catch (Exception ex) { Logger.LogDebug($"Large custom-kungfu range preview hide skipped safely: {ex.Message}"); }
        return false;
    }

    private static void BigMapEventInitPostfix(BigMapRandomEventController __instance) => TrainerBehaviour.RevealBigMapEventIcon(__instance);
    private static void BigMapEventsRecreatedPrefix() => TrainerBehaviour.ClearBigMapEventOverlays();
    private static void BigMapEventsRecreatedPostfix() => TrainerBehaviour.RefreshBigMapEventVisibility();
    private static void CustomKungfuBattleEndedPostfix() => TrainerBehaviour.NotifyCustomKungfuBattleEnded();

    private static bool CountHeroDataPrefix(HeroData hero)
        => !TrainerBehaviour.ShouldBypassCountHeroData(hero);

    private static void BreakThroughStartedPostfix(KungfuSkillLvData __0)
    {
        if (__0 != null) TrainerBehaviour.NotifyBreakThroughStarted(__0);
    }

    private static void BreakThroughChoicePrefix(BreakThroughChoiceController __0)
    {
        if (__0 != null) TrainerBehaviour.OverrideBreakThroughChoice(__0);
    }

    private static void GetMaxTagNumPostfix(HeroData __instance, ref int __result)
    {
        try
        {
            var startPlayer = StartGameSettingController.Instance?.Player;
            if (__instance != null && startPlayer != null && __instance.Pointer == startPlayer.Pointer)
            {
                __result = Math.Max(__result, SafeStartTalentSlotLimit(StartTalentSlotLimit.Value));
                return;
            }
            var player = TrainerBehaviour.Player;
            if (__instance != null && player != null && __instance.heroID == player.heroID)
                __result = Math.Max(__result, SafeTalentSlotLimit(PlayerTalentSlotLimit.Value));
        }
        catch (Exception ex) { Logger.LogDebug($"Talent slot limit skipped safely: {ex.Message}"); }
    }

    private static void HeroSelectedPostfix(HeroData targetHero)
    {
        if (targetHero != null) TrainerBehaviour.SelectHero(targetHero);
    }

    private static bool PlayerUnit(BattleUnit? unit) => unit != null && unit.playerControl;
    private static bool PlayerHero(HeroData? hero)
    {
        var player = TrainerBehaviour.Player;
        return hero != null && player != null && hero.heroID == player.heroID;
    }

    private static void BattleHpPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.InfiniteHp && PlayerUnit(__instance) && num < 0) num = 0; }
    private static void BattleManaPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.InfiniteMana && PlayerUnit(__instance) && num < 0) num = 0; }
    private static void BattlePowerPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.InfinitePower && PlayerUnit(__instance) && num < 0) num = 0; }
    private static void BattleExternalPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.ZeroExternal && PlayerUnit(__instance) && num > 0) num = 0; }
    private static void BattleInternalPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.ZeroInternal && PlayerUnit(__instance) && num > 0) num = 0; }
    private static void BattlePoisonPrefix(BattleUnit __instance, ref float num) { if (TrainerBehaviour.ZeroPoison && PlayerUnit(__instance) && num > 0) num = 0; }
    private static void SkillCooldownPrefix(BattleUnit targetHero, ref float deltaTime) { if (TrainerBehaviour.FastCooldown && PlayerUnit(targetHero)) deltaTime *= 1000f; }
    private static bool CustomKungfuCooldownPrefix(KungfuSkillLvData __instance, ref float __result)
        => !TrainerBehaviour.TryOverrideCustomKungfuCooldown(__instance, out __result);
    private static void SkillChargePostfix(HeroData __instance, ref float __result) { if (TrainerBehaviour.FastCharge && PlayerHero(__instance)) __result = Math.Max(__result, 1000f); }
    private static void TravelSpeedPostfix(HeroData __instance, ref float __result) { if (PlayerHero(__instance)) __result *= TrainerBehaviour.WorldSpeed; }
    private static void SkillExpPrefix(HeroData __instance, ref float num) { if (TrainerBehaviour.UnlimitedSkillExp && PlayerHero(__instance) && num > 0) num *= 1000f; }
    private static void LivingExpPrefix(HeroData __instance, ref float num) { if (TrainerBehaviour.UnlimitedSkillExp && PlayerHero(__instance) && num > 0) num *= 1000f; }
    private static void StartMenuUpdatePostfix(StartMenuController __instance)
    {
        if (!TrainerBehaviour.UnlimitedCreationPoints) return;
        __instance.leftAttriPoint = 999;
        __instance.leftFightSkillPoint = 999;
        __instance.leftLivingSkillPoint = 999;
        var startPlayer = StartGameSettingController.Instance?.Player;
        if (startPlayer != null) startPlayer.heroTagPoint = 999f;
    }

    private static void StartTalentMenuRefreshedPostfix(StartMenuController __instance)
    {
        TrainerBehaviour.RefreshStartTalentLimitText(__instance);
    }

    private static bool StartChooseTalentPrefix(StartMenuController __instance, int __0)
    {
        if (_insideStartTalentBypass) return true;
        try
        {
            var player = StartGameSettingController.Instance?.Player;
            Logger.LogInfo($"Start talent choice {__0}: selected={player?.heroTagData?.Count ?? 0}, limit={SafeStartTalentSlotLimit(StartTalentSlotLimit.Value)}, points={player?.heroTagPoint ?? 0f:0.##}.");

            if (player?.heroTagData == null || __instance == null) return true;
            var configuredLimit = SafeStartTalentSlotLimit(StartTalentSlotLimit.Value);
            var selectedCount = player.heroTagData.Count;
            var definition = GameDataController.Instance?.GetTagDataBase(__0);

            // Replacement talents are already explicitly allowed by the
            // original handler at the built-in cap, so keep that path native.
            if (definition?.replaceTag != null && definition.replaceTag.Count > 0) return true;
            if (selectedCount >= configuredLimit) return false;
            if (selectedCount < 5) return true;

            // StartChooseTagClicked reads heroTagData.Count directly and has a
            // literal cap of five. Give only this native call a four-entry view
            // of the same chosen tags, then merge its newly-created tag back
            // into the real list. All costs, effects and validation still run
            // through the original game method.
            var originalTags = player.heroTagData;
            var originalPointers = new HashSet<IntPtr>();
            for (var i = 0; i < originalTags.Count; i++)
            {
                var tag = originalTags[i];
                if (tag != null) originalPointers.Add(tag.Pointer);
            }

            var nativeView = new Il2CppSystem.Collections.Generic.List<HeroTagData>();
            for (var i = 0; i < Math.Min(4, originalTags.Count); i++)
                nativeView.Add(originalTags[i]);

            _insideStartTalentBypass = true;
            player.heroTagData = nativeView;
            try
            {
                __instance.StartChooseTagClicked(__0);
            }
            finally
            {
                player.heroTagData = originalTags;
                for (var i = 0; i < nativeView.Count; i++)
                {
                    var tag = nativeView[i];
                    if (tag != null && originalPointers.Add(tag.Pointer)) originalTags.Add(tag);
                }
                _insideStartTalentBypass = false;
                __instance.RefreshTagMenu();
            }
            return false;
        }
        catch (Exception ex)
        {
            _insideStartTalentBypass = false;
            Logger.LogWarning($"Extended start talent selection fell back to the original handler: {ex.Message}");
            return true;
        }
    }

    private static void StartTalentCountForLimitPostfix(HeroData __instance, ref int __result)
    {
        if (__instance == null) return;
        try
        {
            var startPlayer = StartGameSettingController.Instance?.Player;
            if (startPlayer == null || __instance.Pointer != startPlayer.Pointer) return;

            // RefreshTagMenu compares this value against its built-in limit of
            // five when deciding whether candidate buttons are interactable.
            var configuredLimit = SafeStartTalentSlotLimit(StartTalentSlotLimit.Value);
            __result = Math.Max(0, __result - (configuredLimit - 5));
        }
        catch (Exception ex) { Logger.LogDebug($"Start talent limit adjustment skipped safely: {ex.Message}"); }
    }

    private static void StartTalentAvailabilityPostfix(HeroTagData __instance, ref bool __result)
    {
        if (UnlockInheritedTalents.Value && __instance != null && __instance.tagID is >= 379 and <= 389)
            __result = true;
        if (UnlockAdvancedStartTalents.Value && IsAdvancedStartTalent(__instance))
            __result = true;
    }

    private static void StartTalentConditionPostfix(HeroTagDataBase __1, ref bool __result)
    {
        if (UnlockAdvancedStartTalents.Value && IsAdvancedStartTalent(__1))
            __result = true;
    }

    private static bool IsAdvancedStartTalent(HeroTagData? tag)
    {
        try { return tag != null && IsAdvancedStartTalent(tag.DataBase()); }
        catch { return false; }
    }

    private static bool IsAdvancedStartTalent(HeroTagDataBase? definition)
    {
        return definition != null && string.Equals(definition.category, "高级", StringComparison.Ordinal);
    }

    private static bool InheritedTalentPrefContainsPrefix(string __0, ref bool __result)
    {
        if (!TrainerBehaviour.ShouldSimulateInheritedTalentAchievement(__0)) return true;
        __result = true;
        return false;
    }

    private static bool InheritedTalentPrefStringPrefix(string __0, ref string __result)
    {
        if (!TrainerBehaviour.ShouldSimulateInheritedTalentAchievement(__0)) return true;
        __result = "true";
        return false;
    }

    private static bool InheritedTalentPrefIntPrefix(string __0, ref int __result)
    {
        if (!TrainerBehaviour.ShouldSimulateInheritedTalentAchievement(__0)) return true;
        __result = 1;
        return false;
    }

    private static void CraftFinalValuePostfix(CraftUIController __instance, ref float __result)
    {
        if (__instance == null) return;
        if (__instance.craftType == CraftType.Equipment && LockEquipmentCraftValue.Value) __result = SafeCraftValue(EquipmentCraftValue.Value);
        else if (__instance.craftType == CraftType.Med && LockMedicineCraftValue.Value) __result = SafeCraftValue(MedicineCraftValue.Value);
        else if (__instance.craftType == CraftType.Food && LockFoodCraftValue.Value) __result = SafeCraftValue(FoodCraftValue.Value);
    }

    internal static float SafeCraftValue(float value)
    {
        if (!float.IsFinite(value)) return 800f;
        return Math.Max(0f, value);
    }

    internal static int SafeCraftTime(int value) => Math.Clamp(value, 0, 3650);

    internal static int SafeTalentSlotLimit(int value) => Math.Clamp(value, 1, 100);

    internal static int SafeStartTalentSlotLimit(int value) => Math.Clamp(value, 1, 1000);

    internal static int SafeExpMultiplier(int value) => Math.Clamp(value, 1, 1000);

    internal static int SafeCreationMultiplier(int value) => Math.Clamp(value, 1, 100);

    internal static float SafeSpeedMultiplier(float value)
    {
        if (!float.IsFinite(value)) return 1f;
        return Math.Clamp(value, 1f, 16f);
    }

    internal static float SafeHorseMultiplier(float value)
    {
        if (!float.IsFinite(value)) return 1f;
        return Math.Clamp(value, 0.05f, 20f);
    }

    internal static int SafeSkillLevelThreshold(int value) => Math.Clamp(value, 1, 1000);

    internal static float SafeTalentPointMultiplier(float value)
    {
        if (!float.IsFinite(value)) return 2f;
        return Math.Clamp(value, 0f, 1000f);
    }

    private static int _lastCreationPointApplyFrame = -1;
    private static bool _startupIntroSkipped;

    private static void ExploreStaminaPrefix(ref int __0)
    {
        if (LockExploreStamina.Value && __0 < 0) __0 = 0;
    }

    private static void BookExpPrefix(HeroData __instance, ref float __0, KungfuSkillLvData __1, out SkillTalentState __state)
    {
        __state = CaptureSkillTalentState(__instance, __1);
        if (__0 <= 0f || !PlayerHero(__instance)) return;
        var multiplier = TrainerBehaviour.UnlimitedSkillExp ? 1000 : SafeExpMultiplier(BookExpMultiplier.Value);
        if (multiplier > 1) __0 *= multiplier;
    }

    private static void BattleSkillExpPrefix(HeroData __instance, ref float __0, KungfuSkillLvData __1, out SkillTalentState __state)
    {
        __state = CaptureSkillTalentState(__instance, __1);
        if (__0 <= 0f || !PlayerHero(__instance)) return;
        var multiplier = TrainerBehaviour.UnlimitedSkillExp ? 1000 : SafeExpMultiplier(BattleSkillExpMultiplier.Value);
        if (multiplier > 1) __0 *= multiplier;
    }

    private static SkillTalentState CaptureSkillTalentState(HeroData hero, KungfuSkillLvData skill)
    {
        var state = new SkillTalentState();
        if (!EnableSkillTalentGrant.Value || !PlayerHero(hero) || skill == null) return state;
        try
        {
            var tier = 1;
            var data = skill.DataBase();
            if (data != null) tier = Math.Max(1, data.rareLv);
            state.Eligible = true;
            state.Hero = hero;
            state.Skill = skill;
            state.LevelBefore = Math.Max(0, skill.lv);
            state.Tier = tier;
        }
        catch (Exception ex) { Logger.LogDebug($"Skill talent capture skipped safely: {ex.Message}"); }
        return state;
    }

    private static void SkillTalentPostfix(SkillTalentState __state)
    {
        if (__state == null || !__state.Eligible || __state.Hero == null || __state.Skill == null) return;
        try
        {
            var threshold = SafeSkillLevelThreshold(SkillTalentLevelThreshold.Value);
            var levelAfter = Math.Max(0, __state.Skill.lv);
            if (__state.LevelBefore >= threshold || levelAfter < threshold) return;
            var grant = Math.Max(1f, (float)Math.Round(__state.Tier * SafeTalentPointMultiplier(SkillTalentTierPointMultiplier.Value)));
            __state.Hero.ChangeTagPoint(grant, true);
            Logger.LogInfo($"Granted {grant:0.##} talent points when skill {__state.Skill.skillID} reached level {levelAfter}.");
        }
        catch (Exception ex) { Logger.LogWarning($"Skill talent grant skipped safely: {ex.Message}"); }
    }

    private static void CreationPointsPostfix(StartMenuController __instance)
    {
        if (__instance == null || _lastCreationPointApplyFrame == Time.frameCount) return;
        _lastCreationPointApplyFrame = Time.frameCount;
        if (TrainerBehaviour.UnlimitedCreationPoints)
        {
            __instance.leftAttriPoint = 999;
            __instance.leftFightSkillPoint = 999;
            __instance.leftLivingSkillPoint = 999;
            var startPlayer = StartGameSettingController.Instance?.Player;
            if (startPlayer != null) startPlayer.heroTagPoint = 999f;
            return;
        }

        var multiplier = SafeCreationMultiplier(CreationPointMultiplier.Value);
        if (multiplier <= 1) return;
        __instance.leftAttriPoint = SafePointPool(__instance.leftAttriPoint, multiplier);
        __instance.leftFightSkillPoint = SafePointPool(__instance.leftFightSkillPoint, multiplier);
        __instance.leftLivingSkillPoint = SafePointPool(__instance.leftLivingSkillPoint, multiplier);
    }

    private static int SafePointPool(int value, int multiplier)
    {
        var scaled = (long)Math.Max(0, value) * multiplier;
        return (int)Math.Min(999999L, scaled);
    }

    private static bool FreezeDatePrefix() => !FreezeDate.Value;

    private static void BattleSpeedPostfix()
    {
        try
        {
            var world = GameController.Instance?.worldData;
            var multiplier = SafeSpeedMultiplier(BattleSpeedMultiplier.Value);
            if (world == null || multiplier <= 1f) return;
            world.battleTimeScale = Math.Clamp(world.battleTimeScale * multiplier, 1f, 64f);
        }
        catch (Exception ex) { Logger.LogDebug($"Battle speed adjustment skipped safely: {ex.Message}"); }
    }

    private static void HorseSprintPostfix(HorseData __instance)
    {
        if (__instance == null) return;
        try
        {
            var duration = SafeHorseMultiplier(HorseSprintDurationMultiplier.Value);
            var cooldown = SafeHorseMultiplier(HorseSprintCooldownMultiplier.Value);
            if (__instance.sprintTimeLeft > 0f && Math.Abs(duration - 1f) > 0.001f)
                __instance.sprintTimeLeft = Math.Clamp(__instance.sprintTimeLeft * duration, 0f, 36000f);
            if (__instance.sprintTimeCd > 0f && Math.Abs(cooldown - 1f) > 0.001f)
                __instance.sprintTimeCd = Math.Clamp(__instance.sprintTimeCd * cooldown, 0f, 36000f);
        }
        catch (Exception ex) { Logger.LogDebug($"Horse sprint adjustment skipped safely: {ex.Message}"); }
    }

    private static void HorseStaminaPrefix(HorseData __instance, ref float __0)
    {
        if (__instance == null || !__instance.equiped) return;
        var multiplier = SafeHorseMultiplier(HorseStaminaMultiplier.Value);
        if (Math.Abs(multiplier - 1f) < 0.001f) return;
        __0 /= multiplier;
    }

    private static void HorseSpeedPostfix(HeroData __instance, ref float __result)
    {
        ApplyHorseSpeed(__instance, ResolveHorseSprintState(__instance), ref __result);
    }

    private static void HorseSpeedWithStatePostfix(HeroData __instance, bool __0, bool __1, ref float __result)
    {
        ApplyHorseSpeed(__instance, __1, ref __result);
    }

    private static void ApplyHorseSpeed(HeroData hero, bool sprinting, ref float speed)
    {
        if (!PlayerHero(hero) || speed <= 0f) return;
        var multiplier = SafeHorseMultiplier(HorseBaseSpeedMultiplier.Value);
        if (sprinting) multiplier *= SafeHorseMultiplier(HorseSprintSpeedMultiplier.Value);
        if (Math.Abs(multiplier - 1f) < 0.001f) return;
        speed = Math.Clamp(speed * multiplier, 0f, 100000f);
    }

    private static bool ResolveHorseSprintState(HeroData hero)
    {
        try
        {
            ResolveHorseMember();
            if (_horseProperty?.GetValue(hero) is HorseData propertyHorse) return propertyHorse.sprintTimeLeft > 0f;
            if (_horseField?.GetValue(hero) is HorseData fieldHorse) return fieldHorse.sprintTimeLeft > 0f;
        }
        catch { }
        return false;
    }

    private static bool _horseMemberResolved;
    private static PropertyInfo? _horseProperty;
    private static FieldInfo? _horseField;

    private static void ResolveHorseMember()
    {
        if (_horseMemberResolved) return;
        _horseMemberResolved = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = typeof(HeroData);
        _horseProperty = type.GetProperty("horse", flags) ?? type.GetProperty("Horse", flags);
        _horseField = type.GetField("horse", flags) ?? type.GetField("Horse", flags);
    }

    private static void EnterScenePostfix(EnterSceneController __instance)
    {
        if (_startupIntroSkipped || !SkipStartupIntro.Value || __instance == null || __instance.videoPlayFinished) return;
        try
        {
            VideoPlayer video = __instance.logoVideo;
            if (video == null) return;
            try { video.Stop(); } catch { }
            __instance.VideoPlayFinished(video);
            _startupIntroSkipped = true;
            Logger.LogInfo("Startup intro skipped safely.");
        }
        catch (Exception ex) { Logger.LogWarning($"Startup intro skip failed safely: {ex.Message}"); }
    }

    private static void CraftTimePostfix(ref int __result)
    {
        if (LockCraftTime.Value) __result = SafeCraftTime(CraftTimeValue.Value);
    }

    private static void CraftResultsPostfix(CraftUIController __instance)
    {
        try
        {
            var rarityMax = Math.Max(0, (GameDataController.Instance?.rareLvData?.Count ?? 6) - 1);
            var min = Math.Clamp(MinimumCraftRarity.Value, 0, rarityMax);
            if (min <= 0 || __instance?.craftResultList == null) return;
            for (var i = 0; i < __instance.craftResultList.Count; i++)
            {
                var item = __instance.craftResultList[i];
                if (item != null && item.rareLv < min) item.rareLv = min;
            }
        }
        catch (Exception ex) { Logger.LogWarning($"Craft result rarity adjustment skipped: {ex.Message}"); }
    }
}

public static partial class TrainerBehaviour
{
    private const string CustomKungfuChoiceMarker = "CodexCustomKungfu";
    private const int CustomKungfuIdMin = 900000;
    private const int CustomKungfuIdMax = 999999;

    private sealed class CustomKungfuStore
    {
        public int Schema = 5;
        public int NextId = CustomKungfuIdMin;
        public List<CustomKungfuRecord> Skills = new();
    }

    private sealed class CustomAttackRangeSpec
    {
        public int Type;
        public int Min = 1;
        public int Max = 1;
    }

    private sealed class CustomAttriNumSpec
    {
        public string Kind = "attri";
        public int Index;
        public float Value;
    }

    private sealed class CustomSpeAddSpec
    {
        public int TypeId;
        public float Value;
    }

    private sealed class CustomKungfuRecord
    {
        public int Id;
        public string Name = "自创功法";
        public string Description = "由藏经阁推演而成的自创功法。";
        public int Type;
        public int RareLv;
        public int ForceId = -1;
        public int TargetType;
        public float ManaCost;
        public float BaseDamage;
        public float ExpRatio = 1f;
        public float CooldownTime = -1f;
        public int BattleMaxUseTime;
        public int DamageOrder;
        public bool AutoMove;
        public int TrailId;
        public int BaseDonorId;
        public int DamageRatioDonorId;
        public int NeedsDonorId;
        public int UpgradeDonorId;
        public int EquipDonorId;
        public int UseDonorId;
        public int AttackRangeDonorId;
        public int DamageRangeDonorId;
        public int AttackPostureDonorId;
        public int DefensePostureDonorId;
        public int AnimationDonorId;
        public int WeaponDonorId;
        public int BulletDonorId;
        public int VisualEffectDonorId;
        public int IconDonorId;
        public string CustomIconFile = "";
        public bool UseCustomDamageRatio;
        public List<CustomAttriNumSpec> CustomDamageRatio = new();
        public bool UseCustomNeeds;
        public List<CustomAttriNumSpec> CustomNeeds = new();
        public bool UseCustomUpgrade;
        public List<CustomSpeAddSpec> CustomUpgrade = new();
        public bool UseCustomEquip;
        public List<CustomSpeAddSpec> CustomEquip = new();
        public bool UseCustomUse;
        public List<CustomSpeAddSpec> CustomUse = new();
        public bool UseCustomAttackRange;
        public List<CustomAttackRangeSpec> CustomAttackRanges = new();
        public bool UseCustomDamageRange;
        public int CustomDamageRangeType;
        public int CustomDamageRangeMin;
        public int CustomDamageRangeMax = 1;
        public bool UseCustomAttackPosture;
        public List<float> CustomAttackPosture = new();
        public bool UseCustomDefensePosture;
        public List<float> CustomDefensePosture = new();
    }

    private sealed class SkillChoiceSnapshot
    {
        public int Id;
        public string Name = "";
        public int Type;
        public int RareLv;
    }

    private sealed class CustomKungfuIconOverlayState
    {
        public int SkillId = int.MinValue;
        public GameObject? Overlay;
        public UISprite? Source;
        public Image? ImageSource;
        public Sprite? SourceSprite;
        public Sprite? AppliedSprite;
        public Color SourceColor;
        public string IconFile = "";
        public string SkillName = "";
        public readonly List<Text> NameTexts = new();
        public readonly List<string> OriginalText = new();
    }

    private sealed class CustomBasicChoice
    {
        public int Value;
        public string Label = "";
    }

    private sealed class CustomEffectChoice
    {
        public string Key = "";
        public string Label = "";
        public string Kind = "";
        public int Index;
        public int TypeId;
        public bool Percent;
    }

    public static bool InfiniteHp;
    public static bool InfiniteMana;
    public static bool InfinitePower;
    public static bool ZeroExternal;
    public static bool ZeroInternal;
    public static bool ZeroPoison;
    public static bool MaxMove;
    public static bool FastCharge;
    public static bool FastCooldown;
    public static bool NoEquipmentWeight;
    public static bool NoInventoryWeight;
    public static bool UnlimitedSkillExp;
    public static bool MaxFavor;
    public static bool UnlimitedCreationPoints;
    public static float WorldSpeed = 1f;

    public static HeroData? Player
    {
        get
        {
            try { return GameController.Instance?.worldData?.Player(); }
            catch { return null; }
        }
    }

    private static int _selectedHeroId = int.MinValue;
    private static string _heroSearchText = "";
    // Never retain an inventory ItemData wrapper across UI rebuilds.  The game
    // replaces those native objects when a bag closes or another hero opens.
    private static int _selectedItemOwnerId = int.MinValue;
    private static int _selectedItemIndex = -1;
    private static int _selectedItemId = int.MinValue;
    private static int _selectedItemType = int.MinValue;
    private static int _selectedItemSubType = int.MinValue;
    private static bool _visible;
    private static int _tab;
    private static bool RuntimeAssetDiscoveryEnabled => false;
    private static float _nextApply;
    private static float _nextHotkeyTime;
    private static int _lastTickFrame = -1;
    private static int _lastGuiFrame = -1;
    private static GameObject? _uiRoot;
    private static RectTransform? _uiContent;
    private static RectTransform? _activePageContent;
    private static RectTransform? _uiPanelRect;
    private static RectTransform? _uiTitleRect;
    private static Text? _uiStatus;
    private static Text? _catalogPreviewText;
    private static InputField? _catalogLevelInput;
    private static InputField? _catalogRarityInput;
    private static Font? _uiFont;
    private static Sprite? _paperSprite;
    private static Sprite? _buttonSprite;
    private static Sprite? _tabSprite;
    private static Sprite? _tabSelectedSprite;
    private static Sprite? _inputSprite;
    private static Sprite? _frameSprite;
    private static Sprite? _checkSprite;
    private static Sprite? _pageArrowSprite;
    private static Sprite? _inkSprite;
    private static Sprite? _addSprite;
    private static Sprite? _deleteSprite;
    private static Sprite? _confirmSprite;
    private static bool _nativeThemeLogged;
    private static int _nativeThemeScore;
    private static bool _pageDirty;
    private static bool _pageClearedForRebuild;
    private static int _pageBuildNotBeforeFrame;
    private static bool _dragging;
    private static NativePoint _lastDragMouse;
    private static bool _nativeMouseLatch;

    private static int _catalogPage;
    private static int _catalogCategory;
    private static string _catalogSearch = "";
    private static string _catalogQuantity = "1";
    private static string _catalogLevel = "1";
    private static string _catalogRarity = "0";
    private static CatalogEntry? _catalogSelected;
    private static readonly List<CatalogEntry> Catalog = new();

    private static int _resourceForceId = int.MinValue;
    private static bool _pendingItemSelection;
    private static int _pendingItemSelectionFrame;
    private static int _pendingItemOwnerId = int.MinValue;
    private static int _pendingItemIndex = -1;
    private static int _pendingItemId = int.MinValue;
    private static int _pendingItemType = int.MinValue;
    private static int _pendingItemSubType = int.MinValue;
    private static IntPtr _pendingItemPointer = IntPtr.Zero;
    private static IntPtr _selectedItemPointer = IntPtr.Zero;
    private static int _lastCapturedItemOwner = int.MinValue;
    private static int _lastCapturedItemId = int.MinValue;
    private static int _lastCapturedItemType = int.MinValue;
    private static int _lastCapturedItemSubType = int.MinValue;
    private static int _lastCapturedItemFrame = -1000;
    private static int _pendingItemRefreshOwnerId = int.MinValue;
    private static float _pendingItemRefreshAt;
    private static bool _pendingHorseStateRefresh;
    private static float _nextAffixWriteAt;
    private static int _breakChoicePage;
    private static int _breakChoiceSkillId = int.MinValue;
    private static int _selectedBreakChoiceId = int.MinValue;
    private static bool _breakChoiceArmed;
    private static readonly JsonSerializerOptions CustomKungfuJsonOptions = new() { IncludeFields = true, WriteIndented = true };
    private static readonly Dictionary<int, int> CustomKungfuIconDonors = new();
    private static readonly Dictionary<int, string> CustomKungfuIconFiles = new();
    private static readonly Dictionary<int, int> CustomKungfuBaseDonors = new();
    private static readonly Dictionary<string, Texture2D?> CustomKungfuIconTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite?> CustomKungfuIconSprites = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<IntPtr, CustomKungfuIconOverlayState> CustomKungfuIconOverlays = new();
    private static readonly Dictionary<IntPtr, GameObject> BigMapEventOverlays = new();

    private static float _nextBigMapEventRefreshAt;
    private static CustomKungfuStore _customKungfuStore = new();
    private static string _customKungfuStorePath = "";
    private static bool _customKungfuStoreLoaded;
    private static bool _customKungfuStoreLoadFailed;
    private static IntPtr _customKungfuRegisteredDatabasePointer;
    private static GameObject? _customUiRoot;
    private static RectTransform? _customUiContent;
    private static Text? _customUiStatus;
    private static Text? _customBasicSummaryText;
    private static string _customBasicChoiceField = "";
    private static int _customBasicChoicePage;
    private static bool _customVisible;
    private static int _customTab;
    private static int _customManagePage;
    private static int _customPickerPage;
    private static string _customPickerField = "";
    private static string _customPickerSearch = "";
    private static int _customPickerPreviewSkillId = int.MinValue;
    private static InputField? _customPickerSearchInput;
    private static Image? _customPickerPreviewIcon;
    private static Text? _customPickerPreviewTitle;
    private static Text? _customPickerPreviewDetail;
    private static Transform? _customAnimationPreviewParent;
    private static SkeletonGraphic? _customAnimationPreviewGraphic;
    private static IntPtr _customAnimationPreviewAssetPointer;
    private static int _customRangeSubTab;
    private static string _customEffectEditorField = "";
    private static int _customEffectCurrentPage;
    private static int _customEffectCatalogPage;
    private static int _customEffectCatalogGroup;
    private static string _customEffectSelectedKey = "";
    private static CustomKungfuRecord? _customDraft;
    private static string _customStatus = "在建筑内推演功法；保存前不会写入存档。";
    private static bool _customBattlePreviewActive;
    private static int _customBattlePreviewSkillId = int.MinValue;
    private static float _customBattlePreviewStartedAt;
    private static bool _customBattlePreviewEnding;
    private static float _customBattlePreviewReturnAt;
    private static HeroData? _customBattlePreviewPlayer;
    private static HeroData? _customBattlePreviewOpponent;
    private static KungfuSkillLvData? _customBattlePreviewSkill;
    private static bool _orphanCustomKungfuScrubbed;
    private static int _customCountBypassLogCount;
    private static readonly HashSet<IntPtr> LoggedCustomSkillIconPointers = new();
    private static bool _legacyCustomKungfuQuickDetailRemoved;
    private static readonly Dictionary<string, InputField> CustomInputs = new();
    private static readonly List<UiClick> CustomPermanentClicks = new();
    private static readonly List<UiClick> CustomPageClicks = new();
    private static readonly List<UiClick> CustomBuildClicks = new();
    private static readonly Dictionary<string, GameObject> CustomPageRoots = new();
    private static readonly Dictionary<string, List<UiClick>> CustomPageClickCache = new();
    private static string _customActivePageKey = "";

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName fileName);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int structSize;
        public IntPtr owner;
        public IntPtr instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string filter;
        [MarshalAs(UnmanagedType.LPWStr)] public string customFilter;
        public int maxCustomFilter;
        public int filterIndex;
        public IntPtr file;
        public int maxFile;
        public IntPtr fileTitle;
        public int maxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string initialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string defaultExtension;
        public IntPtr customData;
        public IntPtr hook;
        [MarshalAs(UnmanagedType.LPWStr)] public string templateName;
        public IntPtr reserved;
        public int reservedInt;
        public int flagsEx;
    }

    private sealed class CatalogEntry
    {
        public string Category = "";
        public string Name = "";
        public string Detail = "";
        public string IconName = "";
        public int RareLv;
        public int BaseLevel;
        public int ItemId;
        public int ItemTypeValue;
        public int ItemSubType;
        public int SkillId;
        public bool IsEquipment;
        public CatalogKind Kind;
    }

    private sealed class AffixRowSnapshot
    {
        public int Key;
        public float Amount;
        public string Description = "";
    }

    private sealed class EquipmentAffixSnapshot
    {
        public string ItemName = "";
        public readonly List<AffixRowSnapshot> Base = new();
        public readonly List<AffixRowSnapshot> Extra = new();
    }

    private enum CatalogKind
    {
        Database,
        Book,
        Material,
        Treasure
    }
    private sealed class UiClick
    {
        public RectTransform Rect = null!;
        public Action Action = null!;
    }
    private static readonly List<UiClick> TabClicks = new();
    private static readonly List<UiClick> PageClicks = new();
    private static readonly List<Button> TabButtons = new();
    private static readonly Dictionary<int, GameObject> PageRoots = new();
    private static readonly Dictionary<int, List<UiClick>> PageClickCache = new();
    private static readonly HashSet<int> DirtyPages = new();
    private static string _status = "已就绪：点击背包物品可选中物品，打开角色详情可选中角色。";
    private static int _fieldHeroId = int.MinValue;
    private static readonly Dictionary<string, string> Fields = new();
    private static string _itemName = "";
    private static string _itemValue = "0";
    private static string _itemLevel = "0";
    private static string _itemRare = "0";
    private static string _itemWeight = "0";
    private static string _itemPoison = "0";
    private static string _duplicateCount = "1";
    private static string _affixType = "maxHp";
    private static string _affixValue = "100";
    private static int _affixPresetGroup;
    private static bool _showItemAffixes;
    private static int _baseAffixPage;
    private static int _extraAffixPage;
    private static string _allAttr = "200";
    private static string _allFight = "200";
    private static string _allLiving = "200";
    private static string _forceMoney = "100000";
    private static readonly List<string> ResourceValues = new();
    private static readonly Dictionary<string, string> AffixTypeByChineseName = new();
    private static readonly Dictionary<int, bool> AffixPercentCache = new();
    private static IntPtr _affixPercentCacheDatabasePointer;

    private static readonly string[] HeroKeys =
    {
        "Money", "Fame", "BadFame", "Favor", "Govern", "ForceContrib",
        "HP", "MaxHP", "Mana", "MaxMana", "Power", "MaxPower",
        "External", "Internal", "Poison", "Loyal", "Evil", "Chaos", "Nature"
    };
    private static readonly string[] HeroLabels =
    {
        "金钱", "名气", "恶名", "好感度", "政府贡献", "势力贡献",
        "生命", "生命上限", "内力", "内力上限", "体力", "体力上限",
        "外伤", "内伤", "毒伤", "忠诚度", "邪恶值", "混乱值", "性格编号"
    };

    private static readonly string[] AffixPresets =
    {
        "maxHp", "maxMana", "maxPower", "damage", "armor", "armorRate", "speed", "acc",
        "evade", "critRate", "counter", "comboRate", "moveRange", "SkillFightExpRate",
        "SkillBookExpRate", "LivingSkillExpRate", "equipmentWeight"
    };
    private static readonly string[] AffixPresetLabels =
    {
        "生命上限", "内力上限", "体力上限", "伤害", "护甲", "护甲率", "速度", "命中",
        "闪避", "暴击率", "反击率", "连击率", "移动范围", "战斗技能经验",
        "读书经验", "生活技能经验", "装备重量"
    };
    private static readonly string[] AttributeAffixLabels = { "力道", "灵巧", "智力", "意志", "体质", "经脉" };
    private static readonly string[] FightSkillAffixLabels = { "内功", "轻功技能", "绝技", "拳掌", "剑法", "刀法", "长兵", "奇门", "射术" };
    private static readonly string[] LivingSkillAffixLabels = { "医术", "毒术", "学识", "口才", "采伐", "木植", "锻造", "炼药", "烹饪" };
    private static readonly string[] PosturePartAffixLabels = { "第一部位", "第二部位", "第三部位", "第四部位", "第五部位", "第六部位" };

    public static bool Visible => _visible;

    public static void InitializeCustomKungfuStorage()
    {
        if (_customKungfuStoreLoaded) return;
        _customKungfuStoreLoaded = true;
        _customKungfuStorePath = Path.Combine(Paths.ConfigPath, "LongYinTrainer.custom-kungfu.json");
        try
        {
            if (File.Exists(_customKungfuStorePath))
            {
                var loaded = JsonSerializer.Deserialize<CustomKungfuStore>(File.ReadAllText(_customKungfuStorePath, Encoding.UTF8), CustomKungfuJsonOptions);
                if (loaded != null) _customKungfuStore = loaded;
            }
            _customKungfuStore.Skills ??= new List<CustomKungfuRecord>();
            for (var i = 0; i < _customKungfuStore.Skills.Count; i++) NormalizeCustomKungfuRecord(_customKungfuStore.Skills[i]);
            _customKungfuStore.Schema = 5;
            _customKungfuStore.NextId = Math.Clamp(_customKungfuStore.NextId, CustomKungfuIdMin, CustomKungfuIdMax);
            LongYinTrainerPlugin.Logger.LogInfo($"Custom kungfu definitions loaded: {_customKungfuStore.Skills.Count}.");
        }
        catch (Exception ex)
        {
            _customKungfuStoreLoadFailed = true;
            _customStatus = "自创功法配置读取失败；为避免覆盖原文件，本次禁止保存：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogError($"Custom kungfu definition load failed safely: {ex}");
        }
    }

    private static void NormalizeCustomKungfuRecord(CustomKungfuRecord record)
    {
        if (!float.IsFinite(record.CooldownTime)) record.CooldownTime = -1f;
        record.CooldownTime = Math.Max(-1f, record.CooldownTime);
        record.CustomIconFile = NormalizeCustomKungfuIconFile(record.CustomIconFile);
        record.CustomDamageRatio = NormalizeCustomAttriSpecs(record.CustomDamageRatio);
        record.CustomNeeds = NormalizeCustomAttriSpecs(record.CustomNeeds);
        record.CustomUpgrade = NormalizeCustomSpeAddSpecs(record.CustomUpgrade);
        record.CustomEquip = NormalizeCustomSpeAddSpecs(record.CustomEquip);
        record.CustomUse = NormalizeCustomSpeAddSpecs(record.CustomUse);
        record.CustomAttackRanges ??= new List<CustomAttackRangeSpec>();
        if (record.CustomAttackRanges.Count == 0) record.CustomAttackRanges.Add(new CustomAttackRangeSpec());
        if (record.CustomAttackRanges.Count > 4) record.CustomAttackRanges.RemoveRange(4, record.CustomAttackRanges.Count - 4);
        for (var i = 0; i < record.CustomAttackRanges.Count; i++)
        {
            var range = record.CustomAttackRanges[i] ?? new CustomAttackRangeSpec();
            record.CustomAttackRanges[i] = range;
            range.Type = Math.Clamp(range.Type, 0, 4);
            range.Min = Math.Max(0, range.Min);
            range.Max = Math.Max(range.Min, range.Max);
        }
        record.CustomDamageRangeType = Math.Clamp(record.CustomDamageRangeType, 0, 8);
        record.CustomDamageRangeMin = Math.Max(0, record.CustomDamageRangeMin);
        record.CustomDamageRangeMax = Math.Max(record.CustomDamageRangeMin, record.CustomDamageRangeMax);
        record.CustomAttackPosture = NormalizePostureValues(record.CustomAttackPosture);
        record.CustomDefensePosture = NormalizePostureValues(record.CustomDefensePosture);
    }

    private static string NormalizeCustomKungfuIconFile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var fileName = Path.GetFileName(value.Trim());
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" ? fileName : "";
    }

    private static List<CustomAttriNumSpec> NormalizeCustomAttriSpecs(List<CustomAttriNumSpec>? values)
    {
        values ??= new List<CustomAttriNumSpec>();
        var normalized = new List<CustomAttriNumSpec>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value == null || !float.IsFinite(value.Value)) continue;
            var kind = value.Kind?.Trim().ToLowerInvariant() ?? "";
            var maxIndex = kind switch { "attri" => 5, "fight" => 8, "living" => 8, "hp" or "power" or "mana" or "charm" => 0, _ => -1 };
            if (maxIndex < 0) continue;
            var index = Math.Clamp(value.Index, 0, maxIndex);
            var key = kind + ":" + index.ToString(CultureInfo.InvariantCulture);
            var entry = new CustomAttriNumSpec { Kind = kind, Index = index, Value = value.Value };
            if (positions.TryGetValue(key, out var existing)) normalized[existing] = entry;
            else { positions[key] = normalized.Count; normalized.Add(entry); }
        }
        return normalized;
    }

    private static List<CustomSpeAddSpec> NormalizeCustomSpeAddSpecs(List<CustomSpeAddSpec>? values)
    {
        values ??= new List<CustomSpeAddSpec>();
        var normalized = new List<CustomSpeAddSpec>();
        var positions = new Dictionary<int, int>();
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value == null || !float.IsFinite(value.Value) || Enum.GetName(typeof(HeroSpeAddDataType), value.TypeId) == null) continue;
            var entry = new CustomSpeAddSpec { TypeId = value.TypeId, Value = value.Value };
            if (positions.TryGetValue(entry.TypeId, out var existing)) normalized[existing] = entry;
            else { positions[entry.TypeId] = normalized.Count; normalized.Add(entry); }
        }
        return normalized;
    }

    private static List<float> NormalizePostureValues(List<float>? values)
    {
        values ??= new List<float>();
        while (values.Count < 6) values.Add(0f);
        if (values.Count > 6) values.RemoveRange(6, values.Count - 6);
        for (var i = 0; i < values.Count; i++)
            if (!float.IsFinite(values[i])) values[i] = 0f;
        return values;
    }

    public static void RegisterCustomKungfuDefinitions()
    {
        InitializeCustomKungfuStorage();
        var database = GameDataController.Instance;
        if (database?.kungfuSkillDataBase == null || _customKungfuStoreLoadFailed) return;
        try
        {
            var pointer = database.Pointer;
            if (pointer != _affixPercentCacheDatabasePointer)
            {
                AffixPercentCache.Clear();
                _affixPercentCacheDatabasePointer = pointer;
            }
            var allPresent = pointer == _customKungfuRegisteredDatabasePointer;
            if (allPresent)
            {
                for (var i = 0; i < _customKungfuStore.Skills.Count; i++)
                {
                    if (!database.kungfuSkillDataBase.ContainsKey(_customKungfuStore.Skills[i].Id)) { allPresent = false; break; }
                }
            }
            if (allPresent) return;

            CustomKungfuIconDonors.Clear();
            CustomKungfuIconFiles.Clear();
            CustomKungfuBaseDonors.Clear();
            var registered = 0;
            for (var i = 0; i < _customKungfuStore.Skills.Count; i++)
            {
                var record = _customKungfuStore.Skills[i];
                var definition = InstallCustomKungfuDefinition(database, record);
                CustomKungfuIconDonors[record.Id] = record.IconDonorId;
                SetRuntimeCustomKungfuIconFile(record.Id, record.CustomIconFile);
                CustomKungfuBaseDonors[record.Id] = record.BaseDonorId;
                registered++;
            }
            PreloadCustomKungfuIcons();
            _customKungfuRegisteredDatabasePointer = pointer;
            if (registered > 0) LongYinTrainerPlugin.Logger.LogInfo($"Registered {registered} custom kungfu definitions in the runtime database.");
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu registration skipped safely: {ex.Message}");
        }
    }

    private static bool IsUnsavedCustomKungfuId(int skillId)
    {
        return skillId >= CustomKungfuIdMin && skillId <= CustomKungfuIdMax && FindSavedCustomKungfu(skillId) == null;
    }

    private static bool HeroHasUnsavedCustomKungfu(HeroData hero)
    {
        try
        {
            var active = hero.GetNowActiveSkill();
            if (active != null && IsUnsavedCustomKungfuId(active.skillID)) return true;
            var skills = hero.kungfuSkills;
            if (skills == null) return false;
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill != null && IsUnsavedCustomKungfuId(skill.skillID)) return true;
            }
        }
        catch { }
        return false;
    }

    public static bool ShouldBypassCountHeroData(HeroData? hero)
    {
        if (hero == null || !HeroHasUnsavedCustomKungfu(hero)) return false;
        if (_customCountBypassLogCount < 3)
        {
            _customCountBypassLogCount++;
            LongYinTrainerPlugin.Logger.LogInfo($"Bypassed CountHeroData for hero {hero.heroID} while an unsaved preview skill is attached.");
        }
        return true;
    }

    private static int RemoveUnsavedCustomKungfu(Il2CppSystem.Collections.Generic.List<KungfuSkillLvData>? skills)
    {
        if (skills == null) return 0;
        var removed = 0;
        for (var i = skills.Count - 1; i >= 0; i--)
        {
            var skill = skills[i];
            if (skill == null || !IsUnsavedCustomKungfuId(skill.skillID)) continue;
            skills.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    private static int RebaseSavedSkillIndex(int savedIndex, int removedIndex)
    {
        if (savedIndex < 0) return savedIndex;
        if (savedIndex == removedIndex) return -1;
        return savedIndex > removedIndex ? savedIndex - 1 : savedIndex;
    }

    private static int RemoveUnsavedCustomKungfuFromHero(HeroData hero)
    {
        var removed = RemoveUnsavedCustomKungfu(hero.attackSkills);
        var learned = hero.kungfuSkills;
        if (learned == null) return removed;
        for (var removedIndex = learned.Count - 1; removedIndex >= 0; removedIndex--)
        {
            var skill = learned[removedIndex];
            if (skill == null || !IsUnsavedCustomKungfuId(skill.skillID)) continue;
            learned.RemoveAt(removedIndex);
            removed++;

            // Every *SaveRecord field stores an index into kungfuSkills, not a
            // database skill ID. Removing a learned entry must therefore rebase
            // later indexes instead of comparing the record with skill.skillID.
            var attackIndexes = hero.attackSkillSaveRecord;
            if (attackIndexes != null)
                for (var i = 0; i < attackIndexes.Count; i++)
                    attackIndexes[i] = RebaseSavedSkillIndex(attackIndexes[i], removedIndex);
            hero.internalSkillSaveRecord = RebaseSavedSkillIndex(hero.internalSkillSaveRecord, removedIndex);
            hero.dodgeSkillSaveRecord = RebaseSavedSkillIndex(hero.dodgeSkillSaveRecord, removedIndex);
            hero.uniqueSkillSaveRecord = RebaseSavedSkillIndex(hero.uniqueSkillSaveRecord, removedIndex);
        }
        return removed;
    }

    private static KungfuSkillLvData? FirstUsableAttackSkill(HeroData hero)
    {
        var attackSkills = hero.attackSkills;
        if (attackSkills != null)
            for (var i = 0; i < attackSkills.Count; i++)
                if (attackSkills[i] != null && !IsUnsavedCustomKungfuId(attackSkills[i].skillID)) return attackSkills[i];
        var skills = hero.kungfuSkills;
        if (skills != null)
            for (var i = 0; i < skills.Count; i++)
                if (skills[i] != null && !IsUnsavedCustomKungfuId(skills[i].skillID)) return skills[i];
        return null;
    }

    private static void TryScrubOrphanCustomKungfuSkills()
    {
        if (_orphanCustomKungfuScrubbed || _customBattlePreviewActive) return;
        try
        {
            var heroes = GameController.Instance?.worldData?.Heros;
            if (heroes == null || heroes.Count == 0) return;
            InitializeCustomKungfuStorage();
            var removed = 0;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (hero == null || !HeroHasUnsavedCustomKungfu(hero)) continue;
                var active = hero.GetNowActiveSkill();
                var removedActive = active != null && IsUnsavedCustomKungfuId(active.skillID);
                removed += RemoveUnsavedCustomKungfuFromHero(hero);
                if (removedActive)
                {
                    var replacement = FirstUsableAttackSkill(hero);
                    if (replacement != null) hero.SetNowActiveSkill(replacement);
                }
            }
            _orphanCustomKungfuScrubbed = true;
            if (removed > 0)
                LongYinTrainerPlugin.Logger.LogWarning($"Removed {removed} orphaned unsaved custom-kungfu skill references left by an interrupted preview.");
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Deferred orphaned custom-kungfu cleanup safely: {ex.Message}");
        }
    }

    private static void RemoveLegacyCustomKungfuQuickDetailOverlay()
    {
        if (_legacyCustomKungfuQuickDetailRemoved) return;
        try
        {
            var quickDetail = QuickDetail.Instance;
            if (quickDetail == null) return;
            var legacy = quickDetail.skillDetail?.transform.Find("CodexCustomKungfuQuickDetailText");
            if (legacy != null)
            {
                UnityEngine.Object.Destroy(legacy.gameObject);
                LongYinTrainerPlugin.Logger.LogInfo("Removed legacy custom-kungfu quick-detail overlay; native skill UI is authoritative again.");
            }
            _legacyCustomKungfuQuickDetailRemoved = true;
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Legacy custom-kungfu quick-detail cleanup deferred safely: {ex.Message}");
        }
    }

    private static KungfuSkillData? BuildCustomKungfuDefinition(GameDataController database, CustomKungfuRecord record)
    {
        NormalizeCustomKungfuRecord(record);
        if (record.Id < CustomKungfuIdMin || record.Id > CustomKungfuIdMax) return null;
        var baseSkill = OriginalKungfu(database, record.BaseDonorId);
        if (baseSkill == null)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu {record.Id} skipped: base donor {record.BaseDonorId} is unavailable.");
            return null;
        }
        var result = baseSkill.Clone()?.TryCast<KungfuSkillData>();
        if (result == null) return null;

        KungfuSkillData Donor(int id) => OriginalKungfu(database, id) ?? baseSkill;
        var ratio = Donor(record.DamageRatioDonorId);
        var needs = Donor(record.NeedsDonorId);
        var upgrade = Donor(record.UpgradeDonorId);
        var equip = Donor(record.EquipDonorId);
        var use = Donor(record.UseDonorId);
        var attackRange = Donor(record.AttackRangeDonorId);
        var damageRange = Donor(record.DamageRangeDonorId);
        var attackPosture = Donor(record.AttackPostureDonorId);
        var defensePosture = Donor(record.DefensePostureDonorId);
        var animation = Donor(record.AnimationDonorId);
        var weapon = Donor(record.WeaponDonorId);
        var bullet = Donor(record.BulletDonorId);
        var visualEffect = Donor(record.VisualEffectDonorId);

        result.summonSkill = false;
        result.skillID = record.Id;
        result.name = string.IsNullOrWhiteSpace(record.Name) ? $"自创功法 {record.Id}" : record.Name.Trim();
        result.describe = record.Description?.Trim() ?? "";
        result.type = Math.Max(0, record.Type);
        result.rareLv = Math.Clamp(record.RareLv, 0, Math.Max(0, (database.rareLvData?.Count ?? 6) - 1));
        result.belongForceID = record.ForceId;
        result.targetType = (SkillTargetType)Math.Max(0, record.TargetType);
        result.manaCost = Math.Max(0f, record.ManaCost);
        result.baseDamage = Math.Max(0f, record.BaseDamage);
        result.expRatio = Math.Max(0.01f, record.ExpRatio);
        result.battleMaxUseTime = Math.Max(0, record.BattleMaxUseTime);
        result.skillDamageOrder = (SkillDamageOrder)Math.Max(0, record.DamageOrder);
        result.autoHeroMove = record.AutoMove;
        result.trailID = Math.Max(0, record.TrailId);
        result.addDamageRatio = record.UseCustomDamageRatio
            ? BuildCustomAttriNumData(record.CustomDamageRatio)
            : ratio.addDamageRatio?.Clone()?.TryCast<AttriNumData>();
        result.skillNeeds = record.UseCustomNeeds
            ? BuildCustomAttriNumData(record.CustomNeeds)
            : needs.skillNeeds?.Clone()?.TryCast<AttriNumData>();
        result.upgradeAddData = record.UseCustomUpgrade
            ? BuildCustomSpeAddData(record.CustomUpgrade)
            : upgrade.upgradeAddData?.Clone()?.TryCast<HeroSpeAddData>();
        result.equipAddData = record.UseCustomEquip
            ? BuildCustomSpeAddData(record.CustomEquip)
            : equip.equipAddData?.Clone()?.TryCast<HeroSpeAddData>();
        result.useAddData = record.UseCustomUse
            ? BuildCustomSpeAddData(record.CustomUse)
            : use.useAddData?.Clone()?.TryCast<HeroSpeAddData>();
        if (record.UseCustomAttackRange)
        {
            result.attackRangeData = new Il2CppSystem.Collections.Generic.List<SkillAttackRangeData>();
            var maxRange = 0;
            for (var i = 0; i < record.CustomAttackRanges.Count; i++)
            {
                var range = record.CustomAttackRanges[i];
                result.attackRangeData.Add(new SkillAttackRangeData(range.Type, range.Min, range.Max));
                maxRange = Math.Max(maxRange, range.Max);
            }
            result.maxAttackRange = maxRange;
        }
        else
        {
            result.attackRangeData = CloneSkillAttackRanges(attackRange.attackRangeData);
            result.maxAttackRange = attackRange.maxAttackRange;
        }
        result.damageRangeData = record.UseCustomDamageRange
            ? new SkillDamageRangeData(record.CustomDamageRangeType, record.CustomDamageRangeMin, record.CustomDamageRangeMax)
            : damageRange.damageRangeData?.Clone()?.TryCast<SkillDamageRangeData>();
        result.atkPartPosture = record.UseCustomAttackPosture
            ? BuildCustomPartPosture(record.CustomAttackPosture)
            : attackPosture.atkPartPosture?.Clone()?.TryCast<PartPostureData>();
        result.defPartPosture = record.UseCustomDefensePosture
            ? BuildCustomPartPosture(record.CustomDefensePosture)
            : defensePosture.defPartPosture?.Clone()?.TryCast<PartPostureData>();
        result.animationName = animation.animationName;
        result.weaponName = weapon.weaponName;
        result.skillBullet = bullet.skillBullet?.Clone()?.TryCast<SkillBulletData>();
        result.skillSpeEffects = CloneSkillVisualEffects(visualEffect.skillSpeEffects);
        result.hide = false;
        return result;
    }

    private static KungfuSkillData InstallCustomKungfuDefinition(GameDataController database, CustomKungfuRecord record)
    {
        var definitions = database.kungfuSkillDataBase
            ?? throw new InvalidOperationException("功法数据库尚未载入");
        var rebuilt = BuildCustomKungfuDefinition(database, record)
            ?? throw new InvalidOperationException($"无法生成自创功法定义 {record.Id}");
        if (definitions.ContainsKey(record.Id))
        {
            var installed = definitions[record.Id];
            if (installed != null && installed.Pointer != rebuilt.Pointer)
            {
                CopyKungfuDefinition(rebuilt, installed);
                return installed;
            }
        }
        definitions[record.Id] = rebuilt;
        return rebuilt;
    }

    private static void CopyKungfuDefinition(KungfuSkillData source, KungfuSkillData target)
    {
        // Battle HUD/controllers retain the KungfuSkillData pointer they first
        // resolved.  Mutate that object in place so every edited field reaches
        // those caches instead of replacing only the dictionary entry.
        target.summonSkill = source.summonSkill;
        target.skillID = source.skillID;
        target.belongForceID = source.belongForceID;
        target.targetType = source.targetType;
        target.name = source.name;
        target.describe = source.describe;
        target.type = source.type;
        target.rareLv = source.rareLv;
        target.manaCost = source.manaCost;
        target.baseDamage = source.baseDamage;
        target.expRatio = source.expRatio;
        target.addDamageRatio = source.addDamageRatio;
        target.skillNeeds = source.skillNeeds;
        target.upgradeAddData = source.upgradeAddData;
        target.equipAddData = source.equipAddData;
        target.useAddData = source.useAddData;
        target.attackRangeData = source.attackRangeData;
        target.damageRangeData = source.damageRangeData;
        target.summonID = source.summonID;
        target.battleMaxUseTime = source.battleMaxUseTime;
        target.atkPartPosture = source.atkPartPosture;
        target.defPartPosture = source.defPartPosture;
        target.weaponName = source.weaponName;
        target.animationName = source.animationName;
        target.skillBullet = source.skillBullet;
        target.skillSpeEffects = source.skillSpeEffects;
        target.skillDamageOrder = source.skillDamageOrder;
        target.autoHeroMove = source.autoHeroMove;
        target.trailID = source.trailID;
        target.maxAttackRange = source.maxAttackRange;
        target.hide = source.hide;
    }

    private static AttriNumData BuildCustomAttriNumData(List<CustomAttriNumSpec> specs)
    {
        var result = new AttriNumData
        {
            attri = new Il2CppSystem.Collections.Generic.List<float>(),
            fightSkill = new Il2CppSystem.Collections.Generic.List<float>(),
            livingSkill = new Il2CppSystem.Collections.Generic.List<float>(),
            Hp = 0f,
            Power = 0f,
            Mana = 0f,
            Charm = 0f
        };
        for (var i = 0; i < 6; i++) result.attri.Add(0f);
        for (var i = 0; i < 9; i++) result.fightSkill.Add(0f);
        for (var i = 0; i < 9; i++) result.livingSkill.Add(0f);
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            switch (spec.Kind)
            {
                case "attri": result.attri[spec.Index] = spec.Value; break;
                case "fight": result.fightSkill[spec.Index] = spec.Value; break;
                case "living": result.livingSkill[spec.Index] = spec.Value; break;
                case "hp": result.Hp = spec.Value; break;
                case "power": result.Power = spec.Value; break;
                case "mana": result.Mana = spec.Value; break;
                case "charm": result.Charm = spec.Value; break;
            }
        }
        return result;
    }

    private static HeroSpeAddData BuildCustomSpeAddData(List<CustomSpeAddSpec> specs)
    {
        var result = new HeroSpeAddData();
        result.Reset();
        for (var i = 0; i < specs.Count; i++) result.Set(specs[i].TypeId, specs[i].Value);
        return result;
    }

    private static PartPostureData BuildCustomPartPosture(List<float> values)
    {
        var result = new PartPostureData
        {
            partPosture = new Il2CppSystem.Collections.Generic.List<float>()
        };
        for (var i = 0; i < 6; i++) result.partPosture.Add(i < values.Count && float.IsFinite(values[i]) ? values[i] : 0f);
        return result;
    }

    private static Il2CppSystem.Collections.Generic.List<SkillAttackRangeData> CloneSkillAttackRanges(Il2CppSystem.Collections.Generic.List<SkillAttackRangeData>? source)
    {
        var result = new Il2CppSystem.Collections.Generic.List<SkillAttackRangeData>();
        if (source == null) return result;
        for (var i = 0; i < source.Count; i++)
        {
            var clone = source[i]?.Clone()?.TryCast<SkillAttackRangeData>();
            if (clone != null) result.Add(clone);
        }
        return result;
    }

    private static Il2CppSystem.Collections.Generic.List<SkillSpeEffectData> CloneSkillVisualEffects(Il2CppSystem.Collections.Generic.List<SkillSpeEffectData>? source)
    {
        var result = new Il2CppSystem.Collections.Generic.List<SkillSpeEffectData>();
        if (source == null) return result;
        for (var i = 0; i < source.Count; i++)
        {
            var clone = source[i]?.Clone()?.TryCast<SkillSpeEffectData>();
            if (clone != null) result.Add(clone);
        }
        return result;
    }

    private static KungfuSkillData? OriginalKungfu(GameDataController database, int skillId)
    {
        if (skillId < 0 || skillId >= CustomKungfuIdMin || database.kungfuSkillDataBase == null || !database.kungfuSkillDataBase.ContainsKey(skillId)) return null;
        return database.kungfuSkillDataBase[skillId];
    }

    public static void ReplaceCustomKungfuIcon(int skillId, ref string iconName)
    {
        if (!CustomKungfuIconDonors.TryGetValue(skillId, out var donorId)) return;
        try
        {
            if (CustomKungfuIconFiles.TryGetValue(skillId, out var file))
            {
                var sprite = GetCustomKungfuIconSprite(file);
                if (sprite != null)
                {
                    var key = "LongYinTrainer.Uploaded." + file;
                    UploadedIconKeys[key] = sprite;
                    iconName = key;
                    return;
                }
            }
            var database = GameDataController.Instance;
            var donor = database == null ? null : OriginalKungfu(database, donorId);
            if (donor != null) iconName = donor.GetSkillIcon();
        }
        catch (Exception ex) { LongYinTrainerPlugin.Logger.LogDebug($"Custom kungfu icon fallback skipped: {ex.Message}"); }
    }

    private static readonly Dictionary<string, Sprite> UploadedIconKeys = new(StringComparer.Ordinal);
    public static bool TryResolveUploadedIcon(string key, out Sprite? sprite)
    {
        sprite = null;
        if (key == null || !UploadedIconKeys.TryGetValue(key, out var found) || found == null) return false;
        sprite = found;
        return true;
    }

    private static string CustomKungfuIconDirectory()
        => Path.Combine(Paths.ConfigPath, "LongYinTrainer.CustomKungfuIcons");

    private static void SetRuntimeCustomKungfuIconFile(int skillId, string? iconFile)
    {
        var normalized = NormalizeCustomKungfuIconFile(iconFile);
        if (string.IsNullOrEmpty(normalized)) CustomKungfuIconFiles.Remove(skillId);
        else CustomKungfuIconFiles[skillId] = normalized;
    }

    private static readonly Dictionary<string, string> IconLoadErrors = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> IconRetryAt = new(StringComparer.OrdinalIgnoreCase);

    private static bool TryGetCustomKungfuIconTexture(int skillId, out Texture2D? texture, out string iconFile)
    {
        texture = null;
        iconFile = "";
        if (!CustomKungfuIconFiles.TryGetValue(skillId, out var configured)) return false;
        iconFile = NormalizeCustomKungfuIconFile(configured);
        texture = LoadCustomIconTexture(iconFile);
        return texture != null;
    }

    private static Texture2D? LoadCustomIconTexture(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        if (CustomKungfuIconTextures.TryGetValue(file, out var cached) && cached != null) return cached;
        if (IconRetryAt.TryGetValue(file, out var retry) && Time.realtimeSinceStartup < retry) return null;
        var path = Path.Combine(CustomKungfuIconDirectory(), file);
        var stage = "读取文件";
        Texture2D? texture = null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            stage = $"解码图片（{bytes.Length} 字节）";
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Il2CppStructArray<byte> nativeBytes = bytes;
            if (!ImageConversion.LoadImage(texture, nativeBytes, false))
                throw new InvalidOperationException("Unity LoadImage 返回 false");
            stage = $"缩放图片（{texture.width}×{texture.height}）";
            texture = DownscaleCustomKungfuIcon(texture);
            texture.name = "LongYinTrainerCustomKungfu_" + Path.GetFileNameWithoutExtension(file);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            UnityEngine.Object.DontDestroyOnLoad(texture);
            CustomKungfuIconTextures[file] = texture;
            IconLoadErrors.Remove(file); IconRetryAt.Remove(file);
            LongYinTrainerPlugin.Logger.LogInfo($"Custom icon texture loaded: path={path}, size={texture.width}x{texture.height}, bytes={bytes.Length}");
            return texture;
        }
        catch (Exception ex)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            CustomKungfuIconTextures.Remove(file);
            RecordIconLoadError(file, path, stage, ex);
            return null;
        }
    }

    private static void RecordIconLoadError(string file, string path, string stage, Exception ex)
    {
        IconLoadErrors[file] = $"{stage}失败：{ex.GetType().Name}：{ex.Message}";
        IconRetryAt[file] = Time.realtimeSinceStartup + 5f;
        LongYinTrainerPlugin.Logger.LogWarning($"Custom icon failure: stage={stage}, path={path}\n{ex}");
    }

    private static void PreloadCustomKungfuIcons()
    {
        var loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CustomKungfuIconFiles)
        {
            var file = NormalizeCustomKungfuIconFile(pair.Value);
            if (string.IsNullOrEmpty(file) || !loadedFiles.Add(file)) continue;
            TryGetCustomKungfuIconTexture(pair.Key, out _, out _);
            GetCustomKungfuIconSprite(file);
        }
    }

    private static Texture2D DownscaleCustomKungfuIcon(Texture2D source)
    {
        const int maxSide = 512;
        if (source.width <= maxSide && source.height <= maxSide) return source;
        var scale = Math.Min(maxSide / (float)source.width, maxSide / (float)source.height);
        var width = Math.Max(1, (int)Math.Round(source.width * scale));
        var height = Math.Max(1, (int)Math.Round(source.height * scale));
        var previous = RenderTexture.active;
        var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            var resized = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = source.name + "_" + width + "x" + height,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            resized.Apply(false, false);
            UnityEngine.Object.Destroy(source);
            return resized;
        }
        catch
        {
            return source;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Sprite? GetCustomKungfuIconSprite(string? iconFile)
    {
        var file = NormalizeCustomKungfuIconFile(iconFile);
        if (string.IsNullOrEmpty(file)) return null;
        if (CustomKungfuIconSprites.TryGetValue(file, out var cached) && cached != null && cached.texture != null) return cached;
        if (IconRetryAt.TryGetValue(file, out var retry) && Time.realtimeSinceStartup < retry) return null;
        var texture = LoadCustomIconTexture(file);
        if (texture == null) return null;
        try
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            if (sprite == null) throw new InvalidOperationException("Unity Sprite.Create 返回空对象");
            sprite.name = "LongYinTrainerCustomKungfuSprite_" + Path.GetFileNameWithoutExtension(file);
            UnityEngine.Object.DontDestroyOnLoad(sprite);
            CustomKungfuIconSprites[file] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            RecordIconLoadError(file, Path.Combine(CustomKungfuIconDirectory(), file), "创建预览 Sprite", ex);
            return null;
        }
    }

    private static void ImportCustomKungfuIcon()
    {
        SyncCustomKungfuDraftFromInputs();
        if (_customDraft == null) return;
        try
        {
            var selected = ShowCustomKungfuIconFilePicker();
            if (string.IsNullOrEmpty(selected)) { _customStatus = "已取消选择图片；也可以在下方粘贴图片完整路径导入。"; return; }
            ImportCustomKungfuIconPath(selected);
        }
        catch (Exception ex)
        {
            _customStatus = "文件选择窗口打开失败，请改用下方路径导入：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning("Custom icon file picker failed: " + ex);
        }
    }

    private static void ImportCustomKungfuIconPath(string selected)
    {
        SyncCustomKungfuDraftFromInputs();
        if (_customDraft == null) return;
        try
        {
            selected = selected.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(selected)) throw new InvalidOperationException("请先粘贴图片的完整路径");
            var info = new FileInfo(selected);
            if (!info.Exists) throw new InvalidOperationException("所选图片不存在");
            if (info.Length <= 0 || info.Length > 8 * 1024 * 1024) throw new InvalidOperationException("图片大小必须在 8 MB 以内");
            var extension = Path.GetExtension(info.Name).ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg")) throw new InvalidOperationException("只支持 PNG、JPG、JPEG 图片");
            var bytes = File.ReadAllBytes(info.FullName);
            var validationTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var validatedWidth = 0;
            var validatedHeight = 0;
            try
            {
                Il2CppStructArray<byte> nativeBytes = bytes;
                if (!ImageConversion.LoadImage(validationTexture, nativeBytes, false)) throw new InvalidOperationException("图片格式无法识别");
                if (validationTexture.width < 16 || validationTexture.height < 16) throw new InvalidOperationException("图片尺寸不能小于 16×16");
                if (validationTexture.width > 4096 || validationTexture.height > 4096) throw new InvalidOperationException("图片尺寸不能超过 4096×4096");
                validatedWidth = validationTexture.width;
                validatedHeight = validationTexture.height;
            }
            finally { UnityEngine.Object.Destroy(validationTexture); }

            Directory.CreateDirectory(CustomKungfuIconDirectory());
            var fileName = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + extension;
            var destination = Path.Combine(CustomKungfuIconDirectory(), fileName);
            if (!File.Exists(destination)) File.Copy(info.FullName, destination, false);
            IconRetryAt.Remove(fileName); // Explicit import always retries a previous failure.
            if (GetCustomKungfuIconSprite(fileName) == null) throw new InvalidOperationException(IconLoadErrors.TryGetValue(fileName, out var reason) ? reason : "无法生成预览；请查看 BepInEx/LogOutput.log");
            _customDraft.CustomIconFile = fileName;
            if (_customBattlePreviewActive) SetRuntimeCustomKungfuIconFile(_customBattlePreviewSkillId, fileName);
            _customStatus = $"已导入本地图标：{info.Name}（{validatedWidth}×{validatedHeight}）；保存功法后永久生效。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        }
        catch (Exception ex)
        {
            _customStatus = "本地图标导入失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu icon import failed: path={selected}, exception={ex}");
        }
    }

    private static string? ShowCustomKungfuIconFilePicker()
    {
        const int capacity = 32768;
        var fileBuffer = Marshal.AllocHGlobal(capacity * sizeof(char));
        var titleBuffer = Marshal.AllocHGlobal(1024 * sizeof(char));
        try
        {
            for (var i = 0; i < capacity; i++) Marshal.WriteInt16(fileBuffer, i * sizeof(char), 0);
            for (var i = 0; i < 1024; i++) Marshal.WriteInt16(titleBuffer, i * sizeof(char), 0);
            var dialog = new OpenFileName
            {
                structSize = Marshal.SizeOf<OpenFileName>(),
                owner = GetForegroundWindow(),
                filter = "图片文件 (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0PNG 图片 (*.png)\0*.png\0JPEG 图片 (*.jpg;*.jpeg)\0*.jpg;*.jpeg\0所有文件 (*.*)\0*.*\0\0",
                customFilter = null!,
                filterIndex = 1,
                file = fileBuffer,
                maxFile = capacity,
                fileTitle = titleBuffer,
                maxFileTitle = 1024,
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                title = "选择自创功法图标",
                flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008,
                defaultExtension = "png",
                templateName = null!
            };
            if (GetOpenFileName(ref dialog)) return Marshal.PtrToStringUni(fileBuffer);
            var error = CommDlgExtendedError();
            if (error != 0) throw new InvalidOperationException($"文件对话框错误 0x{error:X}");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(titleBuffer);
        }
    }

    private static void RestoreCustomKungfuDonorIcon()
    {
        if (_customDraft == null) return;
        _customDraft.CustomIconFile = "";
        if (_customBattlePreviewActive) SetRuntimeCustomKungfuIconFile(_customBattlePreviewSkillId, "");
        _customStatus = "已恢复使用来源功法图标；尚未保存。";
        InvalidateCustomKungfuPages();
        ShowCustomKungfuTab();
    }

    public static bool TryGetRuntimeCustomKungfuDefinition(int skillId, out KungfuSkillData? definition)
    {
        definition = null;
        if (skillId < CustomKungfuIdMin || skillId > CustomKungfuIdMax) return false;
        try
        {
            var definitions = GameDataController.Instance?.kungfuSkillDataBase;
            if (definitions == null || !definitions.ContainsKey(skillId)) return false;
            definition = definitions[skillId];
            return definition != null;
        }
        catch { return false; }
    }

    public static bool ShouldSkipCustomKungfuRangePreview(KungfuSkillLvData? skill)
    {
        if (skill == null || !TryGetRuntimeCustomKungfuDefinition(skill.skillID, out var definition) || definition == null) return false;
        // The native quick-detail grid grows roughly with the square of the
        // range.  Hiding only oversized previews leaves battle data untouched.
        return definition.maxAttackRange > 12;
    }

    public static bool ShouldSimulateInheritedTalentAchievement(string? key)
    {
        if (!LongYinTrainerPlugin.UnlockInheritedTalents.Value || string.IsNullOrEmpty(key) ||
            !key.StartsWith("AchFinished", StringComparison.Ordinal)) return false;
        return int.TryParse(key.AsSpan("AchFinished".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id is >= 28 and <= 38;
    }

    public static void RefreshStartTalentLimitText(StartMenuController? controller)
    {
        try
        {
            if (controller?.tagRoot == null) return;
            var limit = LongYinTrainerPlugin.SafeStartTalentSlotLimit(LongYinTrainerPlugin.StartTalentSlotLimit.Value);
            var labels = controller.tagRoot.GetComponentsInChildren<Text>(true);
            foreach (var label in labels)
            {
                if (label == null || string.IsNullOrEmpty(label.text) ||
                    !label.text.StartsWith("初始上限", StringComparison.Ordinal)) continue;
                var slash = label.text.LastIndexOf('/');
                if (slash < 0) continue;
                label.text = label.text[..(slash + 1)] + limit.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Start talent limit label refresh skipped safely: {ex.Message}");
        }
    }

    public static void RefreshCustomKungfuSkillIcon(SkillIconController? controller)
    {
        try
        {
            if (controller == null) return;
            var skill = controller.skillLvData;
            if (skill == null || !TryGetRuntimeCustomKungfuDefinition(skill.skillID, out var definition) || definition == null)
            {
                RemoveCustomKungfuIconOverlay(controller, false);
                return;
            }

            var pointer = controller.Pointer;
            var configuredIcon = CustomKungfuIconFiles.TryGetValue(skill.skillID, out var iconFile)
                ? NormalizeCustomKungfuIconFile(iconFile)
                : "";
            if (CustomKungfuIconOverlays.TryGetValue(pointer, out var cached) &&
                cached.SkillId == skill.skillID &&
                string.Equals(cached.IconFile, configuredIcon, StringComparison.OrdinalIgnoreCase) &&
                RefreshCachedCustomKungfuIcon(cached, definition.name))
                return;

            RemoveCustomKungfuIconOverlay(controller);
            RefreshUploadedCustomKungfuIcon(controller, skill.skillID, definition);
            if (!CustomKungfuIconOverlays.TryGetValue(pointer, out var state))
            {
                state = new CustomKungfuIconOverlayState { IconFile = configuredIcon };
                CustomKungfuIconOverlays[pointer] = state;
            }
            state.SkillId = skill.skillID;
            state.SkillName = definition.name;
            var texts = controller.GetComponentsInChildren<Text>(true);
            if (LoggedCustomSkillIconPointers.Add(controller.Pointer))
            {
                var hierarchy = new StringBuilder();
                for (var i = 0; i < texts.Length; i++)
                    hierarchy.Append(i == 0 ? "" : " | ").Append(texts[i].gameObject.name).Append('=').Append(texts[i].text);
                LongYinTrainerPlugin.Logger.LogInfo($"Custom battle skill icon bound to {skill.skillID}/{definition.name}; UI text nodes: {hierarchy}");
            }
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                var nodeName = text.gameObject.name ?? "";
                if (nodeName.Contains("name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "吐纳法", StringComparison.Ordinal) ||
                    string.Equals(text.text, "New Text", StringComparison.Ordinal))
                {
                    state.NameTexts.Add(text);
                    state.OriginalText.Add(text.text);
                    text.text = definition.name;
                }
            }
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Custom battle skill icon refresh skipped safely: {ex.Message}");
        }
    }

    private static bool RefreshCachedCustomKungfuIcon(CustomKungfuIconOverlayState state, string skillName)
    {
        try
        {
            if (state.ImageSource != null)
            {
                if (state.AppliedSprite == null) return false;
                if (state.ImageSource.sprite == null || state.ImageSource.sprite.Pointer != state.AppliedSprite.Pointer)
                    state.ImageSource.sprite = state.AppliedSprite;
                state.ImageSource.preserveAspect = true;
            }
            if (state.Overlay != null)
            {
                if (state.Source == null) return false;
                var texture = state.Overlay.GetComponent<UITexture>();
                if (texture == null || texture.mainTexture == null) return false;
                var hidden = state.Source.color;
                if (hidden.a != 0f) { hidden.a = 0f; state.Source.color = hidden; }
                if (!state.Overlay.activeSelf) state.Overlay.SetActive(true);
            }
            for (var i = 0; i < state.NameTexts.Count; i++)
            {
                var text = state.NameTexts[i];
                if (text == null) return false;
                if (!string.Equals(text.text, skillName, StringComparison.Ordinal)) text.text = skillName;
            }
            state.SkillName = skillName;
            return true;
        }
        catch { return false; }
    }

    private static void RefreshUploadedCustomKungfuIcon(SkillIconController controller, int skillId, KungfuSkillData definition)
    {
        if (!TryGetCustomKungfuIconTexture(skillId, out var texture, out var iconFile) || texture == null)
        {
            RemoveCustomKungfuIconOverlay(controller);
            return;
        }

        var pointer = controller.Pointer;
        var imageSource = FindCustomKungfuImageSource(controller, definition);
        var uploadedSprite = GetCustomKungfuIconSprite(iconFile);
        if (imageSource != null && uploadedSprite != null)
        {
            if (CustomKungfuIconOverlays.TryGetValue(pointer, out var imageState))
            {
                if (imageState.ImageSource != null && imageState.ImageSource.Pointer == imageSource.Pointer)
                {
                    imageSource.sprite = uploadedSprite;
                    imageSource.preserveAspect = true;
                    imageState.IconFile = iconFile;
                    return;
                }
                RemoveCustomKungfuIconOverlay(controller);
            }
            var unityImageState = new CustomKungfuIconOverlayState
            {
                ImageSource = imageSource,
                SourceSprite = imageSource.sprite,
                AppliedSprite = uploadedSprite,
                SourceColor = imageSource.color,
                IconFile = iconFile
            };
            imageSource.sprite = uploadedSprite;
            imageSource.preserveAspect = true;
            CustomKungfuIconOverlays[pointer] = unityImageState;
            LongYinTrainerPlugin.Logger.LogInfo($"Applied uploaded icon {iconFile} to custom kungfu {skillId} through Unity UI Image {imageSource.gameObject.name}.");
            return;
        }

        var source = FindCustomKungfuIconSource(controller, definition);
        if (source == null)
        {
            RemoveCustomKungfuIconOverlay(controller);
            return;
        }

        if (CustomKungfuIconOverlays.TryGetValue(pointer, out var existing))
        {
            if (existing.Overlay != null && existing.Source != null && existing.Source.Pointer == source.Pointer)
            {
                var uploaded = existing.Overlay.GetComponent<UITexture>();
                if (uploaded != null)
                {
                    uploaded.mainTexture = texture;
                    uploaded.width = source.width;
                    uploaded.height = source.height;
                    uploaded.depth = source.depth + 1;
                    existing.IconFile = iconFile;
                    var hidden = source.color;
                    hidden.a = 0f;
                    source.color = hidden;
                    existing.Overlay.SetActive(true);
                    return;
                }
            }
            RemoveCustomKungfuIconOverlay(controller);
        }

        var overlay = new GameObject("CodexCustomKungfuUploadedIcon", Il2CppType.Of<UITexture>());
        overlay.layer = source.gameObject.layer;
        overlay.transform.SetParent(source.transform.parent, false);
        overlay.transform.localPosition = source.transform.localPosition;
        overlay.transform.localRotation = source.transform.localRotation;
        overlay.transform.localScale = source.transform.localScale;
        var uiTexture = overlay.GetComponent<UITexture>();
        uiTexture.mainTexture = texture;
        uiTexture.width = source.width;
        uiTexture.height = source.height;
        uiTexture.depth = source.depth + 1;
        uiTexture.pivot = source.pivot;
        uiTexture.color = Color.white;
        uiTexture.fixedAspect = true;
        var state = new CustomKungfuIconOverlayState
        {
            Overlay = overlay,
            Source = source,
            SourceColor = source.color,
            IconFile = iconFile
        };
        var transparent = source.color;
        transparent.a = 0f;
        source.color = transparent;
        CustomKungfuIconOverlays[pointer] = state;
        LongYinTrainerPlugin.Logger.LogInfo($"Applied uploaded icon {iconFile} to custom kungfu {skillId}.");
    }

    private static Image? FindCustomKungfuImageSource(SkillIconController controller, KungfuSkillData definition)
    {
        var expected = "";
        try { expected = definition.GetSkillIcon() ?? ""; }
        catch { }
        var images = controller.GetComponentsInChildren<Image>(true);
        Image? fallback = null;
        var fallbackScore = 0f;
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image.sprite == null) continue;
            var spriteName = image.sprite.name ?? "";
            if (!string.IsNullOrEmpty(expected) && string.Equals(spriteName, expected, StringComparison.Ordinal)) return image;
            var nodeName = image.gameObject.name ?? "";
            if (nodeName.Contains("frame", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("background", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Equals("bg", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("rare", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("cd", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("level", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("outline", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("fill", StringComparison.OrdinalIgnoreCase)) continue;
            var rect = image.sprite.rect;
            var score = rect.width * rect.height + (nodeName.Contains("icon", StringComparison.OrdinalIgnoreCase) ? 10000000f : 0f);
            if (fallback == null || score > fallbackScore) { fallback = image; fallbackScore = score; }
        }
        return fallback;
    }

    private static UISprite? FindCustomKungfuIconSource(SkillIconController controller, KungfuSkillData definition)
    {
        var expected = "";
        try { expected = definition.GetSkillIcon() ?? ""; }
        catch { }
        var sprites = controller.GetComponentsInChildren<UISprite>(true);
        UISprite? fallback = null;
        for (var i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null) continue;
            var spriteName = sprite.spriteName ?? "";
            if (!string.IsNullOrEmpty(expected) && string.Equals(spriteName, expected, StringComparison.Ordinal)) return sprite;
            var nodeName = sprite.gameObject.name ?? "";
            if (nodeName.Contains("frame", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("rare", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("cd", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("level", StringComparison.OrdinalIgnoreCase) ||
                nodeName.Contains("key", StringComparison.OrdinalIgnoreCase)) continue;
            if (sprite.width < 24 || sprite.height < 24) continue;
            if (fallback == null || sprite.width * sprite.height > fallback.width * fallback.height) fallback = sprite;
        }
        return fallback;
    }

    private static void RemoveCustomKungfuIconOverlay(SkillIconController controller, bool restoreImageSprite = true)
    {
        if (!CustomKungfuIconOverlays.TryGetValue(controller.Pointer, out var state)) return;
        try
        {
            if (state.Source != null) state.Source.color = state.SourceColor;
            if (restoreImageSprite && state.ImageSource != null && state.SourceSprite != null) state.ImageSource.sprite = state.SourceSprite;
            if (restoreImageSprite)
                for (var i = 0; i < state.NameTexts.Count && i < state.OriginalText.Count; i++)
                    if (state.NameTexts[i] != null) state.NameTexts[i].text = state.OriginalText[i];
            if (state.Overlay != null) UnityEngine.Object.Destroy(state.Overlay);
        }
        catch { }
        CustomKungfuIconOverlays.Remove(controller.Pointer);
    }

    private static void RemoveAllCustomKungfuIconOverlays()
    {
        foreach (var state in CustomKungfuIconOverlays.Values)
        {
            try
            {
                if (state.Source != null) state.Source.color = state.SourceColor;
                if (state.ImageSource != null && state.SourceSprite != null) state.ImageSource.sprite = state.SourceSprite;
                for (var i = 0; i < state.NameTexts.Count && i < state.OriginalText.Count; i++)
                    if (state.NameTexts[i] != null) state.NameTexts[i].text = state.OriginalText[i];
                if (state.Overlay != null) UnityEngine.Object.Destroy(state.Overlay);
            }
            catch { }
        }
        CustomKungfuIconOverlays.Clear();
    }

    public static bool TryResolveCustomKungfuBaseDonor(int skillId, out int donorId)
    {
        donorId = skillId;
        if (skillId < CustomKungfuIdMin || skillId > CustomKungfuIdMax) return false;
        if (CustomKungfuBaseDonors.TryGetValue(skillId, out donorId) && donorId >= 0 && donorId < CustomKungfuIdMin) return true;
        InitializeCustomKungfuStorage();
        var saved = FindSavedCustomKungfu(skillId);
        if (saved == null || saved.BaseDonorId < 0 || saved.BaseDonorId >= CustomKungfuIdMin) return false;
        donorId = saved.BaseDonorId;
        CustomKungfuBaseDonors[skillId] = donorId;
        return true;
    }

    public static void RetargetConstructedCustomKungfuLevel(KungfuSkillLvData? skill, int requestedSkillId)
    {
        if (skill == null || !TryResolveCustomKungfuBaseDonor(requestedSkillId, out _)) return;
        skill.skillID = requestedSkillId;
        try
        {
            var definitions = GameDataController.Instance?.kungfuSkillDataBase;
            if (definitions == null || !definitions.ContainsKey(requestedSkillId)) return;
            var definition = definitions[requestedSkillId];
            if (definition == null) return;
            var level = Math.Max(1, skill.lv);
            skill.speEquipData = definition.GetSpeEquipData(level);
            skill.speUseData = definition.GetSpeUseData(level);
            skill.skillIconDirty = true;
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Custom kungfu level cache retarget skipped safely for {requestedSkillId}: {ex.Message}");
        }
    }

    public static bool TryOverrideCustomKungfuCooldown(KungfuSkillLvData? skill, out float cooldown)
    {
        cooldown = 0f;
        if (skill == null || skill.skillID < CustomKungfuIdMin || skill.skillID > CustomKungfuIdMax) return false;
        InitializeCustomKungfuStorage();
        var record = FindSavedCustomKungfu(skill.skillID);
        if (record == null || record.CooldownTime < 0f) return false;
        cooldown = record.CooldownTime;
        return true;
    }

    private static void RefreshLearnedCustomKungfuInstances(int skillId)
    {
        try
        {
            var heroes = GameController.Instance?.worldData?.Heros;
            if (heroes == null) return;
            var refreshed = 0;
            var seen = new HashSet<IntPtr>();
            for (var heroIndex = 0; heroIndex < heroes.Count; heroIndex++)
            {
                var hero = heroes[heroIndex];
                var skills = hero?.kungfuSkills;
                if (skills == null) continue;
                for (var skillIndex = 0; skillIndex < skills.Count; skillIndex++)
                {
                    var skill = skills[skillIndex];
                    if (skill == null || skill.skillID != skillId || !seen.Add(skill.Pointer)) continue;
                    RetargetConstructedCustomKungfuLevel(skill, skillId);
                    refreshed++;
                }
            }
            if (refreshed > 0)
                LongYinTrainerPlugin.Logger.LogInfo($"Refreshed {refreshed} learned instances after custom kungfu {skillId} was updated.");
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Learned custom kungfu refresh skipped safely for {skillId}: {ex.Message}");
        }
    }

    private static KungfuSkillLvData GrantCustomKungfuToPlayer(HeroData player, int skillId)
    {
        var existing = player.FindSkill(skillId);
        if (existing != null) return existing;
        var attackBarBefore = AttackBarAudit(player);
        var candidate = new KungfuSkillLvData(skillId)
        {
            equiped = false,
            isNew = true,
            belongHeroID = player.heroID
        };
        RetargetConstructedCustomKungfuLevel(candidate, skillId);
        var learned = player.GetSkill(candidate, true, true) ?? player.FindSkill(skillId) ?? candidate;
        RetargetConstructedCustomKungfuLevel(learned, skillId);
        // Learning is native. Do not equip, unequip, clear, rebuild, or restore
        // any skill bar here; the player manages equipment in the game's UI.
        var attackBarAfter = AttackBarAudit(player);
        if (!string.Equals(attackBarBefore, attackBarAfter, StringComparison.Ordinal))
            LongYinTrainerPlugin.Logger.LogWarning($"Native GetSkill changed the attack bar while learning custom kungfu {skillId}: before={attackBarBefore}; after={attackBarAfter}");
        else
            LongYinTrainerPlugin.Logger.LogInfo($"Learned custom kungfu {skillId} through native GetSkill; attack bar remained unchanged: {attackBarAfter}");
        return learned;
    }

    private static string AttackBarAudit(HeroData hero)
    {
        var result = new StringBuilder("runtime[");
        var runtime = hero.attackSkills;
        if (runtime != null)
            for (var i = 0; i < runtime.Count; i++)
            {
                if (i > 0) result.Append(',');
                result.Append(runtime[i]?.skillID ?? -1);
            }
        result.Append("]/indexes[");
        var indexes = hero.attackSkillSaveRecord;
        if (indexes != null)
            for (var i = 0; i < indexes.Count; i++)
            {
                if (i > 0) result.Append(',');
                result.Append(indexes[i]);
            }
        return result.Append(']').ToString();
    }

    public static bool IsCustomKungfuBuildingChoice(AreaBuildingChoice? choice)
    {
        return choice != null && string.Equals(choice.callFuc, CustomKungfuChoiceMarker, StringComparison.Ordinal);
    }

    public static void EnsureCustomKungfuBuildingButton(BuildingUIController controller)
    {
        if (controller == null) return;
        try
        {
            var building = controller.targetBuildingData ?? controller.buildingData;
            if (!IsKungfuEditingBuilding(building)) return;
            var grid = controller.buildingButtonGrid;
            if (grid == null) return;
            var buttons = grid.GetComponentsInChildren<BuildingButtonController>(true);
            for (var i = 0; i < buttons.Length; i++)
                if (IsCustomKungfuBuildingChoice(buttons[i]?.areaBuildingChoice)) return;

            var choice = new AreaBuildingChoice
            {
                text = "自创功法",
                describe = "拆分现有功法的参数、范围、姿态、动作、弹道与特效，在建筑内推演新的功法。",
                justNeedOneCondition = true,
                mainCondition = new Il2CppSystem.Collections.Generic.List<string>(),
                subCondition = new Il2CppSystem.Collections.Generic.List<string>(),
                callFuc = CustomKungfuChoiceMarker,
                callFucParam = ""
            };
            controller.CreateBuildingButton(choice);
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu building entry skipped safely: {ex.Message}");
        }
    }

    private static bool IsKungfuEditingBuilding(AreaBuildingData? building)
    {
        if (building == null) return false;
        if (building.buildingID == 1 || building.buildingID == 73) return true;
        var data = building.DataBase();
        if (data == null) return false;
        if ((data.name ?? "").Contains("藏经阁", StringComparison.Ordinal) || (data.name ?? "").Contains("书房", StringComparison.Ordinal)) return true;
        var choices = data.areaBuildingChoices;
        if (choices == null) return false;
        for (var i = 0; i < choices.Count; i++)
        {
            var function = choices[i]?.callFuc ?? "";
            if (function == "ShowBookWriter" || function == "ShowBookWriterSelf" || function == "CityHouseBookRoom" || function == "ManageBookStore") return true;
        }
        return false;
    }

    public static void SelectHero(HeroData hero)
    {
        _selectedHeroId = hero.heroID;
        // Inventory ItemData wrappers belong to the previously displayed
        // hero/list.  Never retain one across a hero switch.
        ClearSelectedItem();
        _fieldHeroId = int.MinValue;
        _status = $"已选择角色：{hero.heroName}（ID {hero.heroID}）";
        InvalidatePages(1, 2, 3, 5);
    }

    public static void RevealBigMapEventIcon(BigMapRandomEventController? controller) { }

    private static void ForceBigMapEventOverlayVisible(GameObject overlay)
    {
        if (overlay == null) return;
        overlay.SetActive(true);
        var controller = overlay.GetComponent<BigMapRandomEventController>();
        if (controller != null)
        {
            controller.enabled = false;
            controller.showed = true;
            controller.isNewIcon?.SetActive(false);
        }
        var tweeners = overlay.GetComponentsInChildren<UITweener>(true);
        for (var i = 0; i < tweeners.Length; i++) tweeners[i].enabled = false;
        var sprites = overlay.GetComponentsInChildren<UISprite>(true);
        for (var i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null || (controller?.isNewIcon != null && sprite.transform.IsChildOf(controller.isNewIcon.transform))) continue;
            sprite.gameObject.SetActive(true);
            sprite.enabled = true;
            sprite.alpha = 1f;
            var color = sprite.color;
            color.a = 1f;
            sprite.color = color;
        }
        var renderers = overlay.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].gameObject.SetActive(true);
            renderers[i].enabled = true;
        }
        controller?.isNewIcon?.SetActive(false);
    }

    public static void ClearBigMapEventOverlays()
    {
        ClearEventBadges();
        foreach (var pair in BigMapEventOverlays)
        {
            try { if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value); }
            catch { }
        }
        BigMapEventOverlays.Clear();
    }

    public static void RefreshBigMapEventVisibility() { }

    private static void MaterializeTemporaryBigMapEventOverlay(BigMapController map, EventData eventData)
    {
        var icons = map.bigmapRandomEventIcons;
        if (icons == null) return;
        var existingIcons = new HashSet<IntPtr>();
        for (var i = 0; i < icons.Count; i++)
            if (icons[i] != null) existingIcons.Add(icons[i].Pointer);
        var originalSeen = eventData.seen;
        var originalNoticed = eventData.noticed;
        var originalHovered = eventData.hovered;
        try
        {
            eventData.seen = true;
            eventData.noticed = true;
            map.CreateBigMapRandomEventIcon(eventData);
            for (var i = icons.Count - 1; i >= 0; i--)
            {
                var icon = icons[i];
                if (icon == null || existingIcons.Contains(icon.Pointer)) continue;
                var controller = icon.GetComponent<BigMapRandomEventController>();
                if (controller?.bigMapRandomEventData == null || controller.bigMapRandomEventData.Pointer != eventData.Pointer) continue;
                RevealBigMapEventIcon(controller);
                icons.RemoveAt(i);
                UnityEngine.Object.Destroy(icon);
            }
        }
        finally
        {
            eventData.seen = originalSeen;
            eventData.noticed = originalNoticed;
            eventData.hovered = originalHovered;
        }
    }

    private static void SetShowAllBigMapEvents(bool value)
    {
        LongYinTrainerPlugin.ShowAllBigMapEvents.Value = value;
        RestoreNativeVision();
        ClearBigMapEventOverlays();
        _nextBigMapEventRefreshAt = 0f;
        try
        {
            if (value) RefreshBigMapEventVisibility();
            else
            {
                ClearBigMapEventOverlays();
                BigMapController.Instance?.RecreatAllBigMapRandomEvent();
                _status = "已恢复原版视野；已经发现的事件不会撤销发现。";
            }
        }
        catch (Exception ex) { _status = "切换大地图事件显示失败：" + ex.Message; }
    }

    public static void QueueItemSelection(int ownerId, int listHint, int itemId, int itemType, int itemSubType)
    {
        if (!_visible) return;
        _pendingItemOwnerId = ownerId;
        _pendingItemIndex = listHint;
        _pendingItemId = itemId;
        _pendingItemType = itemType;
        _pendingItemSubType = itemSubType;
        _pendingItemSelectionFrame = Time.frameCount + 2;
        _pendingItemSelection = true;
        _status = "正在安全读取所选物品……";
    }

    public static void QueueItemSelectionFromClick(int ownerId, int listHint, int itemId, int itemType, int itemSubType, IntPtr itemPointer)
    {
        // NGUI may emit more than one OnClick notification while the user is
        // rapidly clicking or while a tooltip is changing. Ignore a duplicate
        // identity for a short window; a different item is accepted immediately.
        if (ownerId == _lastCapturedItemOwner && itemId == _lastCapturedItemId &&
            itemType == _lastCapturedItemType && itemSubType == _lastCapturedItemSubType &&
            Time.frameCount - _lastCapturedItemFrame <= 8)
            return;

        _lastCapturedItemOwner = ownerId;
        _lastCapturedItemId = itemId;
        _lastCapturedItemType = itemType;
        _lastCapturedItemSubType = itemSubType;
        _lastCapturedItemFrame = Time.frameCount;
        _pendingItemPointer = itemPointer;
        QueueItemSelection(ownerId, listHint, itemId, itemType, itemSubType);
        LongYinTrainerPlugin.Logger.LogInfo($"Inventory click identity queued safely: owner={ownerId}, list={listHint}, itemID={itemId}, type={itemType}, subType={itemSubType}.");
    }

    private static void CommitPendingItemSelection()
    {
        if (!_pendingItemSelection || Time.frameCount < _pendingItemSelectionFrame) return;
        _pendingItemSelection = false;
        _selectedItemOwnerId = _pendingItemOwnerId;
        _selectedItemIndex = _pendingItemIndex;
        _selectedItemId = _pendingItemId;
        _selectedItemType = _pendingItemType;
        _selectedItemSubType = _pendingItemSubType;
        _selectedItemPointer = _pendingItemPointer;
        var item = ResolveSelectedItem();
        if (item == null)
        {
            _status = "所选物品已被背包刷新，请再点击一次。";
            return;
        }
        try
        {
            _baseAffixPage = 0;
            _extraAffixPage = 0;
            _showItemAffixes = false;
            _itemName = item.name ?? "";
            _itemValue = item.value.ToString(CultureInfo.InvariantCulture);
            _itemLevel = item.itemLv.ToString(CultureInfo.InvariantCulture);
            _itemRare = item.rareLv.ToString(CultureInfo.InvariantCulture);
            _itemWeight = item.weight.ToString(CultureInfo.InvariantCulture);
            _itemPoison = item.poisonNum.ToString(CultureInfo.InvariantCulture);
            InvalidatePages(2, 3);
            _status = $"已选择物品：{item.Name(false)}（ID {item.itemID}）。点击上方“物品修改”页签载入。";
            LongYinTrainerPlugin.Logger.LogInfo($"Inventory selection committed safely: owner={_selectedItemOwnerId}, index={_selectedItemIndex}, itemID={_selectedItemId}, type={_selectedItemType}, subType={_selectedItemSubType}.");
        }
        catch
        {
            ClearSelectedItem();
            _status = "读取物品时背包已刷新，请再点击一次。";
        }
    }

    public static void SelectItem(ItemData item, int listHint)
    {
        var owner = HeroDetailController.Instance?.nowShowHero ?? Player;
        _selectedItemOwnerId = owner?.heroID ?? Player?.heroID ?? int.MinValue;
        _selectedItemIndex = -1;
        _selectedItemId = item.itemID;
        _selectedItemType = (int)item.type;
        _selectedItemSubType = item.subType;
        _selectedItemPointer = item.Pointer;
        _baseAffixPage = 0;
        _extraAffixPage = 0;
        try
        {
            var list = owner?.itemListData?.allItem;
            if (list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var candidate = list[i];
                    if (candidate != null && candidate.Pointer == item.Pointer) { _selectedItemIndex = i; break; }
                }
            }
        }
        catch { _selectedItemIndex = listHint; }
        if (_selectedItemIndex < 0) _selectedItemIndex = listHint;
        _itemName = item.name ?? "";
        _itemValue = item.value.ToString(CultureInfo.InvariantCulture);
        _itemLevel = item.itemLv.ToString(CultureInfo.InvariantCulture);
        _itemRare = item.rareLv.ToString(CultureInfo.InvariantCulture);
        _itemWeight = item.weight.ToString(CultureInfo.InvariantCulture);
        _itemPoison = item.poisonNum.ToString(CultureInfo.InvariantCulture);
        _status = $"已选择物品：{item.Name(false)}（ID {item.itemID}）";
        InvalidatePages(2, 3);
    }

    private static void ClearSelectedItem()
    {
        _pendingItemSelection = false;
        _selectedItemOwnerId = int.MinValue;
        _selectedItemIndex = -1;
        _selectedItemId = int.MinValue;
        _selectedItemType = int.MinValue;
        _selectedItemSubType = int.MinValue;
        _pendingItemPointer = IntPtr.Zero;
        _selectedItemPointer = IntPtr.Zero;
    }

    private static bool SelectedItemMatches(ItemData? item)
    {
        return item != null && item.itemID == _selectedItemId &&
               (int)item.type == _selectedItemType && item.subType == _selectedItemSubType;
    }

    private static ItemData? ResolveSelectedItem()
    {
        if (_selectedItemOwnerId == int.MinValue) return null;
        try
        {
            var world = GameController.Instance?.worldData;
            var owner = world?.GetHero(_selectedItemOwnerId);
            var list = owner?.itemListData?.allItem;
            if (list == null) return null;
            if (_selectedItemPointer != IntPtr.Zero)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var exact = list[i];
                    if (exact == null || exact.Pointer != _selectedItemPointer) continue;
                    _selectedItemIndex = i;
                    return exact;
                }
                // The bag rebuilt and the clicked instance no longer exists.
                // Do not silently edit another stack/equipment sharing the same
                // database ID; require a fresh click instead.
                ClearSelectedItem();
                return null;
            }
            if (_selectedItemIndex >= 0 && _selectedItemIndex < list.Count)
            {
                var indexed = list[_selectedItemIndex];
                if (SelectedItemMatches(indexed)) return indexed;
            }
            for (var i = 0; i < list.Count; i++)
            {
                var candidate = list[i];
                if (!SelectedItemMatches(candidate)) continue;
                _selectedItemIndex = i;
                return candidate;
            }
        }
        catch { }
        ClearSelectedItem();
        return null;
    }

    public static void Tick()
    {
        if (_lastTickFrame == Time.frameCount) return;
        _lastTickFrame = Time.frameCount;
        UpdateEventBadges();
        EnforceForceLock();
        TickCityState();
        AnimatePageLoading();
        RemoveLegacyCustomKungfuQuickDetailOverlay();
        if (LongYinTrainerPlugin.ShowAllBigMapEvents.Value && Time.realtimeSinceStartup >= _nextBigMapEventRefreshAt)
        {
            _nextBigMapEventRefreshAt = Time.realtimeSinceStartup + 1f;
            RefreshBigMapEventVisibility();
        }
        if (_customBattlePreviewEnding && Time.realtimeSinceStartup >= _customBattlePreviewReturnAt)
        {
            _customBattlePreviewEnding = false;
            _customVisible = true;
            EnsureCustomKungfuUi();
            InvalidateCustomKungfuPages();
            if (_customUiRoot != null) _customUiRoot.SetActive(true);
            ShowCustomKungfuTab();
            LongYinTrainerPlugin.Logger.LogInfo($"Finished isolated custom kungfu battle preview after {Math.Max(0f, Time.realtimeSinceStartup - _customBattlePreviewStartedAt):0.0}s.");
        }
        // Unity's legacy input can be observed more than once because this method is
        // reached through more than one game update path.  A native key edge is stable.
        if (_customVisible)
        {
            EnsureCustomKungfuUi();
            if (Input.GetKeyDown(KeyCode.Escape) || (_customBattlePreviewActive && Input.GetKeyDown(LongYinTrainerPlugin.ToggleKey.Value)))
                CloseCustomKungfuEditor();
            else
            {
                if (_customUiStatus != null) _customUiStatus.text = _customStatus;
                HandleCustomKungfuClicks();
            }
        }
        var hotkeyPressed = !_customVisible && IsGameForeground() && Input.GetKeyDown(LongYinTrainerPlugin.ToggleKey.Value);
        if (hotkeyPressed && _customBattlePreviewActive && Time.realtimeSinceStartup >= _nextHotkeyTime)
        {
            _nextHotkeyTime = Time.realtimeSinceStartup + 0.75f;
            OpenCustomKungfuEditor();
            _customStatus = "试演场编辑器已打开。修改后按 H 或 Esc 应用草稿并返回完整网格。";
            if (_customUiStatus != null) _customUiStatus.text = _customStatus;
            hotkeyPressed = false;
        }
        if (hotkeyPressed && Time.realtimeSinceStartup >= _nextHotkeyTime)
        {
            _nextHotkeyTime = Time.realtimeSinceStartup + 1.0f;
            _visible = !_visible;
            _status = _visible ? "修改器面板已打开。" : "修改器面板已关闭。";
            if (!_visible) ClearSelectedItem();
            LongYinTrainerPlugin.Logger.LogInfo($"Trainer panel {(_visible ? "opened" : "closed")} by {LongYinTrainerPlugin.ToggleKey.Value}.");
            if (_visible)
            {
                EnsureNativeUi();
            }
            if (_uiRoot != null) _uiRoot.SetActive(_visible);
        }
        if (_visible)
        {
            EnsureNativeUi();
            CommitPendingItemSelection();
            ProcessPageRebuild();
            FlushPendingItemRefresh();
            if (_uiStatus != null) _uiStatus.text = _status;
            UpdateCatalogPreview();
            HandleNativeDrag();
            HandleNativeClicks();
        }
        if (Time.realtimeSinceStartup < _nextApply) return;
        _nextApply = Time.realtimeSinceStartup + 0.1f;
        ApplyContinuous();
    }

    private static bool IsGameForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
    }

    private static void ApplyContinuous()
    {
        if (!InfiniteHp && !InfiniteMana && !InfinitePower && !ZeroExternal && !ZeroInternal &&
            !ZeroPoison && !NoInventoryWeight && !NoEquipmentWeight && !MaxFavor && !MaxMove)
            return;
        try
        {
            var player = Player;
            if (player == null) return;
            if (InfiniteHp) player.hp = player.maxhp;
            if (InfiniteMana) player.mana = player.maxMana;
            if (InfinitePower) player.power = player.maxPower;
            if (ZeroExternal) player.externalInjury = 0;
            if (ZeroInternal) player.internalInjury = 0;
            if (ZeroPoison) player.poisonInjury = 0;
            if (NoInventoryWeight && player.itemListData != null) player.itemListData.weight = 0;
            if (NoEquipmentWeight && player.nowEquipment != null) player.nowEquipment.equipmentWeight = 0;
            if (MaxFavor)
            {
                var selected = TargetHero();
                if (selected != null && selected.heroID != player.heroID) selected.favor = 100f;
            }

            var battle = BattleController.Instance;
            if (battle?.activeUnitList == null) return;
            for (var i = 0; i < battle.activeUnitList.Count; i++)
            {
                var unit = battle.activeUnitList[i];
                if (unit == null || !unit.playerControl || unit.heroData == null) continue;
                if (InfiniteHp) unit.heroData.hp = unit.heroData.maxhp;
                if (InfiniteMana) unit.heroData.mana = unit.heroData.maxMana;
                if (InfinitePower) unit.heroData.power = unit.heroData.maxPower;
                if (ZeroExternal) unit.heroData.externalInjury = 0;
                if (ZeroInternal) unit.heroData.internalInjury = 0;
                if (ZeroPoison) unit.heroData.poisonInjury = 0;
                if (MaxMove) unit.battleMove = 999f;
            }
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Continuous apply skipped: {ex.Message}");
        }
    }

    public static void OpenCustomKungfuEditor()
    {
        InitializeCustomKungfuStorage();
        RegisterCustomKungfuDefinitions();
        if (_customKungfuStoreLoadFailed)
        {
            LongYinTrainerPlugin.Logger.LogWarning("Custom kungfu editor opened read-only because its definition file could not be loaded.");
        }
        if (_customDraft == null) _customDraft = NewCustomKungfuDraft();
        _visible = false;
        if (_uiRoot != null) _uiRoot.SetActive(false);
        _customVisible = true;
        EnsureCustomKungfuUi();
        if (!_customVisible || _customUiRoot == null) return;
        if (_customUiRoot != null) _customUiRoot.SetActive(true);
        ShowCustomKungfuTab();
        LongYinTrainerPlugin.Logger.LogInfo(_customBattlePreviewActive
            ? "Custom kungfu editor opened over the isolated battle preview."
            : "Custom kungfu editor opened from a building interaction.");
    }

    private static void CloseCustomKungfuEditor()
    {
        SyncCustomKungfuDraftFromInputs();
        _customVisible = false;
        if (_customUiRoot != null) _customUiRoot.SetActive(false);
        LongYinTrainerPlugin.Logger.LogInfo(_customBattlePreviewActive
            ? "Custom kungfu editor hidden; isolated battle preview remains active."
            : "Custom kungfu editor closed; unsaved draft retained in memory.");
    }

    private static void EnsureCustomKungfuUi()
    {
        if (_customUiRoot != null) return;
        try
        {
            var gameText = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<Text>()) as Text;
            _uiFont ??= gameText?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            _customUiRoot = new GameObject("CodexCustomKungfuCanvas", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Canvas>(), Il2CppType.Of<CanvasScaler>(), Il2CppType.Of<GraphicRaycaster>());
            UnityEngine.Object.DontDestroyOnLoad(_customUiRoot);
            var canvas = _customUiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32761;
            var scaler = _customUiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var shade = UiObject("Shade", _customUiRoot.transform, new Vector2(1920, 1080), Vector2.zero);
            var shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.72f);

            var panel = UiObject("CustomKungfuPanel", _customUiRoot.transform, new Vector2(1320, 900), new Vector2(300, 70));
            var panelImage = panel.AddComponent<Image>();
            ApplySprite(panelImage, _paperSprite, true, new Color(1f, 1f, 1f, 0.99f), new Color(0.93f, 0.90f, 0.80f, 0.995f));
            var frame = UiObject("Frame", panel.transform, new Vector2(1320, 900), Vector2.zero);
            var frameImage = frame.AddComponent<Image>();
            ApplySprite(frameImage, _frameSprite, true, new Color(0.18f, 0.15f, 0.11f, 0.88f), new Color(0.18f, 0.15f, 0.11f, 0.25f));
            frameImage.raycastTarget = false;

            var title = UiObject("Title", panel.transform, new Vector2(1280, 44), new Vector2(20, 14));
            var titleImage = title.AddComponent<Image>();
            ApplySprite(titleImage, _tabSprite ?? _buttonSprite, true, Color.white, new Color(0.075f, 0.065f, 0.055f, 0.99f));
            var titleText = AddText(title.transform, "藏经阁 · 自创功法", new Vector2(18, 0), new Vector2(780, 44), 23, TextAnchor.MiddleLeft);
            titleText.color = new Color(0.94f, 0.69f, 0.18f, 1f);
            AddCustomKungfuButton(title.transform, "关闭  Esc", new Vector2(1090, 4), new Vector2(170, 36), CloseCustomKungfuEditor, true);

            var tabs = new[] { "基本参数", "修炼／效果", "范围／姿态", "动作／特效", "保存／管理" };
            for (var i = 0; i < tabs.Length; i++)
            {
                var captured = i;
                AddCustomKungfuButton(panel.transform, tabs[i], new Vector2(20 + i * 258, 68), new Vector2(248, 42), () =>
                {
                    SyncCustomKungfuDraftFromInputs();
                    UpdateCustomKungfuBasicSummary();
                    _customBasicChoiceField = "";
                    _customPickerField = "";
                    _customTab = captured;
                    ShowCustomKungfuTab();
                }, true);
            }

            var content = UiObject("Content", panel.transform, new Vector2(1280, 700), new Vector2(20, 120));
            _customUiContent = content.GetComponent<RectTransform>();
            var contentImage = content.AddComponent<Image>();
            contentImage.color = new Color(1f, 1f, 1f, 0.25f);

            var status = UiObject("Status", panel.transform, new Vector2(1280, 48), new Vector2(20, 834));
            var statusImage = status.AddComponent<Image>();
            ApplySprite(statusImage, _tabSprite ?? _buttonSprite, true, Color.white, new Color(0.13f, 0.11f, 0.085f, 0.97f));
            _customUiStatus = AddText(status.transform, _customStatus, new Vector2(14, 0), new Vector2(1250, 48), 16, TextAnchor.MiddleLeft);
            _customUiStatus.color = new Color(0.93f, 0.78f, 0.40f, 1f);

            ShowCustomKungfuTab();
            _customUiRoot.SetActive(_customVisible);
        }
        catch (Exception ex)
        {
            _customVisible = false; // Retry only on an explicit reopen, never every frame.
            _customStatus = "自创功法界面创建失败：" + ex.Message;
            _status = _customStatus;
            InvalidateCustomKungfuPages();
            CustomPermanentClicks.Clear();
            CustomBuildClicks.Clear();
            _customUiContent = null;
            _customUiStatus = null;
            LongYinTrainerPlugin.Logger.LogError($"Custom kungfu UI creation failed: {ex}");
            if (_customUiRoot != null) UnityEngine.Object.Destroy(_customUiRoot);
            _customUiRoot = null;
        }
    }

    private static Button AddCustomKungfuButton(Transform parent, string label, Vector2 pos, Vector2 size, Action action, bool permanent = false)
    {
        var go = UiObject("CustomButton_" + label, parent, size, pos);
        var image = go.AddComponent<Image>();
        ApplySprite(image, _buttonSprite, true, Color.white, new Color(0.15f, 0.13f, 0.105f, 0.98f));
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var text = AddText(go.transform, label, Vector2.zero, size, 17, TextAnchor.MiddleCenter);
        text.color = new Color(0.96f, 0.92f, 0.80f, 1f);
        (permanent ? CustomPermanentClicks : CustomBuildClicks).Add(new UiClick { Rect = go.GetComponent<RectTransform>(), Action = action });
        return button;
    }

    private static void HandleCustomKungfuClicks()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        for (var i = CustomPageClicks.Count - 1; i >= 0; i--)
        {
            var click = CustomPageClicks[i];
            if (click.Rect != null && click.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(click.Rect, Input.mousePosition, null))
            {
                click.Action();
                return;
            }
        }
        for (var i = CustomPermanentClicks.Count - 1; i >= 0; i--)
        {
            var click = CustomPermanentClicks[i];
            if (click.Rect != null && click.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(click.Rect, Input.mousePosition, null))
            {
                click.Action();
                return;
            }
        }
    }

    private static void ShowCustomKungfuTab()
    {
        if (_customUiContent == null || _customDraft == null) return;
        var key = "tab:" + _customTab;
        ActivateCustomKungfuPage(key, _customTab switch
        {
            0 => BuildCustomKungfuBasicPage,
            1 => BuildCustomKungfuEffectPage,
            2 => BuildCustomKungfuRangePage,
            3 => BuildCustomKungfuVisualPage,
            _ => BuildCustomKungfuManagePage
        });
    }

    private static void ActivateCustomKungfuPage(string key, Action<Transform> build)
    {
        if (_customUiContent == null) return;
        foreach (var pair in CustomPageRoots) pair.Value.SetActive(false);
        CustomPageClicks.Clear();
        if (!CustomPageRoots.TryGetValue(key, out var root))
        {
            root = UiObject("CustomPage_" + key.Replace(':', '_'), _customUiContent.transform, _customUiContent.sizeDelta, Vector2.zero);
            CustomBuildClicks.Clear();
            build(root.transform);
            CustomPageRoots[key] = root;
            CustomPageClickCache[key] = new List<UiClick>(CustomBuildClicks);
        }
        root.SetActive(true);
        _customActivePageKey = key;
        if (CustomPageClickCache.TryGetValue(key, out var clicks)) CustomPageClicks.AddRange(clicks);
    }

    private static void InvalidateCustomKungfuPages()
    {
        foreach (var pair in CustomPageRoots)
            if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
        CustomPageRoots.Clear();
        CustomPageClickCache.Clear();
        CustomPageClicks.Clear();
        CustomInputs.Clear();
        _customBasicSummaryText = null;
        _customActivePageKey = "";
    }

    private static CustomKungfuRecord NewCustomKungfuDraft()
    {
        RegisterCustomKungfuDefinitions();
        var database = GameDataController.Instance;
        KungfuSkillData? donor = null;
        var donorId = 0;
        if (database?.kungfuSkillDataBase != null)
        {
            foreach (var pair in database.kungfuSkillDataBase)
            {
                if (pair.Key >= CustomKungfuIdMin || pair.Value == null || pair.Value.hide) continue;
                if (donor != null && pair.Key >= donorId) continue;
                donor = pair.Value;
                donorId = pair.Key;
            }
        }
        var draft = new CustomKungfuRecord
        {
            Name = "自创功法",
            Description = "由藏经阁推演而成的自创功法。",
            Type = donor?.type ?? 0,
            RareLv = donor?.rareLv ?? 0,
            ForceId = donor?.belongForceID ?? -1,
            TargetType = donor == null ? 0 : (int)donor.targetType,
            ManaCost = donor?.manaCost ?? 0f,
            BaseDamage = donor?.baseDamage ?? 0f,
            ExpRatio = donor?.expRatio ?? 1f,
            BattleMaxUseTime = donor?.battleMaxUseTime ?? 0,
            DamageOrder = donor == null ? 0 : (int)donor.skillDamageOrder,
            AutoMove = donor?.autoHeroMove ?? false,
            TrailId = donor?.trailID ?? 0,
            BaseDonorId = donorId,
            DamageRatioDonorId = donorId,
            NeedsDonorId = donorId,
            UpgradeDonorId = donorId,
            EquipDonorId = donorId,
            UseDonorId = donorId,
            AttackRangeDonorId = donorId,
            DamageRangeDonorId = donorId,
            AttackPostureDonorId = donorId,
            DefensePostureDonorId = donorId,
            AnimationDonorId = donorId,
            WeaponDonorId = donorId,
            BulletDonorId = donorId,
            VisualEffectDonorId = donorId,
            IconDonorId = donorId
        };
        SeedCustomRangeAndPosture(draft, donor);
        return draft;
    }

    private static CustomKungfuRecord CloneCustomKungfuRecord(CustomKungfuRecord source)
    {
        var json = JsonSerializer.Serialize(source, CustomKungfuJsonOptions);
        var clone = JsonSerializer.Deserialize<CustomKungfuRecord>(json, CustomKungfuJsonOptions) ?? throw new InvalidOperationException("复制自创功法定义失败");
        NormalizeCustomKungfuRecord(clone);
        return clone;
    }

    private static void SeedCustomRangeAndPosture(CustomKungfuRecord draft, KungfuSkillData? donor)
    {
        draft.CustomAttackRanges = new List<CustomAttackRangeSpec>();
        if (donor?.attackRangeData != null)
        {
            for (var i = 0; i < donor.attackRangeData.Count && draft.CustomAttackRanges.Count < 4; i++)
            {
                var range = donor.attackRangeData[i];
                if (range == null) continue;
                draft.CustomAttackRanges.Add(new CustomAttackRangeSpec { Type = (int)range.rangeType, Min = range.minRange, Max = range.maxRange });
            }
        }
        if (draft.CustomAttackRanges.Count == 0) draft.CustomAttackRanges.Add(new CustomAttackRangeSpec());
        var damage = donor?.damageRangeData;
        if (damage != null)
        {
            draft.CustomDamageRangeType = (int)damage.rangeType;
            draft.CustomDamageRangeMin = damage.minRange;
            draft.CustomDamageRangeMax = damage.maxRange;
        }
        draft.CustomAttackPosture = PostureValues(donor?.atkPartPosture);
        draft.CustomDefensePosture = PostureValues(donor?.defPartPosture);
        NormalizeCustomKungfuRecord(draft);
    }

    private static List<float> PostureValues(PartPostureData? posture)
    {
        var result = new List<float>();
        var source = posture?.partPosture;
        for (var i = 0; i < 6; i++) result.Add(source != null && i < source.Count && float.IsFinite(source[i]) ? source[i] : 0f);
        return result;
    }

    private static void AddCustomKungfuField(Transform parent, string label, string key, string value, float x, float y, float inputWidth = 260f)
    {
        AddText(parent, label, new Vector2(x, y), new Vector2(130, 36), 17);
        CustomInputs[key] = AddInput(parent, value, new Vector2(x + 130, y), new Vector2(inputWidth, 36));
    }

    private static void AddCustomKungfuChoiceField(Transform parent, string label, string field, string value, float x, float y)
    {
        AddText(parent, label, new Vector2(x, y), new Vector2(130, 36), 17);
        AddCustomKungfuButton(parent, value + "　▼", new Vector2(x + 130, y), new Vector2(260, 36), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            _customBasicChoiceField = string.Equals(_customBasicChoiceField, field, StringComparison.Ordinal) ? "" : field;
            _customBasicChoicePage = 0;
            InvalidateCustomKungfuPages();
            _customTab = 0;
            ShowCustomKungfuTab();
        });
    }

    private static string CustomBasicChoiceLabel(string field, int value)
    {
        return field switch
        {
            "type" => $"{value} · {CustomKungfuTypeName(value)}",
            "rare" => $"{value} · {RarityName(value)}",
            "force" => $"{value} · {CustomKungfuForceName(value)}",
            "target" => $"{value} · {SkillTargetTypeName(value)}",
            "order" => $"{value} · {SkillDamageOrderName(value)}",
            "trail" => value == 0 ? "0 · 无／默认" : $"{value} · 拖尾样式 {value}",
            _ => value.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string CustomKungfuForceName(int forceId)
    {
        if (forceId < 0) return "通用／无势力";
        try
        {
            var force = GameController.Instance?.worldData?.GetForce(forceId);
            if (force != null) return force.GetForceName(true);
        }
        catch { }
        return "未知势力";
    }

    private static List<CustomBasicChoice> GetCustomKungfuBasicChoices(string field, int current)
    {
        var choices = new List<CustomBasicChoice>();
        var seen = new HashSet<int>();
        void Add(int value, string label)
        {
            if (seen.Add(value)) choices.Add(new CustomBasicChoice { Value = value, Label = label });
        }

        if (field == "type")
        {
            var names = GlobalData.FightSkillName;
            if (names != null)
                for (var value = 0; value < names.Count; value++)
                    Add(value, CustomBasicChoiceLabel("type", value));
        }
        else if (field == "rare")
        {
            var count = Math.Max(6, GameDataController.Instance?.rareLvData?.Count ?? 0);
            for (var value = 0; value < count; value++) Add(value, $"{value} · {RarityName(value)}");
        }
        else if (field == "force")
        {
            Add(-1, "-1 · 通用／无势力");

            try
            {
                var forces = GameController.Instance?.worldData?.Forces;
                if (forces != null)
                    for (var i = 0; i < forces.Count; i++)
                    {
                        var force = forces[i];
                        if (force != null) Add(force.forceID, CustomBasicChoiceLabel("force", force.forceID));
                    }
            }
            catch { }
        }
        else if (field == "target")
        {
            for (var value = 0; value <= 5; value++) Add(value, $"{value} · {SkillTargetTypeName(value)}");
        }
        else if (field == "order")
        {
            for (var value = 0; value <= 2; value++) Add(value, $"{value} · {SkillDamageOrderName(value)}");
        }
        else if (field == "trail")
        {
            Add(0, "0 · 无／默认");
            try
            {
                var skills = GameDataController.Instance?.kungfuSkillDataBase;
                if (skills != null)
                    foreach (var pair in skills)
                    {
                        var skill = pair.Value;
                        if (pair.Key >= CustomKungfuIdMin || skill == null || skill.hide || seen.Contains(skill.trailID)) continue;
                        Add(skill.trailID, $"{skill.trailID} · 示例：{skill.name}");
                    }
            }
            catch { }
        }
        Add(current, CustomBasicChoiceLabel(field, current));
        choices.Sort((left, right) => left.Value.CompareTo(right.Value));
        return choices;
    }

    private static void BuildCustomKungfuBasicChoicePopup(Transform parent)
    {
        var draft = _customDraft!;
        var current = _customBasicChoiceField switch
        {
            "type" => draft.Type, "rare" => draft.RareLv, "force" => draft.ForceId,
            "target" => draft.TargetType, "order" => draft.DamageOrder, "trail" => draft.TrailId, _ => 0
        };
        var choices = GetCustomKungfuBasicChoices(_customBasicChoiceField, current);
        const int pageSize = 6;
        var pageCount = Math.Max(1, (choices.Count + pageSize - 1) / pageSize);
        _customBasicChoicePage = Math.Clamp(_customBasicChoicePage, 0, pageCount - 1);
        var position = _customBasicChoiceField switch
        {
            "rare" => new Vector2(560, 300), "force" => new Vector2(870, 300),
            "target" => new Vector2(140, 350), "order" => new Vector2(870, 400),
            "trail" => new Vector2(140, 410), _ => new Vector2(140, 300)
        };
        var popup = UiObject("CustomBasicChoicePopup", parent, new Vector2(370, 286), position);
        var image = popup.AddComponent<Image>();
        ApplySprite(image, _paperSprite, true, Color.white, new Color(0.11f, 0.095f, 0.075f, 0.995f));
        var start = _customBasicChoicePage * pageSize;
        for (var i = 0; i < pageSize && start + i < choices.Count; i++)
        {
            var choice = choices[start + i];
            var selected = choice.Value == current ? "● " : "";
            AddCustomKungfuButton(popup.transform, selected + choice.Label, new Vector2(8, 8 + i * 39), new Vector2(354, 35), () =>
            {
                SetCustomKungfuBasicChoice(_customBasicChoiceField, choice.Value);
                _customBasicChoiceField = "";
                InvalidateCustomKungfuPages();
                _customTab = 0;
                ShowCustomKungfuTab();
            });
        }
        if (pageCount > 1)
        {
            AddCustomKungfuButton(popup.transform, "◀", new Vector2(8, 246), new Vector2(82, 32), () =>
            {
                _customBasicChoicePage = Math.Max(0, _customBasicChoicePage - 1);
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
            AddText(popup.transform, $"{_customBasicChoicePage + 1}/{pageCount}", new Vector2(100, 246), new Vector2(160, 32), 14, TextAnchor.MiddleCenter);
            AddCustomKungfuButton(popup.transform, "▶", new Vector2(280, 246), new Vector2(82, 32), () =>
            {
                _customBasicChoicePage = Math.Min(pageCount - 1, _customBasicChoicePage + 1);
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
    }

    private static void SetCustomKungfuBasicChoice(string field, int value)
    {
        var draft = _customDraft;
        if (draft == null) return;
        switch (field)
        {
            case "type": draft.Type = Math.Max(0, value); break;
            case "rare": draft.RareLv = Math.Max(0, value); break;
            case "force": draft.ForceId = value; break;
            case "target": draft.TargetType = Math.Max(0, value); break;
            case "order": draft.DamageOrder = Math.Max(0, value); break;
            case "trail": draft.TrailId = Math.Max(0, value); break;
        }
        _customStatus = $"已选择：{CustomBasicChoiceLabel(field, value)}；尚未保存。";
    }

    private static void BuildCustomKungfuBasicPage(Transform parent)
    {
        var draft = _customDraft!;
        AddText(parent, draft.Id == 0 ? "新功法（尚未分配 ID）" : $"正在编辑：{draft.Name}（ID {draft.Id}）", new Vector2(10, 8), new Vector2(900, 38), 22);
        AddText(parent, "只有点击“保存”后才会写入模组配置；游戏存档只保存自创功法 ID。", new Vector2(10, 48), new Vector2(1180, 32), 15);
        AddCustomKungfuField(parent, "名称", "name", draft.Name, 10, 92, 430);
        AddText(parent, "描述", new Vector2(10, 142), new Vector2(130, 36), 17);
        var description = AddInput(parent, draft.Description, new Vector2(140, 142), new Vector2(1030, 92));
        description.lineType = InputField.LineType.MultiLineNewline;
        description.textComponent.alignment = TextAnchor.UpperLeft;
        CustomInputs["description"] = description;

        AddCustomKungfuChoiceField(parent, "类别", "type", CustomBasicChoiceLabel("type", draft.Type), 10, 260);
        AddCustomKungfuChoiceField(parent, "稀有度", "rare", CustomBasicChoiceLabel("rare", draft.RareLv), 430, 260);
        AddCustomKungfuChoiceField(parent, "所属势力", "force", CustomBasicChoiceLabel("force", draft.ForceId), 850, 260);
        AddCustomKungfuChoiceField(parent, "目标", "target", CustomBasicChoiceLabel("target", draft.TargetType), 10, 310);
        AddCustomKungfuField(parent, "基础伤害", "damage", draft.BaseDamage.ToString("0.###", CultureInfo.InvariantCulture), 430, 310);
        AddCustomKungfuField(parent, "内力消耗", "mana", draft.ManaCost.ToString("0.###", CultureInfo.InvariantCulture), 850, 310);
        AddCustomKungfuField(parent, "经验系数", "exp", draft.ExpRatio.ToString("0.###", CultureInfo.InvariantCulture), 10, 360);
        AddCustomKungfuField(parent, "战斗次数", "uses", draft.BattleMaxUseTime.ToString(CultureInfo.InvariantCulture), 430, 360);
        AddCustomKungfuField(parent, "冷却 CD", "cooldown", draft.CooldownTime.ToString("0.###", CultureInfo.InvariantCulture), 850, 360);
        AddCustomKungfuChoiceField(parent, "结算顺序", "order", CustomBasicChoiceLabel("order", draft.DamageOrder), 10, 410);
        AddCustomKungfuChoiceField(parent, "武器拖尾", "trail", CustomBasicChoiceLabel("trail", draft.TrailId), 430, 410);
        AddCustomKungfuButton(parent, draft.AutoMove ? "角色位移：开启" : "角色位移：关闭", new Vector2(980, 410), new Vector2(240, 38), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.AutoMove = !draft.AutoMove;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });

        _customBasicSummaryText = AddText(parent, CustomKungfuBasicSummary(draft), new Vector2(10, 455), new Vector2(1035, 34), 15);
        AddCustomKungfuButton(parent, "刷新编号说明", new Vector2(1060, 453), new Vector2(160, 36), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            UpdateCustomKungfuBasicSummary();
        });
        AddText(parent, "冷却 CD：-1 沿用游戏原生计算，0 表示无冷却，正数为指定冷却；战斗次数填 0 表示无限制。修改编号后切换标签页即可刷新说明。", new Vector2(10, 486), new Vector2(1210, 38), 14);
        AddDonorRow(parent, "总母版", "base", 10, 530, 820);
        AddCustomKungfuButton(parent, "将总母版复制到全部元素", new Vector2(850, 530), new Vector2(370, 42), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            ApplyFullKungfuDonor(draft.BaseDonorId, true);
            _customStatus = "已把总母版同步到全部可选元素；尚未保存。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, "推荐流程：先选择相近的总母版并同步全部元素，再逐页修改。来源选择只复制该项；自定义范围/姿态开启后会覆盖来源。最后到“保存／管理”生成定义。", new Vector2(10, 590), new Vector2(1210, 54), 16);
        if (!string.IsNullOrEmpty(_customBasicChoiceField)) BuildCustomKungfuBasicChoicePopup(parent);
    }

    private static string CustomKungfuBasicSummary(CustomKungfuRecord draft) =>
        $"当前编号说明：类别 {draft.Type}＝{CustomKungfuTypeName(draft.Type)}　｜　稀有度 {draft.RareLv}＝{RarityName(draft.RareLv)}　｜　目标 {draft.TargetType}＝{SkillTargetTypeName(draft.TargetType)}　｜　结算 {draft.DamageOrder}＝{SkillDamageOrderName(draft.DamageOrder)}";

    private static void UpdateCustomKungfuBasicSummary()
    {
        if (_customBasicSummaryText != null && _customDraft != null)
            _customBasicSummaryText.text = CustomKungfuBasicSummary(_customDraft);
    }

    private static string CustomKungfuTypeName(int type)
    {
        var names = GlobalData.FightSkillName;
        return names != null && type >= 0 && type < names.Count
            ? names[type] : "未知类别";
    }

    private static string SkillTargetTypeName(int value) => Math.Clamp(value, 0, 5) switch
    {
        0 => "敌人", 1 => "己方队伍", 2 => "自己", 3 => "队友", 4 => "空地召唤", _ => "空地跳跃"
    };

    private static string SkillDamageOrderName(int value) => Math.Clamp(value, 0, 2) switch
    {
        0 => "同时结算", 1 => "按距离", _ => "随机顺序"
    };

    private static void BuildCustomKungfuEffectPage(Transform parent)
    {
        if (!string.IsNullOrEmpty(_customEffectEditorField))
        {
            BuildCustomKungfuEffectEntryEditor(parent);
            return;
        }
        AddText(parent, "修炼与战斗效果", new Vector2(10, 8), new Vector2(700, 40), 22);
        AddText(parent, "五组都支持逐条自选、逐条改值；也可以先选择原版来源，再一键导入后删改。", new Vector2(10, 48), new Vector2(1120, 32), 16);
        AddDonorRowWithSummary(parent, "威力加成", "ratio", 10, 92, 590);
        AddDonorRowWithSummary(parent, "修炼需求", "needs", 650, 92, 590);
        AddDonorRowWithSummary(parent, "升级效果", "upgrade", 10, 205, 590);
        AddDonorRowWithSummary(parent, "装备效果", "equip", 650, 205, 590);
        AddDonorRowWithSummary(parent, "使用效果", "use", 10, 318, 590);
        AddText(parent, "“升级效果”决定升级后加成；“装备效果”是运功/装配加成；“使用效果”是战斗中施放时的 Buff、吸血等实际效果。", new Vector2(10, 438), new Vector2(1220, 48), 16);
        AddText(parent, "点击每组右侧“逐条自选”进入编辑器。百分比词条输入 50 会按 50% 保存，其他词条按显示数值保存。", new Vector2(10, 500), new Vector2(1220, 52), 16);
        AddText(parent, "这些数据会按原功法的升级规则计算，不会把突破卡牌的稀有度倍率误写成基础值。", new Vector2(10, 566), new Vector2(1220, 42), 15);
    }

    private static void BuildCustomKungfuRangePage(Transform parent)
    {
        AddText(parent, "范围与姿态（可直接编辑）", new Vector2(10, 8), new Vector2(650, 40), 22);
        AddCustomKungfuButton(parent, _customBattlePreviewActive ? "返回原生战斗" : "原生战斗试用", new Vector2(625, 8), new Vector2(180, 38), ToggleCustomBattlePreviewFromEditor);
        AddCustomKungfuButton(parent, _customRangeSubTab == 0 ? "● 范围编辑" : "范围编辑", new Vector2(820, 8), new Vector2(190, 38), () => SwitchCustomRangeSubTab(0));
        AddCustomKungfuButton(parent, _customRangeSubTab == 1 ? "● 姿态编辑" : "姿态编辑", new Vector2(1025, 8), new Vector2(190, 38), () => SwitchCustomRangeSubTab(1));
        if (_customRangeSubTab == 0) BuildCustomKungfuDirectRangeEditor(parent);
        else BuildCustomKungfuDirectPostureEditor(parent);
    }

    private static void SwitchCustomRangeSubTab(int tab)
    {
        SyncCustomKungfuDraftFromInputs();
        _customRangeSubTab = Math.Clamp(tab, 0, 1);
        InvalidateCustomKungfuPages();
        ShowCustomKungfuTab();
    }

    private static void BuildCustomKungfuDirectRangeEditor(Transform parent)
    {
        var draft = _customDraft!;
        NormalizeCustomKungfuRecord(draft);
        AddText(parent, "攻击范围＝施法时可以选择的目标格；伤害范围＝以目标格为中心实际覆盖的格。来源模式直接复制原功法，自定义模式使用下面的形状和距离。", new Vector2(10, 50), new Vector2(1210, 42), 15);
        AddDonorRow(parent, "攻击范围来源", "attackRange", 10, 96, 590);
        AddDonorRow(parent, "伤害范围来源", "damageRange", 650, 96, 590);
        AddCustomKungfuButton(parent, draft.UseCustomAttackRange ? "自定义攻击范围：开启" : "自定义攻击范围：关闭", new Vector2(10, 150), new Vector2(285, 38), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.UseCustomAttackRange = !draft.UseCustomAttackRange;
            if (draft.UseCustomAttackRange) LoadAttackRangesFromDonor(draft);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, "从来源重新载入", new Vector2(305, 150), new Vector2(220, 38), () =>
        {
            LoadAttackRangesFromDonor(draft);
            draft.UseCustomAttackRange = true;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, draft.UseCustomDamageRange ? "自定义伤害范围：开启" : "自定义伤害范围：关闭", new Vector2(650, 150), new Vector2(285, 38), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.UseCustomDamageRange = !draft.UseCustomDamageRange;
            if (draft.UseCustomDamageRange) LoadDamageRangeFromDonor(draft);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, "从来源重新载入", new Vector2(945, 150), new Vector2(220, 38), () =>
        {
            LoadDamageRangeFromDonor(draft);
            draft.UseCustomDamageRange = true;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });

        AddText(parent, "攻击范围层（多层取并集，最多 4 层）", new Vector2(10, 205), new Vector2(600, 34), 18);
        for (var i = 0; i < draft.CustomAttackRanges.Count; i++)
        {
            var index = i;
            var range = draft.CustomAttackRanges[i];
            AddText(parent, $"第 {i + 1} 层", new Vector2(10, 246 + i * 48), new Vector2(80, 36), 16);
            AddCustomKungfuButton(parent, AttackRangeTypeLabel(range.Type), new Vector2(90, 246 + i * 48), new Vector2(185, 36), () =>
            {
                SyncCustomKungfuDraftFromInputs();
                draft.CustomAttackRanges[index].Type = (draft.CustomAttackRanges[index].Type + 1) % 5;
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
            AddText(parent, "最小", new Vector2(286, 246 + i * 48), new Vector2(46, 36), 15);
            CustomInputs[$"attackMin{i}"] = AddInput(parent, range.Min.ToString(CultureInfo.InvariantCulture), new Vector2(334, 246 + i * 48), new Vector2(68, 36));
            AddText(parent, "最大", new Vector2(412, 246 + i * 48), new Vector2(46, 36), 15);
            CustomInputs[$"attackMax{i}"] = AddInput(parent, range.Max.ToString(CultureInfo.InvariantCulture), new Vector2(460, 246 + i * 48), new Vector2(68, 36));
            AddCustomKungfuButton(parent, "删除", new Vector2(538, 246 + i * 48), new Vector2(78, 36), () =>
            {
                SyncCustomKungfuDraftFromInputs();
                if (draft.CustomAttackRanges.Count > 1) draft.CustomAttackRanges.RemoveAt(index);
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        if (draft.CustomAttackRanges.Count < 4)
            AddCustomKungfuButton(parent, "+ 添加一层攻击范围", new Vector2(10, 246 + draft.CustomAttackRanges.Count * 48), new Vector2(265, 36), () =>
            {
                SyncCustomKungfuDraftFromInputs();
                draft.CustomAttackRanges.Add(new CustomAttackRangeSpec());
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });

        AddText(parent, "伤害范围", new Vector2(650, 205), new Vector2(170, 34), 18);
        AddCustomKungfuButton(parent, DamageRangeTypeLabel(draft.CustomDamageRangeType), new Vector2(650, 246), new Vector2(220, 36), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.CustomDamageRangeType = (draft.CustomDamageRangeType + 1) % 9;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, "最小", new Vector2(880, 246), new Vector2(46, 36), 15);
        CustomInputs["damageRangeMin"] = AddInput(parent, draft.CustomDamageRangeMin.ToString(CultureInfo.InvariantCulture), new Vector2(928, 246), new Vector2(68, 36));
        AddText(parent, "最大", new Vector2(1006, 246), new Vector2(46, 36), 15);
        CustomInputs["damageRangeMax"] = AddInput(parent, draft.CustomDamageRangeMax.ToString(CultureInfo.InvariantCulture), new Vector2(1054, 246), new Vector2(68, 36));
        AddCustomKungfuButton(parent, "刷新示意图", new Vector2(1132, 246), new Vector2(105, 36), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        var grid = AddText(parent, BuildRangePreviewGrid(draft), new Vector2(650, 300), new Vector2(590, 300), 16, TextAnchor.UpperCenter);
        grid.font = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? grid.font;
        AddText(parent, "图例：◎左图为施法者、右图为目标点；■是可选攻击格，●是伤害覆盖格。方向类统一朝上示意；最终以战斗网格为准。", new Vector2(650, 575), new Vector2(590, 60), 15);
        AddText(parent, "形状按钮可循环切换：圆形、方形、直线、斜线、方向。最小/最大是格数；输入后点“刷新示意图”。", new Vector2(10, 585), new Vector2(610, 52), 15);
    }

    private static void BuildCustomKungfuDirectPostureEditor(Transform parent)
    {
        var draft = _customDraft!;
        NormalizeCustomKungfuRecord(draft);
        AddText(parent, "进攻姿态表示技能对目标各部位造成的架势变化；防御姿态表示使用功法时自身获得的部位架势。负数会削减，正数会增加。", new Vector2(10, 50), new Vector2(1210, 42), 15);
        AddDonorRow(parent, "进攻姿态来源", "attackPosture", 10, 96, 590);
        AddDonorRow(parent, "防御姿态来源", "defensePosture", 650, 96, 590);
        AddCustomKungfuButton(parent, draft.UseCustomAttackPosture ? "自定义进攻姿态：开启" : "自定义进攻姿态：关闭", new Vector2(10, 155), new Vector2(285, 38), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.UseCustomAttackPosture = !draft.UseCustomAttackPosture;
            if (draft.UseCustomAttackPosture) LoadAttackPostureFromDonor(draft);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, "从来源重新载入", new Vector2(305, 155), new Vector2(220, 38), () =>
        {
            LoadAttackPostureFromDonor(draft);
            draft.UseCustomAttackPosture = true;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, draft.UseCustomDefensePosture ? "自定义防御姿态：开启" : "自定义防御姿态：关闭", new Vector2(650, 155), new Vector2(285, 38), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            draft.UseCustomDefensePosture = !draft.UseCustomDefensePosture;
            if (draft.UseCustomDefensePosture) LoadDefensePostureFromDonor(draft);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddCustomKungfuButton(parent, "从来源重新载入", new Vector2(945, 155), new Vector2(220, 38), () =>
        {
            LoadDefensePostureFromDonor(draft);
            draft.UseCustomDefensePosture = true;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, "进攻姿态（目标部位）", new Vector2(10, 220), new Vector2(570, 34), 19, TextAnchor.MiddleCenter);
        AddText(parent, "防御姿态（自身部位）", new Vector2(650, 220), new Vector2(570, 34), 19, TextAnchor.MiddleCenter);
        for (var i = 0; i < 6; i++)
        {
            var x = i % 3 * 190;
            var y = 270 + i / 3 * 58;
            var label = PartPostureLabel(i);
            AddText(parent, label, new Vector2(10 + x, y), new Vector2(80, 36), 16);
            CustomInputs[$"attackPosture{i}"] = AddInput(parent, draft.CustomAttackPosture[i].ToString("0.###", CultureInfo.InvariantCulture), new Vector2(90 + x, y), new Vector2(90, 36));
            AddText(parent, label, new Vector2(650 + x, y), new Vector2(80, 36), 16);
            CustomInputs[$"defensePosture{i}"] = AddInput(parent, draft.CustomDefensePosture[i].ToString("0.###", CultureInfo.InvariantCulture), new Vector2(730 + x, y), new Vector2(90, 36));
        }
        AddCustomKungfuButton(parent, "应用输入并刷新说明", new Vector2(10, 410), new Vector2(1210, 42), () =>
        {
            SyncCustomKungfuDraftFromInputs();
            _customStatus = "已更新自定义姿态草稿；保存后才写入定义。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, "当前进攻姿态：" + CustomPostureSummary(draft.CustomAttackPosture), new Vector2(10, 475), new Vector2(590, 60), 16);
        AddText(parent, "当前防御姿态：" + CustomPostureSummary(draft.CustomDefensePosture), new Vector2(650, 475), new Vector2(590, 60), 16);
        AddText(parent, "关闭自定义时仍保留输入值，但实际使用来源功法的数据；再次开启即可继续编辑。若组合后不能结算，可先恢复同一来源测试。", new Vector2(10, 570), new Vector2(1210, 55), 15);
    }

    private static KungfuSkillData? CustomDonorSkill(int donorId)
    {
        var database = GameDataController.Instance;
        return database == null ? null : OriginalKungfu(database, donorId);
    }

    private static void LoadAttackRangesFromDonor(CustomKungfuRecord draft)
    {
        var donor = CustomDonorSkill(draft.AttackRangeDonorId);
        var ranges = new List<CustomAttackRangeSpec>();
        if (donor?.attackRangeData != null)
        {
            for (var i = 0; i < donor.attackRangeData.Count && ranges.Count < 4; i++)
            {
                var source = donor.attackRangeData[i];
                if (source != null) ranges.Add(new CustomAttackRangeSpec { Type = (int)source.rangeType, Min = source.minRange, Max = source.maxRange });
            }
        }
        if (ranges.Count == 0) ranges.Add(new CustomAttackRangeSpec());
        draft.CustomAttackRanges = ranges;
        NormalizeCustomKungfuRecord(draft);
    }

    private static void LoadDamageRangeFromDonor(CustomKungfuRecord draft)
    {
        var source = CustomDonorSkill(draft.DamageRangeDonorId)?.damageRangeData;
        if (source == null) return;
        draft.CustomDamageRangeType = (int)source.rangeType;
        draft.CustomDamageRangeMin = source.minRange;
        draft.CustomDamageRangeMax = source.maxRange;
        NormalizeCustomKungfuRecord(draft);
    }

    private static void LoadAttackPostureFromDonor(CustomKungfuRecord draft) => draft.CustomAttackPosture = PostureValues(CustomDonorSkill(draft.AttackPostureDonorId)?.atkPartPosture);
    private static void LoadDefensePostureFromDonor(CustomKungfuRecord draft) => draft.CustomDefensePosture = PostureValues(CustomDonorSkill(draft.DefensePostureDonorId)?.defPartPosture);

    private static string AttackRangeTypeLabel(int type) => Math.Clamp(type, 0, 4) switch
    {
        0 => "圆形／距离环", 1 => "方形", 2 => "十字直线", 3 => "对角斜线", _ => "朝向区域"
    };

    private static string DamageRangeTypeLabel(int type) => Math.Clamp(type, 0, 8) switch
    {
        0 => "圆形／距离环", 1 => "方形", 2 => "十字直线", 3 => "对角斜线", 4 => "朝向直线",
        5 => "朝向扇形", 6 => "朝向斜线", 7 => "全场", _ => "朝向十字"
    };

    private static string PartPostureLabel(int index)
    {
        try
        {
            var names = GlobalData.PartPostureName;
            if (names != null && index >= 0 && index < names.Count && !string.IsNullOrWhiteSpace(names[index])) return names[index];
        }
        catch { }
        return $"部位 {index + 1}";
    }

    private static string CustomPostureSummary(List<float> values)
    {
        var text = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            var value = i < values.Count ? values[i] : 0f;
            if (Math.Abs(value) < 0.0001f) continue;
            if (text.Length > 0) text.Append("　");
            text.Append(PartPostureLabel(i)).Append(value >= 0 ? "+" : "").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
        return text.Length == 0 ? "无变化" : text.ToString();
    }

    private static string BuildRangePreviewGrid(CustomKungfuRecord draft)
    {
        var attackRanges = draft.CustomAttackRanges;
        if (!draft.UseCustomAttackRange)
        {
            var source = CustomDonorSkill(draft.AttackRangeDonorId);
            attackRanges = new List<CustomAttackRangeSpec>();
            if (source?.attackRangeData != null)
                for (var i = 0; i < source.attackRangeData.Count; i++)
                {
                    var range = source.attackRangeData[i];
                    if (range != null) attackRanges.Add(new CustomAttackRangeSpec { Type = (int)range.rangeType, Min = range.minRange, Max = range.maxRange });
                }
        }
        var damageType = draft.CustomDamageRangeType;
        var damageMin = draft.CustomDamageRangeMin;
        var damageMax = draft.CustomDamageRangeMax;
        if (!draft.UseCustomDamageRange)
        {
            var damage = CustomDonorSkill(draft.DamageRangeDonorId)?.damageRangeData;
            if (damage != null) { damageType = (int)damage.rangeType; damageMin = damage.minRange; damageMax = damage.maxRange; }
        }
        var text = new StringBuilder("【攻击范围】　　　　　　【伤害范围】\n");
        for (var y = 4; y >= -4; y--)
        {
            for (var x = -4; x <= 4; x++)
            {
                if (x == 0 && y == 0) { text.Append("◎ "); continue; }
                var attack = false;
                for (var i = 0; i < attackRanges.Count; i++)
                    if (RangeShapeContains(attackRanges[i].Type, attackRanges[i].Min, attackRanges[i].Max, x, y, false)) { attack = true; break; }
                text.Append(attack ? "■ " : "· ");
            }
            text.Append("　　");
            for (var x = -4; x <= 4; x++)
                text.Append(x == 0 && y == 0 ? "◎ " : RangeShapeContains(damageType, damageMin, damageMax, x, y, true) ? "● " : "· ");
            text.AppendLine();
        }
        return text.ToString();
    }

    private static bool RangeShapeContains(int type, int min, int max, int x, int y, bool damage)
    {
        if (max < min) (min, max) = (max, min);
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);
        var manhattan = ax + ay;
        var square = Math.Max(ax, ay);
        if (damage && type == 7) return true;
        var distance = type == 1 ? square : manhattan;
        if (distance < min || distance > max) return false;
        return type switch
        {
            0 => true,
            1 => true,
            2 => x == 0 || y == 0,
            3 => ax == ay,
            4 when damage => x == 0 && y > 0,
            5 when damage => y > 0 && ax <= y,
            6 when damage => y > 0 && ax == y,
            8 when damage => x == 0 || y == 0,
            4 => y > 0 && ax <= y,
            _ => false
        };
    }

    private static void BuildCustomKungfuVisualPage(Transform parent)
    {
        AddText(parent, "动作与战斗特效", new Vector2(10, 8), new Vector2(700, 40), 22);
        AddText(parent, "动作、武器、弹道、特效和图标均可独立选择；“整套同步”兼容性最好。", new Vector2(10, 48), new Vector2(1100, 32), 16);
        AddDonorRow(parent, "使用动作", "animation", 10, 100, 590);
        AddDonorRow(parent, "显示武器", "weapon", 650, 100, 590);
        AddDonorRow(parent, "弹道", "bullet", 10, 175, 590);
        AddDonorRow(parent, "场景特效", "visual", 650, 175, 590);
        AddDonorRow(parent, "功法图标", "icon", 10, 250, 590);
        AddCustomKungfuButton(parent, "按动作来源同步整套视觉", new Vector2(650, 250), new Vector2(590, 44), () =>
        {
            var draft = _customDraft!;
            draft.WeaponDonorId = draft.AnimationDonorId;
            draft.BulletDonorId = draft.AnimationDonorId;
            draft.VisualEffectDonorId = draft.AnimationDonorId;
            draft.IconDonorId = draft.AnimationDonorId;
            draft.CustomIconFile = "";
            var source = GameDataController.Instance == null ? null : OriginalKungfu(GameDataController.Instance, draft.AnimationDonorId);
            if (source != null) { draft.AutoMove = source.autoHeroMove; draft.TrailId = source.trailID; }
            _customStatus = "动作、武器、弹道、特效、图标与位移已整套同步；尚未保存。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        var draftIcon = _customDraft!;
        var customIconLabel = string.IsNullOrEmpty(draftIcon.CustomIconFile)
            ? "本地图标：未选择（当前使用上方的来源图标）"
            : "本地图标：" + draftIcon.CustomIconFile;
        AddText(parent, customIconLabel, new Vector2(10, 310), new Vector2(535, 42), 15);
        AddCustomKungfuButton(parent, "选择本地图标", new Vector2(650, 310), new Vector2(265, 42), ImportCustomKungfuIcon);
        AddCustomKungfuButton(parent, "恢复来源图标", new Vector2(925, 310), new Vector2(315, 42), RestoreCustomKungfuDonorIcon);
        AddText(parent, "窗口无法选择？粘贴 PNG/JPG 图片完整路径后导入：", new Vector2(10, 570), new Vector2(1000, 28), 15);
        var iconPath = AddInput(parent, "", new Vector2(10, 607), new Vector2(920, 38));
        AddCustomKungfuButton(parent, "从路径导入", new Vector2(945, 607), new Vector2(295, 38), () => ImportCustomKungfuIconPath(iconPath.text));
        var customIconSprite = GetCustomKungfuIconSprite(draftIcon.CustomIconFile);
        if (customIconSprite == null && !string.IsNullOrEmpty(draftIcon.CustomIconFile))
        {
            var file = NormalizeCustomKungfuIconFile(draftIcon.CustomIconFile);
            var reason = IconLoadErrors.TryGetValue(file, out var error) ? error : "图标尚未加载，请重新加载查看诊断。";
            AddText(parent, reason + "\n完整诊断：BepInEx/LogOutput.log", new Vector2(10, 651), new Vector2(915, 48), 14);
            AddCustomKungfuButton(parent, "重新加载已存图标", new Vector2(945, 651), new Vector2(295, 38), () =>
            {
                IconRetryAt.Remove(file);
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        if (customIconSprite != null)
        {
            var preview = UiObject("CustomKungfuUploadedIconPreview", parent, new Vector2(56, 56), new Vector2(575, 302));
            var image = preview.AddComponent<Image>();
            image.sprite = customIconSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }
        AddCustomKungfuButton(parent, _customBattlePreviewActive ? "返回原生战斗（不改技能栏）" : "使用当前原生装备进入试战", new Vector2(10, 375), new Vector2(1230, 48), ToggleCustomBattlePreviewFromEditor);
        AddText(parent, _customBattlePreviewActive
            ? "当前是游戏原生战斗：模组没有替换、补位或重建角色技能栏；未保存的草稿不会在战斗中热更新。"
            : "先保存并让主角习得，再通过游戏原生功法界面装备。此按钮只调用原生试战入口，不会临时插入功法或改动任何技能槽。",
            new Vector2(10, 433), new Vector2(1220, 62), 16);
        AddText(parent, "本地图标支持 PNG/JPG/JPEG，透明背景请用 PNG；上传图标优先于来源图标。注意：某些动作依赖固定动画事件，自由混搭异常时请使用整套同步。", new Vector2(10, 500), new Vector2(1220, 58), 15);
    }

    private static void ToggleCustomBattlePreviewFromEditor()
    {
        if (_customBattlePreviewActive)
        {
            SyncCustomKungfuDraftFromInputs();
            _customVisible = false;
            if (_customUiRoot != null) _customUiRoot.SetActive(false);
            _customStatus = "已返回原生战斗；技能栏未被模组改动。";
            return;
        }
        StartCustomKungfuBattlePreview();
    }

    private static void StartCustomKungfuBattlePreview()
    {
        SyncCustomKungfuDraftFromInputs();
        try
        {
            var draft = _customDraft ?? throw new InvalidOperationException("尚未创建功法草稿");
            var battle = BattleController.Instance ?? throw new InvalidOperationException("战斗控制器尚未载入");
            var player = Player ?? throw new InvalidOperationException("主角存档尚未载入");
            if (battle.battleState != BattleState.None && battle.battleState != BattleState.End)
                throw new InvalidOperationException("当前已有战斗正在进行，请先结束该战斗");
            if (draft.Id < CustomKungfuIdMin || FindSavedCustomKungfu(draft.Id) == null)
                throw new InvalidOperationException("请先保存自定义功法");
            var learned = player.FindSkill(draft.Id)
                ?? throw new InvalidOperationException("请先使用“保存并让主角习得”");
            var equipped = false;
            var attackSkills = player.attackSkills;
            if (attackSkills != null)
                for (var i = 0; i < attackSkills.Count; i++)
                    if (attackSkills[i] != null && attackSkills[i].skillID == draft.Id) { equipped = true; break; }
            if (!equipped)
                throw new InvalidOperationException("请先在游戏原生功法界面把该功法装备到攻击栏");
            var opponent = FindCustomKungfuBattlePreviewOpponent(player)
                ?? throw new InvalidOperationException("当前存档中没有可用的试演对手");
            _customBattlePreviewSkillId = draft.Id;
            _customBattlePreviewPlayer = player;
            _customBattlePreviewSkill = learned;
            _customBattlePreviewOpponent = opponent;

            _customBattlePreviewActive = true;
            _customBattlePreviewEnding = false;
            _customCountBypassLogCount = 0;
            _customBattlePreviewStartedAt = Time.realtimeSinceStartup;
            battle.PrepareBattleMap(
                BattleType.StudyFight,
                player,
                opponent,
                string.Empty,
                false,
                false,
                new BattleMapTypeData(BattleMapType.Arena, 12, 12));

            _customStatus = $"已进入原生试战：{draft.Name}（ID {draft.Id}）。技能栏完全由游戏管理。";
            _customVisible = false;
            if (_customUiRoot != null) _customUiRoot.SetActive(false);
            LongYinTrainerPlugin.Logger.LogInfo($"Started native StudyFight for equipped custom kungfu {draft.Id}; no hero skill lists or save records were modified.");
        }
        catch (Exception ex)
        {
            CleanupCustomKungfuBattlePreviewRuntime();
            _customBattlePreviewActive = false;
            _customBattlePreviewEnding = false;
            _customStatus = "无法进入原生试战：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Native custom kungfu StudyFight start failed safely: {ex.Message}");
        }
    }

    private static int ResolveCustomKungfuBaseDonor(GameDataController database, CustomKungfuRecord record)
    {
        var candidates = new[]
        {
            record.BaseDonorId, record.AnimationDonorId, record.WeaponDonorId, record.BulletDonorId,
            record.VisualEffectDonorId, record.IconDonorId, record.AttackRangeDonorId, record.DamageRangeDonorId,
            record.DamageRatioDonorId, record.NeedsDonorId, record.UpgradeDonorId, record.EquipDonorId,
            record.UseDonorId, record.AttackPostureDonorId, record.DefensePostureDonorId
        };
        for (var i = 0; i < candidates.Length; i++)
            if (OriginalKungfu(database, candidates[i]) != null) return candidates[i];
        if (database.kungfuSkillDataBase == null) return -1;
        var result = int.MaxValue;
        foreach (var pair in database.kungfuSkillDataBase)
            if (pair.Key >= 0 && pair.Key < CustomKungfuIdMin && pair.Value != null && !pair.Value.hide)
                result = Math.Min(result, pair.Key);
        return result == int.MaxValue ? -1 : result;
    }

    private static HeroData? FindCustomKungfuBattlePreviewOpponent(HeroData player)
    {
        var selected = TargetHero();
        if (IsUsableCustomKungfuBattlePreviewOpponent(selected, player)) return selected;
        try
        {
            var heroes = GameController.Instance?.worldData?.Heros;
            if (heroes == null) return null;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (IsUsableCustomKungfuBattlePreviewOpponent(hero, player)) return hero;
            }
        }
        catch { }
        return null;
    }

    private static bool IsUsableCustomKungfuBattlePreviewOpponent(HeroData? hero, HeroData player)
    {
        if (hero == null || hero.heroID == player.heroID || hero.dead || hero.hide || hero.maxhp <= 0f) return false;
        try { return hero.attackSkills != null && hero.attackSkills.Count > 0; }
        catch { return false; }
    }

    public static void NotifyCustomKungfuBattleEnded()
    {
        if (!_customBattlePreviewActive) return;
        _customBattlePreviewEnding = true;
        _customBattlePreviewActive = false;
        CleanupCustomKungfuBattlePreviewRuntime();
        _customBattlePreviewReturnAt = Time.realtimeSinceStartup + 0.1f;
        _customStatus = "原生试战已结束；模组没有改动角色技能栏或装备记录。";
    }

    private static void CleanupCustomKungfuBattlePreviewRuntime()
    {
        // Native StudyFight owns all hero/battle cleanup. This method deliberately
        // clears only mod-side tracking and never touches skills or save records.
        _customBattlePreviewPlayer = null;
        _customBattlePreviewOpponent = null;
        _customBattlePreviewSkill = null;
        _customBattlePreviewSkillId = int.MinValue;
    }

    private static void LogCustomKungfuPreviewDefinition(string action, CustomKungfuRecord record, KungfuSkillData definition)
    {
        try
        {
            var attackRanges = definition.attackRangeData?.Count ?? 0;
            var effects = definition.skillSpeEffects?.Count ?? 0;
            LongYinTrainerPlugin.Logger.LogInfo(
                $"Custom kungfu preview {action}: id={definition.skillID}, name={definition.name}, type={definition.type}, rare={definition.rareLv}, " +
                $"damage={definition.baseDamage:0.##}, mana={definition.manaCost:0.##}, attackRanges={attackRanges}, damageRange={(definition.damageRangeData == null ? "null" : "set")}, " +
                $"animation={definition.animationName}, weapon={definition.weaponName}, bullet={(definition.skillBullet == null ? "null" : "set")}, effects={effects}, iconDonor={record.IconDonorId}.");
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Custom kungfu definition audit skipped safely: {ex.Message}");
        }
    }

    private static void AddDonorRow(Transform parent, string label, string field, float x, float y, float width)
    {
        AddText(parent, label, new Vector2(x, y), new Vector2(130, 42), 18);
        AddText(parent, DonorDisplay(GetCustomDonor(field)), new Vector2(x + 130, y), new Vector2(width - 310, 42), 16);
        AddCustomKungfuButton(parent, "选择来源", new Vector2(x + width - 170, y), new Vector2(170, 42), () => OpenCustomKungfuDonorPicker(field));
    }

    private static void AddDonorRowWithSummary(Transform parent, string label, string field, float x, float y, float width)
    {
        AddDonorRow(parent, label, field, x, y, width);
        AddText(parent, CustomKungfuDonorInlineSummary(field), new Vector2(x + 8, y + 45), new Vector2(width - 168, 62), 14, TextAnchor.UpperLeft);
        AddCustomKungfuButton(parent, CustomEffectUsesCustom(field) ? "● 逐条自选" : "逐条自选", new Vector2(x + width - 150, y + 50), new Vector2(150, 38), () => OpenCustomKungfuEffectEntryEditor(field));
    }

    private static bool CustomEffectUsesCustom(string field)
    {
        var draft = _customDraft;
        if (draft == null) return false;
        return field switch
        {
            "ratio" => draft.UseCustomDamageRatio,
            "needs" => draft.UseCustomNeeds,
            "upgrade" => draft.UseCustomUpgrade,
            "equip" => draft.UseCustomEquip,
            "use" => draft.UseCustomUse,
            _ => false
        };
    }

    private static void SetCustomEffectUsesCustom(string field, bool value)
    {
        var draft = _customDraft;
        if (draft == null) return;
        switch (field)
        {
            case "ratio": draft.UseCustomDamageRatio = value; break;
            case "needs": draft.UseCustomNeeds = value; break;
            case "upgrade": draft.UseCustomUpgrade = value; break;
            case "equip": draft.UseCustomEquip = value; break;
            case "use": draft.UseCustomUse = value; break;
        }
    }

    private static void OpenCustomKungfuEffectEntryEditor(string field)
    {
        SyncCustomKungfuDraftFromInputs();
        if (!CustomEffectUsesCustom(field)) ImportCustomEffectFromDonor(field);
        _customEffectEditorField = field;
        _customEffectCurrentPage = 0;
        _customEffectCatalogPage = 0;
        _customEffectCatalogGroup = 0;
        _customEffectSelectedKey = "";
        InvalidateCustomKungfuPages();
        _customTab = 1;
        ShowCustomKungfuTab();
    }

    private static void ImportCustomEffectFromDonor(string field)
    {
        var draft = _customDraft;
        if (draft == null) return;
        var donor = CustomDonorSkill(GetCustomDonor(field));
        if (field is "ratio" or "needs")
        {
            var source = field == "ratio" ? donor?.addDamageRatio : donor?.skillNeeds;
            var destination = new List<CustomAttriNumSpec>();
            if (source != null)
            {
                AddCustomAttriList(destination, "attri", source.attri, 6);
                AddCustomAttriList(destination, "fight", source.fightSkill, 9);
                AddCustomAttriList(destination, "living", source.livingSkill, 9);
                if (source.Hp != 0f) destination.Add(new CustomAttriNumSpec { Kind = "hp", Value = source.Hp });
                if (source.Power != 0f) destination.Add(new CustomAttriNumSpec { Kind = "power", Value = source.Power });
                if (source.Mana != 0f) destination.Add(new CustomAttriNumSpec { Kind = "mana", Value = source.Mana });
                if (source.Charm != 0f) destination.Add(new CustomAttriNumSpec { Kind = "charm", Value = source.Charm });
            }
            if (field == "ratio") draft.CustomDamageRatio = destination;
            else draft.CustomNeeds = destination;
        }
        else
        {
            var source = field switch { "upgrade" => donor?.upgradeAddData, "equip" => donor?.equipAddData, _ => donor?.useAddData };
            var destination = new List<CustomSpeAddSpec>();
            var keys = source?.GetKeys();
            if (source != null && keys != null)
                for (var i = 0; i < keys.Count; i++) destination.Add(new CustomSpeAddSpec { TypeId = keys[i], Value = source.Get(keys[i]) });
            if (field == "upgrade") draft.CustomUpgrade = destination;
            else if (field == "equip") draft.CustomEquip = destination;
            else draft.CustomUse = destination;
        }
        SetCustomEffectUsesCustom(field, true);
        NormalizeCustomKungfuRecord(draft);
        _customStatus = $"已把{CustomDonorFieldLabel(field)}来源中的词条导入逐条编辑器；尚未保存。";
    }

    private static void AddCustomAttriList(List<CustomAttriNumSpec> destination, string kind, Il2CppSystem.Collections.Generic.List<float>? source, int count)
    {
        if (source == null) return;
        for (var i = 0; i < count && i < source.Count; i++)
            if (source[i] != 0f) destination.Add(new CustomAttriNumSpec { Kind = kind, Index = i, Value = source[i] });
    }

    private static void BuildCustomKungfuEffectEntryEditor(Transform parent)
    {
        var field = _customEffectEditorField;
        var attriMode = field is "ratio" or "needs";
        AddText(parent, $"逐条自选 · {CustomDonorFieldLabel(field)}", new Vector2(10, 8), new Vector2(760, 38), 22);
        AddCustomKungfuButton(parent, "返回效果总览", new Vector2(1020, 8), new Vector2(210, 38), () =>
        {
            _customEffectEditorField = "";
            _customEffectSelectedKey = "";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, attriMode
            ? "左侧是当前词条，右侧选类型并填写底层系数/要求值。0 可以明确覆盖为无加成；负数也允许。"
            : "左侧是当前词条，右侧选类型并填写显示值。标有百分比的类型输入 50 即保存为 50%，不会变成 5000%。",
            new Vector2(10, 48), new Vector2(1210, 38), 15);

        var current = GetCustomEffectCurrentChoices(field);
        const int currentPageSize = 6;
        var currentPageCount = Math.Max(1, (current.Count + currentPageSize - 1) / currentPageSize);
        _customEffectCurrentPage = Math.Clamp(_customEffectCurrentPage, 0, currentPageCount - 1);
        AddText(parent, $"当前自选词条（{current.Count}）", new Vector2(10, 92), new Vector2(570, 32), 18, TextAnchor.MiddleCenter);
        var currentStart = _customEffectCurrentPage * currentPageSize;
        for (var i = 0; i < currentPageSize && currentStart + i < current.Count; i++)
        {
            var choice = current[currentStart + i];
            var capturedKey = choice.Key;
            AddCustomKungfuButton(parent, (_customEffectSelectedKey == capturedKey ? "● " : "") + choice.Label, new Vector2(10, 132 + i * 52), new Vector2(480, 42), () =>
            {
                _customEffectSelectedKey = capturedKey;
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
            AddCustomKungfuButton(parent, "删除", new Vector2(500, 132 + i * 52), new Vector2(80, 42), () =>
            {
                RemoveCustomEffectEntry(field, capturedKey);
                if (_customEffectSelectedKey == capturedKey) _customEffectSelectedKey = "";
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        AddCustomKungfuButton(parent, "上一页", new Vector2(10, 450), new Vector2(125, 34), () => { _customEffectCurrentPage = Math.Max(0, _customEffectCurrentPage - 1); InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });
        AddText(parent, $"{_customEffectCurrentPage + 1}/{currentPageCount}", new Vector2(145, 450), new Vector2(290, 34), 15, TextAnchor.MiddleCenter);
        AddCustomKungfuButton(parent, "下一页", new Vector2(445, 450), new Vector2(135, 34), () => { _customEffectCurrentPage = Math.Min(currentPageCount - 1, _customEffectCurrentPage + 1); InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });
        AddCustomKungfuButton(parent, "从当前来源重新导入", new Vector2(10, 520), new Vector2(270, 40), () => { ImportCustomEffectFromDonor(field); _customEffectSelectedKey = ""; InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });
        AddCustomKungfuButton(parent, "清空全部", new Vector2(290, 520), new Vector2(140, 40), () => { ClearCustomEffectEntries(field); _customEffectSelectedKey = ""; InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });
        AddCustomKungfuButton(parent, "改回来源模式", new Vector2(440, 520), new Vector2(140, 40), () =>
        {
            SetCustomEffectUsesCustom(field, false);
            _customEffectEditorField = "";
            _customEffectSelectedKey = "";
            _customStatus = $"{CustomDonorFieldLabel(field)}已改回整组来源模式；自选内容仍保留，重新进入即可继续编辑。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });

        var groupLabels = attriMode ? new[] { "全部", "六维", "武学", "生活", "资源" } : new[] { "常用", "六维", "武学", "战斗", "特殊", "全部" };
        _customEffectCatalogGroup = Math.Clamp(_customEffectCatalogGroup, 0, groupLabels.Length - 1);
        AddText(parent, "可选词条类型", new Vector2(650, 92), new Vector2(590, 30), 18, TextAnchor.MiddleCenter);
        var groupWidth = 590f / groupLabels.Length;
        for (var i = 0; i < groupLabels.Length; i++)
        {
            var captured = i;
            AddCustomKungfuButton(parent, (i == _customEffectCatalogGroup ? "● " : "") + groupLabels[i], new Vector2(650 + i * groupWidth, 124), new Vector2(groupWidth - 5, 32), () =>
            {
                _customEffectCatalogGroup = captured;
                _customEffectCatalogPage = 0;
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        var catalog = GetCustomEffectCatalog(field, _customEffectCatalogGroup);
        const int catalogPageSize = 7;
        var catalogPageCount = Math.Max(1, (catalog.Count + catalogPageSize - 1) / catalogPageSize);
        _customEffectCatalogPage = Math.Clamp(_customEffectCatalogPage, 0, catalogPageCount - 1);
        var catalogStart = _customEffectCatalogPage * catalogPageSize;
        for (var i = 0; i < catalogPageSize && catalogStart + i < catalog.Count; i++)
        {
            var choice = catalog[catalogStart + i];
            var capturedKey = choice.Key;
            AddCustomKungfuButton(parent, (_customEffectSelectedKey == capturedKey ? "● " : "") + choice.Label, new Vector2(650, 166 + i * 45), new Vector2(590, 37), () =>
            {
                _customEffectSelectedKey = capturedKey;
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        AddCustomKungfuButton(parent, "◀", new Vector2(650, 486), new Vector2(90, 32), () => { _customEffectCatalogPage = Math.Max(0, _customEffectCatalogPage - 1); InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });
        AddText(parent, $"第 {_customEffectCatalogPage + 1}/{catalogPageCount} 页，共 {catalog.Count} 项", new Vector2(750, 486), new Vector2(380, 32), 14, TextAnchor.MiddleCenter);
        AddCustomKungfuButton(parent, "▶", new Vector2(1140, 486), new Vector2(100, 32), () => { _customEffectCatalogPage = Math.Min(catalogPageCount - 1, _customEffectCatalogPage + 1); InvalidateCustomKungfuPages(); ShowCustomKungfuTab(); });

        var selected = FindCustomEffectChoice(field, _customEffectSelectedKey);
        AddText(parent, selected == null ? "先从当前词条或右侧目录选择一项" : $"当前选择：{selected.Label}", new Vector2(650, 530), new Vector2(590, 32), 15);
        var displayValue = selected == null ? "0" : GetCustomEffectDisplayValue(field, selected).ToString("0.###", CultureInfo.InvariantCulture);
        AddText(parent, selected?.Percent == true ? "数值（%）" : "数值", new Vector2(650, 570), new Vector2(110, 38), 16);
        var valueInput = AddInput(parent, displayValue, new Vector2(760, 570), new Vector2(250, 38));
        AddCustomKungfuButton(parent, "添加／更新", new Vector2(1020, 570), new Vector2(220, 38), () =>
        {
            if (selected == null) { _customStatus = "请先选择一个词条类型。"; return; }
            if (!float.TryParse(valueInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value)) { _customStatus = "词条数值无效。"; return; }
            SetCustomEffectEntry(field, selected, value);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, attriMode ? "威力/需求表使用底层原始数值；可以用来源导入观察原版量级后再调整。" : "百分比转换只发生在界面边界，配置文件与游戏底层仍保存小数。", new Vector2(650, 618), new Vector2(590, 42), 14);
    }

    private static List<CustomEffectChoice> GetCustomEffectCurrentChoices(string field)
    {
        var result = new List<CustomEffectChoice>();
        var draft = _customDraft;
        if (draft == null) return result;
        if (field is "ratio" or "needs")
        {
            var source = field == "ratio" ? draft.CustomDamageRatio : draft.CustomNeeds;
            for (var i = 0; i < source.Count; i++)
            {
                var spec = source[i];
                result.Add(new CustomEffectChoice
                {
                    Key = CustomAttriSpecKey(spec.Kind, spec.Index), Kind = spec.Kind, Index = spec.Index,
                    Label = $"{CustomAttriSpecLabel(spec.Kind, spec.Index)} {spec.Value:+0.###;-0.###;0}"
                });
            }
        }
        else
        {
            var source = CustomSpeAddSpecs(field);
            for (var i = 0; i < source.Count; i++)
            {
                var spec = source[i];
                result.Add(new CustomEffectChoice
                {
                    Key = "spe:" + spec.TypeId.ToString(CultureInfo.InvariantCulture), TypeId = spec.TypeId,
                    Percent = IsPercentageAffix(spec.TypeId), Label = DescribeSingleAffix(spec.TypeId, spec.Value)
                });
            }
        }
        return result;
    }

    private static List<CustomEffectChoice> GetCustomEffectCatalog(string field, int group)
    {
        var result = new List<CustomEffectChoice>();
        if (field is "ratio" or "needs")
        {
            void AddKind(string kind, string[] labels)
            {
                for (var i = 0; i < labels.Length; i++)
                    if (group == 0 || (group == 1 && kind == "attri") || (group == 2 && kind == "fight") || (group == 3 && kind == "living"))
                        result.Add(new CustomEffectChoice { Key = CustomAttriSpecKey(kind, i), Kind = kind, Index = i, Label = labels[i] });
            }
            AddKind("attri", AttributeAffixLabels);
            AddKind("fight", FightSkillAffixLabels);
            AddKind("living", LivingSkillAffixLabels);
            if (group is 0 or 4)
            {
                result.Add(new CustomEffectChoice { Key = "hp:0", Kind = "hp", Label = "生命" });
                result.Add(new CustomEffectChoice { Key = "mana:0", Kind = "mana", Label = "内力" });
                result.Add(new CustomEffectChoice { Key = "power:0", Kind = "power", Label = "体力" });
                result.Add(new CustomEffectChoice { Key = "charm:0", Kind = "charm", Label = "魅力" });
            }
            return result;
        }

        var common = new HashSet<string>(AffixPresets, StringComparer.Ordinal)
        {
            "externalDamage", "internalDamage", "poisonDamage", "suckHp", "suckMana", "killMana",
            "trueDamage", "internalTrueDamage", "recoverHp", "recoverMana", "recoverPower", "invincible", "stun", "reborn"
        };
        var seen = new HashSet<int>();
        foreach (var raw in Enum.GetValues(typeof(HeroSpeAddDataType)))
        {
            var id = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            if (!seen.Add(id)) continue;
            var name = Enum.GetName(typeof(HeroSpeAddDataType), id);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var include = group switch
            {
                0 => common.Contains(name),
                1 => name.Contains("Attri", StringComparison.Ordinal) || name.StartsWith("attri", StringComparison.Ordinal) || name is "maxHp" or "maxMana" or "maxPower",
                2 => name.Contains("fightSkill", StringComparison.OrdinalIgnoreCase) || name.Contains("livingSkill", StringComparison.OrdinalIgnoreCase),
                3 => IsCustomBattleEffectName(name),
                4 => !common.Contains(name) && !name.Contains("Attri", StringComparison.Ordinal) && !name.Contains("Skill", StringComparison.Ordinal),
                _ => true
            };
            if (!include) continue;
            result.Add(new CustomEffectChoice
            {
                Key = "spe:" + id.ToString(CultureInfo.InvariantCulture), TypeId = id, Percent = IsPercentageAffix(id),
                Label = DescribeSingleAffix(id, 0f) + $"  [ID {id}]"
            });
        }
        result.Sort((left, right) => left.TypeId.CompareTo(right.TypeId));
        return result;
    }

    private static bool IsCustomBattleEffectName(string name) =>
        name.Contains("Damage", StringComparison.OrdinalIgnoreCase) || name.Contains("Armor", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Speed", StringComparison.OrdinalIgnoreCase) || name.Contains("Acc", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Evade", StringComparison.OrdinalIgnoreCase) || name.Contains("Crit", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Counter", StringComparison.OrdinalIgnoreCase) || name.Contains("Combo", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Hp", StringComparison.OrdinalIgnoreCase) || name.Contains("Mana", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Power", StringComparison.OrdinalIgnoreCase) || name.Contains("Wound", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Move", StringComparison.OrdinalIgnoreCase);

    private static string CustomAttriSpecKey(string kind, int index) => kind.ToLowerInvariant() + ":" + index.ToString(CultureInfo.InvariantCulture);

    private static string CustomAttriSpecLabel(string kind, int index) => kind switch
    {
        "attri" when index >= 0 && index < AttributeAffixLabels.Length => AttributeAffixLabels[index],
        "fight" when index >= 0 && index < FightSkillAffixLabels.Length => FightSkillAffixLabels[index],
        "living" when index >= 0 && index < LivingSkillAffixLabels.Length => LivingSkillAffixLabels[index],
        "hp" => "生命", "mana" => "内力", "power" => "体力", "charm" => "魅力", _ => kind + index
    };

    private static List<CustomSpeAddSpec> CustomSpeAddSpecs(string field)
    {
        var draft = _customDraft!;
        return field switch { "upgrade" => draft.CustomUpgrade, "equip" => draft.CustomEquip, _ => draft.CustomUse };
    }

    private static CustomEffectChoice? FindCustomEffectChoice(string field, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var allGroup = field is "ratio" or "needs" ? 0 : 5;
        var catalog = GetCustomEffectCatalog(field, allGroup);
        for (var i = 0; i < catalog.Count; i++) if (catalog[i].Key == key) return catalog[i];
        return null;
    }

    private static float GetCustomEffectDisplayValue(string field, CustomEffectChoice choice)
    {
        var draft = _customDraft!;
        if (field is "ratio" or "needs")
        {
            var source = field == "ratio" ? draft.CustomDamageRatio : draft.CustomNeeds;
            for (var i = 0; i < source.Count; i++) if (CustomAttriSpecKey(source[i].Kind, source[i].Index) == choice.Key) return source[i].Value;
            return 0f;
        }
        var spe = CustomSpeAddSpecs(field);
        for (var i = 0; i < spe.Count; i++) if (spe[i].TypeId == choice.TypeId) return choice.Percent ? spe[i].Value * 100f : spe[i].Value;
        return 0f;
    }

    private static void SetCustomEffectEntry(string field, CustomEffectChoice choice, float displayValue)
    {
        var draft = _customDraft!;
        SetCustomEffectUsesCustom(field, true);
        if (field is "ratio" or "needs")
        {
            var source = field == "ratio" ? draft.CustomDamageRatio : draft.CustomNeeds;
            var found = false;
            for (var i = 0; i < source.Count; i++)
            {
                if (CustomAttriSpecKey(source[i].Kind, source[i].Index) != choice.Key) continue;
                source[i].Value = displayValue;
                found = true;
                break;
            }
            if (!found) source.Add(new CustomAttriNumSpec { Kind = choice.Kind, Index = choice.Index, Value = displayValue });
        }
        else
        {
            var storedValue = choice.Percent ? displayValue / 100f : displayValue;
            var source = CustomSpeAddSpecs(field);
            var found = false;
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i].TypeId != choice.TypeId) continue;
                source[i].Value = storedValue;
                found = true;
                break;
            }
            if (!found) source.Add(new CustomSpeAddSpec { TypeId = choice.TypeId, Value = storedValue });
        }
        NormalizeCustomKungfuRecord(draft);
        _customStatus = $"已更新{CustomDonorFieldLabel(field)}词条：{choice.Label}；尚未保存。";
    }

    private static void RemoveCustomEffectEntry(string field, string key)
    {
        var draft = _customDraft!;
        if (field is "ratio" or "needs")
        {
            var source = field == "ratio" ? draft.CustomDamageRatio : draft.CustomNeeds;
            source.RemoveAll(value => CustomAttriSpecKey(value.Kind, value.Index) == key);
        }
        else
        {
            var source = CustomSpeAddSpecs(field);
            if (key.StartsWith("spe:", StringComparison.Ordinal) && int.TryParse(key[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                source.RemoveAll(value => value.TypeId == id);
        }
        _customStatus = $"已删除{CustomDonorFieldLabel(field)}中的该词条；尚未保存。";
    }

    private static void ClearCustomEffectEntries(string field)
    {
        var draft = _customDraft!;
        if (field == "ratio") draft.CustomDamageRatio.Clear();
        else if (field == "needs") draft.CustomNeeds.Clear();
        else CustomSpeAddSpecs(field).Clear();
        SetCustomEffectUsesCustom(field, true);
        _customStatus = $"已清空{CustomDonorFieldLabel(field)}的全部自选词条；尚未保存。";
    }

    private static string CustomKungfuDonorInlineSummary(string field)
    {
        try
        {
            if (CustomEffectUsesCustom(field))
            {
                var entries = GetCustomEffectCurrentChoices(field);
                if (entries.Count == 0) return "自选：无（该组明确为空）";
                var summary = string.Join("；", entries.ConvertAll(entry => entry.Label));
                if (summary.Length > 94) summary = summary[..91] + "……";
                return "自选：" + summary;
            }
            var skill = CustomDonorSkill(GetCustomDonor(field));
            if (skill == null) return "内容：来源不可用";
            var detail = CustomKungfuDonorPreviewDetail(skill, field, true)
                .Replace("\r", "")
                .Replace("\n", "；")
                .Trim('；', ' ');
            if (detail.Length > 118) detail = detail[..115] + "……";
            return "内容：" + detail;
        }
        catch (Exception ex)
        {
            return "内容读取失败：" + ex.Message;
        }
    }

    private static string DonorDisplay(int skillId)
    {
        try
        {
            var database = GameDataController.Instance;
            var skill = database == null ? null : OriginalKungfu(database, skillId);
            return skill == null ? $"不可用（ID {skillId}）" : $"{skill.name}（ID {skillId}）";
        }
        catch { return $"ID {skillId}"; }
    }

    private static void OpenCustomKungfuDonorPicker(string field)
    {
        SyncCustomKungfuDraftFromInputs();
        _customPickerField = field;
        _customPickerPage = 0;
        _customPickerSearch = "";
        _customPickerPreviewSkillId = GetCustomDonor(field);
        ShowCustomKungfuDonorPicker();
    }

    private static void ShowCustomKungfuDonorPicker()
    {
        InvalidateCustomKungfuPickerPages();
        var key = $"picker:{_customPickerField}:{_customPickerPage}:{_customPickerSearch}";
        ActivateCustomKungfuPage(key, BuildCustomKungfuDonorPicker);
    }

    private static void BuildCustomKungfuDonorPicker(Transform parent)
    {
        var choices = GetOriginalKungfuChoices(_customPickerSearch);
        const int pageSize = 8;
        var pageCount = Math.Max(1, (choices.Count + pageSize - 1) / pageSize);
        _customPickerPage = Math.Clamp(_customPickerPage, 0, pageCount - 1);
        AddText(parent, $"选择{CustomDonorFieldLabel(_customPickerField)}的来源功法", new Vector2(10, 8), new Vector2(650, 40), 22);
        AddText(parent, "单击左侧功法先预览，确认后才会采用。", new Vector2(700, 8), new Vector2(540, 40), 16, TextAnchor.MiddleCenter);
        _customPickerSearchInput = AddInput(parent, _customPickerSearch, new Vector2(10, 58), new Vector2(455, 38));
        AddCustomKungfuButton(parent, "搜索", new Vector2(475, 58), new Vector2(100, 38), () =>
        {
            _customPickerSearch = _customPickerSearchInput?.text?.Trim() ?? "";
            _customPickerPage = 0;
            ShowCustomKungfuDonorPicker();
        });
        AddCustomKungfuButton(parent, "返回当前页", new Vector2(585, 58), new Vector2(105, 38), () =>
        {
            _customPickerField = "";
            InvalidateCustomKungfuPickerPages();
            ShowCustomKungfuTab();
        });
        var start = _customPickerPage * pageSize;
        for (var i = 0; i < pageSize && start + i < choices.Count; i++)
        {
            var choice = choices[start + i];
            var captured = choice.Id;
            AddCustomKungfuButton(parent, $"{choice.Name}  [ID {choice.Id}]  类{choice.Type}/稀{choice.RareLv}", new Vector2(10, 110 + i * 56), new Vector2(680, 46), () =>
            {
                _customPickerPreviewSkillId = captured;
                UpdateCustomKungfuDonorPreview();
            });
        }
        AddCustomKungfuButton(parent, "上一页", new Vector2(10, 574), new Vector2(145, 38), () => { _customPickerPage = Math.Max(0, _customPickerPage - 1); ShowCustomKungfuDonorPicker(); });
        AddText(parent, $"第 {_customPickerPage + 1} / {pageCount} 页，共 {choices.Count} 项", new Vector2(165, 574), new Vector2(360, 38), 16, TextAnchor.MiddleCenter);
        AddCustomKungfuButton(parent, "下一页", new Vector2(535, 574), new Vector2(155, 38), () => { _customPickerPage = Math.Min(pageCount - 1, _customPickerPage + 1); ShowCustomKungfuDonorPicker(); });

        var preview = UiObject("DonorPreview", parent, new Vector2(540, 462), new Vector2(710, 108));
        var previewImage = preview.AddComponent<Image>();
        ApplySprite(previewImage, _paperSprite, true, new Color(1f, 1f, 1f, 0.96f), new Color(0.86f, 0.83f, 0.74f, 0.98f));
        var iconFrame = UiObject("DonorIconFrame", preview.transform, new Vector2(150, 150), new Vector2(18, 18));
        var iconFrameImage = iconFrame.AddComponent<Image>();
        ApplySprite(iconFrameImage, _frameSprite, true, Color.white, new Color(0.18f, 0.15f, 0.11f, 0.35f));
        var icon = UiObject("DonorIcon", iconFrame.transform, new Vector2(136, 136), new Vector2(7, 7));
        _customPickerPreviewIcon = icon.AddComponent<Image>();
        _customPickerPreviewIcon.preserveAspect = true;
        _customPickerPreviewTitle = AddText(preview.transform, "", new Vector2(182, 18), new Vector2(340, 150), 17, TextAnchor.UpperLeft);
        _customPickerPreviewDetail = AddText(preview.transform, "", new Vector2(18, 178), new Vector2(300, 266), 14, TextAnchor.UpperLeft);
        var animationFrame = UiObject("AnimationPreviewFrame", preview.transform, new Vector2(196, 258), new Vector2(326, 178));
        var animationFrameImage = animationFrame.AddComponent<Image>();
        ApplySprite(animationFrameImage, _frameSprite, true, new Color(1f, 1f, 1f, 0.85f), new Color(0.12f, 0.10f, 0.08f, 0.18f));
        animationFrameImage.raycastTarget = false;
        _customAnimationPreviewParent = animationFrame.transform;
        var canPreviewAnimation = _customPickerField is "animation" or "weapon" or "bullet" or "visual" or "icon";
        AddText(animationFrame.transform, canPreviewAnimation ? "动画预演区\n选择动作后点击下方按钮" : "该项是数据元素\n完整说明显示在左侧", new Vector2(8, 82), new Vector2(180, 92), 14, TextAnchor.MiddleCenter).raycastTarget = false;
        if (canPreviewAnimation)
            AddCustomKungfuButton(parent, "实时播放一次动作", new Vector2(710, 530), new Vector2(540, 40), PlayCustomKungfuAnimationPreview);
        AddCustomKungfuButton(parent, $"确认用于：{CustomDonorFieldLabel(_customPickerField)}", new Vector2(710, 584), new Vector2(540, 46), ConfirmCustomKungfuDonorPreview);
        UpdateCustomKungfuDonorPreview();
    }

    private static void InvalidateCustomKungfuPickerPages()
    {
        var keys = new List<string>();
        foreach (var pair in CustomPageRoots)
            if (pair.Key.StartsWith("picker:", StringComparison.Ordinal)) keys.Add(pair.Key);
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (CustomPageRoots.TryGetValue(key, out var root) && root != null) UnityEngine.Object.Destroy(root);
            CustomPageRoots.Remove(key);
            CustomPageClickCache.Remove(key);
        }
        CustomPageClicks.Clear();
        _customPickerPreviewIcon = null;
        _customPickerPreviewTitle = null;
        _customPickerPreviewDetail = null;
        _customAnimationPreviewParent = null;
        _customAnimationPreviewGraphic = null;
        _customAnimationPreviewAssetPointer = IntPtr.Zero;
    }

    private static void ConfirmCustomKungfuDonorPreview()
    {
        var database = GameDataController.Instance;
        if (database == null || OriginalKungfu(database, _customPickerPreviewSkillId) == null)
        {
            _customStatus = "请先从左侧选择一个可用的来源功法。";
            return;
        }
        var field = _customPickerField;
        SetCustomDonor(field, _customPickerPreviewSkillId);
        if (CustomEffectUsesCustom(field)) ImportCustomEffectFromDonor(field);
        _customStatus = $"已选择来源：{DonorDisplay(_customPickerPreviewSkillId)}；尚未保存。";
        _customPickerField = "";
        InvalidateCustomKungfuPages();
        ShowCustomKungfuTab();
    }

    private static void UpdateCustomKungfuDonorPreview()
    {
        if (_customPickerPreviewIcon == null || _customPickerPreviewTitle == null || _customPickerPreviewDetail == null) return;
        try
        {
            var database = GameDataController.Instance;
            var skill = database == null ? null : OriginalKungfu(database, _customPickerPreviewSkillId);
            if (skill == null)
            {
                _customPickerPreviewIcon.sprite = null;
                _customPickerPreviewIcon.color = Color.clear;
                _customPickerPreviewTitle.text = "请从左侧选择功法";
                _customPickerPreviewDetail.text = "选择后会显示图标、动作、弹道、特效和当前元素的详细数据。";
                return;
            }
            var iconName = skill.GetSkillIcon();
            try
            {
                var book = new ItemData(ItemType.Book).SetBookData(skill.skillID, skill.rareLv);
                var bookIcon = book?.GetItemIconName();
                if (!string.IsNullOrWhiteSpace(bookIcon)) iconName = bookIcon;
            }
            catch { }
            var sprite = GetItemSprite(iconName);
            _customPickerPreviewIcon.sprite = sprite;
            _customPickerPreviewIcon.color = sprite == null ? Color.clear : Color.white;
            _customPickerPreviewTitle.text = $"{skill.name}\nID {skill.skillID}　{skill.TypeDescribe()}\n稀有度：{RarityName(skill.rareLv)}\n图标：{iconName}";
            _customPickerPreviewDetail.text = CustomKungfuDonorPreviewDetail(skill, _customPickerField, sprite != null);
        }
        catch (Exception ex)
        {
            _customPickerPreviewDetail.text = "预览失败：" + ex.Message;
        }
    }

    private static void PlayCustomKungfuAnimationPreview()
    {
        try
        {
            var skill = CustomDonorSkill(_customPickerPreviewSkillId);
            if (skill == null || string.IsNullOrWhiteSpace(skill.animationName))
            {
                _customStatus = "该来源没有可播放的使用动作。";
                return;
            }
            if (_customAnimationPreviewParent == null)
            {
                _customStatus = "动画预演区尚未创建，请重新打开来源选择页。";
                return;
            }

            SkeletonDataAsset? asset = null;
            Material? material = null;
            var sourceGraphic = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<SkeletonGraphic>()) as SkeletonGraphic;
            if (sourceGraphic != null && sourceGraphic != _customAnimationPreviewGraphic)
            {
                asset = sourceGraphic.SkeletonDataAsset;
                material = sourceGraphic.material;
            }
            if (asset == null)
            {
                var sourceAnimation = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<SkeletonAnimation>()) as SkeletonAnimation;
                if (sourceAnimation != null)
                {
                    asset = sourceAnimation.SkeletonDataAsset;
                    material = sourceAnimation.GetComponent<MeshRenderer>()?.sharedMaterial;
                }
            }
            if (asset == null)
            {
                _customStatus = "当前建筑场景没有可复用的角色骨骼。切换到能显示动态人物的建筑或界面后再试；数据预览仍然有效。";
                return;
            }

            if (_customAnimationPreviewGraphic == null || _customAnimationPreviewAssetPointer != asset.Pointer)
            {
                if (_customAnimationPreviewGraphic != null) UnityEngine.Object.Destroy(_customAnimationPreviewGraphic.gameObject);
                _customAnimationPreviewGraphic = SkeletonGraphic.NewSkeletonGraphicGameObject(asset, _customAnimationPreviewParent, material);
                _customAnimationPreviewAssetPointer = asset.Pointer;
                _customAnimationPreviewGraphic.raycastTarget = false;
                var rect = _customAnimationPreviewGraphic.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.05f);
                rect.anchoredPosition = new Vector2(98f, 18f);
                rect.sizeDelta = new Vector2(190f, 235f);
                rect.localScale = new Vector3(0.7f, 0.7f, 1f);
                _customAnimationPreviewGraphic.Initialize(false);
            }

            var animation = _customAnimationPreviewGraphic.SkeletonData?.FindAnimation(skill.animationName);
            if (animation == null)
            {
                _customStatus = $"当前预览角色不包含动作“{skill.animationName}”。这表示该动作依赖另一套人物骨骼；仍可保存并在兼容角色上使用。";
                return;
            }
            _customAnimationPreviewGraphic.Skeleton.SetToSetupPose();
            _customAnimationPreviewGraphic.AnimationState.ClearTracks();
            _customAnimationPreviewGraphic.AnimationState.SetAnimation(0, skill.animationName, false);
            _customStatus = $"正在独立预演动作：{skill.animationName}。未调用技能结算、弹道或伤害逻辑。";
        }
        catch (Exception ex)
        {
            _customStatus = "动画预演失败但未影响草稿：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu animation preview skipped safely: {ex.Message}");
        }
    }

    private static string CustomKungfuDonorPreviewDetail(KungfuSkillData skill, string field, bool iconAvailable)
    {
        var text = new StringBuilder();
        if (!iconAvailable) text.AppendLine("图标资源未找到，但仍可查看数据并选择。\n");
        if (field is "animation" or "weapon" or "bullet" or "visual" or "icon")
        {
            text.AppendLine($"动作：{(string.IsNullOrWhiteSpace(skill.animationName) ? "无" : skill.animationName)}");
            text.AppendLine($"武器：{(string.IsNullOrWhiteSpace(skill.weaponName) ? "无" : skill.weaponName)}");
            var bullet = skill.skillBullet;
            text.AppendLine(bullet == null || string.IsNullOrWhiteSpace(bullet.bulletName)
                ? "弹道：无"
                : $"弹道：{bullet.bulletName}｜{bullet.bulletMoveType}｜速度 {bullet.bulletSpeed:0.###}｜缩放 {bullet.bulletScale:0.###}");
            var effects = skill.skillSpeEffects;
            if (effects == null || effects.Count == 0) text.AppendLine("场景特效：无");
            else
            {
                text.AppendLine($"场景特效（{effects.Count}）：");
                for (var i = 0; i < effects.Count && i < 6; i++)
                {
                    var effect = effects[i];
                    if (effect == null) continue;
                    text.AppendLine($"• {effect.speName}｜{LocalizeSkillEffectTarget(effect.speEffectTargetType)}｜{LocalizeSkillEffectTrigger(effect.triggerType)}{(effect.selfSpe ? "｜自身" : "")}");
                }
                if (effects.Count > 6) text.AppendLine($"……另有 {effects.Count - 6} 项");
            }
            text.AppendLine("右侧可用独立骨骼实时播放一次动作；不会驱动战斗角色，也不会触发伤害结算。若当前建筑场景没有兼容骨骼，会明确提示。");
        }
        else if (field == "attackRange") text.AppendLine("攻击范围：" + skill.GetAttackRangeDescribe());
        else if (field == "damageRange") text.AppendLine("伤害范围：" + skill.GetDamageRangeDescribe());
        else if (field == "attackPosture") text.AppendLine("进攻姿态：" + (skill.atkPartPosture?.GetSkillDescribe() ?? "无"));
        else if (field == "defensePosture") text.AppendLine("防御姿态：" + (skill.defPartPosture?.GetSkillDescribe() ?? "无"));
        else if (field == "upgrade") text.AppendLine("升级效果：\n" + LocalizeAffixDescription(skill.upgradeAddData?.GetDescribe(false, true, 2, false) ?? "无"));
        else if (field == "equip") text.AppendLine("装备效果：\n" + LocalizeAffixDescription(skill.equipAddData?.GetDescribe(false, true, 2, false) ?? "无"));
        else if (field == "use") text.AppendLine("使用效果：\n" + LocalizeAffixDescription(skill.useAddData?.GetDescribe(false, true, 2, false) ?? "无"));
        else if (field == "ratio") text.AppendLine("威力加成：\n" + (skill.addDamageRatio?.GetDamageRatioDescribe(1f) ?? "无"));
        else if (field == "needs")
        {
            var player = Player;
            text.AppendLine("修炼需求：\n" + (player == null ? "进入存档后显示角色对应的完整需求。" : skill.skillNeeds?.GetSkillNeedsDescribe(player) ?? "无"));
        }
        else
        {
            text.AppendLine($"基础伤害：{skill.baseDamage:0.###}");
            text.AppendLine($"内力消耗：{skill.manaCost:0.###}");
            text.AppendLine($"经验系数：{skill.expRatio:0.###}");
            text.AppendLine("攻击范围：" + skill.GetAttackRangeDescribe());
            text.AppendLine("伤害范围：" + skill.GetDamageRangeDescribe());
        }
        var result = text.ToString();
        return result.Length <= 900 ? result : result[..897] + "……";
    }

    private static string LocalizeSkillEffectTarget(SkillSpeEffectTargetType target) => target switch
    {
        SkillSpeEffectTargetType.Hip => "角色位置",
        SkillSpeEffectTargetType.Grid => "地格",
        SkillSpeEffectTargetType.Weapon => "武器",
        _ => "未知目标"
    };

    private static string LocalizeSkillEffectTrigger(SkillSpeEffectTriggerType trigger) => trigger switch
    {
        SkillSpeEffectTriggerType.Start => "开始时",
        SkillSpeEffectTriggerType.Happen => "生效时",
        _ => "未知时机"
    };

    private static List<SkillChoiceSnapshot> GetOriginalKungfuChoices(string search)
    {
        var result = new List<SkillChoiceSnapshot>();
        var database = GameDataController.Instance;
        if (database?.kungfuSkillDataBase == null) return result;
        foreach (var pair in database.kungfuSkillDataBase)
        {
            var skill = pair.Value;
            if (pair.Key >= CustomKungfuIdMin || skill == null || skill.hide) continue;
            var name = skill.name ?? $"功法 {pair.Key}";
            if (!string.IsNullOrWhiteSpace(search) && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && !pair.Key.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new SkillChoiceSnapshot { Id = pair.Key, Name = name, Type = skill.type, RareLv = skill.rareLv });
        }
        result.Sort((a, b) => a.Id.CompareTo(b.Id));
        return result;
    }

    private static string CustomDonorFieldLabel(string field) => field switch
    {
        "base" => "总母版", "ratio" => "威力加成", "needs" => "修炼需求", "upgrade" => "升级效果",
        "equip" => "装备效果", "use" => "使用效果", "attackRange" => "攻击范围", "damageRange" => "伤害范围",
        "attackPosture" => "进攻姿态", "defensePosture" => "防御姿态", "animation" => "使用动作", "weapon" => "显示武器",
        "bullet" => "弹道", "visual" => "场景特效", "icon" => "功法图标", _ => "元素"
    };

    private static int GetCustomDonor(string field)
    {
        var draft = _customDraft!;
        return field switch
        {
            "base" => draft.BaseDonorId, "ratio" => draft.DamageRatioDonorId, "needs" => draft.NeedsDonorId,
            "upgrade" => draft.UpgradeDonorId, "equip" => draft.EquipDonorId, "use" => draft.UseDonorId,
            "attackRange" => draft.AttackRangeDonorId, "damageRange" => draft.DamageRangeDonorId,
            "attackPosture" => draft.AttackPostureDonorId, "defensePosture" => draft.DefensePostureDonorId,
            "animation" => draft.AnimationDonorId, "weapon" => draft.WeaponDonorId, "bullet" => draft.BulletDonorId,
            "visual" => draft.VisualEffectDonorId, "icon" => draft.IconDonorId, _ => draft.BaseDonorId
        };
    }

    private static void SetCustomDonor(string field, int value)
    {
        var draft = _customDraft!;
        switch (field)
        {
            case "base": draft.BaseDonorId = value; break;
            case "ratio": draft.DamageRatioDonorId = value; break;
            case "needs": draft.NeedsDonorId = value; break;
            case "upgrade": draft.UpgradeDonorId = value; break;
            case "equip": draft.EquipDonorId = value; break;
            case "use": draft.UseDonorId = value; break;
            case "attackRange": draft.AttackRangeDonorId = value; break;
            case "damageRange": draft.DamageRangeDonorId = value; break;
            case "attackPosture": draft.AttackPostureDonorId = value; break;
            case "defensePosture": draft.DefensePostureDonorId = value; break;
            case "animation": draft.AnimationDonorId = value; break;
            case "weapon": draft.WeaponDonorId = value; break;
            case "bullet": draft.BulletDonorId = value; break;
            case "visual": draft.VisualEffectDonorId = value; break;
            case "icon": draft.IconDonorId = value; draft.CustomIconFile = ""; break;
        }
    }

    private static void ApplyFullKungfuDonor(int donorId, bool copyScalars)
    {
        var draft = _customDraft!;
        draft.DamageRatioDonorId = donorId;
        draft.NeedsDonorId = donorId;
        draft.UpgradeDonorId = donorId;
        draft.EquipDonorId = donorId;
        draft.UseDonorId = donorId;
        draft.AttackRangeDonorId = donorId;
        draft.DamageRangeDonorId = donorId;
        draft.AttackPostureDonorId = donorId;
        draft.DefensePostureDonorId = donorId;
        draft.AnimationDonorId = donorId;
        draft.WeaponDonorId = donorId;
        draft.BulletDonorId = donorId;
        draft.VisualEffectDonorId = donorId;
        draft.IconDonorId = donorId;
        draft.CustomIconFile = "";
        draft.UseCustomDamageRatio = false;
        draft.UseCustomNeeds = false;
        draft.UseCustomUpgrade = false;
        draft.UseCustomEquip = false;
        draft.UseCustomUse = false;
        if (!copyScalars) return;
        var database = GameDataController.Instance;
        var donor = database == null ? null : OriginalKungfu(database, donorId);
        if (donor == null) return;
        draft.Type = donor.type;
        draft.RareLv = donor.rareLv;
        draft.ForceId = donor.belongForceID;
        draft.TargetType = (int)donor.targetType;
        draft.ManaCost = donor.manaCost;
        draft.BaseDamage = donor.baseDamage;
        draft.ExpRatio = donor.expRatio;
        draft.CooldownTime = -1f;
        draft.BattleMaxUseTime = donor.battleMaxUseTime;
        draft.DamageOrder = (int)donor.skillDamageOrder;
        draft.AutoMove = donor.autoHeroMove;
        draft.TrailId = donor.trailID;
        draft.UseCustomAttackRange = false;
        draft.UseCustomDamageRange = false;
        draft.UseCustomAttackPosture = false;
        draft.UseCustomDefensePosture = false;
        SeedCustomRangeAndPosture(draft, donor);
    }

    private static void SyncCustomKungfuDraftFromInputs()
    {
        var draft = _customDraft;
        if (draft == null) return;
        string Text(string key, string fallback) => CustomInputs.TryGetValue(key, out var field) && field != null ? field.text : fallback;
        int IntValue(string key, int fallback) => int.TryParse(Text(key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        float FloatValue(string key, float fallback)
        {
            if (!float.TryParse(Text(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value)) return fallback;
            return value;
        }
        draft.Name = Text("name", draft.Name).Trim();
        draft.Description = Text("description", draft.Description);
        draft.Type = Math.Max(0, IntValue("type", draft.Type));
        draft.RareLv = Math.Max(0, IntValue("rare", draft.RareLv));
        draft.ForceId = IntValue("force", draft.ForceId);
        draft.TargetType = Math.Max(0, IntValue("target", draft.TargetType));
        draft.BaseDamage = Math.Max(0f, FloatValue("damage", draft.BaseDamage));
        draft.ManaCost = Math.Max(0f, FloatValue("mana", draft.ManaCost));
        draft.ExpRatio = Math.Max(0.01f, FloatValue("exp", draft.ExpRatio));
        draft.CooldownTime = Math.Max(-1f, FloatValue("cooldown", draft.CooldownTime));
        draft.BattleMaxUseTime = Math.Max(0, IntValue("uses", draft.BattleMaxUseTime));
        draft.DamageOrder = Math.Max(0, IntValue("order", draft.DamageOrder));
        draft.TrailId = Math.Max(0, IntValue("trail", draft.TrailId));
        NormalizeCustomKungfuRecord(draft);
        for (var i = 0; i < draft.CustomAttackRanges.Count; i++)
        {
            var range = draft.CustomAttackRanges[i];
            range.Min = Math.Max(0, IntValue($"attackMin{i}", range.Min));
            range.Max = Math.Max(range.Min, IntValue($"attackMax{i}", range.Max));
        }
        draft.CustomDamageRangeMin = Math.Max(0, IntValue("damageRangeMin", draft.CustomDamageRangeMin));
        draft.CustomDamageRangeMax = Math.Max(draft.CustomDamageRangeMin, IntValue("damageRangeMax", draft.CustomDamageRangeMax));
        for (var i = 0; i < 6; i++)
        {
            draft.CustomAttackPosture[i] = FloatValue($"attackPosture{i}", draft.CustomAttackPosture[i]);
            draft.CustomDefensePosture[i] = FloatValue($"defensePosture{i}", draft.CustomDefensePosture[i]);
        }
    }

    private static void BuildCustomKungfuManagePage(Transform parent)
    {
        var draft = _customDraft!;
        AddText(parent, "保存与管理", new Vector2(10, 8), new Vector2(700, 40), 22);
        AddText(parent, $"当前草稿：{draft.Name}　ID {(draft.Id == 0 ? "未分配" : draft.Id.ToString(CultureInfo.InvariantCulture))}　母版 {draft.BaseDonorId}", new Vector2(10, 50), new Vector2(1220, 36), 17);
        AddText(parent, "已保存的自创功法", new Vector2(10, 102), new Vector2(560, 36), 19, TextAnchor.MiddleCenter);

        const int pageSize = 6;
        var pageCount = Math.Max(1, (_customKungfuStore.Skills.Count + pageSize - 1) / pageSize);
        _customManagePage = Math.Clamp(_customManagePage, 0, pageCount - 1);
        var start = _customManagePage * pageSize;
        for (var i = 0; i < pageSize && start + i < _customKungfuStore.Skills.Count; i++)
        {
            var saved = _customKungfuStore.Skills[start + i];
            var captured = saved.Id;
            AddCustomKungfuButton(parent, $"{saved.Name}　[ID {saved.Id}]", new Vector2(10, 150 + i * 62), new Vector2(560, 48), () =>
            {
                var record = FindSavedCustomKungfu(captured);
                if (record == null) return;
                _customDraft = CloneCustomKungfuRecord(record);
                _customStatus = $"已载入 {record.Name}；修改后需再次保存。";
                _customTab = 0;
                InvalidateCustomKungfuPages();
                ShowCustomKungfuTab();
            });
        }
        AddCustomKungfuButton(parent, "上一页", new Vector2(10, 540), new Vector2(150, 40), () =>
        {
            _customManagePage = Math.Max(0, _customManagePage - 1);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, $"第 {_customManagePage + 1} / {pageCount} 页", new Vector2(170, 540), new Vector2(220, 40), 16, TextAnchor.MiddleCenter);
        AddCustomKungfuButton(parent, "下一页", new Vector2(400, 540), new Vector2(170, 40), () =>
        {
            _customManagePage = Math.Min(pageCount - 1, _customManagePage + 1);
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });

        AddText(parent, "写入操作", new Vector2(650, 102), new Vector2(570, 36), 19, TextAnchor.MiddleCenter);
        AddCustomKungfuButton(parent, "仅保存定义", new Vector2(650, 160), new Vector2(570, 52), () => SaveCustomKungfuDraft(false, false));
        AddCustomKungfuButton(parent, "保存并让主角习得", new Vector2(650, 230), new Vector2(570, 52), () => SaveCustomKungfuDraft(true, false));
        AddCustomKungfuButton(parent, "保存并生成一本秘籍", new Vector2(650, 300), new Vector2(570, 52), () => SaveCustomKungfuDraft(false, true));
        AddCustomKungfuButton(parent, "新建空白草稿", new Vector2(650, 390), new Vector2(570, 48), () =>
        {
            _customDraft = NewCustomKungfuDraft();
            _customStatus = "已新建草稿；尚未保存。";
            _customTab = 0;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        });
        AddText(parent, "安全说明：定义保存在 BepInEx/config，并在每次保存前自动备份 .bak。游戏存档只记录技能 ID；停用模组前不要保留自创技能或秘籍。", new Vector2(650, 470), new Vector2(570, 92), 16);
        AddText(parent, "当前版本不提供直接删除按钮，避免存档中残留未知技能 ID。", new Vector2(650, 575), new Vector2(570, 42), 16);
    }

    private static CustomKungfuRecord? FindSavedCustomKungfu(int id)
    {
        for (var i = 0; i < _customKungfuStore.Skills.Count; i++)
            if (_customKungfuStore.Skills[i].Id == id) return _customKungfuStore.Skills[i];
        return null;
    }

    private static void SaveCustomKungfuDraft(bool grantToPlayer, bool createBook)
    {
        SyncCustomKungfuDraftFromInputs();
        if (_customDraft == null) return;
        if (_customKungfuStoreLoadFailed)
        {
            _customStatus = "配置文件此前读取失败，为保护原文件，本次禁止保存。";
            return;
        }
        try
        {
            var database = GameDataController.Instance ?? throw new InvalidOperationException("功法数据库尚未载入");
            var record = CloneCustomKungfuRecord(_customDraft);
            record.Name = record.Name.Trim();
            if (string.IsNullOrWhiteSpace(record.Name)) throw new InvalidOperationException("功法名称不能为空");
            if (record.Id == 0) record.Id = AllocateCustomKungfuId(database);
            var resolvedBaseDonorId = ResolveCustomKungfuBaseDonor(database, record);
            if (resolvedBaseDonorId < 0) throw new InvalidOperationException($"总母版 ID {record.BaseDonorId} 无效，且没有可回退的来源功法");
            record.BaseDonorId = resolvedBaseDonorId;
            ValidateCustomKungfuDonors(database, record);
            if (BuildCustomKungfuDefinition(database, record) == null) throw new InvalidOperationException("无法根据当前元素组合生成定义");

            var candidate = new CustomKungfuStore
            {
                Schema = 5,
                NextId = Math.Max(_customKungfuStore.NextId, record.Id < CustomKungfuIdMax ? record.Id + 1 : CustomKungfuIdMax),
                Skills = new List<CustomKungfuRecord>()
            };
            var replaced = false;
            for (var i = 0; i < _customKungfuStore.Skills.Count; i++)
            {
                if (_customKungfuStore.Skills[i].Id == record.Id)
                {
                    candidate.Skills.Add(CloneCustomKungfuRecord(record));
                    replaced = true;
                }
                else candidate.Skills.Add(CloneCustomKungfuRecord(_customKungfuStore.Skills[i]));
            }
            if (!replaced) candidate.Skills.Add(CloneCustomKungfuRecord(record));
            candidate.Skills.Sort((a, b) => a.Id.CompareTo(b.Id));
            WriteCustomKungfuStore(candidate);
            _customKungfuStore = candidate;
            _customDraft = CloneCustomKungfuRecord(record);
            _customKungfuRegisteredDatabasePointer = IntPtr.Zero;
            RegisterCustomKungfuDefinitions();
            RefreshLearnedCustomKungfuInstances(record.Id);
            Catalog.Clear();

            var followUp = "";
            if (grantToPlayer)
            {
                var player = Player ?? throw new InvalidOperationException("定义已保存，但主角存档尚未载入，无法习得");
                var existing = player.FindSkill(record.Id);
                if (existing == null) GrantCustomKungfuToPlayer(player, record.Id);
                followUp = existing == null ? "，主角已习得" : "，主角原本已习得";
            }
            if (createBook)
            {
                var player = Player ?? throw new InvalidOperationException("定义已保存，但主角存档尚未载入，无法生成秘籍");
                var book = new ItemData(ItemType.Book).SetBookData(record.Id, record.RareLv);
                if (book == null) throw new InvalidOperationException("定义已保存，但秘籍生成失败");
                player.GetItem(book, false, false, 0, true);
                followUp = "，秘籍已放入主角背包";
            }
            _customStatus = $"已保存 {record.Name}（ID {record.Id}）{followUp}。";
            _customTab = 4;
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
            LongYinTrainerPlugin.Logger.LogInfo($"Custom kungfu saved safely: id={record.Id}, name={record.Name}, grant={grantToPlayer}, book={createBook}.");
        }
        catch (Exception ex)
        {
            _customStatus = "保存失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Custom kungfu save failed safely: {ex}");
        }
    }

    private static int AllocateCustomKungfuId(GameDataController database)
    {
        var start = Math.Clamp(_customKungfuStore.NextId, CustomKungfuIdMin, CustomKungfuIdMax);
        for (var id = start; id <= CustomKungfuIdMax; id++)
        {
            if (FindSavedCustomKungfu(id) != null) continue;
            if (database.kungfuSkillDataBase != null && database.kungfuSkillDataBase.ContainsKey(id)) continue;
            return id;
        }
        throw new InvalidOperationException("自创功法保留 ID 区间已经用完");
    }

    private static void ValidateCustomKungfuDonors(GameDataController database, CustomKungfuRecord record)
    {
        var donors = new[]
        {
            record.BaseDonorId, record.DamageRatioDonorId, record.NeedsDonorId, record.UpgradeDonorId, record.EquipDonorId,
            record.UseDonorId, record.AttackRangeDonorId, record.DamageRangeDonorId, record.AttackPostureDonorId,
            record.DefensePostureDonorId, record.AnimationDonorId, record.WeaponDonorId, record.BulletDonorId,
            record.VisualEffectDonorId, record.IconDonorId
        };
        for (var i = 0; i < donors.Length; i++)
            if (OriginalKungfu(database, donors[i]) == null) throw new InvalidOperationException($"来源功法 ID {donors[i]} 不可用");
        if (!float.IsFinite(record.BaseDamage) || !float.IsFinite(record.ManaCost) || !float.IsFinite(record.ExpRatio))
            throw new InvalidOperationException("数值中包含无效浮点数");
    }

    private static void WriteCustomKungfuStore(CustomKungfuStore store)
    {
        if (string.IsNullOrWhiteSpace(_customKungfuStorePath)) InitializeCustomKungfuStorage();
        var directory = Path.GetDirectoryName(_customKungfuStorePath) ?? throw new InvalidOperationException("配置目录无效");
        Directory.CreateDirectory(directory);
        var temp = _customKungfuStorePath + ".tmp";
        var backup = _customKungfuStorePath + ".bak";
        var json = JsonSerializer.Serialize(store, CustomKungfuJsonOptions);
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        if (File.Exists(_customKungfuStorePath)) File.Copy(_customKungfuStorePath, backup, true);
        File.Move(temp, _customKungfuStorePath, true);
    }

    private static void EnsureNativeUi()
    {
        if (_uiRoot != null) return;
        try
        {
            var gameText = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<Text>()) as Text;
            ResolveNativeTheme();
            _uiFont ??= gameText?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            _uiRoot = new GameObject("CodexLongYinTrainerCanvas", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Canvas>(), Il2CppType.Of<CanvasScaler>(), Il2CppType.Of<GraphicRaycaster>());
            UnityEngine.Object.DontDestroyOnLoad(_uiRoot);
            var canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = _uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = UiObject("Panel", _uiRoot.transform, new Vector2(1120, 820), new Vector2(42, 32));
            _uiPanelRect = panel.GetComponent<RectTransform>();
            var panelImage = panel.AddComponent<Image>();
            ApplySprite(panelImage, _paperSprite, true, new Color(1f, 1f, 1f, 0.985f), new Color(0.93f, 0.90f, 0.80f, 0.985f));

            var border = UiObject("NativeFrame", panel.transform, new Vector2(1120, 820), Vector2.zero);
            var borderImage = border.AddComponent<Image>();
            ApplySprite(borderImage, _frameSprite, true, new Color(0.19f, 0.17f, 0.13f, 0.82f), new Color(0.19f, 0.17f, 0.13f, 0.25f));
            borderImage.raycastTarget = false;

            if (_inkSprite != null)
            {
                var ink = UiObject("InkDecoration", panel.transform, new Vector2(180, 120), new Vector2(10, 686));
                var inkImage = ink.AddComponent<Image>();
                inkImage.sprite = _inkSprite;
                inkImage.color = new Color(0.08f, 0.07f, 0.055f, 0.16f);
                inkImage.preserveAspect = true;
                inkImage.raycastTarget = false;
            }

            var titleBar = UiObject("TitleBar", panel.transform, new Vector2(1080, 36), new Vector2(18, 10));
            _uiTitleRect = titleBar.GetComponent<RectTransform>();
            var titleImage = titleBar.AddComponent<Image>();
            ApplySprite(titleImage, _tabSprite ?? _buttonSprite, true, Color.white, new Color(0.075f, 0.065f, 0.055f, 0.98f));
            var titleText = AddText(titleBar.transform, "江湖札记", new Vector2(16, 0), new Vector2(400, 36), 22, TextAnchor.MiddleLeft);
            titleText.color = new Color(0.93f, 0.67f, 0.16f, 1f);
            var closeText = AddText(titleBar.transform, "H  收起", new Vector2(910, 0), new Vector2(140, 36), 16, TextAnchor.MiddleRight);
            closeText.color = new Color(0.88f, 0.82f, 0.68f, 1f);

            var tabNames = new[] { "战斗辅助", "角色数值", "背包物品", "物品修改", "打造／炼丹", "势力／开局", "更多功能", "突破自选", "城市属性" };
            var tabGap = 3f;
            var tabWidth = (1080f - tabGap * (tabNames.Length - 1)) / tabNames.Length;
            for (var i = 0; i < tabNames.Length; i++)
            {
                var captured = i;
                TabButtons.Add(AddButton(panel.transform, tabNames[i], new Vector2(18 + i * (tabWidth + tabGap), 50), new Vector2(tabWidth, 42), () => SelectNativeTab(captured), true));
            }

            var content = UiObject("Content", panel.transform, new Vector2(1080, 654), new Vector2(18, 100));
            _uiContent = content.GetComponent<RectTransform>();
            var contentWash = content.AddComponent<Image>();
            ApplySprite(contentWash, _paperSprite, true, new Color(1f, 1f, 1f, 0.42f), Color.clear);
            contentWash.raycastTarget = false;

            var statusBar = UiObject("StatusBar", panel.transform, new Vector2(1080, 38), new Vector2(18, 764));
            var statusImage = statusBar.AddComponent<Image>();
            ApplySprite(statusImage, _tabSprite ?? _buttonSprite, true, Color.white, new Color(0.13f, 0.11f, 0.085f, 0.96f));
            _uiStatus = AddText(statusBar.transform, _status, new Vector2(12, 0), new Vector2(1056, 36), 17, TextAnchor.MiddleLeft);
            _uiStatus.color = new Color(0.92f, 0.77f, 0.38f, 1f);
            _pageDirty = true;
            _pageClearedForRebuild = false;
            _uiRoot.SetActive(_visible);
            LongYinTrainerPlugin.Logger.LogInfo("Native Canvas trainer UI created.");
        }
        catch (Exception ex)
        {
            _status = "界面创建失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogError($"Native UI creation failed: {ex}");
            if (_uiRoot != null) UnityEngine.Object.Destroy(_uiRoot);
            ResetNativePageCache();
            _uiRoot = null;
        }
    }

    private static void ResolveNativeTheme()
    {
        if (!RuntimeAssetDiscoveryEnabled) return;
        try
        {
            var roots = new List<GameObject>();
            // Do not inspect HeroDetailController or its item grid here.  Those
            // IL2CPP objects are rebuilt while opening/switching a hero bag;
            // traversing them from an overlay update can race native teardown
            // and produce an access violation in GameAssembly.dll.  Theme
            // assets below are discovered only from Unity's loaded resources.

            _paperSprite ??= FindNativeSprite(roots, "通用白色纸块", "LightBackground", "Background");
            _buttonSprite ??= FindNativeSprite(roots, "按钮", "标签内容条(九宫格)", "拉伸框");
            _tabSprite ??= FindNativeSprite(roots, "标签底", "标签底条", "DarkBackground");
            _tabSelectedSprite ??= FindNativeSprite(roots, "标签选中", "标签变色渐变条（白色）");
            _inputSprite ??= FindNativeSprite(roots, "下拉菜单框", "通用白色纸块", "LightBackground");
            _frameSprite ??= FindNativeSprite(roots, "黑色外框", "游戏模式_框", "NoBackgroundDarkSideBorder");
            _checkSprite ??= FindNativeSprite(roots, "Checkmark", "家族身世_选中框");
            _pageArrowSprite ??= FindNativeSprite(roots, "进度条_左右按钮", "下拉菜单_按钮");
            _inkSprite ??= FindNativeSprite(roots, "墨迹");
            _addSprite ??= FindNativeSprite(roots, "潜力_加号");
            _deleteSprite ??= FindNativeSprite(roots, "探索_垃圾");
            _confirmSprite ??= FindNativeSprite(roots, "战斗操作_自动设置弹出选择勾", "Checkmark");

            _nativeThemeScore = (_paperSprite != null ? 1 : 0) + (_buttonSprite != null ? 1 : 0) +
                (_tabSprite != null ? 1 : 0) + (_tabSelectedSprite != null ? 1 : 0) +
                (_inputSprite != null ? 1 : 0) + (_frameSprite != null ? 1 : 0) +
                (_checkSprite != null ? 1 : 0) + (_pageArrowSprite != null ? 1 : 0) +
                (_inkSprite != null ? 1 : 0);

            if (!_nativeThemeLogged)
            {
                _nativeThemeLogged = true;
                LongYinTrainerPlugin.Logger.LogInfo($"Native theme: paper={_paperSprite != null}, button={_buttonSprite != null}, tab={_tabSprite != null}, selected={_tabSelectedSprite != null}, input={_inputSprite != null}, frame={_frameSprite != null}, check={_checkSprite != null}, arrow={_pageArrowSprite != null}, font={_uiFont != null}.");
            }
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogWarning($"Native theme discovery skipped: {ex.Message}");
        }
    }

    private static Sprite? FindNativeSprite(List<GameObject> roots, params string[] names)
    {
        var loaded = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>());
        for (var nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            for (var spriteIndex = 0; spriteIndex < loaded.Length; spriteIndex++)
            {
                var sprite = loaded[spriteIndex] as Sprite;
                if (sprite != null && string.Equals(sprite.name, names[nameIndex], StringComparison.Ordinal)) return sprite;
            }
        }
        // NGUI stores most of the game's visible UI as named regions inside
        // ScriptableObject atlases rather than standalone Unity Sprites.  The
        // atlas assets are safe to read and are independent of the live bag
        // hierarchy/controllers.
        for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            var widgets = roots[rootIndex].GetComponentsInChildren<UISprite>(true);
            for (var widgetIndex = 0; widgetIndex < widgets.Length; widgetIndex++)
            {
                var widget = widgets[widgetIndex];
                if (widget?.atlas == null) continue;
                for (var nameIndex = 0; nameIndex < names.Length; nameIndex++)
                {
                    var data = widget.GetSprite(names[nameIndex]);
                    var texture = widget.atlas.texture as Texture2D;
                    if (data == null || texture == null || data.width <= 0 || data.height <= 0) continue;
                    var rect = new Rect(data.x, texture.height - data.y - data.height, data.width, data.height);
                    var border = new Vector4(data.borderLeft, data.borderBottom, data.borderRight, data.borderTop);
                    var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                    sprite.name = "TrainerNative_" + names[nameIndex];
                    return sprite;
                }
            }
        }
        return null;
    }

    private static void RecreateNativeUi()
    {
        if (_uiRoot != null) UnityEngine.Object.Destroy(_uiRoot);
        ResetNativePageCache();
        _uiRoot = null;
        _uiContent = null;
        _uiPanelRect = null;
        _uiTitleRect = null;
        _uiStatus = null;
        TabClicks.Clear();
        PageClicks.Clear();
        TabButtons.Clear();
        EnsureNativeUi();
    }

    private static void ApplySprite(Image image, Sprite? sprite, bool sliced, Color nativeColor, Color fallbackColor)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sliced && sprite.border.sqrMagnitude > 0.01f ? Image.Type.Sliced : Image.Type.Simple;
            image.color = nativeColor;
        }
        else image.color = fallbackColor;
    }

    private static GameObject UiObject(string name, Transform parent, Vector2 size, Vector2 topLeft)
    {
        var go = new GameObject(name, Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        return go;
    }

    private static Text AddText(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize = 18, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var go = UiObject("Text", parent, size, pos);
        var text = go.AddComponent<Text>();
        text.font = _uiFont;
        text.fontSize = fontSize;
        text.text = value;
        text.color = new Color(0.12f, 0.105f, 0.085f, 1f);
        text.alignment = anchor;
        text.supportRichText = true;
        text.raycastTarget = false;
        return text;
    }

    private static Button AddButton(Transform parent, string label, Vector2 pos, Vector2 size, Action action, bool permanent = false)
    {
        var go = UiObject("Button_" + label, parent, size, pos);
        var image = go.AddComponent<Image>();
        ApplySprite(image, _buttonSprite, true, Color.white, new Color(0.15f, 0.13f, 0.105f, 0.98f));
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.48f, 1f);
        colors.pressedColor = new Color(0.78f, 0.58f, 0.22f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        var semanticIcon = ButtonIcon(label);
        var iconSpace = semanticIcon != null ? 28f : 0f;
        if (semanticIcon != null)
        {
            var iconObject = UiObject("NativeButtonIcon", go.transform, new Vector2(22, 22), new Vector2(9, Math.Max(0f, (size.y - 22f) / 2f)));
            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = semanticIcon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            if (label.Contains("下一页", StringComparison.Ordinal)) iconObject.GetComponent<RectTransform>().localScale = new Vector3(-1f, 1f, 1f);
        }
        var buttonText = AddText(go.transform, label, new Vector2(iconSpace, 0), new Vector2(size.x - iconSpace, size.y), 17, TextAnchor.MiddleCenter);
        buttonText.color = new Color(0.96f, 0.92f, 0.80f, 1f);
        (permanent ? TabClicks : PageClicks).Add(new UiClick { Rect = go.GetComponent<RectTransform>(), Action = action });
        return button;
    }

    private static Sprite? ButtonIcon(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        if (label.Contains("上一页", StringComparison.Ordinal) || label.Contains("下一页", StringComparison.Ordinal)) return _pageArrowSprite;
        if (label.Contains("删除", StringComparison.Ordinal) || label.Contains("清空", StringComparison.Ordinal)) return _deleteSprite;
        if (label.Contains("添加", StringComparison.Ordinal)) return _addSprite;
        if (label.Contains("应用", StringComparison.Ordinal) || label.Contains("写入", StringComparison.Ordinal) || label.Contains("设置", StringComparison.Ordinal)) return _confirmSprite;
        return null;
    }

    private static void HandleNativeClicks()
    {
        if (_loadingOverlay != null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        for (var i = PageClicks.Count - 1; i >= 0; i--)
        {
            var click = PageClicks[i];
            if (click.Rect != null && click.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(click.Rect, Input.mousePosition, null))
            {
                click.Action();
                return;
            }
        }
        for (var i = TabClicks.Count - 1; i >= 0; i--)
        {
            var click = TabClicks[i];
            if (click.Rect != null && click.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(click.Rect, Input.mousePosition, null))
            {
                click.Action();
                return;
            }
        }
    }

    private static void HandleNativeDrag()
    {
        if (_uiPanelRect == null || _uiTitleRect == null) return;
        var mouseDown = IsGameForeground() && (GetAsyncKeyState(0x01) & 0x8000) != 0;
        if (mouseDown && !_nativeMouseLatch && GetCursorPos(out var downPoint))
        {
            var unityPoint = new Vector2(downPoint.X, Screen.height - downPoint.Y);
            if (RectTransformUtility.RectangleContainsScreenPoint(_uiTitleRect, unityPoint, null))
            {
                _dragging = true;
                _lastDragMouse = downPoint;
            }
        }
        _nativeMouseLatch = mouseDown;
        if (!mouseDown) { _dragging = false; return; }
        if (!_dragging || !GetCursorPos(out var now)) return;
        var scale = _uiRoot?.GetComponent<Canvas>()?.scaleFactor ?? 1f;
        if (scale <= 0.001f) scale = 1f;
        var delta = new Vector2(now.X - _lastDragMouse.X, _lastDragMouse.Y - now.Y) / scale;
        _uiPanelRect.anchoredPosition += delta;
        _lastDragMouse = now;
    }

    private static InputField AddInput(Transform parent, string value, Vector2 pos, Vector2 size)
    {
        var go = UiObject("Input", parent, size, pos);
        var image = go.AddComponent<Image>();
        ApplySprite(image, _inputSprite, true, Color.white, new Color(0.985f, 0.97f, 0.90f, 1f));
        var field = go.AddComponent<InputField>();
        field.targetGraphic = image;
        var text = AddText(go.transform, value, new Vector2(8, 0), new Vector2(size.x - 16, size.y), 17, TextAnchor.MiddleLeft);
        field.textComponent = text;
        field.text = value;
        return field;
    }

    private static void AddNativeField(Transform parent, string label, string value, float x, float y, Dictionary<string, InputField> fields, string key)
    {
        AddText(parent, label, new Vector2(x, y), new Vector2(125, 32));
        fields[key] = AddInput(parent, value, new Vector2(x + 125, y), new Vector2(180, 32));
    }

    private static void AddNativeToggle(Transform parent, string label, float x, float y, Func<bool> get, Action<bool> set)
    {
        var row = UiObject("Toggle_" + label, parent, new Vector2(480, 38), new Vector2(x, y));
        var rowImage = row.AddComponent<Image>();
        ApplySprite(rowImage, _paperSprite, true, new Color(1f, 1f, 1f, 0.72f), new Color(0.84f, 0.81f, 0.72f, 0.92f));
        var labelText = AddText(row.transform, label, new Vector2(14, 0), new Vector2(405, 38), 17, TextAnchor.MiddleLeft);
        var box = UiObject("CheckBox", row.transform, new Vector2(28, 28), new Vector2(438, 5));
        var boxImage = box.AddComponent<Image>();
        ApplySprite(boxImage, _inputSprite, true, Color.white, new Color(0.96f, 0.94f, 0.86f, 1f));
        var check = UiObject("Checkmark", box.transform, new Vector2(22, 22), new Vector2(3, 3));
        var checkImage = check.AddComponent<Image>();
        ApplySprite(checkImage, _checkSprite, false, new Color(0.08f, 0.64f, 0.28f, 1f), new Color(0.08f, 0.64f, 0.28f, 1f));
        checkImage.raycastTarget = false;
        void Refresh()
        {
            var enabled = get();
            check.SetActive(enabled);
            labelText.color = enabled ? new Color(0.54f, 0.10f, 0.07f, 1f) : new Color(0.12f, 0.105f, 0.085f, 1f);
        }
        PageClicks.Add(new UiClick { Rect = row.GetComponent<RectTransform>(), Action = () => { set(!get()); Refresh(); } });
        Refresh();
    }

    private static Transform NativePageParent => (_activePageContent ?? _uiContent)?.transform
        ?? throw new InvalidOperationException("Native page container is not available.");

    private static void ResetNativePageCache()
    {
        _activePageContent = null;
        _catalogPreviewText = null;
        _catalogLevelInput = null;
        _catalogRarityInput = null;
        PageClicks.Clear();
        PageRoots.Clear();
        PageClickCache.Clear();
        DirtyPages.Clear();
        _pageDirty = false;
        _pageClearedForRebuild = false;
    }

    private static void ClearNativePage(int tab)
    {
        if (tab == 2)
        {
            _catalogPreviewText = null;
            _catalogLevelInput = null;
            _catalogRarityInput = null;
        }
        PageClicks.Clear();
        PageClickCache.Remove(tab);
        if (PageRoots.TryGetValue(tab, out var page))
        {
            PageRoots.Remove(tab);
            if (page != null)
            {
                page.SetActive(false);
                UnityEngine.Object.Destroy(page);
            }
        }
    }

    private static void SelectNativeTab(int tab)
    {
        if (tab < 0 || tab >= TabButtons.Count) return;
        if (tab == _tab)
        {
            // Clicking the active tab is the explicit refresh gesture used by
            // the equipment/affix pages.
            if (tab != 2) RequestPageRebuild();
            return;
        }
        if (PageRoots.TryGetValue(_tab, out var current) && current != null) current.SetActive(false);
        _tab = tab;
        _pageDirty = true;
        _pageClearedForRebuild = false;
    }

    private static void InvalidatePages(params int[] tabs)
    {
        for (var i = 0; i < tabs.Length; i++)
        {
            DirtyPages.Add(tabs[i]);
            if (tabs[i] == _tab) _pageDirty = true;
        }
    }

    private static void RequestPageRebuild()
    {
        InvalidatePages(_tab);
    }

    private static void ProcessPageRebuild()
    {
        if (!_pageDirty || _uiContent == null) return;
        if ((!PageRoots.ContainsKey(_tab) || DirtyPages.Contains(_tab)) && !PreparePageLoading()) return;
        var cachedPageNeedsRebuild = DirtyPages.Contains(_tab) && PageRoots.ContainsKey(_tab);
        // Destroy is deferred by Unity. Clear only the stale active page on one
        // frame and construct its replacement two frames later. Other page
        // hierarchies remain cached and inactive.
        if (cachedPageNeedsRebuild && !_pageClearedForRebuild)
        {
            ClearNativePage(_tab);
            _pageClearedForRebuild = true;
            _pageBuildNotBeforeFrame = Time.frameCount + 2;
            return;
        }
        if (Time.frameCount < _pageBuildNotBeforeFrame) return;
        BuildNativePage();
        FinishPageLoading();
        _pageDirty = false;
        _pageClearedForRebuild = false;
    }

    private static void BuildNativePage()
    {
        if (_uiContent == null) return;
        for (var i = 0; i < TabButtons.Count; i++)
        {
            var image = TabButtons[i].GetComponent<Image>();
            var text = TabButtons[i].GetComponentInChildren<Text>();
            if (image != null)
            {
                var selected = i == _tab;
                var sprite = selected ? (_tabSelectedSprite ?? _tabSprite ?? _buttonSprite) : (_tabSprite ?? _buttonSprite);
                ApplySprite(image, sprite, true, Color.white, selected ? new Color(0.70f, 0.43f, 0.08f, 1f) : new Color(0.095f, 0.08f, 0.065f, 0.98f));
            }
            if (text != null) text.color = i == _tab ? new Color(1f, 0.88f, 0.48f, 1f) : new Color(0.94f, 0.90f, 0.80f, 1f);
        }
        foreach (var pair in PageRoots)
        {
            if (pair.Value != null) pair.Value.SetActive(pair.Key == _tab);
        }
        PageClicks.Clear();
        if (!DirtyPages.Contains(_tab) && PageRoots.TryGetValue(_tab, out var cachedPage) && cachedPage != null)
        {
            if (PageClickCache.TryGetValue(_tab, out var cachedClicks)) PageClicks.AddRange(cachedClicks);
            return;
        }

        var page = UiObject("LazyPage_" + _tab, _uiContent.transform, new Vector2(1080, 654), Vector2.zero);
        PageRoots[_tab] = page;
        _activePageContent = page.GetComponent<RectTransform>();
        try
        {
            switch (_tab)
            {
                case 0: BuildNativeBattle(); break;
                case 1: BuildNativeHero(); break;
                case 2: BuildNativeItemCatalog(); break;
                case 3: BuildNativeItemModification(); break;
                case 4: BuildNativeCraft(); break;
                case 5: BuildNativeFaction(); break;
                case 6: BuildNativeExtras(); break;
                case 7: BuildNativeBreakThroughChoice(); break;
                case 8: BuildNativeCity(); break;
            }
            PageClickCache[_tab] = new List<UiClick>(PageClicks);
            DirtyPages.Remove(_tab);
        }
        catch
        {
            PageRoots.Remove(_tab);
            PageClickCache.Remove(_tab);
            PageClicks.Clear();
            page.SetActive(false);
            UnityEngine.Object.Destroy(page);
            throw;
        }
        finally
        {
            _activePageContent = null;
        }
    }

    private static void BuildNativeBattle()
    {
        var p = NativePageParent;
        AddNativeToggle(p, "无限生命／体力", 0, 0, () => InfiniteHp, v => InfiniteHp = v);
        AddNativeToggle(p, "无限内力", 0, 44, () => InfiniteMana, v => InfiniteMana = v);
        AddNativeToggle(p, "无限气力／耐力", 0, 88, () => InfinitePower, v => InfinitePower = v);
        AddNativeToggle(p, "外伤归零", 0, 132, () => ZeroExternal, v => ZeroExternal = v);
        AddNativeToggle(p, "内伤归零", 0, 176, () => ZeroInternal, v => ZeroInternal = v);
        AddNativeToggle(p, "毒伤归零", 0, 220, () => ZeroPoison, v => ZeroPoison = v);
        AddNativeToggle(p, "战斗移动范围最大", 0, 264, () => MaxMove, v => MaxMove = v);
        AddNativeToggle(p, "技能快速充能", 0, 308, () => FastCharge, v => FastCharge = v);
        AddNativeToggle(p, "技能快速冷却", 0, 352, () => FastCooldown, v => FastCooldown = v);
        AddNativeToggle(p, "在场同门亲友助战／逐人处置", 0, 410,
            () => BuildingCombat.Enabled.Value, v => BuildingCombat.Enabled.Value = v);
        AddText(p, "仇敌袭击：同场景同门亲友直接参战；胜后每人选择一次处置。", new Vector2(0, 452), new Vector2(500, 65));

        AddNativeToggle(p, "装备重量归零", 540, 0, () => NoEquipmentWeight, v => NoEquipmentWeight = v);
        AddNativeToggle(p, "背包重量归零", 540, 44, () => NoInventoryWeight, v => NoInventoryWeight = v);
        AddNativeToggle(p, "技能经验 ×1000", 540, 88, () => UnlimitedSkillExp, v => UnlimitedSkillExp = v);
        AddNativeToggle(p, "所选角色最高好感", 540, 132, () => MaxFavor, v => MaxFavor = v);
        AddNativeToggle(p, "新开局无限点数", 540, 176, () => UnlimitedCreationPoints, v => UnlimitedCreationPoints = v);
        AddText(p, "大地图移动速度", new Vector2(540, 240), new Vector2(480, 30));
        var speeds = new[] { 1f, 2f, 4f, 8f, 16f };
        for (var i = 0; i < speeds.Length; i++)
        {
            var speed = speeds[i];
            var selected = Math.Abs(WorldSpeed - speed) < 0.001f;
            AddButton(p, (selected ? "● " : "") + speed + "倍", new Vector2(540 + i * 96, 278), new Vector2(88, 38), () =>
            {
                WorldSpeed = speed;
                _status = $"大地图移动速度已设为 {speed} 倍，将直接作用于主角最终旅行速度。";
                LongYinTrainerPlugin.Logger.LogInfo($"World travel speed multiplier changed to {speed}x.");
                RequestPageRebuild();
            });
        }
        AddText(p, "操作提示：点击背包物品图标可选物品；查看 NPC 详情后，到“角色数值”页点击“选择当前查看角色”。", new Vector2(540, 350), new Vector2(500, 80));
    }

    private static void BuildNativeHero()
    {
        var hero = TargetHero();
        var p = NativePageParent;
        if (hero == null) { AddText(p, "请先载入存档。", Vector2.zero, new Vector2(600, 40)); return; }
        AddText(p, $"当前角色：{hero.heroName}（ID {hero.heroID}）", Vector2.zero, new Vector2(800, 34), 20);
        var inputs = new Dictionary<string, InputField>();
        for (var i = 0; i < HeroKeys.Length; i++)
        {
            var col = i / 7;
            var row = i % 7;
            AddNativeField(p, HeroLabels[i], Fields[HeroKeys[i]], col * 355, 45 + row * 43, inputs, HeroKeys[i]);
        }
        AddButton(p, "应用角色数值", new Vector2(0, 370), new Vector2(250, 40), () => { foreach (var pair in inputs) Fields[pair.Key] = pair.Value.text; ApplyHero(hero); });
        AddButton(p, "选择当前查看角色", new Vector2(260, 370), new Vector2(210, 40), SelectCurrentViewedHero);
        AddButton(p, "选择主角", new Vector2(480, 370), new Vector2(130, 40), () => { SelectHero(Player ?? hero); RequestPageRebuild(); });
        var heroSearch = AddInput(p, _heroSearchText, new Vector2(620, 370), new Vector2(235, 40));
        AddButton(p, "按姓名／ID选择", new Vector2(865, 370), new Vector2(175, 40), () =>
        {
            _heroSearchText = heroSearch.text.Trim();
            SelectHeroBySearch(_heroSearchText);
        });
        var attr = AddInput(p, _allAttr, new Vector2(125, 440), new Vector2(100, 34));
        AddText(p, "六维属性", new Vector2(0, 440), new Vector2(120, 34));
        AddButton(p, "设置基础值＋上限", new Vector2(235, 440), new Vector2(170, 34), () => { _allAttr = attr.text; SetAll(hero.baseAttri, hero.maxAttri, _allAttr); });
        var fight = AddInput(p, _allFight, new Vector2(125, 485), new Vector2(100, 34));
        AddText(p, "全部战斗技能", new Vector2(0, 485), new Vector2(120, 34));
        AddButton(p, "设置基础值＋上限", new Vector2(235, 485), new Vector2(170, 34), () => { _allFight = fight.text; SetAll(hero.baseFightSkill, hero.maxFightSkill, _allFight); });
        var living = AddInput(p, _allLiving, new Vector2(125, 530), new Vector2(100, 34));
        AddText(p, "全部生活技能", new Vector2(0, 530), new Vector2(120, 34));
        AddButton(p, "设置基础值＋上限", new Vector2(235, 530), new Vector2(170, 34), () => { _allLiving = living.text; SetAll(hero.baseLivingSkill, hero.maxLivingSkill, _allLiving); });
        AddText(p, "天赋槽位上限", new Vector2(520, 440), new Vector2(150, 34));
        var talentSlots = AddInput(p, LongYinTrainerPlugin.PlayerTalentSlotLimit.Value.ToString(CultureInfo.InvariantCulture), new Vector2(675, 440), new Vector2(100, 34));
        AddButton(p, "应用槽位", new Vector2(785, 440), new Vector2(150, 34), () =>
        {
            try
            {
                LongYinTrainerPlugin.PlayerTalentSlotLimit.Value = LongYinTrainerPlugin.SafeTalentSlotLimit(ParseInt(talentSlots.text));
                _status = $"主角永久天赋槽位上限已设为 {LongYinTrainerPlugin.PlayerTalentSlotLimit.Value}。重新获得天赋时立即生效。";
            }
            catch (Exception ex) { _status = "设置天赋槽位失败：" + ex.Message; }
        });
        AddText(p, "范围 1–100。这里只提高主角的数据上限；原版天赋区域会使用已有滚动条继续显示，不修改 NPC。", new Vector2(520, 485), new Vector2(500, 70), 15);
        AddText(p, "NPC 操作：先在左侧原版界面打开目标角色详情，再点击上面的“选择当前查看角色”；也可输入姓名或数字 ID。应用前请核对本页顶部姓名和 ID。", new Vector2(520, 555), new Vector2(510, 72), 15);
    }

    private static void SelectCurrentViewedHero()
    {
        try
        {
            var viewed = HeroDetailController.Instance?.nowShowHero;
            if (viewed == null)
            {
                _status = "当前没有正在查看的角色。请先在原版角色界面打开目标 NPC 的详情。";
                return;
            }
            SelectHero(viewed);
            _status = $"已选择当前查看角色：{viewed.heroName}（ID {viewed.heroID}）。";
            RequestPageRebuild();
        }
        catch (Exception ex) { _status = "选择当前查看角色失败：" + ex.Message; }
    }

    private static void SelectHeroBySearch(string search)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(search)) { _status = "请输入角色姓名或数字 ID。"; return; }
            var world = GameController.Instance?.worldData;
            if (world == null) { _status = "请先载入存档。"; return; }
            HeroData? found = null;
            if (int.TryParse(search, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) found = world.GetHero(id);
            else
            {
                void SearchList(Il2CppSystem.Collections.Generic.List<HeroData>? list, bool exact)
                {
                    if (list == null || found != null) return;
                    for (var i = 0; i < list.Count; i++)
                    {
                        var candidate = list[i];
                        var name = candidate?.heroName ?? "";
                        if (candidate == null || (exact ? !string.Equals(name, search, StringComparison.OrdinalIgnoreCase) : name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        found = candidate;
                        return;
                    }
                }
                SearchList(world.Heros, true);
                SearchList(world.TempHeros, true);
                SearchList(world.Heros, false);
                SearchList(world.TempHeros, false);
            }
            if (found == null) { _status = $"没有找到角色：{search}。"; return; }
            SelectHero(found);
            _status = $"已按搜索选择：{found.heroName}（ID {found.heroID}）。";
            RequestPageRebuild();
        }
        catch (Exception ex) { _status = "搜索角色失败：" + ex.Message; }
    }

    private static void BuildLegacyItemCatalog()
    {
        var p = NativePageParent;
        EnsureCatalog();
        AddText(p, "物品图鉴", Vector2.zero, new Vector2(160, 34), 22);
        var search = AddInput(p, _catalogSearch, new Vector2(160, 0), new Vector2(245, 34));
        AddButton(p, "搜索", new Vector2(415, 0), new Vector2(80, 34), () => { _catalogSearch = search.text.Trim(); _catalogPage = 0; RequestPageRebuild(); });
        AddButton(p, "清空", new Vector2(505, 0), new Vector2(80, 34), () => { _catalogSearch = ""; _catalogPage = 0; RequestPageRebuild(); });
        AddButton(p, "刷新数据", new Vector2(595, 0), new Vector2(100, 34), () =>
        {
            Catalog.Clear();
            _catalogSelected = null;
            RefreshRuntimeIconIndex();
            RequestPageRebuild();
        });

        var categories = CatalogCategories();
        for (var i = 0; i < categories.Count; i++)
        {
            var index = i;
            var button = AddButton(p, categories[i], new Vector2(0, 48 + i * 38), new Vector2(125, 32), () => { _catalogCategory = index; _catalogPage = 0; RequestPageRebuild(); });
            if (i == _catalogCategory)
            {
                button.GetComponent<Image>().color = new Color(0.70f, 0.43f, 0.08f, 1f);
                button.GetComponentInChildren<Text>().color = new Color(1f, 0.88f, 0.48f, 1f);
            }
        }

        var filtered = FilterCatalog(categories[Math.Clamp(_catalogCategory, 0, categories.Count - 1)], _catalogSearch);
        const int pageSize = 12;
        var pageCount = Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
        _catalogPage = Math.Clamp(_catalogPage, 0, pageCount - 1);
        var start = _catalogPage * pageSize;
        for (var i = 0; i < pageSize && start + i < filtered.Count; i++)
        {
            var entry = filtered[start + i];
            var col = i % 4;
            var row = i / 4;
            var x = 140 + col * 142;
            var y = 48 + row * 132;
            AddItemCard(p, entry, new Vector2(x, y), ReferenceEquals(entry, _catalogSelected), () =>
            {
                ClearSelectedItem();
                _catalogSelected = entry;
                RequestPageRebuild();
            });
        }
        AddButton(p, "上一页", new Vector2(140, 452), new Vector2(120, 34), () => { if (_catalogPage > 0) _catalogPage--; RequestPageRebuild(); });
        AddText(p, $"第 {_catalogPage + 1} / {pageCount} 页　共 {filtered.Count} 项", new Vector2(270, 452), new Vector2(250, 34), 16, TextAnchor.MiddleCenter);
        AddButton(p, "下一页", new Vector2(530, 452), new Vector2(120, 34), () => { if (_catalogPage + 1 < pageCount) _catalogPage++; RequestPageRebuild(); });

        var bagItem = ResolveSelectedItem();
        if (bagItem != null)
        {
            BuildSelectedItemEditor(p, bagItem);
            AddText(p, "提示：右侧正在编辑游戏背包中点击的现有物品。点击左侧任意图鉴卡片可切回物品生成。", new Vector2(0, 520), new Vector2(690, 56), 15);
            return;
        }

        var selected = _catalogSelected;
        AddText(p, selected == null ? "从左侧图鉴选择物品" : selected.Name, new Vector2(730, 48), new Vector2(330, 38), 21, TextAnchor.MiddleCenter);
        AddText(p, selected?.Detail ?? "列表直接读取当前游戏版本的数据。\n即使背包里没有，也可以直接添加。", new Vector2(730, 90), new Vector2(330, 82), 16);
        AddText(p, "数量", new Vector2(730, 190), new Vector2(80, 34));
        var quantity = AddInput(p, _catalogQuantity, new Vector2(815, 190), new Vector2(130, 34));
        InputField? level = null;
        InputField? rarity = null;
        var customEquipment = selected?.IsEquipment == true;
        var usesLevel = selected != null && (customEquipment || selected.Category == "材料" || selected.Category == "宝物");
        var usesRarity = selected != null && (customEquipment || selected.Category == "秘籍" || usesLevel);
        if (usesRarity)
        {
            if (usesLevel)
            {
                AddText(p, "等级", new Vector2(730, 234), new Vector2(80, 34));
                level = AddInput(p, _catalogLevel, new Vector2(815, 234), new Vector2(130, 34));
                _catalogLevelInput = level;
            }
            var rarityY = usesLevel ? 278 : 234;
            AddText(p, "稀有度", new Vector2(730, rarityY), new Vector2(80, 34));
            rarity = AddInput(p, _catalogRarity, new Vector2(815, rarityY), new Vector2(130, 34));
            _catalogRarityInput = rarity;
            var current = usesLevel ? $"将生成：{LevelName(ParseIntSafe(_catalogLevel))}｜{RarityName(ParseIntSafe(_catalogRarity))}" : $"当前：{RarityName(ParseIntSafe(_catalogRarity))}";
            _catalogPreviewText = AddText(p, current, new Vector2(730, 316), new Vector2(330, 28), 14);
        }
        else if (selected != null)
        {
            AddText(p, $"原始等级　{selected.BaseLevel}", new Vector2(730, 234), new Vector2(330, 34), 16);
            AddText(p, $"原始品质　{RarityName(selected.RareLv)}", new Vector2(730, 278), new Vector2(330, 34), 16);
            AddText(p, "该条目从当前游戏数据库即时复制，不会持有背包或界面的原生对象。", new Vector2(730, 316), new Vector2(330, 52), 14);
        }
        AddButton(p, "添加到主角背包", new Vector2(730, 380), new Vector2(300, 44), () =>
        {
            _catalogQuantity = quantity.text;
            if (level != null) _catalogLevel = level.text;
            if (rarity != null) _catalogRarity = rarity.text;
            AddCatalogItem(selected, _catalogQuantity, _catalogLevel, _catalogRarity);
        });
        AddText(p, "稀有度说明\n" + BuildRarityGuide(), new Vector2(730, 438), new Vector2(330, 160), 15);

        AddText(p, "提示：这里展示的是生成图鉴，不会当作背包现有物品。编辑已有物品时，请在游戏背包中点击该物品，再进入“物品修改”。", new Vector2(0, 520), new Vector2(690, 56), 15);
    }

    private static void BuildSelectedItemEditor(Transform parent, ItemData item)
    {
        var editedPointer = item.Pointer;
        AddText(parent, "编辑背包物品", new Vector2(730, 48), new Vector2(330, 34), 21, TextAnchor.MiddleCenter);
        AddText(parent, item.Name(false), new Vector2(730, 78), new Vector2(330, 32), 16, TextAnchor.MiddleCenter);

        AddText(parent, "名称", new Vector2(730, 116), new Vector2(70, 32));
        var name = AddInput(parent, _itemName, new Vector2(800, 116), new Vector2(260, 32));
        AddText(parent, "价值", new Vector2(730, 156), new Vector2(70, 32));
        var itemValue = AddInput(parent, _itemValue, new Vector2(800, 156), new Vector2(120, 32));
        AddText(parent, "等级", new Vector2(730, 196), new Vector2(70, 32));
        var level = AddInput(parent, _itemLevel, new Vector2(800, 196), new Vector2(120, 32));
        AddText(parent, "稀有度", new Vector2(730, 236), new Vector2(70, 32));
        var rarity = AddInput(parent, _itemRare, new Vector2(800, 236), new Vector2(120, 32));
        AddText(parent, RarityName(ParseIntSafe(_itemRare)), new Vector2(930, 236), new Vector2(130, 32), 14);
        AddText(parent, "重量", new Vector2(730, 276), new Vector2(70, 32));
        var weight = AddInput(parent, _itemWeight, new Vector2(800, 276), new Vector2(120, 32));
        AddText(parent, "毒量", new Vector2(730, 316), new Vector2(70, 32));
        var poison = AddInput(parent, _itemPoison, new Vector2(800, 316), new Vector2(120, 32));

        AddButton(parent, "应用现有物品数值", new Vector2(730, 360), new Vector2(330, 42), () =>
        {
            var current = ResolveSelectedItem();
            if (current == null || current.Pointer != editedPointer) { _status = "所选物品已经变化，请重新打开物品修改页。"; return; }
            _itemName = name.text;
            _itemValue = itemValue.text;
            _itemLevel = level.text;
            _itemRare = rarity.text;
            _itemWeight = weight.text;
            _itemPoison = poison.text;
            ApplyItem(current);
        });
        AddButton(parent, "返回图鉴生成", new Vector2(730, 410), new Vector2(330, 36), () =>
        {
            ClearSelectedItem();
            _status = "已返回物品图鉴生成。";
            _tab = 2;
            RequestPageRebuild();
        });
        AddText(parent, "稀有度：0 普通（灰）　1 优良（绿）\n2 稀有（蓝）　3 精良（紫）\n4 完美（橙）　5 绝世（红）\n等级只修改现有字段，不会重新抽取装备词条。", new Vector2(730, 456), new Vector2(330, 130), 14);
    }

    private static void AddItemCard(Transform parent, CatalogEntry entry, Vector2 pos, bool selected, Action action)
    {
        var rareColor = RarityColor(entry.RareLv);
        var card = AddButton(parent, "", pos, new Vector2(132, 122), action);
        var cardImage = card.GetComponent<Image>();
        ApplySprite(cardImage, _frameSprite, true, Color.white,
            selected ? new Color(0.72f, 0.12f, 0.08f, 1f) : rareColor);
        var paper = UiObject("CardPaper", card.transform, new Vector2(124, 114), new Vector2(4, 4));
        var paperImage = paper.AddComponent<Image>();
        ApplySprite(paperImage, _paperSprite, true, Color.white, new Color(0.97f, 0.95f, 0.87f, 0.98f));
        paperImage.raycastTarget = false;
        var sprite = GetItemSprite(entry.IconName);
        if (sprite != null)
        {
            var iconObj = UiObject("OriginalItemIcon", paper.transform, new Vector2(82, 82), new Vector2(21, 3));
            var icon = iconObj.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else AddText(paper.transform, "暂无图标", new Vector2(6, 26), new Vector2(112, 40), 14, TextAnchor.MiddleCenter);
        var gemSprite = GetItemSprite("RareLv" + Math.Clamp(entry.RareLv, 0, 5));
        if (gemSprite != null)
        {
            var gemObj = UiObject("OriginalRarityGem", card.transform, new Vector2(18, 18), new Vector2(57, -7));
            var gem = gemObj.AddComponent<Image>();
            gem.sprite = gemSprite;
            gem.preserveAspect = true;
            gem.raycastTarget = false;
        }
        var name = AddText(paper.transform, entry.Name, new Vector2(3, 90), new Vector2(118, 21), 14, TextAnchor.MiddleCenter);
        name.color = new Color(0.08f, 0.07f, 0.055f, 1f);
    }

    private static Sprite? GetItemSprite(string iconName) => string.IsNullOrWhiteSpace(iconName) ? null
        : TryResolveUploadedIcon(iconName, out var uploaded) ? uploaded : GetRuntimeItemSprite(iconName);

    private static Color RarityColor(int rareLv)
    {
        try
        {
            var list = GameDataController.Instance?.rareLvData;
            if (list != null && rareLv >= 0 && rareLv < list.Count && list[rareLv] != null) return list[rareLv].color;
        }
        catch { }
        return rareLv switch
        {
            <= 0 => new Color(0.42f, 0.42f, 0.40f, 1f),
            1 => new Color(0.20f, 0.62f, 0.28f, 1f),
            2 => new Color(0.16f, 0.48f, 0.82f, 1f),
            3 => new Color(0.46f, 0.25f, 0.72f, 1f),
            4 => new Color(0.92f, 0.52f, 0.08f, 1f),
            _ => new Color(0.82f, 0.08f, 0.10f, 1f)
        };
    }

    private static string BuildRarityGuide()
    {
        try
        {
            var list = GameDataController.Instance?.rareLvData;
            if (list == null || list.Count == 0) return "0 普通　1 优良　2 稀有　3 珍奇　4 绝世";
            var text = "";
            for (var i = 0; i < list.Count && i < 8; i++)
            {
                var data = list[i];
                if (data == null) continue;
                var hex = ColorUtility.ToHtmlStringRGB(data.color);
                var name = string.IsNullOrWhiteSpace(data.name) ? DefaultRarityName(i) : data.name;
                text += $"<color=#{hex}>■ {i} = {name}</color>　";
                if (i % 2 == 1) text += "\n";
            }
            return text.TrimEnd();
        }
        catch { return "0 普通　1 优良　2 稀有\n3 珍奇　4 绝世（以游戏数据为准）"; }
    }

    private static int ParseIntSafe(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static void UpdateCatalogPreview()
    {
        if (_catalogPreviewText == null || _catalogRarityInput == null) return;
        var rarityMax = Math.Max(0, (GameDataController.Instance?.rareLvData?.Count ?? 6) - 1);
        var rarityRaw = ParseIntSafe(_catalogRarityInput.text);
        var rarity = Math.Clamp(rarityRaw, 0, rarityMax);
        if (_catalogLevelInput != null)
        {
            var isEquipment = _catalogSelected?.IsEquipment == true;
            var levelMax = isEquipment ? Math.Max(0, (GlobalData.EquipLvName?.Count ?? 1) - 1) : 999;
            var levelRaw = ParseIntSafe(_catalogLevelInput.text);
            var level = Math.Clamp(levelRaw, 0, levelMax);
            var limited = level != levelRaw || rarity != rarityRaw ? "（已按游戏范围修正）" : "";
            _catalogPreviewText.text = $"将生成：{LevelName(level)}｜{RarityName(rarity)}{limited}";
        }
        else
        {
            var limited = rarity != rarityRaw ? "（已按游戏范围修正）" : "";
            _catalogPreviewText.text = $"将生成：{RarityName(rarity)}{limited}";
        }
    }

    private static string DefaultRarityName(int value) => value switch
    {
        0 => "普通（灰）",
        1 => "优良（绿）",
        2 => "稀有（蓝）",
        3 => "精良（紫）",
        4 => "完美（橙）",
        5 => "绝世（红）",
        _ => "无效"
    };

    private static string RarityName(int value)
    {
        try
        {
            var list = GameDataController.Instance?.rareLvData;
            if (list != null && value >= 0 && value < list.Count && list[value] != null && !string.IsNullOrWhiteSpace(list[value].name)) return list[value].name;
        }
        catch { }
        return DefaultRarityName(value);
    }

    private static string LevelName(int value)
    {
        try
        {
            var list = GlobalData.EquipLvName;
            if (list != null && value >= 0 && value < list.Count && !string.IsNullOrWhiteSpace(list[value])) return list[value];
        }
        catch { }
        return $"等级 {value}";
    }

    private static List<string> CatalogCategories() => new()
    {
        "全部", "武器", "护甲", "头盔", "鞋靴", "药品", "食物", "秘籍", "宝物", "材料", "坐骑"
    };

    private static List<CatalogEntry> FilterCatalog(string category, string search)
    {
        var result = new List<CatalogEntry>();
        for (var i = 0; i < Catalog.Count; i++)
        {
            var entry = Catalog[i];
            if (category != "全部" && entry.Category != category) continue;
            if (!string.IsNullOrWhiteSpace(search) && entry.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            result.Add(entry);
        }
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return result;
    }

    private static void EnsureCatalog()
    {
        if (Catalog.Count > 0) return;
        try { foreach (var step in BuildCatalogSteps()) { } }
        catch (Exception ex) { Catalog.Clear(); _status="读取物品数据库失败："+ex.Message; LongYinTrainerPlugin.Logger.LogError(ex); }
    }

    private static System.Collections.Generic.IEnumerable<object?> BuildCatalogSteps()
    {
        var database=GameDataController.Instance;
        if(database==null) yield break;
            AddItemDatabase("武器", database.weaponDataBase);
            yield return null;
            AddItemDatabase("护甲", database.armorDataBase);
            yield return null;
            AddItemDatabase("头盔", database.helmetDataBase);
            yield return null;
            AddItemDatabase("鞋靴", database.shoesDataBase);
            yield return null;
            AddItemDatabase("药品", database.medDataBase);
            yield return null;
            AddItemDatabase("食物", database.foodDataBase);
            yield return null;
            AddItemDatabase("坐骑", database.horseDataBase);
            yield return null;

            var batch = 0;
            foreach (var pair in database.kungfuSkillDataBase)
            {
                if (++batch % 32 == 0) yield return null;
                var skill = pair.Value;
                if (skill == null || skill.hide) continue;
                var skillId = skill.skillID;
                var skillName = skill.Name(false);
                var bookPreview = new ItemData(ItemType.Book).SetBookData(skillId, 0);
                Catalog.Add(new CatalogEntry
                {
                    Category = "秘籍",
                    Name = skillName + "秘籍",
                    Detail = $"秘籍｜技能 ID {skillId}\n稀有度可在添加前设置。",
                    IconName = bookPreview?.GetItemIconName() ?? "",
                    RareLv = 0,
                    BaseLevel = 0,
                    SkillId = skillId,
                    ItemTypeValue = (int)ItemType.Book,
                    Kind = CatalogKind.Book
                });
            }

            AddGeneratedCatalog("材料", CatalogKind.Material, ItemType.Material, GlobalData.MaterialTypeName);
            AddGeneratedCatalog("宝物", CatalogKind.Treasure, ItemType.Treasure, GlobalData.TreasureTypeName);

        _status = $"物品图鉴已读取：共 {Catalog.Count} 项。";
    }

    private static void AddItemDatabase(string category, Il2CppSystem.Collections.Generic.Dictionary<int, ItemData> database)
    {
        if (database == null) return;
        foreach (var pair in database)
        {
            var template = pair.Value;
            if (template == null) continue;
            var itemName = SafeItemName(template);
            var itemId = template.itemID;
            var itemType = template.type;
            var itemSubType = template.subType;
            var itemLevel = template.itemLv;
            var itemRarity = template.rareLv;
            var itemValue = template.value;
            var iconName = template.GetItemIconName();
            var isEquipment = template.equipmentData != null;
            Catalog.Add(new CatalogEntry
            {
                Category = category,
                Name = itemName,
                Detail = $"{category}｜ID {itemId}\n{GlobalData.GetItemTypeString(itemType, itemSubType)}｜价值 {itemValue}",
                IconName = iconName,
                RareLv = itemRarity,
                BaseLevel = itemLevel,
                ItemId = itemId,
                ItemTypeValue = (int)itemType,
                ItemSubType = itemSubType,
                IsEquipment = isEquipment,
                Kind = CatalogKind.Database
            });
        }
    }

    private static void AddGeneratedCatalog(string category, CatalogKind kind, ItemType type, Il2CppSystem.Collections.Generic.List<string> names)
    {
        if (names == null) return;
        for (var i = 0; i < names.Count; i++)
        {
            var subType = i;
            var typeName = names[i];
            if (string.IsNullOrWhiteSpace(typeName)) typeName = $"{category} {i + 1}";
            var preview = kind == CatalogKind.Material
                ? new ItemData(ItemType.Material).SetMaterialData(subType, 1, 0)
                : new ItemData(ItemType.Treasure).SetTreasureData(subType, 1, 0);
            Catalog.Add(new CatalogEntry
            {
                Category = category,
                Name = typeName,
                Detail = $"{category}类型：{typeName}\n等级与稀有度可在添加前设置。",
                IconName = preview?.GetItemIconName() ?? "",
                RareLv = 0,
                BaseLevel = 1,
                ItemTypeValue = (int)type,
                ItemSubType = subType,
                Kind = kind
            });
        }
    }

    private static string SafeItemName(ItemData item)
    {
        try
        {
            var value = item.Name(false);
            return string.IsNullOrWhiteSpace(value) ? item.name : value;
        }
        catch { return string.IsNullOrWhiteSpace(item.name) ? $"物品 {item.itemID}" : item.name; }
    }

    private static ItemData? ResolveDatabaseTemplate(CatalogEntry entry)
    {
        var data = GameDataController.Instance;
        if (data == null || entry.Kind != CatalogKind.Database) return null;
        Il2CppSystem.Collections.Generic.Dictionary<int, ItemData>? database = entry.Category switch
        {
            "武器" => data.weaponDataBase,
            "护甲" => data.armorDataBase,
            "头盔" => data.helmetDataBase,
            "鞋靴" => data.shoesDataBase,
            "药品" => data.medDataBase,
            "食物" => data.foodDataBase,
            "坐骑" => data.horseDataBase,
            _ => null
        };
        if (database == null || !database.ContainsKey(entry.ItemId)) return null;
        return database[entry.ItemId];
    }

    private static ItemData? CreateCatalogItem(CatalogEntry entry, int level, int rarity)
    {
        return entry.Kind switch
        {
            CatalogKind.Database => ResolveDatabaseTemplate(entry)?.Clone()?.TryCast<ItemData>(),
            CatalogKind.Book => new ItemData(ItemType.Book).SetBookData(entry.SkillId, Math.Max(0, rarity)),
            CatalogKind.Material => new ItemData(ItemType.Material).SetMaterialData(entry.ItemSubType, Math.Max(0, level), Math.Max(0, rarity)),
            CatalogKind.Treasure => new ItemData(ItemType.Treasure).SetTreasureData(entry.ItemSubType, Math.Max(0, level), Math.Max(0, rarity)),
            _ => null
        };
    }

    private static void AddCatalogItem(CatalogEntry? entry, string quantityText, string levelText, string rarityText)
    {
        if (entry == null) { _status = "请先从图鉴中选择物品。"; return; }
        var player = Player;
        if (player == null) { _status = "存档尚未载入。"; return; }
        try
        {
            var quantity = Math.Clamp(ParseInt(quantityText), 1, entry.IsEquipment ? 100 : 999);
            var equipmentLevelMax = entry.IsEquipment ? Math.Max(0, (GlobalData.EquipLvName?.Count ?? 1) - 1) : 999;
            var level = Math.Clamp(ParseInt(levelText), 0, equipmentLevelMax);
            var rarityMax = Math.Max(0, (GameDataController.Instance?.rareLvData?.Count ?? 6) - 1);
            var rarity = Math.Clamp(ParseInt(rarityText), 0, rarityMax);
            var added = 0;
            for (var i = 0; i < quantity; i++)
            {
                var item = entry.IsEquipment
                    ? GenerateEquipment(entry, level, rarity, player)
                    : CreateCatalogItem(entry, level, rarity);
                if (item == null) continue;
                player.GetItem(item, false, false, 0, true);
                added++;
            }
            _status = $"已将 {entry.Name} ×{added} 添加到主角背包。";
        }
        catch (Exception ex)
        {
            _status = "添加物品失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogError($"Add catalog item failed: {ex}");
        }
    }

    private static ItemData? GenerateEquipment(CatalogEntry entry, int desiredLevel, int desiredRarity, HeroData player)
    {
        var source = ResolveDatabaseTemplate(entry);
        var equipment = source?.equipmentData;
        var controller = GameController.Instance;
        if (source == null || equipment == null || controller == null) return null;
        ItemData? best = null;
        var bestScore = int.MaxValue;
        var bossLv = Math.Max(1f, desiredLevel + 1f);
        var value = Math.Clamp((desiredLevel + 1f) * 100f, 100f, 3200f);
        for (var attempt = 0; attempt < 64; attempt++)
        {
            ItemData? candidate;
            try
            {
                candidate = controller.GenerateRandomItemValue(
                    value,
                    (int)source.type,
                    bossLv,
                    source.subType,
                    equipment.littleType,
                    player,
                    0);
            }
            catch { candidate = null; }
            if (candidate?.equipmentData != null)
            {
                var score = Math.Abs(candidate.itemLv - desiredLevel) * 100 + Math.Abs(candidate.rareLv - desiredRarity) * 10;
                if (candidate.subType != source.subType) score += 1000;
                if (candidate.equipmentData.littleType != equipment.littleType) score += 1000;
                if (score < bestScore) { best = candidate; bestScore = score; }
                if (score == 0) return candidate;
                if (candidate.rareLv < desiredRarity) value *= 1.42f;
                else if (candidate.rareLv > desiredRarity) value = Math.Max(1f, value * 0.76f);
                else value *= 1.08f;
            }
            else value *= 1.35f;
            value = Math.Clamp(value, 100f, 3200f);
        }
        // Never overwrite level/rarity after native generation.  Those fields
        // must stay consistent with the generated affixes, frame and value.
        return best;
    }

    private static void BuildNativeItemModification()
    {
        var p = NativePageParent;
        var item = ResolveSelectedItem();
        if (item == null)
        {
            AddText(p, "请先在游戏背包中点击要修改的物品，再打开此页。", Vector2.zero, new Vector2(1040, 42), 21);
            AddText(p, "药品、食物、材料、秘籍等可修改名称、价值、等级、稀有度、重量和毒量。装备与坐骑还有独立的词条和属性编辑。", new Vector2(0, 55), new Vector2(1000, 80), 17);
            return;
        }
        bool supportsAffixes = item.equipmentData != null || item.horseData != null;
        if (_showItemAffixes && supportsAffixes) BuildNativeAffix();
        else
        {
            var editor = UiObject("ExistingItemFields", p, new Vector2(1080, 600), new Vector2(-700, 0));
            BuildSelectedItemEditor(editor.transform, item);
            AddText(p, "修改的是背包中已选中的这一件物品。\n\n价值、等级和稀有度分别保存；更改等级或稀有度不会重新生成装备词条。\n\n修改后重新查看物品详情。", new Vector2(430, 110), new Vector2(610, 180), 18);
        }
        AddButton(p, _showItemAffixes ? "物品数值" : "物品数值（当前）", new Vector2(0, 620), new Vector2(240, 32), () => { _showItemAffixes = false; RequestPageRebuild(); });
        if (supportsAffixes)
            AddButton(p, _showItemAffixes ? "装备／坐骑（当前）" : "装备／坐骑", new Vector2(260, 620), new Vector2(240, 32), () => { _showItemAffixes = true; RequestPageRebuild(); });
    }

    private static void BuildNativeAffix()
    {
        var p = NativePageParent;
        var selectedItem = ResolveSelectedItem();
        if (selectedItem?.horseData != null)
        {
            BuildNativeHorseAffix(p, selectedItem);
            return;
        }
        var snapshot = CaptureSelectedAffixes();
        if (snapshot == null) { AddText(p, "请先选择武器、护甲、马匹或鞍具。", Vector2.zero, new Vector2(700, 40)); return; }
        AddText(p, $"当前装备：{snapshot.ItemName}", Vector2.zero, new Vector2(690, 38), 21);
        AddText(p, "点击左侧已有词条即可载入编辑器", new Vector2(720, 0), new Vector2(340, 38), 15, TextAnchor.MiddleCenter);

        AddText(p, $"基础词条（{snapshot.Base.Count} 条）", new Vector2(0, 50), new Vector2(330, 34), 19, TextAnchor.MiddleCenter);
        AddText(p, $"额外词条（{snapshot.Extra.Count} 条，总计 {snapshot.Base.Count + snapshot.Extra.Count} 条）", new Vector2(350, 50), new Vector2(330, 34), 17, TextAnchor.MiddleCenter);
        AddText(p, "词条编辑器", new Vector2(720, 50), new Vector2(340, 34), 19, TextAnchor.MiddleCenter);
        var selectedAffixName = AddText(p, "当前词条：请从左侧选择", new Vector2(720, 92), new Vector2(340, 34), 16, TextAnchor.MiddleCenter);
        AddText(p, "数值", new Vector2(720, 136), new Vector2(100, 34));
        var value = AddInput(p, _affixValue, new Vector2(820, 136), new Vector2(240, 34));

        AddAffixRows(p, snapshot.Base, 0, 92, selectedAffixName, value, false);
        AddAffixRows(p, snapshot.Extra, 350, 92, selectedAffixName, value, true);

        AddButton(p, "写入基础词条", new Vector2(720, 184), new Vector2(165, 38), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } _affixValue = value.text; SetAffix(false, current); });
        AddButton(p, "写入额外词条", new Vector2(895, 184), new Vector2(165, 38), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } _affixValue = value.text; SetAffix(true, current); });
        AddButton(p, "删除基础中的该词条", new Vector2(720, 232), new Vector2(165, 36), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } RemoveAffix(false, _affixType, current); });
        AddButton(p, "删除额外中的该词条", new Vector2(895, 232), new Vector2(165, 36), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } RemoveAffix(true, _affixType, current); });

        var groupNames = new[] { "常用", "六维", "武学", "战斗", "增益", "特殊", "生活" };
        for (var i = 0; i < groupNames.Length; i++)
        {
            var groupIndex = i;
            var groupButton = AddButton(p, groupNames[i], new Vector2(720 + i * 49, 282), new Vector2(45, 32), () =>
            {
                if (_affixPresetGroup == groupIndex) return;
                _affixPresetGroup = groupIndex;
                RequestPageRebuild();
            });
            if (i == _affixPresetGroup)
            {
                groupButton.GetComponent<Image>().color = new Color(0.70f, 0.43f, 0.08f, 1f);
                groupButton.GetComponentInChildren<Text>().color = new Color(1f, 0.88f, 0.48f, 1f);
            }
        }

        string[] presetNames;
        string[] presetTypes;
        switch (_affixPresetGroup)
        {
            case 1:
                presetNames = new[] { "力道", "灵巧", "智力", "意志", "体质", "经脉", "力道上限", "灵巧上限", "智力上限", "意志上限", "体质上限", "经脉上限" };
                presetTypes = new[] { "attri0", "attri1", "attri2", "attri3", "attri4", "attri5", "maxAttri0", "maxAttri1", "maxAttri2", "maxAttri3", "maxAttri4", "maxAttri5" };
                break;
            case 2:
                presetNames = new[] { "内功", "轻功", "绝技", "拳掌", "剑法", "刀法", "长兵", "奇门", "射术", "内功威力", "轻功威力", "绝技威力", "拳掌威力", "剑法威力", "刀法威力", "长兵威力", "奇门威力", "射术威力" };
                presetTypes = new[] { "fightSkill0", "fightSkill1", "fightSkill2", "fightSkill3", "fightSkill4", "fightSkill5", "fightSkill6", "fightSkill7", "fightSkill8", "fightSkillPower0", "fightSkillPower1", "fightSkillPower2", "fightSkillPower3", "fightSkillPower4", "fightSkillPower5", "fightSkillPower6", "fightSkillPower7", "fightSkillPower8" };
                break;
            case 3:
                presetNames = new[] { "伤害", "护甲", "护甲率", "速度", "命中", "闪避", "暴击率", "抗暴率", "反击率", "抗反击", "连击率", "抗连击", "恢复效率", "减伤率", "伤势抵抗" };
                presetTypes = new[] { "damage", "armor", "armorRate", "speed", "acc", "evade", "critRate", "antiCrit", "counter", "antiCounter", "comboRate", "antiCombo", "recoverRate", "reduceReciveDamageRate", "woundResist" };
                break;
            case 4:
                presetNames = new[] { "伤害增加", "伤害降低", "护甲增加", "护甲降低", "速度增加", "速度降低", "命中增加", "命中降低", "闪避增加", "闪避降低", "暴击增加", "暴击降低", "连击增加", "连击降低", "负面抵抗" };
                presetTypes = new[] { "addDamage", "reduceDamage", "addArmor", "reduceArmor", "addSpeed", "reduceSpeed", "addAcc", "reduceAcc", "addEvade", "reduceEvade", "addCrit", "reduceCrit", "addCombo", "reduceCombo", "addReduceDebuffRate" };
                break;
            case 5:
                presetNames = new[] { "外伤伤害", "内伤伤害", "毒伤伤害", "吸取生命", "吸取内力", "削减内力", "真实伤害", "内伤真伤", "每回合回血", "每回合回蓝", "每回合体力", "内力护盾", "伤害反弹", "无敌", "复活" };
                presetTypes = new[] { "externalDamage", "internalDamage", "poisonDamage", "suckHp", "suckMana", "killMana", "trueDamage", "internalTrueDamage", "recoverHpPerRound", "recoverManaPerRound", "recoverPowerPerRound", "manaShield", "reboundDamage", "invincible", "reborn" };
                break;
            case 6:
                presetNames = new[] { "医术", "毒术", "学识", "口才", "采伐", "木植", "锻造", "炼药", "烹饪", "医术上限", "毒术上限", "学识上限", "口才上限", "采伐上限", "木植上限", "锻造上限", "炼药上限", "烹饪上限" };
                presetTypes = new[] { "livingSkill0", "livingSkill1", "livingSkill2", "livingSkill3", "livingSkill4", "livingSkill5", "livingSkill6", "livingSkill7", "livingSkill8", "maxLivingSkill0", "maxLivingSkill1", "maxLivingSkill2", "maxLivingSkill3", "maxLivingSkill4", "maxLivingSkill5", "maxLivingSkill6", "maxLivingSkill7", "maxLivingSkill8" };
                break;
            default:
                presetNames = new[] { "生命上限", "内力上限", "体力上限", "伤害", "护甲", "护甲率", "速度", "命中", "闪避", "暴击率", "反击率", "连击率", "移动范围", "恢复效率", "装备重量" };
                presetTypes = new[] { "maxHp", "maxMana", "maxPower", "damage", "armor", "armorRate", "speed", "acc", "evade", "critRate", "counter", "comboRate", "moveRange", "recoverRate", "equipmentWeight" };
                break;
        }
        for (var i = 0; i < presetNames.Length; i++)
        {
            var label = presetNames[i];
            var typeName = presetTypes[i];
            var col = i % 3;
            var row = i / 3;
            AddButton(p, label, new Vector2(720 + col * 116, 322 + row * 33), new Vector2(108, 28), () =>
            {
                _affixType = typeName;
                _affixValue = IsPercentageAffix(typeName) ? "5" : "10";
                value.text = _affixValue;
                selectedAffixName.text = $"当前词条：{label}";
                _status = $"已载入词条：{label}（{typeName}）{(IsPercentageAffix(typeName) ? "；输入框单位为百分比" : "")}";
            });
        }
        AddButton(p, "清空全部基础词条", new Vector2(0, 520), new Vector2(330, 38), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } current.equipmentData.baseAddData.Reset(); _baseAffixPage = 0; ItemChanged(current); _status = "基础词条已清空。"; RequestPageRebuild(); });
        AddButton(p, "清空全部额外词条", new Vector2(350, 520), new Vector2(330, 38), () => { var current = ResolveSelectedItem(); if (current?.equipmentData == null) { _status = "原装备已变化，请重新选择。"; return; } current.equipmentData.extraAddData.Reset(); _extraAffixPage = 0; ItemChanged(current); _status = "额外词条已清空。"; RequestPageRebuild(); });
        AddText(p, "提示：标有 % 的词条按百分数输入，输入 50 会保存为底层 0.5，并在游戏中显示 50%。速度与轻功技能是两个不同参数。", new Vector2(0, 578), new Vector2(1060, 42), 15);
    }

    private static void BuildNativeHorseAffix(Transform p, ItemData selectedItem)
    {
        var horse = selectedItem.horseData;
        if (horse == null) return;
        var isSaddle = selectedItem.subType == 1;
        var itemKind = isSaddle ? "鞍具" : "马匹";
        AddText(p, $"当前{itemKind}：{selectedItem.Name(false)}", Vector2.zero, new Vector2(700, 38), 21);
        AddText(p, isSaddle
            ? "鞍具的四项数值会作为绿色加成叠加到当前坐骑。"
            : "这里修改坐骑本体数值；绿色加成来自已装备的鞍具。",
            new Vector2(0, 40), new Vector2(1020, 38), 16);

        AddText(p, isSaddle ? "速度加成" : "速度", new Vector2(0, 100), new Vector2(150, 36));
        var speed = AddInput(p, F(horse.speed), new Vector2(150, 100), new Vector2(190, 36));
        AddText(p, isSaddle ? "冲刺加成" : "冲刺", new Vector2(390, 100), new Vector2(150, 36));
        var sprint = AddInput(p, F(horse.sprint), new Vector2(540, 100), new Vector2(190, 36));
        AddText(p, isSaddle ? "耐力加成" : "耐力", new Vector2(0, 154), new Vector2(150, 36));
        var power = AddInput(p, F(horse.power), new Vector2(150, 154), new Vector2(190, 36));
        AddText(p, isSaddle ? "坚韧加成" : "坚韧", new Vector2(390, 154), new Vector2(150, 36));
        var resist = AddInput(p, F(horse.resist), new Vector2(540, 154), new Vector2(190, 36));

        InputField? nowPower = null;
        InputField? favorRate = null;
        if (!isSaddle)
        {
            AddText(p, "当前耐力", new Vector2(0, 208), new Vector2(150, 36));
            nowPower = AddInput(p, F(horse.nowPower), new Vector2(150, 208), new Vector2(190, 36));
            AddText(p, "驯服度（%）", new Vector2(390, 208), new Vector2(150, 36));
            favorRate = AddInput(p, F(horse.favorRate * 100f), new Vector2(540, 208), new Vector2(190, 36));
        }

        var derivedText = AddText(p, HorseDerivedDescription(selectedItem), new Vector2(0, 278), new Vector2(1020, 86), 17);
        AddButton(p, $"应用{itemKind}数值", new Vector2(0, 390), new Vector2(340, 44), () =>
        {
            var current = ResolveSelectedItem();
            if (current?.horseData == null || current.subType != (isSaddle ? 1 : 0))
            {
                _status = $"原{itemKind}已经变化，请重新选择。";
                return;
            }
            if (ApplyHorseStats(current, speed.text, sprint.text, power.text, resist.text,
                    nowPower?.text, favorRate?.text))
                derivedText.text = HorseDerivedDescription(current);
        });
        AddText(p, isSaddle
            ? "已装备的鞍具会在本次按钮操作结束后同步到当前坐骑；无需卸下再装备。"
            : "负重上限、视野范围和探索耐力是游戏根据坐骑数据计算的派生值，修改后会自动重算。",
            new Vector2(0, 455), new Vector2(1020, 70), 16);
    }

    private static string HorseDerivedDescription(ItemData item)
    {
        if (item.horseData == null || item.subType == 1) return "鞍具属性：速度、冲刺、耐力、坚韧。";
        try
        {
            var maxWeight = item.GetHorseMaxWeightAdd();
            var seeRange = item.GetHorseSeeRange();
            var stepRate = item.GetHorseStepAddRate();
            var seePercent = Math.Abs(seeRange) <= 5f ? seeRange * 100f : seeRange;
            var stepPercent = Math.Abs(stepRate) <= 5f ? stepRate * 100f : stepRate;
            return $"派生属性：负重上限 {maxWeight:+0.###;-0.###;0}　视野范围 {seePercent:+0.###;-0.###;0}%　探索耐力 {stepPercent:+0.###;-0.###;0}%";
        }
        catch
        {
            return "派生属性会由游戏根据坐骑数值自动计算。";
        }
    }

    private static EquipmentAffixSnapshot? CaptureSelectedAffixes()
    {
        try
        {
            var item = ResolveSelectedItem();
            var equipment = item?.equipmentData;
            if (item == null || equipment == null) return null;
            var snapshot = new EquipmentAffixSnapshot { ItemName = item.Name(false) };
            CopyAffixes(equipment.baseAddData, snapshot.Base);
            CopyAffixes(equipment.extraAddData, snapshot.Extra);
            return snapshot;
        }
        catch (Exception ex)
        {
            _status = "读取装备词条失败，请重新选择装备：" + ex.Message;
            return null;
        }
    }

    private static void CopyAffixes(HeroSpeAddData data, List<AffixRowSnapshot> destination)
    {
        var keys = data.GetKeys();
        if (keys == null) return;
        var count = Math.Min(keys.Count, 128);
        for (var i = 0; i < count; i++)
        {
            var key = keys[i];
            var amount = data.Get(key);
            destination.Add(new AffixRowSnapshot
            {
                Key = key,
                Amount = amount,
                Description = DescribeSingleAffix(key, amount)
            });
        }
    }

    private static void AddAffixRows(Transform parent, List<AffixRowSnapshot> rows, float x, float y, Text selectedAffixName, InputField value, bool extra)
    {
        if (rows.Count == 0)
        {
            AddText(parent, "（无）", new Vector2(x, y), new Vector2(330, 36), 16, TextAnchor.MiddleCenter);
            return;
        }
        const int pageSize = 8;
        var pageCount = Math.Max(1, (rows.Count + pageSize - 1) / pageSize);
        var page = Math.Clamp(extra ? _extraAffixPage : _baseAffixPage, 0, pageCount - 1);
        if (extra) _extraAffixPage = page;
        else _baseAffixPage = page;
        var start = page * pageSize;
        var displayed = 0;
        for (var i = start; i < rows.Count && i < start + pageSize; i++)
        {
            var key = rows[i].Key;
            var amount = rows[i].Amount;
            var description = rows[i].Description;
            if (string.IsNullOrWhiteSpace(description)) description = $"未知词条 ID {key}{(amount >= 0 ? "+" : "")}{amount.ToString("0.###", CultureInfo.InvariantCulture)}";
            AddButton(parent, description, new Vector2(x, y + displayed * 43), new Vector2(330, 36), () =>
            {
                _affixType = key.ToString(CultureInfo.InvariantCulture);
                _affixValue = (IsPercentageAffix(key) ? amount * 100f : amount).ToString("0.###", CultureInfo.InvariantCulture);
                value.text = _affixValue;
                selectedAffixName.text = $"当前词条：{description}";
                _status = $"已载入词条：{description}{(IsPercentageAffix(key) ? "；输入框单位为百分比" : "")}";
            });
            displayed++;
        }
        if (displayed == 0) AddText(parent, "（无可识别词条）", new Vector2(x, y), new Vector2(330, 36), 16, TextAnchor.MiddleCenter);
        if (pageCount > 1)
        {
            AddButton(parent, "上一页", new Vector2(x, y + pageSize * 43), new Vector2(92, 30), () =>
            {
                if (extra) _extraAffixPage = Math.Max(0, _extraAffixPage - 1);
                else _baseAffixPage = Math.Max(0, _baseAffixPage - 1);
                RequestPageRebuild();
            });
            AddText(parent, $"第 {page + 1}/{pageCount} 页", new Vector2(x + 98, y + pageSize * 43), new Vector2(128, 30), 14, TextAnchor.MiddleCenter);
            AddButton(parent, "下一页", new Vector2(x + 232, y + pageSize * 43), new Vector2(98, 30), () =>
            {
                if (extra) _extraAffixPage = Math.Min(pageCount - 1, _extraAffixPage + 1);
                else _baseAffixPage = Math.Min(pageCount - 1, _baseAffixPage + 1);
                RequestPageRebuild();
            });
        }
    }

    private static string DescribeSingleAffix(int key, float amount)
    {
        var enumName = Enum.GetName(typeof(HeroSpeAddDataType), key);
        if (string.IsNullOrWhiteSpace(enumName)) return "";
        var label = DescribeAffixName(key, enumName);
        var percent = IsPercentageAffix(enumName);
        var displayAmount = percent ? amount * 100f : amount;
        return $"{label}{(displayAmount >= 0 ? "+" : "")}{displayAmount.ToString("0.###", CultureInfo.InvariantCulture)}{(percent ? "%" : "")}";
    }

    private static string DescribeAffixName(int key, string? enumName = null)
    {
        enumName ??= Enum.GetName(typeof(HeroSpeAddDataType), key);
        if (string.IsNullOrWhiteSpace(enumName)) return "未知效果";
        var indexedLabel = DescribeIndexedAffix(enumName);
        if (indexedLabel.Length > 0) return indexedLabel;
        var label = enumName switch
        {
            "maxHp" => "生命上限",
            "maxMana" => "内力上限",
            "maxPower" => "体力上限",
            "damage" => "伤害",
            "armor" => "护甲",
            "armorRate" => "护甲率",
            "speed" => "速度",
            "acc" => "命中",
            "evade" => "闪避",
            "critRate" => "暴击率",
            "antiCrit" => "抗暴率",
            "counter" => "反击率",
            "antiCounter" => "抗反击率",
            "comboRate" => "连击率",
            "antiCombo" => "抗连击率",
            "expRate" => "经验效率",
            "recoverRate" => "恢复效率",
            "reduceReciveDamageRate" => "减伤率",
            "addDebuffRate" => "负面施加率",
            "reduceDebuffRate" => "负面抵抗率",
            "woundResist" => "伤势抵抗",
            "clearMovePower" => "清空移动力",
            "recoverMovePower" => "恢复移动力",
            "externalDamage" => "外伤伤害",
            "internalDamage" => "内伤伤害",
            "poisonDamage" => "毒伤伤害",
            "suckHp" => "吸取生命",
            "suckMana" => "吸取内力",
            "killMana" => "削减内力",
            "recoverAll" => "全部恢复",
            "burn" => "灼烧",
            "recoverHp" => "生命恢复",
            "bleed" => "流血",
            "recoverMana" => "内力恢复",
            "elec" => "感电",
            "recoverPower" => "体力恢复",
            "losePower" => "体力削减",
            "frozen" => "冰冻",
            "hitHandPoint" => "打击手部",
            "hitFootPoint" => "打击足部",
            "hitChestPoint" => "打击胸部",
            "hitBackPoint" => "打击背部",
            "hitHeadPoint" => "打击头部",
            "reduceDamage" => "受到伤害降低",
            "addArmor" => "护甲增加",
            "reduceArmor" => "护甲降低",
            "addSpeed" => "速度增加",
            "reduceSpeed" => "速度降低",
            "addAcc" => "命中增加",
            "reduceAcc" => "命中降低",
            "addEvade" => "闪避增加",
            "reduceEvade" => "闪避降低",
            "reduceReciveDamage" => "受到伤害降低",
            "addReciveDamage" => "受到伤害增加",
            "crazy" => "狂暴",
            "confusion" => "混乱",
            "deathFight" => "死斗",
            "minusArmor" => "破甲",
            "addCrit" => "暴击增加",
            "reduceCrit" => "暴击降低",
            "addAntiCrit" => "抗暴增加",
            "reduceAntiCrit" => "抗暴降低",
            "addBlock" => "格挡增加",
            "addAntiBlock" => "抗格挡增加",
            "addCombo" => "连击增加",
            "reduceCombo" => "连击降低",
            "addAntiCombo" => "抗连击增加",
            "reduceAntiCombo" => "抗连击降低",
            "addDamage" => "伤害增加",
            "addRecoverRate" => "恢复效率增加",
            "reduceRecoverRate" => "恢复效率降低",
            "addReduceDebuffRate" => "负面抵抗增加",
            "reduceReduceDebuffRate" => "负面抵抗降低",
            "internalTrueDamage" => "内伤真实伤害",
            "trueDamage" => "真实伤害",
            "recoverHpPerRound" => "每回合恢复生命",
            "recoverManaPerRound" => "每回合恢复内力",
            "recoverPowerPerRound" => "每回合恢复体力",
            "recoverWoundPerRound" => "每回合恢复伤势",
            "manaShield" => "内力护盾",
            "reboundDamage" => "伤害反弹",
            "defenceDamage" => "防御反伤",
            "stopMove" => "无法移动",
            "invincible" => "无敌",
            "stun" => "眩晕",
            "reborn" => "复活",
            "backDamage" => "反击伤害",
            "backSuckMana" => "反击吸取内力",
            "backKillMana" => "反击削减内力",
            "hitFar" => "远程攻击",
            "hitClose" => "近战攻击",
            "moveRange" => "移动范围",
            "addMoveRange" => "移动范围增加",
            "reduceMoveRange" => "移动范围降低",
            "postureThrough" => "架势穿透",
            "postureBlock" => "架势格挡",
            "changeAttckTarget" => "改变攻击目标",
            "FameGainRate" => "名气获取率",
            "ContributionRate" => "贡献获取率",
            "TravelSpeedRate" => "旅行速度",
            "CureWoundRate" => "疗伤效率",
            "SkillFightExpRate" => "战斗技能经验",
            "SkillBookExpRate" => "读书经验",
            "LivingSkillExpRate" => "生活技能经验",
            "dealPriceRate" => "交易价格",
            "horseAddAttri" => "坐骑属性加成",
            "equipAddRate" => "装备加成率",
            "medResistReduce" => "药物抗性降低",
            "equipmentWeight" => "装备重量",
            "summonDamage" => "召唤物伤害",
            "summonSpeed" => "召唤物速度",
            "summonHp" => "召唤物生命",
            "defenceBuildingHp" => "防御建筑生命",
            "favorRate" => "好感获取率",
            "selfForceSkillPower" => "所属势力功法威力",
            "reduceBadFame" => "恶名降低",
            _ => ""
        };
        if (label.Length > 0) return label;
        try
        {
            var databaseName = FindSpeAddDefinition(key)?.name;
            if (!string.IsNullOrWhiteSpace(databaseName) && !string.Equals(databaseName, enumName, StringComparison.OrdinalIgnoreCase)) return databaseName;
        }
        catch { }
        return $"特殊效果 {key}";
    }

    private static string LocalizeAffixDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return description;
        var localized = description;
        var seen = new HashSet<int>();
        foreach (var raw in Enum.GetValues(typeof(HeroSpeAddDataType)))
        {
            var key = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            if (!seen.Add(key)) continue;
            var enumName = Enum.GetName(typeof(HeroSpeAddDataType), key);
            if (string.IsNullOrWhiteSpace(enumName)) continue;
            var label = DescribeAffixName(key, enumName);
            localized = localized.Replace("其他：" + enumName, label, StringComparison.Ordinal);
            localized = localized.Replace("其他:" + enumName, label, StringComparison.Ordinal);
        }
        return localized;
    }

    private static bool IsPercentageAffix(int key)
    {
        if (AffixPercentCache.TryGetValue(key, out var cached)) return cached;
        var result = false;
        try
        {
            var definition = FindSpeAddDefinition(key);
            if (definition != null && definition.showPercent) result = true;
        }
        catch { }
        if (!result)
        {
            var enumName = Enum.GetName(typeof(HeroSpeAddDataType), key);
            result = !string.IsNullOrWhiteSpace(enumName) && IsPercentageAffixNameFallback(enumName);
        }
        AffixPercentCache[key] = result;
        return result;
    }

    private static bool IsPercentageAffix(string enumName)
    {
        if (string.IsNullOrWhiteSpace(enumName)) return false;
        if (Enum.TryParse<HeroSpeAddDataType>(enumName, true, out var parsed)) return IsPercentageAffix((int)parsed);
        return IsPercentageAffixNameFallback(enumName);
    }

    private static bool IsPercentageAffixNameFallback(string enumName)
    {
        if (enumName.EndsWith("Rate", StringComparison.Ordinal) || enumName.Contains("ExpRate", StringComparison.Ordinal)) return true;
        if (enumName.StartsWith("fightSkillPower", StringComparison.Ordinal)) return true;
        return enumName is
            "damage" or "speed" or "acc" or "evade" or
            "antiCrit" or "counter" or "antiCounter" or "antiCombo" or "woundResist" or
            "suckHp" or "suckMana" or "killMana" or
            "addArmor" or "reduceArmor" or "addSpeed" or "reduceSpeed" or
            "addAcc" or "reduceAcc" or "addEvade" or "reduceEvade" or
            "addCrit" or "reduceCrit" or "addAntiCrit" or "reduceAntiCrit" or
            "addBlock" or "addAntiBlock" or "addCombo" or "reduceCombo" or
            "addAntiCombo" or "reduceAntiCombo" or "addDamage" or
            "reduceDamage" or "reduceReciveDamage" or "addReciveDamage" or
            "addRecoverRate" or "reduceRecoverRate" or "addReduceDebuffRate" or
            "reduceReduceDebuffRate" or "manaShield" or "reboundDamage" or
            "defenceDamage" or "medResistReduce" or "selfForceSkillPower";
    }

    private static string DescribeIndexedAffix(string enumName)
    {
        if (TryIndexedAffix(enumName, "maxAttri", "上限", AttributeAffixLabels, out var label)) return label;
        if (TryIndexedAffix(enumName, "addAttri", "增加", AttributeAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "reduceAttri", "降低", AttributeAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "attri", "", AttributeAffixLabels, out label)) return label;

        if (TryIndexedAffix(enumName, "maxFightSkill", "上限", FightSkillAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "fightSkillPower", "威力", FightSkillAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "fightSkill", "", FightSkillAffixLabels, out label)) return label;
        if (TryIndexedRateAffix(enumName, "fightSkill", "ExpRate", "经验效率", FightSkillAffixLabels, out label)) return label;

        if (TryIndexedAffix(enumName, "maxLivingSkill", "上限", LivingSkillAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "livingSkill", "", LivingSkillAffixLabels, out label)) return label;
        if (TryIndexedRateAffix(enumName, "livingSkill", "ExpRate", "经验效率", LivingSkillAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "recoverPartPosture", "架势恢复", PosturePartAffixLabels, out label)) return label;
        if (TryIndexedAffix(enumName, "fightSkillRange", "攻击范围", FightSkillAffixLabels, out label)) return label;
        return "";
    }

    private static bool TryIndexedAffix(string enumName, string prefix, string suffix, string[] labels, out string label)
    {
        label = "";
        if (!enumName.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var indexText = enumName.Substring(prefix.Length);
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= labels.Length) return false;
        label = labels[index] + suffix;
        return true;
    }

    private static bool TryIndexedRateAffix(string enumName, string prefix, string ending, string suffix, string[] labels, out string label)
    {
        label = "";
        if (!enumName.StartsWith(prefix, StringComparison.Ordinal) || !enumName.EndsWith(ending, StringComparison.Ordinal)) return false;
        var indexText = enumName.Substring(prefix.Length, enumName.Length - prefix.Length - ending.Length);
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= labels.Length) return false;
        label = labels[index] + suffix;
        return true;
    }

    private static string ResolveAffixType(string chineseName)
    {
        if (AffixTypeByChineseName.TryGetValue(chineseName, out var cached)) return cached;
        var enumName = chineseName switch
        {
            "生命上限" => "maxHp",
            "内力上限" => "maxMana",
            "体力上限" => "maxPower",
            "伤害" => "damage",
            "护甲" => "armor",
            "护甲率" => "armorRate",
            "轻功属性" or "速度" => "speed",
            "命中" => "acc",
            "闪避" => "evade",
            "暴击率" => "critRate",
            "反击率" => "counter",
            "连击率" => "comboRate",
            "移动范围" => "moveRange",
            "恢复效率" => "recoverRate",
            "战斗技能经验" => "SkillFightExpRate",
            "读书经验" => "SkillBookExpRate",
            "生活技能经验" => "LivingSkillExpRate",
            "装备重量" => "equipmentWeight",
            _ => ""
        };
        if (enumName.Length > 0 && Enum.TryParse<HeroSpeAddDataType>(enumName, true, out _))
        {
            AffixTypeByChineseName[chineseName] = enumName;
            return enumName;
        }
        AffixTypeByChineseName[chineseName] = "";
        return "";
    }

    private static void RemoveAffix(bool extra, string typeText, ItemData item)
    {
        try
        {
            var equipment = item.equipmentData ?? throw new InvalidOperationException("所选物品已不再是装备");
            var data = extra ? equipment.extraAddData : equipment.baseAddData;
            var key = int.TryParse(typeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
                ? numeric
                : (int)Enum.Parse<HeroSpeAddDataType>(typeText, true);
            data.heroSpeAddData.Remove(key);
            ItemChanged(item);
            _status = "已删除当前词条。";
            RequestPageRebuild();
        }
        catch (Exception ex) { _status = "删除词条失败：" + ex.Message; }
    }

    private static void BuildNativeCraft()
    {
        var p = NativePageParent;
        AddText(p, "打造与炼制辅助", Vector2.zero, new Vector2(900, 38), 22);
        AddText(p, "锁定的是游戏用于生成成品的实际价值预算，预览与结算会使用同一数值。", new Vector2(0, 38), new Vector2(1000, 45), 16);

        AddNativeToggle(p, "锁定装备打造价值", 0, 90, () => LongYinTrainerPlugin.LockEquipmentCraftValue.Value, v => LongYinTrainerPlugin.LockEquipmentCraftValue.Value = v);
        AddText(p, "目标价值", new Vector2(505, 90), new Vector2(100, 38));
        var equipValue = AddInput(p, F(LongYinTrainerPlugin.EquipmentCraftValue.Value), new Vector2(610, 90), new Vector2(180, 38));

        AddNativeToggle(p, "锁定炼丹价值", 0, 142, () => LongYinTrainerPlugin.LockMedicineCraftValue.Value, v => LongYinTrainerPlugin.LockMedicineCraftValue.Value = v);
        AddText(p, "目标价值", new Vector2(505, 142), new Vector2(100, 38));
        var medicineValue = AddInput(p, F(LongYinTrainerPlugin.MedicineCraftValue.Value), new Vector2(610, 142), new Vector2(180, 38));

        AddNativeToggle(p, "锁定烹饪价值", 0, 194, () => LongYinTrainerPlugin.LockFoodCraftValue.Value, v => LongYinTrainerPlugin.LockFoodCraftValue.Value = v);
        AddText(p, "目标价值", new Vector2(505, 194), new Vector2(100, 38));
        var foodValue = AddInput(p, F(LongYinTrainerPlugin.FoodCraftValue.Value), new Vector2(610, 194), new Vector2(180, 38));

        AddText(p, "已解除 3200 上限：接受任意非负有限值。5000 会原样用于预览与结算；极端大值可能使原游戏公式溢出。", new Vector2(0, 250), new Vector2(1050, 38), 16);

        AddNativeToggle(p, "锁定制作天数", 0, 296, () => LongYinTrainerPlugin.LockCraftTime.Value, v => LongYinTrainerPlugin.LockCraftTime.Value = v);
        AddText(p, "天数（可填 0）", new Vector2(505, 296), new Vector2(150, 38));
        var craftTime = AddInput(p, LongYinTrainerPlugin.CraftTimeValue.Value.ToString(CultureInfo.InvariantCulture), new Vector2(660, 296), new Vector2(130, 38));

        AddText(p, "成品最低品质（0 关闭，1 绿，2 蓝，3 紫，4 橙，5 红）", new Vector2(0, 348), new Vector2(470, 38), 16);
        var rarity = AddInput(p, LongYinTrainerPlugin.MinimumCraftRarity.Value.ToString(), new Vector2(475, 348), new Vector2(120, 38));

        AddButton(p, "应用制作设置", new Vector2(0, 410), new Vector2(330, 44), () =>
        {
            try
            {
                LongYinTrainerPlugin.EquipmentCraftValue.Value = LongYinTrainerPlugin.SafeCraftValue(ParseFloat(equipValue.text));
                LongYinTrainerPlugin.MedicineCraftValue.Value = LongYinTrainerPlugin.SafeCraftValue(ParseFloat(medicineValue.text));
                LongYinTrainerPlugin.FoodCraftValue.Value = LongYinTrainerPlugin.SafeCraftValue(ParseFloat(foodValue.text));
                LongYinTrainerPlugin.CraftTimeValue.Value = LongYinTrainerPlugin.SafeCraftTime(ParseInt(craftTime.text));
                LongYinTrainerPlugin.InstantCraft.Value = LongYinTrainerPlugin.LockCraftTime.Value && LongYinTrainerPlugin.CraftTimeValue.Value == 0;
                var rarityMax = Math.Max(0, (GameDataController.Instance?.rareLvData?.Count ?? 6) - 1);
                LongYinTrainerPlugin.MinimumCraftRarity.Value = Math.Clamp(ParseInt(rarity.text), 0, rarityMax);
                _status = $"制作设置已应用：装备 {LongYinTrainerPlugin.EquipmentCraftValue.Value:0.##}，炼丹 {LongYinTrainerPlugin.MedicineCraftValue.Value:0.##}，烹饪 {LongYinTrainerPlugin.FoodCraftValue.Value:0.##}，天数 {LongYinTrainerPlugin.CraftTimeValue.Value}，最低品质 {LongYinTrainerPlugin.MinimumCraftRarity.Value}。";
            }
            catch (Exception ex) { _status = "制作设置应用失败：" + ex.Message; }
        });

        AddText(p, "成功率与制作效率已完全交还游戏原版计算；修改器不再挂接或改写该数值。天数范围为 0–3650。", new Vector2(0, 475), new Vector2(1020, 45), 16);
        AddText(p, "修改器不会主动调用制作界面的内部方法；游戏在实际预览与结算时读取锁定值。设置会保存到 BepInEx 配置。", new Vector2(0, 525), new Vector2(1020, 70), 16);
    }

    private static void BuildNativeFaction()
    {
        var p = NativePageParent;
        var hero = TargetHero();
        var force = hero?.GetForce(true);
        AddText(p, force == null ? "当前角色没有所属势力。" : $"当前势力：{force.forceName}（ID {force.forceID}，跟随所选角色；未选时为主角）", Vector2.zero, new Vector2(1050, 36), 18);
        AddText(p, "势力金钱", new Vector2(0, 55), new Vector2(150, 34));
        if (force != null && _resourceForceId != force.forceID)
        {
            _resourceForceId = force.forceID;
            _forceMoney = F(force.forceStorage?.money ?? 0);
            ResourceValues.Clear();
            if (force.resourceStore != null)
                for (var i = 0; i < force.resourceStore.Count; i++) ResourceValues.Add(F(force.resourceStore[i]));
        }
        var money = AddInput(p, _forceMoney, new Vector2(160, 55), new Vector2(180, 34));
        var resourceCount = force?.resourceStore?.Count ?? 0;
        while (ResourceValues.Count < resourceCount) ResourceValues.Add("0");
        var resources = new InputField[resourceCount];
        for (var i = 0; i < resources.Length; i++)
        {
            var resourceName = i < GlobalData.ResourceName.Count && !string.IsNullOrWhiteSpace(GlobalData.ResourceName[i])
                ? GlobalData.ResourceName[i]
                : $"资源 {i + 1}";
            AddText(p, resourceName, new Vector2(0, 105 + i * 45), new Vector2(150, 34));
            resources[i] = AddInput(p, ResourceValues[i], new Vector2(160, 105 + i * 45), new Vector2(180, 34));
        }
        var applyY = Math.Max(390, 115 + resourceCount * 45);
        AddButton(p, "应用势力数值", new Vector2(0, applyY), new Vector2(340, 40), () => { _forceMoney = money.text; for (var i = 0; i < resources.Length; i++) ResourceValues[i] = resources[i].text; ApplyForce(force); });
        var lockButton = AddButton(p, IsForceLocked(force) ? "解除资源锁定" : "锁定当前资源", new Vector2(0, applyY + 50), new Vector2(340, 40), () => ToggleForceLock(force));
        _forceLockButtonText = lockButton.GetComponentInChildren<Text>();
        _forceLockPageId = force?.forceID ?? int.MinValue;
        AddText(p, "先应用数值，再锁定。锁定金钱及上方全部资源。\n关闭面板仍生效；换存档或重启后自动解除。", new Vector2(0, applyY + 98), new Vector2(490, 64), 15);
        AddNativeToggle(p, "新建角色无限分配点", 540, 55, () => UnlimitedCreationPoints, v => UnlimitedCreationPoints = v);
        AddNativeToggle(p, "解锁全部传承天赋", 540, 105,
            () => LongYinTrainerPlugin.UnlockInheritedTalents.Value,
            SetUnlockInheritedTalents);
        AddNativeToggle(p, "开局允许高级天赋", 540, 155,
            () => LongYinTrainerPlugin.UnlockAdvancedStartTalents.Value,
            SetUnlockAdvancedStartTalents);
        AddText(p, "开局天赋上限", new Vector2(540, 210), new Vector2(150, 36));
        var startTalentLimit = AddInput(p, LongYinTrainerPlugin.StartTalentSlotLimit.Value.ToString(CultureInfo.InvariantCulture), new Vector2(700, 210), new Vector2(120, 36));
        AddButton(p, "应用上限", new Vector2(835, 210), new Vector2(165, 36), () =>
        {
            try
            {
                LongYinTrainerPlugin.StartTalentSlotLimit.Value = LongYinTrainerPlugin.SafeStartTalentSlotLimit(ParseInt(startTalentLimit.text));
                RefreshStartTalentMenu();
                _status = $"新建角色的初始天赋上限已设为 {LongYinTrainerPlugin.StartTalentSlotLimit.Value}。";
            }
            catch (Exception ex) { _status = "设置开局天赋上限失败：" + ex.Message; }
        });
        AddText(p,
            "无限分配点会把天赋点、属性、战斗技能、生活技能的剩余点数锁定为 999。允许高级天赋仅放宽本次开局的初始领悟与前置条件；不会改变进入游戏后的领悟规则。天赋上限范围 1–1000。",
            new Vector2(540, 265), new Vector2(490, 145), 16);
        BuildOtherSectMerit(p);
    }

    private static void SetUnlockInheritedTalents(bool enabled)
    {
        LongYinTrainerPlugin.UnlockInheritedTalents.Value = enabled;
        RefreshStartTalentMenu();
        _status = enabled
            ? "传承天赋已在开局界面临时解锁；不会写入 Steam／WeGame 成就。"
            : "传承天赋恢复使用原版结局解锁条件。";
    }

    private static void SetUnlockAdvancedStartTalents(bool enabled)
    {
        LongYinTrainerPlugin.UnlockAdvancedStartTalents.Value = enabled;
        RefreshStartTalentMenu();
        _status = enabled
            ? "高级天赋已允许在新建角色阶段直接领悟。"
            : "高级天赋已恢复原版的初始领悟与前置条件。";
    }

    private static void RefreshStartTalentMenu()
    {
        try
        {
            var startMenu = StartMenuController.Instance;
            if (startMenu != null && startMenu.tagRoot != null && startMenu.tagRoot.activeInHierarchy)
                startMenu.RefreshTagMenu();
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Start talent menu refresh deferred safely: {ex.Message}");
        }
    }

    private static void BuildNativeExtras()
    {
        var p = NativePageParent;
        AddText(p, "扩展功能", Vector2.zero, new Vector2(320, 36), 22);
        AddText(p, "来自 longyin_plus 已验证玩法钩子的独立兼容实现", new Vector2(350, 2), new Vector2(690, 34), 15, TextAnchor.MiddleRight);

        AddNativeToggle(p, "探索移动不消耗体力", 0, 45,
            () => LongYinTrainerPlugin.LockExploreStamina.Value,
            value => LongYinTrainerPlugin.LockExploreStamina.Value = value);
        AddNativeToggle(p, "冻结日期推进", 540, 45,
            () => LongYinTrainerPlugin.FreezeDate.Value,
            value => LongYinTrainerPlugin.FreezeDate.Value = value);
        AddNativeToggle(p, "新建角色无限分配点", 0, 89,
            () => UnlimitedCreationPoints,
            value => UnlimitedCreationPoints = value);
        AddNativeToggle(p, "下次启动跳过片头", 540, 89,
            () => LongYinTrainerPlugin.SkipStartupIntro.Value,
            value => LongYinTrainerPlugin.SkipStartupIntro.Value = value);
        AddNativeToggle(p, "技能满级奖励天赋点", 0, 133,
            () => LongYinTrainerPlugin.EnableSkillTalentGrant.Value,
            value => LongYinTrainerPlugin.EnableSkillTalentGrant.Value = value);
        AddNativeToggle(p, "主角全地图视野", 540, 133,
            () => LongYinTrainerPlugin.ShowAllBigMapEvents.Value,
            SetShowAllBigMapEvents);

        AddText(p, "读书经验倍率", new Vector2(0, 195), new Vector2(180, 34));
        var bookExp = AddInput(p, LongYinTrainerPlugin.BookExpMultiplier.Value.ToString(CultureInfo.InvariantCulture), new Vector2(190, 195), new Vector2(150, 34));
        AddText(p, "战斗技能经验倍率", new Vector2(0, 237), new Vector2(180, 34));
        var battleExp = AddInput(p, LongYinTrainerPlugin.BattleSkillExpMultiplier.Value.ToString(CultureInfo.InvariantCulture), new Vector2(190, 237), new Vector2(150, 34));
        AddText(p, "开局点数倍率", new Vector2(0, 279), new Vector2(180, 34));
        var creationPoints = AddInput(p, LongYinTrainerPlugin.CreationPointMultiplier.Value.ToString(CultureInfo.InvariantCulture), new Vector2(190, 279), new Vector2(150, 34));
        AddText(p, "战斗倍速倍率", new Vector2(0, 321), new Vector2(180, 34));
        var battleSpeed = AddInput(p, LongYinTrainerPlugin.BattleSpeedMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(190, 321), new Vector2(150, 34));
        AddText(p, "天赋奖励触发等级", new Vector2(0, 363), new Vector2(180, 34));
        var talentLevel = AddInput(p, LongYinTrainerPlugin.SkillTalentLevelThreshold.Value.ToString(CultureInfo.InvariantCulture), new Vector2(190, 363), new Vector2(150, 34));
        AddText(p, "每阶奖励天赋点", new Vector2(0, 405), new Vector2(180, 34));
        var talentPoints = AddInput(p, LongYinTrainerPlugin.SkillTalentTierPointMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(190, 405), new Vector2(150, 34));

        AddText(p, "坐骑基础速度倍率", new Vector2(540, 195), new Vector2(190, 34));
        var horseBase = AddInput(p, LongYinTrainerPlugin.HorseBaseSpeedMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(745, 195), new Vector2(150, 34));
        AddText(p, "坐骑冲刺速度倍率", new Vector2(540, 237), new Vector2(190, 34));
        var horseSprint = AddInput(p, LongYinTrainerPlugin.HorseSprintSpeedMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(745, 237), new Vector2(150, 34));
        AddText(p, "冲刺持续时间倍率", new Vector2(540, 279), new Vector2(190, 34));
        var horseDuration = AddInput(p, LongYinTrainerPlugin.HorseSprintDurationMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(745, 279), new Vector2(150, 34));
        AddText(p, "冲刺冷却倍率", new Vector2(540, 321), new Vector2(190, 34));
        var horseCooldown = AddInput(p, LongYinTrainerPlugin.HorseSprintCooldownMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(745, 321), new Vector2(150, 34));
        AddText(p, "坐骑耐力倍率", new Vector2(540, 363), new Vector2(190, 34));
        var horseStamina = AddInput(p, LongYinTrainerPlugin.HorseStaminaMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(745, 363), new Vector2(150, 34));

        AddButton(p, "应用扩展设置", new Vector2(540, 405), new Vector2(355, 40), () =>
        {
            try
            {
                LongYinTrainerPlugin.BookExpMultiplier.Value = LongYinTrainerPlugin.SafeExpMultiplier(ParseInt(bookExp.text));
                LongYinTrainerPlugin.BattleSkillExpMultiplier.Value = LongYinTrainerPlugin.SafeExpMultiplier(ParseInt(battleExp.text));
                LongYinTrainerPlugin.CreationPointMultiplier.Value = LongYinTrainerPlugin.SafeCreationMultiplier(ParseInt(creationPoints.text));
                LongYinTrainerPlugin.BattleSpeedMultiplier.Value = LongYinTrainerPlugin.SafeSpeedMultiplier(ParseFloat(battleSpeed.text));
                LongYinTrainerPlugin.HorseBaseSpeedMultiplier.Value = LongYinTrainerPlugin.SafeHorseMultiplier(ParseFloat(horseBase.text));
                LongYinTrainerPlugin.HorseSprintSpeedMultiplier.Value = LongYinTrainerPlugin.SafeHorseMultiplier(ParseFloat(horseSprint.text));
                LongYinTrainerPlugin.HorseSprintDurationMultiplier.Value = LongYinTrainerPlugin.SafeHorseMultiplier(ParseFloat(horseDuration.text));
                LongYinTrainerPlugin.HorseSprintCooldownMultiplier.Value = LongYinTrainerPlugin.SafeHorseMultiplier(ParseFloat(horseCooldown.text));
                LongYinTrainerPlugin.HorseStaminaMultiplier.Value = LongYinTrainerPlugin.SafeHorseMultiplier(ParseFloat(horseStamina.text));
                LongYinTrainerPlugin.SkillTalentLevelThreshold.Value = LongYinTrainerPlugin.SafeSkillLevelThreshold(ParseInt(talentLevel.text));
                LongYinTrainerPlugin.SkillTalentTierPointMultiplier.Value = LongYinTrainerPlugin.SafeTalentPointMultiplier(ParseFloat(talentPoints.text));
                _status = $"扩展设置已应用：读书 ×{LongYinTrainerPlugin.BookExpMultiplier.Value}，战斗经验 ×{LongYinTrainerPlugin.BattleSkillExpMultiplier.Value}，开局点数 ×{LongYinTrainerPlugin.CreationPointMultiplier.Value}。";
            }
            catch (Exception ex) { _status = "扩展设置应用失败：" + ex.Message; }
        });

        AddText(p,
            "说明：全地图视野使用原生发现机制，已发现状态可能写入存档；关闭后不撤销发现。交互距离不变。战斗倍速是在游戏内点击倍速按钮后追加倍率；冲刺冷却低于 1 会缩短冷却。所有新功能默认关闭或 1 倍。",
            new Vector2(0, 475), new Vector2(1025, 68), 15);
    }

    public static void NotifyBreakThroughStarted(KungfuSkillLvData skill)
    {
        try
        {
            if (_breakChoiceSkillId != skill.skillID)
            {
                _breakChoiceSkillId = skill.skillID;
                _selectedBreakChoiceId = int.MinValue;
                _breakChoiceArmed = false;
                _breakChoicePage = 0;
            }
            _status = $"已识别突破功法：{skill.Name(false)}。打开“突破自选”页选择最终效果。";
            InvalidatePages(7);
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Break-through target capture skipped safely: {ex.Message}");
        }
    }

    private static KungfuSkillLvData? CurrentBreakThroughSkill()
    {
        try { return BreakThroughController.Instance?.targetSkill; }
        catch { return null; }
    }

    private static List<int> CurrentBreakThroughChoices(KungfuSkillLvData skill)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        try
        {
            var choices = skill.GetBreakThroughAvailableChoice();
            if (choices == null) return result;
            for (var i = 0; i < choices.Count; i++)
                if (seen.Add(choices[i])) result.Add(choices[i]);
        }
        catch (Exception ex)
        {
            _status = "读取突破候选失败：" + ex.Message;
        }
        return result;
    }

    private static HeroSpeAddDataBase? FindSpeAddDefinition(int id)
    {
        try
        {
            var database = GameDataController.Instance?.speAddDataBase;
            return database == null || id < 0 || id >= database.Count ? null : database[id];
        }
        catch { return null; }
    }

    private static string BreakChoiceName(int id, HeroSpeAddDataBase? definition = null)
    {
        definition ??= FindSpeAddDefinition(id);
        var description = DescribeSingleAffix(id, 0f);
        if (description.EndsWith("+0%", StringComparison.Ordinal)) description = description[..^3];
        else if (description.EndsWith("+0", StringComparison.Ordinal)) description = description[..^2];
        if (!string.IsNullOrWhiteSpace(description) && !description.StartsWith("其他：", StringComparison.Ordinal)) return description;
        if (!string.IsNullOrWhiteSpace(definition?.name)) return definition.name;
        return $"候选效果 ID {id}";
    }

    private static string BreakChoiceValue(HeroSpeAddDataBase definition, float multiplier, bool includeSign = true)
    {
        var appliedMultiplier = definition.noAutoUpgrade ? 1f : Math.Max(1f, multiplier);
        var value = definition.speValue * appliedMultiplier;
        var displayValue = definition.showPercent ? value * 100f : value;
        var sign = includeSign && displayValue >= 0f ? "+" : "";
        return $"{sign}{displayValue.ToString("0.###", CultureInfo.InvariantCulture)}{(definition.showPercent ? "%" : "")}";
    }

    private static string BreakChoiceLabel(int id, float multiplier = 1f)
    {
        var definition = FindSpeAddDefinition(id);
        if (definition == null) return $"候选效果 ID {id}";
        return $"{BreakChoiceName(id, definition)}{BreakChoiceValue(definition, multiplier)}";
    }

    private static string BreakChoiceButtonLabel(int id)
    {
        var definition = FindSpeAddDefinition(id);
        if (definition == null) return $"候选效果 ID {id}";
        var name = BreakChoiceName(id, definition);
        if (definition.noAutoUpgrade) return $"{name}：固定 {BreakChoiceValue(definition, 1f)}";
        return $"{name}：每阶 {BreakChoiceValue(definition, 1f)}｜红卡 {BreakChoiceValue(definition, 5f)}";
    }

    private static string BreakChoiceLockSummary(int id)
    {
        var definition = FindSpeAddDefinition(id);
        if (definition == null) return BreakChoiceLabel(id);
        var name = BreakChoiceName(id, definition);
        return definition.noAutoUpgrade
            ? $"{name}：固定 {BreakChoiceValue(definition, 1f)}（不随卡牌稀有度变化）"
            : $"{name}：每阶 {BreakChoiceValue(definition, 1f)}；最终数值取决于所点卡牌（红卡 {BreakChoiceValue(definition, 5f)}）";
    }

    private static void BuildNativeBreakThroughChoice()
    {
        var p = NativePageParent;
        var skill = CurrentBreakThroughSkill();
        AddText(p, "功法突破自选", Vector2.zero, new Vector2(400, 38), 22);
        if (skill == null)
        {
            AddText(p, "请先在游戏的功法界面点击“突破”，打开图一所示的突破准备页。", new Vector2(0, 55), new Vector2(1020, 48), 18);
            AddText(p, "然后重新点击本页签刷新；模组会读取该功法真正允许出现的完整候选池。", new Vector2(0, 105), new Vector2(1020, 48), 17);
            return;
        }

        var choices = CurrentBreakThroughChoices(skill);
        if (_breakChoiceSkillId != skill.skillID)
        {
            _breakChoiceSkillId = skill.skillID;
            _selectedBreakChoiceId = int.MinValue;
            _breakChoiceArmed = false;
            _breakChoicePage = 0;
        }
        if (_selectedBreakChoiceId != int.MinValue && !choices.Contains(_selectedBreakChoiceId))
        {
            _selectedBreakChoiceId = int.MinValue;
            _breakChoiceArmed = false;
        }

        AddText(p, $"当前功法：{skill.Name(false)}　等级 {skill.lv}　完整候选 {choices.Count} 项", new Vector2(0, 44), new Vector2(1020, 38), 18);
        var selectedText = AddText(p,
            _selectedBreakChoiceId == int.MinValue
                ? "尚未指定；选择下方一项即可锁定下一次突破。"
                : $"已{(_breakChoiceArmed ? "锁定" : "选择")}：{BreakChoiceLockSummary(_selectedBreakChoiceId)}",
            new Vector2(0, 82), new Vector2(1020, 38), 17);

        const int pageSize = 15;
        var pageCount = Math.Max(1, (choices.Count + pageSize - 1) / pageSize);
        _breakChoicePage = Math.Clamp(_breakChoicePage, 0, pageCount - 1);
        var first = _breakChoicePage * pageSize;
        var last = Math.Min(first + pageSize, choices.Count);
        for (var i = first; i < last; i++)
        {
            var choiceId = choices[i];
            var localId = choiceId;
            var localLabel = BreakChoiceButtonLabel(choiceId);
            var relative = i - first;
            var col = relative % 3;
            var row = relative / 3;
            var button = AddButton(p, localLabel, new Vector2(col * 355, 130 + row * 54), new Vector2(340, 44), () =>
            {
                _selectedBreakChoiceId = localId;
                _breakChoiceArmed = true;
                var summary = BreakChoiceLockSummary(localId);
                selectedText.text = $"已锁定：{summary}";
                _status = $"突破效果已锁定为“{summary}”。进入三张卡页面后，最终数值继承你所点击卡牌的稀有度。";
            });
            if (choiceId == _selectedBreakChoiceId)
            {
                button.GetComponent<Image>().color = new Color(0.70f, 0.43f, 0.08f, 1f);
                button.GetComponentInChildren<Text>().color = new Color(1f, 0.88f, 0.48f, 1f);
            }
        }

        AddButton(p, "上一页", new Vector2(0, 424), new Vector2(160, 38), () => { if (_breakChoicePage > 0) _breakChoicePage--; RequestPageRebuild(); });
        AddText(p, $"第 {_breakChoicePage + 1} / {pageCount} 页", new Vector2(175, 424), new Vector2(180, 38), 17, TextAnchor.MiddleCenter);
        AddButton(p, "下一页", new Vector2(370, 424), new Vector2(160, 38), () => { if (_breakChoicePage + 1 < pageCount) _breakChoicePage++; RequestPageRebuild(); });
        AddButton(p, "取消锁定", new Vector2(720, 424), new Vector2(340, 38), () =>
        {
            _breakChoiceArmed = false;
            _selectedBreakChoiceId = int.MinValue;
            selectedText.text = "已取消；本次突破使用游戏原始随机结果。";
            _status = "已取消突破自选。";
        });
        AddText(p,
            "突破效率决定三张卡会生成的稀有度，不会把候选预先固定为 5 阶。这里显示“每阶值”和“红卡值”；锁定只替换词条种类，最终数值继承你实际点击的卡牌（例如每阶 1%，点红卡就是 5%）。伤势代价和原版结算不变。当前版本不改卡面文字，以状态栏和日志中的最终数值为准。",
            new Vector2(0, 485), new Vector2(1060, 92), 15);
    }

    public static void OverrideBreakThroughChoice(BreakThroughChoiceController choice)
    {
        if (!_breakChoiceArmed || _selectedBreakChoiceId == int.MinValue) return;
        try
        {
            var skill = CurrentBreakThroughSkill();
            if (skill == null) throw new InvalidOperationException("当前突破功法已失效");
            var available = CurrentBreakThroughChoices(skill);
            if (!available.Contains(_selectedBreakChoiceId)) throw new InvalidOperationException("所选效果不在当前功法的候选池中");
            var data = choice.extraAddData ?? throw new InvalidOperationException("突破卡没有可替换的效果数据");
            var keys = data.GetKeys();
            if (keys == null || keys.Count == 0) throw new InvalidOperationException("突破卡效果为空");

            var originalId = keys[0];
            var originalValue = data.Get(originalId);
            var originalDefinition = FindSpeAddDefinition(originalId);
            var selectedDefinition = FindSpeAddDefinition(_selectedBreakChoiceId)
                ?? throw new InvalidOperationException("找不到所选效果定义");
            var cardMultiplier = Math.Max(1f, choice.rareLv);
            if (originalDefinition != null && !originalDefinition.noAutoUpgrade && Math.Abs(originalDefinition.speValue) > 0.000001f)
            {
                var storedMultiplier = originalValue / originalDefinition.speValue;
                if (float.IsFinite(storedMultiplier) && storedMultiplier > 0f) cardMultiplier = storedMultiplier;
            }
            var selectedMultiplier = selectedDefinition.noAutoUpgrade ? 1f : cardMultiplier;
            var selectedValue = selectedDefinition.speValue * selectedMultiplier;
            if (!float.IsFinite(selectedValue)) throw new InvalidOperationException("计算出的突破效果数值无效");

            data.Reset();
            data.Set(_selectedBreakChoiceId, selectedValue);
            var label = BreakChoiceLabel(_selectedBreakChoiceId, selectedMultiplier);
            _breakChoiceArmed = false;
            _status = selectedDefinition.noAutoUpgrade
                ? $"突破自选已应用：{label}（该词条为固定值，不随卡牌稀有度升级）。"
                : $"突破自选已应用：{label}（卡牌稀有度倍率 ×{cardMultiplier:0.###}）。";
            LongYinTrainerPlugin.Logger.LogInfo($"Break-through choice overridden safely: skillID={skill.skillID}, originalID={originalId}, selectedID={_selectedBreakChoiceId}, cardMultiplier={cardMultiplier}, selectedMultiplier={selectedMultiplier}, storedValue={selectedValue}.");
            InvalidatePages(7);
        }
        catch (Exception ex)
        {
            _breakChoiceArmed = false;
            _status = "突破自选失败，已保留原卡效果：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Break-through choice override skipped safely: {ex.Message}");
        }
    }

    public static void Render()
    {
        if (!_visible) return;
        if (_lastGuiFrame == Time.frameCount) return;
        _lastGuiFrame = Time.frameCount;
        GUI.depth = -10000;
        var width = Math.Min(960f, Screen.width - 30f);
        var height = Math.Min(720f, Screen.height - 30f);
        var panel = new Rect(15, 15, width, height);
        GUI.Box(panel, $"LongYin Trainer 0.1   [{LongYinTrainerPlugin.ToggleKey.Value} close]");

        var tabs = new[] { "Battle", "Hero", "Inventory", "Equipment Affix", "Faction/Create" };
        for (var i = 0; i < tabs.Length; i++)
            if (GUI.Button(new Rect(28 + i * 178, 48, 170, 30), tabs[i])) _tab = i;

        try
        {
            switch (_tab)
            {
                case 0: DrawBattle(); break;
                case 1: DrawHero(); break;
                case 2: DrawItem(); break;
                case 3: DrawAffix(); break;
                case 4: DrawFactionAndCreation(); break;
            }
        }
        catch (Exception ex)
        {
            _status = "界面错误：" + ex.Message;
        }
        GUI.Box(new Rect(28, height - 48, width - 56, 30), _status);
    }

    private static Rect R(int col, int row, float w = 400f) => new(35 + col * 450, 95 + row * 38, w, 29);

    private static void DrawBattle()
    {
        InfiniteHp = GUI.Toggle(R(0, 0), InfiniteHp, " Infinite HP / Stamina");
        InfiniteMana = GUI.Toggle(R(0, 1), InfiniteMana, " Infinite Mana");
        InfinitePower = GUI.Toggle(R(0, 2), InfinitePower, " Infinite Power / Endurance");
        ZeroExternal = GUI.Toggle(R(0, 3), ZeroExternal, " Zero External Injury");
        ZeroInternal = GUI.Toggle(R(0, 4), ZeroInternal, " Zero Internal Injury");
        ZeroPoison = GUI.Toggle(R(0, 5), ZeroPoison, " Zero Poison Injury");
        MaxMove = GUI.Toggle(R(0, 6), MaxMove, " Maximum Battle Move Range");
        FastCharge = GUI.Toggle(R(0, 7), FastCharge, " Fast Skill Charge");
        FastCooldown = GUI.Toggle(R(0, 8), FastCooldown, " Fast Skill Cooldown");

        GUI.Label(R(0, 10, 200), "World map speed");
        WorldSpeed = GUI.HorizontalSlider(new Rect(210, 480, 210, 25), WorldSpeed, 1f, 20f);
        GUI.Label(new Rect(430, 473, 80, 29), WorldSpeed.ToString("0.0") + "x");

        NoEquipmentWeight = GUI.Toggle(R(1, 0), NoEquipmentWeight, " Equipment Weight = 0");
        NoInventoryWeight = GUI.Toggle(R(1, 1), NoInventoryWeight, " Inventory Weight = 0");
        UnlimitedSkillExp = GUI.Toggle(R(1, 2), UnlimitedSkillExp, " Unlimited / x1000 Skill EXP");
        MaxFavor = GUI.Toggle(R(1, 3), MaxFavor, " Selected Hero Maximum Favor");
        UnlimitedCreationPoints = GUI.Toggle(R(1, 4), UnlimitedCreationPoints, " New Game Unlimited Points");
        GUI.Label(R(1, 6), "Tip: click an inventory icon to select an item.");
        GUI.Label(R(1, 7), "Open a hero detail page to select that hero.");
        GUI.Label(R(1, 8), "Changes only affect the current local game/save.");
    }

    private static HeroData? TargetHero()
    {
        HeroData? hero = null;
        try
        {
            var world = GameController.Instance?.worldData;
            if (_selectedHeroId != int.MinValue && world != null) hero = world.GetHero(_selectedHeroId);
        }
        catch { hero = null; }
        hero ??= Player;
        if (hero != null && hero.heroID != _fieldHeroId) InitHeroFields(hero);
        return hero;
    }

    private static void InitHeroFields(HeroData h)
    {
        _fieldHeroId = h.heroID;
        Fields["Money"] = (h.itemListData?.money ?? 0).ToString(CultureInfo.InvariantCulture);
        Fields["Fame"] = F(h.fame); Fields["BadFame"] = F(h.badFame); Fields["Favor"] = F(h.favor);
        Fields["Govern"] = F(h.governContribution); Fields["ForceContrib"] = F(h.forceContribution);
        Fields["HP"] = F(h.hp); Fields["MaxHP"] = F(h.maxhp); Fields["Mana"] = F(h.mana); Fields["MaxMana"] = F(h.maxMana);
        Fields["Power"] = F(h.power); Fields["MaxPower"] = F(h.maxPower);
        Fields["External"] = F(h.externalInjury); Fields["Internal"] = F(h.internalInjury); Fields["Poison"] = F(h.poisonInjury);
        Fields["Loyal"] = F(h.loyal); Fields["Evil"] = F(h.evil); Fields["Chaos"] = F(h.chaos); Fields["Nature"] = h.nature.ToString();
    }

    private static void DrawHero()
    {
        var h = TargetHero();
        if (h == null) { GUI.Label(R(0, 0), "Load a save first."); return; }
        GUI.Label(new Rect(35, 82, 850, 28), $"Target: {h.heroName} (ID {h.heroID})");
        for (var i = 0; i < HeroKeys.Length; i++)
        {
            var col = i / 10;
            var row = i % 10;
            var x = 35 + col * 450;
            var y = 115 + row * 38;
            GUI.Label(new Rect(x, y, 130, 28), HeroKeys[i]);
            Fields[HeroKeys[i]] = GUI.TextField(new Rect(x + 135, y, 230, 28), Fields[HeroKeys[i]]);
        }
        if (GUI.Button(new Rect(35, 510, 365, 34), "Apply all hero numeric fields")) ApplyHero(h);
        if (GUI.Button(new Rect(485, 510, 110, 34), "Use player")) SelectHero(Player ?? h);
        if (GUI.Button(new Rect(605, 510, 110, 34), "Refresh")) InitHeroFields(h);
        GUI.Label(new Rect(35, 560, 120, 28), "All attributes");
        _allAttr = GUI.TextField(new Rect(155, 560, 90, 28), _allAttr);
        if (GUI.Button(new Rect(250, 560, 130, 28), "Set base + cap")) SetAll(h.baseAttri, h.maxAttri, _allAttr);
        GUI.Label(new Rect(400, 560, 110, 28), "All combat");
        _allFight = GUI.TextField(new Rect(510, 560, 90, 28), _allFight);
        if (GUI.Button(new Rect(605, 560, 130, 28), "Set base + cap")) SetAll(h.baseFightSkill, h.maxFightSkill, _allFight);
        GUI.Label(new Rect(35, 600, 120, 28), "All life skills");
        _allLiving = GUI.TextField(new Rect(155, 600, 90, 28), _allLiving);
        if (GUI.Button(new Rect(250, 600, 130, 28), "Set base + cap")) SetAll(h.baseLivingSkill, h.maxLivingSkill, _allLiving);
    }

    private static void ApplyHero(HeroData h)
    {
        try
        {
            if (h.itemListData != null) h.itemListData.money = I("Money");
            h.fame = V("Fame"); h.badFame = V("BadFame"); h.favor = V("Favor");
            h.governContribution = V("Govern"); h.forceContribution = V("ForceContrib");
            h.hp = V("HP"); h.maxhp = V("MaxHP"); h.mana = V("Mana"); h.maxMana = V("MaxMana");
            h.power = V("Power"); h.maxPower = V("MaxPower");
            h.externalInjury = V("External"); h.internalInjury = V("Internal"); h.poisonInjury = V("Poison");
            h.loyal = V("Loyal"); h.evil = V("Evil"); h.chaos = V("Chaos"); h.nature = I("Nature");
            h.CheckHeroDetailDirty(true);
            RefreshHero(h);
            _status = "角色数值已应用。";
        }
        catch (Exception ex) { _status = "角色数值应用失败：" + ex.Message; }
    }

    private static void DrawItem()
    {
        var item = ResolveSelectedItem();
        if (item == null) { GUI.Label(R(0, 0), "Click an item icon in the inventory first."); return; }
        GUI.Label(R(0, 0, 850), $"Selected: {item.Name(false)} | itemID {item.itemID} | type {item.type} | subType {item.subType}");
        DrawTextRow("Name", ref _itemName, 1);
        DrawTextRow("Value", ref _itemValue, 2);
        DrawTextRow("Item level", ref _itemLevel, 3);
        DrawTextRow("Rarity level", ref _itemRare, 4);
        DrawTextRow("Weight", ref _itemWeight, 5);
        DrawTextRow("Poison", ref _itemPoison, 6);
        if (GUI.Button(R(0, 8, 300), "Apply selected item fields")) ApplyItem(item);
        GUI.Label(R(1, 1, 130), "Duplicate count");
        _duplicateCount = GUI.TextField(new Rect(620, 133, 100, 29), _duplicateCount);
        if (GUI.Button(R(1, 2, 300), "Duplicate into player backpack")) DuplicateItem(item);
        if (GUI.Button(R(1, 4, 300), "Delete selected from player backpack")) DeleteItem(item);
        GUI.Label(R(1, 6, 390), "Identity fields are intentionally read-only to avoid broken items.");
        GUI.Label(R(1, 7, 390), "For equipment properties, use the Equipment Affix tab.");
    }

    private static void DrawTextRow(string label, ref string value, int row)
    {
        GUI.Label(R(0, row, 120), label);
        value = GUI.TextField(new Rect(170, 95 + row * 38, 260, 29), value);
    }

    private static void ApplyItem(ItemData item)
    {
        try
        {
            var rarity = ParseInt(_itemRare);
            if (rarity < 0 || rarity > 5) throw new InvalidOperationException("稀有度只能填写 0–5");
            var level = ParseInt(_itemLevel);
            if (level < 0 || level > 999) throw new InvalidOperationException("等级只能填写 0–999");
            // Validate every input before changing the native item.
            var value = Math.Max(0, ParseInt(_itemValue));
            var weight = ParseFloat(_itemWeight);
            var poison = ParseFloat(_itemPoison);
            if (!float.IsFinite(weight) || !float.IsFinite(poison)) throw new InvalidOperationException("重量和毒量必须是有限数字");
            item.name = _itemName;
            item.value = value;
            item.itemLv = level;
            item.rareLv = rarity;
            item.weight = Math.Max(0f, weight);
            item.poisonNum = Math.Max(0f, poison);
            _itemValue = item.value.ToString(CultureInfo.InvariantCulture);
            _itemLevel = item.itemLv.ToString(CultureInfo.InvariantCulture);
            _itemRare = item.rareLv.ToString(CultureInfo.InvariantCulture);
            _itemWeight = item.weight.ToString(CultureInfo.InvariantCulture);
            _itemPoison = item.poisonNum.ToString(CultureInfo.InvariantCulture);
            ItemChanged(item);
            _status = $"物品数值已应用：等级 {level}，稀有度 {RarityName(rarity)}。";
        }
        catch (Exception ex) { _status = "物品数值应用失败：" + ex.Message; }
    }

    private static void DuplicateItem(ItemData item)
    {
        try
        {
            var player = Player ?? throw new InvalidOperationException("尚未载入主角数据");
            var count = Math.Clamp(ParseInt(_duplicateCount), 1, 99);
            for (var i = 0; i < count; i++)
            {
                var clone = item.Clone()?.TryCast<ItemData>() ?? throw new InvalidOperationException("物品克隆失败");
                clone.isNew = true;
                player.GetItem(clone, false, false, 0, true);
            }
            RefreshHero(player);
            _status = $"已复制 {count} 件物品到主角背包。";
        }
        catch (Exception ex) { _status = "复制物品失败：" + ex.Message; }
    }

    private static void DeleteItem(ItemData item)
    {
        try
        {
            var player = Player ?? throw new InvalidOperationException("尚未载入主角数据");
            if (item.Equiped()) throw new InvalidOperationException("请先卸下该装备");
            player.itemListData.LoseItem(item, false);
            ClearSelectedItem();
            RefreshHero(player);
            _status = "所选物品已从主角背包删除。";
        }
        catch (Exception ex) { _status = "删除物品失败：" + ex.Message; }
    }

    private static void DrawAffix()
    {
        var item = ResolveSelectedItem();
        if (item?.horseData != null) { GUI.Label(R(0, 0), "Horse and saddle editing is available in the native trainer panel."); return; }
        if (item?.equipmentData == null) { GUI.Label(R(0, 0), "Select a weapon, armor, horse, or saddle first."); return; }
        var equip = item.equipmentData;
        GUI.Label(R(0, 0, 850), $"Equipment: {item.Name(false)} | enhance {equip.enhanceLv} | rarity {item.rareLv}");
        GUI.Label(R(0, 1, 120), "Affix type");
        _affixType = GUI.TextField(new Rect(170, 133, 260, 29), _affixType);
        GUI.Label(R(0, 2, 120), "Affix value");
        _affixValue = GUI.TextField(new Rect(170, 171, 260, 29), _affixValue);
        if (GUI.Button(R(0, 3, 190), "Set BASE affix")) SetAffix(false, item);
        if (GUI.Button(new Rect(235, 209, 195, 29), "Set EXTRA affix")) SetAffix(true, item);
        if (GUI.Button(R(0, 4, 190), "Clear BASE affixes")) { equip.baseAddData.Reset(); ItemChanged(item); }
        if (GUI.Button(new Rect(235, 247, 195, 29), "Clear EXTRA affixes")) { equip.extraAddData.Reset(); ItemChanged(item); }
        GUI.Label(new Rect(35, 295, 405, 25), "Base affixes");
        GUI.TextArea(new Rect(35, 320, 405, 190), equip.baseAddData.GetDescribe(false, true, 2, false));
        GUI.Label(new Rect(475, 295, 405, 25), "Extra affixes");
        GUI.TextArea(new Rect(475, 320, 405, 190), equip.extraAddData.GetDescribe(false, true, 2, false));
        GUI.Label(new Rect(475, 95, 380, 25), "Quick affix names (click to load):");
        for (var i = 0; i < AffixPresets.Length; i++)
        {
            var col = i % 3;
            var row = i / 3;
            if (GUI.Button(new Rect(475 + col * 135, 125 + row * 31, 128, 27), AffixPresets[i])) _affixType = AffixPresets[i];
        }
        GUI.Label(new Rect(35, 525, 850, 48), "Affix type accepts an enum name (for example maxHp, damage, critRate, moveRange) or its numeric ID. Values are written to the real equipment data.");
    }

    private static void SetAffix(bool extra, ItemData item)
    {
        try
        {
            if (Time.realtimeSinceStartup < _nextAffixWriteAt)
            {
                _status = "操作过快：请稍等片刻再写入，避免游戏连续重算装备。";
                return;
            }
            var equipment = item.equipmentData ?? throw new InvalidOperationException("所选物品已不再是装备");
            var data = extra ? equipment.extraAddData : equipment.baseAddData;
            int id;
            if (!int.TryParse(_affixType, out id))
            {
                if (!Enum.TryParse<HeroSpeAddDataType>(_affixType, true, out var parsed)) throw new InvalidOperationException("未知的词条类型");
                id = (int)parsed;
            }
            var displayValue = ParseFloat(_affixValue);
            var percent = IsPercentageAffix(id);
            var value = percent ? displayValue / 100f : displayValue;
            var keys = data.GetKeys();
            var exists = false;
            if (keys != null)
                for (var i = 0; i < keys.Count; i++)
                    if (keys[i] == id) { exists = true; break; }

            var otherData = extra ? equipment.baseAddData : equipment.extraAddData;
            var otherKeys = otherData.GetKeys();
            var duplicateOnOtherSide = false;
            if (otherKeys != null)
                for (var i = 0; i < otherKeys.Count; i++)
                    if (otherKeys[i] == id) { duplicateOnOtherSide = true; break; }

            if (exists && !duplicateOnOtherSide && Math.Abs(data.Get(id) - value) < 0.0001f)
            {
                _status = "该词条已经是这个数值，没有重复写入。";
                return;
            }

            _nextAffixWriteAt = Time.realtimeSinceStartup + 0.25f;
            if (duplicateOnOtherSide) otherData.heroSpeAddData.Remove(id);
            data.Set(id, value);
            ItemChanged(item);
            _status = $"已将{(extra ? "额外" : "基础")}词条 {_affixType} 设为 {_affixValue}{(percent ? "%（底层保存 " + value.ToString("0.###", CultureInfo.InvariantCulture) + "）" : "")}{(duplicateOnOtherSide ? "，并移除另一栏的同名词条" : "")}。";
            LongYinTrainerPlugin.Logger.LogInfo($"Affix write completed safely: itemID={_selectedItemId}, extra={extra}, key={id}, displayValue={displayValue}, storedValue={value}.");
            RequestPageRebuild();
        }
        catch (Exception ex) { _status = "词条修改失败：" + ex.Message; }
    }

    private static bool ApplyHorseStats(ItemData item, string speedText, string sprintText, string powerText,
        string resistText, string? nowPowerText, string? favorRateText)
    {
        try
        {
            var horse = item.horseData ?? throw new InvalidOperationException("所选物品已不再是马匹或鞍具");
            horse.speed = Math.Clamp(ParseFloat(speedText), -100000f, 100000f);
            horse.sprint = Math.Clamp(ParseFloat(sprintText), -100000f, 100000f);
            horse.power = Math.Clamp(ParseFloat(powerText), -100000f, 100000f);
            horse.resist = Math.Clamp(ParseFloat(resistText), -100000f, 100000f);
            if (item.subType != 1)
            {
                if (favorRateText != null)
                    horse.favorRate = Math.Clamp(ParseFloat(favorRateText) / 100f, 0f, 1f);
                if (nowPowerText != null)
                    horse.nowPower = Math.Max(0f, ParseFloat(nowPowerText));
                horse.RefreshState();
            }
            ItemChanged(item);
            var itemKind = item.subType == 1 ? "鞍具" : "马匹";
            _status = $"{itemKind}数值已写入；装备状态将在本次操作结束后安全刷新。";
            LongYinTrainerPlugin.Logger.LogInfo($"Horse data write completed safely: itemID={item.itemID}, subType={item.subType}, speed={horse.speed}, sprint={horse.sprint}, power={horse.power}, resist={horse.resist}.");
            return true;
        }
        catch (Exception ex)
        {
            _status = "马匹／鞍具修改失败：" + ex.Message;
            return false;
        }
    }

    private static void ItemChanged(ItemData item)
    {
        item.isNew = true;
        // Collapse a burst of edits into one later dirty notification. Repeated
        // CheckHeroDetailDirty calls force NGUI to rebuild the open bag while
        // our button callback is still using its wrappers, which reproduces the
        // GameAssembly.dll access violation seen in the crash log.
        _pendingItemRefreshOwnerId = _selectedItemOwnerId;
        _pendingItemRefreshAt = Time.realtimeSinceStartup + 0.5f;
        if (item.horseData != null) _pendingHorseStateRefresh = true;
    }

    private static void FlushPendingItemRefresh()
    {
        if (_pendingItemRefreshOwnerId == int.MinValue || Time.realtimeSinceStartup < _pendingItemRefreshAt) return;
        var ownerId = _pendingItemRefreshOwnerId;
        _pendingItemRefreshOwnerId = int.MinValue;
        try
        {
            var owner = GameController.Instance?.worldData?.GetHero(ownerId) ?? Player;
            if (_pendingHorseStateRefresh)
            {
                _pendingHorseStateRefresh = false;
                SyncEquippedHorseArmor(owner);
            }
            RefreshHero(owner);
            LongYinTrainerPlugin.Logger.LogInfo($"Batched equipment refresh completed safely: owner={ownerId}.");
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogDebug($"Batched equipment refresh skipped safely: {ex.Message}");
        }
    }

    private static void SyncEquippedHorseArmor(HeroData? owner)
    {
        if (owner?.horse?.horseData == null) return;
        var equippedHorse = owner.horse.horseData;
        var equippedSaddle = owner.horseArmor?.horseData;
        equippedHorse.speedAdd = equippedSaddle?.speed ?? 0f;
        equippedHorse.sprintAdd = equippedSaddle?.sprint ?? 0f;
        equippedHorse.powerAdd = equippedSaddle?.power ?? 0f;
        equippedHorse.resistAdd = equippedSaddle?.resist ?? 0f;
        equippedHorse.RefreshState();
        LongYinTrainerPlugin.Logger.LogInfo($"Equipped saddle bonuses synchronized safely: owner={owner.heroID}, saddle={owner.horseArmor?.Name(false) ?? "none"}.");
    }

    private static void DrawFactionAndCreation()
    {
        var h = TargetHero();
        var force = h?.GetForce(true);
        GUI.Label(R(0, 0, 850), force == null ? "Selected hero has no faction." : $"Faction: {force.forceName} (ID {force.forceID})");
        GUI.Label(R(0, 1, 140), "Faction money");
        _forceMoney = GUI.TextField(new Rect(180, 133, 180, 29), _forceMoney);
        for (var i = 0; i < ResourceValues.Count; i++)
        {
            GUI.Label(R(0, i + 2, 140), $"Resource index {i}");
            ResourceValues[i] = GUI.TextField(new Rect(180, 95 + (i + 2) * 38, 180, 29), ResourceValues[i]);
        }
        if (GUI.Button(R(0, 8, 325), "Apply faction money + resources")) ApplyForce(force);
        UnlimitedCreationPoints = GUI.Toggle(R(1, 1), UnlimitedCreationPoints, " New game: unlimited point pools");
        GUI.Label(R(1, 2, 400), "Locks attribute/combat/life remaining points at 999.");
        GUI.Label(R(1, 4, 400), "Character attribute and skill caps can be changed");
        GUI.Label(R(1, 5, 400), "from the Hero tab after loading a save.");
        GUI.Label(R(1, 7, 400), "Resource indices follow the game's internal order.");
    }

    private static void ApplyForce(ForceData? force)
    {
        try
        {
            if (force == null) throw new InvalidOperationException("没有选中的势力");
            if (GameController.Instance?.worldData?.GetForce(force.forceID)?.Pointer != force.Pointer)
                throw new InvalidOperationException("势力已改变，请重新打开势力页面。");
            var moneyValue = ParseInt(_forceMoney);
            if (moneyValue < 0) throw new InvalidOperationException("金钱不能为负数");
            var resourceStore = force.resourceStore;
            var count = Math.Min(ResourceValues.Count, resourceStore?.Count ?? 0);
            var values = new float[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = ParseFloat(ResourceValues[i]);
                if (!float.IsFinite(values[i]) || values[i] < 0) throw new InvalidOperationException("资源必须是非负有限数值");
            }
            if (force.forceStorage != null) force.forceStorage.money = moneyValue;
            if (resourceStore != null)
                for (var i = 0; i < count; i++) resourceStore[i] = values[i];
            if (IsForceLocked(force)) CaptureForceLock(force);
            force.forceDetailDirty = true;
            _status = "势力数值已应用。";
        }
        catch (Exception ex) { _status = "势力数值应用失败：" + ex.Message; }
    }

    private static void SetAll(Il2CppSystem.Collections.Generic.List<float> baseValues, Il2CppSystem.Collections.Generic.List<float> caps, string text)
    {
        try
        {
            var value = ParseFloat(text);
            for (var i = 0; i < baseValues.Count; i++) baseValues[i] = value;
            for (var i = 0; i < caps.Count; i++) caps[i] = value;
            TargetHero()?.CheckHeroDetailDirty(true);
            RefreshHero(TargetHero());
            _status = $"已将 {baseValues.Count} 项基础值和 {caps.Count} 项上限设为 {value}。";
        }
        catch (Exception ex) { _status = "批量修改失败：" + ex.Message; }
    }

    private static void RefreshHero(HeroData? hero)
    {
        try
        {
            if (hero == null) return;
            hero.CheckHeroDetailDirty(true);
            // Do not re-enter HeroDetailController while its inventory/detail
            // callbacks may still be executing.  The game refreshes dirty hero
            // data on its own next UI pass.
        }
        catch { }
    }

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static float V(string key) => ParseFloat(Fields[key]);
    private static int I(string key) => ParseInt(Fields[key]);
    private static float ParseFloat(string value)
    {
        var result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!float.IsFinite(result)) throw new FormatException("数值不能是 NaN 或无穷大");
        return result;
    }
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

