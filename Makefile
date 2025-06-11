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

# Publish SDKs to NuGet
publish-sdks::
	@echo "Publishing Pulumi SDKs to NuGet"
	@if [ -z "$$NUGET_PUBLISH_KEY" ]; then echo "Missing NUGET_PUBLISH_KEY" && exit 1; fi
	@if [ -z "$$PULUMI_VERSION" ]; then echo "Missing PULUMI_VERSION" && exit 1; fi
	$(eval SDK_VERSION := $(shell cd pulumi-language-dotnet && grep -oP 'github.com/pulumi/pulumi/sdk/v3 v\K[0-9.]+' go.mod))
	for project in Pulumi Pulumi.Automation Pulumi.FSharp; do \
	  PKG_EXISTS=$$(curl -s "https://api.nuget.org/v3-flatcontainer/$$project/index.json" | jq -e ".versions | index(\"$$PULUMI_VERSION\")" > /dev/null 2>&1 && echo yes || echo no); \
	  if [ "$$PKG_EXISTS" = "yes" ]; then \
	    echo "$$project $$PULUMI_VERSION already published, skipping"; \
	  else \
	    echo "Publishing $$project $$PULUMI_VERSION"; \
	    cd sdk/$$project && dotnet build --configuration Release -p:Version=$$PULUMI_VERSION -p:PulumiSdkVersion=$(SDK_VERSION); \
	    PKG_FILE=$$(ls bin/Release/*.nupkg | head -n1); \
	    dotnet nuget push $$PKG_FILE -s https://api.nuget.org/v3/index.json -k $$NUGET_PUBLISH_KEY; \
	    cd -; \
	  fi; \
	done

.PHONY: install build
