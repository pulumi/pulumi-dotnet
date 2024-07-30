// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Collections.Generic;

namespace Pulumi
{
    /// <summary>
    /// <see cref="StackOptions"/> is a bag of optional settings that control a stack's behavior.
    /// </summary>
    public class StackOptions
    {
        private List<ResourceTransformation>? _resourceTransformations;

        /// <summary>
        /// Optional list of transformations to apply to this stack's resources during construction.
        /// The transformations are applied in order, and are applied after all the transformations of custom
        /// and component resources in the stack.
        /// </summary>
        public List<ResourceTransformation> ResourceTransformations
        {
            get => _resourceTransformations ??= new List<ResourceTransformation>();
            set => _resourceTransformations = value;
        }

        private List<ResourceTransform>? _resourceTransforms;

        /// <summary>
        /// Optional list of transforms to apply to this stack's resources during construction. The transforms are
        /// applied in order, and are applied after all the transforms of custom and component resources in the stack.
        /// </summary>
        public List<ResourceTransform> ResourceTransforms
        {
            get => _resourceTransforms ??= new List<ResourceTransform>();
            set => _resourceTransforms = value;
        }

        private List<InvokeTransform>? _invokeTransforms;

        /// <summary>
        /// Optional list of transforms to apply to this stack's invokes.
        /// </summary>
        public List<InvokeTransform> InvokeTransforms
        {
            get => _invokeTransforms ??= new List<InvokeTransform>();
            set => _invokeTransforms = value;
        }
    }
}
