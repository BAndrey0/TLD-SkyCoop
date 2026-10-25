#!/bin/sh
# Builds SkyCoop.dll with the portable SDK in ../../tools/dotnet.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/../.." && pwd -W)"
export DOTNET_CLI_HOME="$ROOT/tools/dotnet-home" NUGET_PACKAGES="$ROOT/tools/nuget-packages"
exec "$ROOT/tools/dotnet/dotnet.exe" build "$(dirname "$0")/SkyCoop.csproj" -c Release -nologo "$@"
