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
	"bytes"
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"math/rand"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"strconv"
	"strings"
	"syscall"
	"time"

	"github.com/blang/semver"
	"github.com/pkg/errors"
	"github.com/pulumi/pulumi-dotnet/pulumi-language-dotnet/v3/version"
	dotnetcodegen "github.com/pulumi/pulumi/pkg/v3/codegen/dotnet"
	hclsyntax "github.com/pulumi/pulumi/pkg/v3/codegen/hcl2/syntax"
	"github.com/pulumi/pulumi/pkg/v3/codegen/pcl"
	"github.com/pulumi/pulumi/pkg/v3/codegen/schema"
	"github.com/pulumi/pulumi/sdk/v3/go/common/resource/plugin"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/cmdutil"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/errutil"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/executable"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/logging"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/rpcutil"
	"github.com/pulumi/pulumi/sdk/v3/go/common/workspace"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/emptypb"
	"google.golang.org/protobuf/types/known/structpb"
)

// A exit-code we recognize when the nodejs process exits.  If we see this error, there's no
// need for us to print any additional error messages since the user already got a a good
// one they can handle.
var dotnetProcessExitedAfterShowingUserActionableMessage = 32

// Launches the language host RPC endpoint, which in turn fires up an RPC server implementing the
// LanguageRuntimeServer RPC endpoint.
func main() {
	var tracing string
	var binary string
	var root string
	flag.StringVar(&tracing, "tracing", "", "Emit tracing to a Zipkin-compatible tracing endpoint")
	flag.StringVar(&binary, "binary", "",
		"[obsolete] A relative or an absolute path to a precompiled .NET assembly to execute")
	flag.StringVar(&root, "root", "", "[obsolete] Project root path to use")

	// You can use the below flag to request that the language host load a specific executor instead of probing the
	// PATH.  This can be used during testing to override the default location.
	var givenExecutor string
	flag.StringVar(&givenExecutor, "use-executor", "",
		"[obsolete] Use the given program as the executor instead of looking for one on PATH")

	flag.Parse()
	args := flag.Args()
	logging.InitLogging(false, 0, false)
	cmdutil.InitTracing("pulumi-language-dotnet", "pulumi-language-dotnet", tracing)

	// Optionally pluck out the engine so we can do logging, etc.
	var engineAddress string
	if len(args) > 0 {
		engineAddress = args[0]
	}

	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt)
	// Map the context Done channel to the rpcutil boolean cancel channel.
	// The context will close on SIGINT or Healthcheck failure.
	cancelChannel := make(chan bool)
	go func() {
		<-ctx.Done()
		cancel() // remove the interrupt handler
		close(cancelChannel)
	}()
	err := rpcutil.Healthcheck(ctx, engineAddress, 5*time.Minute, cancel)
	if err != nil {
		cmdutil.Exit(errors.Wrapf(err, "could not start health check host RPC server"))
	}

	// Fire up a gRPC server, letting the kernel choose a free port.
	handle, err := rpcutil.ServeWithOptions(rpcutil.ServeOptions{
		Cancel: cancelChannel,
		Init: func(srv *grpc.Server) error {
			host := newLanguageHost(engineAddress, tracing)
			pulumirpc.RegisterLanguageRuntimeServer(srv, host)
			return nil
		},
		Options: rpcutil.OpenTracingServerInterceptorOptions(nil),
	})
	if err != nil {
		cmdutil.Exit(errors.Wrapf(err, "could not start language host RPC server"))
	}

	// Otherwise, print out the port so that the spawner knows how to reach us.
	fmt.Printf("%d\n", handle.Port)

	// And finally wait for the server to stop serving.
	if err := <-handle.Done; err != nil {
		cmdutil.Exit(errors.Wrapf(err, "language host RPC stopped serving"))
	}
}

// dotnetLanguageHost implements the LanguageRuntimeServer interface
// for use as an API endpoint.
type dotnetLanguageHost struct {
	pulumirpc.UnimplementedLanguageRuntimeServer

	engineAddress        string
	tracing              string
	dotnetBuildSucceeded bool
}

type dotnetOptions struct {
	// Look on path for a binary executable with this name.
	binary string
	// Use this executable as the dotnet executor.
	dotnetExec string
}

func parseOptions(root string, options map[string]interface{}) (dotnetOptions, error) {
	var dotnetOptions dotnetOptions
	if binary, ok := options["binary"]; ok {
		if binary, ok := binary.(string); ok {
			dotnetOptions.binary = binary
		} else {
			return dotnetOptions, errors.New("binary option must be a string")
		}
	}

	if givenExecutor, ok := options["use-executor"]; ok {
		if givenExecutor, ok := givenExecutor.(string); ok {
			dotnetOptions.dotnetExec = givenExecutor
		} else {
			return dotnetOptions, errors.New("use-executor option must be a string")
		}
	}

	switch {
	case dotnetOptions.dotnetExec != "":
		logging.V(3).Infof("language host asked to use specific executor: `%s`", dotnetOptions.dotnetExec)
	case dotnetOptions.binary != "" && !strings.HasSuffix(dotnetOptions.binary, ".dll"):
		logging.V(3).Info("language host requires no .NET SDK for a self-contained binary")
	default:
		pathExec, err := exec.LookPath("dotnet")
		if err != nil {
			err = errors.Wrap(err, "could not find `dotnet` on the $PATH")
			return dotnetOptions, err
		}

		logging.V(3).Infof("language host identified executor from path: `%s`", pathExec)
		dotnetOptions.dotnetExec = pathExec
	}

	return dotnetOptions, nil
}

func newLanguageHost(engineAddress, tracing string) pulumirpc.LanguageRuntimeServer {
	return &dotnetLanguageHost{
		engineAddress: engineAddress,
		tracing:       tracing,
	}
}

