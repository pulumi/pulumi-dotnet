// Copyright 2016-2026, Pulumi Corporation

using System;
using System.IO;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

namespace Pulumi.Serialization
{
    internal static class Protobuf
    {
        private const int MaxMessageSize = 400 * 1024 * 1024;
        private const int RecursionLimit = 10_000;

        public static T Parse<T>(ByteString payload)
            where T : IMessage, new()
        {
            using var input = CodedInputStream.CreateWithLimits(
                new MemoryStream(payload.ToByteArray()),
                MaxMessageSize,
                RecursionLimit);
            var message = new T();
            message.MergeFrom(input);
            return message;
        }

        public static Marshaller<T> CreateMarshaller<T>(Marshaller<T> marshaller)
            where T : class
        {
            if (!typeof(IMessage).IsAssignableFrom(typeof(T)))
            {
                return marshaller;
            }

            return Marshallers.Create(
                marshaller.ContextualSerializer,
                context => ParseMessage<T>(context.PayloadAsNewBuffer()));
        }

        public static CallInvoker CreateCallInvoker(GrpcChannel channel)
            => channel.CreateCallInvoker()
                .Intercept(new ProtobufRecursionLimitInterceptor())
                .Intercept(new TracingInterceptor());

        private static T ParseMessage<T>(byte[] payload)
            where T : class
        {
            using var input = CodedInputStream.CreateWithLimits(
                new MemoryStream(payload),
                MaxMessageSize,
                RecursionLimit);
            var message = (IMessage)Activator.CreateInstance<T>();
            message.MergeFrom(input);
            return (T)message;
        }
    }
}
