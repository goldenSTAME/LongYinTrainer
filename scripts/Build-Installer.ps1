param([string]$GamePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $repo 'LongYinTrainer\LongYinTrainer.cs') -Raw
$match=[regex]::Match($source,'BepInPlugin\("codex\.longyin\.trainer", "LongYin Trainer", "([0-9.]+)"\)')
if(!$match.Success){throw '未找到版本号'}
$version=$match.Groups[1].Value
$zip=Join-Path $repo "dist\LongYinTrainer-$version.zip"
if(!(Test-Path -LiteralPath $zip)){throw '请先运行 Package.ps1 生成插件 ZIP。'}
$out=Join-Path $repo "dist\LongYinTrainer-Setup-$version.exe"
if(Test-Path -LiteralPath $out){throw "安装器已存在：$out"}
$installerSource=Get-Content (Join-Path $repo 'Installer\Installer.cs') -Raw
$installerSource=[regex]::Replace($installerSource,'internal const string ModVersion = "[0-9.]+";',('internal const string ModVersion = "'+$version+'";'))
$tempSource=Join-Path ([IO.Path]::GetTempPath()) ('LongYinInstaller-'+[guid]::NewGuid().ToString('N')+'.cs')
[IO.File]::WriteAllText($tempSource,$installerSource,[Text.UTF8Encoding]::new($false))
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/resource:'+ $zip+',ModPayload') ('/out:'+$out) $tempSource
if($LASTEXITCODE -ne 0){throw '安装器编译失败。'}
$hash=(Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($out))" | Set-Content ($out+'.sha256')
Write-Output $out
Write-Output $hash




