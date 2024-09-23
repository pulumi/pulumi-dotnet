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

package integration_tests

import (
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/pulumi/pulumi/pkg/v3/testing/integration"
	"github.com/pulumi/pulumi/sdk/v3/go/common/apitype"
	"github.com/pulumi/pulumi/sdk/v3/go/common/resource"
	ptesting "github.com/pulumi/pulumi/sdk/v3/go/common/testing"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// TestPrintfDotNet tests that we capture stdout and stderr streams properly, even when the last line lacks an \n.
func TestPrintfDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                    "printf",
		Quick:                  true,
		ExtraRuntimeValidation: printfTestValidation,
	})
}

func TestStackOutputsDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "stack_outputs",
		Quick: true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			// Ensure the checkpoint contains a single resource, the Stack, with two outputs.
			fmt.Printf("Deployment: %v", stackInfo.Deployment)
			assert.NotNil(t, stackInfo.Deployment)
			if assert.Equal(t, 1, len(stackInfo.Deployment.Resources)) {
				stackRes := stackInfo.Deployment.Resources[0]
				assert.NotNil(t, stackRes)
				assert.Equal(t, resource.RootStackType, stackRes.URN.Type())
				assert.Equal(t, 0, len(stackRes.Inputs))
				assert.Equal(t, 2, len(stackRes.Outputs))
				assert.Equal(t, "ABC", stackRes.Outputs["xyz"])
				assert.Equal(t, float64(42), stackRes.Outputs["foo"])
			}
		},
	})
}

// TestStackComponentDotNet tests the programming model of defining a stack as an explicit top-level component.
func TestStackComponentDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "stack_component",
		Quick: true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			// Ensure the checkpoint contains a single resource, the Stack, with two outputs.
			fmt.Printf("Deployment: %v", stackInfo.Deployment)
			assert.NotNil(t, stackInfo.Deployment)
			if assert.Equal(t, 1, len(stackInfo.Deployment.Resources)) {
				stackRes := stackInfo.Deployment.Resources[0]
				assert.NotNil(t, stackRes)
				assert.Equal(t, resource.RootStackType, stackRes.URN.Type())
				assert.Equal(t, 0, len(stackRes.Inputs))
				assert.Equal(t, 2, len(stackRes.Outputs))
				assert.Equal(t, "ABC", stackRes.Outputs["abc"])
				assert.Equal(t, float64(42), stackRes.Outputs["Foo"])
			}
		},
	})
}

// TestStackComponentServiceProviderDotNet tests the creation of the stack using IServiceProvider.
func TestStackComponentServiceProviderDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "dotnet_service_provider",
		Quick: true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			// Ensure the checkpoint contains a single resource, the Stack, with two outputs.
			fmt.Printf("Deployment: %v", stackInfo.Deployment)
			assert.NotNil(t, stackInfo.Deployment)
			if assert.Equal(t, 1, len(stackInfo.Deployment.Resources)) {
				stackRes := stackInfo.Deployment.Resources[0]
				assert.NotNil(t, stackRes)
				assert.Equal(t, resource.RootStackType, stackRes.URN.Type())
				assert.Equal(t, 0, len(stackRes.Inputs))
				assert.Equal(t, 2, len(stackRes.Outputs))
				assert.Equal(t, "ABC", stackRes.Outputs["abc"])
				assert.Equal(t, float64(42), stackRes.Outputs["Foo"])
			}
		},
	})
}

// Tests basic configuration from the perspective of a Pulumi .NET program.
func TestConfigBasicDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "config_basic",
		Quick: true,
		Config: map[string]string{
			"aConfigValue": "this value is a value",
		},
		Secrets: map[string]string{
			"bEncryptedSecret": "this super secret is encrypted",
		},
		OrderedConfig: []integration.ConfigValue{
			{Key: "outer.inner", Value: "value", Path: true},
			{Key: "names[0]", Value: "a", Path: true},
			{Key: "names[1]", Value: "b", Path: true},
			{Key: "names[2]", Value: "c", Path: true},
			{Key: "names[3]", Value: "super secret name", Path: true, Secret: true},
			{Key: "servers[0].port", Value: "80", Path: true},
			{Key: "servers[0].host", Value: "example", Path: true},
			{Key: "a.b[0].c", Value: "true", Path: true},
			{Key: "a.b[1].c", Value: "false", Path: true},
			{Key: "tokens[0]", Value: "shh", Path: true, Secret: true},
			{Key: "foo.bar", Value: "don't tell", Path: true, Secret: true},
		},
	})
}

