using Xunit;
using Pulumi.Experimental.Provider;

namespace Pulumi.Tests.Provider
{
    public class ComponentProviderHostTests
    {

        [Fact]
        public void ParseAssemblyName_NullInput_ReturnsNullValues()
        {
            var (namespaceName, packageName) = ComponentProviderHost.ParseAssemblyName(null);
            Assert.Null(namespaceName);
            Assert.Null(packageName);
        }

        [Fact]
        public void ParseAssemblyName_SinglePart_ReturnsNullNamespaceAndKebabCasePackage()
        {
            var (namespaceName, packageName) = ComponentProviderHost.ParseAssemblyName("MyPackage");
            Assert.Null(namespaceName);
            Assert.Equal("my-package", packageName);
        }

        [Fact]
        public void ParseAssemblyName_MultiPart_ReturnsKebabCaseNamespaceAndPackage()
        {
            var (namespaceName, packageName) = ComponentProviderHost.ParseAssemblyName("MyNamespace.MyPackage");
            Assert.Equal("my-namespace", namespaceName);
            Assert.Equal("my-package", packageName);
        }

        [Fact]
        public void ParseAssemblyName_ComplexName_HandlesMultipleParts()
        {
            var (namespaceName, packageName) = ComponentProviderHost.ParseAssemblyName("MyCompany.MyProduct.MyPackage");
            Assert.Equal("my-company", namespaceName);
            Assert.Equal("my-package", packageName);
        }
    }
}
