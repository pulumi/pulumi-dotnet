#!/usr/bin/env pwsh
# Installs the .NET 6.0 runtime next to the SDK that the mise `vfox:dotnet`
# plugin provisions. The Pulumi .NET SDK targets net6.0, so the 6.0 runtime is
# required in addition to the 8.0 SDK.
#
# Invoked from the `vfox:dotnet` postinstall hook in .mise.toml on Windows.
# Kept as a dedicated script (rather than an inline postinstall command) to
# avoid nested bash/PowerShell quoting, which is impossible to get right across
# the shells mise uses to run the hook on different platforms.
$ErrorActionPreference = 'Stop'

$installer = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
& $installer -Version 6.0.427 -InstallDir $env:MISE_TOOL_INSTALL_PATH -NoPath -SkipNonVersionedFiles
