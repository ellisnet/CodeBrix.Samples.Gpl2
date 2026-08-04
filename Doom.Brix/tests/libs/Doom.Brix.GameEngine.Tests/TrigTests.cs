using System;
using System.Collections.Generic;
using System.Linq;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

public class TrigTests
{
    [Fact]
    public void Tan()
    {
        for (var deg = 1; deg < 180; deg++)
        {
            var angle = Angle.FromDegree(deg);
            var fineAngle = (int)(angle.Data >> Trig.AngleToFineShift);

            var radian = 2 * Math.PI * (deg + 90) / 360;
            var expected = Math.Tan(radian);

            {
                var actual = Trig.Tan(angle).ToDouble();
                var delta = Math.Max(Math.Abs(expected) / 50, 1.0E-3);
                actual.ShouldBeApproximately(expected, delta);
            }

            {
                var actual = Trig.Tan(fineAngle).ToDouble();
                var delta = Math.Max(Math.Abs(expected) / 50, 1.0E-3);
                actual.ShouldBeApproximately(expected, delta);
            }
        }
    }

    [Fact]
    public void Sin()
    {
        for (var deg = -720; deg <= 720; deg++)
        {
            var angle = Angle.FromDegree(deg);
            var fineAngle = (int)(angle.Data >> Trig.AngleToFineShift);

            var radian = 2 * Math.PI * deg / 360;
            var expected = Math.Sin(radian);

            {
                var actual = Trig.Sin(angle).ToDouble();
                actual.ShouldBeApproximately(expected, 1.0E-3);
            }

            {
                var actual = Trig.Sin(fineAngle).ToDouble();
                actual.ShouldBeApproximately(expected, 1.0E-3);
            }
        }
    }

    [Fact]
    public void Cos()
    {
        for (var deg = -720; deg <= 720; deg++)
        {
            var angle = Angle.FromDegree(deg);
            var fineAngle = (int)(angle.Data >> Trig.AngleToFineShift);

            var radian = 2 * Math.PI * deg / 360;
            var expected = Math.Cos(radian);

            {
                var actual = Trig.Cos(angle).ToDouble();
                actual.ShouldBeApproximately(expected, 1.0E-3);
            }

            {
                var actual = Trig.Cos(fineAngle).ToDouble();
                actual.ShouldBeApproximately(expected, 1.0E-3);
            }
        }
    }
}
