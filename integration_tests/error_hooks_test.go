// Copyright 2026, Pulumi Corporation.
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
	"testing"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	"github.com/stretchr/testify/require"
)

// TestDotnetErrorHooks tests that error hooks (OnError) in .NET work as expected.
// Uses the Go testprovider's FlakyCreate resource (fails first create with ErrorResourceInitFailed,
// succeeds on retry) and an error hook that returns true to retry.
//
// We must use the Go testprovider from the pulumi submodule, not the C# testprovider in
// integration_tests/testprovider. The engine only invokes error hooks for retryable failures,
// and only the Go provider returns the ErrorResourceInitFailed gRPC detail that marks a create
// failure as retryable. The C# testprovider's plain Exception is not treated as retryable.
//
//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotnetErrorHooks(t *testing.T) {
	goTestproviderPath, err := filepath.Abs(filepath.Join("..", "pulumi", "tests", "testprovider"))
	require.NoError(t, err)

	testDotnetProgram(t, &integration.ProgramTestOptions{
		LocalProviders: []integration.LocalDependency{
			{
				Package: "testprovider",
				Path:    goTestproviderPath,
			},
		},
		Quick: true,
		Dir:   filepath.Join("error_hooks", "step1"),
		ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
			requirePrinted(t, stack, "info", "onError was called")
		},
	})
}
