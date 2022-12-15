// Copyright 2016-2020, Pulumi Corporation.  All rights reserved.

package integration_tests

import (
	"path/filepath"
	"testing"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
)

var dirs = []string{
	"rename",
	"adopt_into_component",
	"rename_component_and_child",
	"retype_component",
	"rename_component",
	"retype_parents",
}

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

func TestDotNetAliasesRename(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename"))
}

func TestDotNetAliasesAdoptIntoComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("adopt_into_component"))
}

func TestDotNetAliasesRenameComponentAndChild(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename_component_and_child"))
}

func TestDotNetAliasesRetypeComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("retype_component"))
}

func TestDotNetAliasesRenameComponent(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("rename_component"))
}

func TestDotNetAliasesRetypeParents(t *testing.T) {
	testDotnetProgram(t, aliasesTestOptions("retype_parents"))
}
