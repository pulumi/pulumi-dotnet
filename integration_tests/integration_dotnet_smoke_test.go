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
	"os"
	"path/filepath"
	"testing"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	"github.com/pulumi/pulumi/sdk/v3/go/common/resource"
	"github.com/stretchr/testify/assert"
)

// TestEmptyDotNet simply tests that we can run an empty .NET project.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestEmptyDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "empty",
		Quick: true,
	})
}

// Tests that stack references work in .NET.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestStackReferenceDotnet(t *testing.T) {
	if owner := os.Getenv("PULUMI_TEST_OWNER"); owner == "" {
		t.Skipf("Skipping: PULUMI_TEST_OWNER is not set")
	}

	opts := &integration.ProgramTestOptions{
		RequireService: true,
		Dir:            "stack_reference",
		Quick:          true,
		// SkipRefresh is required because the stack reference is for the stack itself so it always changes and triggers
		// the --expect-no-changes error.
		SkipRefresh: true,
		EditDirs: []integration.EditDir{
			{
				Dir:      filepath.Join("stack_reference", "step1"),
				Additive: true,
			},
			{
				Dir:      filepath.Join("stack_reference", "step2"),
				Additive: true,
			},
		},
	}
	testDotnetProgram(t, opts)
}

// Test remote component construction in .NET.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestConstructDotnet(t *testing.T) {
	testDir := "construct_component"
	componentDir := "testcomponent-go"
	expectedResourceCount := 8

	localProviders := []integration.LocalDependency{
		{Package: "testprovider", Path: "testprovider"},
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	testDotnetProgram(t, optsForConstructDotnet(expectedResourceCount, localProviders))
}

func optsForConstructDotnet(
	expectedResourceCount int, localProviders []integration.LocalDependency, env ...string,
) *integration.ProgramTestOptions {
	return &integration.ProgramTestOptions{
		Env:            env,
		Dir:            filepath.Join("construct_component", "dotnet"),
		LocalProviders: localProviders,
		Secrets: map[string]string{
			"secret": "this super secret is encrypted",
		},
		Quick: true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.NotNil(t, stackInfo.Deployment)
			if assert.Equal(t, expectedResourceCount, len(stackInfo.Deployment.Resources)) {
				stackRes := stackInfo.Deployment.Resources[0]
				assert.NotNil(t, stackRes)
				assert.Equal(t, resource.RootStackType, stackRes.Type)
				assert.Equal(t, "", string(stackRes.Parent))

				// Check that dependencies flow correctly between the originating program and the remote component
				// plugin.
				urns := make(map[string]resource.URN)
				for _, res := range stackInfo.Deployment.Resources[1:] {
					assert.NotNil(t, res)

					urns[res.URN.Name()] = res.URN
					switch res.URN.Name() {
					case "child-a":
						for _, deps := range res.PropertyDependencies {
							assert.Empty(t, deps)
						}
					case "child-b":
						expected := []resource.URN{urns["a"]}
						assert.ElementsMatch(t, expected, res.Dependencies)
						assert.ElementsMatch(t, expected, res.PropertyDependencies["echo"])
					case "child-c":
						expected := []resource.URN{urns["a"], urns["child-a"]}
						assert.ElementsMatch(t, expected, res.Dependencies)
						assert.ElementsMatch(t, expected, res.PropertyDependencies["echo"])
					case "a", "b", "c":
						secretPropValue, ok := res.Outputs["secret"].(map[string]interface{})
						assert.Truef(t, ok, "secret output was not serialized as a secret")
						assert.Equal(t, resource.SecretSig, secretPropValue[resource.SigKey].(string))
					}
				}
			}
		},
	}
}
