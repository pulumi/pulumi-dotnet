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

package dotnet

import (
	"path/filepath"
	"testing"

	"github.com/pulumi/pulumi/pkg/v3/codegen/schema"
	"github.com/pulumi/pulumi/pkg/v3/codegen/testing/test"
)

func TestGenerateExtensionParameterizedPackage(t *testing.T) {
	t.Parallel()

	test.TestSDKCodegen(t, &test.SDKCodegenOptions{
		Language: "dotnet",
		GenPackage: func(
			tool string, p *schema.Package, e map[string][]byte, l schema.ReferenceLoader,
		) (map[string][]byte, error) {
			return GeneratePackage(tool, p, e, nil)
		},
		TestCases: []*test.SDKTest{
			{
				Directory:   "extension-parameterized-resource",
				Description: "An extension package layered onto a base provider",
			},
		},
		InputDir:  filepath.Join("testdata", "schemas"),
		ResultDir: "testdata",
	})
}
