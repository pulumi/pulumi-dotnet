// Copyright 2016-2021, Pulumi Corporation.
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

package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/pulumi/pulumi/sdk/v3/go/common/resource/plugin"
	ptesting "github.com/pulumi/pulumi/sdk/v3/go/common/testing"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestDeterminePluginDependency(t *testing.T) {
	t.Parallel()

	cases := []struct {
		// Test name
		Name string

		// Inputs
		PackageName    string
		PackageVersion string
		VersionTxt     string
		PulumiPlugin   *plugin.PulumiPluginJSON

		// Expected outputs
		ExpectError bool
		Expected    *pulumirpc.PluginDependency
	}{
		{
			Name:           "non-package",
			PackageName:    "Pulumi.Foo",
			PackageVersion: "v1.2.3",
			Expected:       nil,
		},
		{
			Name:           "default-name-non-pulumi",
			PackageName:    "HelloWorld",
			PackageVersion: "v1.2.3",
			PulumiPlugin: &plugin.PulumiPluginJSON{
				Resource: true,
			},
			Expected: &pulumirpc.PluginDependency{
				Name:    "helloworld",
				Version: "v1.2.3",
			},
		},
		{
			Name:           "version-txt",
			PackageName:    "Pulumi.AzureNative",
			PackageVersion: "v1.2.3",
			VersionTxt:     "v2.3.4",
			Expected: &pulumirpc.PluginDependency{
				Name:    "azurenative",
				Version: "v2.3.4",
			},
		},
		{
			Name:           "version-txt-with-name",
			PackageName:    "NotImportant",
			PackageVersion: "0.0.0",
			VersionTxt:     "AzureNative\nv2.3.4\n",
			Expected: &pulumirpc.PluginDependency{
				Name:    "AzureNative",
				Version: "v2.3.4",
			},
		},
		{
			Name:           "version-txt-invalid-version",
			PackageName:    "Pulumi.AzureNative",
			PackageVersion: "v1.2.3",
			VersionTxt:     "abcdefg",
			ExpectError:    true,
		},
		{
			Name:           "pulumiplugin",
			PackageName:    "Pulumi.AzureNative",
			PackageVersion: "v1.2.3",
			PulumiPlugin: &plugin.PulumiPluginJSON{
				Resource: true,
				Name:     "corporate-native",
				Version:  "v3.2.1",
				Server:   "website.com/page",
			},
			Expected: &pulumirpc.PluginDependency{
				Name:    "corporate-native",
				Version: "v3.2.1",
				Server:  "website.com/page",
			},
		},
		{
			Name:           "pulumiplugin-invalid-version",
			PackageName:    "Pulumi.AzureNative",
			PackageVersion: "v1.2.3",
			PulumiPlugin: &plugin.PulumiPluginJSON{
				Name:     "hello",
				Version:  "v one point two point three",
				Resource: true,
			},
			ExpectError: true,
		},
		{
			Name:           "pulumiplugin-and-version-txt",
			PackageName:    "A.Package",
			PackageVersion: "v0.0.0",
			VersionTxt:     "name1\nv1.1.1",
			PulumiPlugin: &plugin.PulumiPluginJSON{
				Name:     "name2",
				Version:  "v2.2.2",
				Server:   "a.org/server",
				Resource: true,
			},
			Expected: &pulumirpc.PluginDependency{
				Name:    "name2",
				Version: "v2.2.2",
				Server:  "a.org/server",
			},
		},
	}

	for _, c := range cases {
		c := c
		t.Run(c.Name, func(t *testing.T) {
			t.Parallel()

			cwd := t.TempDir()
			artifactPath := filepath.Join(cwd, strings.ToLower(c.PackageName), c.PackageVersion, "content")
			err := os.MkdirAll(artifactPath, 0o700)
			assert.NoError(t, err)

			// Setup testing environment
			if c.VersionTxt != "" {
				path := filepath.Join(artifactPath, "version.txt")
				err := os.WriteFile(path, []byte(c.VersionTxt), 0o600)
				assert.NoError(t, err)
				t.Logf("Wrote version.txt file to %q", path)
			}
			if c.PulumiPlugin != nil {
				path := filepath.Join(artifactPath, "pulumi-plugin.json")
				bytes, err := c.PulumiPlugin.JSON()
				assert.NoError(t, err)
				err = os.WriteFile(path, bytes, 0o600)
				assert.NoError(t, err)
				t.Logf("Wrote pulumi-plugin.json file to %q", path)
			}

			// Update expected for the common case.
			if c.Expected != nil && c.Expected.Kind == "" {
				c.Expected.Kind = "resource"
			}

			actual, err := DeterminePluginDependency(cwd, c.PackageName, c.PackageVersion)

			if c.ExpectError {
				t.Logf("Error expected")
				assert.Errorf(t, err, "actual = %v", actual)
			} else {
				t.Logf("No error expected")
				assert.NoError(t, err)
				assert.Equal(t, c.Expected, actual)
			}
		})
	}
}

