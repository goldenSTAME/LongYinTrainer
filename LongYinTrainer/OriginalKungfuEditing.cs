using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;

public static partial class TrainerBehaviour
{
    private static readonly Dictionary<int, KungfuSkillData> OriginalKungfuSources = new();
    private static IntPtr _originalKungfuDefinitionsPointer;
    private static readonly HashSet<IntPtr> RefreshedEditedKungfuInstances = new();

    public static void PrepareEditedKungfuLoad() => RefreshedEditedKungfuInstances.Clear();

    public static void RefreshEditedKungfuHeroCaches(HeroData? hero)
    {
        if (hero == null || _customKungfuStoreLoadFailed || _customKungfuStore.Skills.Count == 0) return;
        try
        {
            KungfuSkillRefresh.RefreshHeroSkills(hero, skill =>
                !RefreshedEditedKungfuInstances.Contains(skill.Pointer) && FindSavedCustomKungfu(skill.skillID) != null);
            foreach (var skill in EditedKungfuInstances(hero)) RefreshedEditedKungfuInstances.Add(skill.Pointer);
        }
        catch (Exception ex)
        {
            _customStatus = "功法缓存刷新失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning($"Edited kungfu cache refresh failed for hero {hero.heroID}: {ex}");
        }
    }

    private static IEnumerable<KungfuSkillLvData> EditedKungfuInstances(HeroData hero)
    {
        var skills = hero.kungfuSkills;
        if (skills != null)
            for (var i = 0; i < skills.Count; i++)
                if (skills[i] != null && FindSavedCustomKungfu(skills[i].skillID) != null) yield return skills[i];
        foreach (var skill in new[] { hero.internalSkill, hero.dodgeSkill, hero.uniqueSkill, hero.GetNowActiveSkill() })
            if (skill != null && FindSavedCustomKungfu(skill.skillID) != null) yield return skill;
        var attacks = hero.attackSkills;
        if (attacks != null)
            for (var i = 0; i < attacks.Count; i++)
                if (attacks[i] != null && FindSavedCustomKungfu(attacks[i].skillID) != null) yield return attacks[i];
    }

    public static void ResetOriginalKungfuSources()
    {
        OriginalKungfuSources.Clear();
        _originalKungfuDefinitionsPointer = IntPtr.Zero;
        _customKungfuRegisteredDatabasePointer = IntPtr.Zero;
        PrepareEditedKungfuLoad();
    }

    private static bool IsEditableKungfuRecord(CustomKungfuRecord record)
        => record.EditOriginal
            ? record.Id >= 0 && record.Id < CustomKungfuIdMin
            : record.Id >= CustomKungfuIdMin && record.Id <= CustomKungfuIdMax;

    private static void CaptureOriginalKungfuSources(GameDataController database)
    {
        var definitions = database.kungfuSkillDataBase;
        if (definitions == null) return;
        if (_originalKungfuDefinitionsPointer != definitions.Pointer)
        {
            ResetOriginalKungfuSources();
            _originalKungfuDefinitionsPointer = definitions.Pointer;
        }
        // Capture every override target before applying any of them. Donors
        // always refer to the unmodified source, even when two edited skills
        // borrow from one another or a definition is saved repeatedly.
        foreach (var record in _customKungfuStore.Skills)
        {
            if (!record.EditOriginal) continue;
            if (!IsEditableKungfuRecord(record) || !definitions.ContainsKey(record.Id))
                throw new InvalidOperationException($"原版功法 ID {record.Id} 不存在");
            if (OriginalKungfuSources.ContainsKey(record.Id)) continue;
            var snapshot = new KungfuSkillData();
            CopyKungfuDefinition(definitions[record.Id], snapshot);
            OriginalKungfuSources.Add(record.Id, snapshot);
        }
    }

    private static IEnumerable<HeroData> KungfuHeroes()
    {
        var seen = new HashSet<IntPtr>();
        var worldHeroes = GameController.Instance?.worldData?.Heros;
        if (worldHeroes != null)
            for (var i = 0; i < worldHeroes.Count; i++)
            {
                var hero = worldHeroes[i];
                if (hero != null && seen.Add(hero.Pointer)) yield return hero;
            }
        var player = Player;
        if (player != null && seen.Add(player.Pointer)) yield return player;
        var units = BattleController.Instance?.activeUnitList;
        if (units != null)
            for (var i = 0; i < units.Count; i++)
            {
                var hero = units[i]?.heroData;
                if (hero != null && seen.Add(hero.Pointer)) yield return hero;
            }
    }

    private static bool HasEquippedKungfu(HeroData hero, int skillId)
    {
        if (hero.internalSkill?.skillID == skillId || hero.dodgeSkill?.skillID == skillId ||
            hero.uniqueSkill?.skillID == skillId) return true;
        var attacks = hero.attackSkills;
        if (attacks != null)
            for (var i = 0; i < attacks.Count; i++)
                if (attacks[i]?.skillID == skillId) return true;
        return false;
    }

    private static void ValidateKungfuCategoryChange(GameDataController database, CustomKungfuRecord record)
    {
        var definitions = database.kungfuSkillDataBase;
        if (definitions == null || !definitions.ContainsKey(record.Id) || definitions[record.Id].type == record.Type) return;
        foreach (var hero in KungfuHeroes())
            if (HasEquippedKungfu(hero, record.Id))
                throw new InvalidOperationException("更换功法类别前，请先让装备该功法的角色卸下它，再保存并按新类别装备");
    }

    private static void LoadOriginalKungfuDraft(int skillId)
    {
        var database = GameDataController.Instance ?? throw new InvalidOperationException("功法数据库尚未载入");
        var source = OriginalKungfu(database, skillId) ?? throw new InvalidOperationException("原版功法不存在");
        var saved = FindSavedCustomKungfu(skillId);
        if (saved?.EditOriginal == true) _customDraft = CloneCustomKungfuRecord(saved);
        else
        {
            _customDraft = NewCustomKungfuDraft(source.type);
            _customDraft.EditOriginal = true;
            _customDraft.Id = skillId;
            _customDraft.BaseDonorId = skillId;
            _customDraft.Name = source.name;
            _customDraft.Description = source.describe;
            if (!ApplyFullKungfuDonor(skillId, true)) throw new InvalidOperationException("原版功法模板类别不匹配，未载入");
        }
        _customTab = 0;
        _customEffectEditorField = "";
        _customBasicChoiceField = "";
        _customStatus = $"已载入原版功法 {source.name}（ID {skillId}）；保存后覆盖该功法，已习得角色会刷新。";
    }
}