func (host *dotnetLanguageHost) connectToEngine() (pulumirpc.EngineClient, io.Closer, error) {
	// Make a connection to the real engine that we will log messages to.
	conn, err := grpc.NewClient(
		host.engineAddress,
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		rpcutil.GrpcChannelOptions(),
	)
	if err != nil {
		return nil, nil, fmt.Errorf("language host could not make connection to engine: %w", err)
	}

	// Make a client around that connection.
	engineClient := pulumirpc.NewEngineClient(conn)
	return engineClient, conn, nil
}

// GetRequiredPlugins computes the complete set of anticipated plugins required by a program.
func (host *dotnetLanguageHost) GetRequiredPlugins(
	ctx context.Context,
	req *pulumirpc.GetRequiredPluginsRequest,
) (*pulumirpc.GetRequiredPluginsResponse, error) {
	return nil, status.Errorf(codes.Unimplemented, "method GetRequiredPlugins not implemented")
}

func (host *dotnetLanguageHost) GetRequiredPackages(ctx context.Context,
	req *pulumirpc.GetRequiredPackagesRequest,
) (*pulumirpc.GetRequiredPackagesResponse, error) {
	logging.V(5).Infof("GetRequiredPackages: %v", req.Info.GetProgramDirectory())

	opts, err := parseOptions(req.Info.RootDirectory, req.Info.Options.AsMap())
	if err != nil {
		return nil, err
	}
	if opts.binary != "" {
		logging.V(5).Infof("GetRequiredPackages: no packages can be listed when a binary is specified")
		return &pulumirpc.GetRequiredPackagesResponse{}, nil
	}

	engineClient, closer, err := host.connectToEngine()
	if err != nil {
		return nil, err
	}
	defer contract.IgnoreClose(closer)

	// First do a `dotnet build`.  This will ensure that all the nuget dependencies of the project
	// are restored and locally available for us.
	if err := host.DotnetBuild(ctx, opts.dotnetExec, req, engineClient); err != nil {
		return nil, err
	}

	// now, introspect the user project to see which pulumi resource packages it references.
	possiblePulumiPackages, err := host.DeterminePossiblePulumiPackages(
		ctx, opts.dotnetExec, engineClient, req.Info.ProgramDirectory)
	if err != nil {
		return nil, err
	}

	// Ensure we know where the local nuget package cache directory is.  User can specify where that
	// is located, so this makes sure we respect any custom location they may have.
	packageDir, err := host.DetermineDotnetPackageDirectory(ctx, opts.dotnetExec, engineClient, req.Info.ProgramDirectory)
	if err != nil {
		return nil, err
	}

	// Now that we know the set of pulumi packages referenced and we know where packages have been restored to,
	// we can examine each package to determine the corresponding resource-plugin for it.

	packages := []*pulumirpc.PackageDependency{}
	packageToVersion := make(map[string]string)
	for _, parts := range possiblePulumiPackages {
		packageName := parts[0]
		packageVersion := parts[1]

		if existingVersion := packageToVersion[packageName]; existingVersion == packageVersion {
			// only include distinct dependencies.
			continue
		}

		packageToVersion[packageName] = packageVersion

		plugin, err := DeterminePackageDependency(packageDir, packageName, packageVersion)
		if err != nil {
			return nil, err
		}

		if plugin != nil {
			packages = append(packages, plugin)
		}
	}

	return &pulumirpc.GetRequiredPackagesResponse{Packages: packages}, nil
}

func (host *dotnetLanguageHost) DeterminePossiblePulumiPackages(
	ctx context.Context,
	dotnetExec string,
	engineClient pulumirpc.EngineClient,
	programDirectory string,
) ([][]string, error) {
	logging.V(5).Infof("GetRequiredPlugins: Determining pulumi packages")

	// Run the `dotnet list package --include-transitive` command.  Importantly, do not clutter the
	// stream with the extra steps we're performing. This is just so we can determine the required
	// plugins.  And, after the first time we do this, subsequent runs will see that the plugin is
	// installed locally and not need to do anything.
	args := []string{"list", "package", "--include-transitive"}
	commandStr := strings.Join(args, " ")
	commandOutput, err := RunDotnetCommand(ctx, dotnetExec, engineClient, args, false /*logToUser*/, programDirectory)
	if err != nil {
		return nil, err
	}

	// expected output should be like so:
	//
	//    Project 'Aliases' has the following package references
	//    [netcoreapp3.1]:
	//    Top-level Package      Requested                        Resolved
	//    > Pulumi               1.5.0-preview-alpha.1572911568   1.5.0-preview-alpha.1572911568
	//
	//    Transitive Package                                       Resolved
	//    > Google.Protobuf                                        3.10.0
	//    > Grpc                                                   2.24.0
	outputLines := strings.Split(strings.ReplaceAll(commandOutput, "\r\n", "\n"), "\n")

	sawPulumi := false
	packages := [][]string{}
	for _, line := range outputLines {
		fields := strings.Fields(line)
		if len(fields) < 3 {
			continue
		}

		// Has to start with `>` and have at least 3 chunks:
		//
		//    > name requested_ver? resolved_ver
		if fields[0] != ">" {
			continue
		}

		// We only care about `Pulumi.` packages
		packageName := fields[1]
		if packageName == "Pulumi" {
			sawPulumi = true
			continue
		}

		version := fields[len(fields)-1]
		packages = append(packages, []string{packageName, version})
	}

	if !sawPulumi && len(packages) == 0 {
		return nil, errors.Errorf(
			"unexpected output from 'dotnet %v'. Program does not appear to reference any 'Pulumi.*' packages",
			commandStr)
	}

	logging.V(5).Infof("GetRequiredPlugins: Pulumi packages: %#v", packages)

	return packages, nil
}

