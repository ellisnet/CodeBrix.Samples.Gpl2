//
// PropertyChangedEventArgs.cs
//
// Copyright (c) 2026 Jeremy Ellis and contributors
//     (adapted from CodeBrix.Develop for Wolfenstein.Brix; inspired by
//      MonoDevelop.Core.PropertyChangedEventArgs, simplified)
// SPDX-License-Identifier: MIT
//

using System;

namespace Wolfenstein.Brix.Settings; //was previously: CodeBrix.Develop.Core.Options

/// <summary>
/// Event arguments describing a change to a single stored setting value.
/// </summary>
public class PropertyChangedEventArgs : EventArgs
{
    /// <summary>The key of the setting that changed.</summary>
    public string Key { get; }

    /// <summary>The previous value, or null when the setting was not set before.</summary>
    public object OldValue { get; }

    /// <summary>The new value, or null when the setting was removed.</summary>
    public object NewValue { get; }

    /// <summary>Creates event arguments for a changed setting.</summary>
    public PropertyChangedEventArgs(string key, object oldValue, object newValue)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
