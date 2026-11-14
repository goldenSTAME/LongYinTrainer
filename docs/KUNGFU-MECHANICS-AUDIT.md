# 功法与 Buff 原生机制核对（2026-10-25）

本轮只核对本机 GameAssembly.dll 与对应 IL2CPP 元数据，并对照编辑器源码。尚未逐个实战验证所有命名功法或特殊脚本。早期探索性改动已撤回。用户确认后，0.4.30.9 开始第一阶段的规则展示与筛选；战斗结算仍保持原生。

## 九类功法与共用数据

| 类别 | 原生槽位 | 主动资源机制 | Buff 不能仅按类别判断 |
| --- | --- | --- | --- |
| 0 内功 | internalSkill | 充能，释放后有 activeTimeLeft | 装备词条、突破数据及主动类别分支共同决定效果 |
| 1 轻功 | dodgeSkill | 充能，释放后有 activeTimeLeft | 同上；跳跃目标与范围另有施放分支 |
| 2 绝技 | uniqueSkill | 充能，释放后有 activeTimeLeft | 同上；不是单纯“挂 Buff”类别 |
| 3 拳掌 | attackSkills | 原生攻击技能 CD | 可带装备被动、施放词条、攻守触发及 Buff |
| 4 剑法 | attackSkills | 同上 | 与拳掌共用 Buff 结算主路径 |
| 5 刀法 | attackSkills | 同上 | 与拳掌共用 Buff 结算主路径 |
| 6 长兵 | attackSkills | 同上 | 与拳掌共用 Buff 结算主路径 |
| 7 奇门 | attackSkills | 同上 | 与拳掌共用 Buff 结算主路径 |
| 8 射术 | attackSkills | 同上 | 与拳掌共用 Buff 结算主路径 |

九类共用 KungfuSkillData / KungfuSkillLvData，但类别分支决定读取哪些表、什么时候读取。目标、范围、动作、弹道、召唤等维度还会改变施放路径，不能只修改类别字段推断行为。

## 效果表的实际用途

- upgradeAddData：升级时写入角色属性，不是战斗临时 Buff。
- equipAddData / speEquipData：运功或装备时合入角色属性，包括攻击栏功法的装备效果。
- useAddData / speUseData：本次施放使用的词条。GameController.CountHeroData 的通用合并分支要求当前角色正处于施放状态且功法 type >= 3；不能推断前三类填同一张使用表就有相同效果。
- extraAddData：突破数据。其在装备、施放阶段的使用有类别分支，不能当成另一份同名基础词条直接叠加。
- skillSpeEffects：场景/角色/武器视觉特效，和真正的 HeroData Buff 数据不是同一套系统。

前三类主动释放把充能归零，并设置 activeTimeLeft = GetActiveTime()。当前二进制返回 5；GetSpeEquipData 在 activeTimeLeft > 0 时返回装备效果乘 2，否则返回原装备效果。还分别存在内力、体力、生命相关的类别处理。该激活计时不是 Buff 剩余回合，也不是普通攻击技能 CD。

## 原生 Buff 的触发、目标和生命期

HeroSpeAddDataBase 提供 lastTime、selfBuff、triggerType、stackable 等规则。GetTriggerDescribe 的标签表为无标签、[攻]、[守]。词条是否给自己或对方，以及在攻守哪个阶段读取，与功法的武器类别是不同维度。

- BattleController.ManageDamage 包含防守方词条的概率判断，依 selfBuff 给防守方自身或攻击方添加状态；普通伤害技能能进入该路径。
- BattleUnitAttackFinish 包含施放后的自身 Buff 判断；除角色汇总词条，还会检查当前技能的使用/突破数据。不能把它简化成“所有自身词条都无条件释放即挂”。
- ManageRecover 对恢复类目标另有 Buff 路径，因此辅助、队友目标也需纳入检查。
- 特殊 ID 存在专用分支，例如无敌效果会走独立条件检查，不能只靠通用字段归类覆盖全部技能。
- HeroData.AddBuff 将现有持续值与新值取较大值；stackable 为真时维护并增加层数。重复添加不等于把持续值直接相加。
- ManageHeroBuffRound 对 lastTime > 0 的状态按回合处理，部分状态还执行周期效果；ManageHeroBuffTime 对 lastTime == -1 的状态按时间处理。某些路径还根据双方内力计算动态持续时间。
- 技能数据的 cdTimeLeft、power、activeTimeLeft，与角色的 heroBuff、heroBuffLevel 分开保存。不能把 Buff 持续值统称为“被动冷却”，也不能未经核对给全部 Buff 加一套独立 CD。

## 编辑器适配前必须解决的问题

1. 按效果来源、触发阶段、作用对象和持续规则组织编辑，而不是把前三类分作增益、六类攻击分作输出。
2. 核对“使用效果”在前三类缺少通用合并路径时，原版具体如何配置同类词条；不能仅加一个自身 Buff 列表就宣布适配完成。
3. 区分词条概率、属性倍率、Buff 层数、持续回合/时间；现有单一数值输入不能自动代表这些语义。
4. 为装备被动、主动挂状态、命中触发、受击触发、恢复队友、特殊效果分别核对原版样本。触发标签本身不足以证明全部运行行为。
5. 修改方案需保持每个自创功法独立，不能更改共享 Buff 定义而连带改变所有原版功法；继续复用原生添加、叠层和到期结算。

## 本机检查证据

本地分析输出在未分发的 KungfuAudit 目录：

- native-self-buff-equipment.txt：CountHeroData 与 GetSpeEquipData。
- native-all-skill-rules.txt：MaxPower、CDTimeTotal、ChangeSkillPower、ResetBuff、触发标签。
- native-buff-coroutines.txt：施放开始、命中与结束协程。
- native-hit-buff-rules.txt：ManageDamage、ManageRecover、GetManaLastTime、FightReset。
- native-buff-flow.txt / native-buff-round.txt：AddBuff、时间和回合处理。

上述是静态代码检查证据，不是全功法实战通过报告。0.4.30.9 仅增加编辑器规则筛选和原版配置示例；前三类使用效果的运行时扩展尚未实现。

## 0.4.30.11 原生运行时验证补充

2026-10-25：本机运行中的游戏已完成 441 个原版模板的独立实例验证，内功 153、轻功 136、绝技 152，失败 0。测试走实际编辑器导入、修改原有词条 1.25 倍、JSON 序列化再读取、原生 ResetSpeEquipData / ResetSpeUseData、原生 GetSpeEquipData 及 ManageCdTimeLeft。每个模板验证 1 级和 10 级；激活结束恢复通过。未向英雄附加测试技能，未修改玩家存档。

发现并修复 0.4.30.10 的原版编辑入口类别初始化错误：载入前必须 NewCustomKungfuDraft(source.type)，否则同类模板限制会拒绝非默认类别。

验证边界：尚未覆盖真实战斗协程中的完整施放、随机 Buff、受击触发、突破数据组合和所有专用脚本，不能把原生缓存与激活测试表述为全部实战通过。