func (host *dotnetLanguageHost) DetermineDotnetPackageDirectory(
	ctx context.Context,
	dotnetExec string,
	engineClient pulumirpc.EngineClient,
	programDirectory string,
) (string, error) {
	logging.V(5).Infof("GetRequiredPackages: Determining package directory")

	// Run the `dotnet nuget locals global-packages --list` command.  Importantly, do not clutter
	// the stream with the extra steps we're performing. This is just so we can determine the
	// required plugins.  And, after the first time we do this, subsequent runs will see that the
	// plugin is installed locally and not need to do anything.
	args := []string{"nuget", "locals", "global-packages", "--list"}
	commandStr := strings.Join(args, " ")
	commandOutput, err := RunDotnetCommand(ctx, dotnetExec, engineClient, args, false /*logToUser*/, programDirectory)
	if err != nil {
		return "", err
	}

	// expected output should be like so: "info : global-packages: /home/cyrusn/.nuget/packages/"
	// so grab the portion after "global-packages:"
	index := strings.Index(commandOutput, "global-packages:")
	if index < 0 {
		return "", errors.Errorf("Unexpected output from 'dotnet %v': %v", commandStr, commandOutput)
	}

	dir := strings.TrimSpace(commandOutput[index+len("global-packages:"):])
	logging.V(5).Infof("GetRequiredPlugins: Package directory: %v", dir)

	return dir, nil
}

type versionFile struct {
	name    string
	version string
}

func newVersionFile(b []byte, packageName string) *versionFile {
	var name string
	version := strings.TrimSpace(string(b))
	parts := strings.SplitN(version, "\n", 2)
	if len(parts) == 2 {
		// version.txt may contain two lines, in which case it's "plugin name\nversion"
		name = strings.TrimSpace(parts[0])
		version = strings.TrimSpace(parts[1])
	}

	if !strings.HasPrefix(version, "v") {
		// Version file has stripped off the "v" that we need. So add it back here.
		version = fmt.Sprintf("v%v", version)
	}

	return &versionFile{
		name:    name,
		version: version,
	}
}

func DeterminePackageDependency(packageDir, packageName, packageVersion string) (*pulumirpc.PackageDependency, error) {
	logging.V(5).Infof("GetRequiredPlugins: Determining plugin dependency: %v, %v, %v",
		packageDir, packageName, packageVersion)

	// Check for a `~/.nuget/packages/package_name/package_version/content/{pulumi-plugin.json,version.txt}` file.

	artifactPath := filepath.Join(packageDir, strings.ToLower(packageName), packageVersion, "content")
	pulumiPluginFilePath := filepath.Join(artifactPath, "pulumi-plugin.json")
	versionFilePath := filepath.Join(artifactPath, "version.txt")
	logging.V(5).Infof("GetRequiredPlugins: plugin file path: %v", versionFilePath)
	logging.V(5).Infof("GetRequiredPlugins: version file path: %v", versionFilePath)

	pulumiPlugin, err := plugin.LoadPulumiPluginJSON(pulumiPluginFilePath)
	if err != nil && !os.IsNotExist(err) {
		return nil, err
	}
	// Explicitly not a resource
	if pulumiPlugin != nil && !pulumiPlugin.Resource {
		return nil, nil
	}

	var vf *versionFile
	b, err := os.ReadFile(versionFilePath)

	switch {
	case err == nil:
		vf = newVersionFile(b, packageName)
	case os.IsNotExist(err):
		break
	default:
		return nil, fmt.Errorf("failed to read version file: %w", err)
	}

	defaultName := strings.ToLower(strings.TrimPrefix(packageName, "Pulumi."))

	// No pulumi-plugin.json or version.txt
	// That means this is not a resource.
	if pulumiPlugin == nil && vf == nil {
		return nil, nil
	}
	// Create stubs to avoid dereferencing a null
	if pulumiPlugin == nil {
		pulumiPlugin = &plugin.PulumiPluginJSON{}
	} else if vf == nil {
		vf = &versionFile{}
	}

	or := func(o ...string) string {
		for _, s := range o {
			if s != "" {
				return s
			}
		}
		return ""
	}

	name := or(pulumiPlugin.Name, vf.name, defaultName)
	version := or(pulumiPlugin.Version, vf.version, packageVersion)
	_, err = semver.ParseTolerant(version)
	if err != nil {
		return nil, fmt.Errorf("invalid package version: %w", err)
	}

	result := &pulumirpc.PackageDependency{
		Name:    name,
		Version: version,
		Server:  pulumiPlugin.Server,
		Kind:    "resource",
	}

	if pulumiPlugin.Parameterization != nil {
		result.Parameterization = &pulumirpc.PackageParameterization{
			Name:    pulumiPlugin.Parameterization.Name,
			Version: pulumiPlugin.Parameterization.Version,
			Value:   pulumiPlugin.Parameterization.Value,
		}
	}

	logging.V(5).Infof("GetRequiredPackages: Determining plugin dependency: %#v", result)
	return result, nil
}

func (host *dotnetLanguageHost) DotnetBuild(
	ctx context.Context, dotnetExec string, req *pulumirpc.GetRequiredPackagesRequest, engineClient pulumirpc.EngineClient,
) error {
	args := []string{"build", "-nologo"}

	// Run the `dotnet build` command.  Importantly, report the output of this to the user
	// (ephemerally) as it is happening so they're aware of what's going on and can see the progress
	// of things.
	_, err := RunDotnetCommand(ctx, dotnetExec, engineClient, args, true /*logToUser*/, req.Info.ProgramDirectory)
	if err != nil {
		return err
	}

	host.dotnetBuildSucceeded = true
	return nil
}

