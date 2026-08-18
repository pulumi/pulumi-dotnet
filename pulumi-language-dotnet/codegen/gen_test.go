// Copyright 2020-2024, Pulumi Corporation.
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

package dotnet

import (
	"os"
	"path/filepath"
	"regexp"
	"slices"
	"sync"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/pulumi/pulumi/pkg/v3/codegen"
	"github.com/pulumi/pulumi/pkg/v3/codegen/schema"
	"github.com/pulumi/pulumi/pkg/v3/codegen/testing/test"
)

var (
	skip = map[string]string{
		"hyphenated-symbols": "dotnet/any",
	}
	skipCompileCheck = []string{
		"hyphen-url",
	}
)

func filterTests() []*test.SDKTest {
	tests := test.PulumiPulumiSDKTests
	for _, test := range tests {
		if check, ok := skip[test.Directory]; ok {
			test.Skip = codegen.NewStringSet(check)
		}
		if slices.Contains(skipCompileCheck, test.Directory) {
			test.SkipCompileCheck = codegen.NewStringSet("dotnet")
		}
	}
	return tests
}

func TestGeneratePackage(t *testing.T) {
	t.Parallel()

	test.TestSDKCodegen(t, &test.SDKCodegenOptions{
		Language: "dotnet",
		GenPackage: func(
			t string, p *schema.Package, e map[string][]byte, l schema.ReferenceLoader,
		) (map[string][]byte, error) {
			return GeneratePackage(t, p, e, nil)
		},
		Checks: map[string]test.CodegenCheck{
			"dotnet/compile": typeCheckGeneratedPackage,
			"dotnet/test":    testGeneratedPackage,
		},
		TestCases: filterTests(),

		InputDir:  filepath.Join("..", "..", "pulumi", "tests", "testdata", "codegen"),
		ResultDir: "testdata",
	})
}

var buildMutex sync.Mutex

func typeCheckGeneratedPackage(t *testing.T, pwd string) {
	versionPath := filepath.Join(pwd, "version.txt")
	if _, err := os.Stat(versionPath); os.IsNotExist(err) {
		err := os.WriteFile(versionPath, []byte("0.0.0\n"), 0o600)
		require.NoError(t, err)
	} else if err != nil {
		require.NoError(t, err)
	}

	// dotnet build requires exclusive access to shared nuget package
	// See: https://github.com/pulumi/pulumi/issues/18738
	buildMutex.Lock()
	defer buildMutex.Unlock()
	test.RunCommand(t, "dotnet build", pwd, "dotnet", "build")
}

func testGeneratedPackage(t *testing.T, pwd string) {
	// dotnet test requires exclusive access to shared nuget package
	// See: https://github.com/pulumi/pulumi/issues/18738
	buildMutex.Lock()
	defer buildMutex.Unlock()
	test.RunCommand(t, "dotnet test", pwd, "dotnet", "test")
}

func TestGenerateType(t *testing.T) {
	t.Parallel()

	cases := []struct {
		typ      schema.Type
		expected string
	}{
		{
			&schema.InputType{
				ElementType: &schema.ArrayType{
					ElementType: &schema.InputType{
						ElementType: &schema.ArrayType{
							ElementType: &schema.InputType{
								ElementType: schema.NumberType,
							},
						},
					},
				},
			},
			"InputList<ImmutableArray<double>>",
		},
		{
			&schema.InputType{
				ElementType: &schema.MapType{
					ElementType: &schema.InputType{
						ElementType: &schema.ArrayType{
							ElementType: &schema.InputType{
								ElementType: schema.NumberType,
							},
						},
					},
				},
			},
			"InputMap<ImmutableArray<double>>",
		},
	}

	mod := &modContext{mod: "main"}
	//nolint:paralleltest // false positive because range var isn't used directly in t.Run(name) arg
	for _, c := range cases {
		t.Run(c.typ.String(), func(t *testing.T) {
			t.Parallel()

			typeString := mod.typeString(c.typ, "", true, false, false)
			assert.Equal(t, c.expected, typeString)
		})
	}
}

func TestTokenToFunctionNameAvoidsInvokeMemberCollisions(t *testing.T) {
	t.Parallel()

	assert.Equal(t, "InvokeFunction", tokenToFunctionName("primitive:index:invoke"))
	assert.Equal(t, "InvokeAsyncFunction", tokenToFunctionName("primitive:index:invokeAsync"))
	assert.Equal(t, "GetValue", tokenToFunctionName("primitive:index:getValue"))
}

func TestGenerateTypeNames(t *testing.T) {
	t.Parallel()

	test.TestTypeNameCodegen(t, "dotnet", func(pkg *schema.Package) test.TypeNameGeneratorFunc {
		modules, _, err := generateModuleContextMap("test", pkg)
		require.NoError(t, err)

		root, ok := modules[""]
		require.True(t, ok)

		return func(t schema.Type) string {
			return root.typeString(t, "", false, false, false)
		}
	}, filepath.FromSlash("../../pulumi/tests/testdata/codegen"))
}

func TestGenProjectFilePulumiVersion(t *testing.T) {
	t.Parallel()

	pulumiReference := func(pkg *schema.Package) string {
		csproj, err := genProjectFile(pkg, "Pulumi.Test", nil, nil, "0.0.1", nil)
		require.NoError(t, err)
		re := regexp.MustCompile(`<PackageReference Include="Pulumi" Version="([^"]+)" />`)
		match := re.FindStringSubmatch(string(csproj))
		require.Len(t, match, 2, "expected a Pulumi package reference in:\n%s", csproj)
		return match[1]
	}

	assert.Equal(t, "[3.76.1.0,4)", pulumiReference(&schema.Package{Name: "test"}))
	assert.Equal(t, "[3.109.0,4)", pulumiReference(&schema.Package{
		Name: "test",
		ExtensionParameterization: &schema.ExtensionParameterization{
			BaseProvider: schema.BaseProvider{Name: "base"},
			Parameter:    []byte("param"),
		},
	}))
}
