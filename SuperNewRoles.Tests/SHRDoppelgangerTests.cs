using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRDoppelgangerTests
{
    [Theory]
    [InlineData(true, true, 15f)]
    [InlineData(true, false, 45f)]
    [InlineData(false, true, 45f)]
    [InlineData(false, false, 45f)]
    public void SuccessRequiresAnActiveDisguiseAndItsTarget(bool shifted, bool sameTarget, float expected)
        => Assert.Equal(expected, DoppelgangerAbility.SelectCooldown(shifted, sameTarget, 15f, 45f));

    [Fact]
    public void SupportedRoleAppearsInSHRSettings()
        => Assert.True(SHRRoleTypes.ShouldShowInSettings(RoleId.Doppelganger, true));
}
