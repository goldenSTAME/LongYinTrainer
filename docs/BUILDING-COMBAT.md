# 在场仇敌助战与逐人处置（本机试用版 0.4.28.2）

入口：H → 战斗辅助 → 在场同门亲友助战／逐人处置。默认开启，重新启动游戏后加载新 DLL。

本功能针对同一个门派／城镇场景内，与主角存在仇敌关系的普通 NPC 冲突：

- 主动出手袭击：`DeathFightInteractHeroResult`。
- 仇敌主动挑衅：`NpcAttackPlayerResult`。
- 目标的在场同门、亲友，以及其他与主角有仇的在场人物加入敌方初始名单。
- 不加入外地、隐藏、死亡、被囚禁、临时角色及已属我方的人员。
- 切磋和其他剧情回调不接管。场景指底部头像所在的 AreaData，并不是单个农田、工坊格子。

获胜后仅为实际成功入场的敌人依次显示一次菜单。主动袭击为抢钱、抢物、伤人、放过；被袭反击为惩戒、放过。财物、伤势和恶名变化复用原版处理。额外敌人不会重复执行整场战斗结算；最后一人结束时才调用完整的原版结束流程。

## 0.4.28.1 修复依据

原版 `SupportType.None` 返回 null，最终 `fightSupportData` 中也会保留 null 内层列表。出手袭击为 `None~Hero`，截图中的“那便来吧”为 `HardFight-NpcAttackPlayerResult--Hero~None`。0.4.28 直接读取内层 Count，导致两条路径均可能在扩充名单前抛出空引用异常。

修复前，用原版的 null 数据形状复现了 BuildingCombat.cs 的异常；修复后加入针对两种方向、两个 null 列表、空援军容器及其他空值的回归检查。`maxHeroNum <= 0` 在原版 BattleTeamPrepare 中是不限制人数，保留其语义；有正数限制时提升至新增敌方名单人数。

## 验证

`dotnet run --project CombatRegression/CombatRegression.csproj -c Release`

模拟对象测试直接编译生产文件，检查筛选和结算队列。它不验证 Harmony 原生拦截、站位、剧情界面或 IL2CPP 生命周期，不能代替实战。

检查 `BepInEx/LogOutput.log` 中的 `Building conflict`：

1. `prepare callback=...`：两方原始主名单与援军数量（null 是合法输入）。
2. `initial enemies=...`：扩充后姓名和 ID。
3. `native preparation enemy roster=..., selected=...`：原版战前界面实际读取并选中的人数。
4. `victory: entered=..., settlement=...`：真正进场与进入结算队列的人数。
5. `choice: hero=..., remaining=...`：逐人处置进度。

必须重新启动游戏。BepInEx 的插件列表可能仍显示三段版本号 0.4.28；请以新增的 `prepare callback=...` 和 `native preparation enemy roster=...` 诊断行确认修复代码实际运行。磁盘 DLL 中的 BepInPlugin 版本为 0.4.28.1。

2026-09-26 本机实战日志已确认主动袭击路径：区域 54、目标 67，初始敌人 8、原版战前选中 8、实际入场 8、胜后依次显示 8 人的菜单，剩余人数从 7 到 0。用户确认后续逐人处置正常；被袭反击路径仍需单独实战核对。

## 0.4.28.2 首人菜单修复

首人的原版结果回调已启动文字动画，旧插件又在结果后调用 ChangePlot，造成两次文字完成回调重复生成四个按钮。现改为在原版 ShowSinglePlot 开始前替换本次菜单参数，保持只有一次原生显示调用；后续人的队列衔接不变。三项悬浮说明恢复原版完整文案（含 ♦ 与换行）。模拟回归新增首人单次显示与原版文案检查，共 23 项通过；本次画面修复尚待游戏内复核。
