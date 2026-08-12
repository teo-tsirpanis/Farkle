// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// We define the polyfill ourselves instead of getting it from PolySharp.
// The production factory generator is being used near method calls with
// custom priority, but it cannot see the source code that PolySharp generates,
// which leads to weird errors on frameworks without inbox ORPA. This affects
// only uses of production factories within the Farkle project itself.

#if !NET9_0_OR_GREATER
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Specifies the priority of a member in overload resolution. When unspecified, the default priority is 0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property)]
    [ExcludeFromCodeCoverage]
    [Embedded]
    internal sealed class OverloadResolutionPriorityAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OverloadResolutionPriorityAttribute"/> class.
        /// </summary>
        /// <param name="priority">The priority of the attributed member. Higher numbers are prioritized, lower numbers are deprioritized. 0 is the default if no attribute is present.</param>
        public OverloadResolutionPriorityAttribute(int priority)
        {
            Priority = priority;
        }

        /// <summary>
        /// The priority of the member.
        /// </summary>
        public int Priority { get; }
    }
}
#endif
