param(
 [string]$GameDir=(Join-Path $PSScriptRoot '..\..\player-0.16.13\game'),
 [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$timer=[Diagnostics.Stopwatch]::StartNew()
# Installed dependencies only. Restore explicitly once on a fresh checkout.
& $Dotnet build (Join-Path $root 'SkyCoop\SkyCoop.csproj') -c Release --no-restore -nologo "-p:GameDir=$GameDir" -p:NuGetAudit=false -v:q
if($LASTEXITCODE -ne 0){throw 'Mod build failed'}
foreach($project in @('SessionPolicies','NativeAssets')){
 & $Dotnet run --project (Join-Path $root "tests\$project") -c Release --no-restore -p:NuGetAudit=false
 if($LASTEXITCODE -ne 0){throw "Checks failed: $project"}
}
Write-Host ('Fast checks passed in {0:N1}s. No game process was started.' -f $timer.Elapsed.TotalSeconds)
