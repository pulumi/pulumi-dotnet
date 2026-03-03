// Copyright 2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Pulumi
{
    internal static class Instrumentation
    {
        private static readonly ActivitySource _activitySource = new("pulumi-sdk-dotnet");
        private static TracerProvider? _tracerProvider;
        private static Activity? _rootActivity;

        /// <summary>
        /// Initialize OpenTelemetry tracing if TRACEPARENT is set.
        /// This sets up a TracerProvider with OTLP exporter and gRPC client instrumentation.
        /// </summary>
        internal static void Initialize()
        {
            var traceparent = Environment.GetEnvironmentVariable("TRACEPARENT");
            if (string.IsNullOrEmpty(traceparent))
            {
                return;
            }

            var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

            var builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("pulumi-sdk-dotnet"))
                .AddSource("pulumi-sdk-dotnet")
                .AddGrpcClientInstrumentation();

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri($"http://{otlpEndpoint}");
                    options.Protocol = OtlpExportProtocol.Grpc;
                });
            }

            _tracerProvider = builder.Build();

            Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

            var carrier = new[] { new KeyValuePair<string, string>("traceparent", traceparent) };
            var propagator = new TraceContextPropagator();
            var ctx = propagator.Extract(default, carrier, (c, key) =>
            {
                foreach (var kvp in c)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return new[] { kvp.Value };
                    }
                }
                return Array.Empty<string>();
            });

            var activityContext = ctx.ActivityContext;
            if (activityContext.IsValid())
            {
                _rootActivity = _activitySource.StartActivity(
                    "pulumi-sdk-dotnet",
                    ActivityKind.Internal,
                    parentContext: activityContext);
            }
        }

        /// <summary>
        /// Shutdown the tracer provider and flush any pending spans.
        /// </summary>
        internal static void Shutdown()
        {
            _rootActivity?.Stop();
            _rootActivity?.Dispose();
            _rootActivity = null;

            _tracerProvider?.Shutdown();
            _tracerProvider?.Dispose();
            _tracerProvider = null;
        }
    }
}
