// Copyright 2026, Pulumi Corporation

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Testing;
using Pulumi.Tests.Mocks;
using Pulumirpc;
using Xunit;

namespace Pulumi.Tests
{
    public class PackageReferenceTests
    {
        private sealed class ParameterizedResource : CustomResource
        {
            public ParameterizedResource(string name, RegisterPackageRequest request)
                : base("pkg:index:MyCustom", name, ResourceArgs.Empty, options: null, request)
            {
            }
        }

        private sealed class PackageRefTrackingMonitor : MockMonitor
        {
            private int _refCounter;

            public int RegisterPackageCallCount;
            public readonly List<string> ResourcePackageRefs = new List<string>();

            public PackageRefTrackingMonitor(IMocks mocks)
                : base(mocks)
            {
            }

            public override Task<RegisterPackageResponse> RegisterPackageAsync(Pulumirpc.RegisterPackageRequest request)
            {
                Interlocked.Increment(ref RegisterPackageCallCount);
                var n = Interlocked.Increment(ref _refCounter);
                return Task.FromResult(new RegisterPackageResponse { Ref = $"pkgref-{n}" });
            }

            public override Task<RegisterResourceResponse> RegisterResourceAsync(Resource resource, RegisterResourceRequest request)
            {
                if (request.Type != Stack._rootPulumiStackTypeName)
                {
                    lock (ResourcePackageRefs)
                    {
                        ResourcePackageRefs.Add(request.PackageRef);
                    }
                }
                return base.RegisterResourceAsync(resource, request);
            }
        }

        private static RegisterPackageRequest NewRequest(string parameterizationName = "myparameterizedpackage") =>
            new RegisterPackageRequest(
                name: "terraform-provider",
                version: "1.0.0",
                downloadUrl: "",
                parameterization: new RegisterPackageRequest.PackageParameterization(
                    name: parameterizationName,
                    version: "2.0.0",
                    value: new byte[] { 1, 2, 3 }));

        private static Task RunDeploymentAsync(IMonitor monitor, params RegisterPackageRequest[] requests) =>
            Pulumi.Deployment.CreateRunnerAndRunAsync(
                () => new Pulumi.Deployment(new MockEngine(), monitor, null),
                runner => runner.RunAsync(() =>
                {
                    for (var i = 0; i < requests.Length; i++)
                    {
                        _ = new ParameterizedResource($"res-{i}", requests[i]);
                    }
                    return Task.FromResult((IDictionary<string, object?>)new Dictionary<string, object?>());
                }, null));

        /// <summary>
        /// Regression test for https://github.com/pulumi/pulumi/issues/21950.
        ///
        /// Package references are only valid for a single engine invocation; they don't
        /// persist across multiple runs. When the Automation API runs inline programs in a
        /// long-running process, each run gets a fresh deployment. The SDK must register the
        /// package with the engine on every run rather than caching the reference at the
        /// module/class level, otherwise subsequent runs send a stale reference and the
        /// engine fails with "unknown provider package".
        /// </summary>
        [Fact]
        public async Task PackageReferenceIsResolvedPerDeployment()
        {
            // Share a single monitor across both runs so the package references it hands
            // out are globally unique. If the SDK cached the reference statically, the
            // second run would reuse the first run's reference instead of registering again.
            var monitor = new PackageRefTrackingMonitor(new MyMocks());

            await RunDeploymentAsync(monitor, NewRequest());
            await RunDeploymentAsync(monitor, NewRequest());

            // The package must be registered once per deployment, not once per process.
            Assert.Equal(2, monitor.RegisterPackageCallCount);

            // Each run's resource must carry that run's freshly-resolved package reference.
            Assert.Equal(new[] { "pkgref-1", "pkgref-2" }, monitor.ResourcePackageRefs);
        }

        /// <summary>
        /// Within a single deployment, the same package should only be registered once: the
        /// per-deployment cache deduplicates identical registration requests.
        /// </summary>
        [Fact]
        public async Task SamePackageIsRegisteredOncePerDeployment()
        {
            var monitor = new PackageRefTrackingMonitor(new MyMocks());

            await RunDeploymentAsync(monitor, NewRequest(), NewRequest());

            Assert.Equal(1, monitor.RegisterPackageCallCount);
            Assert.Equal(new[] { "pkgref-1", "pkgref-1" }, monitor.ResourcePackageRefs);
        }

        /// <summary>
        /// Two different parameterized packages that share the same base provider name and
        /// version (for example two packages bridged from the same Terraform provider) must
        /// each receive their own package reference. Caching by base provider name and version
        /// alone would make the second package collide with the first and reuse its reference.
        /// </summary>
        [Fact]
        public async Task DistinctParameterizedPackagesDoNotCollide()
        {
            var monitor = new PackageRefTrackingMonitor(new MyMocks());

            await RunDeploymentAsync(monitor, NewRequest("packageA"), NewRequest("packageB"));

            Assert.Equal(2, monitor.RegisterPackageCallCount);
            Assert.Equal(new[] { "pkgref-1", "pkgref-2" }, monitor.ResourcePackageRefs);
        }
    }
}
