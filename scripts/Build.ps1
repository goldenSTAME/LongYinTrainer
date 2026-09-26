param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$GamePath=(Resolve-Path -LiteralPath $GamePath).Path
$project=Join-Path $repo 'LongYinTrainer\LongYinTrainer.csproj'
dotnet build $project -c Release "-p:GamePath=$GamePath" --nologo
if($LASTEXITCODE -ne 0){throw '构建失败'}