func TestBuildDll(t *testing.T) {
	t.Parallel()

	cases := []struct {
		Name       string
		EntryPoint string
		ExtraSetup func(t *testing.T, e *ptesting.Environment)

		ExpectedErrorContains string
		ExpectedBinaryPath    string
	}{
		{
			Name:               "regular case works",
			EntryPoint:         "",
			ExpectedBinaryPath: filepath.Join("bin", "pulumi-debugging", "Empty.dll"),
		},
		{
			Name:               "entrypoint specified",
			EntryPoint:         "Empty.csproj",
			ExpectedBinaryPath: filepath.Join("bin", "pulumi-debugging", "Empty.dll"),
		},
		{
			Name:                  "entrypoint not found",
			EntryPoint:            "Wrong.csproj",
			ExpectedErrorContains: "Project file does not exist",
		},
		{
			Name:       "fsproj works",
			EntryPoint: "",
			ExtraSetup: func(t *testing.T, e *ptesting.Environment) {
				os.Rename(filepath.Join(e.RootPath, "Empty.csproj"), filepath.Join(e.RootPath, "Empty.fsproj"))
			},
			ExpectedBinaryPath: filepath.Join("bin", "pulumi-debugging", "Empty.dll"),
		},
		{
			Name:       "vbproj works",
			EntryPoint: "",
			ExtraSetup: func(t *testing.T, e *ptesting.Environment) {
				os.Rename(filepath.Join(e.RootPath, "Empty.csproj"), filepath.Join(e.RootPath, "Empty.vbproj"))
			},
			ExpectedErrorContains: "'Sub Main' was not found in 'Empty'.",
		},
		{
			Name:       "multiple projects with entrypoint",
			EntryPoint: "Empty.csproj",
			ExtraSetup: func(t *testing.T, e *ptesting.Environment) {
				data, err := os.ReadFile("Empty.csproj")
				assert.NoError(t, err)
				err = os.WriteFile("Another.fsproj", data, 0o644)
				assert.NoError(t, err)
			},
			ExpectedBinaryPath: filepath.Join("bin", "pulumi-debugging", "Empty.dll"),
		},
		{
			Name:                  "incorrect entry point name",
			EntryPoint:            "Another",
			ExpectedErrorContains: "Project file does not exist",
		},
	}

	for _, c := range cases {
		c := c
		t.Run(c.Name, func(t *testing.T) {
			e := ptesting.NewEnvironment(t)
			e.ImportDirectory("testdata/build-dll")

			pwd, err := os.Getwd()
			require.NoError(t, err)
			os.Chdir(e.RootPath)
			defer os.Chdir(pwd)

			if c.ExtraSetup != nil {
				c.ExtraSetup(t, e)
			}

			host := &dotnetLanguageHost{
				exec: "dotnet",
			}

			binaryPath, err := host.buildDebuggingDLL(c.EntryPoint)

			if c.ExpectedErrorContains != "" {
				assert.ErrorContains(t, err, c.ExpectedErrorContains)
			} else {
				require.NoError(t, err)
				assert.Equal(t, c.ExpectedBinaryPath, binaryPath)
			}
		})
	}
}
