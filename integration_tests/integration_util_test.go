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

package integrationtests

import (
	"bytes"
	"fmt"
	"io/fs"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/pulumi/pulumi/pkg/v3/engine"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	ptesting "github.com/pulumi/pulumi/sdk/v3/go/common/testing"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

const WindowsOS = "windows"

func setTimeout(t *testing.T, duration time.Duration) {
	go func() {
		select {
		case <-time.After(duration):
			t.Logf("Timed out after %s", duration)
			t.Fail()
		case <-t.Context().Done():
			return
		}
	}()
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
			err = os.WriteFile(path, []byte(modifiedProjectContent), 0o600)
			if err != nil {
				return err
			}
		}

		return nil
	})
	return err
}

var (
	languagePluginOnce sync.Once
	languagePluginDir  string
	languagePluginErr  error
)

// The path to a built pulumi-language-dotnet.
func languagePluginPath(t *testing.T) string {
	languagePluginOnce.Do(func() {
		dir, err := filepath.Abs("../pulumi-language-dotnet")
		if err != nil {
			languagePluginErr = err
			return
		}
		cmd := exec.Command("go", "build", "-ldflags",
			"-X github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version.Version=3.0.0-dev.0",
			"-o", "pulumi-language-dotnet", ".")
		cmd.Dir = dir
		if out, err := cmd.CombinedOutput(); err != nil {
			languagePluginErr = fmt.Errorf("building pulumi-language-dotnet: %w\n%s", err, out)
			return
		}
		languagePluginDir = dir
	})
	require.NoError(t, languagePluginErr)
	return languagePluginDir
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
	return "PATH=" + providerDir
}

func newEnvironmentDotnet(t *testing.T) *ptesting.Environment {
	e := ptesting.NewEnvironment(t)
	e.Env = append(e.Env, getProviderPath(languagePluginPath(t)))
	return e
}

func testDotnetProgram(t *testing.T, options *integration.ProgramTestOptions) {
	options.PrepareProject = prepareDotnetProject
	options.Env = append(options.Env, getProviderPath(languagePluginPath(t)))
	integration.ProgramTest(t, options)
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

// printfTestValidation is used by the TestPrintfXYZ test cases in the language-specific test
// files. It validates that there are a precise count of expected stdout/stderr lines in the test output.
func printfTestValidation(t *testing.T, stack integration.RuntimeValidationStackInfo) {
	var foundStdout int
	var foundStderr int
	for _, ev := range stack.Events {
		if de := ev.DiagnosticEvent; de != nil {
			if strings.Contains(de.Message, fmt.Sprintf("Line %d", foundStdout)) {
				foundStdout++
			} else if strings.Contains(de.Message, fmt.Sprintf("Errln %d", foundStderr+10)) {
				foundStderr++
			}
		}
	}
	assert.Equal(t, 11, foundStdout)
	assert.Equal(t, 11, foundStderr)
}
