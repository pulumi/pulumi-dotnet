// Copyright 2016-2019, Pulumi Corporation

using System;

namespace Pulumi
{
    /// <summary>
    /// UndeferrableValueException is raised when accessing a PolicyResource property that has an unknown value.
    /// </summary>
    public class UndeferrableValueException : Exception
    {
        public UndeferrableValueException(string message) : base(message)
        {
        }
    }
}
