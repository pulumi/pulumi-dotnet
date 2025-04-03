// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneOf.Types;
using Pulumi.Serialization;
using Pulumi.Testing;

namespace Pulumi.Tests.Mocks
{
    /// <summary>
    /// Supports testing that null returned for an InputMap doesn't cause a null reference exception.
    ///
    /// See https://github.com/pulumi/pulumi-dotnet/issues/456
    /// </summary>
    public sealed class Issue456
    {
        public class ReproStack : Stack
        {
            [Output("result")]
            public Output<string> Result { get; private set; }

            public ReproStack()
            {
                var instance1 = new CustomMap("instance1");
                var instance2 = new CustomMap("instance2");
                // Assert that both instance1 and instance2 don't have null for Metadata.Labels
               
                this.Result =  Output.Tuple(instance1.Metadata, instance2.Metadata).Apply(tuple => {
                    if (tuple.Item1.Labels == null || tuple.Item2.Labels == null) {
                        throw new Exception("Labels should not be null");
                    }
                    return "success";
                });
            }
        }

        public class ReproMocks : IMocks
        {
            public Task<object> CallAsync(MockCallArgs args)
            {
                throw new Exception("CallAsync should not be called");
            }

            public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
            {
                if (args.Type != "pkg:index:CustomMap")
                {
                    throw new Exception($"Unknown resource {args.Type}");
                }

                if (args.Name == "instance1") {
                    return Task.FromResult<(string?, object)>(
                        ("some_id",
                            new Dictionary<string, object>
                            {
                                { 
                                    "metadata",
                                    new Dictionary<string, object>
                                    {
                                        { "labels", null! },
                                    }
                                },
                            }
                        ));
                } else if (args.Name == "instance2") {
                    return Task.FromResult<(string?, object)>(
                        ("some_id",
                            new Dictionary<string, object>
                            {
                                { 
                                    "metadata",
                                    new Dictionary<string, object>
                                    {
                                    }
                                },
                            }
                        ));
                } else {
                    throw new Exception($"Unknown resource {args.Name}");
                }
            }
        }
    }
}
