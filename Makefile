GO := go

WORKING_DIR := $(shell pwd)

# Try to get the dev version using changie, otherwise fall back
FALLBACK_DEV_VERSION := 3.0.0-dev.0
DEV_VERSION := $(shell if command -v changie > /dev/null; then changie next patch -p dev.0; else echo "$(FALLBACK_DEV_VERSION)"; fi)

# Ensure all directories exist before evaluating targets to avoid issues with `touch` creating directories.
_ := $(shell mkdir -p .make bin)

install::
	cd pulumi-language-dotnet && ${GO} install \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/version.Version=$(DEV_VERSION)" ./...

build::
	cd pulumi-language-dotnet && ${GO} build \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/version.Version=$(DEV_VERSION)" .

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

.PHONY: install build

clean:
	rm -rf sdk/{dotnet,nodejs,go,python}
	rm -rf bin/*
	rm -rf .make/*
	if dotnet nuget list source | grep "$(WORKING_DIR)/nuget"; then \
		dotnet nuget remove source "$(WORKING_DIR)/nuget" \
	; fi
.PHONY: clean

install_dotnet_sdk: .make/install_dotnet_sdk
.make/install_dotnet_sdk: .make/build_dotnet
	mkdir -p nuget
	find sdk -name '*.nupkg' -path '*/Release/*' -print -exec cp -p "{}" ${WORKING_DIR}/nuget \;
	if ! dotnet nuget list source | grep "${WORKING_DIR}/nuget"; then \
		dotnet nuget add source "${WORKING_DIR}/nuget" --name "${WORKING_DIR}/nuget" \
	; fi
	@touch $@

install_dotnet_sdk_debug: .make/install_dotnet_sdk_debug
.make/install_dotnet_sdk_debug: .make/build_dotnet
	mkdir -p nuget
	find sdk -name '*.nupkg' -path '*/Debug/*' -print -exec cp -p "{}" ${WORKING_DIR}/nuget \;
	if ! dotnet nuget list source | grep "${WORKING_DIR}/nuget"; then \
		dotnet nuget add source "${WORKING_DIR}/nuget" --name "${WORKING_DIR}/nuget" \
	; fi
	@touch $@

build_dotnet: .make/build_dotnet
.make/build_dotnet:
	cd sdk/ && dotnet build
	@touch $@

.PHONY: build_dotnet

