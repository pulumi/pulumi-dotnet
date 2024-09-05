using System.Collections.Generic;
using System.Collections.Immutable;

namespace Pulumi.Experimental.Provider;

public class CheckResult
{
    public static readonly CheckResult Empty = new CheckResult();
    public bool IsValid => Failures.Count == 0;
    public IList<CheckFailure> Failures { get; set; }

    private CheckResult()
        : this(ImmutableList<CheckFailure>.Empty)
    {
    }

    public CheckResult(IList<CheckFailure> failures)
    {
        Failures = failures;
    }
}
