using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulumi.Tests.Logging;

public sealed class DelegatedLoggerProvider : ILoggerProvider
{
    private readonly NullLoggerProvider fallback = NullLoggerProvider.Instance;
    private readonly Func<ILoggerProvider?> providerAccess;

    public DelegatedLoggerProvider(Func<ILoggerProvider?> providerAccess)
    {
        this.providerAccess = providerAccess;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return (providerAccess() ?? fallback).CreateLogger(categoryName);
    }

    public void Dispose()
    {
        fallback.Dispose();
    }
}
