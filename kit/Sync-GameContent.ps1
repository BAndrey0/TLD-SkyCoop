param([Parameter(Mandatory=$true)][string]$GameDirectory,[Parameter(Mandatory=$true)][string]$InstallDirectory,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
$install=[IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$target=Join-Path $install 'game'
if($source -eq $target){throw 'Select the original Steam game, not the co-op copy.'}
foreach($root in @($source,$target)){if(!(Test-Path -LiteralPath (Join-Path $root 'GameAssembly.dll'))){throw "Game not found: $root"}}
if((Get-FileHash -LiteralPath (Join-Path $source 'GameAssembly.dll')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target 'GameAssembly.dll')).Hash){throw 'Steam game and co-op copy have different versions. Update the mod/game before syncing DLC.'}
foreach($p in @(Get-Process tld -ErrorAction SilentlyContinue)){throw 'Close The Long Dark before syncing DLC.'}
$content=Join-Path $source 'tld_dlc'
$changed=@()
if(Test-Path -LiteralPath $content){
 foreach($file in Get-ChildItem -LiteralPath $content -Recurse -File){
  $relative=$file.FullName.Substring($content.Length).TrimStart('\')
  $destination=[IO.Path]::GetFullPath((Join-Path (Join-Path $target 'tld_dlc') $relative))
  if(!$destination.StartsWith(([IO.Path]::GetFullPath((Join-Path $target 'tld_dlc'))+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe DLC destination'}
  $old=Get-Item -LiteralPath $destination -ErrorAction SilentlyContinue
  if(!$old -or $old.Length -ne $file.Length -or $old.LastWriteTimeUtc -ne $file.LastWriteTimeUtc){$changed+=@{source=$file.FullName;destination=$destination;bytes=$file.Length}}
 }
}
$bytes=($changed | ForEach-Object {$_.bytes} | Measure-Object -Sum).Sum
if(!$bytes){$bytes=0}
$dlc=@(Get-ChildItem -LiteralPath $content -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
if($CheckOnly){[pscustomobject]@{changedFiles=$changed.Count;copyBytes=$bytes;content=($dlc -join ', ')};return}
$drive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($target))
if($bytes -gt 0 -and $drive.AvailableFreeSpace -lt $bytes+128MB){throw 'Not enough disk space for the newly installed DLC.'}
foreach($entry in $changed){
 New-Item -ItemType Directory -Force -Path (Split-Path -Parent $entry.destination)|Out-Null
 Copy-Item -LiteralPath $entry.source -Destination $entry.destination -Force
 if((Get-FileHash -LiteralPath $entry.source).Hash -ne (Get-FileHash -LiteralPath $entry.destination).Hash){throw "DLC copy verification failed: $($entry.destination)"}
}
Write-Host ("DLC content synced: {0} changed files, {1:N1} MB. Available: {2}" -f $changed.Count,($bytes/1MB),($dlc -join ', '))
@{gameDirectory=$source}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $install 'game-source.json') -Encoding utf8
