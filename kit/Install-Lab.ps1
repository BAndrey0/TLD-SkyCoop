param([string]$GameDirectory,[string]$InstallDirectory=(Join-Path $PSScriptRoot '..\player'),[switch]$PlanOnly)
$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw|ConvertFrom-Json
foreach($entry in $manifest.files){$p=Join-Path $PSScriptRoot ('files\'+$entry.name);if((Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $entry.sha256){throw "Kit file checksum mismatch: $($entry.name)"}}
if(!$GameDirectory){$GameDirectory=Read-Host 'Path to your Steam TheLongDark folder'}
$source=[IO.Path]::GetFullPath($GameDirectory.Trim('"'));$target=[IO.Path]::GetFullPath($InstallDirectory)
$game=Join-Path $target 'game';$cache=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\downloads'))
if($game -match '[^\x00-\x7F]'){throw 'The co-op installation path must use English letters/numbers. Move friend-kit to a folder such as D:\TLD-Coop\friend-kit and run Install.cmd there. The original Steam installation can stay where it is.'}
if($game.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $game -eq $source){throw 'The separate copy must be outside your original game folder'}
$exe=Join-Path $source 'tld.exe'
if(!(Test-Path -LiteralPath $exe) -or (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.gameExecutableSHA256){throw "This kit requires Steam The Long Dark $($manifest.targetGame). Executable does not match."}
if((Get-FileHash -LiteralPath (Join-Path $source 'GameAssembly.dll') -Algorithm SHA256).Hash -ne $manifest.gameAssemblySHA256){throw "GameAssembly does not match tested $($manifest.targetGame). Do not install this kit on another game update."}
if(Test-Path -LiteralPath (Join-Path $source 'MelonLoader')){throw 'Select the original unmodified Steam game, not another modded copy'}
if(Test-Path -LiteralPath $game){throw 'Target game folder already exists. Select a new empty installation directory.'}
if($PlanOnly){[pscustomobject]@{source=$source;target=$target;gameVersion=$manifest.targetGame;copyGameGB=21.4;downloadMB=103;originalSaves='untouched';testBots='excluded'};return}
New-Item -ItemType Directory -Force -Path $target,$cache|Out-Null
$marker=Join-Path $target '.install-incomplete';'Installation in progress'|Set-Content -LiteralPath $marker
function Fetch($name,$url,$hash){$p=Join-Path $cache $name;if(!(Test-Path -LiteralPath $p)){[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $p};if((Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $hash){throw "Checksum mismatch: $name"};return $p}
$loader=Fetch 'MelonLoader-0.7.3-x64.zip' 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip' '5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416'
$assets=Fetch 'SkyCoop-v13.2.zip' 'https://github.com/Filigrani/SkyCoop/releases/download/v13.2/SkyCoopRelease.zip' '6FEE0723EBD72AF22C5362AA7DE966881875962305599DC38F321118E0B661B2'
$runtime=Fetch 'dotnet-runtime-6.0.36-win-x64.zip' 'https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.36/dotnet-runtime-6.0.36-win-x64.zip' 'C077CCC342C5571CEB7A3C06D1DFBC99B989ED5A6E0F3A0B0B450531C1259615'
Write-Host 'Copying your game to a separate folder (about 21.4 GB)...'
& robocopy $source $game /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
if($LASTEXITCODE -ge 8){throw "Game copy failed: robocopy $LASTEXITCODE"}
$unpack=Join-Path $target 'setup-cache';New-Item -ItemType Directory -Force -Path $unpack|Out-Null
Expand-Archive -LiteralPath $loader -DestinationPath (Join-Path $unpack 'loader') -Force
Expand-Archive -LiteralPath $assets -DestinationPath (Join-Path $unpack 'assets') -Force
Expand-Archive -LiteralPath $runtime -DestinationPath (Join-Path $target 'runtime') -Force
Copy-Item -LiteralPath (Join-Path $unpack 'loader\MelonLoader') -Destination (Join-Path $game 'MelonLoader') -Recurse
Copy-Item -LiteralPath (Join-Path $unpack 'loader\version.dll') -Destination (Join-Path $game 'winhttp.dll')
New-Item -ItemType Directory -Force -Path (Join-Path $game 'Mods'),(Join-Path $game 'UserLibs')|Out-Null
$bundle=@(Get-ChildItem -LiteralPath (Join-Path $unpack 'assets') -Recurse -File -Filter 'multiplayerstuff.unity3d');if($bundle.Count -ne 1){throw 'Unexpected asset archive structure'}
Copy-Item -LiteralPath $bundle[0].FullName -Destination (Join-Path $game 'Mods\multiplayerstuff.unity3d')
foreach($name in @('SkyCoop.dll','CoopProfile.dll')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot "files\$name") -Destination (Join-Path $game "Mods\$name")}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'files\Steamworks.NET.dll') -Destination (Join-Path $game 'UserLibs\Steamworks.NET.dll')
[IO.File]::WriteAllText((Join-Path $game 'steam_appid.txt'),'305620',[Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Launch-Coop.ps1') -Destination (Join-Path $target 'Launch-Coop.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Play-Coop.cmd') -Destination (Join-Path $target 'Play-Coop.cmd')
# The archive launcher can find custom installation directories too.
$locationFile=Join-Path (Split-Path -Parent $PSScriptRoot) 'coop-install-location.json'
@{installDirectory=$target}|ConvertTo-Json|Set-Content -LiteralPath $locationFile -Encoding utf8
Remove-Item -LiteralPath $marker
Write-Host "Installed separate co-op copy: $target. Start Play-Coop.cmd."
