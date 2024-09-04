GO := go

install::
	cd pulumi-language-dotnet && ${GO} install ./...

build::
	cd pulumi-language-dotnet && ${GO} build .

.PHONY: install build
