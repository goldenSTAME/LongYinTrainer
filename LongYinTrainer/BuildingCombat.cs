using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using HeroList = Il2CppSystem.Collections.Generic.List<HeroData>;
using TeamLists = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<HeroData>>;

// Only the two ordinary personal-conflict callbacks are supported. Never
// replay the original result callback for extra opponents: it awards battle
// rewards and changes relationships in addition to displaying the menu.
internal static class BuildingCombat
{
    internal static ConfigEntry<bool> Enabled = null!;
    private static readonly HashSet<int> Entered = new();
    private static readonly Queue<HeroData> Pending = new();
    private static WorldData? _world;
    private static HeroData? _main;
    private static string _callback = "";
    private static int _enemyTeam = -1;
    private static int _current = -1;
    private static bool _won;
    private static bool _settling;
    private static bool _changing;
    private static bool _startingResult;

    internal static void Install(Harmony harmony, ConfigFile config)
    {
        Enabled = config.Bind("Battle", "BuildingConflictGroup", true,
            "Personal enemy conflicts in the current area: present same-faction friends/relatives join immediately; each entered enemy gets one post-battle choice.");
        void Patch(Type type, string method, Type[] args, string? pre = null, string? post = null)
        {
            var original = AccessTools.Method(type, method, args)
                ?? throw new MissingMethodException(type.Name, method);
            harmony.Patch(original,
                pre == null ? null : new HarmonyMethod(typeof(BuildingCombat), pre),
                post == null ? null : new HarmonyMethod(typeof(BuildingCombat), post));
        }
        Patch(typeof(BattleController), "PrepareBattleMap", new[] { typeof(BattleType), typeof(TeamLists), typeof(TeamLists), typeof(float), typeof(string), typeof(bool), typeof(BattleMapTypeData), typeof(int), typeof(float) }, nameof(Prepare));
        Patch(typeof(BattleController), "BattleTeamPrepare", Type.EmptyTypes, post: nameof(Prepared));
        Patch(typeof(BattleController), "HeroEnterBattleField", new[] { typeof(HeroData), typeof(BattleTeam), typeof(GridUnitData), typeof(int), typeof(float) }, post: nameof(Joined));
        Patch(typeof(BattleController), "BattleRealEnd", Type.EmptyTypes, nameof(Ending));
        foreach (var name in new[] { "DeathFightInteractHeroResult", "NpcAttackPlayerResult" })
            Patch(typeof(PlotController), name, new[] { typeof(string) }, nameof(ResultBeginning), nameof(ResultFinished));
        Patch(typeof(PlotController), "ShowSinglePlot", new[] { typeof(SinglePlotData) }, nameof(ReplaceFirstMenu));
        foreach (var name in new[] { "DeathFightHeroFinish", "NpcAttackPlayerFinish" })
            Patch(typeof(PlotController), name, Type.EmptyTypes, nameof(Finish));
        foreach (var name in new[] { "HideInteractUI", "HideInteractUIOneDay", "HideInteractUIImmediate" })
            Patch(typeof(PlotController), name, Type.EmptyTypes, nameof(Hidden));
        Patch(typeof(GameDataController), "Load", new[] { typeof(int) }, nameof(Reset));
        Patch(typeof(GameDataController), "GameDataIntoGame", Type.EmptyTypes, nameof(Reset));
    }

    private static void Reset()
    {
        Entered.Clear(); Pending.Clear(); _world = null; _main = null;
        _callback = ""; _enemyTeam = -1; _current = -1;
        _won = false; _settling = false;
        _startingResult = false;
    }

    private static bool ValidWorld() => _world != null && GameController.Instance?.worldData?.Pointer == _world.Pointer;
    private static bool Usable(HeroData? hero) => hero != null && hero.heroID != 0 && !hero.hide && !hero.dead && !hero.inPrison && !hero.isTempHero;
    private static bool Contains(HeroList? list, int id)
    {
        for (var i = 0; i < (list?.Count ?? 0); i++) if (list![i]?.heroID == id) return true;
        return false;
    }

