#!/usr/bin/env bash
#
# checkout_pulumi.sh <dest> <sparse-path>...
#
# Sparse checkout of pulumi/pulumi into <dest>, restricted to the given paths,
# at the version pinned as github.com/pulumi/pulumi/sdk/v3 in
# pulumi-language-dotnet/go.mod.

set -euo pipefail

cd "$(dirname "$0")/.."

dest=$1
shift

VERSION=$(sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' pulumi-language-dotnet/go.mod)
if [ -z "$VERSION" ]; then
    echo "error: could not determine the pulumi version from pulumi-language-dotnet/go.mod" >&2
    exit 1
fi

if echo "$VERSION" | grep -Eq '\.[0-9]{14}-[0-9a-f]{12}$'; then
    # A pseudo-version has no tag to clone; resolve it to its commit through
    # the module proxy and fetch by hash.
    hash=$(curl -fsSL "https://proxy.golang.org/github.com/pulumi/pulumi/sdk/v3/@v/v$VERSION.info" |
        sed -n 's/.*"Hash"[[:space:]]*:[[:space:]]*"\([0-9a-f]\{40\}\)".*/\1/p')
    if [ -z "$hash" ]; then
        echo "error: could not resolve pseudo-version v$VERSION to a commit" >&2
        exit 1
    fi
    git init --quiet "$dest"
    git -C "$dest" remote add origin https://github.com/pulumi/pulumi.git
    git -C "$dest" sparse-checkout set --no-cone "$@"
    git -C "$dest" fetch --quiet --depth 1 --filter=blob:none origin "$hash"
    git -C "$dest" -c advice.detachedHead=false checkout --quiet FETCH_HEAD
else
    git -c advice.detachedHead=false clone --quiet --depth 1 --branch "v$VERSION" --filter=blob:none --sparse \
        https://github.com/pulumi/pulumi.git "$dest"
    git -C "$dest" sparse-checkout set --no-cone "$@"
fi
