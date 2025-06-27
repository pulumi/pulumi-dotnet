GO := go

FALLBACK_DEV_VERSION := 3.0.0-dev.0
DEV_VERSION := $(shell if command -v changie > /dev/null; then changie next patch -p dev.0; else echo "$(FALLBACK_DEV_VERSION)"; fi)
SDK_VERSION := $(shell cd pulumi-language-dotnet && sed -n 's/^.*github\.com\/pulumi\/pulumi\/sdk\/v3 v//p' go.mod)

GO_TEST_FILTER_FLAG := $(if $(TEST_FILTER),-run $(TEST_FILTER))
DOTNET_TEST_FILTER_FLAG := $(if $(TEST_FILTER),--filter $(TEST_FILTER))

.PHONY: all
all: build

.PHONY: build
build: build_sdk build_language_host

.PHONY: build_sdk
build_sdk:
	cd sdk && dotnet build --configuration Release -p:PulumiSdkVersion=$(SDK_VERSION)

.PHONY: build_language_host
build_language_host:
	cd pulumi-language-dotnet && ${GO} build \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" .

.PHONY: changelog
changelog:
	changie new

.PHONY: clean
clean:
	cd sdk && dotnet clean
	rm -rf sdk/*/{bin,obj}

.PHONY: format_check
format_check: format_sdk_check format_language_host_check format_integration_tests_check

.PHONY: format
format: format_sdk format_language_host format_integration_tests

.PHONY: format_sdk_check
format_sdk_check:
	cd sdk && dotnet format --verify-no-changes

.PHONY: format_sdk
format_sdk:
	cd sdk && dotnet format

.PHONY: format_language_host_check
format_language_host_check:
	@problems=$$(gofumpt -l pulumi-language-dotnet); \
	if [ $$? -ne 0 ]; then \
		echo "$$problems"; \
		exit 1; \
	fi

.PHONY: format_language_host
format_language_host:
	gofumpt -w pulumi-language-dotnet

.PHONY: format_integration_tests_check
format_integration_tests_check:
	@problems=$$(gofumpt -l integration_tests); \
	if [ $$? -ne 0 ]; then \
		echo "$$problems"; \
		exit 1; \
	fi

.PHONY: format_integration_tests
format_integration_tests:
	gofumpt -w integration_tests

.PHONY: install
install:
	cd pulumi-language-dotnet && ${GO} install \
		-ldflags "-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=$(DEV_VERSION)" ./...

.PHONY: lint
lint: lint_sdk lint_language_host lint_integration_tests

.PHONY: lint_fix
lint_fix: lint_sdk_fix lint_language_host_fix lint_integration_tests_fix

.PHONY: lint_sdk
lint_sdk: format_sdk_check

.PHONY: lint_sdk_fix
lint_sdk_fix: format_sdk

.PHONY: lint_language_host
lint_language_host: format_language_host_check
	cd pulumi-language-dotnet && golangci-lint run $(GOLANGCI_LINT_ARGS) --config ../.golangci.yml --timeout 5m --path-prefix pulumi-language-dotnet

.PHONY: lint_language_host_fix
lint_language_host_fix: format_language_host
	cd pulumi-language-dotnet && golangci-lint run $(GOLANGCI_LINT_ARGS) --fix --config ../.golangci.yml --timeout 5m --path-prefix pulumi-language-dotnet

.PHONY: lint_integration_tests
lint_integration_tests: format_integration_tests_check
	cd integration_tests && golangci-lint run $(GOLANGCI_LINT_ARGS) --config ../.golangci.yml --timeout 5m --path-prefix integration_tests

.PHONY: lint_integration_tests_fix
lint_integration_tests_fix: format_integration_tests
	cd integration_tests && golangci-lint run $(GOLANGCI_LINT_ARGS) --fix --config ../.golangci.yml --timeout 5m --path-prefix integration_tests

.PHONY: test
test: test_conformance test_integration test_sdk test_sdk_automation

.PHONY: test_fast
test_fast: test_sdk test_sdk_automation

.PHONY: test_conformance
test_conformance: build clean
	cd pulumi-language-dotnet && gotestsum -- $(GO_TEST_FILTER_FLAG) --timeout 60m ./...

.PHONY: test_integration
test_integration: build clean
	cd integration_tests && gotestsum -- $(GO_TEST_FILTER_FLAG) --parallel 1 --timeout 60m ./...

.PHONY: test_sdk
test_sdk: build clean
	cd sdk/Pulumi.Tests && dotnet test --configuration Release $(DOTNET_TEST_FILTER_FLAG)

.PHONY: test_sdk_automation
test_sdk_automation: clean
	cd sdk/Pulumi.Automation.Tests && \
		dotnet test --configuration Release $(DOTNET_TEST_FILTER_FLAG) \
			-p:PulumiSdkVersion=$(SDK_VERSION)

.PHONY: test_coverage
test_coverage: test_sdk_coverage test_sdk_automation_coverage

.PHONY: test_sdk_coverage
test_sdk_coverage: clean
	cd sdk/Pulumi.Tests && \
		dotnet test --configuration Release $(DOTNET_TEST_FILTER_FLAG) -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=./coverage/coverage.pulumi.xml

.PHONY: test_sdk_automation_coverage
test_sdk_automation_coverage: clean
	cd sdk/Pulumi.Automation.Tests && \
		dotnet test --configuration Release $(DOTNET_TEST_FILTER_FLAG) \
			-p:PulumiSdkVersion=$(SDK_VERSION) \
			-p:CollectCoverage=true \
			-p:CoverletOutputFormat=cobertura \
			-p:CoverletOutput=./coverage/coverage.pulumi.automation.xml

.PHONY: help
help:
	@awk 'BEGIN { print "Available targets:" } \
		/^##/ { help=$$0; next } \
		/^[a-zA-Z0-9_-]+:/ { sub(/:.*/, "", $$1); printf "  %-20s %s\n", $$1, help; help="" }' $(MAKEFILE_LIST)
