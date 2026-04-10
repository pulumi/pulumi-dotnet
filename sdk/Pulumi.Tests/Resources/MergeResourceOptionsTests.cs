// Copyright 2016-2022, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Collections.Generic;

namespace Pulumi.Tests.Resources
{
    public class MergeResourceOptionsTests
    {
        // Regression test for https://github.com/pulumi/pulumi-dotnet/issues/957
        // ResourceTransforms must survive CustomResourceOptions.Merge (used by all generated SDK resources
        // via their MakeResourceOptions pattern).

        [Fact]
        public void MergeCustom_ResourceTransforms_FromOptions2_ArePropagated()
        {
            ResourceTransform transform = (_, _) => Task.FromResult<ResourceTransformResult?>(null);

            var o1 = new CustomResourceOptions();
            var o2 = new CustomResourceOptions { ResourceTransforms = { transform } };

            var result = CustomResourceOptions.Merge(o1, o2);

            Assert.Single(result.ResourceTransforms);
            Assert.Contains(transform, result.ResourceTransforms);
        }

        [Fact]
        public void MergeCustom_ResourceTransforms_FromBothOptions_AreCombined()
        {
            ResourceTransform transform1 = (_, _) => Task.FromResult<ResourceTransformResult?>(null);
            ResourceTransform transform2 = (_, _) => Task.FromResult<ResourceTransformResult?>(null);

            var o1 = new CustomResourceOptions { ResourceTransforms = { transform1 } };
            var o2 = new CustomResourceOptions { ResourceTransforms = { transform2 } };

            var result = CustomResourceOptions.Merge(o1, o2);

            Assert.Equal(2, result.ResourceTransforms.Count);
            Assert.Contains(transform1, result.ResourceTransforms);
            Assert.Contains(transform2, result.ResourceTransforms);
        }

        [Fact]
        public void MergeCustom_ResourceTransforms_FromOptions1_ArePreservedWhenOptions2IsEmpty()
        {
            ResourceTransform transform = (_, _) => Task.FromResult<ResourceTransformResult?>(null);

            var o1 = new CustomResourceOptions { ResourceTransforms = { transform } };
            var o2 = new CustomResourceOptions();

            var result = CustomResourceOptions.Merge(o1, o2);

            Assert.Single(result.ResourceTransforms);
            Assert.Contains(transform, result.ResourceTransforms);
        }

        [Fact]
        public void MergeComponent_ResourceTransforms_FromOptions2_ArePropagated()
        {
            ResourceTransform transform = (_, _) => Task.FromResult<ResourceTransformResult?>(null);

            var o1 = new ComponentResourceOptions();
            var o2 = new ComponentResourceOptions { ResourceTransforms = { transform } };

            var result = ComponentResourceOptions.Merge(o1, o2);

            Assert.Single(result.ResourceTransforms);
            Assert.Contains(transform, result.ResourceTransforms);
        }

        [Fact]
        public void MergeComponent_ResourceTransforms_FromBothOptions_AreCombined()
        {
            ResourceTransform transform1 = (_, _) => Task.FromResult<ResourceTransformResult?>(null);
            ResourceTransform transform2 = (_, _) => Task.FromResult<ResourceTransformResult?>(null);

            var o1 = new ComponentResourceOptions { ResourceTransforms = { transform1 } };
            var o2 = new ComponentResourceOptions { ResourceTransforms = { transform2 } };

            var result = ComponentResourceOptions.Merge(o1, o2);

            Assert.Equal(2, result.ResourceTransforms.Count);
            Assert.Contains(transform1, result.ResourceTransforms);
            Assert.Contains(transform2, result.ResourceTransforms);
        }


        [Fact]
        public void MergeCustom()
        {
            var prov = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::default_4_13_0");
            var o1 = new CustomResourceOptions
            {
                Provider = prov,
            };
            var o2 = new CustomResourceOptions
            {
                Protect = true,
            };
            var result = CustomResourceOptions.Merge(o1, o2);
            Assert.Equal("aws", result.Provider!.Package);
            Assert.True(result.Protect);
        }

        [Fact]
        public void MergeComponent()
        {
            var awsDefault = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::default_4_13_0");
            var awsExplicit = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::explicit");
            var azureDefault = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:azure::default_4_13_0");

            var o1 = new ComponentResourceOptions
            {
                Providers = new List<ProviderResource> { awsDefault, azureDefault },
                Protect = true,
            };

            var o2 = new ComponentResourceOptions
            {
                Providers = new List<ProviderResource> { awsExplicit },
                Protect = false,
            };

            var result = ComponentResourceOptions.Merge(o1, o2);
            Assert.False(result.Protect);
            Assert.Equal(azureDefault, result.Providers[0]);
            Assert.Equal(awsExplicit, result.Providers[1]);
            Assert.Equal(2, result.Providers.Count);
        }

        [Fact]
        public void MergeComponentEmpty()
        {
            var awsDefault = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::default_4_13_0");
            var awsExplicit = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::explicit");
            var azureDefault = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:azure::default_4_13_0");

            var o1 = new ComponentResourceOptions
            {
                Providers = new List<ProviderResource> { awsDefault, azureDefault },
                Provider = awsExplicit,
            };
            Assert.Equal(o1.Providers, ComponentResourceOptions.Merge(o1, null).Providers);
        }

        [Fact]
        public void MergeComponentSingleton()
        {
            var aws = new DependencyProviderResource("urn:pulumi:stack::project::pulumi:providers:aws::default_4_13_0");
            var o1 = new ComponentResourceOptions
            {
                Providers = new List<ProviderResource> { aws },
            };
            var o2 = new ComponentResourceOptions
            {
                Protect = true,
            };

            var result = ComponentResourceOptions.Merge(o1, o2);
            Assert.True(result.Protect);
            Assert.Null(result.Provider);
            Assert.Equal(aws, result.Providers[0]);
        }
    }
}