    private static void AddIds(HashSet<int> ids, HeroList? heroes)
    {
        for (var i = 0; i < (heroes?.Count ?? 0); i++)
            if (heroes![i] != null) ids.Add(heroes[i].heroID);
    }

    private static string Count(HeroList? list) => list == null ? "null" : list.Count.ToString();
    private static void Trace(string message) => LongYinTrainerPlugin.Logger.LogInfo("Building conflict: " + message);

    private static void Prepare(BattleType targetType, ref TeamLists fightMemData,
        ref TeamLists fightSupportData, string fightEndCall, ref int _maxHeroNum)
    {
        Reset();
        var stage = "eligibility";
        try
        {
            if (!Enabled.Value || targetType == BattleType.StudyFight ||
                (fightEndCall != "DeathFightInteractHeroResult" && fightEndCall != "NpcAttackPlayerResult")) return;
            if (fightMemData == null || fightMemData.Count != 2 || fightMemData[0] == null || fightMemData[1] == null)
            { Trace($"skip {fightEndCall}: missing two main teams"); return; }
            // SupportType.None returns null, NOT an allocated empty list.
            // Both vanilla None~Hero and Hero~None pass null inner entries.
            var support0 = fightSupportData == null || fightSupportData.Count == 0 ? null : fightSupportData[0];
            var support1 = fightSupportData == null || fightSupportData.Count < 2 ? null : fightSupportData[1];
            Trace($"prepare callback={fightEndCall}, members={Count(fightMemData[0])}/{Count(fightMemData[1])}, supports={Count(support0)}/{Count(support1)}, cap={_maxHeroNum}");
            stage = "resolve location";
            var world = GameController.Instance?.worldData;
            var player = world?.Player();
            var target = PlotController.Instance?.targetInteractHero;
            var area = AreaController.Instance?.areaData;
            if (player == null || !Usable(target) || area == null ||
                player.GetAreaID(false) != area.areaID || target!.GetAreaID(false) != area.areaID ||
                !(target.HaveHater(player.heroID) || player.HaveHater(target.heroID)))
            { Trace($"skip {fightEndCall}: target/player not eligible in current area"); return; }
            var playerTeam = Contains(fightMemData[0], player.heroID) ? 0 : Contains(fightMemData[1], player.heroID) ? 1 : -1;
            if (playerTeam < 0) { Trace("skip: player missing from main teams"); return; }
            var enemyTeam = 1 - playerTeam;
            if (!Contains(fightMemData[enemyTeam], target.heroID)) { Trace("skip: interaction target missing from enemy team"); return; }
            stage = "collect participants";

            // Stage new lists before changing native arguments. In this game the
            // bottom portrait strip represents AreaData.insideHeros (the sect
            // premises), not individual workshop/farm tiles inside that area.
            var allies = new HashSet<int> { player.heroID };
            AddIds(allies, fightMemData[playerTeam]);
            AddIds(allies, playerTeam == 0 ? support0 : support1);
            var teammates = player.teamMates;
            for (var i = 0; i < (teammates?.Count ?? 0); i++) allies.Add(teammates![i]);
            var enemies = new HeroList();
            for (var i = 0; i < fightMemData[enemyTeam].Count; i++)
            {
                var hero = fightMemData[enemyTeam][i];
                if (hero != null && !Contains(enemies, hero.heroID)) enemies.Add(hero);
            }
            for (var i = 0; i < (area.insideHeros?.Count ?? 0); i++)
            {
                var hero = area.GetInsideHero(i);
                if (!Usable(hero) || allies.Contains(hero.heroID) || Contains(enemies, hero.heroID) || hero.GetAreaID(false) != area.areaID) continue;
                if (target.SameForce(hero) || target.HaveRelationBetterThanFriend(hero.heroID, true, true) ||
                    hero.HaveHater(player.heroID) || player.HaveHater(hero.heroID)) enemies.Add(hero);
            }
            var supports = new HeroList();
            var enemySupport = enemyTeam == 0 ? support0 : support1;
            for (var i = 0; i < (enemySupport?.Count ?? 0); i++)
            {
                var hero = enemySupport![i];
                if (hero != null && !allies.Contains(hero.heroID) && !Contains(enemies, hero.heroID) && !Contains(supports, hero.heroID)) supports.Add(hero);
            }
            var members = new TeamLists(); var supportLists = new TeamLists();
            for (var i = 0; i < 2; i++)
            {
                members.Add(i == enemyTeam ? enemies : fightMemData[i]);
                supportLists.Add(i == enemyTeam ? supports : (i == 0 ? support0 : support1)!);
            }
            _world = world; _main = target; _callback = fightEndCall; _enemyTeam = enemyTeam;
            fightMemData = members; fightSupportData = supportLists;
            if (_maxHeroNum > 0) _maxHeroNum = Math.Max(_maxHeroNum, enemies.Count);
            var names = new List<string>();
            for (var i = 0; i < enemies.Count; i++) names.Add($"{enemies[i].heroName}({enemies[i].heroID})");
            Trace($"area={area.areaID}, target={target.heroID}, enemy team={enemyTeam}, initial enemies={enemies.Count}: {string.Join(", ", names)}");
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogError($"Building conflict preparation failed at {stage}: {ex}"); }
    }

