// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pulumi.Automation.Commands;
using Semver;
using Xunit;

namespace Pulumi.Automation.Tests
{
    public class LocalPulumiCommandTests
    {
        [Fact]
        public async Task CheckVersionCommand()
        {
            var localCmd = await LocalPulumiCommand.CreateAsync();
            var extraEnv = new Dictionary<string, string?>();
            var args = new[] { "version" };

            var stdoutLines = new List<string>();
            var stderrLines = new List<string>();

            // NOTE: not testing onEngineEvent arg as that is not
            // supported for "version"; to test it one needs
            // workspace-aware commands such as up or preview;
            // currently this is covered by
            // LocalWorkspaceTests.HandlesEvents.

            var result = await localCmd.RunAsync(
                args, ".", extraEnv,
                onStandardOutput: line => stdoutLines.Add(line),
                onStandardError: line => stderrLines.Add(line));

            Assert.Equal(0, result.Code);

            Assert.Matches(@"^v?\d+\.\d+\.\d+", result.StandardOutput);
            // stderr must strictly begin with the version warning message or be an empty string:
            if (result.StandardError.Length > 0)
            {
                Assert.StartsWith("warning: A new version of Pulumi", result.StandardError);
            }

            // If these tests begin failing, it may be because the automation output now emits CRLF
            // (\r\n) on Windows.
            //
            // If so, update the Lines method to split on Environment.NewLine instead of "\n".
            Assert.Equal(Lines(result.StandardOutput), stdoutLines.Select(x => x.Trim()).ToList());
            Assert.Equal(Lines(result.StandardError), stderrLines.Select(x => x.Trim()).ToList());
        }

        private List<string> Lines(string s)
        {
            return s.Split("\n",
                           StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();
        }

        [Fact]
        public async Task InstallDefaultRoot()
        {
            var requestedVersion = new SemVersion(3, 102, 0);
            await LocalPulumiCommand.Install(new LocalPulumiCommandOptions { Version = requestedVersion });
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var pulumiBin = Path.Combine(home, ".pulumi", "versions", requestedVersion.ToString(), "bin", "pulumi");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pulumiBin += ".exe";
            }
            Assert.True(File.Exists(pulumiBin));
        }

        [Fact]
        public async Task InstallTwice()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "automation-test-" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var requestedVersion = new SemVersion(3, 102, 0);
                await LocalPulumiCommand.Install(new LocalPulumiCommandOptions { Version = requestedVersion, Root = tempDir });
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var pulumiBin = Path.Combine(home, ".pulumi", "versions", requestedVersion.ToString(), "bin", "pulumi");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    pulumiBin += ".exe";
                }
                var t1 = File.GetCreationTime(pulumiBin);
                // Install again with the same options
                await LocalPulumiCommand.Install(new LocalPulumiCommandOptions { Version = requestedVersion, Root = tempDir });
                var t2 = File.GetCreationTime(pulumiBin);
                Assert.Equal(t1, t2);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

        }

        [Fact]
        public async Task VersionCheck()
        {
            var dirPath = Path.Combine(Path.GetTempPath(), "automation-test-" + Guid.NewGuid().ToString());
            var dir = Directory.CreateDirectory(dirPath);
            try
            {
                // Install an old version
                var installed_version = new SemVersion(3, 99, 0);
                await LocalPulumiCommand.Install(new LocalPulumiCommandOptions { Version = installed_version, Root = dirPath });

                // Try to create a command with a more recent version
                var requested_version = new SemVersion(3, 102, 0);
                await Assert.ThrowsAsync<InvalidOperationException>(() => LocalPulumiCommand.CreateAsync(new LocalPulumiCommandOptions
                {
                    Version = requested_version,
                    Root = dirPath
                }));

                // Opting out of the version check works
                await LocalPulumiCommand.CreateAsync(new LocalPulumiCommandOptions
                {
                    Version = requested_version,
                    Root = dirPath,
                    SkipVersionCheck = true
                });
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public void PulumiEnvironment()
        {
            // Plain "pulumi" command
            var env = new Dictionary<string, string?> { { "PATH", "/usr/bin" } };
            var newEnv = LocalPulumiCommand.PulumiEnvironment(env, "pulumi", false);
            Assert.Equal("/usr/bin", newEnv["PATH"]);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                env = new Dictionary<string, string?> { { "PATH", "%SystemRoot%\\system32" } };
                newEnv = LocalPulumiCommand.PulumiEnvironment(env, "C:\\some\\install\\root\\bin\\pulumi", false);
                Assert.Equal("C:\\some\\install\\root\\bin;%SystemRoot%\\system32", newEnv["PATH"]);
            }
            else
            {
                env = new Dictionary<string, string?> { { "PATH", "/usr/bin" } };
                newEnv = LocalPulumiCommand.PulumiEnvironment(env, "/some/install/root/bin/pulumi", false);
                Assert.Equal("/some/install/root/bin:/usr/bin", newEnv["PATH"]);
            }
        }

        [Theory]
        [InlineData("100.0.0", true, false)]
        [InlineData("1.0.0", true, false)]
        [InlineData("2.22.0", false, false)]
        [InlineData("2.1.0", true, false)]
        [InlineData("2.21.2", false, false)]
        [InlineData("2.21.1", false, false)]
        [InlineData("2.21.0", true, false)]
        // Note that prerelease < release so this case should error
        [InlineData("2.21.1-alpha.1234", true, false)]
        [InlineData("2.20.0", false, true)]
        [InlineData("2.22.0", false, true)]
        // Invalid version check
        [InlineData("invalid", false, true)]
        [InlineData("invalid", true, false)]
        public void ValidVersionTheory(string currentVersion, bool errorExpected, bool optOut)
        {
            var testMinVersion = new SemVersion(2, 21, 1);

            if (errorExpected)
            {
                void ValidatePulumiVersion() => LocalPulumiCommand.ParseAndValidatePulumiVersion(testMinVersion, currentVersion, optOut);
                Assert.Throws<InvalidOperationException>(ValidatePulumiVersion);
            }
            else
            {
                LocalPulumiCommand.ParseAndValidatePulumiVersion(testMinVersion, currentVersion, optOut);
            }
        }

    }

}
