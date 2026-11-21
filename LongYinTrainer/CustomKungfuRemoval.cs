using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NativeLists = Il2CppSystem.Collections.Generic;

public static partial class TrainerBehaviour
{
    private static List<HeroData> RemovalHeroes(WorldData world)
    {
        var heroes = KungfuHeroes().ToList();
        if (world.TempHeros != null)
            foreach (var hero in world.TempHeros)
                if (hero != null) heroes.Add(hero);
        return heroes.GroupBy(hero => hero.Pointer).Select(group => group.First()).ToList();
    }

    private static bool ReferencesSkill(HeroData hero, int id) =>
        hero.FindSkill(id) != null || HasEquippedKungfu(hero, id) || hero.GetNowActiveSkill()?.skillID == id;

    private static void OpenKungfuRemoval(CustomKungfuRecord record)
    {
        try
        {
            var world = GameController.Instance?.worldData ?? throw new InvalidOperationException("请先载入存档。");
            var users = RemovalHeroes(world).Where(hero => ReferencesSkill(hero, record.Id)).ToList();
            void Show(int page)
            {
                InvalidateCustomKungfuPages();
                ActivateCustomKungfuPage("remove", parent =>
                {
                    var action = record.EditOriginal ? "恢复原版定义" : "永久删除";
                    AddText(parent, $"{action}「{record.Name}」  ID {record.Id}", new Vector2(15, 12), new Vector2(1210, 52), 22);
                    AddText(parent, record.EditOriginal ? "将移除原版功法的自定义配置，角色继续习得原版功法。" :
                        "确认后会从当前存档的下列角色卸下并遗忘此武学，并从模组公共配置删除定义。其他存档若使用此功法也受影响。", new Vector2(15, 72), new Vector2(1210, 70), 17);
                    AddText(parent, $"当前存档已学或装备此功法的角色：{users.Count} 人", new Vector2(15, 155), new Vector2(1200, 42), 19);
                    const int pageSize = 20;
                    for (int i = 0; i < pageSize && page * pageSize + i < users.Count; i++)
                    {
                        var h = users[page * pageSize + i];
                        AddText(parent, $"{h.heroName}（ID {h.heroID}）", new Vector2(15 + i % 4 * 305, 212 + i / 4 * 52), new Vector2(300, 44), 16);
                    }
                    if (page > 0) AddCustomKungfuButton(parent, "上一页", new Vector2(15, 486), new Vector2(180, 40), () => Show(page - 1));
                    if ((page + 1) * pageSize < users.Count) AddCustomKungfuButton(parent, "下一页", new Vector2(220, 486), new Vector2(180, 40), () => Show(page + 1));
                    AddCustomKungfuButton(parent, "取消", new Vector2(15, 575), new Vector2(380, 52), ShowCustomKungfuTab);
                    AddCustomKungfuButton(parent, "确认" + action, new Vector2(650, 575), new Vector2(550, 52), () =>
                    {
                        try
                        {
                            if (GameController.Instance?.worldData?.Pointer != world.Pointer || FindSavedCustomKungfu(record.Id) != record)
                                throw new InvalidOperationException("存档或功法已变更，请重新选择。");
                            var current = RemovalHeroes(world);
                            var currentUsers = current.Where(h => ReferencesSkill(h, record.Id)).ToList();
                            if (!users.Select(h => h.Pointer).ToHashSet().SetEquals(currentUsers.Select(h => h.Pointer)))
                            { OpenKungfuRemoval(record); return; }
                            DeleteKungfu(record, world, current, currentUsers);
                        }
                        catch (Exception ex) { _customStatus = ex.Message; LongYinTrainerPlugin.Logger.LogWarning(ex); }
                    });
                });
            }
            Show(0);
        }
        catch (Exception ex) { _customStatus = ex.Message; }
    }

    private static void CheckKungfuBooks(WorldData world, List<HeroData> heroes, int id)
    {
        void Book(ItemData? item, string location)
        {
            if (item?.type == ItemType.Book && item.bookData?.skillID == id)
                throw new InvalidOperationException($"「{location}」仍有此功法的秘籍，请先处理秘籍后再删除。");
        }
        void Storage(ItemListData? store, string location)
        {
            if (store?.allItem != null) foreach (var item in store.allItem) Book(item, location);
        }
        void Display(NativeLists.List<NativeLists.List<ItemData>>? shared, string location)
        {
            if (shared != null) foreach (var row in shared)
                if (row != null) foreach (var item in row) Book(item, location);
        }
        void Writers(NativeLists.List<BookWriterData>? writers)
        {
            if (writers == null) return;
            foreach (var writer in writers)
            {
                if (writer == null) continue;
                Book(writer.targetBookData, "著书"); Book(writer.combineBookData, "合书");
                if (writer.targetSkillData?.skillID == id) throw new InvalidOperationException("此功法仍在著书列表中，请先处理著书任务。");
            }
        }
        foreach (var h in heroes)
        {
            Storage(h.itemListData, h.heroName + "的背包"); Storage(h.selfStorage, h.heroName + "的仓库");
        }
        if (world.Forces != null) foreach (var f in world.Forces)
        {
            if (f == null) continue;
            Storage(f.forceStorage, "势力仓库"); Storage(f.bookStorage, "藏经阁");
            Display(f.showRoomItems, "势力陈列室"); Writers(f.bookWriterList);
        }
        Storage(world.governStorage, "官府仓库"); Storage(world.speBookStorage, "特殊藏书");
        Storage(GameDataController.ExternalStorage, "外部仓库");
        Display(world.showRoomItems, "陈列室"); Book(world.playerAuctionItem, "拍卖"); Writers(world.playerBookWriter);
    }