func RunDotnetCommand(
	ctx context.Context,
	dotnetExec string,
	engineClient pulumirpc.EngineClient,
	args []string, logToUser bool,
	programDirectory string,
) (string, error) {
	commandStr := strings.Join(args, " ")
	if logging.V(5) {
		logging.V(5).Infoln("Language host launching process: ", dotnetExec, commandStr)
	}

	// Buffer the writes we see from dotnet from its stdout and stderr streams. We will display
	// these ephemerally as `dotnet build` runs.  If the build does fail though, we will dump
	// messages back to our own stdout/stderr so they get picked up and displayed to the user.
	streamID := rand.Int31() //nolint:gosec

	infoBuffer := &bytes.Buffer{}
	errorBuffer := &bytes.Buffer{}

	infoWriter := &logWriter{
		ctx:          ctx,
		logToUser:    logToUser,
		engineClient: engineClient,
		streamID:     streamID,
		buffer:       infoBuffer,
		severity:     pulumirpc.LogSeverity_INFO,
	}

	errorWriter := &logWriter{
		ctx:          ctx,
		logToUser:    logToUser,
		engineClient: engineClient,
		streamID:     streamID,
		buffer:       errorBuffer,
		severity:     pulumirpc.LogSeverity_ERROR,
	}

	// Now simply spawn a process to execute the requested program, wiring up stdout/stderr directly.
	cmd := exec.CommandContext(ctx, dotnetExec, args...) //nolint:gas // intentionally running dynamic program name.
	cmd.Stdout = infoWriter
	cmd.Stderr = errorWriter
	cmd.Dir = programDirectory
	_, err := infoWriter.LogToUser(fmt.Sprintf("running 'dotnet %v'", commandStr))
	if err != nil {
		return "", err
	}

	if err := cmd.Run(); err != nil {
		// The command failed.  Dump any data we collected to the actual stdout/stderr streams so
		// they get displayed to the user.
		os.Stdout.Write(infoBuffer.Bytes())
		os.Stderr.Write(errorBuffer.Bytes())

		if exiterr, ok := err.(*exec.ExitError); ok {
			// If the program ran, but exited with a non-zero error code.  This will happen often, since user
			// errors will trigger this.  So, the error message should look as nice as possible.
			if status, stok := exiterr.Sys().(syscall.WaitStatus); stok {
				return "", errors.Errorf(
					"'dotnet %v' exited with non-zero exit code: %d", commandStr, status.ExitStatus())
			}

			return "", errors.Wrapf(exiterr, "'dotnet %v' exited unexpectedly", commandStr)
		}

		// Otherwise, we didn't even get to run the program.  This ought to never happen unless there's
		// a bug or system condition that prevented us from running the language exec.  Issue a scarier error.
		return "", errors.Wrapf(err, "Problem executing 'dotnet %v'", commandStr)
	}

	_, err = infoWriter.LogToUser(fmt.Sprintf("'dotnet %v' completed successfully", commandStr))
	return infoBuffer.String(), err
}

type logWriter struct {
	ctx          context.Context
	logToUser    bool
	engineClient pulumirpc.EngineClient
	streamID     int32
	severity     pulumirpc.LogSeverity
	buffer       *bytes.Buffer
}

func (w *logWriter) Write(p []byte) (n int, err error) {
	n, err = w.buffer.Write(p)
	if err != nil {
		return
	}

	return w.LogToUser(string(p))
}

func (w *logWriter) LogToUser(val string) (int, error) {
	if w.logToUser {
		_, err := w.engineClient.Log(w.ctx, &pulumirpc.LogRequest{
			Message:   strings.ToValidUTF8(val, "�"),
			Urn:       "",
			Ephemeral: true,
			StreamId:  w.streamID,
			Severity:  w.severity,
		})
		if err != nil {
			return 0, err
		}
	}

	return len(val), nil
}

// When debugging, we need to build the project, as the debugger does not support running using `dotnet run`.
// This function will build the project and return the path to the built DLL.
func buildDebuggingDLL(ctx context.Context, dotnetExec, programDirectory, entryPoint string) (string, error) {
	// If we are running from source, we need to build the project.
	// Run the `dotnet build` command.  Importantly, report the output of this to the user
	// (ephemerally) as it is happening so they're aware of what's going on and can see the progress
	// of things.
	args := []string{
		"build", "-nologo", "-o",
		filepath.Join(programDirectory, "bin", "pulumi-debugging"),
	}
	args = append(args, filepath.Join(programDirectory, entryPoint))

	cmd := exec.CommandContext(ctx, dotnetExec, args...)
	out, err := cmd.CombinedOutput()
	if err != nil {
		return "", errors.Wrapf(err, "failed to build project: %v, output: %v", err, string(out))
	}

	if entryPoint != "." {
		lastDot := strings.LastIndex(entryPoint, ".")
		if lastDot == -1 {
			return "", errors.New("entry point must have a file extension")
		}
		return filepath.Join("bin", "pulumi-debugging", entryPoint[:lastDot]+".dll"), nil
	}

	var binaryPath string
	err = filepath.WalkDir(programDirectory, func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}

		if name, ok := strings.CutSuffix(d.Name(), ".csproj"); ok {
			binaryPath = filepath.Join("bin", "pulumi-debugging", name+".dll")
			return filepath.SkipAll
		}
		if name, ok := strings.CutSuffix(d.Name(), ".fsproj"); ok {
			binaryPath = filepath.Join("bin", "pulumi-debugging", name+".dll")
			return filepath.SkipAll
		}
		if name, ok := strings.CutSuffix(d.Name(), ".vbproj"); ok {
			binaryPath = filepath.Join("bin", "pulumi-debugging", name+".dll")
			return filepath.SkipAll
		}
		return nil
	})

	return binaryPath, err
}

