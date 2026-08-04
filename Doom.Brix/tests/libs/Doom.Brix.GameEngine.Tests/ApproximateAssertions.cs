using System;
using SilverAssertions;

namespace Doom.Brix.GameEngine.Tests;

// Delta-tolerance equality for the ported managed-doom math tests
// (upstream used MSTest's Assert.AreEqual(expected, actual, delta)).
// When the tolerance is exceeded, the exact Be() assertion runs so the
// failure message shows the expected and actual values.
internal static class ApproximateAssertions
{
    public static void ShouldBeApproximately(this double actual, double expected, double delta)
    {
        if (double.IsNaN(actual) || Math.Abs(actual - expected) > delta)
        {
            actual.Should().Be(expected);
        }
    }

    public static void ShouldBeApproximately(this float actual, double expected, double delta) =>
        ((double)actual).ShouldBeApproximately(expected, delta);

    public static void ShouldBeApproximately(this int actual, double expected, double delta) =>
        ((double)actual).ShouldBeApproximately(expected, delta);
}
