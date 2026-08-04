//
// Copyright (c) 2022 James Randall
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Vendored for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Game/Model.cs (Side).
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//

namespace Wolfenstein.Brix.GameEngine.Rendering;

/// <summary>Which axis of a tile a ray hit.</summary>
public enum Side
{
    /// <summary>The ray crossed a vertical (x) grid line.</summary>
    NorthSouth,

    /// <summary>The ray crossed a horizontal (y) grid line.</summary>
    EastWest,
}
