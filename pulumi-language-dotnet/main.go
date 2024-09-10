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
	"io/ioutil"
	"math/rand"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"strings"
	"syscall"
	"time"

	"github.com/blang/semver"
	pbempty "github.com/golang/protobuf/ptypes/empty"
	"github.com/pkg/errors"
	"github.com/pulumi/pulumi/sdk/v3/go/common/resource/plugin"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/cmdutil"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/contract"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/executable"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/logging"
	"github.com/pulumi/pulumi/sdk/v3/go/common/util/rpcutil"
	"github.com/pulumi/pulumi/sdk/v3/go/common/version"
	pulumirpc "github.com/pulumi/pulumi/sdk/v3/proto/go"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
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
	flag.StringVar(&binary, "binary", "", "A relative or an absolute path to a precompiled .NET assembly to execute")
	flag.StringVar(&root, "root", "", "Project root path to use")

	// You can use the below flag to request that the language host load a specific executor instead of probing the
	// PATH.  This can be used during testing to override the default location.
	var givenExecutor string
	flag.StringVar(&givenExecutor, "use-executor", "",
		"Use the given program as the executor instead of looking for one on PATH")

	flag.Parse()
	args := flag.Args()
	logging.InitLogging(false, 0, false)
	cmdutil.InitTracing("pulumi-language-dotnet", "pulumi-language-dotnet", tracing)
	var dotnetExec string
	switch {
	case givenExecutor != "":
		logging.V(3).Infof("language host asked to use specific executor: `%s`", givenExecutor)
		dotnetExec = givenExecutor
	case binary != "" && !strings.HasSuffix(binary, ".dll"):
		logging.V(3).Info("language host requires no .NET SDK for a self-contained binary")
	default:
		pathExec, err := exec.LookPath("dotnet")
		if err != nil {
			err = errors.Wrap(err, "could not find `dotnet` on the $PATH")
			cmdutil.Exit(err)
		}

		logging.V(3).Infof("language host identified executor from path: `%s`", pathExec)
		dotnetExec = pathExec
	}

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
			host := newLanguageHost(dotnetExec, engineAddress, tracing, binary)
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

	exec                 string
	engineAddress        string
	tracing              string
	binary               string
	dotnetBuildSucceeded bool
}

func newLanguageHost(exec, engineAddress, tracing string, binary string) pulumirpc.LanguageRuntimeServer {
	return &dotnetLanguageHost{
		exec:          exec,
		engineAddress: engineAddress,
		tracing:       tracing,
		binary:        binary,
	}
}

