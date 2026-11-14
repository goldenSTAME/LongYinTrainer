using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _characterEditor;
    private static int _characterEditorHero = -1;
    private static void OpenCharacterEditor(HeroData hero, int page)
    {
        _characterEditorHero = hero.heroID;
        _characterEditor = page;
        RequestPageRebuild();
    }

    private static bool BuildCharacterEditor(HeroData hero)
    {
        if (_characterEditorHero != hero.heroID) _characterEditor = 0;
        if (_characterEditor == 0) return false;
        var p = NativePageParent;
        AddButton(p, "返回角色数值", new Vector2(810, 0), new Vector2(220, 36), () => { _characterEditor = 0; RequestPageRebuild(); });
        if (_characterEditor <= 3) BuildCharacterStats(hero, _characterEditor);
        else if (_characterEditor == 4) BuildCharacterFace(hero);
        else BuildCharacterPreferences(hero);
        return true;
    }

    private static void BuildCharacterStats(HeroData hero, int kind)
    {
        var p = NativePageParent;
        var values = kind == 1 ? hero.baseAttri : kind == 2 ? hero.baseFightSkill : hero.baseLivingSkill;
        var caps = kind == 1 ? hero.maxAttri : kind == 2 ? hero.maxFightSkill : hero.maxLivingSkill;
        var names = kind == 1 ? GlobalData.AttriName : kind == 2 ? GlobalData.FightSkillName : GlobalData.LivingSkillName;
        AddText(p, "属性", new Vector2(0, 55), new Vector2(180, 30));
        AddText(p, "基础值（不含加成）", new Vector2(200, 55), new Vector2(260, 30));
        AddText(p, "上限", new Vector2(490, 55), new Vector2(240, 30));
        int count = Math.Min(names.Count, Math.Min(values.Count, caps.Count));
        var baseInputs = new List<InputField>();
        var capInputs = new List<InputField>();
        for (int i = 0; i < count; i++)
        {
            float y = 95 + i * 45;
            AddText(p, names[i], new Vector2(0, y), new Vector2(180, 34));
            baseInputs.Add(AddInput(p, F(values[i]), new Vector2(200, y), new Vector2(240, 34)));
            capInputs.Add(AddInput(p, F(caps[i]), new Vector2(490, y), new Vector2(240, 34)));
        }
        var batchBase = AddInput(p, "200", new Vector2(770, 95), new Vector2(260, 34));
        AddButton(p, "基础值全部填入", new Vector2(770, 140), new Vector2(260, 34), () => { foreach (var input in baseInputs) input.text = batchBase.text; });
        var batchCap = AddInput(p, "200", new Vector2(770, 200), new Vector2(260, 34));
        AddButton(p, "上限全部填入", new Vector2(770, 245), new Vector2(260, 34), () => { foreach (var input in capInputs) input.text = batchCap.text; });
        void Apply(bool applyBase, bool applyCap)
        {
            try
            {
                // Validate the entire selected column before writing any value.
                var b = new float[count]; var c = new float[count];
                for (int i = 0; i < count; i++)
                {
                    if (applyBase) b[i] = ParseFloat(baseInputs[i].text);
                    if (applyCap) c[i] = ParseFloat(capInputs[i].text);
                    if (b[i] < 0 || c[i] < 0) throw new FormatException("基础值和上限不能为负数。");
                }
                for (int i = 0; i < count; i++) { if (applyBase) values[i] = b[i]; if (applyCap) caps[i] = c[i]; }
                RefreshHero(hero);
                _status = $"已更新 {hero.heroName} 的" + (applyBase && applyCap ? "基础值及上限。" : applyBase ? "基础值；上限未改动。" : "上限；基础值未改动。");
            }
            catch (Exception ex) { _status = "未应用：" + ex.Message; }
        }
        AddButton(p, "仅应用基础值", new Vector2(0, 550), new Vector2(240, 38), () => Apply(true, false));
        AddButton(p, "仅应用上限", new Vector2(260, 550), new Vector2(240, 38), () => Apply(false, true));
        AddButton(p, "应用两列", new Vector2(520, 550), new Vector2(210, 38), () => Apply(true, true));
        AddText(p, "填入只修改输入框，点击应用才写入角色；返回放弃未应用的输入。", new Vector2(0, 600), new Vector2(1000, 32), 16);
    }

    private static void BuildCharacterFace(HeroData hero)
    {
        var p = NativePageParent;
        AddText(p, "使用新建角色界面的形象代码，可在主角与 NPC 之间共用。", new Vector2(0, 60), new Vector2(1000, 40));
        var code = AddInput(p, hero.GenerateFaceCode(), new Vector2(0, 120), new Vector2(1030, 45));
        AddButton(p, "复制当前外观代码", new Vector2(0, 185), new Vector2(300, 40), () => { code.text = hero.GenerateFaceCode(); GUIUtility.systemCopyBuffer = code.text; _status = "当前角色的形象代码已复制。"; });
        AddButton(p, "粘贴代码", new Vector2(320, 185), new Vector2(230, 40), () => code.text = GUIUtility.systemCopyBuffer);
        AddButton(p, "应用到当前角色", new Vector2(570, 185), new Vector2(300, 40), () =>
        {
            string before = hero.GenerateFaceCode();
            var preferences = GameDataController.playerPrefData?.playerPrefData;
            string portraitKey = hero.heroName + "hideSpeSkeleton";
            int previousPortrait = preferences?.GetInt(portraitKey) ?? 0;
            bool changedPortrait = false;
            try
            {
                var parts = code.text.Trim().Split('/');
                if (parts.Length != hero.faceData.faceID.Count + 2 || (parts[0] != "0" && parts[0] != "1")) throw new FormatException("形象代码字段数量或性别格式错误。");
                for (int i = 1; i < parts.Length - 1; i++) if (!int.TryParse(parts[i], out int part) || part < -1) throw new FormatException("五官编号格式错误。");
                ParseFloat(parts[parts.Length - 1]);
                if (string.IsNullOrWhiteSpace(code.text) || code.text.Length > 4096 || !hero.LoadFaceCode(code.text.Trim())) throw new FormatException("形象代码无效，请从游戏创建角色界面导出完整代码。");
                // Named NPCs may use a fixed portrait. Use the game's own
                // portrait preference to reveal the imported editable face.
                if (hero.UseSpeSkeleton() && preferences != null) { preferences.SetKey(portraitKey, 1); changedPortrait = true; }
                hero.HeroIconDirty = true;
                RefreshHero(hero);
                if (HeroDetailController.Instance?.nowShowHero?.heroID == hero.heroID) HeroDetailController.Instance.RefreshHeroSkeleton();
                _status = $"已修改 {hero.heroName} 的外观，请保存游戏以保留修改。";
            }
            catch (Exception ex) { hero.LoadFaceCode(before); if (changedPortrait) preferences?.SetKey(portraitKey, previousPortrait); _status = "外观未应用：" + ex.Message; }
        });
        AddText(p, "代码包含性别、五官和肤色；服装仍由角色装备决定。应用会直接修改上方姓名对应的角色。", new Vector2(0, 250), new Vector2(1000, 80), 18);
    }

    private static void BuildCharacterPreferences(HeroData hero)
    {
        var p = NativePageParent;
        var fight = new HashSet<int>(); var living = new HashSet<int>(); var hobbies = new HashSet<int>();
        if (hero.kungfuSkillFocus != null) foreach (var x in hero.kungfuSkillFocus) fight.Add(x);
        if (hero.livingSkillFocus != null) foreach (var x in hero.livingSkillFocus) living.Add(x);
        if (hero.hobby != null) foreach (var x in hero.hobby) hobbies.Add(x);
        AddText(p, "专长（可多选）", new Vector2(0, 50), new Vector2(1000, 30));
        void Choice(string label, int id, HashSet<int> selected, int col, float y)
        {
            AddNativeToggle(p, label, col * 205, y, () => selected.Contains(id), v => { if (v) selected.Add(id); else selected.Remove(id); }, 195);
        }
        for (int i = 0; i < GlobalData.FightSkillName.Count; i++) Choice(GlobalData.FightSkillName[i], i, fight, i % 5, 90 + i / 5 * 38);
        for (int i = 0; i < GlobalData.LivingSkillName.Count; i++) Choice(GlobalData.LivingSkillName[i], i, living, i % 5, 170 + i / 5 * 38);
        AddText(p, "喜好（可多选）", new Vector2(0, 255), new Vector2(1000, 30));
        var options = new List<(string Label, int Id)>();
        void Group(int type, Il2CppSystem.Collections.Generic.List<string> labels)
        {
            for (int i = 0; i < labels.Count && i < 10; i++) options.Add((labels[i], type * 10 + i));
        }
        Group(0, GlobalData.EquipmentTypeName);
        options.Add((GlobalData.ItemTypeName[1], 10));
        Group(2, GlobalData.FoodTypeName);
        options.Add((GlobalData.ItemTypeName[3], 30));
        Group(4, GlobalData.TreasureTypeName);
        Group(5, GlobalData.MaterialTypeName);
        Group(6, GlobalData.HorseTypeName);
        var pages = new List<GameObject>();
        for (int page = 0; page * 30 < options.Count; page++)
        {
            var root = UiObject("HobbyPage" + page, p, new Vector2(1030, 225), new Vector2(0, 295));
            pages.Add(root);
            for (int j = page * 30; j < Math.Min(options.Count, (page + 1) * 30); j++)
            {
                var option = options[j]; int slot = j % 30;
                AddNativeToggle(root.transform, option.Label, slot % 5 * 205, slot / 5 * 36,
                    () => hobbies.Contains(option.Id), v => { if (v) hobbies.Add(option.Id); else hobbies.Remove(option.Id); }, 195);
            }
            root.SetActive(page == 0);
        }
        int currentPage = 0;
        var pageText = AddText(p, $"1 / {pages.Count}", new Vector2(220, 535), new Vector2(180, 35));
        void Turn(int delta) { currentPage = Math.Clamp(currentPage + delta, 0, pages.Count - 1); for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == currentPage); pageText.text = $"{currentPage + 1} / {pages.Count}"; }
        AddButton(p, "上一页", new Vector2(0, 535), new Vector2(180, 35), () => Turn(-1));
        AddButton(p, "下一页", new Vector2(420, 535), new Vector2(180, 35), () => Turn(1));
        AddButton(p, "应用专长与喜好", new Vector2(0, 590), new Vector2(300, 38), () =>
        {
            Il2CppSystem.Collections.Generic.List<int> Make(HashSet<int> set) { var list = new Il2CppSystem.Collections.Generic.List<int>(); foreach (int x in new SortedSet<int>(set)) list.Add(x); return list; }
            hero.kungfuSkillFocus = Make(fight); hero.livingSkillFocus = Make(living); hero.hobby = Make(hobbies);
            RefreshHero(hero); _status = $"已更新 {hero.heroName} 的专长与喜好。";
        });
    }
}