// Run is the RPC endpoint for LanguageRuntimeServer::Run
func (host *dotnetLanguageHost) Run(ctx context.Context, req *pulumirpc.RunRequest) (*pulumirpc.RunResponse, error) {
	opts, err := parseOptions(req.Info.RootDirectory, req.Info.Options.AsMap())
	if err != nil {
		return nil, err
	}

	binaryPath := opts.binary

	if req.GetAttachDebugger() && opts.binary == "" {
		var err error
		binaryPath, err = buildDebuggingDLL(ctx,
			opts.dotnetExec, req.GetInfo().GetProgramDirectory(), req.GetInfo().GetEntryPoint())
		if err != nil {
			return nil, err
		}
		if binaryPath == "" {
			return nil, errors.New("failed to find project file, and could not start debugging")
		}
	}
	config, err := host.constructConfig(req)
	if err != nil {
		err = errors.Wrap(err, "failed to serialize configuration")
		return nil, err
	}
	configSecretKeys, err := host.constructConfigSecretKeys(req)
	if err != nil {
		err = errors.Wrap(err, "failed to serialize configuration secret keys")
		return nil, err
	}

	executable := opts.dotnetExec
	args := []string{}

	switch {
	case binaryPath != "" && strings.HasSuffix(binaryPath, ".dll"):
		// Portable pre-compiled dll: run `dotnet <name>.dll`
		args = append(args, binaryPath)
	case binaryPath != "":
		// Self-contained executable: run it directly.
		executable = binaryPath
	default:
		// Run from source.
		args = append(args, "run")

		// If we are certain the project has been built,
		// passing a --no-build flag to dotnet run results in
		// up to 1s time savings.
		if host.dotnetBuildSucceeded {
			args = append(args, "--no-build")
		}
	}

	if logging.V(5) {
		commandStr := strings.Join(args, " ")
		logging.V(5).Infoln("Language host launching process: ", opts.dotnetExec, commandStr)
	}

	cmd := exec.CommandContext(ctx, executable, args...) //nolint:gas // intentionally running dynamic program name.

	// Now simply spawn a process to execute the requested program, wiring up stdout/stderr directly.
	var errResult string
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	cmd.Dir = req.Info.ProgramDirectory
	cmd.Env = host.constructEnv(req, config, configSecretKeys)
	if err := cmd.Start(); err != nil {
		return nil, err
	}

	if req.GetAttachDebugger() {
		engineClient, closer, err := host.connectToEngine()
		if err != nil {
			return nil, err
		}
		defer contract.IgnoreClose(closer)

		ctx, cancel := context.WithCancel(ctx)
		defer cancel()
		go func() {
			err = startDebugging(ctx, engineClient, cmd)
			if err != nil {
				// kill the program if we can't start debugging.
				logging.Errorf("Unable to start debugging: %v", err)
				contract.IgnoreError(cmd.Process.Kill())
			}
		}()
	}
	if err := cmd.Wait(); err != nil {
		if exiterr, ok := err.(*exec.ExitError); ok {
			// If the program ran, but exited with a non-zero error code.  This will happen often, since user
			// errors will trigger this.  So, the error message should look as nice as possible.
			if status, stok := exiterr.Sys().(syscall.WaitStatus); stok {
				// Check if we got special exit code that means "we already gave the user an
				// actionable message". In that case, we can simply bail out and terminate `pulumi`
				// without showing any more messages.
				if status.ExitStatus() == dotnetProcessExitedAfterShowingUserActionableMessage {
					return &pulumirpc.RunResponse{Error: "", Bail: true}, nil
				}

				err = errors.Errorf("Program exited with non-zero exit code: %d", status.ExitStatus())
			} else {
				err = errors.Wrapf(exiterr, "Program exited unexpectedly")
			}
		} else {
			// Otherwise, we didn't even get to run the program.  This ought to never happen unless there's
			// a bug or system condition that prevented us from running the language exec.  Issue a scarier error.
			err = errors.Wrapf(err, "Problem executing program (could not run language executor)")
		}

		errResult = err.Error()
	}

	return &pulumirpc.RunResponse{Error: errResult}, nil
}

func startDebugging(ctx context.Context, engineClient pulumirpc.EngineClient, cmd *exec.Cmd) error {
	// wait for the debugger to be ready
	ctx, cancel := context.WithTimeoutCause(ctx, 1*time.Minute, errors.New("debugger startup timed out"))
	defer cancel()
	// wait for the debugger to be ready

	debugConfig, err := structpb.NewStruct(map[string]interface{}{
		"name":      "Pulumi: Program (Dotnet)",
		"type":      "coreclr",
		"request":   "attach",
		"processId": cmd.Process.Pid,
	})
	if err != nil {
		return err
	}
	_, err = engineClient.StartDebugging(ctx, &pulumirpc.StartDebuggingRequest{
		Config:  debugConfig,
		Message: fmt.Sprintf("on process id %d", cmd.Process.Pid),
	})
	if err != nil {
		return fmt.Errorf("unable to start debugging: %w", err)
	}

	return nil
}

func (host *dotnetLanguageHost) constructEnv(req *pulumirpc.RunRequest, config, configSecretKeys string) []string {
	env := os.Environ()

	maybeAppendEnv := func(k, v string) {
		if v != "" {
			env = append(env, strings.ToUpper("PULUMI_"+k)+"="+v)
		}
	}

	maybeAppendEnv("monitor", req.GetMonitorAddress())
	maybeAppendEnv("engine", host.engineAddress)
	maybeAppendEnv("organization", req.GetOrganization())
	maybeAppendEnv("project", req.GetProject())
	maybeAppendEnv("stack", req.GetStack())
	maybeAppendEnv("pwd", req.GetPwd())
	maybeAppendEnv("dry_run", strconv.FormatBool(req.GetDryRun()))
	// The engine no longer supports query mode, but the runtime expects this envvar to be set to tell that it
	// was called correctly. See the Deployment constructor in sdk/Pulumi/Deployment/Deployment.cs.
	maybeAppendEnv("query_mode", "false")
	maybeAppendEnv("parallel", strconv.Itoa(int(req.GetParallel())))
	maybeAppendEnv("tracing", host.tracing)
	maybeAppendEnv("config", config)
	maybeAppendEnv("config_secret_keys", configSecretKeys)
	maybeAppendEnv("attach_debugger", strconv.FormatBool(req.GetAttachDebugger()))

	return env
}

// constructConfig json-serializes the configuration data given as part of a RunRequest.
func (host *dotnetLanguageHost) constructConfig(req *pulumirpc.RunRequest) (string, error) {
	configMap := req.GetConfig()
	if configMap == nil {
		return "", nil
	}

	configJSON, err := json.Marshal(configMap)
	if err != nil {
		return "", err
	}

	return string(configJSON), nil
}

