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
	"context"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"testing"

	"github.com/pulumi/pulumi/sdk/v3/go/common/diag"
	pbempty "google.golang.org/protobuf/types/known/emptypb"

	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/rpcutil"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"
	testingrpc "github.com/pulumi/pulumi/sdk/v3/proto/go/testing"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type hostEngine struct {
	pulumirpc.UnimplementedEngineServer
	t *testing.T

	logLock         sync.Mutex
	logRepeat       int
	previousMessage string
}

func (e *hostEngine) Log(_ context.Context, req *pulumirpc.LogRequest) (*pbempty.Empty, error) {
	e.logLock.Lock()
	defer e.logLock.Unlock()

	var sev diag.Severity
	switch req.Severity {
	case pulumirpc.LogSeverity_DEBUG:
		sev = diag.Debug
	case pulumirpc.LogSeverity_INFO:
		sev = diag.Info
	case pulumirpc.LogSeverity_WARNING:
		sev = diag.Warning
	case pulumirpc.LogSeverity_ERROR:
		sev = diag.Error
	default:
		return nil, fmt.Errorf("Unrecognized logging severity: %v", req.Severity)
	}

	message := req.Message
	if os.Getenv("PULUMI_LANGUAGE_TEST_SHOW_FULL_OUTPUT") != "true" {
		// Cut down logs so they don't overwhelm the test output
		if len(message) > 1024 {
			message = message[:1024] + "... (truncated, run with PULUMI_LANGUAGE_TEST_SHOW_FULL_OUTPUT=true to see full logs))"
		}
	}

	if e.previousMessage == message {
		e.logRepeat++
		return &pbempty.Empty{}, nil
	}

	if e.logRepeat > 1 {
		e.t.Logf("Last message repeated %d times", e.logRepeat)
	}
	e.logRepeat = 1
	e.previousMessage = message

	if req.StreamId != 0 {
		e.t.Logf("(%d) %s[%s]: %s", req.StreamId, sev, req.Urn, message)
	} else {
		e.t.Logf("%s[%s]: %s", sev, req.Urn, message)
	}
	return &pbempty.Empty{}, nil
}

func runEngine(t *testing.T) string {
	// Run a gRPC server that implements the Pulumi engine RPC interface. But all we do is forward logs on to T.
	engine := &hostEngine{t: t}
	stop := make(chan bool)
	t.Cleanup(func() {
		close(stop)
	})
	handle, err := rpcutil.ServeWithOptions(rpcutil.ServeOptions{
		Cancel: stop,
		Init: func(srv *grpc.Server) error {
			pulumirpc.RegisterEngineServer(srv, engine)
			return nil
		},
		Options: rpcutil.OpenTracingServerInterceptorOptions(nil),
	})
	require.NoError(t, err)
	return fmt.Sprintf("127.0.0.1:%v", handle.Port)
}

func runTestingHost(t *testing.T) (string, testingrpc.LanguageTestClient) {
	// We can't just go run the pulumi-test-language package because of
	// https://github.com/golang/go/issues/39172, so we build it to a temp file then run that.
	binary := t.TempDir() + "/pulumi-test-language"
	cmd := exec.Command("go", "build", "-C", "../pulumi/cmd/pulumi-test-language", "-o", binary)
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

	engineAddress := runEngine(t)
	return engineAddress, client
}

// Add test names here that are expected to fail and the reason why they are failing
var expectedFailures = map[string]string{
	"l1-builtin-can":           "#489 codegen not implemented",
	"l1-builtin-try":           "#490 codegen not implemented",
	"l1-config-types":          "dotnet build failed",
	"l1-keyword-overlap":       "#493 update to pulumi 1.50 conformance failure",
	"l1-proxy-index":           "dotnet build failed",
	"l2-component-call-simple": "#491 update to pulumi 1.50 conformance failure",
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
	"l2-invoke-secrets": "" +
		"Pulumi.Deployment+InvokeException: 'simple-invoke:index:secretInvoke' failed: value is not a string",
	"l2-map-keys":                    "dotnet build failed",
	"l2-resource-secret":             "test hanging",
	"l1-builtin-project-root":        "#466",
	"l2-rtti":                        "codegen not implemented",
	"l2-namespaced-provider":         "error CS0117: 'ResourceArgs' does not contain a definition for 'ResourceRef'",
	"l2-resource-parent-inheritance": "expected child to inherit retain on delete flag",
	"l2-invoke-scalar":               "run bailed",
	"l2-union":                       "dotnet build failed",
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

	tests, err := engine.GetLanguageTests(context.Background(), &testingrpc.GetLanguageTestsRequest{})
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
	prepare, err := engine.PrepareLanguageTests(context.Background(), &testingrpc.PrepareLanguageTestsRequest{
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

			result, err := engine.RunLanguageTest(context.Background(), &testingrpc.RunLanguageTestRequest{
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