// Tests that accessing config secrets using non-secret APIs results in warnings being logged.
func TestConfigSecretsWarnDotNet(t *testing.T) {
	// TODO[pulumi/pulumi#7127]: Re-enabled the warning.
	t.Skip("Temporarily skipping test until we've re-enabled the warning - pulumi/pulumi#7127")
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   "config_secrets_warn",
		Quick: true,
		Config: map[string]string{
			"plainstr1":    "1",
			"plainstr2":    "2",
			"plainstr3":    "3",
			"plainstr4":    "4",
			"plainbool1":   "true",
			"plainbool2":   "true",
			"plainbool3":   "true",
			"plainbool4":   "true",
			"plainint1":    "1",
			"plainint2":    "2",
			"plainint3":    "3",
			"plainint4":    "4",
			"plaindouble1": "1.5",
			"plaindouble2": "2.5",
			"plaindouble3": "3.5",
			"plaindouble4": "4.5",
			"plainobj1":    "{}",
			"plainobj2":    "{}",
			"plainobj3":    "{}",
			"plainobj4":    "{}",
		},
		Secrets: map[string]string{
			"str1":    "1",
			"str2":    "2",
			"str3":    "3",
			"str4":    "4",
			"bool1":   "true",
			"bool2":   "true",
			"bool3":   "true",
			"bool4":   "true",
			"int1":    "1",
			"int2":    "2",
			"int3":    "3",
			"int4":    "4",
			"double1": "1.5",
			"double2": "2.5",
			"double3": "3.5",
			"double4": "4.5",
			"obj1":    "{}",
			"obj2":    "{}",
			"obj3":    "{}",
			"obj4":    "{}",
		},
		OrderedConfig: []integration.ConfigValue{
			{Key: "parent1.foo", Value: "plain1", Path: true},
			{Key: "parent1.bar", Value: "secret1", Path: true, Secret: true},
			{Key: "parent2.foo", Value: "plain2", Path: true},
			{Key: "parent2.bar", Value: "secret2", Path: true, Secret: true},
			{Key: "names1[0]", Value: "plain1", Path: true},
			{Key: "names1[1]", Value: "secret1", Path: true, Secret: true},
			{Key: "names2[0]", Value: "plain2", Path: true},
			{Key: "names2[1]", Value: "secret2", Path: true, Secret: true},
		},
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.NotEmpty(t, stackInfo.Events)
			//nolint:lll
			expectedWarnings := []string{
				"Configuration 'config_secrets_dotnet:str1' value is a secret; use `GetSecret` instead of `Get`",
				"Configuration 'config_secrets_dotnet:str2' value is a secret; use `RequireSecret` instead of `Require`",
				"Configuration 'config_secrets_dotnet:bool1' value is a secret; use `GetSecretBoolean` instead of `GetBoolean`",
				"Configuration 'config_secrets_dotnet:bool2' value is a secret; use `RequireSecretBoolean` instead of `RequireBoolean`",
				"Configuration 'config_secrets_dotnet:int1' value is a secret; use `GetSecretInt32` instead of `GetInt32`",
				"Configuration 'config_secrets_dotnet:int2' value is a secret; use `RequireSecretInt32` instead of `RequireInt32`",
				"Configuration 'config_secrets_dotnet:double1' value is a secret; use `GetSecretDouble` instead of `GetDouble`",
				"Configuration 'config_secrets_dotnet:double2' value is a secret; use `RequireSecretDouble` instead of `RequireDouble`",
				"Configuration 'config_secrets_dotnet:obj1' value is a secret; use `GetSecretObject` instead of `GetObject`",
				"Configuration 'config_secrets_dotnet:obj2' value is a secret; use `RequireSecretObject` instead of `RequireObject`",
				"Configuration 'config_secrets_dotnet:parent1' value is a secret; use `GetSecretObject` instead of `GetObject`",
				"Configuration 'config_secrets_dotnet:parent2' value is a secret; use `RequireSecretObject` instead of `RequireObject`",
				"Configuration 'config_secrets_dotnet:names1' value is a secret; use `GetSecretObject` instead of `GetObject`",
				"Configuration 'config_secrets_dotnet:names2' value is a secret; use `RequireSecretObject` instead of `RequireObject`",
			}
			for _, warning := range expectedWarnings {
				var found bool
				for _, event := range stackInfo.Events {
					if event.DiagnosticEvent != nil && event.DiagnosticEvent.Severity == "warning" &&
						strings.Contains(event.DiagnosticEvent.Message, warning) {
						found = true
						break
					}
				}
				assert.True(t, found, "expected warning %q", warning)
			}

			// These keys should not be in any warning messages.
			unexpectedWarnings := []string{
				"plainstr1",
				"plainstr2",
				"plainstr3",
				"plainstr4",
				"plainbool1",
				"plainbool2",
				"plainbool3",
				"plainbool4",
				"plainint1",
				"plainint2",
				"plainint3",
				"plainint4",
				"plaindouble1",
				"plaindouble2",
				"plaindouble3",
				"plaindouble4",
				"plainobj1",
				"plainobj2",
				"plainobj3",
				"plainobj4",
				"str3",
				"str4",
				"bool3",
				"bool4",
				"int3",
				"int4",
				"double3",
				"double4",
				"obj3",
				"obj4",
			}
			for _, warning := range unexpectedWarnings {
				for _, event := range stackInfo.Events {
					if event.DiagnosticEvent != nil {
						assert.NotContains(t, event.DiagnosticEvent.Message, warning)
					}
				}
			}
		},
	})
}

