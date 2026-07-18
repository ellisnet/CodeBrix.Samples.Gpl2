//
// Copyright (c) 2022 James Randall
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Vendored for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), files CSharpWolfenstein/Engine/RayCasting/
// AbstractRayCaster.cs and RayCaster.cs; adapted to the RenderWorld
// view over the translated game logic.
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

namespace Wolfenstein.Brix.GameEngine.Rendering;

/// <summary>The outcome of one ray cast.</summary>
public struct RayCastResult
{
    /// <summary>True when the ray stopped on a hit (rather than leaving the map).</summary>
    public bool IsHit;

    /// <summary>The per-axis distance the ray travels between grid lines.</summary>
    public (double X, double Y) DeltaDistance;

    /// <summary>The accumulated per-axis distances to the next grid lines.</summary>
    public (double X, double Y) TotalSideDistance;

    /// <summary>The tile the ray stopped in.</summary>
    public (int X, int Y) MapHit;

    /// <summary>Which axis of the tile was crossed.</summary>
    public Side Side;
}

/// <summary>
/// The DDA grid walker: steps a ray from the camera through the map
/// until it hits a wall, a closed-enough door, or leaves the grid.
/// </summary>
public static class RayCaster
{
    /// <summary>Casts one ray from the camera along a direction.</summary>
    public static RayCastResult Cast(RenderWorld world, double fromX, double fromY, double directionX, double directionY)
    {
        var initialMapX = (int)fromX;
        var initialMapY = (int)fromY;
        var deltaDistX = directionX == 0.0 ? double.MaxValue : Math.Abs(1.0 / directionX);
        var deltaDistY = directionY == 0.0 ? double.MaxValue : Math.Abs(1.0 / directionY);
        var (stepX, initialSideDistX) =
            directionX < 0.0
                ? (-1, (fromX - initialMapX) * deltaDistX)
                : (1, (initialMapX + 1.0 - fromX) * deltaDistX);
        var (stepY, initialSideDistY) =
            directionY < 0.0
                ? (-1, (fromY - initialMapY) * deltaDistY)
                : (1, (initialMapY + 1.0 - fromY) * deltaDistY);

        var result = new RayCastResult
        {
            IsHit = false,
            DeltaDistance = (deltaDistX, deltaDistY),
            TotalSideDistance = (initialSideDistX, initialSideDistY),
            MapHit = (initialMapX, initialMapY),
            Side = initialSideDistX < initialSideDistY ? Side.NorthSouth : Side.EastWest,
        };

        while (!result.IsHit && world.InMap(result.MapHit.X, result.MapHit.Y))
        {
            int newMapX, newMapY;
            Side newSide;
            if (result.TotalSideDistance.X < result.TotalSideDistance.Y)
            {
                newMapX = result.MapHit.X + stepX;
                newMapY = result.MapHit.Y;
                newSide = Side.NorthSouth;
                result.TotalSideDistance = (result.TotalSideDistance.X + deltaDistX, result.TotalSideDistance.Y);
            }
            else
            {
                newMapX = result.MapHit.X;
                newMapY = result.MapHit.Y + stepY;
                newSide = Side.EastWest;
                result.TotalSideDistance = (result.TotalSideDistance.X, result.TotalSideDistance.Y + deltaDistY);
            }

            result.MapHit = (newMapX, newMapY);
            result.Side = newSide;
            if (!world.InMap(newMapX, newMapY))
            {
                break;
            }

            result.IsHit = world.KindAt(newMapX, newMapY) switch
            {
                RenderCellKind.Wall => true,
                RenderCellKind.DoorCell => IsDoorHit(
                    world, (stepX, stepY), fromX, fromY, directionX, directionY,
                    (newMapX, newMapY), newSide),
                _ => false,
            };
        }

        return result;
    }

    /// <summary>
    /// Decides whether a ray crossing a door tile hits the recessed,
    /// possibly partly-open door slab at the tile's midline.
    /// </summary>
    private static bool IsDoorHit(
        RenderWorld world,
        (int X, int Y) stepDelta,
        double posX,
        double posY,
        double directionX,
        double directionY,
        (int X, int Y) newMap,
        Side newSide)
    {
        var doorOffset = world.DoorOffset(newMap.X, newMap.Y);
        var halfStepDeltaX =
            directionX == 0.0
                ? double.MaxValue
                : Math.Sqrt(1.0 + directionY * directionY / directionX * directionX);
        var halfStepDeltaY =
            directionY == 0.0
                ? double.MaxValue
                : Math.Sqrt(1.0 + directionX * directionX / directionY * directionY);

        const double tolerance = 0.0001;
        var mapX2 = posX < newMap.X ? newMap.X - 1 : newMap.X;
        var mapY2 = posY > newMap.Y ? newMap.Y + 1 : newMap.Y;
        var adjacent = newSide == Side.EastWest ? mapY2 - posY : mapX2 - posX + 1.0;
        var rayMultiplier = newSide == Side.EastWest
            ? adjacent / directionY
            : adjacent / directionX;
        var (rayPositionX, rayPositionY) =
            (posX + directionX * rayMultiplier, posY + directionY * rayMultiplier);
        var trueDeltaX = halfStepDeltaX < tolerance ? 100.0 : halfStepDeltaX;
        var trueDeltaY = halfStepDeltaY < tolerance ? 100.0 : halfStepDeltaY;

        if (newSide == Side.NorthSouth)
        {
            var trueYStep = Math.Sqrt(trueDeltaX * trueDeltaX - 1.0);
            var halfStepInY = rayPositionY + (stepDelta.Y * trueYStep) / 2.0;
            return Math.Abs(Math.Floor(halfStepInY) - newMap.Y) < tolerance &&
                (halfStepInY - newMap.Y) < (1.0 - doorOffset / 64.0);
        }

        var trueXStep = Math.Sqrt(trueDeltaY * trueDeltaY - 1.0);
        var halfStepInX = rayPositionX + (stepDelta.X * trueXStep) / 2.0;
        return Math.Abs(Math.Floor(halfStepInX) - newMap.X) < tolerance &&
            (halfStepInX - newMap.X) < (1.0 - doorOffset / 64.0);
    }
}
