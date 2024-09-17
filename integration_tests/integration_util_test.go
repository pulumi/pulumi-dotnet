// Copyright 2016-2022, Pulumi Corporation.
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

// The linter doesn't see the uses since the consumers are conditionally compiled tests.
//
// nolint:unused,deadcode,varcheck
package integration_tests

import (
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io/fs"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
	"testing"
	"time"

	"github.com/pulumi/pulumi/pkg/v3/engine"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	"github.com/pulumi/pulumi/sdk/v3/go/common/apitype"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/rpcutil"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"
	"github.com/stretchr/testify/assert"
	"google.golang.org/grpc"
)

const WindowsOS = "windows"

// assertPerfBenchmark implements the integration.TestStatsReporter interface, and reports test
// failures when a scenario exceeds the provided threshold.
type assertPerfBenchmark struct {
	T                  *testing.T
	MaxPreviewDuration time.Duration
	MaxUpdateDuration  time.Duration
}

func prepareDotnetProject(projInfo *engine.Projinfo) error {
	cwd, _, err := projInfo.GetPwdMain()
	if err != nil {
		return err
	}
	return prepareDotnetProjectAtCwd(cwd)
}

func prepareDotnetProjectAtCwd(cwd string) error {
	testUtilsPath, err := filepath.Abs("./utils/Pulumi.IntegrationTests.Utils.csproj")
	if err != nil {
		return err
	}

	err = filepath.WalkDir(cwd, func(path string, entry fs.DirEntry, err error) error {
		if err != nil {
			return err
		}

		if entry.IsDir() && (entry.Name() == "bin" || entry.Name() == "obj") {
			err = os.RemoveAll(path)
			if err != nil {
				return err
			}
			return filepath.SkipDir
		}

		if strings.HasSuffix(entry.Name(), "csproj") {
			projectContent, err := os.ReadFile(path)
			if err != nil {
				return err
			}

			// if a package reference already exists
			// then pulumi is a transitive reference
			// no need to add it
			if strings.Contains(string(projectContent), "Include=\"Pulumi") {
				return nil
			}

			packageReference := fmt.Sprintf(`<ProjectReference Include="%s" />`, testUtilsPath)

			// If we're running edit tests we might have already have added the ProjectReference (edit tests
			// rerun prepareProject)
			if strings.Contains(string(projectContent), packageReference) {
				return nil
			}

			modifiedContent := fmt.Sprintf(`
	<ItemGroup>
		%s
	</ItemGroup>
</Project>
`, packageReference)

			modifiedProjectContent := strings.ReplaceAll(string(projectContent), "</Project>", modifiedContent)
			err = os.WriteFile(path, []byte(modifiedProjectContent), 0o644)
			if err != nil {
				return err
			}
		}

		return nil
	})
	return err
}

func getProviderPath(providerDir string) string {
	environ := os.Environ()
	for _, env := range environ {
		split := strings.SplitN(env, "=", 2)
		contract.Assertf(len(split) == 2, "expected split to be of length 2")
		key, value := split[0], split[1]

		// Case-insensitive compare, as Windows will normally be "Path", not "PATH".
		if strings.EqualFold(key, "PATH") {
			// Prepend the provider directory to PATH so any calls to run
			// pulumi-language-dotnet pick up the locally built one.
			path := fmt.Sprintf("%s=%s%s%s", key, providerDir, string(os.PathListSeparator), value)
			return path
		}
	}
	return fmt.Sprintf("PATH=%s", providerDir)
}

func testDotnetProgram(t *testing.T, options *integration.ProgramTestOptions) {
	languagePluginPath, err := filepath.Abs("../pulumi-language-dotnet")
	assert.NoError(t, err)
	options.PrepareProject = prepareDotnetProject
	options.Env = append(options.Env, getProviderPath(languagePluginPath))
	integration.ProgramTest(t, options)
}

func (t assertPerfBenchmark) ReportCommand(stats integration.TestCommandStats) {
	var maxDuration *time.Duration
	if strings.HasPrefix(stats.StepName, "pulumi-preview") {
		maxDuration = &t.MaxPreviewDuration
	}
	if strings.HasPrefix(stats.StepName, "pulumi-update") {
		maxDuration = &t.MaxUpdateDuration
	}

	if maxDuration != nil && *maxDuration != 0 {
		if stats.ElapsedSeconds < maxDuration.Seconds() {
			t.T.Logf(
				"Test step %q was under threshold. %.2fs (max %.2fs)",
				stats.StepName, stats.ElapsedSeconds, maxDuration.Seconds())
		} else {
			t.T.Errorf(
				"Test step %q took longer than expected. %.2fs vs. max %.2fs",
				stats.StepName, stats.ElapsedSeconds, maxDuration.Seconds())
		}
	}
}

func testComponentSlowLocalProvider(t *testing.T) integration.LocalDependency {
	return integration.LocalDependency{
		Package: "testcomponent",
		Path:    filepath.Join("construct_component_slow", "testcomponent"),
	}
}