func TestStackReferenceSecretsDotnet(t *testing.T) {
	owner := os.Getenv("PULUMI_TEST_OWNER")
	if owner == "" {
		t.Skipf("Skipping: PULUMI_TEST_OWNER is not set")
	}

	d := "stack_reference_secrets"

	testDotnetProgram(t, &integration.ProgramTestOptions{
		RequireService: true,
		Dir:            filepath.Join(d, "step1"),
		Quick:          true,
		EditDirs: []integration.EditDir{
			{
				Dir:             filepath.Join(d, "step2"),
				Additive:        true,
				ExpectNoChanges: true,
				ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
					_, isString := stackInfo.Outputs["refNormal"].(string)
					assert.Truef(t, isString, "referenced non-secret output was not a string")

					secretPropValue, ok := stackInfo.Outputs["refSecret"].(map[string]interface{})
					assert.Truef(t, ok, "secret output was not serialized as a secret")
					assert.Equal(t, resource.SecretSig, secretPropValue[resource.SigKey].(string))
				},
			},
		},
	})
}

// Tests a resource with a large (>4mb) string prop in .Net
func TestLargeResourceDotNet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir: "large_resource",
	})
}

// tests that when a resource transformation throws an exception, the program exits
// and doesn't hang indefinitely.
func TestFailingTransfomationExitsProgram(t *testing.T) {
	stderr := &strings.Builder{}
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:           "failing_transformation_exits",
		ExpectFailure: true,
		Stderr:        stderr,
	})

	assert.Contains(t, stderr.String(), "Boom!")
}

// Test remote component construction with a child resource that takes a long time to be created, ensuring it's created.
//func TestConstructSlowDotnet(t *testing.T) {
//	localProvider := testComponentSlowLocalProvider(t)
//
//	// TODO[pulumi/pulumi#5455]: Dynamic providers fail to load when used from multi-lang components.
//	// Until we've addressed this, set PULUMI_TEST_YARN_LINK_PULUMI, which tells the integration test
//	// module to run `yarn install && yarn link @pulumi/pulumi` in the .NET program's directory, allowing
//	// the Node.js dynamic provider plugin to load.
//	// When the underlying issue has been fixed, the use of this environment variable inside the integration
//	// test module should be removed.
//	const testYarnLinkPulumiEnv = "PULUMI_TEST_YARN_LINK_PULUMI=true"
//
//	testDir := "construct_component_slow"
//	runComponentSetup(t, testDir)
//
//	opts := &integration.ProgramTestOptions{
//		Env:            []string{testYarnLinkPulumiEnv},
//		Dir:            filepath.Join(testDir, "dotnet"),
//		Dependencies:   []string{"Pulumi"},
//		LocalProviders: []integration.LocalDependency{localProvider},
//		Quick:          true,
//		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
//			assert.NotNil(t, stackInfo.Deployment)
//			if assert.Equal(t, 5, len(stackInfo.Deployment.Resources)) {
//				stackRes := stackInfo.Deployment.Resources[0]
//				assert.NotNil(t, stackRes)
//				assert.Equal(t, resource.RootStackType, stackRes.Type)
//				assert.Equal(t, "", string(stackRes.Parent))
//			}
//		},
//	}
//	integration.ProgramTest(t, opts)
//}

// Test remote component construction with prompt inputs.
func TestConstructPlainDotnet(t *testing.T) {
	testDir := "construct_component_plain"
	componentDir := "testcomponent-go"
	expectedResourceCount := 8

	localProviders := []integration.LocalDependency{
		{Package: "testcomponent", Path: filepath.Join(testDir, componentDir)},
	}

	testDotnetProgram(t, optsForConstructPlainDotnet(t, expectedResourceCount, localProviders))
}

