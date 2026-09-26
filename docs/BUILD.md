# 构建、打包和上传 GitHub

## 1. 准备

安装 Git 和能构建 net6.0 的 .NET SDK。本机使用 .NET SDK 10.0.101 构建成功。需要运行模拟测试时，还需兼容的 .NET 6 运行时。

使用自己的游戏安装目录，先装好 BepInEx 6 IL2CPP x64 并运行游戏一次，确认下列文件存在：

- BepInEx/core/BepInEx.Core.dll
- BepInEx/interop/Assembly-CSharp.dll
- BepInEx/interop/UnityEngine.CoreModule.dll

interop 是 BepInEx 根据本机游戏生成的接口文件。游戏更新后，应先正常运行一次，使接口文件与当前游戏匹配，再重新编译。

## 2. 构建源码

在仓库根目录打开 PowerShell：

```powershell
.\scripts\Build.ps1 -GamePath 'D:\steam\steamapps\common\LongYinLiZhiZhuan'
```

替换为你的实际路径。脚本只编译，不修改游戏中的 DLL。输出在 `LongYinTrainer/bin/Release/net6.0/LongYinTrainer.dll`。

也可以直接用 MSBuild 参数或环境变量：

```powershell
dotnet build .\LongYinTrainer\LongYinTrainer.csproj -c Release '-p:GamePath=D:\你的游戏目录'
$env:LONGYIN_GAME_PATH = 'D:\你的游戏目录'
dotnet build .\LongYinTrainer\LongYinTrainer.csproj -c Release
```

## 3. 回归与游戏检查

```powershell
dotnet run --project .\IconRegression\IconRegression.csproj -c Release
dotnet run --project .\ForceLockRegression\ForceLockRegression.csproj -c Release
```

这些测试使用模拟对象，不加载真实游戏。正式发布前还应验证打开面板、分类/翻页、图标来源、物品添加、资源锁定/解锁、切换存档、原生全视野，并检查游戏日志。先备份存档。

## 4. 一键打包

```powershell
.\scripts\Package.ps1 -GamePath 'D:\steam\steamapps\common\LongYinLiZhiZhuan'
```

脚本重新编译，从 BepInPlugin 特性读取版本号，在 `dist` 生成 ZIP 和 SHA256 校验文件。同名 ZIP 已存在时停止，先重命名旧包再重新打包。

ZIP 结构：

```text
BepInEx/
  plugins/
    LongYinTrainer.dll
INSTALL.md
USAGE.md
```

不要直接压缩整个 bin 目录：它可能含复制来的游戏和第三方依赖。也不要上传游戏本体、interop/core DLL、解包图片、存档、配置、日志、分析输出或个人自定义功法数据。打包脚本只复制本 Mod DLL 与安装说明，dist 默认不加入 Git。

## 5. 上传源码到 GitHub

本地已经有 Git 提交。在 GitHub 新建空仓库，不勾选自动生成 README 等文件。复制仓库地址，在本地仓库根目录运行，替换示例地址：

```powershell
git remote add origin https://github.com/你的用户名/LongYinTrainer.git
git push -u origin main
```

如果已有 origin，先用 `git remote -v` 确认，不要盲目覆盖。登录凭据交给 Git/GitHub Credential Manager 管理，不要写进脚本或文档。

## 6. 发布安装包

源码推送后，在 GitHub 的 Releases 页面创建 `v0.4.26` Release，选择 main 对应提交，填写更新内容，上传 `dist/LongYinTrainer-0.4.26.zip` 和 `.sha256` 文件。GitHub 自动生成的 Source code 压缩包是源码，玩家应下载单独上传的 Mod ZIP。

## 7. 后续更新

修改 BepInPlugin 的版本号、README 和变更说明，构建、测试、重新打包；确认 diff 后提交并推送。GitHub 新建对应版本 Release。上传前决定合适的许可证；仓库目前未擅自添加许可。