    private static void Prepared(BattleController __instance)
    {
        try
        {
            if (_main == null || !ValidWorld()) return;
            var teams = __instance.teamMemPrepareData;
            var list = teams != null && teams.Count > _enemyTeam ? teams[_enemyTeam] : null;
            var selected = 0;
            for (var i = 0; i < (list?.Count ?? 0); i++) if (list![i] != null && list[i].enterBattle) selected++;
            Trace($"native preparation enemy roster={list?.Count ?? 0}, selected={selected}");
        }
        catch (Exception ex) { LongYinTrainerPlugin.Logger.LogWarning($"Building conflict preparation diagnostics: {ex.Message}"); }
    }

    private static void Joined(HeroData __0, BattleTeam __1, UnityEngine.GameObject __result)
    {
        try
        {
            if (_main != null && ValidWorld() && __result != null && __1.ID == _enemyTeam && Usable(__0))
                Entered.Add(__0.heroID);
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogWarning($"Building conflict entry tracking stopped: {ex.Message}"); }
    }

    private static void Ending(BattleController __instance)
    {
        try
        {
            if (_main == null || !ValidWorld()) { Reset(); return; }
            var playerTeam = __instance.GetPlayerTeam();
            if (playerTeam == null || __instance.winTeamID != playerTeam.ID) { Reset(); return; }
            // BattleRealEnd destroys teams before invoking the plot callback.
            // Snapshot now, using only units that were actually instantiated.
            var ids = new List<int>(Entered); ids.Sort();
            if (Entered.Contains(_main.heroID)) Pending.Enqueue(_main);
            foreach (var id in ids)
            {
                var hero = _world!.GetHero(id);
                if (id != _main.heroID && Usable(hero)) Pending.Enqueue(hero);
            }
            _won = Pending.Count > 0;
            LongYinTrainerPlugin.Logger.LogInfo($"Building conflict victory: entered={Entered.Count}, settlement={Pending.Count}.");
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogError($"Building conflict result capture failed: {ex}"); }
    }

    private static void ResultBeginning(PlotController __instance, MethodBase __originalMethod)
    {
        try
        {
            if (!_won || _settling || !ValidWorld() || __originalMethod.Name != _callback || __instance.targetInteractHero?.heroID != _main?.heroID) return;
            _startingResult = Pending.Count > 0 && Pending.Peek().heroID == _main?.heroID;
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogError($"Building conflict menu failed: {ex}"); }
    }

    private static void ResultFinished() => _startingResult = false;

