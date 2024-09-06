using System.Collections.Generic;
using Pulumi.Experimental.Provider;
using Xunit;

namespace Pulumi.Tests.Provider;

public class DependenciesValidationVisitor : PropertyValueVisitorBase
{
    private readonly ISet<Experimental.Provider.Urn> expectedDependencies;

    public DependenciesValidationVisitor(ISet<Pulumi.Experimental.Provider.Urn> expectedDependencies)
    {
        this.expectedDependencies = expectedDependencies;
    }

    protected override void VisitOutput(PropertyValue propertyValue, OutputReference output)
    {
        Assert.Equal(expectedDependencies, output.Dependencies);
        base.VisitOutput(propertyValue, output);
    }
}
