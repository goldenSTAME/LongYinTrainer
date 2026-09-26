param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $GamePath
$source=Get-Content (Join-Path $repo 'LongYinTrainer\LongYinTrainer.cs') -Raw
$match=[regex]::Match($source,'BepInPlugin\("codex\.longyin\.trainer", "LongYin Trainer", "([0-9.]+)"\)')
if(!$match.Success){throw '未找到版本号'}
$version=$match.Groups[1].Value
$dist=Join-Path $repo 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$stage=Join-Path $dist ('stage-'+[guid]::NewGuid().ToString('N'))
$plugins=Join-Path $stage 'BepInEx\plugins'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item (Join-Path $repo 'LongYinTrainer\bin\Release\net6.0\LongYinTrainer.dll') $plugins
Copy-Item (Join-Path $repo 'docs\INSTALL.md') (Join-Path $stage 'INSTALL.md')
Copy-Item (Join-Path $repo 'docs\USAGE.md') (Join-Path $stage 'USAGE.md')
$zip=Join-Path $dist "LongYinTrainer-$version.zip"
if(Test-Path -LiteralPath $zip){throw "文件已存在，请先重命名旧包：$zip"}
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content ($zip+'.sha256')
Write-Output "发布包：$zip"
Write-Output "SHA256：$hash"
