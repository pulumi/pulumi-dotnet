// Copyright 2016-2024, Pulumi Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package main

import (
	"bufio"
	"fmt"
	"io"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"testing"

	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/rpcutil"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"
	testingrpc "github.com/pulumi/pulumi/sdk/v3/proto/go/testing"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

func runTestingHost(t *testing.T) (string, testingrpc.LanguageTestClient) {
	// We can't just go run the pulumi-test-language package because of
	// https://github.com/golang/go/issues/39172, so we build it to a temp file then run that.
	binary := t.TempDir() + "/pulumi-test-language"
	cmd := exec.Command("go", "build", "-C", "../pulumi/pkg/testing/pulumi-test-language/", "-o", binary)
	output, err := cmd.CombinedOutput()
	t.Logf("build output: %s", output)
	require.NoError(t, err)

	cmd = exec.Command(binary)
	stdout, err := cmd.StdoutPipe()
	require.NoError(t, err)
	stderr, err := cmd.StderrPipe()
	require.NoError(t, err)
	stderrReader := bufio.NewReader(stderr)

	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		for {
			text, err := stderrReader.ReadString('\n')
			if err != nil {
				wg.Done()
				return
			}
			t.Logf("engine: %s", text)
		}
	}()

	err = cmd.Start()
	require.NoError(t, err)

	stdoutBytes, err := io.ReadAll(stdout)
	require.NoError(t, err)

	address := string(stdoutBytes)

	conn, err := grpc.NewClient(
		address,
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithUnaryInterceptor(rpcutil.OpenTracingClientInterceptor()),
		grpc.WithStreamInterceptor(rpcutil.OpenTracingStreamClientInterceptor()),
		rpcutil.GrpcChannelOptions(),
	)
	require.NoError(t, err)

	client := testingrpc.NewLanguageTestClient(conn)

	t.Cleanup(func() {
		assert.NoError(t, cmd.Process.Kill())
		wg.Wait()
		// We expect this to error because we just killed it.
		contract.IgnoreError(cmd.Wait())
	})

	return address, client
}

// Add test names here that are expected to fail and the reason why they are failing
var expectedFailures = map[string]string{
	"l1-builtin-can":                        "#489 codegen not implemented",
	"l1-builtin-try":                        "#490 codegen not implemented",
	"l1-builtin-stash":                      "testdata not yet generated for .NET",
	"l1-keyword-overlap":                    "#493 update to pulumi 1.50 conformance failure",
	"l1-proxy-index":                        "dotnet build failed",
	"l2-component-call-simple":              "#491 update to pulumi 1.50 conformance failure",
	"l2-resource-option-replace-on-changes": "not yet implemented",
	"l2-resource-asset-archive": "" +
		"The namespace 'Pulumi.AssetArchive' conflicts with the type 'AssetArchive' in 'Pulumi, Version=1.0.0.0",
	"l2-resource-config": "sdk packing for config: build error before pack",
	"l2-resource-alpha": "" +
		"wrong package reference Include=Pulumi.Alpha.3.0 Version=0-alpha.1.internal",
	"l1-output-null":                        "dotnet build failed",
	"l1-output-array":                       "error CS0826: No best type found for implicitly-typed array",
	"l1-output-map":                         "Same error as with arrays about implicitly typed maps",
	"l1-stack-reference":                    "TODO: call getOutput",
	"l2-resource-primitives":                "Cannot implicitly convert type 'int[]' to 'Pulumi.InputList<double>'",
	"l2-failed-create-continue-on-error":    "build error before pack: exit status 1",
	"l2-provider-call":                      "invalid token",
	"l2-provider-call-explicit":             "invalid token",
	"l2-provider-grpc-config":               "dotnet build failed",
	"l2-provider-grpc-config-secret":        "dotnet build failed",
	"l2-provider-grpc-config-schema":        "dotnet build failed",
	"l2-provider-grpc-config-schema-secret": "dotnet build failed",
	"l2-proxy-index":                        "dotnet build failed",
	"l2-invoke-options-depends-on":          "dotnet build failed",
	"l2-invoke-scalar": "" +
		"result contains invalid type Dictionary: only ImmutableArray and ImmutableDictionary allowed",
	"l2-invoke-scalars": "" +
		"result contains invalid type Dictionary: only ImmutableArray and ImmutableDictionary allowed",
	"l2-invoke-secrets": "" +
		"Pulumi.Deployment+InvokeException: 'simple-invoke:index:secretInvoke' failed: value is not a string",
	"l2-map-keys":                       "dotnet build failed",
	"l2-resource-secret":                "test hanging",
	"l1-builtin-project-root":           "#466",
	"l2-rtti":                           "codegen not implemented",
	"l2-namespaced-provider":            "error CS0117: 'ResourceArgs' does not contain a definition for 'ResourceRef'", //nolint:lll
	"l2-union":                          "dotnet build failed",
	"l2-resource-option-alias":          "aliases not recognized: expected 0 create operations but got 3",
	"l2-resource-option-hide-diffs":     "programgen bug: https://github.com/pulumi/pulumi/issues/20665",
	"l2-resource-option-ignore-changes": "property path has @ prefix: expected 'value' but got '@value'",
	"l1-builtin-cwd":                    "testdata not yet generated for .NET",
	"l1-builtin-project-root-main":      "testdata not yet generated for .NET",
	"l2-keywords":                       "testdata not yet generated for .NET",
	"l2-parallel-resources":             "testdata not yet generated for .NET",
	"l2-parameterized-invoke": "dotnet build failed: " +
		"DoHelloWorld does not exist in namespace Pulumi.Subpackage",
	"l2-parameterized-resource-twice":              "testdata not yet generated for .NET",
	"l2-resource-option-replacement-trigger":       "not yet implemented",
	"l2-resource-option-replace-with":              "not yet implemented",
	"l2-resource-option-delete-before-replace":     "https://github.com/pulumi/pulumi-dotnet/issues/813",
	"l2-resource-option-additional-secret-outputs": "https://github.com/pulumi/pulumi-dotnet/issues/814",
	"l2-resource-option-custom-timeouts":           "https://github.com/pulumi/pulumi-dotnet/issues/822",
	"l2-resource-option-version":                   "https://github.com/pulumi/pulumi-dotnet/issues/823",
	"l3-range-resource-output-traversal":           "dotnet build failed: Output<ImmutableArray> missing Select extension method", //nolint:lll
	"l2-resource-option-plugin-download-url":       "https://github.com/pulumi/pulumi-dotnet/issues/824",
	"l1-config-types-object":                       "dotnet build failed: Cannot initialize type 'object' with a collection initializer", //nolint:lll
	"l1-elide-index":                               "https://github.com/pulumi/pulumi-dotnet/issues/865",
	"l2-elide-index":                               "https://github.com/pulumi/pulumi-dotnet/issues/868",
	"l2-discriminated-union":                       "https://github.com/pulumi/pulumi-dotnet/issues/866",
	"l2-module-format":                             "https://github.com/pulumi/pulumi-dotnet/issues/867",
}

