# 新版兼容性自动检查

运行（先退出游戏）：

```powershell
pwsh -File .\scripts\Test-Compatibility.ps1 -GamePath 'D:\steam\steamapps\common\LongYinLiZhiZhuan' -Startup
```

不加 `-Startup` 只跑编译和模拟回归。脚本不自动安装 DLL；启动检查要求已安装 DLL 与本次编译产物的 SHA256 一致，否则报失败。不要在测试期间另行启动游戏。

结果保存到 `test-results/时间/REPORT.md` 和 `results.json`，每组测试保留完整日志。FAIL 返回退出码 1；PENDING 表示没有测试，不能理解为通过。退出码 0 仅表示已执行的自动检查没有失败，不表示完整游戏兼容认证。

## 自动执行用例

| 组 | 检查内容 | 通过条件 |
| --- | --- | --- |
| BUILD | 使用当前 interop 编译全部修改器源码 | 编译退出码 0 |
| BookCatalogRegression | 秘籍分类、品质组合筛选与排序 | 所有断言通过；未提供资源数据时不覆盖原版资源完整性 |
| CategoryRegression | 自创功法类别 | 所有断言通过 |
| CityRegression | 城市编辑与状态锁定 | 所有断言通过 |
| CombatRegression | 群战参战者、援军去重、结算、失败与取消路径 | 所有断言通过 |
| DateRegression | 日期冻结及补丁开关 | 所有断言通过 |
| EventRegression | 奇遇规则与世界奇遇 | 所有断言通过 |
| ForceLockRegression | 势力资源锁定 | 所有断言通过 |
| IconRegression | 图标索引 | 所有断言通过 |
| KungfuRegression | 功法编辑、类别、效果规则 | 所有断言通过 |
| SkillBindingRegression | 战斗技能栏绑定 | 所有断言通过 |
| DEPLOYED_BUILD | 已安装插件与编译产物一致 | SHA256 相同 |
| FRESH_STARTUP_LOG | 后台启动新版游戏 | 日志写入时间晚于本次启动，不接受旧日志 |
| PLUGIN_LOAD | 插件及加载器初始化 | 两个成功标记均出现 |
| PATCH_BINDINGS | 实际启动中的接口绑定 | 无 Error/Fatal、方法缺失、构造函数缺失 |
| STARTUP_WARNINGS | 启动警告 | 原文保留；有警告标 WARN |
| CUSTOM_DEFINITIONS | 自创功法运行时注册 | 出现注册成功日志；没有该日志则 SKIP |
| SAVE_UNCHANGED | 全部存档测试前后完整性 | 相对文件名集合和 SHA256 完全一致 |

模拟测试的逐项名称会列入生成的报告。启动测试不读取存档，不进入战斗，不验证画面；启动完成后观察 10 秒并终止本脚本创建的后台进程。

## 仍需隔离实机测试的用例（未自动执行）

必须使用独立游戏目录及存档副本；以下写盘行为不得对唯一原档执行。

| 用例 | 操作 | 验收 |
| --- | --- | --- |
| H_PANEL_AND_PAGING | 读入副本，H 打开/收起，各页签切换，物品搜索与翻页 | 无异常、无重复控件，显示数据与目标角色相符 |
| OLD_SAVE_LOAD_SAVE_RELOAD | 加载旧存档副本，记录角色/物品/功法，另存空槽，退出并重载 | 新压缩存档可读，关键数据一致，自创功法保留 |
| NATIVE_AND_MOD_CUSTOM_KUNGFU | 私宅创建官方武学；修改器创建另一武学；分别学习、装备、存盘重载 | 入口不相互劫持，ID 不冲突，名称图标效果独立，重载保留 |
| REAL_BATTLE_AND_REINFORCEMENTS | 普通仇敌群战、剧情援军战各完成一次；分别验证胜负和退出 | 实际参战者正确、援军按新规则入场、奖励和逐人结算不重复 |

不能以模拟回归或启动成功替代以上四项。构造函数补丁回退警告尤其需要通过真实创建、学习与读档来确认行为。
