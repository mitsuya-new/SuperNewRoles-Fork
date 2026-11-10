using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRRocketTests
{
    [Fact]
    public void RocketUsesPhantomControls()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Rocket));
        Assert.Equal(RoleTypes.Phantom, SHRRoleTypes.GetInitialRole(RoleId.Rocket));
    }

    [Theory]
    [InlineData(0, 15f)]
    [InlineData(1, 5f)]
    [InlineData(14, 5f)]
    public void MultipleCapturesUseTheSubsequentCooldown(int count, float expected)
        => Assert.Equal(expected, RocketGrabAbility.CooldownForCount(count, 15f, 5f));

    [Fact]
    public void LaunchingAllCaptivesRestoresTheInitialGrabCooldown()
    {
        Assert.Equal(15f, RocketGrabAbility.CooldownForCount(0, 15f, 5f));
        Assert.Equal(5f, RocketGrabAbility.CooldownForCount(1, 15f, 5f));
        Assert.Equal(5f, RocketGrabAbility.CooldownForCount(2, 15f, 5f));
        Assert.Equal(15f, RocketGrabAbility.CooldownForCount(0, 15f, 5f));
    }
}