// constructConfigSecretKeys JSON-serializes the list of keys that contain secret values given as part of
// a RunRequest.
func (host *dotnetLanguageHost) constructConfigSecretKeys(req *pulumirpc.RunRequest) (string, error) {
	configSecretKeys := req.GetConfigSecretKeys()
	if configSecretKeys == nil {
		return "[]", nil
	}

	configSecretKeysJSON, err := json.Marshal(configSecretKeys)
	if err != nil {
		return "", err
	}

	return string(configSecretKeysJSON), nil
}

func (host *dotnetLanguageHost) GetPluginInfo(ctx context.Context, req *emptypb.Empty) (*pulumirpc.PluginInfo, error) {
	return &pulumirpc.PluginInfo{
		Version: version.Version,
	}, nil
}

func (host *dotnetLanguageHost) InstallDependencies(
	req *pulumirpc.InstallDependenciesRequest, server pulumirpc.LanguageRuntime_InstallDependenciesServer,
) error {
	closer, stdout, stderr, err := rpcutil.MakeInstallDependenciesStreams(server, req.IsTerminal)
	if err != nil {
		return err
	}
	// best effort close, but we try an explicit close and error check at the end as well
	defer closer.Close()

	stdout.Write([]byte("Installing dependencies...\n\n"))

	dotnetbin, err := executable.FindExecutable("dotnet")
	if err != nil {
		return err
	}

	cmd := exec.CommandContext(server.Context(), dotnetbin, "build")
	cmd.Dir = req.Info.ProgramDirectory
	cmd.Stdout, cmd.Stderr = stdout, stderr

	if err := cmd.Run(); err != nil {
		return fmt.Errorf("`dotnet build` failed to install dependencies: %w", err)
	}
	stdout.Write([]byte("Finished installing dependencies\n\n"))

	if err := closer.Close(); err != nil {
		return err
	}

	return nil
}

func (host *dotnetLanguageHost) RuntimeOptionsPrompts(ctx context.Context,
	req *pulumirpc.RuntimeOptionsRequest,
) (*pulumirpc.RuntimeOptionsResponse, error) {
	return &pulumirpc.RuntimeOptionsResponse{}, nil
}

func (host *dotnetLanguageHost) About(
	ctx context.Context, req *pulumirpc.AboutRequest,
) (*pulumirpc.AboutResponse, error) {
	getResponse := func(execString string, args ...string) (string, string, error) {
		ex, err := executable.FindExecutable(execString)
		if err != nil {
			return "", "", fmt.Errorf("could not find executable '%s': %w", execString, err)
		}
		cmd := exec.CommandContext(ctx, ex, args...)
		var out []byte
		if out, err = cmd.Output(); err != nil {
			cmd := ex
			if len(args) != 0 {
				cmd += " " + strings.Join(args, " ")
			}
			return "", "", fmt.Errorf("failed to execute '%s'", cmd)
		}
		return ex, strings.TrimSpace(string(out)), nil
	}

	dotnet, version, err := getResponse("dotnet", "--version")
	if err != nil {
		return nil, err
	}

	return &pulumirpc.AboutResponse{
		Executable: dotnet,
		Version:    version,
	}, nil
}

func (host *dotnetLanguageHost) GetProgramDependencies(
	ctx context.Context, req *pulumirpc.GetProgramDependenciesRequest,
) (*pulumirpc.GetProgramDependenciesResponse, error) {
	// dotnet list package

	opts, err := parseOptions(req.Info.RootDirectory, req.Info.Options.AsMap())
	if err != nil {
		return nil, err
	}

	if opts.binary != "" {
		return nil, errors.New("Could not get dependencies because pulumi specifies a binary")
	}
	var ex string
	var out []byte
	ex, err = executable.FindExecutable("dotnet")
	if err != nil {
		return nil, err
	}
	cmdArgs := []string{"list", "package"}
	if req.TransitiveDependencies {
		cmdArgs = append(cmdArgs, "--include-transitive")
	}
	cmd := exec.CommandContext(ctx, ex, cmdArgs...)
	cmd.Dir = req.Info.ProgramDirectory
	if out, err = cmd.Output(); err != nil {
		return nil, fmt.Errorf("failed to call \"%s\": %w", ex, err)
	}
	lines := strings.Split(strings.ReplaceAll(string(out), "\r\n", "\n"), "\n")
	var packages []*pulumirpc.DependencyInfo

	for _, p := range lines {
		p := strings.TrimSpace(p)
		if strings.HasPrefix(p, ">") {
			p = strings.TrimPrefix(p, "> ")
			segments := strings.Split(p, " ")
			var nameRequiredVersion []string
			for _, s := range segments {
				if s != "" {
					nameRequiredVersion = append(nameRequiredVersion, s)
				}
			}
			var version int
			if len(nameRequiredVersion) == 3 {
				// Top level package => name required version
				version = 2
			} else if len(nameRequiredVersion) == 2 {
				// Transitive package => name version
				version = 1
			} else {
				return nil, fmt.Errorf("failed to parse \"%s\"", p)
			}
			packages = append(packages, &pulumirpc.DependencyInfo{
				Name:    nameRequiredVersion[0],
				Version: nameRequiredVersion[version],
			})
		}
	}
	return &pulumirpc.GetProgramDependenciesResponse{
		Dependencies: packages,
	}, nil
}

