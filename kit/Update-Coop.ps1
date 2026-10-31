param([string]$InstallDirectory,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$parent=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest.files){
 $file=Join-Path $PSScriptRoot ('files\'+$entry.name)
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256){throw "Kit checksum mismatch: $($entry.name)"}
}
if(!$InstallDirectory){
 $pointer=Join-Path $parent 'coop-install-location.json'
 if(Test-Path -LiteralPath $pointer){$recorded=[string](Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json).installDirectory;if($recorded -and (Test-Path -LiteralPath (Join-Path $recorded 'game\tld.exe'))){$InstallDirectory=$recorded}}
 if(!$InstallDirectory){$InstallDirectory=Join-Path $parent 'player'}
}
$target=[IO.Path]::GetFullPath($InstallDirectory)
$game=Join-Path $target 'game'
if(!(Test-Path -LiteralPath (Join-Path $game 'Mods\CoopProfile.dll')) -or !(Test-Path -LiteralPath (Join-Path $target 'runtime\dotnet.exe'))){throw 'Installed co-op copy not found. Run Install.cmd first, or specify -InstallDirectory.'}
if(Test-Path -LiteralPath (Join-Path $game 'Mods\CoopLab.dll')){throw 'Refusing to update a test-bot profile'}
if((Get-FileHash -LiteralPath (Join-Path $game 'tld.exe') -Algorithm SHA256).Hash -ne $manifest.gameExecutableSHA256){throw "Game version mismatch; requires $($manifest.targetGame)"}
if((Get-FileHash -LiteralPath (Join-Path $game 'GameAssembly.dll') -Algorithm SHA256).Hash -ne $manifest.gameAssemblySHA256){throw "GameAssembly version mismatch; requires $($manifest.targetGame)"}
foreach($process in @(Get-Process tld -ErrorAction SilentlyContinue)){
 if(!$process.Path -or $process.Path -eq (Join-Path $game 'tld.exe')){throw 'Close the co-op game normally through its pause menu, then run Update.cmd again.'}
}
$destinations=@{'SkyCoop.dll'='Mods\SkyCoop.dll';'CoopProfile.dll'='Mods\CoopProfile.dll';'Steamworks.NET.dll'='UserLibs\Steamworks.NET.dll'}
foreach($entry in $manifest.files){if(!$destinations.ContainsKey([string]$entry.name)){throw 'Unexpected manifest entry'}}
if($CheckOnly){[pscustomobject]@{installRoot=$target;version=$manifest.localRevision;ready=$true;saveChanges=$false};return}
$backup=Join-Path $target ('updates\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach($entry in $manifest.files){$old=Join-Path $game $destinations[[string]$entry.name];if(Test-Path -LiteralPath $old){Copy-Item -LiteralPath $old -Destination (Join-Path $backup $entry.name)}}
foreach($name in @('Launch-Coop.ps1','Play-Coop.cmd')){if(Test-Path -LiteralPath (Join-Path $target $name)){Copy-Item -LiteralPath (Join-Path $target $name) -Destination (Join-Path $backup $name)}}
$marker=Join-Path $target '.update-incomplete'
'Update in progress. Run Update.cmd again if interrupted.' | Set-Content -LiteralPath $marker
foreach($entry in $manifest.files){
 $destination=Join-Path $game $destinations[[string]$entry.name]
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('files\'+$entry.name)) -Destination $destination -Force
 if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.sha256){throw "Installed file checksum mismatch: $($entry.name)"}
}
foreach($name in @('Launch-Coop.ps1','Play-Coop.cmd')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $target $name) -Force}
# SkyCoopHotfix.dll fixes are built into SkyCoop.dll 0.14.2; keeping it would patch the same code twice.
$obsolete=Join-Path $game 'Mods\SkyCoopHotfix.dll'
if(Test-Path -LiteralPath $obsolete){Move-Item -LiteralPath $obsolete -Destination (Join-Path $backup 'SkyCoopHotfix.dll') -Force}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $target 'coop-version.json')
Remove-Item -LiteralPath $marker
Write-Host "Updated: $target"
Write-Host "Backup: $backup"
Write-Host 'Save files were not changed. Start player\Play-Coop.cmd. Every player needs this update.'