func testComponentProviderSchema(t *testing.T, path string) {
	t.Parallel()

	tests := []struct {
		name          string
		env           []string
		version       int32
		expected      string
		expectedError string
	}{
		{
			name:     "Default",
			expected: "{}",
		},
		{
			name:     "Schema",
			env:      []string{"INCLUDE_SCHEMA=true"},
			expected: `{"hello": "world"}`,
		},
		{
			name:          "Invalid Version",
			version:       15,
			expectedError: "unsupported schema version 15",
		},
	}
	for _, test := range tests {
		test := test
		t.Run(test.name, func(t *testing.T) {
			t.Parallel()
			// Start the plugin binary.
			cmd := exec.Command(path, "ignored")
			cmd.Env = append(os.Environ(), test.env...)
			stdout, err := cmd.StdoutPipe()
			assert.NoError(t, err)
			err = cmd.Start()
			assert.NoError(t, err)
			defer func() {
				// Ignore the error as it may fail with access denied on Windows.
				cmd.Process.Kill() // nolint: errcheck
			}()

			// Read the port from standard output.
			reader := bufio.NewReader(stdout)
			bytes, err := reader.ReadBytes('\n')
			assert.NoError(t, err)
			port := strings.TrimSpace(string(bytes))

			// Create a connection to the server.
			conn, err := grpc.Dial("127.0.0.1:"+port, grpc.WithInsecure(), rpcutil.GrpcChannelOptions())
			assert.NoError(t, err)
			client := pulumirpc.NewResourceProviderClient(conn)

			// Call GetSchema and verify the results.
			resp, err := client.GetSchema(context.Background(), &pulumirpc.GetSchemaRequest{Version: test.version})
			if test.expectedError != "" {
				assert.Error(t, err)
				assert.Contains(t, err.Error(), test.expectedError)
			} else {
				assert.Equal(t, test.expected, resp.GetSchema())
			}
		})
	}
}

// Test remote component inputs properly handle unknowns.
func testConstructUnknown(t *testing.T, lang string) {
	const testDir = "construct_component_unknown"
	componentDir := "testcomponent-go"

	localProviders := []integration.LocalDependency{
		{Package: "testprovider", Path: "testprovider"},
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                    filepath.Join(testDir, lang),
		LocalProviders:         localProviders,
		SkipRefresh:            true,
		SkipPreview:            false,
		SkipUpdate:             true,
		SkipExportImport:       true,
		SkipEmptyPreviewUpdate: true,
		Quick:                  false,
	})
}

// Test methods properly handle unknowns.
func testConstructMethodsUnknown(t *testing.T, lang string) {
	const testDir = "construct_component_methods_unknown"
	componentDir := "testcomponent-go"

	localProviders := []integration.LocalDependency{
		{Package: "testprovider", Path: "testprovider"},
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                    filepath.Join(testDir, lang),
		LocalProviders:         localProviders,
		SkipRefresh:            true,
		SkipPreview:            false,
		SkipUpdate:             true,
		SkipExportImport:       true,
		SkipEmptyPreviewUpdate: true,
		Quick:                  false,
	})
}

// Test methods that create resources.
func testConstructMethodsResources(t *testing.T, lang string) {
	const testDir = "construct_component_methods_resources"
	componentDir := "testcomponent-go"

	localProviders := []integration.LocalDependency{
		{Package: "testprovider", Path: "testprovider"},
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join(testDir, lang),
		LocalProviders: localProviders,
		Quick:          true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.NotNil(t, stackInfo.Deployment)
			assert.Equal(t, 6, len(stackInfo.Deployment.Resources))
			var hasExpectedResource bool
			var result string
			for _, res := range stackInfo.Deployment.Resources {
				if res.URN.Name() == "myrandom" {
					hasExpectedResource = true
					result = res.Outputs["result"].(string)
					assert.Equal(t, float64(10), res.Inputs["length"])
					assert.Equal(t, 10, len(result))
				}
			}
			assert.True(t, hasExpectedResource)
			assert.Equal(t, result, stackInfo.Outputs["result"])
		},
	})
}

// Test failures returned from methods are observed.
func testConstructMethodsErrors(t *testing.T, lang string) {
	const testDir = "construct_component_methods_errors"
	componentDir := "testcomponent-go"

	stderr := &bytes.Buffer{}
	expectedError := "the failure reason (the failure property)"

	localProvider := integration.LocalDependency{
		Package: "testcomponent", Path: filepath.Join(testDir, componentDir),
	}
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join(testDir, lang),
		LocalProviders: []integration.LocalDependency{localProvider},
		Quick:          true,
		Stderr:         stderr,
		ExpectFailure:  true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			output := stderr.String()
			assert.Contains(t, output, expectedError)
		},
	})
}

func testConstructOutputValues(t *testing.T, lang string, dependencies ...string) {
	t.Parallel()

	const testDir = "construct_component_output_values"
	componentDir := "testcomponent-go"

	localProviders := []integration.LocalDependency{
		{Package: "testprovider", Path: "testprovider"},
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	integration.ProgramTest(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join(testDir, lang),
		Dependencies:   dependencies,
		LocalProviders: localProviders,
		Quick:          true,
	})
}

var previewSummaryRegex = regexp.MustCompile(
	`{\s+"steps": \[[\s\S]+],\s+"duration": \d+,\s+"changeSummary": {[\s\S]+}\s+}`)

func assertOutputContainsEvent(t *testing.T, evt apitype.EngineEvent, output string) {
	evtJSON := bytes.Buffer{}
	encoder := json.NewEncoder(&evtJSON)
	encoder.SetEscapeHTML(false)
	err := encoder.Encode(evt)
	assert.NoError(t, err)
	assert.Contains(t, output, evtJSON.String())
}

// printfTestValidation is used by the TestPrintfXYZ test cases in the language-specific test
// files. It validates that there are a precise count of expected stdout/stderr lines in the test output.
func printfTestValidation(t *testing.T, stack integration.RuntimeValidationStackInfo) {
	var foundStdout int
	var foundStderr int
	for _, ev := range stack.Events {
		if de := ev.DiagnosticEvent; de != nil {
			if strings.HasPrefix(de.Message, fmt.Sprintf("Line %d", foundStdout)) {
				foundStdout++
			} else if strings.HasPrefix(de.Message, fmt.Sprintf("Errln %d", foundStderr+10)) {
				foundStderr++
			}
		}
	}
	assert.Equal(t, 11, foundStdout)
	assert.Equal(t, 11, foundStderr)
}
