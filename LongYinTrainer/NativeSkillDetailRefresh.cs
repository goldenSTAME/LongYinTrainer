using System;
using HarmonyLib;
using UnityEngine;

// Rebuild the native tooltip when Shift changes while the pointer stays still.
internal static class NativeSkillDetailRefresh
{
    private static GameObject? target;
    private static KungfuSkillLvData? skill;
    private static bool showUseTime;
    private static bool refreshing;
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(QuickDetail), "ShowSkillQuickDetail"),
            prefix: new HarmonyMethod(typeof(NativeSkillDetailRefresh), nameof(Prepare)),
            postfix: new HarmonyMethod(typeof(NativeSkillDetailRefresh), nameof(Remember)),
            finalizer: new HarmonyMethod(typeof(NativeSkillDetailRefresh), nameof(Restore)));
        harmony.Patch(AccessTools.Method(typeof(QuickDetail), "Update"),
            postfix: new HarmonyMethod(typeof(NativeSkillDetailRefresh), nameof(Refresh)));
    }
    private sealed class CreatorScope
    {
        internal Il2CppSystem.Collections.Generic.Dictionary<int, CustomSkillData> Records = null!;
        internal int Id;
        internal CustomSkillData Record = null!;
    }
    private static void Prepare(KungfuSkillLvData __1, out CreatorScope? __state)
    {
        __state = null;
        if (__1 == null || __1.skillID < GlobalData.CustomSkillIDStart) return;
        var records = GameController.Instance?.worldData?.customSkills;
        if (records == null || records.ContainsKey(__1.skillID)) return;
        var data = __1.DataBase();
        if (data == null) return;
        var record = new CustomSkillData { skillData = data, createHeroName = "模组", createByPlayer = false };
        records.Add(__1.skillID, record);
        __state = new CreatorScope { Records = records, Id = __1.skillID, Record = record };
    }
    private static Exception? Restore(Exception? __exception, CreatorScope? __state)
    {
        if (__state != null && __state.Records.ContainsKey(__state.Id) && __state.Records[__state.Id]?.Pointer == __state.Record.Pointer)
            __state.Records.Remove(__state.Id);
        return __exception;
    }
    private static void Remember(GameObject __0, KungfuSkillLvData __1, bool __2)
    {
        target = __0; skill = __1; showUseTime = __2;
    }
    private static void Refresh(QuickDetail __instance)
    {
        if (refreshing) return;
        if (__instance.skillDetail == null || !__instance.skillDetail.activeInHierarchy || target == null || !target.activeInHierarchy)
        { target = null; skill = null; return; }
        if (skill == null || !(Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.LeftShift))) return;
        try {
            refreshing = true;
            __instance.ShowSkillQuickDetail(target, skill, showUseTime);
            LongYinTrainerPlugin.Logger.LogInfo("Native skill tooltip refreshed for Shift; skill=" + skill.skillID);
        } catch (Exception ex) {
            LongYinTrainerPlugin.Logger.LogWarning("Native skill tooltip refresh failed: " + ex);
        } finally { refreshing = false; }
    }
}
