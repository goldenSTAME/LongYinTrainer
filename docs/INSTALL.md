# 龙胤立志传 修改器安装教程（BepInEx）

安装后进入存档，按 **H** 打开 / 收起“江湖札记”。H 只控制面板显示，各项功能需单独开启，详见 [使用说明](USAGE.md)。发布包同时附带本安装教程和 USAGE.md。

## 发布包是否完整？

`LongYinTrainer-0.4.26.zip` 是完整的 **Mod 插件包**，包含 `BepInEx/plugins/LongYinTrainer.dll` 和本教程；它不是包含加载器的整合包。首次安装需要额外下载下面的 BepInEx。已经正确安装 BepInEx 的玩家只需更新 Mod DLL。无需旧图片、开发工具、游戏解包数据，也不需要安装 .NET SDK。

## 第一步：安装 BepInEx（首次安装必做）

1. 先退出游戏。在 Steam 库右键游戏 → 管理 → 浏览本地文件。确认这里有 `LongYinLiZhiZhuan.exe`。
2. 打开 [BepInEx 官方构建下载页](https://builds.bepinex.dev/projects/bepinex_be)。本 Mod 本机验证使用 **build #785**，下载文件名：
   `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785+6abdba4.zip`。
   页面找不到时按 Ctrl+F 搜索 `#785`。选择 **Unity.IL2CPP / win / x64**；不要下载 Mono、x86 或 BepInEx 5。更新构建可能可用，但此 Mod 未逐一验证。
3. 将下载 ZIP 的**全部内容**解压到游戏根目录，与游戏 EXE 同级。不要多套一层压缩包名字的文件夹。包含 `BepInEx`、`dotnet`、`winhttp.dll`、`doorstop_config.ini` 等加载器文件；不要只复制 BepInEx 文件夹。
4. 启动游戏一次，等待主菜单出现后正常退出。IL2CPP 首次启动需要生成接口文件，可能比平时慢，并可能需要网络下载依赖；不要一看到黑屏就强制结束。
5. 检查 `BepInEx/config`、`BepInEx/interop` 和日志是否生成；本机日志是 `BepInEx/LogOutput.log`，部分构建或文档使用 `LogOutput.txt`。确认加载器正常后继续安装 Mod。

官方步骤参考：[BepInEx 的 Unity IL2CPP 安装说明](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html)。

## 第二步：安装 Mod

解压发布 ZIP，将里面的 BepInEx 文件夹合并到游戏根目录。完成后目录应类似：

```text
游戏根目录/
  LongYinLiZhiZhuan.exe
  winhttp.dll
  doorstop_config.ini
  dotnet/
  BepInEx/
    core/
    interop/
    config/
    plugins/
      LongYinTrainer.dll
```

重新启动游戏，按 **H**。日志应出现 `Loading [LongYin Trainer 0.4.26]`。游戏退出后再更换 DLL。已有旧版时将旧 DLL 备份到 plugins 目录之外，防止重复加载。

## 常见问题

- **没有 BepInEx 日志**：检查是否解压到 EXE 同级，是否漏了根目录加载文件，是否误用 Mono/x86 包。若系统报告权限不足，请停止并处理权限问题。
- **首次启动慢**：查看日志是否仍在生成 interop 或下载依赖；记录具体报错，不要反复覆盖不同版本加载器。
- **有 BepInEx 日志，但 H 无效**：检查插件路径是否多套一层目录、日志里是否识别 LongYin Trainer，窗口是否获得焦点，以及是否改过快捷键配置。
- **更新后异常**：先确认游戏能只带 BepInEx 启动，再安装 Mod；提交问题时附游戏版本、BepInEx 构建号和相关日志片段。
- **游戏更新后编译或加载失败**：先让 BepInEx 为当前游戏生成匹配的 interop；重大游戏接口变化可能需要更新 Mod。

## 功能与卸载

旧的 LongYinTrainerAssets 图片目录不再必需。请只保留一个 LongYinTrainer.dll，备份 DLL 放到 plugins 之外。

“势力/开局”针对所选角色的所属势力，未选角色时默认主角。先应用数值，再点“锁定当前资源”；锁定固定金钱及全部资源，收入也不会改变固定值，再次点击解锁。关闭面板仍生效；重启或换存档后需要重新锁定。

“更多功能 → 主角全地图视野”使用原生发现机制；发现状态可能写入存档。关闭恢复原始视野范围，不撤销已发现事件，不扩大交互距离。

卸载或回退：退出游戏后移走 Mod DLL，或恢复备份 DLL。Mod 已经写入存档的数值不会自动恢复，需要原存档备份。
