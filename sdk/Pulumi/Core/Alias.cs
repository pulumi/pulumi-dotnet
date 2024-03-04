// Copyright 2016-2019, Pulumi Corporation

using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// Alias is a description of prior named used for a resource. It can be processed in the
    /// context of a resource creation to determine what the full aliased URN would be.
    /// <para/>
    /// Use <see cref="Urn"/> in the case where a prior URN is known and can just be specified in
    /// full.  Otherwise, provide some subset of the other properties in this type to generate an
    /// appropriate urn from the pre-existing values of the <see cref="Resource"/> with certain
    /// parts overridden.
    /// <para/>
    /// The presence of a property indicates if its value should be used. If absent (i.e.
    /// <see langword="null"/>), then the value is not used.
    /// <para/>
    /// Note: because of the above, there needs to be special handling to indicate that the previous
    /// <see cref="Parent"/> of a <see cref="Resource"/> was <see langword="null"/>.  Specifically,
    /// pass in:
    /// <para/>
    /// <c>Aliases = { new Alias { NoParent = true } }</c>
    /// </summary>
    public sealed class Alias
    {
        /// <summary>
        /// The previous urn to alias to.  If this is provided, no other properties in this type
        /// should be provided.
        /// </summary>
        public string? Urn { get; set; }

        /// <summary>
        /// The previous name of the resource.  If <see langword="null"/>, the current name of the
        /// resource is used.
        /// </summary>
        public Input<string>? Name { get; set; }

        /// <summary>
        /// The previous type of the resource.  If <see langword="null"/>, the current type of the
        /// resource is used.
        /// </summary>
        public Input<string>? Type { get; set; }

        /// <summary>
        /// The previous stack of the resource.  If <see langword="null"/>, defaults to the value of
        /// <see cref="IDeployment.StackName"/>.
        /// </summary>
        public Input<string>? Stack { get; set; }

        /// <summary>
        /// The previous project of the resource. If <see langword="null"/>, defaults to the value
        /// of <see cref="IDeployment.ProjectName"/>.
        /// </summary>
        public Input<string>? Project { get; set; }

        /// <summary>
        /// The previous parent of the resource. If <see langword="null"/>, the current parent of
        /// the resource is used.
        /// <para/>
        /// To specify no original parent, use <c>new Alias { NoParent = true }</c>.
        /// <para/>
        /// Only specify one of <see cref="Parent"/> or <see cref="ParentUrn"/> or <see cref="NoParent"/>.
        /// </summary>
        public Resource? Parent { get; set; }

        /// <summary>
        /// The previous parent of the resource. If <see langword="null"/>, the current parent of
        /// the resource is used.
        /// <para/>
        /// To specify no original parent, use <c>new Alias { NoParent = true }</c>.
        /// <para/>
        /// Only specify one of <see cref="Parent"/> or <see cref="ParentUrn"/> or <see cref="NoParent"/>.
        /// </summary>
        public Input<string>? ParentUrn { get; set; }

        /// <summary>
        /// Used to indicate the resource previously had no parent.  If <see langword="false"/> this
        /// property is ignored.
        /// <para/>
        /// To specify no original parent, use <c>new Alias { NoParent = true }</c>.
        /// <para/>
        /// Only specify one of <see cref="Parent"/> or <see cref="ParentUrn"/> or <see cref="NoParent"/>.
        /// </summary>
        public bool NoParent { get; set; }

        /// <summary>
        /// Deserialize a wire protocol alias to an alias object.
        /// </summary>
        internal static Alias Deserialize(Pulumirpc.Alias alias)
        {
            if (alias.AliasCase == Pulumirpc.Alias.AliasOneofCase.Urn)
            {
                return new Alias
                {
                    Urn = alias.Urn,
                };
            }

            var spec = alias.Spec;
            return new Alias
            {
                Name = spec.Name == "" ? null : (Input<string>)spec.Name,
                Type = spec.Type == "" ? null : (Input<string>)spec.Type,
                Stack = spec.Stack == "" ? null : (Input<string>)spec.Stack,
                Project = spec.Project == "" ? null : (Input<string>)spec.Project,
                Parent = spec.ParentUrn == "" ? null : new DependencyResource(spec.ParentUrn),
                ParentUrn = spec.ParentUrn == "" ? null : (Input<string>)spec.ParentUrn,
                NoParent = spec.NoParent,
            };
        }

        static async Task<T> Resolve<T>(Input<T>? input, T whenUnknown)
        {
            return input == null
                ? whenUnknown
                : await input.ToOutput().GetValueAsync(whenUnknown).ConfigureAwait(false);
        }

        internal async Task<Pulumirpc.Alias> SerializeAsync()
        {
            if (Urn != null)
            {
                // Alias URN fully provided, use it as is
                return new Pulumirpc.Alias
                {
                    Urn = Urn,
                };
            }

            var aliasSpec = new Pulumirpc.Alias.Types.Spec
            {
                Name = await Resolve(Name, ""),
                Type = await Resolve(Type, ""),
                Stack = await Resolve(Stack, ""),
                Project = await Resolve(Project, ""),
            };

            // Here we specify whether the alias has a parent or not.
            // aliasSpec must only specify one of NoParent or ParentUrn, not both!
            // this determines the wire format of the alias which is used by the engine.
            if (Parent == null && ParentUrn == null)
            {
                aliasSpec.NoParent = NoParent;
            }
            else if (Parent != null)
            {
                var aliasParentUrn = await Resolve(Parent.Urn, "");
                if (!string.IsNullOrEmpty(aliasParentUrn))
                {
                    aliasSpec.ParentUrn = aliasParentUrn;
                }
            }
            else
            {
                var aliasParentUrn = await Resolve(ParentUrn, "");
                if (!string.IsNullOrEmpty(aliasParentUrn))
                {
                    aliasSpec.ParentUrn = aliasParentUrn;
                }
            }

            return new Pulumirpc.Alias
            {
                Spec = aliasSpec,
            };
        }
    }
}
