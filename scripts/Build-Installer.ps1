param([string]$GamePath, [string]$LoaderZip)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $repo 'LongYinTrainer\LongYinTrainer.cs') -Raw
$match=[regex]::Match($source,'BepInPlugin\("codex\.longyin\.trainer", "LongYin Trainer", "([0-9.]+)"\)')
if(!$match.Success){throw '未找到版本号'}
$version=$match.Groups[1].Value
$zip=Join-Path $repo "dist\LongYinTrainer-$version.zip"
if(!(Test-Path -LiteralPath $zip)){throw '请先运行 Package.ps1 生成插件 ZIP。'}
if (!$LoaderZip) { $LoaderZip=Join-Path $repo 'downloads\BepInEx-785.zip' }
if (!(Test-Path -LiteralPath $LoaderZip)) { throw '请提供 -LoaderZip：官方 BepInEx 785 ZIP，用于构建离线安装器。' }
$expected='2A7CBF74D26ABE4765C3E662DB1721B923BAC39849EBFEF2CA5DC7DE7E2D9B7F'
if ((Get-FileHash -LiteralPath $LoaderZip).Hash -ne $expected) { throw '加载器 SHA256 不匹配' }
$manifest=Join-Path ([IO.Path]::GetTempPath()) ('LongYinLoader-'+[guid]::NewGuid().ToString('N')+'.txt')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($LoaderZip)
try {
    $lines=foreach($entry in $archive.Entries) {
        if (!$entry.Name -or $entry.FullName -match '\.(ini|txt|md)$') { continue }
        $stream=$entry.Open(); $sha=[Security.Cryptography.SHA256]::Create()
        try { ([BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-',''))+'  '+$entry.FullName }
        finally { $stream.Dispose(); $sha.Dispose() }
    }
    [IO.File]::WriteAllLines($manifest,[string[]]$lines,[Text.UTF8Encoding]::new($false))
} finally { $archive.Dispose() }
$out=Join-Path $repo "dist\LongYinTrainer-Setup-$version.exe"
if(Test-Path -LiteralPath $out){throw "安装器已存在：$out"}
$installerSource=Get-Content (Join-Path $repo 'Installer\Installer.cs') -Raw
$installerSource=[regex]::Replace($installerSource,'internal const string ModVersion = "[0-9.]+";',('internal const string ModVersion = "'+$version+'";'))
$tempSource=Join-Path ([IO.Path]::GetTempPath()) ('LongYinInstaller-'+[guid]::NewGuid().ToString('N')+'.cs')
[IO.File]::WriteAllText($tempSource,$installerSource,[Text.UTF8Encoding]::new($false))
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/resource:'+ $zip+',ModPayload') ('/resource:'+ $LoaderZip+',LoaderPayload') ('/resource:'+ $manifest+',LoaderManifest') ('/out:'+$out) $tempSource
if($LASTEXITCODE -ne 0){throw '安装器编译失败。'}
$hash=(Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($out))" | Set-Content ($out+'.sha256')
Write-Output $out
Write-Output $hash




