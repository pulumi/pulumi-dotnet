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

package dotnet

import (
	"encoding/json"

	"github.com/pulumi/pulumi/pkg/v3/codegen/schema"
)

// CSharpPropertyInfo represents the C# language-specific info for a property.
type CSharpPropertyInfo struct {
	Name string `json:"name,omitempty"`
}

// CSharpResourceInfo represents the C# language-specific info for a resource.
type CSharpResourceInfo struct {
	Name string `json:"name,omitempty"`
}

// CSharpPackageInfo represents the C# language-specific info for a package.
type CSharpPackageInfo struct {
	PackageReferences      map[string]string `json:"packageReferences,omitempty"`
	Namespaces             map[string]string `json:"namespaces,omitempty"`
	Compatibility          string            `json:"compatibility,omitempty"`
	DictionaryConstructors bool              `json:"dictionaryConstructors,omitempty"`
	ProjectReferences      []string          `json:"projectReferences,omitempty"`
	// Determines whether to make single-return-value methods return an output object or the single value.
	LiftSingleValueMethodReturns bool `json:"liftSingleValueMethodReturns,omitempty"`

	// The root namespace used for the package. This defaults to `Pulumi`.
	RootNamespace string `json:"rootNamespace,omitempty"`

	// Allow the Pkg.Version field to filter down to emitted code.
	RespectSchemaVersion bool `json:"respectSchemaVersion,omitempty"`
}

// Returns the root namespace, or "Pulumi" if not provided.
func (info *CSharpPackageInfo) GetRootNamespace() string {
	if r := info.RootNamespace; r != "" {
		return r
	}
	return "Pulumi"
}

// pulumiRootNamespaceConflicts is the set of normalized (PascalCased) package names
// that would produce a `namespace Pulumi.<Name>` colliding with a type of the same
// name exported from the Pulumi SDK's root `Pulumi` namespace (C# error CS0434).
// When the root namespace is `Pulumi` and a package normalizes to one of these,
// we append a trailing underscore to the module namespace so the generated
// `namespace Pulumi.<Name>_` doesn't clash.
var pulumiRootNamespaceConflicts = map[string]bool{
	"Config":       true,
	"AssetArchive": true,
	"Output":       true,
}

func normalizePackageInfo(info *CSharpPackageInfo, pkgName, pkgNamespace string) {
	if info.RootNamespace == "" && pkgNamespace != "" {
		info.RootNamespace = namespaceName(nil, pkgNamespace)
	}

	if info.GetRootNamespace() != "Pulumi" {
		return
	}

	// Normalize the package name the same way namespaceName would, then compare
	// against the set of SDK-root types. This catches variants like "asset-archive",
	// "asset_archive", or "AssetArchive" that all normalize to the same identifier.
	normalized := namespaceName(nil, pkgName)
	if !pulumiRootNamespaceConflicts[normalized] {
		return
	}

	if info.Namespaces == nil {
		info.Namespaces = map[string]string{}
	}
	if _, ok := info.Namespaces[pkgName]; !ok {
		info.Namespaces[pkgName] = normalized + "_"
	}
}

// Importer implements schema.Language for .NET.
var Importer schema.Language = importer(0)

type importer int

// ImportDefaultSpec decodes language-specific metadata associated with a DefaultValue.
func (importer) ImportDefaultSpec(raw json.RawMessage) (any, error) {
	return raw, nil
}

// ImportPropertySpec decodes language-specific metadata associated with a Property.
func (importer) ImportPropertySpec(raw json.RawMessage) (any, error) {
	var info CSharpPropertyInfo
	if err := json.Unmarshal([]byte(raw), &info); err != nil {
		return nil, err
	}
	return info, nil
}

// ImportObjectTypeSpec decodes language-specific metadata associated with a ObjectType.
func (importer) ImportObjectTypeSpec(raw json.RawMessage) (any, error) {
	return raw, nil
}

// ImportResourceSpec decodes language-specific metadata associated with a Resource.
func (importer) ImportResourceSpec(raw json.RawMessage) (any, error) {
	var info CSharpResourceInfo
	if err := json.Unmarshal([]byte(raw), &info); err != nil {
		return nil, err
	}
	return info, nil
}

// ImportFunctionSpec decodes language-specific metadata associated with a Function.
func (importer) ImportFunctionSpec(raw json.RawMessage) (any, error) {
	return raw, nil
}

// ImportPackageSpec decodes language-specific metadata associated with a Package.
func (importer) ImportPackageSpec(raw json.RawMessage) (any, error) {
	var info CSharpPackageInfo
	if err := json.Unmarshal([]byte(raw), &info); err != nil {
		return nil, err
	}
	return info, nil
}
