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
	"context"
	"errors"
	"fmt"
	"maps"
	"os"
	"path/filepath"
	"slices"
	"sync"
	"testing"

	"github.com/blang/semver"
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
		// The generated discriminated union interface carries DiscriminatedUnionDiscriminator and
		// DiscriminatedUnionCase, which this repository adds to the SDK in the same release as this
		// generator. The testdata project references Pulumi from nuget, so the check can only pass
		// once that release is published. Remove this entry then.
		"output-funcs",
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

// noopLoader keeps the union tests below independent of the pulumi submodule and of any package
// that is not defined inline in the test.
type noopLoader struct{}

func (noopLoader) LoadPackage(string, *semver.Version) (*schema.Package, error) {
	return nil, errors.New("external packages are not available in this test")
}

func (noopLoader) LoadPackageV2(context.Context, *schema.PackageDescriptor) (*schema.Package, error) {
	return nil, errors.New("external packages are not available in this test")
}

func (noopLoader) LoadPackageReference(string, *semver.Version) (schema.PackageReference, error) {
	return nil, errors.New("external packages are not available in this test")
}

func (noopLoader) LoadPackageReferenceV2(
	context.Context, *schema.PackageDescriptor,
) (schema.PackageReference, error) {
	return nil, errors.New("external packages are not available in this test")
}

func generateTestPackage(t *testing.T, spec schema.PackageSpec) map[string]string {
	t.Helper()

	pkg, diags, err := schema.BindSpec(spec, noopLoader{}, schema.ValidationOptions{})
	require.NoError(t, err)
	require.False(t, diags.HasErrors(), "%v", diags)

	generated, err := GeneratePackage("test", pkg, nil, nil)
	require.NoError(t, err)

	files := map[string]string{}
	for name, contents := range generated {
		files[name] = string(contents)
	}
	return files
}

// unionTestSpec builds a package with a discriminated union over the first variantCount variants,
// plus, when subsetCount is non-zero, a second resource holding a union over the first subsetCount
// of the same variants.
func unionTestSpec(variantCount, subsetCount int, discriminator string) schema.PackageSpec {
	stringT := schema.TypeSpec{Type: "string"}

	types := map[string]schema.ComplexTypeSpec{}
	oneOf := make([]schema.TypeSpec, 0, variantCount)
	mapping := map[string]string{}
	for i := 1; i <= variantCount; i++ {
		token := fmt.Sprintf("union:index:Variant%d", i)
		tag := fmt.Sprintf("variant%d", i)
		properties := map[string]schema.PropertySpec{
			"payload": {TypeSpec: stringT},
		}
		required := []string{}
		if discriminator != "" {
			properties[discriminator] = schema.PropertySpec{TypeSpec: stringT, Const: tag}
			required = append(required, discriminator)
		}
		types[token] = schema.ComplexTypeSpec{ObjectTypeSpec: schema.ObjectTypeSpec{
			Type:       "object",
			Properties: properties,
			Required:   required,
		}}
		oneOf = append(oneOf, schema.TypeSpec{Ref: "#/types/" + token})
		mapping[tag] = "#/types/" + token
	}

	union := schema.TypeSpec{OneOf: oneOf}
	if discriminator != "" {
		union.Discriminator = &schema.DiscriminatorSpec{PropertyName: discriminator, Mapping: mapping}
	}

	properties := map[string]schema.PropertySpec{
		"unionOf":        {TypeSpec: union},
		"arrayOfUnionOf": {TypeSpec: schema.TypeSpec{Type: "array", Items: &union}},
	}
	resources := map[string]schema.ResourceSpec{
		"union:index:Example": {
			ObjectTypeSpec:  schema.ObjectTypeSpec{Type: "object", Properties: properties},
			InputProperties: properties,
		},
	}

	if subsetCount > 0 {
		subsetMapping := map[string]string{}
		for i := 1; i <= subsetCount; i++ {
			tag := fmt.Sprintf("variant%d", i)
			subsetMapping[tag] = mapping[tag]
		}
		subset := schema.TypeSpec{
			OneOf: oneOf[:subsetCount],
			Discriminator: &schema.DiscriminatorSpec{
				PropertyName: discriminator,
				Mapping:      subsetMapping,
			},
		}
		subsetProperties := map[string]schema.PropertySpec{"unionOf": {TypeSpec: subset}}
		resources["union:index:SubsetExample"] = schema.ResourceSpec{
			ObjectTypeSpec:  schema.ObjectTypeSpec{Type: "object", Properties: subsetProperties},
			InputProperties: subsetProperties,
		}
	}

	return schema.PackageSpec{
		Name:      "union",
		Version:   "1.0.0",
		Types:     types,
		Resources: resources,
	}
}

