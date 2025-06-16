GO := go

# Try to get the dev version using changie, otherwise fall back
FALLBACK_DEV_VERSION := 3.0.0-dev.0
DEV_VERSION := $(shell if command -v changie > /dev/null; then changie next patch -p dev.0; else echo "$(FALLBACK_DEV_VERSION)"; fi)
SDK_VERSION := $(shell cd pulumi-language-dotnet && sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' go.mod)

install::
	cd pulumi-language-dotnet && ${GO} install \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" ./...

build: build_sdk build_language_host

build_sdk:
	cd sdk && dotnet restore --no-cache
	cd sdk && dotnet build --configuration Release -p:PulumiSdkVersion=$(SDK_VERSION)

build_language_host:
	cd pulumi-language-dotnet && ${GO} build \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" .

changelog::
	changie new

clean:
	cd sdk && dotnet clean
	rm -rf {bin,obj} sdk/*/{bin,obj}

test_integration:: build
	cd integration_tests && gotestsum -- --parallel 1 --timeout 60m ./...

test_conformance:: build
	cd pulumi-language-dotnet && gotestsum -- --timeout 60m ./...

# Relative paths to directories with go.mod files that should be linted.
LINT_GOLANG_PKGS := pulumi-language-dotnet integration_tests

lint::
	$(eval GOLANGCI_LINT_CONFIG = $(shell pwd)/.golangci.yml)
	@$(foreach pkg,$(LINT_GOLANG_PKGS),(cd $(pkg) && \
		echo "[golangci-lint] Linting $(pkg)..." && \
		golangci-lint run $(GOLANGCI_LINT_ARGS) \
			--config $(GOLANGCI_LINT_CONFIG) \
			--timeout 5m \
			--path-prefix $(pkg)) \
		&&) true

.PHONY: install build build_language_host build_sdk clean
