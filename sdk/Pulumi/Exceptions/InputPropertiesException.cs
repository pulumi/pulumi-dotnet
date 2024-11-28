// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Collections.Generic;
using Grpc.Core;
using Pulumirpc;

namespace Pulumi
{
	public class PropertyError {
		public string PropertyPath { get; set; }
		public string Reason { get; set; }

		public PropertyError(string propertyPath, string reason) {
			PropertyPath = propertyPath;
			Reason = reason;
		}
	}

	public class InputPropertiesException : RpcException
	{
		public InputPropertiesException(String message, IList<PropertyError> propertyErrors)
			: base(new Grpc.Core.Status(StatusCode.InvalidArgument, ""), constructTrailers(message, propertyErrors), message)		{
		}

		public static Metadata constructTrailers(String message, IList<PropertyError> propertyErrors)
		{
			var errorDetails = new InputPropertiesError();
			foreach (var propertyError in propertyErrors)
			{
				var error = new InputPropertiesError.Types.PropertyError();
				error.PropertyPath = propertyError.PropertyPath;
				error.Reason = propertyError.Reason;
				errorDetails.Errors.Add(error);
			}
			var status = new Google.Rpc.Status {
				Code = (int)StatusCode.Unknown,
				Message = "Bad request",
				Details = { Google.Protobuf.WellKnownTypes.Any.Pack(errorDetails) }
			};

//			throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, "wtf"), "failing for a reason");

			// var details = Google.Protobuf.WellKnownTypes.Any.Pack(status);
			var metadata = new Metadata();
			metadata.Add("grpc-status-details-bin", Google.Protobuf.MessageExtensions.ToByteArray(status));
			Console.WriteLine("metadata: " + metadata.GetAll("grpc-status-details-bin"));
			return metadata;
        }
	}
}
