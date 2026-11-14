using System;

internal static class SummonSkillValidation
{
    internal static void Validate(KungfuSkillData skill)
    {
        if (skill == null || skill.targetType != SkillTargetType.EmptyGridSummon) return;
        var summons = GameDataController.Instance?.SummonDataBase;
        if (summons == null || !summons.ContainsKey(skill.summonID) || summons[skill.summonID] == null)
            throw new InvalidOperationException($"召唤功法“{skill.name}”的召唤对象无效（ID {skill.summonID}）。请改用原版召唤功法作为基础，不能只把普通功法的目标改为空地召唤。");
        LongYinTrainerPlugin.Logger.LogInfo($"Summon skill validated: skill={skill.skillID}, summon={skill.summonID}, animation={skill.animationName}");
    }
}