func optsForConstructPlainDotnet(t *testing.T, expectedResourceCount int, localProviders []integration.LocalDependency,
	env ...string,
) *integration.ProgramTestOptions {
	return &integration.ProgramTestOptions{
		Env:            env,
		Dir:            filepath.Join("construct_component_plain", "dotnet"),
		LocalProviders: localProviders,
		Quick:          true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.NotNil(t, stackInfo.Deployment)
			assert.Equal(t, expectedResourceCount, len(stackInfo.Deployment.Resources))
		},
	}
}

// Test remote component inputs properly handle unknowns.
func TestConstructUnknownDotnet(t *testing.T) {
	testConstructUnknown(t, "dotnet")
}

// Test methods on remote components.
func TestConstructMethodsDotnet(t *testing.T) {
	testDir := "construct_component_methods"
	componentDir := "testcomponent-go"

	localProvider := integration.LocalDependency{
		Package: "testcomponent",
		Path:    filepath.Join(testDir, componentDir),
	}

	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join(testDir, "dotnet"),
		LocalProviders: []integration.LocalDependency{localProvider},
		Quick:          true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.Equal(t, "Hello World, Alice!", stackInfo.Outputs["message"])
		},
	})
}

func TestConstructMethodsUnknownDotnet(t *testing.T) {
	testConstructMethodsUnknown(t, "dotnet")
}

func TestConstructMethodsErrorsDotnet(t *testing.T) {
	testConstructMethodsErrors(t, "dotnet")
}

func TestConstructProviderDotnet(t *testing.T) {
	const testDir = "construct_component_provider"
	componentDir := "testcomponent-go"
	localProvider := integration.LocalDependency{
		Package: "testcomponent", Path: filepath.Join(testDir, componentDir),
	}
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join(testDir, "dotnet"),
		LocalProviders: []integration.LocalDependency{localProvider},
		Quick:          true,
		ExtraRuntimeValidation: func(t *testing.T, stackInfo integration.RuntimeValidationStackInfo) {
			assert.Equal(t, "hello world", stackInfo.Outputs["message"])
		},
	})
}

func TestGetResourceDotnet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                      "get_resource",
		AllowEmptyPreviewChanges: true,
		ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
			assert.NotNil(t, stack.Outputs)
			assert.Equal(t, float64(2), stack.Outputs["getPetLength"])

			out, ok := stack.Outputs["secret"].(map[string]interface{})
			assert.True(t, ok)

			_, ok = out["ciphertext"]
			assert.True(t, ok)
		},
	})
}

// Test that the about command works as expected. Because about parses the
// results of each runtime independently, we have an integration test in each
// language.
func TestAboutDotnet(t *testing.T) {
	t.Parallel()

	e := ptesting.NewEnvironment(t)
	defer func() {
		if !t.Failed() {
			e.DeleteEnvironmentFallible()
		}
	}()
	e.ImportDirectory("about")

	e.RunCommand("pulumi", "login", "--cloud-url", e.LocalURL())
	_, stderr := e.RunCommand("pulumi", "about")
	// This one doesn't have a current stack. Assert that we caught it.
	assert.Contains(t, stderr, "No current stack")
}

// TestResourceRefsGetResourceDotnet tests that invoking the built-in 'pulumi:pulumi:getResource' function
// returns resource references for any resource reference in a resource's state.
func TestResourceRefsGetResourceDotnet(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   filepath.Join("resource_refs_get_resource"),
		Quick: true,
	})
}

// TestSln tests that we run a program with a .sln file next to it.
func TestSln(t *testing.T) {
	validation := func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
		var foundStdout int
		for _, ev := range stack.Events {
			if de := ev.DiagnosticEvent; de != nil {
				if strings.HasPrefix(de.Message, "With sln") {
					foundStdout++
				}
			}
		}
		assert.Equal(t, 1, foundStdout)
	}
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                    "sln",
		Quick:                  true,
		ExtraRuntimeValidation: validation,
	})
}

// TestSlnMultiple tests that we run a .sln file with multiple nested projects by setting the "main" option.
func TestSlnMultipleNested(t *testing.T) {
	validation := func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
		var foundStdout int
		for _, ev := range stack.Events {
			if de := ev.DiagnosticEvent; de != nil {
				if strings.HasPrefix(de.Message, "With sln") {
					foundStdout++
				}
			}
		}
		assert.Equal(t, 1, foundStdout)
	}
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:                    "sln_multiple_nested",
		Quick:                  true,
		ExtraRuntimeValidation: validation,
	})
}

