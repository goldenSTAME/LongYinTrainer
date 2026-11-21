using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _learnedSkillPage;
    private static IntPtr _forgetSelection;

    private static string? LearnedOriginalName(int id)
    {
        var world = GameController.Instance?.worldData;
        var db = GameDataController.Instance;
        if (id < 0 || id >= GlobalData.CustomSkillIDStart || world == null || db == null || world.customSkills?.ContainsKey(id) == true) return null;
        return OriginalKungfu(db, id)?.name;
    }

    private static void BuildLearnedSkills(HeroData hero)
    {
        var p = NativePageParent;
        var rows = new List<(KungfuSkillLvData Skill, string Name)>();
        const int pageSize = 18;
        var buttons = new Button[pageSize];
        var labels = new Text[pageSize];
        AddText(p, "已学原版武学 · 先卸下要遗忘的武学，再选择并确认。", new Vector2(0, 55), new Vector2(1000, 40));
        Button previous = null!, next = null!;
        Text counter = null!;
        var empty = AddText(p, "该角色没有已学的原版武学。", new Vector2(0, 120), new Vector2(800, 36));
        for (int cell = 0; cell < pageSize; cell++)
        {
            int slot = cell;
            buttons[cell] = AddButton(p, "", new Vector2(cell % 3 * 345, 120 + cell / 3 * 66), new Vector2(330, 50), () => Choose(slot));
            labels[cell] = buttons[cell].GetComponentInChildren<Text>();
        }
        previous = AddButton(p, "上一页", new Vector2(0, 550), new Vector2(210, 38), () => ChangePage(-1));
        counter = AddText(p, "", new Vector2(380, 550), new Vector2(300, 38));
        next = AddButton(p, "下一页", new Vector2(810, 550), new Vector2(210, 38), () => ChangePage(1));
        RefreshList();

        void RefreshList()
        {
            rows.Clear();
            var learned = hero.kungfuSkills;
            for (int i = 0; learned != null && i < learned.Count; i++)
            {
                var skill = learned[i];
                if (skill != null && LearnedOriginalName(skill.skillID) is string name) rows.Add((skill, name));
            }
            int pages = Math.Max(1, (rows.Count + pageSize - 1) / pageSize);
            _learnedSkillPage = Math.Clamp(_learnedSkillPage, 0, pages - 1);
            for (int cell = 0; cell < pageSize; cell++)
            {
                bool visible = _learnedSkillPage * pageSize + cell < rows.Count;
                buttons[cell].gameObject.SetActive(visible);
                if (visible) UpdateLabel(cell);
            }
            empty.gameObject.SetActive(rows.Count == 0);
            counter.text = $"共 {rows.Count} 门　第 {_learnedSkillPage + 1}/{pages} 页";
            previous.gameObject.SetActive(_learnedSkillPage > 0);
            next.gameObject.SetActive(_learnedSkillPage + 1 < pages);
        }

        bool InUse(KungfuSkillLvData skill) =>
            HasEquippedKungfu(hero, skill.skillID) || hero.GetNowActiveSkill()?.skillID == skill.skillID;

        void UpdateLabel(int cell)
        {
            var (skill, name) = rows[_learnedSkillPage * pageSize + cell];
            labels[cell].text = InUse(skill) ? $"{name}（使用中）" : _forgetSelection == skill.Pointer ? $"确认遗忘「{name}」" : name;
        }

        void ChangePage(int direction)
        {
            _forgetSelection = IntPtr.Zero;
            _learnedSkillPage += direction;
            RefreshList();
        }

        void Choose(int cell)
        {
            var (skill, name) = rows[_learnedSkillPage * pageSize + cell];
            if (TargetHero()?.Pointer != hero.Pointer)
            {
                _forgetSelection = IntPtr.Zero;
                _status = "当前角色已变更，请重新选择。";
                RequestPageRebuild();
                return;
            }
            try
            {
                if (InUse(skill)) throw new InvalidOperationException("请先在游戏武学页卸下或切换当前武学。");
                if (_forgetSelection == skill.Pointer)
                {
                    ForgetLearnedOriginal(hero, skill);
                    _forgetSelection = IntPtr.Zero;
                    _status = $"{hero.heroName} 已遗忘「{name}」。";
                    RefreshList();
                    return;
                }
                var old = _forgetSelection;
                _forgetSelection = skill.Pointer;
                if (old != IntPtr.Zero)
                {
                    int oldCell = rows.FindIndex(row => row.Skill.Pointer == old) - _learnedSkillPage * pageSize;
                    if (oldCell >= 0 && oldCell < pageSize) UpdateLabel(oldCell);
                }
                UpdateLabel(cell);
                _status = $"再次点击确认让 {hero.heroName} 遗忘「{name}」。";
            }
            catch (Exception ex)
            {
                _forgetSelection = IntPtr.Zero;
                _status = ex.Message;
                RefreshList();
            }
        }
    }

    private static void ForgetLearnedOriginal(HeroData hero, KungfuSkillLvData skill)
    {
        var battle = BattleController.Instance;
        if (battle != null && battle.battleState != BattleState.None && battle.battleState != BattleState.End)
            throw new InvalidOperationException("请在战斗结束后遗忘武学。");
        var learned = hero.kungfuSkills;
        int index = learned?.IndexOf(skill) ?? -1;
        if (index < 0 || LearnedOriginalName(skill.skillID) == null)
            throw new InvalidOperationException("武学列表已变化，请重新选择。");
        ForgetUnassignedSkill(hero, skill);
    }

    private static void ForgetUnassignedSkill(HeroData hero, KungfuSkillLvData skill)
    {
        var learned = hero.kungfuSkills;
        int index = learned?.IndexOf(skill) ?? -1;
        if (index < 0) throw new InvalidOperationException("该角色的已学武学已变更。");
        var slots = hero.attackSkillSaveRecord;
        var attacks = new int[slots?.Count ?? 0];
        for (int i = 0; i < attacks.Length; i++) attacks[i] = slots![i];
        int inner = hero.internalSkillSaveRecord, dodge = hero.dodgeSkillSaveRecord, unique = hero.uniqueSkillSaveRecord;
        if (HasEquippedKungfu(hero, skill.skillID) || hero.GetNowActiveSkill()?.skillID == skill.skillID ||
            inner == index || dodge == index || unique == index || Array.IndexOf(attacks, index) >= 0)
            throw new InvalidOperationException("请先在游戏武学页卸下这门武学，并切换当前使用的武学。");
        int before = learned!.Count;
        hero.LoseSkill(skill);
        if (learned.Count != before - 1 || learned.IndexOf(skill) >= 0)
            throw new InvalidOperationException("游戏未完成单项遗忘，请重新查看角色列表。");
        // Derive the saved positions from their pre-removal values even if LoseSkill already adjusted them.
        for (int i = 0; i < attacks.Length; i++) slots![i] = RebaseSavedSkillIndex(attacks[i], index);
        hero.internalSkillSaveRecord = RebaseSavedSkillIndex(inner, index);
        hero.dodgeSkillSaveRecord = RebaseSavedSkillIndex(dodge, index);
        hero.uniqueSkillSaveRecord = RebaseSavedSkillIndex(unique, index);
        RefreshHero(hero);
    }
}
