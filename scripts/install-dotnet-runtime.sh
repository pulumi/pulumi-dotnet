#!/usr/bin/env bash
# Installs the .NET 6.0 runtime next to the SDK that the mise `vfox:dotnet`
# plugin provisions. The Pulumi .NET SDK targets net6.0, so the 6.0 runtime is
# required in addition to the 8.0 SDK.
#
# Invoked from the `vfox:dotnet` postinstall hook in .mise.toml on non-Windows
# platforms. Kept as a dedicated script (rather than an inline postinstall
# command) to avoid nested shell quoting across the shells mise uses to run the
# hook on different platforms.
set -euo pipefail

curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
  --version 6.0.427 \
  --install-dir "$MISE_TOOL_INSTALL_PATH" \
  --no-path \
  --skip-non-versioned-files
