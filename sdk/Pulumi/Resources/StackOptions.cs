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
        ///
        /// This property is experimental.
        /// </summary>
        public List<ResourceTransform> XResourceTransforms
        {
            get => _resourceTransforms ??= new List<ResourceTransform>();
            set => _resourceTransforms = value;
        }
    }
}
