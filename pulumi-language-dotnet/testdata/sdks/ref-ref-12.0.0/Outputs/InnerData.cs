// *** WARNING: this file was generated by pulumi-language-dotnet. ***
// *** Do not edit by hand unless you're certain you know what you are doing! ***

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi.Serialization;

namespace Pulumi.RefRef.Outputs
{

    [OutputType]
    public sealed class InnerData
    {
        public readonly ImmutableArray<bool> BoolArray;
        public readonly bool Boolean;
        public readonly double Float;
        public readonly int Integer;
        public readonly string String;
        public readonly ImmutableDictionary<string, string> StringMap;

        [OutputConstructor]
        private InnerData(
            ImmutableArray<bool> boolArray,

            bool boolean,

            double @float,

            int integer,

            string @string,

            ImmutableDictionary<string, string> stringMap)
        {
            BoolArray = boolArray;
            Boolean = boolean;
            Float = @float;
            Integer = integer;
            String = @string;
            StringMap = stringMap;
        }
    }
}