func TestGenerateDiscriminatedUnionInterface(t *testing.T) {
	t.Parallel()

	files := generateTestPackage(t, unionTestSpec(4, 0, "discriminantKind"))

	outputs, ok := files["Outputs/IExampleUnionOf.cs"]
	require.True(t, ok, "output interface not generated, got %v", slices.Sorted(maps.Keys(files)))
	assert.Contains(t, outputs, `[DiscriminatedUnionDiscriminator("discriminantKind")]`)
	assert.Contains(t, outputs, `[DiscriminatedUnionCase("variant1", typeof(Variant1))]`)
	assert.Contains(t, outputs, `[DiscriminatedUnionCase("variant4", typeof(Variant4))]`)
	assert.Contains(t, outputs, "public interface IExampleUnionOf\n")

	inputs, ok := files["Inputs/IExampleUnionOfArgs.cs"]
	require.True(t, ok, "input interface not generated")
	assert.Contains(t, inputs, `[DiscriminatedUnionDiscriminator("discriminantKind")]`)
	assert.Contains(t, inputs, `[DiscriminatedUnionCase("variant1", typeof(Variant1Args))]`)
	assert.Contains(t, inputs, "public interface IExampleUnionOfArgs\n")

	assert.Contains(t, files["Outputs/Variant1.cs"], "public sealed class Variant1 : IExampleUnionOf")
	assert.Contains(t, files["Inputs/Variant1Args.cs"],
		"public sealed class Variant1Args : global::Pulumi.ResourceArgs, IExampleUnionOfArgs")

	example := files["Example.cs"]
	assert.Contains(t, example, "public Output<Outputs.IExampleUnionOf?> UnionOf")
	assert.Contains(t, example, "public Output<ImmutableArray<Outputs.IExampleUnionOf>> ArrayOfUnionOf")
	assert.Contains(t, example, "public Input<Inputs.IExampleUnionOfArgs>? UnionOf { get; set; }")
	assert.Contains(t, example, "public InputList<Inputs.IExampleUnionOfArgs> ArrayOfUnionOf")
	assert.NotContains(t, example, "object")
}

func TestGenerateConstValuedProperty(t *testing.T) {
	t.Parallel()

	files := generateTestPackage(t, unionTestSpec(4, 0, "discriminantKind"))

	// The discriminator tag is a constant, so the generated args class fills it in and the caller
	// never has to write it by hand. The property stays settable.
	variant := files["Inputs/Variant1Args.cs"]
	assert.Contains(t, variant, "public Input<string> DiscriminantKind { get; set; } = null!;")
	assert.Contains(t, variant, "        public Variant1Args()\n"+
		"        {\n"+
		"            DiscriminantKind = \"variant1\";\n"+
		"        }\n")
	assert.Contains(t, files["Inputs/Variant4Args.cs"], `DiscriminantKind = "variant4";`)
}

func TestGenerateDiscriminatedUnionSubsetInterface(t *testing.T) {
	t.Parallel()

	files := generateTestPackage(t, unionTestSpec(4, 3, "discriminantKind"))

	// A value of the narrower union must assign into a slot typed as the wider one.
	assert.Contains(t, files["Outputs/ISubsetExampleUnionOf.cs"],
		"public interface ISubsetExampleUnionOf : IExampleUnionOf\n")
	assert.Contains(t, files["Inputs/ISubsetExampleUnionOfArgs.cs"],
		"public interface ISubsetExampleUnionOfArgs : IExampleUnionOfArgs\n")

	// Members of both unions name only the narrower interface, which already extends the wider one.
	assert.Contains(t, files["Outputs/Variant1.cs"], "public sealed class Variant1 : ISubsetExampleUnionOf\n")
	assert.Contains(t, files["Outputs/Variant4.cs"], "public sealed class Variant4 : IExampleUnionOf\n")

	assert.Contains(t, files["SubsetExample.cs"], "public Output<Outputs.ISubsetExampleUnionOf?> UnionOf")
	assert.Contains(t, files["SubsetExample.cs"], "public Input<Inputs.ISubsetExampleUnionOfArgs>? UnionOf { get; set; }")
}

func TestGenerateTwoMemberDiscriminatedUnionIsUnchanged(t *testing.T) {
	t.Parallel()

	files := generateTestPackage(t, unionTestSpec(2, 0, "discriminantKind"))

	for name := range files {
		assert.NotContains(t, name, "IExample", "no interface should be generated for a two-member union")
	}

	example := files["Example.cs"]
	assert.Contains(t, example, "public Output<Union<Outputs.Variant1, Outputs.Variant2>?> UnionOf")
	assert.Contains(t, example, "public InputUnion<Inputs.Variant1Args, Inputs.Variant2Args>? UnionOf { get; set; }")
	assert.Contains(t, example, "InputList<Union<Inputs.Variant1Args, Inputs.Variant2Args>>")
	assert.NotContains(t, files["Outputs/Variant1.cs"], "public sealed class Variant1 :")
}

func TestGenerateUndiscriminatedUnionIsUnchanged(t *testing.T) {
	t.Parallel()

	files := generateTestPackage(t, unionTestSpec(4, 0, ""))

	for name := range files {
		assert.NotContains(t, name, "IExample", "no interface should be generated without a discriminator")
	}
	assert.Contains(t, files["Example.cs"], "public Output<object?> UnionOf")
	assert.Contains(t, files["Example.cs"], "public object? UnionOf { get; set; }")
	assert.Contains(t, files["Example.cs"], "public InputList<object> ArrayOfUnionOf")
}
