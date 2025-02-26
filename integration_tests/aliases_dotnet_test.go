// Copyright 2016-2020, Pulumi Corporation.
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
)

func aliasesTestOptions(dir string) *integration.ProgramTestOptions {
	return &integration.ProgramTestOptions{
		DebugLogLevel: 9,
		Dir:           filepath.Join("aliases", dir, "step1"),
		Quick:         true,
		EditDirs: []integration.EditDir{
			{
				Dir:             filepath.Join("aliases", dir, "step2"),
				Additive:        true,
				ExpectNoChanges: true,
			},
		},
	}
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesRename(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename"))
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesAdoptIntoComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("adopt_into_component"))
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesRenameComponentAndChild(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename_component_and_child"))
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesRetypeComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("retype_component"))
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesRenameComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename_component"))
}

//nolint:paralleltest // ProgramTest calls testing.T.Parallel
func TestDotNetAliasesRetypeParents(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("retype_parents"))
}
