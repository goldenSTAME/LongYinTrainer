using System;
using System.Collections.Generic;

public static partial class TrainerBehaviour
{
    private static int _pendingRemoveKungfu = -1;

    private static void RemoveSavedKungfuContent()
    {
        try
        {
            if (_customKungfuStoreLoadFailed) throw new InvalidOperationException("配置读取失败，请先恢复备份。");
            var record = _customDraft == null ? null : FindSavedCustomKungfu(_customDraft.Id);
            if (record == null || record.RemovedContent) throw new InvalidOperationException("请先从已保存列表载入目标功法。");
            var battle = BattleController.Instance;
            if (battle != null && battle.battleState != BattleState.None && battle.battleState != BattleState.End)
                throw new InvalidOperationException("请先结束战斗。");
            if (_pendingRemoveKungfu != record.Id)
            {
                _pendingRemoveKungfu = record.Id;
                _customStatus = $"再次点击确认删除 {record.Name}（{record.Id}）的自定义内容；恢复同类原版母版，保留 ID 和已学/秘籍引用，不撤回历史属性。";
                return;
            }
            _pendingRemoveKungfu = -1;
            var db = GameDataController.Instance ?? throw new InvalidOperationException("数据库未载入");
            var donorId = record.EditOriginal ? record.Id : record.BaseDonorId;
            var donor = OriginalKungfu(db, donorId);
            if (donor == null || donor.type != record.Type)
                throw new InvalidOperationException("原版母版缺失或类别不符，请先恢复配置备份。");
            var replacement = new CustomKungfuRecord {
                Id = record.Id, EditOriginal = record.EditOriginal, RemovedContent = true,
                Name = "[已删除] " + donor.name, Type = donor.type, RareLv = donor.rareLv,
                BaseDonorId = donorId, IconDonorId = donorId
            };
            var candidate = new CustomKungfuStore { Schema = 6, NextId = _customKungfuStore.NextId, Skills = new List<CustomKungfuRecord>() };
            foreach (var saved in _customKungfuStore.Skills)
                candidate.Skills.Add(saved.Id == record.Id ? replacement : CloneCustomKungfuRecord(saved));
            WriteCustomKungfuStore(candidate);
            _customKungfuStore = candidate;
            _customDraft = NewCustomKungfuDraft();
            _customEffectEditorField = "";
            _customBasicChoiceField = "";
            _customKungfuRegisteredDatabasePointer = IntPtr.Zero;
            RegisterCustomKungfuDefinitionsChecked();
            RefreshLearnedCustomKungfuInstances(record.Id);
            Catalog.Clear();
            _customStatus = "已删除并移出列表。已有技能和秘籍保留兼容引用；请另存游戏。";
            InvalidateCustomKungfuPages();
            ShowCustomKungfuTab();
        }
        catch (Exception ex)
        {
            _pendingRemoveKungfu = -1;
            _customStatus = "删除处理未完成：" + ex.Message + "；请保留配置及 .bak，不要删除 BepInEx。";
            LongYinTrainerPlugin.Logger.LogError(ex);
        }
    }
}