    private static void ReplaceFirstMenu(PlotController __instance, ref SinglePlotData __0)
    {
        try
        {
            if (!_startingResult || !ValidWorld() || Pending.Count == 0 || __instance.targetInteractHero?.heroID != _main?.heroID) return;
            var hero = Pending.Peek();
            var menu = CreateMenu(hero, Pending.Count - 1);
            // Replace the data before the native text tween starts. Calling
            // ChangePlot a second time from a result postfix leaves two tween
            // completion callbacks that both append the same choice buttons.
            __0 = menu;
            Pending.Dequeue();
            _current = hero.heroID;
            _settling = true;
            _startingResult = false;
            LongYinTrainerPlugin.Logger.LogInfo($"Building conflict choice: hero={hero.heroID}, remaining={Pending.Count}; native menu replaced once.");
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogError($"Building conflict first menu failed: {ex}"); }
    }

    private static SinglePlotData CreateMenu(HeroData hero, int remaining)
    {
        var choices = new Il2CppSystem.Collections.Generic.List<string>();
        if (_callback == "DeathFightInteractHeroResult")
        {
            choices.Add("抢夺银两;DeathFightRobHeroMoney;;;♦抢夺对方约一半的银两\n♦根据抢夺数额增加恶名");
            choices.Add("抢夺财物;DeathFightRobHero;;;♦抢夺对方一件物品\n♦根据物品价值增加恶名");
            choices.Add("出手伤人;DeathFightHurtHero;;;♦重伤对方\n♦增加10恶名");
            choices.Add("放你一马;DeathFightHeroFinish");
        }
        else
        {
            choices.Add("施以惩戒;NpcAttackPlayerResultHurt");
            choices.Add("放你一马;NpcAttackPlayerResultRelease");
        }
        return new SinglePlotData($"我#$TargetInteractName#技不如人，听凭处置！\n（逐人处置：此人之后还有 {remaining} 人）", choices, PlotTargetHeroType.HeroID, hero.heroID.ToString());
    }

    private static bool ShowNext(PlotController plot)
    {
        HeroData? hero = null;
        while (Pending.Count > 0 && hero == null)
        {
            var candidate = Pending.Dequeue();
            if (Usable(candidate)) hero = candidate;
        }
        if (hero == null) return false;
        _current = hero.heroID;
        plot.targetInteractHero = hero;
        _changing = true;
        try
        {
            plot.ChangePlot(CreateMenu(hero, Pending.Count));
        }
        finally { _changing = false; }
        LongYinTrainerPlugin.Logger.LogInfo($"Building conflict choice: hero={hero.heroID}, remaining={Pending.Count}.");
        return true;
    }

    private static bool Finish(PlotController __instance, MethodBase __originalMethod)
    {
        try
        {
            var expected = _callback == "DeathFightInteractHeroResult" ? "DeathFightHeroFinish" : "NpcAttackPlayerFinish";
            if (!_settling) return true;
            if (!ValidWorld() || __originalMethod.Name != expected || __instance.targetInteractHero?.heroID != _current) { Reset(); return true; }
            if (Pending.Count == 0) { Reset(); return true; }
            // Apply the native per-person departure once; only the final native
            // Finish closes the interaction/advances the day for the whole fight.
            var previous = __instance.targetInteractHero;
            AIController.Instance.HeroLoseFightOnBigMap(previous);
            if (ShowNext(__instance)) return false;
            __instance.targetInteractHero = previous;
            Reset();
            // No valid targets remain: close without executing departure twice.
            if (expected == "DeathFightHeroFinish") __instance.HideInteractUIOneDay();
            else __instance.HideInteractUI();
            return false;
        }
        catch (Exception ex) { Reset(); LongYinTrainerPlugin.Logger.LogError($"Building conflict settlement stopped: {ex}"); return true; }
    }

    private static void Hidden()
    {
        if (_settling && !_changing) Reset();
    }
}
