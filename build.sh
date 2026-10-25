#!/bin/sh
# Builds SkyCoop.dll with the portable SDK in ../../tools/dotnet.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export DOTNET_CLI_HOME="$(dirname "$0")/../../tools/dotnet-home" NUGET_PACKAGES="$(dirname "$0")/../../tools/nuget-packages"
exec "$(dirname "$0")/../../tools/dotnet/dotnet.exe" build "$(dirname "$0")/SkyCoop.csproj" -c Release -nologo "$@"
