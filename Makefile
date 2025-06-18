GO := go

# Try to get the dev version using changie, otherwise fall back
FALLBACK_DEV_VERSION := 3.0.0-dev.0
DEV_VERSION := $(shell if command -v changie > /dev/null; then changie next patch -p dev.0; else echo "$(FALLBACK_DEV_VERSION)"; fi)
SDK_VERSION := $(shell cd pulumi-language-dotnet && sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' go.mod)

.PHONY: install
install:
	cd pulumi-language-dotnet && ${GO} install \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" ./...

.PHONY: build
build: build_sdk build_language_host

.PHONY: build_sdk
build_sdk:
	cd sdk && dotnet build --configuration Release -p:PulumiSdkVersion=$(SDK_VERSION)

.PHONY: build_language_host
build_language_host:
	cd pulumi-language-dotnet && ${GO} build \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" .

changelog::
	changie new

.PHONY: clean
clean:
	cd sdk && dotnet clean
	rm -rf {bin,obj} sdk/*/{bin,obj}

.PHONY: format
format: format_sdk format_language_host format_integration_tests

.PHONY: format_fix
format_fix: format_sdk_fix format_language_host_fix format_integration_tests_fix

.PHONY: format_sdk
format_sdk:
	cd sdk && dotnet format --verify-no-changes

.PHONY: format_sdk_fix
format_sdk_fix:
	cd sdk && dotnet format

.PHONY: format_language_host
format_language_host:
	@problems=$(gofumpt -l pulumi-language-dotnet); \
	if [ -n "$$problems" ]; then \
		echo "$$problems"; \
		exit 1; \
	fi

.PHONY: format_language_host_fix
format_language_host_fix:
	gofumpt -w pulumi-language-dotnet

.PHONY: format_integration_tests
format_integration_tests:
	@problems=$$(gofumpt -l integration_tests); \
	if [ -n "$$problems" ]; then \
		echo "$$problems"; \
		exit 1; \
	fi

.PHONY: format_integration_tests_fix
format_integration_tests_fix:
	gofumpt -w integration_tests

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
