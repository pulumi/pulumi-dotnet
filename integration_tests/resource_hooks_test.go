// Copyright 2025, Pulumi Corporation.
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
	"path/filepath"
	"strings"
	"testing"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	"github.com/stretchr/testify/require"
)

// Tests that custom resource hooks in .NET work as expected.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotnetCustomResourceHooks(t *testing.T) {
	testDir := "custom_resource_hooks"

	testDotnetProgram(t, &integration.ProgramTestOptions{
		LocalProviders: []integration.LocalDependency{
			{
				Package: "testprovider",
				Path:    "testprovider",
			},
		},
		Quick: true,

		// Step 1 -- create a resource in order to test creation hooks.
		Dir: filepath.Join(testDir, "step1-create"),
		ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
			requirePrinted(t, stack, "info", "BeforeCreate: value is step1")
			requirePrinted(t, stack, "info", "AfterCreate: value is step1")
		},
		EditDirs: []integration.EditDir{
			// Step 2 -- update the resource to test update hooks.
			{
				Dir:      filepath.Join(testDir, "step2-update"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "info", "BeforeUpdate: value was step1, is step2")
					requirePrinted(t, stack, "info", "AfterUpdate: value was step1, is step2")
				},
			},

			// Step 3 -- delete the resource to test delete hooks.
			{
				Dir:      filepath.Join(testDir, "step3-delete"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "info", "BeforeDelete: value was step2")
					requirePrinted(t, stack, "info", "AfterDelete: value was step2")
				},
			},

			// Step 4 -- attempt to recreate the resource, this time with a failing before-create hook. We expect the
			// deployment to fail and the resource not to be created.
			{
				Dir:           filepath.Join(testDir, "step4-fail-before"),
				Additive:      true,
				ExpectFailure: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "error", "BeforeCreate hook failed")
					requireNoResourceWithName(t, stack, "updatable")
					requireNotPrinted(t, stack, "AfterCreate: value is step4")
				},
			},

			// Step 5 -- attempt to recreate the resource, this time with a failing after-create hook. We expect the
			// deployment to succeed, but the after-create hook to fail. This should mean the resource is created and the
			// failure message is printed as a warning.
			{
				Dir:      filepath.Join(testDir, "step5-fail-after"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "info", "BeforeCreate: value is step5")
					requireResourceWithName(t, stack, "updatable")
					requirePrinted(t, stack, "warning", "AfterCreate hook failed")
				},
			},

			// Step 6 -- run with an empty program to test that we can remove all hooks and resources.
			{
				Dir:      filepath.Join(testDir, "step6-empty"),
				Additive: true,
			},
		},
	})
}

// Tests that component resource hooks in .NET work as expected.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotnetComponentResourceHooks(t *testing.T) {
	testDir := "component_resource_hooks"

	testDotnetProgram(t, &integration.ProgramTestOptions{
		LocalProviders: []integration.LocalDependency{
			{
				Package: "testcomponent",
				Path:    filepath.Join(testDir, "testcomponent-go"),
			},
		},
		Quick: true,

		// Step 1 -- create a resource in order to test creation hooks.
		Dir: filepath.Join(testDir, "step1-create"),
		ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
			requirePrinted(t, stack, "info", "BeforeCreate was called")
			requirePrinted(t, stack, "info", "AfterCreate was called")
		},
		EditDirs: []integration.EditDir{
			// Step 2 -- update the resource to test update hooks.
			{
				Dir:      filepath.Join(testDir, "step2-update"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					// TODO: Currently, updates never fire for component resources because their inputs are not consistently
					// propagated to the engine by the various language SDKs. Thus we do not exercise update hooks here.
					//
					// requirePrinted(t, stack, "info", "BeforeUpdate was called")
					// requirePrinted(t, stack, "info", "AfterUpdate was called")
				},
			},

			// Step 3 -- delete the resource to test delete hooks.
			{
				Dir:      filepath.Join(testDir, "step3-delete"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "info", "BeforeDelete was called")
					requirePrinted(t, stack, "info", "AfterDelete was called")
				},
			},

			// Step 4 -- attempt to recreate the resource, this time with a failing before-create hook. We expect the
			// deployment to fail and the resource not to be created.
			{
				Dir:           filepath.Join(testDir, "step4-fail-before"),
				Additive:      true,
				ExpectFailure: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "error", "BeforeCreate hook failed")
					requireNoResourceWithName(t, stack, "component")
					requireNotPrinted(t, stack, "AfterCreate was called")
				},
			},

			// Step 5 -- attempt to recreate the resource, this time with a failing after-create hook. We expect the
			// deployment to succeed, but the after-create hook to fail. This should mean the resource is created and the
			// failure message is printed as a warning.
			{
				Dir:      filepath.Join(testDir, "step5-fail-after"),
				Additive: true,
				ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
					requirePrinted(t, stack, "info", "BeforeCreate was called")
					requireResourceWithName(t, stack, "component")
					requirePrinted(t, stack, "warning", "AfterCreate hook failed")
				},
			},

			// Step 6 -- run with an empty program to test that we can remove all hooks and resources.
			{
				Dir:      filepath.Join(testDir, "step6-empty"),
				Additive: true,
			},
		},
	})
}

func requirePrinted(
	t *testing.T,
	stack integration.RuntimeValidationStackInfo,
	severity string,
	text string,
) {
	found := false
	for _, event := range stack.Events {
		if event.DiagnosticEvent != nil &&
			event.DiagnosticEvent.Severity == severity && strings.Contains(event.DiagnosticEvent.Message, text) {
			found = true
			break
		}
	}
	require.True(t, found, "Expected to find printed message: %s", text)
}

func requireNotPrinted(t *testing.T, stack integration.RuntimeValidationStackInfo, text string) {
	found := false
	for _, event := range stack.Events {
		if event.DiagnosticEvent != nil && strings.Contains(event.DiagnosticEvent.Message, text) {
			found = true
			break
		}
	}
	require.False(t, found, "Did not expect to find printed message: %s", text)
}

func requireResourceWithName(t *testing.T, stack integration.RuntimeValidationStackInfo, name string) {
	found := false
	for _, res := range stack.Deployment.Resources {
		if res.URN.Name() == name {
			found = true
			break
		}
	}
	require.True(t, found, "Expected to find resource with name %s", name)
}

func requireNoResourceWithName(t *testing.T, stack integration.RuntimeValidationStackInfo, name string) {
	for _, res := range stack.Deployment.Resources {
		if res.URN.Name() == name {
			t.Errorf("Expected no resource with name %s, but found: %v", name, res)
		}
	}
}
