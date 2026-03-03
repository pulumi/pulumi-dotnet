// Copyright 2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Pulumi
{
    /// <summary>
    /// gRPC client interceptor that propagates W3C trace context (traceparent)
    /// in outgoing call metadata so the engine can parent its spans correctly.
    /// </summary>
    internal class TracingInterceptor : Interceptor
    {
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                var metadata = context.Options.Headers ?? new Metadata();
                metadata.Add("traceparent",
                    $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}");

                var options = context.Options.WithHeaders(metadata);
                context = new ClientInterceptorContext<TRequest, TResponse>(
                    context.Method, context.Host, options);
            }

            return continuation(request, context);
        }
    }

    internal static class Instrumentation
    {
        internal static readonly ActivitySource ActivitySource = new("pulumi-sdk-dotnet");
        private static TracerProvider? _tracerProvider;
        private static Activity? _rootActivity;

        /// <summary>
        /// Initialize OpenTelemetry tracing if TRACEPARENT is set.
        /// This sets up a TracerProvider with OTLP exporter.
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
                .AddSource("pulumi-sdk-dotnet");

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
                _rootActivity = ActivitySource.StartActivity(
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
