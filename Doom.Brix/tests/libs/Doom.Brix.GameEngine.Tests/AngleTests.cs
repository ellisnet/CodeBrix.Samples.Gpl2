using System;
using System.Collections.Generic;
using System.Linq;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

public class AngleTests
{
    private static readonly double delta = 1.0E-3;

    [Fact]
    public void ToRadian()
    {
        Angle.Ang0.ToRadian().ShouldBeApproximately(0.00 * Math.PI, delta);
        Angle.Ang45.ToRadian().ShouldBeApproximately(0.25 * Math.PI, delta);
        Angle.Ang90.ToRadian().ShouldBeApproximately(0.50 * Math.PI, delta);
        Angle.Ang180.ToRadian().ShouldBeApproximately(1.00 * Math.PI, delta);
        Angle.Ang270.ToRadian().ShouldBeApproximately(1.50 * Math.PI, delta);
    }

    [Fact]
    public void FromDegrees()
    {
        for (var deg = -720; deg <= 720; deg++)
        {
            var expectedSin = Math.Sin(2 * Math.PI * deg / 360);
            var expectedCos = Math.Cos(2 * Math.PI * deg / 360);

            var angle = Angle.FromDegree(deg);
            var actualSin = Math.Sin(angle.ToRadian());
            var actualCos = Math.Cos(angle.ToRadian());

            actualSin.ShouldBeApproximately(expectedSin, delta);
            actualCos.ShouldBeApproximately(expectedCos, delta);
        }
    }

    [Fact]
    public void FromRadianToDegrees()
    {
        0.ShouldBeApproximately(Angle.FromRadian(0.00 * Math.PI).ToDegree(), delta);
        45.ShouldBeApproximately(Angle.FromRadian(0.25 * Math.PI).ToDegree(), delta);
        90.ShouldBeApproximately(Angle.FromRadian(0.50 * Math.PI).ToDegree(), delta);
        180.ShouldBeApproximately(Angle.FromRadian(1.00 * Math.PI).ToDegree(), delta);
        270.ShouldBeApproximately(Angle.FromRadian(1.50 * Math.PI).ToDegree(), delta);
    }

    [Fact]
    public void Sign()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = random.Next(1440) - 720;
            var b = +a;
            var c = -a;

            var aa = Angle.FromDegree(a);
            var ab = +aa;
            var ac = -aa;

            {
                var expectedSin = Math.Sin(2 * Math.PI * b / 360);
                var expectedCos = Math.Cos(2 * Math.PI * b / 360);

                var actualSin = Math.Sin(ab.ToRadian());
                var actualCos = Math.Cos(ab.ToRadian());

                actualSin.ShouldBeApproximately(expectedSin, delta);
                actualCos.ShouldBeApproximately(expectedCos, delta);
            }

            {
                var expectedSin = Math.Sin(2 * Math.PI * c / 360);
                var expectedCos = Math.Cos(2 * Math.PI * c / 360);

                var actualSin = Math.Sin(ac.ToRadian());
                var actualCos = Math.Cos(ac.ToRadian());

                actualSin.ShouldBeApproximately(expectedSin, delta);
                actualCos.ShouldBeApproximately(expectedCos, delta);
            }
        }
    }

    [Fact]
    public void Abs()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = random.Next(120) - 60;
            var b = Math.Abs(a);

            var aa = Angle.FromDegree(a);
            var ab = Angle.Abs(aa);

            var expectedSin = Math.Sin(2 * Math.PI * b / 360);
            var expectedCos = Math.Cos(2 * Math.PI * b / 360);

            var actualSin = Math.Sin(ab.ToRadian());
            var actualCos = Math.Cos(ab.ToRadian());

            actualSin.ShouldBeApproximately(expectedSin, delta);
            actualCos.ShouldBeApproximately(expectedCos, delta);
        }
    }

    [Fact]
    public void Addition()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = random.Next(1440) - 720;
            var b = random.Next(1440) - 720;
            var c = a + b;

            var fa = Angle.FromDegree(a);
            var fb = Angle.FromDegree(b);
            var fc = fa + fb;

            var expectedSin = Math.Sin(2 * Math.PI * c / 360);
            var expectedCos = Math.Cos(2 * Math.PI * c / 360);

            var actualSin = Math.Sin(fc.ToRadian());
            var actualCos = Math.Cos(fc.ToRadian());

            actualSin.ShouldBeApproximately(expectedSin, delta);
            actualCos.ShouldBeApproximately(expectedCos, delta);
        }
    }

    [Fact]
    public void Subtraction()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = random.Next(1440) - 720;
            var b = random.Next(1440) - 720;
            var c = a - b;

            var fa = Angle.FromDegree(a);
            var fb = Angle.FromDegree(b);
            var fc = fa - fb;

            var expectedSin = Math.Sin(2 * Math.PI * c / 360);
            var expectedCos = Math.Cos(2 * Math.PI * c / 360);

            var actualSin = Math.Sin(fc.ToRadian());
            var actualCos = Math.Cos(fc.ToRadian());

            actualSin.ShouldBeApproximately(expectedSin, delta);
            actualCos.ShouldBeApproximately(expectedCos, delta);
        }
    }

    [Fact]
    public void Multiplication1()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = (uint)random.Next(30);
            var b = (uint)random.Next(12);
            var c = a * b;

            var fa = Angle.FromDegree(a);
            var fc = fa * b;

            fc.ToDegree().ShouldBeApproximately(c, delta);
        }
    }

    [Fact]
    public void Multiplication2()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = (uint)random.Next(30);
            var b = (uint)random.Next(12);
            var c = a * b;

            var fb = Angle.FromDegree(b);
            var fc = a * fb;

            fc.ToDegree().ShouldBeApproximately(c, delta);
        }
    }

    [Fact]
    public void Division()
    {
        var random = new Random(666);
        for (var i = 0; i < 100; i++)
        {
            var a = (double)random.Next(360);
            var b = (uint)(random.Next(30) + 1);
            var c = a / b;

            var fa = Angle.FromDegree(a);
            var fc = fa / b;

            fc.ToDegree().ShouldBeApproximately(c, delta);
        }
    }

    [Fact]
    public void Comparison()
    {
        var random = new Random(666);
        for (var i = 0; i < 10000; i++)
        {
            var a = random.Next(1140) - 720;
            var b = random.Next(1140) - 720;

            var fa = Angle.FromDegree(a);
            var fb = Angle.FromDegree(b);

            a = ((a % 360) + 360) % 360;
            b = ((b % 360) + 360) % 360;

            ((a == b) == (fa == fb)).Should().BeTrue();
            ((a != b) == (fa != fb)).Should().BeTrue();
            ((a < b) == (fa < fb)).Should().BeTrue();
            ((a > b) == (fa > fb)).Should().BeTrue();
            ((a <= b) == (fa <= fb)).Should().BeTrue();
            ((a >= b) == (fa >= fb)).Should().BeTrue();
        }
    }
}
