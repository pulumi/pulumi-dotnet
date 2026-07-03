# testprovider_go

A copy of the Go test provider from
[pulumi/pulumi `tests/testprovider`](https://github.com/pulumi/pulumi/tree/master/tests/testprovider),
vendored when the `pulumi` git submodule was removed.

It is used by integration tests that need provider behavior only the Go
provider implements — for example, `FlakyCreate` returns the
`ErrorResourceInitFailed` gRPC detail that marks a create failure as
retryable, which the engine requires to invoke error hooks.

The provider runs shimless (see `PulumiPlugin.yaml`): the Pulumi CLI builds it
from source with the Go toolchain when a test references it.
