# Builds SkyCoop.dll and CoopProfile.dll and assembles the player kit:
#   out\friend-kit\{Install.cmd, Update.cmd, ..., files\*.dll, manifest.json, licenses\}
#   out\TLD-SkyCoop-<version>.zip
# Requires a MelonLoader-installed co-op game copy (for the generated Il2Cpp assemblies), see README.md.
param(
	[string]$GameDir = (Join-Path $PSScriptRoot '..\..\player\game'),
	[string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$GameDir = [IO.Path]::GetFullPath($GameDir)
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))) {
	throw "No MelonLoader Il2Cpp assemblies in $GameDir. Install the kit once and start the game to generate them, or pass -GameDir."
}

foreach ($project in @('SkyCoop\SkyCoop.csproj', 'CoopProfile\CoopProfile.csproj')) {
	& $Dotnet build (Join-Path $root $project) -c Release -nologo "-p:GameDir=$GameDir"
	if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}

$version = [regex]::Match((Get-Content -LiteralPath (Join-Path $root 'SkyCoop\Properties\AssemblyInfo.cs') -Raw), 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value

$out = Join-Path $root 'out'
$kit = Join-Path $out 'friend-kit'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $kit 'files'), (Join-Path $kit 'licenses') | Out-Null

Copy-Item -Path (Join-Path $root 'kit\*') -Destination $kit
Copy-Item -Path (Join-Path $root 'licenses\*') -Destination (Join-Path $kit 'licenses')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $kit 'licenses\TLD-SkyCoop-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $root 'SkyCoop\bin\Release\netcoreapp6.0\SkyCoop.dll') -Destination (Join-Path $kit 'files')
Copy-Item -LiteralPath (Join-Path $root 'CoopProfile\bin\Release\netcoreapp6.0\CoopProfile.dll') -Destination (Join-Path $kit 'files')
# Steamworks.NET (MIT) as shipped with the game copy's UserLibs by the original kit.
Copy-Item -LiteralPath (Join-Path $GameDir 'UserLibs\Steamworks.NET.dll') -Destination (Join-Path $kit 'files')

$files = foreach ($name in @('CoopProfile.dll', 'SkyCoop.dll', 'Steamworks.NET.dll')) {
	[ordered]@{ sha256 = (Get-FileHash -LiteralPath (Join-Path $kit "files\$name") -Algorithm SHA256).Hash; name = $name }
}
$commit = (& git -C $root rev-parse --short HEAD 2>$null)
$manifest = [ordered]@{
	gameAssemblySHA256 = '4CFA661139B20C257853D0EFA4F69B5BD394D12EC1484DB73BC824129C192C6F'
	files = @($files)
	source = 'https://github.com/BAndrey0/TLD-SkyCoop'
	sourceCommit = "$commit"
	targetGame = 'Steam 22915504 / Survival 2.55'
	localRevision = "survival-2.55-$version"
	modVersion = $version
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $kit 'manifest.json') -Encoding utf8

$zip = Join-Path $out "TLD-SkyCoop-$version.zip"
Compress-Archive -Path $kit -DestinationPath $zip
Write-Host "Kit: $kit"
Write-Host "Zip: $zip"
