GO := go

install::
	cd pulumi-language-dotnet && ${GO} install ./...

build::
	cd pulumi-language-dotnet && ${GO} build .

test_integration::
	cd integration_tests && gotestsum -- --parallel 1 --timeout 30m ./...

.PHONY: install build