func (host *dotnetLanguageHost) RunPlugin(
	req *pulumirpc.RunPluginRequest, server pulumirpc.LanguageRuntime_RunPluginServer,
) error {
	logging.V(5).Infof("Attempting to run dotnet plugin in %s", req.Pwd)

	closer, stdout, stderr, err := rpcutil.MakeRunPluginStreams(server, false)
	if err != nil {
		return err
	}
	// best effort close, but we try an explicit close and error check at the end as well
	defer closer.Close()

	opts, err := parseOptions(req.Info.RootDirectory, req.Info.Options.AsMap())
	if err != nil {
		return err
	}

	binaryPath := opts.binary
	if req.GetAttachDebugger() && opts.binary == "" {
		var err error
		binaryPath, err = buildDebuggingDLL(server.Context(),
			opts.dotnetExec, req.GetInfo().GetRootDirectory(), req.GetInfo().GetEntryPoint())
		if err != nil {
			return err
		}
		binaryPath = filepath.Join(req.GetInfo().GetRootDirectory(), binaryPath)
		if binaryPath == "" {
			return errors.New("failed to find project file, and could not start debugging")
		}
	}

	executable := opts.dotnetExec
	args := []string{}

	switch {
	case binaryPath != "" && strings.HasSuffix(binaryPath, ".dll"):
		// Portable pre-compiled dll: run `dotnet <name>.dll`
		args = append(args, binaryPath)
	case binaryPath != "":
		// Self-contained executable: run it directly.
		executable = binaryPath
	default:
		// Build from source and then run. We build separately so that we can elide the build output from the
		// user unless there's an error. You would think you could pass something like `-v=q` to `dotnet run`
		// to get the same effect, but it doesn't work.
		project := req.Info.ProgramDirectory
		if req.Info.EntryPoint != "" {
			project = filepath.Join(project, req.Info.EntryPoint)
		}

		buildArgs := []string{"build", project}

		if logging.V(5) {
			commandStr := strings.Join(buildArgs, " ")
			logging.V(5).Infoln("Language host launching process: ", executable, " ", commandStr)
		}

		cmd := exec.CommandContext(
			server.Context(), executable, buildArgs...) //nolint:gas // intentionally running dynamic program name.
		cmd.Dir = req.Pwd
		cmd.Env = req.Env
		var buildOutput bytes.Buffer
		cmd.Stdout, cmd.Stderr = &buildOutput, &buildOutput
		if err = cmd.Run(); err != nil {
			// Build failed for some reason.  Dump the output to the user so they can see what went wrong.
			stderr.Write(buildOutput.Bytes())

			if exiterr, ok := err.(*exec.ExitError); ok {
				if status, stok := exiterr.Sys().(syscall.WaitStatus); stok {
					err = errors.Errorf("Build exited with non-zero exit code: %d", status.ExitStatus())
				} else {
					err = errors.Wrapf(exiterr, "Build exited unexpectedly")
				}
			} else {
				// Otherwise, we didn't even get to run the build. This ought to never happen unless there's
				// a bug or system condition that prevented us from running the language exec. Issue a scarier error.
				err = errors.Wrapf(err, "Problem building plugin program (could not run language executor)")
			}
			return err
		}

		// Now run from source without re-building.
		args = append(args, "run", "--no-build", "--project", project, "--")
	}

	// Add on all the request args to start this plugin
	args = append(args, req.Args...)

	if logging.V(5) {
		commandStr := strings.Join(args, " ")
		logging.V(5).Infoln("Language host launching process: ", executable, " ", commandStr)
	}

	// Now simply spawn a process to execute the requested program, wiring up stdout/stderr directly.
	cmd := exec.CommandContext(
		server.Context(), executable, args...) //nolint:gas // intentionally running dynamic program name.
	cmd.Dir = req.Pwd

	cmd.Env = req.Env
	if req.GetAttachDebugger() {
		cmd.Env = append(req.Env, "PULUMI_ATTACH_DEBUGGER=true")
	}
	cmd.Stdout, cmd.Stderr = stdout, stderr
	if err := cmd.Start(); err != nil {
		return err
	}

	if req.GetAttachDebugger() {
		engineClient, closer, err := host.connectToEngine()
		if err != nil {
			return err
		}
		defer contract.IgnoreClose(closer)

		ctx, cancel := context.WithCancel(server.Context())
		defer cancel()
		go func() {
			err = startDebugging(ctx, engineClient, cmd)
			if err != nil {
				// kill the program if we can't start debugging.
				logging.Errorf("Unable to start debugging: %v", err)
				contract.IgnoreError(cmd.Process.Kill())
			}
		}()
	}

	if err = cmd.Wait(); err != nil {
		if exiterr, ok := err.(*exec.ExitError); ok {
			// If the program ran, but exited with a non-zero error code.  This will happen often, since user
			// errors will trigger this.  So, the error message should look as nice as possible.
			if status, stok := exiterr.Sys().(syscall.WaitStatus); stok {
				err = errors.Errorf("Program exited with non-zero exit code: %d", status.ExitStatus())
			} else {
				err = errors.Wrapf(exiterr, "Program exited unexpectedly")
			}
		} else {
			// Otherwise, we didn't even get to run the program.  This ought to never happen unless there's
			// a bug or system condition that prevented us from running the language exec.  Issue a scarier error.
			err = errors.Wrapf(err, "Problem executing plugin program (could not run language executor)")
		}
	}

	if err != nil {
		return err
	}

	if err := closer.Close(); err != nil {
		return err
	}

	return nil
}

