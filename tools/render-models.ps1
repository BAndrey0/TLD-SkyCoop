# One automated render fixture; no world load, server, UI input, or player saves.
param([string]$GameDir=(Join-Path $PSScriptRoot '..\..\player\game'),[switch]$Alignment)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$GameDir=[IO.Path]::GetFullPath($GameDir)
if(Get-Process tld -ErrorAction SilentlyContinue){throw 'Close the game before the model probe.'}
if(!(Test-Path -LiteralPath (Join-Path $GameDir 'UserData\CharacterSkins\skins.index'))){throw 'Prepare costumes once through F4 before running the quick render.'}
$qa=Join-Path $root '.model-qa';New-Item -ItemType Directory -Path $qa -Force|Out-Null
$installed=Join-Path $GameDir 'Mods\SkyCoop.dll'
$built=Join-Path $root 'SkyCoop\bin\Release\netcoreapp6.0\SkyCoop.dll'
if(!(Test-Path -LiteralPath $built)){throw 'Build SkyCoop first with check-fast.ps1.'}
$backup=Join-Path $qa 'original.dll';Copy-Item -LiteralPath $installed -Destination $backup -Force
$digest=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
$render=Join-Path $qa ('models-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.jpg')
$vars=@{DOTNET_ROOT=(Join-Path (Split-Path $GameDir -Parent) 'runtime');DOTNET_MULTILEVEL_LOOKUP='0';SteamAppId='305620';SteamGameId='305620';SKYCOOP_TEST_SAVE_ROOT=(Join-Path $qa 'saves');SKYCOOP_MODEL_ONLY_TEST='1';SKYCOOP_MODEL_RENDER=$render}
$saved=@{};$child=$null
if($Alignment){$vars['SKYCOOP_MODEL_ALIGNMENT_TEST']='1'}
try{
 Copy-Item -LiteralPath $built -Destination $installed -Force
 foreach($key in $vars.Keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$vars[$key],'Process')}
 $log=Join-Path $qa 'engine.log'
 $child=Start-Process -FilePath (Join-Path $GameDir 'tld.exe') -WorkingDirectory $GameDir -WindowStyle Hidden -ArgumentList ('--melonloader.disablestartscreen --melonloader.hideconsole -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "'+$log+'"') -PassThru
 Write-Host 'Rendering four costume poses; the test exits automatically (90s limit).'
 $timer=[Diagnostics.Stopwatch]::StartNew()
 while(!$child.WaitForExit(1000)){if((Test-Path -LiteralPath $render) -and (Get-Item -LiteralPath $render).Length -gt 1000){$null=$child.WaitForExit(2000);break};if($timer.Elapsed.TotalSeconds>90){throw 'Model render timed out.'}}
 if(!(Test-Path -LiteralPath $render)){throw ('No render produced; inspect '+$log)}
 Write-Host ('Render: '+$render)
}finally{
 if($child -and !$child.HasExited){$child.Kill();if(!$child.WaitForExit(8000)){throw 'Test process did not stop; restore original.dll manually before playing.'}}
 foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}
 Copy-Item -LiteralPath $backup -Destination $installed -Force
 if((Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne $digest){throw 'Original DLL checksum mismatch after model probe.'}
}
