using System;
using System.Collections.Generic;

// Called at the native binding event, never from SkillIconController.Update.
// Keep native value/state updates while avoiding the repeated inited=false writes.
internal static class BattleSkillBarBinding
{
    private static IntPtr _controller, _root, _unit, _hero;
    private static readonly List<(IntPtr Pointer, int Id)> Skills = new();
    private static bool _bound;

    internal static void Clear()
    {
        _bound = false;
        Skills.Clear();
    }

    internal static bool CanReuse(BattleController controller)
    {
        var root = controller.nowActiveHeroUI;
        var unit = controller.nowActiveUnit;
        var hero = unit?.heroData;
        if (!_bound || root == null || unit == null || hero == null ||
            controller.Pointer != _controller || root.Pointer != _root ||
            unit.Pointer != _unit || hero.Pointer != _hero) return false;
        var attacks = hero.attackSkills;
        var count = attacks?.Count ?? 0;
        if (Skills.Count != count + 3 || !Matches(0, hero.internalSkill) ||
            !Matches(1, hero.dodgeSkill) || !Matches(2, hero.uniqueSkill)) return false;
        for (var i = 0; i < count; i++)
            if (!Matches(i + 3, attacks![i])) return false;
        return true;
    }

    private static bool Matches(int index, KungfuSkillLvData? skill)
        => Skills[index] == (skill?.Pointer ?? IntPtr.Zero, skill?.skillID ?? -1);

    internal static void Remember(BattleController controller)
    {
        Clear();
        var root = controller.nowActiveHeroUI;
        var unit = controller.nowActiveUnit;
        var hero = unit?.heroData;
        if (root == null || unit == null || hero == null) return;
        _controller = controller.Pointer;
        _root = root.Pointer;
        _unit = unit.Pointer;
        _hero = hero.Pointer;
        Add(hero.internalSkill);
        Add(hero.dodgeSkill);
        Add(hero.uniqueSkill);
        var attacks = hero.attackSkills;
        if (attacks != null)
            for (var i = 0; i < attacks.Count; i++) Add(attacks[i]);
        _bound = true;
    }

    private static void Add(KungfuSkillLvData? skill)
        => Skills.Add((skill?.Pointer ?? IntPtr.Zero, skill?.skillID ?? -1));
}
