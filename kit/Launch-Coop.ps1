param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$parent=Split-Path -Parent $PSScriptRoot
$candidates=@($PSScriptRoot)
$locationFile=Join-Path $parent 'coop-install-location.json'
if(Test-Path -LiteralPath $locationFile){try{$location=Get-Content -LiteralPath $locationFile -Raw|ConvertFrom-Json;if($location.installDirectory){$candidates+=([string]$location.installDirectory)}}catch{Write-Warning 'Could not read install location; checking the default player folder.'}}
$candidates+=(Join-Path $parent 'player')
$installRoot=$null
foreach($candidate in $candidates){$candidate=[IO.Path]::GetFullPath($candidate);if(Test-Path -LiteralPath (Join-Path $candidate 'game\tld.exe')){$installRoot=$candidate;break}}
if(!$installRoot){throw ('Co-op game is not installed here. Run Install.cmd in friend-kit first. After installation, start Play-Coop.cmd in the player folder: '+(Join-Path $parent 'player'))}
$game=Join-Path $installRoot 'game'
if($game -match '[^\x00-\x7F]'){throw 'MelonLoader cannot reliably load from a path containing non-ASCII letters. Close the game and move the entire co-op folder (player and friend-kit together) to a path such as D:\TLD-Coop, then run player\Play-Coop.cmd. No reinstall is needed.'}
if(Test-Path -LiteralPath (Join-Path $installRoot '.update-incomplete')){throw 'Update is incomplete. Run Update.cmd from the new friend-kit again.'}
if(Test-Path -LiteralPath (Join-Path $installRoot '.install-incomplete')){throw 'Installation is incomplete. Complete Install.cmd before starting the game.'}
foreach($file in @('tld.exe','Mods\SkyCoop.dll','Mods\CoopProfile.dll','Mods\multiplayerstuff.unity3d','winhttp.dll','MelonLoader\net6\MelonLoader.dll','MelonLoader\Dependencies\SupportModules\Il2Cpp.dll')){if(!(Test-Path -LiteralPath (Join-Path $game $file))){throw "Missing co-op file in installed player copy: $file. Installation folder: $installRoot"}}
if(!(Test-Path -LiteralPath (Join-Path $installRoot 'runtime\dotnet.exe'))){throw "Portable runtime is missing in $installRoot. Complete installation first."}
if(Test-Path -LiteralPath (Join-Path $game 'Mods\CoopLab.dll')){throw 'Test bot found. Use a production copy without CoopLab.dll'}
if($CheckOnly){[pscustomobject]@{installRoot=$installRoot;game=$game;runtime=(Join-Path $installRoot 'runtime');ready=$true};return}
foreach($running in @(Get-Process tld -ErrorAction SilentlyContinue)){throw 'The Long Dark is already running. Close it normally before starting the co-op copy.'}
Write-Host ("Starting co-op copy: "+$game)
if(!(Get-Process steam -ErrorAction SilentlyContinue)){throw 'Start Steam and sign in first'}
foreach($v in @('COOP_QA_MODE','COOP_QA_STEAM_WIRE','COOP_QA_NATIVE_GENDER','COOP_QA_DROP_FIRST_WELCOME','COOP_QA_DROP_FIRST_ACK','COOP_QA_RUN_ID','COOP_QA_REJOIN','COOP_QA_SEED','COOP_QA_STEAM_HOST_SAVE','COOP_QA_QUIT_STYLE','SKYCOOP_BIND_ADDRESS')){[Environment]::SetEnvironmentVariable($v,$null,'Process')}
$env:SteamAppId='305620';$env:SteamGameId='305620';$env:DOTNET_ROOT=Join-Path $installRoot 'runtime';$env:DOTNET_MULTILEVEL_LOOKUP='0'
$logs=Join-Path $installRoot 'logs';New-Item -ItemType Directory -Force -Path $logs|Out-Null
$log=Join-Path $logs ('play-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.log')
Start-Process -FilePath (Join-Path $game 'tld.exe') -WorkingDirectory $game -WindowStyle Normal -ArgumentList @('--melonloader.disablestartscreen','--melonloader.hideconsole','-logFile',('"'+$log+'"'))
