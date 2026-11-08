using System;
using System.Collections.Generic;

public static partial class TrainerBehaviour
{
    // Called only while constructing the editor. Never hook combat updates here.
    private static bool MatchesCustomEffectRule(int id, int group)
    {
        var rule = FindSpeAddDefinition(id);
        if (rule == null) return false;
        return group switch
        {
            6 => rule.lastTime != 0 && rule.selfBuff,
            7 => rule.lastTime != 0 && !rule.selfBuff,
            8 => (int)rule.triggerType == 1,
            9 => (int)rule.triggerType == 2,
            _ => false
        };
    }

    private static string CustomEffectRuleDescription(CustomEffectChoice? choice)
    {
        if (choice == null) return "选中词条后查看原生触发、对象和持续规则；仅显示本类别原版配置中的可选词条。";
        var rule = FindSpeAddDefinition(choice.TypeId);
        if (rule == null) return "未找到原生规则；保留原有数值格式，不推断触发方式。";
        var trigger = (int)rule.triggerType switch { 1 => "攻触发", 2 => "守触发", _ => "无攻守标签（依具体效果）" };
        if (rule.lastTime == 0)
            return $"{trigger}；非持续状态词条\n数值按原版显示单位编辑；具体属性或效果由原版处理。";
        var duration = rule.lastTime > 0 ? $"持续参数 {rule.lastTime} 回合" : "动态持续时间（原生规则）";
        var target = rule.selfBuff ? "触发方自身" : "交互对方（攻守阶段决定）";
        return $"{trigger}；对象：{target}\n{duration}；{(rule.stackable ? "可叠层" : "不叠层")}；特殊效果仍走专用规则\n这里编辑词条值，非持续时间；冷却、概率判定和到期由原版处理。";
    }

    private static string CustomEffectPlacementDescription(string field, CustomEffectChoice? choice)
    {
        var placement = field switch
        {
            "upgrade" => "升级效果：升级属性表，不等于持续挂状态。",
            "equip" => "装备效果：装备后提供词条，按原生条件触发。",
            "use" when _customDraft?.Type < 3 => "此类使用效果不走攻击功法的通用合并分支，需对照原版配置。",
            _ => "使用效果：施放阶段提供词条，仍需满足原生触发条件。"
        };
        if (choice == null) return placement;
        var database = GameDataController.Instance;
        if (database?.kungfuSkillDataBase == null) return placement + "\n原版配置尚未加载。";
        // Read immutable donor snapshots, not edited live definitions. Prefer the
        // current category only; never suggest another category as a fallback.
        var same = new List<string>();

        foreach (var pair in database.kungfuSkillDataBase)
        {
            if (pair.Key < 0 || pair.Key >= CustomKungfuIdMin) continue;
            var skill = OriginalKungfu(database, pair.Key);
            if (skill == null || skill.type != _customDraft?.Type) continue;
            var table = field switch { "upgrade" => skill.upgradeAddData, "equip" => skill.equipAddData, _ => skill.useAddData };
            if (table == null || table.Get(choice.TypeId) == 0f) continue;
            var examples = same;
            if (examples.Count < 2) examples.Add($"{skill.name}（{CustomKungfuTypeName(skill.type)}，ID {pair.Key}）");
        }
        var samples = same;
        return placement + "\n" + (samples.Count == 0
            ? "该表未找到原版非零配置示例；不代表已验证可触发。"
            : "同类原版示例：" + string.Join("、", samples));
    }
}
