using System;
using System.Collections.Generic;

// Native ResetSpe* calculates the costs of every effect and incorporates the
// breakthrough data according to the skill's category. Assigning spe*Data alone
// leaves these derived values stale, particularly for the three utility slots.
internal static class KungfuSkillRefresh
{
    internal static void RefreshSkill(KungfuSkillLvData skill)
    {
        // Native resets return early for an empty effect table. Clear the costs
        // first so removing the final effect cannot retain its previous cost.
        skill.equipUseSpeAddValue = 0f;
        skill.damageUseSpeAddValue = 0f;
        skill.selfUseSpeAddValue = 0f;
        skill.enemyUseSpeAddValue = 0f;
        skill.ResetSpeEquipData();
        skill.ResetSpeUseData();
        skill.skillIconDirty = true;
    }

    internal static int RefreshHero(HeroData hero, int skillId)
    {
        var count = RefreshHeroSkills(hero, skill => skill.skillID == skillId);
        if (count > 0)
        {
            hero.heroDetailDirty = true;
            hero.CheckHeroDetailDirty(true);
        }
        return count;
    }

    internal static int RefreshHeroSkills(HeroData hero, Func<KungfuSkillLvData, bool> shouldRefresh)
    {
        var seen = new HashSet<IntPtr>();
        var refreshed = 0;
        void Refresh(KungfuSkillLvData? skill)
        {
            if (skill == null || !seen.Add(skill.Pointer) || !shouldRefresh(skill)) return;
            RefreshSkill(skill);
            refreshed++;
        }
        var learned = hero.kungfuSkills;
        if (learned != null)
            for (var i = 0; i < learned.Count; i++) Refresh(learned[i]);
        Refresh(hero.internalSkill);
        Refresh(hero.dodgeSkill);
        Refresh(hero.uniqueSkill);
        var attacks = hero.attackSkills;
        if (attacks != null)
            for (var i = 0; i < attacks.Count; i++) Refresh(attacks[i]);
        Refresh(hero.GetNowActiveSkill());
        return refreshed;
    }
}
