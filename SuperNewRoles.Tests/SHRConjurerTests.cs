using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRConjurerTests
{
    [Theory]
    [InlineData(3, 0f, 15f, 0f)]
    [InlineData(0, 2f, 15f, 13f)]
    [InlineData(0, 20f, 15f, 0f)]
    public void CompressionDoesNotRestartTheLastPlacementCooldown(int count, float now, float readyAt, float expected)
        => Assert.Equal(expected, SHRConjurerAbility.RemainingAt(count, now, readyAt));

    [Fact]
    public void ConjurerUsesShapeshifterControls()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Conjurer));
        Assert.Equal(RoleTypes.Shapeshifter, SHRRoleTypes.GetInitialRole(RoleId.Conjurer));
    }

    [Theory]
    [InlineData(0, 100f, 10f, true)]
    [InlineData(1, 9.9f, 10f, true)]
    [InlineData(1, 10f, 10f, false)]
    [InlineData(2, 10.1f, 10f, false)]
    [InlineData(2, 0f, 10f, true)]
    [InlineData(3, 0f, 10f, false)]
    public void PlacementKeepsTheNormalDistanceAndThreeMarkerLimit(int count, float distance, float limit, bool expected)
        => Assert.Equal(expected, SHRConjurerAbility.CanPlace(count, distance, limit));

    [Theory]
    [InlineData(1f, 1f, true)]
    [InlineData(3f, 3f, false)]
    [InlineData(-1f, 1f, false)]
    public void DamageUsesTheSharedTriangleForEitherWinding(float x, float y, bool expected)
    {
        var triangle = new[] { (0f, 0f), (4f, 0f), (0f, 4f) };
        Assert.Equal(expected, ConjurerAbility.PointInPolygon(x, y, triangle));
        System.Array.Reverse(triangle);
        Assert.Equal(expected, ConjurerAbility.PointInPolygon(x, y, triangle));
    }

    [Fact]
    public void DuplicateMarkersCannotCreateAnInfiniteDamageArea()
    {
        var triangle = new[] { (0f, 0f), (0f, 0f), (0f, 0f) };
        Assert.False(ConjurerAbility.PointInPolygon(1f, 1f, triangle));
    }
}
