GO := go

install::
	cd pulumi-language-dotnet && ${GO} install ./...

build::
	cd pulumi-language-dotnet && ${GO} build .

test_integration:: build
	cd integration_tests && go test --parallel 1 --timeout 30m ./... -v -run TestProviderConstructUnknown

.PHONY: install build
