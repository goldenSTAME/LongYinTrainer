# 官方窗口复用试验 0.4.31.2

分支 experiment/native-kungfu-editor。

当前入口：私宅自创武学 -> 官方 CustomSkillCreateController.ShowCreatePanel。不调用旧 MOD 编辑窗口，不克隆按钮，不添加 Unity 点击委托。直接绕过剧情品级、模板消耗和闭关步骤。

点数显示隐藏，GetEffectBudget 仅对本次官方草稿返回放宽预算；每次重新打开清理作用域。进入后检查文本隐藏及预算补丁是否确实生效并写日志。名称检查、确认和 WorldData.AddCustomSkill 等原生注册路径保留。

当前默认使用最低 ID 的原生拳掌母版；Experimental.NativeKungfuTemplate 可指定其他母版。类别品级自由选择、旧功法编辑迁移、额外数值和特效功能未完成。原版效果数量上限、单项参数上限未在本次解除。

构建通过；启动检查不等同于点击、编辑和保存重载测试。需观察 Native editor: official draft ready 日志，并验证确认及另存重载后结果。未证明此前卡死根因已解决。
