//
// Copyright (c) 2022 James Randall
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The level-header layout and plane decoding are vendored for
// Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Assets/Level.cs, cross-checked
// against the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_map.c.
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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// One decoded level from GAMEMAPS: the three 64x64 tile planes
/// (walls/doors, objects, extra) fully Carmack- and RLEW-expanded,
/// plus the level's name from its TED5 header.
/// </summary>
public sealed class MapData
{
    internal MapData(string name, int width, int height, ushort[] plane0, ushort[] plane1, ushort[] plane2)
    {
        Name = name;
        Width = width;
        Height = height;
        Plane0 = plane0;
        Plane1 = plane1;
        Plane2 = plane2;
    }

    /// <summary>The level name from the TED5 header, e.g. "Wolf1 Map1".</summary>
    public string Name { get; }

    /// <summary>The map width in tiles (64 for every retail level).</summary>
    public int Width { get; }

    /// <summary>The map height in tiles (64 for every retail level).</summary>
    public int Height { get; }

    /// <summary>The wall/door plane, row-major (index y * Width + x).</summary>
    public ushort[] Plane0 { get; }

    /// <summary>The object plane (start positions, enemies, statics), row-major.</summary>
    public ushort[] Plane1 { get; }

    /// <summary>The third plane (unused by the game logic), row-major.</summary>
    public ushort[] Plane2 { get; }

    /// <summary>Returns the wall-plane value at a map position.</summary>
    public ushort GetPlane0(int x, int y) => Plane0[y * Width + x];

    /// <summary>Returns the object-plane value at a map position.</summary>
    public ushort GetPlane1(int x, int y) => Plane1[y * Width + x];
}

/// <summary>
/// The decoded contents of a MAPHEAD/GAMEMAPS file pair: every level's
/// tile planes, expanded and ready for level construction.
/// </summary>
public sealed class GameMapsFile
{
    private const int MaxMaps = 100;
    private const int LevelHeaderSize = 38;

    private GameMapsFile(ushort rlewTag, MapData[] maps)
    {
        RlewTag = rlewTag;
        Maps = maps;
    }

    /// <summary>The RLEW run marker word from MAPHEAD (0xABCD for the retail files).</summary>
    public ushort RlewTag { get; }

    /// <summary>The decoded levels, in file order (episode 1 maps 1-10 for shareware).</summary>
    public MapData[] Maps { get; }

    /// <summary>Loads and fully decodes a MAPHEAD/GAMEMAPS pair.</summary>
    public static GameMapsFile Load(string mapHeadPath, string gameMapsPath)
    {
        var mapHead = File.ReadAllBytes(mapHeadPath);
        var gameMaps = File.ReadAllBytes(gameMapsPath);
        if (mapHead.Length < 2 + MaxMaps * 4)
        {
            throw new InvalidWolfDataException("MAPHEAD file is too short to hold its level pointers.");
        }

        var rlewTag = BitConverter.ToUInt16(mapHead, 0);
        var maps = new List<MapData>();
        for (var i = 0; i < MaxMaps; i++)
        {
            var headerOffset = BitConverter.ToInt32(mapHead, 2 + i * 4);
            if (headerOffset <= 0)
            {
                continue;
            }

            maps.Add(DecodeLevel(gameMaps, headerOffset, rlewTag));
        }

        if (maps.Count == 0)
        {
            throw new InvalidWolfDataException("MAPHEAD file references no levels.");
        }

        return new GameMapsFile(rlewTag, maps.ToArray());
    }

    private static MapData DecodeLevel(byte[] gameMaps, int headerOffset, ushort rlewTag)
    {
        if (headerOffset + LevelHeaderSize > gameMaps.Length)
        {
            throw new InvalidWolfDataException("GAMEMAPS level header extends past the end of the file.");
        }

        var planeStarts = new int[3];
        var planeLengths = new int[3];
        for (var p = 0; p < 3; p++)
        {
            planeStarts[p] = BitConverter.ToInt32(gameMaps, headerOffset + p * 4);
            planeLengths[p] = BitConverter.ToUInt16(gameMaps, headerOffset + 12 + p * 2);
        }

        var width = BitConverter.ToUInt16(gameMaps, headerOffset + 18);
        var height = BitConverter.ToUInt16(gameMaps, headerOffset + 20);
        var name = Encoding.ASCII.GetString(gameMaps, headerOffset + 22, 16);
        var nul = name.IndexOf('\0');
        if (nul >= 0)
        {
            name = name.Substring(0, nul);
        }

        var planes = new ushort[3][];
        for (var p = 0; p < 3; p++)
        {
            planes[p] = DecodePlane(gameMaps, planeStarts[p], planeLengths[p], rlewTag, width * height);
        }

        return new MapData(name.Trim(), width, height, planes[0], planes[1], planes[2]);
    }

    /// <summary>
    /// Expands one map plane: the stored data is Carmack-compressed,
    /// and the result of that expansion is RLEW-compressed words.
    /// </summary>
    private static ushort[] DecodePlane(byte[] gameMaps, int start, int length, ushort rlewTag, int tileCount)
    {
        if (start < 0 || length <= 0 || start + length > gameMaps.Length)
        {
            throw new InvalidWolfDataException("GAMEMAPS plane data extends past the end of the file.");
        }

        var compressed = new byte[length];
        Array.Copy(gameMaps, start, compressed, 0, length);
        var expanded = Compression.RlewDecode(Compression.CarmackDecode(compressed), rlewTag);

        var words = new ushort[tileCount];
        var available = Math.Min(tileCount, expanded.Length / 2);
        for (var i = 0; i < available; i++)
        {
            words[i] = BitConverter.ToUInt16(expanded, i * 2);
        }

        return words;
    }
}
