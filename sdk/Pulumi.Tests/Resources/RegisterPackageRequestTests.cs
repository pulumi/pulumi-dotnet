// Copyright 2016-2026, Pulumi Corporation

using System.Collections.Generic;
using Xunit;

namespace Pulumi.Tests.Resources
{
    public class RegisterPackageRequestTests
    {
        [Fact]
        public void KeepsPreExtensionConstructorForBinaryCompatibility()
        {
            // SDKs generated before the `extension` parameter existed bind to this exact
            // signature at runtime; if it disappears they fail with MissingMethodException.
            var ctor = typeof(RegisterPackageRequest).GetConstructor(new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(Dictionary<string, byte[]>),
                typeof(RegisterPackageRequest.PackageParameterization),
            });
            Assert.NotNull(ctor);
        }
    }
}