// Add program overrides here for programs that can't yet be generated correctly due to programgen bugs.
var programOverrides = map[string]*testingrpc.PrepareLanguageTestsRequest_ProgramOverride{
	// TODO[pulumi/pulumi#18741]: Remove when programgen support for call is implemented.
	"l2-component-property-deps": {
		Paths: []string{
			filepath.Join("testdata", "overrides", "l2-component-property-deps"),
		},
	},
}

func TestLanguage(t *testing.T) {
	t.Parallel()

	engineAddress, engine := runTestingHost(t)

	tests, err := engine.GetLanguageTests(t.Context(), &testingrpc.GetLanguageTestsRequest{})
	require.NoError(t, err)

	cancel := make(chan bool)

	// Run the language plugin
	handle, err := rpcutil.ServeWithOptions(rpcutil.ServeOptions{
		Init: func(srv *grpc.Server) error {
			host := newLanguageHost(engineAddress, "")
			pulumirpc.RegisterLanguageRuntimeServer(srv, host)
			return nil
		},
		Cancel: cancel,
	})
	require.NoError(t, err)

	// Create a temp project dir for the test to run in
	rootDir := t.TempDir()

	snapshotDir := "./testdata/"

	// Prepare to run the tests
	prepare, err := engine.PrepareLanguageTests(t.Context(), &testingrpc.PrepareLanguageTestsRequest{
		LanguagePluginName:   "dotnet",
		LanguagePluginTarget: fmt.Sprintf("127.0.0.1:%d", handle.Port),
		TemporaryDirectory:   rootDir,
		SnapshotDirectory:    snapshotDir,
		CoreSdkDirectory:     "../sdk/Pulumi",
		SnapshotEdits: []*testingrpc.PrepareLanguageTestsRequest_Replacement{
			{
				Pattern:     rootDir + "/artifacts",
				Replacement: "ROOT/artifacts",
			},
		},
		ProgramOverrides: programOverrides,
	})
	require.NoError(t, err)

	for _, tt := range tests.Tests {
		tt := tt
		t.Run(tt, func(t *testing.T) {
			t.Parallel()
			if expected, ok := expectedFailures[tt]; ok {
				t.Skipf("test %s is expected to fail: %s", tt, expected)
			}
			if strings.HasPrefix(tt, "policy-") {
				t.Skipf("dotnet doesn't support policy tests yet: %s", tt)
			}
			if strings.HasPrefix(tt, "provider-") {
				t.Skipf("dotnet doesn't support provider tests yet: %s", tt)
			}

			result, err := engine.RunLanguageTest(t.Context(), &testingrpc.RunLanguageTestRequest{
				Token: prepare.Token,
				Test:  tt,
			})

			require.NoError(t, err)
			for _, msg := range result.Messages {
				t.Log(msg)
			}
			t.Logf("stdout: %s", result.Stdout)
			t.Logf("stderr: %s", result.Stderr)
			assert.True(t, result.Success)
		})
	}

	t.Cleanup(func() {
		close(cancel)
		assert.NoError(t, <-handle.Done)
	})
}