func TestProvider(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            filepath.Join("provider"),
		LocalProviders: []integration.LocalDependency{{Package: "testprovider", Path: "testprovider"}},
		ExtraRuntimeValidation: func(t *testing.T, stack integration.RuntimeValidationStackInfo) {
			assert.NotNil(t, stack.Outputs)
			assert.Equal(t, float64(42), stack.Outputs["echoA"])
			assert.Equal(t, "hello", stack.Outputs["echoB"])
			assert.Equal(t, []interface{}{float64(1), "goodbye", true}, stack.Outputs["echoC"])
		},
	})
}

// TestDeletedWith tests the DeletedWith resource option.
func TestDeletedWith(t *testing.T) {
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:            "deleted_with",
		LocalProviders: []integration.LocalDependency{{Package: "testprovider", Path: "testprovider"}},
		Quick:          true,
	})
}

func TestProviderCall(t *testing.T) {
	const testDir = "provider_call"
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   filepath.Join(testDir, "dotnet"),
		Env:   []string{"TEST_VALUE=HelloWorld"},
		Quick: true,
	})
}

func TestProviderCallInvalidArgument(t *testing.T) {
	const testDir = "provider_call"
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:           filepath.Join(testDir, "dotnet"),
		Env:           []string{"TEST_VALUE="},
		ExpectFailure: true,
		Quick:         true,
	})
}

func TestProviderConstruct(t *testing.T) {
	const testDir = "provider_construct"
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   filepath.Join(testDir, "dotnet"),
		Quick: true,
	})
}

func TestProviderConstructDependencies(t *testing.T) {
	const testDir = "provider_construct_dependencies"
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   filepath.Join(testDir, "dotnet"),
		Quick: true,
	})
}

func TestProviderConstructUnknown(t *testing.T) {
	const testDir = "provider_construct_unknown"
	testDotnetProgram(t, &integration.ProgramTestOptions{
		Dir:   filepath.Join(testDir, "dotnet"),
		Quick: true,
	})
}

func readUpdateEventLog(logfile string) ([]apitype.EngineEvent, error) {
	events := make([]apitype.EngineEvent, 0)
	eventsFile, err := os.Open(logfile)
	if err != nil {
		if os.IsNotExist(err) {
			return nil, nil
		}
		return nil, fmt.Errorf("expected to be able to open event log file %s: %w",
			logfile, err)
	}

	defer contract.IgnoreClose(eventsFile)

	decoder := json.NewDecoder(eventsFile)
	for {
		var event apitype.EngineEvent
		if err = decoder.Decode(&event); err != nil {
			if err == io.EOF {
				break
			}
			return nil, fmt.Errorf("failed decoding engine event from log file %s: %w",
				logfile, err)
		}
		events = append(events, event)
	}
	return events, nil
}

func TestDebuggerAttachDotnet(t *testing.T) {
	t.Parallel()

	e := ptesting.NewEnvironment(t)
	defer e.DeleteIfNotFailed()
	e.ImportDirectory("printf")

	prepareDotnetProjectAtCwd(e.RootPath)

	e.RunCommand("pulumi", "login", "--cloud-url", e.LocalURL())

	wg := sync.WaitGroup{}
	wg.Add(1)
	go func() {
		defer wg.Done()
		e.Env = append(e.Env, "PULUMI_DEBUG_COMMANDS=true")
		e.RunCommand("pulumi", "stack", "init", "debugger-test")
		e.RunCommand("pulumi", "stack", "select", "debugger-test")
		e.RunCommand("pulumi", "preview", "--attach-debugger",
			"--event-log", filepath.Join(e.RootPath, "debugger.log"))
	}()

	// Wait for the debugging event
	wait := 20 * time.Millisecond
	var debugEvent *apitype.StartDebuggingEvent
outer:
	for i := 0; i < 50; i++ {
		events, err := readUpdateEventLog(filepath.Join(e.RootPath, "debugger.log"))
		require.NoError(t, err)
		for _, event := range events {
			if event.StartDebuggingEvent != nil {
				debugEvent = event.StartDebuggingEvent
				break outer
			}
		}
		time.Sleep(wait)
		wait *= 2
	}
	require.NotNil(t, debugEvent)

	// We just need to send some command to netcoredbg that will make the program continue.
	// We don't care about the actual command, and the `thread-info` command just works.
	in := strings.NewReader("1-thread-info")

	cmd := exec.Command("netcoredbg", "--interpreter=mi", "--attach", strconv.Itoa(int(debugEvent.Config["processId"].(float64))))
	cmd.Stdin = in
	out, err := cmd.CombinedOutput()
	require.NoError(t, err)
	// Check that we get valid output from netcoredbg, so we know it was actually attached.
	require.Contains(t, string(out), "1^done")

	wg.Wait()
}
