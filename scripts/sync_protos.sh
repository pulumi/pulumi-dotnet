#!/usr/bin/env bash
#
# Syncs assets vendored from pulumi/pulumi at the version pinned in
# pulumi-language-dotnet/go.mod:
#
#   proto/                                                    <- proto/
#   sdk/Pulumi.Automation.Codegen/automation-overrides.json   <- tools/automation/automation-overrides.json
#
# Run this whenever the github.com/pulumi/pulumi/{pkg,sdk}/v3 dependencies are
# updated. CI verifies that the vendored files match the pinned version.

set -euo pipefail

cd "$(dirname "$0")/.."

VERSION=$(sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' pulumi-language-dotnet/go.mod)
if [ -z "$VERSION" ]; then
    echo "error: could not determine the pulumi version from pulumi-language-dotnet/go.mod" >&2
    exit 1
fi

echo "Syncing vendored files from pulumi/pulumi v$VERSION"

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

git -c advice.detachedHead=false clone --quiet --depth 1 --branch "v$VERSION" --filter=blob:none --sparse \
    https://github.com/pulumi/pulumi.git "$tmp"
git -C "$tmp" sparse-checkout set --no-cone proto tools/automation

rsync -a --delete "$tmp/proto/" proto/
cp "$tmp/tools/automation/automation-overrides.json" sdk/Pulumi.Automation.Codegen/automation-overrides.json