func (host *dotnetLanguageHost) connectToEngine() (pulumirpc.EngineClient, io.Closer, error) {
	// Make a connection to the real engine that we will log messages to.
	conn, err := grpc.Dial(
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
	logging.V(5).Infof("GetRequiredPlugins: %v", req.GetProgram())

	if host.binary != "" {
		logging.V(5).Infof("GetRequiredPlugins: no plugins can be listed when a binary is specified")
		return &pulumirpc.GetRequiredPluginsResponse{}, nil
	}

	engineClient, closer, err := host.connectToEngine()
	if err != nil {
		return nil, err
	}
	defer contract.IgnoreClose(closer)

	// First do a `dotnet build`.  This will ensure that all the nuget dependencies of the project
	// are restored and locally available for us.
	if err := host.DotnetBuild(ctx, req, engineClient); err != nil {
		return nil, err
	}

	// now, introspect the user project to see which pulumi resource packages it references.
	possiblePulumiPackages, err := host.DeterminePossiblePulumiPackages(ctx, engineClient)
	if err != nil {
		return nil, err
	}

	// Ensure we know where the local nuget package cache directory is.  User can specify where that
	// is located, so this makes sure we respect any custom location they may have.
	packageDir, err := host.DetermineDotnetPackageDirectory(ctx, engineClient)
	if err != nil {
		return nil, err
	}

	// Now that we know the set of pulumi packages referenced and we know where packages have been restored to,
	// we can examine each package to determine the corresponding resource-plugin for it.

	plugins := []*pulumirpc.PluginDependency{}
	packageToVersion := make(map[string]string)
	for _, parts := range possiblePulumiPackages {
		packageName := parts[0]
		packageVersion := parts[1]

		if existingVersion := packageToVersion[packageName]; existingVersion == packageVersion {
			// only include distinct dependencies.
			continue
		}

		packageToVersion[packageName] = packageVersion

		plugin, err := DeterminePluginDependency(packageDir, packageName, packageVersion)
		if err != nil {
			return nil, err
		}

		if plugin != nil {
			plugins = append(plugins, plugin)
		}
	}

	return &pulumirpc.GetRequiredPluginsResponse{Plugins: plugins}, nil
}

func (host *dotnetLanguageHost) DeterminePossiblePulumiPackages(
	ctx context.Context, engineClient pulumirpc.EngineClient,
) ([][]string, error) {
	logging.V(5).Infof("GetRequiredPlugins: Determining pulumi packages")

	// Run the `dotnet list package --include-transitive` command.  Importantly, do not clutter the
	// stream with the extra steps we're performing. This is just so we can determine the required
	// plugins.  And, after the first time we do this, subsequent runs will see that the plugin is
	// installed locally and not need to do anything.
	args := []string{"list", "package", "--include-transitive"}
	commandStr := strings.Join(args, " ")
	commandOutput, err := host.RunDotnetCommand(ctx, engineClient, args, false /*logToUser*/)
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
	outputLines := strings.Split(strings.Replace(commandOutput, "\r\n", "\n", -1), "\n")

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
	ctx context.Context, engineClient pulumirpc.EngineClient,
) (string, error) {
	logging.V(5).Infof("GetRequiredPlugins: Determining package directory")

	// Run the `dotnet nuget locals global-packages --list` command.  Importantly, do not clutter
	// the stream with the extra steps we're performing. This is just so we can determine the
	// required plugins.  And, after the first time we do this, subsequent runs will see that the
	// plugin is installed locally and not need to do anything.
	args := []string{"nuget", "locals", "global-packages", "--list"}
	commandStr := strings.Join(args, " ")
	commandOutput, err := host.RunDotnetCommand(ctx, engineClient, args, false /*logToUser*/)
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

func DeterminePluginDependency(packageDir, packageName, packageVersion string) (*pulumirpc.PluginDependency, error) {
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
	b, err := ioutil.ReadFile(versionFilePath)

	switch {
	case err == nil:
		vf = newVersionFile(b, packageName)
		break
	case os.IsNotExist(err):
		break
	case err != nil:
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

	result := &pulumirpc.PluginDependency{
		Name:    name,
		Version: version,
		Server:  pulumiPlugin.Server,
		Kind:    "resource",
	}

	logging.V(5).Infof("GetRequiredPlugins: Determining plugin dependency: %#v", result)
	return result, nil
}

func (host *dotnetLanguageHost) DotnetBuild(
	ctx context.Context, req *pulumirpc.GetRequiredPluginsRequest, engineClient pulumirpc.EngineClient,
) error {
	args := []string{"build", "-nologo"}

	if req.GetProgram() != "" {
		args = append(args, req.GetProgram())
	}

	// Run the `dotnet build` command.  Importantly, report the output of this to the user
	// (ephemerally) as it is happening so they're aware of what's going on and can see the progress
	// of things.
	_, err := host.RunDotnetCommand(ctx, engineClient, args, true /*logToUser*/)
	if err != nil {
		return err
	}

	host.dotnetBuildSucceeded = true
	return nil
}

func (host *dotnetLanguageHost) RunDotnetCommand(
	ctx context.Context, engineClient pulumirpc.EngineClient, args []string, logToUser bool,
) (string, error) {
	commandStr := strings.Join(args, " ")
	if logging.V(5) {
		logging.V(5).Infoln("Language host launching process: ", host.exec, commandStr)
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
	cmd := exec.Command(host.exec, args...) // nolint: gas // intentionally running dynamic program name.

	cmd.Stdout = infoWriter
	cmd.Stderr = errorWriter

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

func (host *dotnetLanguageHost) buildDll() (string, error) {
	// If we are running from source, we need to build the project.
	// Run the `dotnet build` command.  Importantly, report the output of this to the user
	// (ephemerally) as it is happening so they're aware of what's going on and can see the progress
	// of things.
	args := []string{"build", "-nologo", "-o", "bin/pulumi-debugging"}

	cmd := exec.Command(host.exec, args...)
	err := cmd.Run()
	if err != nil {
		return "", errors.Wrapf(err, "failed to build project: %v", err)
	}

	var binaryPath string
	err = filepath.WalkDir(".", func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}

		if name, ok := strings.CutSuffix(d.Name(), ".csproj"); ok {
			binaryPath = filepath.Join("bin", "pulumi-debugging", name+".dll")
			return filepath.SkipAll
		}
		return nil
	})

	return binaryPath, err
}

// Run is the RPC endpoint for LanguageRuntimeServer::Run
func (host *dotnetLanguageHost) Run(ctx context.Context, req *pulumirpc.RunRequest) (*pulumirpc.RunResponse, error) {
	binaryPath := host.binary
	if req.GetAttachDebugger() && host.binary == "" {
		binaryPath, err := host.buildDll()
		if err != nil {
			return nil, err
		}
		if binaryPath == "" {
			return nil, errors.New("failed to find .csproj file, and could not start debugging")
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

	executable := host.exec
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

		if req.GetProgram() != "" {
			args = append(args, req.GetProgram())
		}
	}

	if logging.V(5) {
		commandStr := strings.Join(args, " ")
		logging.V(5).Infoln("Language host launching process: ", host.exec, commandStr)
	}

	cmd := exec.Command(executable, args...) // nolint: gas // intentionally running dynamic program name.

	// Now simply spawn a process to execute the requested program, wiring up stdout/stderr directly.
	var errResult string
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
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
	ctx, _ = context.WithTimeoutCause(ctx, 1*time.Minute, errors.New("debugger startup timed out"))
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
	maybeAppendEnv("dry_run", fmt.Sprintf("%v", req.GetDryRun()))
	maybeAppendEnv("query_mode", fmt.Sprint(req.GetQueryMode()))
	maybeAppendEnv("parallel", fmt.Sprint(req.GetParallel()))
	maybeAppendEnv("tracing", host.tracing)
	maybeAppendEnv("config", config)
	maybeAppendEnv("config_secret_keys", configSecretKeys)
	maybeAppendEnv("attach_debugger", fmt.Sprint(req.GetAttachDebugger()))

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

func (host *dotnetLanguageHost) GetPluginInfo(ctx context.Context, req *pbempty.Empty) (*pulumirpc.PluginInfo, error) {
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

	cmd := exec.Command(dotnetbin, "build")
	cmd.Dir = req.Directory
	cmd.Env = os.Environ()
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

func (host *dotnetLanguageHost) About(ctx context.Context, req *pulumirpc.AboutRequest) (*pulumirpc.AboutResponse, error) {
	getResponse := func(execString string, args ...string) (string, string, error) {
		ex, err := executable.FindExecutable(execString)
		if err != nil {
			return "", "", fmt.Errorf("could not find executable '%s': %w", execString, err)
		}
		cmd := exec.Command(ex, args...)
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

	var err error
	if host.binary != "" {
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
	cmd := exec.Command(ex, cmdArgs...)
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
	logging.V(5).Infof("Attempting to run dotnet plugin in %s", req.Program)

	closer, stdout, stderr, err := rpcutil.MakeRunPluginStreams(server, false)
	if err != nil {
		return err
	}
	// best effort close, but we try an explicit close and error check at the end as well
	defer closer.Close()

	executable := host.exec
	args := []string{}

	switch {
	case host.binary != "" && strings.HasSuffix(host.binary, ".dll"):
		// Portable pre-compiled dll: run `dotnet <name>.dll`
		args = append(args, host.binary)
	case host.binary != "":
		// Self-contained executable: run it directly.
		executable = host.binary
	default:
		// Run from source.
		args = append(args, "run")
		if req.Program != "" {
			args = append(args, "--project", req.Program)
		}
	}
	// Add on all the request args to start this plugin
	args = append(args, req.Args...)

	if logging.V(5) {
		commandStr := strings.Join(args, " ")
		logging.V(5).Infoln("Language host launching process: ", executable, commandStr)
	}

	// Now simply spawn a process to execute the requested program, wiring up stdout/stderr directly.
	cmd := exec.Command(executable, args...) // nolint: gas // intentionally running dynamic program name.
	cmd.Dir = req.Pwd
	cmd.Env = req.Env
	cmd.Stdout, cmd.Stderr = stdout, stderr
	if err := cmd.Run(); err != nil {
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
