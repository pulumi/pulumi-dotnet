using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevSpells.WebApi.Testing;

public sealed class DelegatedLoggerProvider : ILoggerProvider
{
    private readonly NullLoggerProvider fallback = NullLoggerProvider.Instance;
    private readonly Func<ILoggerProvider?> providerAccess1;

    public DelegatedLoggerProvider(Func<ILoggerProvider?> providerAccess)
    {
        providerAccess1 = providerAccess;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return (providerAccess1() ?? fallback).CreateLogger(categoryName);
    }

    public void Dispose()
    {
        fallback.Dispose();
    }
}