func (host *dotnetLanguageHost) Pack(ctx context.Context, req *pulumirpc.PackRequest) (*pulumirpc.PackResponse, error) {
	// copy the source to the target.
	projectFile := ""
	err := filepath.Walk(req.PackageDirectory, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if filepath.Ext(path) == ".csproj" {
			projectFile = strings.TrimSuffix(path, ".csproj")
		}
		return nil
	})

	if err != nil || projectFile == "" {
		return nil, fmt.Errorf("find project file: %w", err)
	}

	// Get the default options so we can get the dotnet executable to use
	opts, err := parseOptions(req.PackageDirectory, nil)
	if err != nil {
		return nil, err
	}

	build := func() ([]byte, error) {
		err := os.RemoveAll(filepath.Join(req.PackageDirectory, "bin"))
		if err != nil {
			return nil, err
		}
		err = os.RemoveAll(filepath.Join(req.PackageDirectory, "obj"))
		if err != nil {
			return nil, err
		}

		cmd := exec.CommandContext( //nolint:gosec // intentionally running dynamic program name.
			ctx,
			opts.dotnetExec, "build", "-c", "Release")
		cmd.Dir = req.PackageDirectory
		return cmd.CombinedOutput()
	}

	if out, err := build(); err != nil {
		return nil, errutil.ErrorWithStderr(err, "build error before pack.  stdout: "+string(out))
	}

	destination := filepath.Join(req.DestinationDirectory, filepath.Base(projectFile))

	cmd := exec.CommandContext( //nolint:gosec // intentionally running dynamic program name.
		ctx,
		opts.dotnetExec, "pack", "-c", "Release", "-o", destination)
	cmd.Dir = req.PackageDirectory

	if err := cmd.Run(); err != nil {
		return nil, fmt.Errorf("failed to pack: %w", err)
	}

	var nugetFilePath string
	err = filepath.Walk(destination, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if filepath.Ext(path) == ".nupkg" {
			nugetFilePath = path
		}
		return nil
	})

	if err != nil || nugetFilePath == "" {
		return nil, fmt.Errorf("couldn't find packed nuget: %w", err)
	}

	return &pulumirpc.PackResponse{
		ArtifactPath: nugetFilePath,
	}, nil
}

func (host *dotnetLanguageHost) GeneratePackage(
	ctx context.Context, req *pulumirpc.GeneratePackageRequest,
) (*pulumirpc.GeneratePackageResponse, error) {
	loader, err := schema.NewLoaderClient(req.LoaderTarget)
	if err != nil {
		return nil, err
	}

	var spec schema.PackageSpec
	err = json.Unmarshal([]byte(req.Schema), &spec)
	if err != nil {
		return nil, err
	}

	pkg, diags, err := schema.BindSpec(spec, loader, schema.ValidationOptions{
		AllowDanglingReferences: true,
	})
	if err != nil {
		return nil, err
	}
	rpcDiagnostics := plugin.HclDiagnosticsToRPCDiagnostics(diags)
	if diags.HasErrors() {
		return &pulumirpc.GeneratePackageResponse{
			Diagnostics: rpcDiagnostics,
		}, nil
	}
	files, err := dotnetcodegen.GeneratePackage("pulumi-language-dotnet", pkg, req.ExtraFiles, req.LocalDependencies)
	if err != nil {
		return nil, err
	}

	for filename, data := range files {
		outPath := filepath.Join(req.Directory, filename)
		err := os.MkdirAll(filepath.Dir(outPath), 0o700)
		if err != nil {
			return nil, fmt.Errorf("could not create output directory %s: %w", filepath.Dir(filename), err)
		}

		err = os.WriteFile(outPath, data, 0o600)
		if err != nil {
			return nil, fmt.Errorf("could not write output file %s: %w", filename, err)
		}
	}

	return &pulumirpc.GeneratePackageResponse{
		Diagnostics: rpcDiagnostics,
	}, nil
}

func (host *dotnetLanguageHost) GenerateProject(
	ctx context.Context, req *pulumirpc.GenerateProjectRequest,
) (*pulumirpc.GenerateProjectResponse, error) {
	loader, err := schema.NewLoaderClient(req.LoaderTarget)
	if err != nil {
		return nil, err
	}

	var extraOptions []pcl.BindOption
	if !req.Strict {
		extraOptions = append(extraOptions, pcl.NonStrictBindOptions()...)
	}

	program, diags, err := pcl.BindDirectory(req.SourceDirectory, loader, extraOptions...)
	if err != nil {
		return nil, err
	}

	rpcDiagnostics := plugin.HclDiagnosticsToRPCDiagnostics(diags)
	if diags.HasErrors() {
		return &pulumirpc.GenerateProjectResponse{
			Diagnostics: rpcDiagnostics,
		}, nil
	}
	if program == nil {
		return nil, errors.New("internal error: program was nil")
	}

	var project workspace.Project
	if err := json.Unmarshal([]byte(req.Project), &project); err != nil {
		return nil, err
	}

	err = dotnetcodegen.GenerateProject(req.TargetDirectory, project, program, req.LocalDependencies)
	if err != nil {
		return nil, err
	}

	return &pulumirpc.GenerateProjectResponse{
		Diagnostics: rpcDiagnostics,
	}, nil
}

func (host *dotnetLanguageHost) GenerateProgram(
	ctx context.Context, req *pulumirpc.GenerateProgramRequest,
) (*pulumirpc.GenerateProgramResponse, error) {
	loader, err := schema.NewLoaderClient(req.LoaderTarget)
	if err != nil {
		return nil, err
	}
	defer loader.Close()

	parser := hclsyntax.NewParser()
	// Load all .pp files in the directory
	for path, contents := range req.Source {
		err = parser.ParseFile(strings.NewReader(contents), path)
		if err != nil {
			return nil, err
		}
		diags := parser.Diagnostics
		if diags.HasErrors() {
			return nil, diags
		}
	}

	bindOptions := []pcl.BindOption{
		pcl.Loader(schema.NewCachedLoader(loader)),
	}

	if !req.Strict {
		bindOptions = append(bindOptions, pcl.NonStrictBindOptions()...)
	}

	program, diags, err := pcl.BindProgram(parser.Files, bindOptions...)
	if err != nil {
		return nil, err
	}

	rpcDiagnostics := plugin.HclDiagnosticsToRPCDiagnostics(diags)
	if diags.HasErrors() {
		return &pulumirpc.GenerateProgramResponse{
			Diagnostics: rpcDiagnostics,
		}, nil
	}
	if program == nil {
		return nil, errors.New("internal error program was nil")
	}

	files, diags, err := dotnetcodegen.GenerateProgram(program)
	if err != nil {
		return nil, err
	}
	rpcDiagnostics = append(rpcDiagnostics, plugin.HclDiagnosticsToRPCDiagnostics(diags)...)

	return &pulumirpc.GenerateProgramResponse{
		Source:      files,
		Diagnostics: rpcDiagnostics,
	}, nil
}
