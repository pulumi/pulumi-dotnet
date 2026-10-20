// Copyright 2016-2020, Pulumi Corporation

using System.Linq;
using System.Threading;
using Grpc.Core;
using Moq;
using Moq.Protected;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class ResourcePackagesTests
    {
        [Fact]
        public void UnknownNotFound()
        {
            if (ResourcePackages.TryGetResourceType("test:index/UnknownResource", null, out _))
            {
                Assert.Fail("Unknown resource found");
            }
            if (ResourcePackages.TryGetResourceType("test:index/UnknownResource", "", out _))
            {
                Assert.Fail("Unknown resource found");
            }
            if (ResourcePackages.TryGetResourceType("unknown:index/TestResource", "0.0.1", out _))
            {
                Assert.Fail("Unknown resource found");
            }
            if (ResourcePackages.TryGetResourceType("unknown:index/AnotherResource", "1.0.0", out _))
            {
                Assert.Fail("Resource with non-matching assembly version found");
            }
        }

        [Fact]
        public void NullReturnsHighestVersion()
        {
            if (ResourcePackages.TryGetResourceType("test:index/TestResource", null, out var type))
            {
                Assert.Equal(typeof(Version202TestResource), type);
            }
            else
            {
                Assert.Fail("Test resource not found");
            }
        }

        [Fact]
        public void BlankReturnsHighestVersion()
        {
            if (ResourcePackages.TryGetResourceType("test:index/TestResource", "", out var type))
            {
                Assert.Equal(typeof(Version202TestResource), type);
            }
            else
            {
                Assert.Fail("Test resource not found");
            }
        }

        [Fact]
        public void MajorVersionRespected()
        {
            if (ResourcePackages.TryGetResourceType("test:index/TestResource", "1.0.0", out var type))
            {
                Assert.Equal(typeof(Version102TestResource), type);
            }
            else
            {
                Assert.Fail("Test resource not found");
            }
        }

        [Fact]
        public void WildcardSelectedIfOthersDontMatch()
        {
            if (ResourcePackages.TryGetResourceType("test:index/TestResource", "3.0.0", out var type))
            {
                Assert.Equal(typeof(WildcardTestResource), type);
            }
            else
            {
                Assert.Fail("Test resource not found");
            }
        }

        [ResourceType("test:index/TestResource", "1.0.1-alpha1")]
        private class Version101TestResource : CustomResource
        {
            public Version101TestResource(string type, string name, ResourceArgs? args, CustomResourceOptions? options = null) : base(type, name, args, options)
            {
            }
        }

        [ResourceType("test:index/TestResource", "1.0.2")]
        private class Version102TestResource : CustomResource
        {
            public Version102TestResource(string type, string name, ResourceArgs? args, CustomResourceOptions? options = null) : base(type, name, args, options)
            {
            }
        }

        [Fact]
        public void DiscoveryIgnoresDynamicAssemblies()
        {
            // Moq puts these proxies into two dynamic assemblies, both named DynamicProxyGenAssembly2: one
            // weak-named, and one strong-named that references the weak-named one.
            _ = new Mock<IDeploymentInternal>().Object;
            var context = new Mock<ServerCallContext>();
            context.Protected().SetupGet<CancellationToken>("CancellationTokenCore").Returns(CancellationToken.None);
            _ = context.Object;

            var types = ResourcePackages.DiscoverResourceTypes();

            Assert.Equal(
                new[] { typeof(Version101TestResource), typeof(Version102TestResource), typeof(Version202TestResource), typeof(WildcardTestResource) }
                    .OrderBy(t => t.Name),
                types["test:index/TestResource"].Select(vt => vt.Item2).OrderBy(t => t.Name));
        }

        [ResourceType("test:index/TestResource", "2.0.2")]
        private class Version202TestResource : CustomResource
        {
            public Version202TestResource(string type, string name, ResourceArgs? args, CustomResourceOptions? options = null) : base(type, name, args, options)
            {
            }
        }

        [ResourceType("test:index/TestResource", null)]
        private class WildcardTestResource : CustomResource
        {
            public WildcardTestResource(string type, string name, ResourceArgs? args, CustomResourceOptions? options = null) : base(type, name, args, options)
            {
            }
        }

        [ResourceType("test:index/UnrelatedResource", "1.0.3")]
        private class OtherResource : CustomResource
        {
            public OtherResource(string type, string name, ResourceArgs? args, CustomResourceOptions? options = null) : base(type, name, args, options)
            {
            }
        }
    }
}
