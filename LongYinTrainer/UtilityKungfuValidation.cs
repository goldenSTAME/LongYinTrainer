using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using BepInEx;

public static partial class TrainerBehaviour
{
    private static void AssertEffectTable(HeroSpeAddData? expected, HeroSpeAddData? actual, string label)
    {
        var keys = new HashSet<int>();
        void Collect(HeroSpeAddData? table)
        {
            var ids = table?.GetKeys();
            if (ids != null) for (var i = 0; i < ids.Count; i++) keys.Add(ids[i]);
        }
        Collect(expected); Collect(actual);
        foreach (var key in keys)
        {
            var a = expected?.Get(key) ?? 0f;
            var b = actual?.Get(key) ?? 0f;
            if (!float.IsFinite(a) || !float.IsFinite(b) || Math.Abs(a - b) > 0.00001f * Math.Max(1f, Math.Abs(a)))
                throw new InvalidOperationException($"{label}词条 {key} 重建不一致：{a} / {b}");
        }
    }

    // A detached native level object exercises the same getters/reset/timer as
    // battle, without attaching it to a hero or touching a live combat state.
    private static void ValidateUtilityNativeDefinition(GameDataController database, CustomKungfuRecord record, KungfuSkillData definition)
    {
        if (record.Type < 0 || record.Type > 2) return;
        var definitions = database.kungfuSkillDataBase;
        var probeId = CustomKungfuIdMax;
        while (probeId >= CustomKungfuIdMin && (definitions.ContainsKey(probeId) || FindSavedCustomKungfu(probeId) != null)) probeId--;
        if (probeId < CustomKungfuIdMin) throw new InvalidOperationException("没有空闲功法 ID 可用于原生验证");
        definitions[probeId] = definition;
        try
        {
            var level = new KungfuSkillLvData(record.BaseDonorId);
            level.skillID = probeId;
            level.extraAddData = new HeroSpeAddData();
            foreach (var lv in new[] { 1, 10 })
            {
                level.lv = lv;
                level.activeTimeLeft = 0f;
                KungfuSkillRefresh.RefreshSkill(level);
                if (level.DataBase()?.Pointer != definition.Pointer)
                    throw new InvalidOperationException("原生等级实例未读取到修改后的定义");
                var equip = definition.GetSpeEquipData(lv);
                AssertEffectTable(equip, level.speEquipData, "装备缓存");
                AssertEffectTable(definition.GetSpeUseData(lv), level.speUseData, "使用缓存");
                AssertEffectTable(equip, level.GetSpeEquipData(), "未激活装备效果");
                var duration = level.GetActiveTime();
                if (!float.IsFinite(duration) || duration <= 0f || !float.IsFinite(level.MaxPower()) || level.MaxPower() <= 0f)
                    throw new InvalidOperationException("原生激活或充能参数无效");
                level.activeTimeLeft = duration;
                AssertEffectTable(equip * 2f, level.GetSpeEquipData(), "原生激活装备效果");
                // The native caller supplies a negative delta to this method.
                level.ManageCdTimeLeft(-duration);
                if (level.activeTimeLeft != 0f) throw new InvalidOperationException("原生激活时间未正常结束");
                AssertEffectTable(equip, level.GetSpeEquipData(), "原生激活结束恢复");
                foreach (var cost in new[] { level.equipUseSpeAddValue, level.damageUseSpeAddValue, level.selfUseSpeAddValue, level.enemyUseSpeAddValue })
                    if (!float.IsFinite(cost)) throw new InvalidOperationException("原生效果消耗计算产生非有限数值");
            }
        }
        finally { definitions.Remove(probeId); }
    }

    // Opt-in, one-shot audit at native data-load completion. No update hook,
    // player skill assignment, save write, buff injection or combat scheduler.
    public static void RunRequestedUtilityAudit()
    {
        var request = Path.Combine(Paths.ConfigPath, "LongYinTrainer.utility-audit.request");
        if (!File.Exists(request)) return;
        File.Delete(request);
        var report = new StringBuilder("Utility native pipeline audit; detached objects, NOT a full battle trial.\n");
        var passed = 0;
        var failed = 0;
        var oldDraft = _customDraft;
        var oldStatus = _customStatus;
        try
        {
            var database = GameDataController.Instance;
            var ids = new List<int>();
            foreach (var pair in database.kungfuSkillDataBase)
                if (pair.Key >= 0 && pair.Key < CustomKungfuIdMin && OriginalKungfu(database, pair.Key)?.type is >= 0 and <= 2) ids.Add(pair.Key);
            ids.Sort();
            foreach (var id in ids)
            {
                try
                {
                    var source = OriginalKungfu(database, id)!;
                    _customDraft = new CustomKungfuRecord { Type = source.type, BaseDonorId = id, Name = source.name, Description = source.describe };
                    if (!ApplyFullKungfuDonor(id, true)) throw new InvalidOperationException("同类模板复制失败");
                    _customDraft.Id = CustomKungfuIdMax;
                    // Use the actual UI import and record serializer, then alter
                    // every nonzero native effect to verify edits survive reload.
                    foreach (var field in new[] { "upgrade", "equip", "use" })
                    {
                        ImportCustomEffectFromDonor(field);
                        foreach (var entry in CustomSpeAddSpecs(field)) entry.Value *= 1.25f;
                    }
                    var restored = CloneCustomKungfuRecord(_customDraft);
                    var before = BuildCustomKungfuDefinition(database, _customDraft)!;
                    var after = BuildCustomKungfuDefinition(database, restored)!;
                    AssertEffectTable(before.upgradeAddData, after.upgradeAddData, "JSON 升级表");
                    AssertEffectTable(before.equipAddData, after.equipAddData, "JSON 装备表");
                    AssertEffectTable(before.useAddData, after.useAddData, "JSON 使用表");
                    ValidateUtilityNativeDefinition(database, restored, after);
                    report.AppendLine($"PASS type={source.type} id={id} {source.name}");
                    passed++;
                }
                catch (Exception ex) { failed++; report.AppendLine($"FAIL id={id} {ex}"); }
            }
            report.AppendLine($"TOTAL passed={passed} failed={failed}");
        }
        catch (Exception ex) { report.AppendLine("AUDIT ABORTED " + ex); }
        finally
        {
            _customDraft = oldDraft;
            _customStatus = oldStatus;
            File.WriteAllText(Path.Combine(Paths.ConfigPath, "LongYinTrainer.utility-audit.txt"), report.ToString(), Encoding.UTF8);
            LongYinTrainerPlugin.Logger.LogInfo($"Utility native audit complete: {passed} passed, {failed} failed. See utility-audit.txt; battle trial is separate.");
        }
    }
}
