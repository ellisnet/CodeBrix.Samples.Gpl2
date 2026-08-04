//
// Copyright (c) 2026 Jeremy Ellis and contributors
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

using System;

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// Thrown when one of the Wolfenstein 3-D data files does not match the
/// expected on-disk structure. The application verifies file md5s before
/// handing a folder to the engine, so in practice this indicates either
/// a truncated download or a parser defect.
/// </summary>
public class InvalidWolfDataException : Exception
{
    /// <summary>Creates the exception with a description of the structural problem.</summary>
    public InvalidWolfDataException(string message) : base(message)
    {
    }
}
