# 一键安装 0.4.30.21

退出游戏，把 `LongYinTrainer-Setup-0.4.30.21.exe` 放在 `LongYinLiZhiZhuan.exe` 同级运行。安装器内置稳定插件与校验过的 BepInEx 785；安装加载器无需联网，游戏第一次生成接口仍可能下载依赖。

本包基于适配 1.1.3f0 的 0.4.30.20，不含实验中的原版自创功法编辑器扩展。旧 0.4.30.4 不应继续用于新版游戏。

安装器校验加载器完整文件清单，缺失或损坏时重新安装已验证版本，保留存档、修改器配置和其他插件。文件安装成功不等于游戏启动成功：进入存档按 H；若无反应，反馈 `BepInEx/LogOutput.log`。没有游戏日志时，提供游戏目录 `LongYinTrainer-install.log` 或安装窗口给出的临时日志路径。

构建：先运行 Package.ps1，再运行 Build-Installer.ps1 -LoaderZip <官方785 ZIP路径>。加载器 ZIP 必须匹配源码中固定的 SHA256。
