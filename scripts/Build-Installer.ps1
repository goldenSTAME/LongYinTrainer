param([string]$GamePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$zip=Join-Path $repo 'dist\LongYinTrainer-0.4.26.zip'
if(!(Test-Path -LiteralPath $zip)){throw '请先运行 Package.ps1 生成插件 ZIP。'}
$out=Join-Path $repo 'dist\LongYinTrainer-Setup-0.4.26.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/resource:'+ $zip+',ModPayload') ('/out:'+$out) (Join-Path $repo 'Installer\Installer.cs')
if($LASTEXITCODE -ne 0){throw '安装器编译失败。'}
$hash=(Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($out))" | Set-Content ($out+'.sha256')
Write-Output $out
Write-Output $hash