    private static Action CaptureList<T>(NativeLists.List<T>? list)
    {
        if (list == null) return () => { };
        var entries = new T[list.Count];
        for (var i = 0; i < entries.Length; i++) entries[i] = list[i];
        return () =>
        {
            list.Clear();
            foreach (var entry in entries) list.Add(entry);
        };
    }

    private static Action CaptureLoadout(HeroData hero)
    {
        var learned = CaptureList(hero.kungfuSkills);
        var attacks = CaptureList(hero.attackSkills);
        var slots = CaptureList(hero.attackSkillSaveRecord);
        var inner = hero.internalSkill;
        var dodge = hero.dodgeSkill;
        var unique = hero.uniqueSkill;
        var active = hero.nowActiveSkill;
        var innerSlot = hero.internalSkillSaveRecord;
        var dodgeSlot = hero.dodgeSkillSaveRecord;
        var uniqueSlot = hero.uniqueSkillSaveRecord;
        var flags = new List<(KungfuSkillLvData Skill, bool Equipped)>();
        if (hero.kungfuSkills != null) foreach (var entry in hero.kungfuSkills)
            if (entry != null) flags.Add((entry, entry.equiped));
        return () =>
        {
            learned(); attacks(); slots();
            hero.internalSkill = inner; hero.dodgeSkill = dodge; hero.uniqueSkill = unique;
            hero.internalSkillSaveRecord = innerSlot; hero.dodgeSkillSaveRecord = dodgeSlot;
            hero.uniqueSkillSaveRecord = uniqueSlot; hero.nowActiveSkill = active;
            foreach (var flag in flags) flag.Skill.equiped = flag.Equipped;
            hero.RecountSkillState();
            RefreshHero(hero);
        };
    }

    private static void DeleteKungfu(CustomKungfuRecord record, WorldData world, List<HeroData> heroes, List<HeroData> users)
    {
        var battle = BattleController.Instance;
        if (_customKungfuStoreLoadFailed || _customBattlePreviewActive ||
            (battle != null && battle.battleState != BattleState.None && battle.battleState != BattleState.End))
            throw new InvalidOperationException("请在配置正常载入且战斗结束后删除。");
        var db = GameDataController.Instance ?? throw new InvalidOperationException("数据库未载入。");
        var id = record.Id;
        var original = record.EditOriginal ? OriginalKungfu(db, id) : null;
        if (record.EditOriginal && original == null) throw new InvalidOperationException("原版定义未载入。");
        if (!record.EditOriginal) CheckKungfuBooks(world, heroes, id);
        var store = new CustomKungfuStore { Schema = _customKungfuStore.Schema, NextId = _customKungfuStore.NextId,
            Skills = _customKungfuStore.Skills.Where(s => s.Id != id).ToList() };
        var undo = new Stack<Action>();
        try
        {
            if (!record.EditOriginal) foreach (var hero in users)
            {
                undo.Push(CaptureLoadout(hero));
                var skill = hero.FindSkill(id);
                while (skill != null)
                {
                    if (HasEquippedKungfu(hero, id) || skill.equiped) hero.UnequipSkill(skill, false);
                    ForgetUnassignedSkill(hero, skill);
                    skill = hero.FindSkill(id);
                }
                if (ReferencesSkill(hero, id)) throw new InvalidOperationException($"请先从 {hero.heroName} 卸下残留招式，再重试。");
            }
            WriteCustomKungfuStore(store);
        }
        catch
        {
            while (undo.Count > 0) undo.Pop()();
            throw;
        }
        _customKungfuStore = store;
        if (original != null)
        {
            CopyKungfuDefinition(original, db.kungfuSkillDataBase[id]);
            RefreshLearnedCustomKungfuInstances(id);
        }
        else db.kungfuSkillDataBase.Remove(id);
        CustomKungfuIconDonors.Remove(id); CustomKungfuIconFiles.Remove(id); CustomKungfuBaseDonors.Remove(id);
        _customDraft = NewCustomKungfuDraft();
        _customBasicChoiceField = _customEffectEditorField = "";
        Catalog.Clear();
        _customStatus = original == null ? $"已永久删除「{record.Name}」。请保存当前游戏。" : $"已恢复「{original.name}」的原版定义。";
        InvalidateCustomKungfuPages();
        ShowCustomKungfuTab();
    }
}
