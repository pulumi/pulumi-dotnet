#!/usr/bin/env python3

"""
Generate changie fragments for dependency bumps that Renovate applied.

Invoked from `make renovate` (which is itself called from a renovate postUpgradeTasks hook,
see renovate.json5). Detects bumps by diffing the working tree against HEAD and writes one
fragment per bumped dependency via `changie new`. Only released artifacts get entries:
NuGet packages under sdk/ and Go modules in pulumi-language-dotnet/. No-op when nothing
relevant changed.

Runs before the PR exists, so fragments carry no PR number; the `dependencies` kind in
.changie.yaml is formatted accordingly.
"""

import re
import subprocess
import sys

# Matches an added csproj line like: <PackageReference Include="Grpc.Tools" Version="2.63.0">
CSPROJ_RE = re.compile(r'<PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"')

# Matches an added go.mod require line like: `\tgithub.com/pkg/errors v0.9.1` (direct
# dependencies only: `// indirect` lines and `replace` directives don't match).
GOMOD_RE = re.compile(r"^\t?(?:require )?([A-Za-z0-9._/-]+) (v[0-9][\w.+-]*)$")

# Pinned to the changie version in .mise.toml; `go run` because the renovate container
# has Go but not our mise toolchain.
CHANGIE = ["go", "run", "github.com/miniscruff/changie@v1.24.2", "new"]


def bumped_dependencies(diff: str):
    """Yield (component, name, version) tuples for added lines in a unified diff."""
    path = None
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            path = line[len("+++ b/") :]
        elif line.startswith("+") and not line.startswith("+++"):
            if path.startswith("sdk/") and path.endswith(".csproj"):
                match = CSPROJ_RE.search(line[1:])
                if match:
                    yield "sdk", match.group(1), match.group(2)
            elif path == "pulumi-language-dotnet/go.mod":
                match = GOMOD_RE.match(line[1:])
                if match:
                    yield "runtime", match.group(1), match.group(2)


def main():
    diff = subprocess.run(
        ["git", "diff", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout

    for component, name, version in sorted(set(bumped_dependencies(diff))):
        slug = re.sub(r"[^a-z0-9]+", "-", f"{name}-{version}".lower()).strip("-")
        fragment = subprocess.run(
            CHANGIE
            + [
                "--dry-run",
                "--component",
                component,
                "--kind",
                "dependencies",
                "--body",
                f"Update {name} to {version}",
            ],
            check=True,
            capture_output=True,
            text=True,
        ).stdout
        with open(f".changes/unreleased/dependencies-{slug}.yaml", "w") as f:
            f.write(fragment)


if __name__ == "__main__":
    sys.exit(main())
