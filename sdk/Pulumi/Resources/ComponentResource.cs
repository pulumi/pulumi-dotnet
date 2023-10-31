// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// A <see cref="Resource"/> that aggregates one or more other child resources into a higher
    /// level abstraction. The component resource itself is a resource, but does not require custom
    /// CRUD operations for provisioning.
    /// </summary>
    public class ComponentResource : Resource
    {
        internal readonly bool remote;

        /// <summary>
        /// Creates and registers a new component resource.  <paramref name="type"/> is the fully
        /// qualified type token and <paramref name="name"/> is the "name" part to use in creating a
        /// stable and globally unique URN for the object. <c>options.parent</c> is the optional parent
        /// for this component, and [options.dependsOn] is an optional list of other resources that
        /// this resource depends on, controlling the order in which we perform resource operations.
        /// </summary>
        /// <param name="type">The type of the resource.</param>
        /// <param name="name">The unique name of the resource.</param>
        /// <param name="options">A bag of options that control this resource's behavior.</param>
        public ComponentResource(string type, string name, ComponentResourceOptions? options = null)
            : this(type, name, ResourceArgs.Empty, options)
        {
        }

        /// <summary>
        /// Creates and registers a new component resource.  <paramref name="type"/> is the fully
        /// qualified type token and <paramref name="name"/> is the "name" part to use in creating a
        /// stable and globally unique URN for the object. <c>options.parent</c> is the optional parent
        /// for this component, and [options.dependsOn] is an optional list of other resources that
        /// this resource depends on, controlling the order in which we perform resource operations.
        /// </summary>
        /// <param name="type">The type of the resource.</param>
        /// <param name="name">The unique name of the resource.</param>
        /// <param name="args">The arguments to use to populate the new resource.</param>
        /// <param name="options">A bag of options that control this resource's behavior.</param>
        /// <param name="remote">True if this is a remote component resource.</param>
#pragma warning disable RS0022 // Constructor make noninheritable base class inheritable
        public ComponentResource(
            string type, string name, ResourceArgs? args, ComponentResourceOptions? options = null, bool remote = false)
            : base(type, name, custom: false, args ?? ResourceArgs.Empty, options ?? new ComponentResourceOptions(), remote)
#pragma warning restore RS0022 // Constructor make noninheritable base class inheritable
        {
            this.remote = remote;
        }

        internal static Dictionary<string, object?> CollectOutputProperties(ComponentResource instance)
        {
            var outputs = new Dictionary<string, object?>();
            var currentType = instance.GetType();
            foreach (var prop in currentType.GetProperties())
            {
                if (prop.Name == nameof(Urn))
                {
                    continue;
                }

                var outputAttribute =
                    prop.GetCustomAttributes(typeof(OutputAttribute), false)
                        .FirstOrDefault();
                if (outputAttribute is OutputAttribute attr && attr.Name != null)
                {
                    // when using [Output("<name>")] we will export the value of this property
                    // named as the provided <name>
                    var value = prop.GetValue(instance);
                    outputs.Add(attr.Name, value);
                }
                else if (outputAttribute is OutputAttribute)
                {
                    // otherwise of we only have [Output] we will simply use the name of the property itself
                    // when exporting the value
                    var value = prop.GetValue(instance);
                    outputs.Add(prop.Name, value);
                }
                else
                {
                    var propertyType = prop.PropertyType;
                    // only if the type is Output<T> then we will consider it an output property
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Output<>))
                    {
                        var value = prop.GetValue(instance);
                        outputs.Add(prop.Name, value);
                    }
                }
            }

            return outputs;
        }

        /// <summary>
        /// RegisterOutputs registers synthetic outputs that a component has initialized, usually by
        /// allocating other child sub-resources and propagating their resulting property values.
        /// ComponentResources should always call this at the end of their constructor to indicate
        /// that they are done creating child resources.  While not strictly necessary, this helps
        /// the experience by ensuring the UI transitions the ComponentResource to the 'complete'
        /// state as quickly as possible (instead of waiting until the entire application completes).
        /// </summary>
        protected void RegisterOutputs()
        {
            var outputs = CollectOutputProperties(this);
            RegisterOutputs(outputs);
        }

        protected void RegisterOutputs(IDictionary<string, object?> outputs)
            => RegisterOutputs(Task.FromResult(outputs ?? throw new ArgumentNullException(nameof(outputs))));

        protected void RegisterOutputs(Task<IDictionary<string, object?>> outputs)
            => RegisterOutputs(Output.Create(outputs ?? throw new ArgumentNullException(nameof(outputs))));

        protected void RegisterOutputs(Output<IDictionary<string, object?>> outputs)
            => Deployment.InternalInstance.RegisterResourceOutputs(this, outputs ?? throw new ArgumentNullException(nameof(outputs)));
    }
}
