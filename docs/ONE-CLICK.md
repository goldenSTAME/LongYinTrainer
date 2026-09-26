# 一键安装：BepInEx + 龙胤立志传 修改器

1. [下载 LongYinTrainer-Setup-0.4.26.exe](https://github.com/goldenSTAME/LongYinTrainer/raw/refs/heads/main/downloads/LongYinTrainer-Setup-0.4.26.exe)。
2. 退出游戏。在 Steam 右键游戏 → 管理 → 浏览本地文件，将安装器放到 `LongYinLiZhiZhuan.exe` 同级。
3. 双击安装器，等待显示“安装完成”。放错目录时可在窗口中选择游戏目录重新安装。
4. 启动游戏并读取存档，按 **H** 打开 / 收起面板，各功能单独开启。

**这是较为暴力的修改，会破坏数值平衡、影响游戏体验。修改前备份存档，按需开启功能。**

## 包含什么

EXE 内置 0.4.26 Mod 和安装、使用说明。首次安装从 [BepInEx 官方](https://builds.bepinex.dev/projects/bepinex_be) 下载约 33 MB 的 `Unity.IL2CPP-win-x64 6.0.0-be.785+6abdba4`，校验 SHA256 后安装。因此它是**联网安装器，不是离线整合包**。不需要 .NET SDK；安装器使用 Windows 的 .NET Framework 4.x，适用 Windows x64。

锁定的官方包 SHA256：`2A7CBF74D26ABE4765C3E662DB1721B923BAC39849EBFEF2CA5DC7DE7E2D9B7F`。

## 安装行为

- 检查游戏 EXE、IL2CPP 文件、64 位架构、游戏是否运行和目录写入权限。
- 已有 BepInEx 6 IL2CPP 且基础文件、启用配置齐全时保留现有加载器，只安装 Mod。其他版本未逐一验证；本机验证版本为 #785。
- 不替换存档、现有配置及其他插件。检测到不完整或冲突的加载器时停止，按 [手动教程](INSTALL.md) 检查。
- 被覆盖文件备份在游戏目录 `ModBackups/Installer-时间-唯一编号/original/`；`manifest.txt` 记录新增和覆盖路径。失败会尝试恢复已写文件，并显示回滚失败的路径。
- 安装说明放在游戏目录 `LongYinTrainer-说明`。暂存下载位于系统临时目录 `LongYinInstaller-*`，关闭安装器后可自行清理。
- 安装器不会自动启动游戏、申请管理员权限或修改系统安全设置。遇到权限不足会停止并提示。

## 常见问题

首次启动 BepInEx 还需生成接口，可能联网获取依赖；安装完成不代表已完成第一次游戏初始化。等待游戏正常进入菜单，再读取存档。

下载失败或校验失败时不会安装加载器，检查网络后重新运行。可从官方手动安装 BepInEx，再运行 EXE 安装 Mod。

EXE 尚未进行代码签名，Windows 可能显示未知发布者。请核对来源与仓库同目录的 SHA256 校验文件；不要关闭系统安全防护。

更新时同样先退出游戏再运行安装器。卸载 Mod 可在退出游戏后移走 `BepInEx/plugins/LongYinTrainer.dll`；卸载或回退不撤销已写入存档的修改。

## 开发者构建

先运行 `scripts/Package.ps1 -GamePath '你的游戏目录'`，再运行 `scripts/Build-Installer.ps1`。后者使用 Windows .NET Framework C# 编译器生成单个 x64 EXE，嵌入当前插件 ZIP，不嵌入游戏文件或加载器。

`Installer/InstallerTests.cs` 验证路径拒绝、首次安装、更新备份、配置保留、失败回滚和官方加载器识别。它在临时目录测试，不操作真实游戏。
