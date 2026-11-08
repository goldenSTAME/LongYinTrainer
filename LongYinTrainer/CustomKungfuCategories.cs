using System;
using System.Collections.Generic;
using System.Globalization;

public static partial class TrainerBehaviour
{
    private static CustomKungfuRecord? _categoryDraftOwner;
    private static readonly Dictionary<int, CustomKungfuRecord> CategoryDrafts = new();

    private static void SwitchCustomKungfuCategory(int category)
    {
        if (_customDraft == null || category == _customDraft.Type) return;
        if (category < 0 || category > 8) return;
        SyncCustomKungfuDraftFromInputs();
        var current = _customDraft;
        try
        {
            if (!ReferenceEquals(_categoryDraftOwner, current)) CategoryDrafts.Clear();
            // Build first, so a missing native template cannot discard the current draft.
            var next = CategoryDrafts.TryGetValue(category, out var previous)
                ? CloneCustomKungfuRecord(previous) : NewCustomKungfuDraft(category);
            CategoryDrafts[current.Type] = CloneCustomKungfuRecord(current);
            next.Id = current.Id;
            next.EditOriginal = current.EditOriginal;
            next.Name = current.Name;
            next.Description = current.Description;
            _customDraft = next;
            _categoryDraftOwner = next;
            _customEffectSelectedKey = "";
            _customEffectEditorField = "";
            _customPickerPreviewSkillId = int.MinValue;
            _customStatus = $"已切换到{CustomKungfuTypeName(category)}专属模板与内容；上一类别草稿已暂存，尚未保存。";
        }
        catch (Exception ex) { _customStatus = "类别切换失败，原草稿保留：" + ex.Message; }
    }

    private static HashSet<string> GetCategoryEffectKeys(string field)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var database = GameDataController.Instance;
        if (database?.kungfuSkillDataBase == null || _customDraft == null) return keys;
        foreach (var pair in database.kungfuSkillDataBase)
        {
            if (pair.Key < 0 || pair.Key >= CustomKungfuIdMin) continue;
            var skill = OriginalKungfu(database, pair.Key);
            if (skill == null || skill.type != _customDraft.Type) continue;
            if (field is "ratio" or "needs")
            {
                var table = field == "ratio" ? skill.addDamageRatio : skill.skillNeeds;
                if (table == null) continue;
                var entries = new List<CustomAttriNumSpec>();
                AddCustomAttriList(entries, "attri", table.attri, 6);
                AddCustomAttriList(entries, "fight", table.fightSkill, 9);
                AddCustomAttriList(entries, "living", table.livingSkill, 9);
                foreach (var entry in entries) keys.Add(CustomAttriSpecKey(entry.Kind, entry.Index));
                if (table.Hp != 0f) keys.Add("hp:0");
                if (table.Mana != 0f) keys.Add("mana:0");
                if (table.Power != 0f) keys.Add("power:0");
                if (table.Charm != 0f) keys.Add("charm:0");
            }
            else
            {
                var table = field switch { "upgrade" => skill.upgradeAddData, "equip" => skill.equipAddData, _ => skill.useAddData };
                var ids = table?.GetKeys();
                if (table == null || ids == null) continue;
                for (var i = 0; i < ids.Count; i++)
                    if (table.Get(ids[i]) != 0f) keys.Add("spe:" + ids[i].ToString(CultureInfo.InvariantCulture));
            }
        }
        return keys;
    }
}
