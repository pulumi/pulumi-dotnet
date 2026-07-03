#!/usr/bin/env bash
#
# Fetches the shared codegen test corpus (tests/testdata/codegen) from
# pulumi/pulumi at the version pinned in pulumi-language-dotnet/go.mod into
# pulumi-language-dotnet/codegen/testdata/upstream (gitignored). The download
# is skipped when the corpus is already present at the pinned version.
#
# Uses cp instead of rsync because this also runs under Git Bash on Windows.

set -euo pipefail

cd "$(dirname "$0")/.."

VERSION=$(sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' pulumi-language-dotnet/go.mod)
if [ -z "$VERSION" ]; then
    echo "error: could not determine the pulumi version from pulumi-language-dotnet/go.mod" >&2
    exit 1
fi

DEST=pulumi-language-dotnet/codegen/testdata/upstream
if [ -f "$DEST/.version" ] && [ "$(cat "$DEST/.version")" = "v$VERSION" ]; then
    exit 0
fi

echo "Fetching the codegen test corpus from pulumi/pulumi v$VERSION"

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

git -c advice.detachedHead=false clone --quiet --depth 1 --branch "v$VERSION" --filter=blob:none --sparse \
    https://github.com/pulumi/pulumi.git "$tmp"
git -C "$tmp" sparse-checkout set --no-cone tests/testdata/codegen

rm -rf "$DEST"
mkdir -p "$DEST"
cp -R "$tmp/tests/testdata/codegen" "$DEST/codegen"
echo "v$VERSION" > "$DEST/.version"
