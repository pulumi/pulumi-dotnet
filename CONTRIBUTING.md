# Contributing

This repo is made up of three main components. The host runtime (pulumi-language-dotnet), the SDK and integration tests.

## Changelog

Changelog management is done via [`changie`](https://changie.dev/).
See the [installation](https://changie.dev/guide/installation/) guide for `changie`.

Run `changie new` in the top level directory. Here is an example of what that looks like:

```shell
$ changie new
✔ Component … sdk
✔ Kind … Improvements
✔ Body … Cool new SDK feature.
✔ GitHub Pull Request … 123
```

## Release

To release a new version use `changie` to update the changelog file, open a PR for that change. Once that PR merges it will trigger a release workflow.

```bash
changie batch auto
changie merge
git add .changes
git commit -m "Changelog for $(changie latest)"
```

After the release, also bump the version in `pulumi/pulumi`.  It needs to be bumped in `scripts/get-language-providers.sh`.
