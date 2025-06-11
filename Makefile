GO := go

# Try to get the dev version using changie, otherwise fall back
FALLBACK_DEV_VERSION := 3.0.0-dev.0
DEV_VERSION := $(shell if command -v changie > /dev/null; then changie next patch -p dev.0; else echo "$(FALLBACK_DEV_VERSION)"; fi)

install::
	cd pulumi-language-dotnet && ${GO} install \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" ./...

build::
	cd pulumi-language-dotnet && ${GO} build \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" .

changelog::
	changie new

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

# Test the language plugin
test-language-plugin::
	@echo "Testing pulumi-language-dotnet Plugin"
	rm -f pulumi-language-dotnet/pulumi-language-dotnet
	cd pulumi-language-dotnet && ${GO} test

.PHONY: install build
