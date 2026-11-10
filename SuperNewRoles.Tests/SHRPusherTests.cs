using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRPusherTests
{
    [Fact]
    public void PusherUsesPhantomControls()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Pusher));
        Assert.Equal(RoleTypes.Phantom, SHRRoleTypes.GetInitialRole(RoleId.Pusher));
    }

    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, false)]
    [InlineData((byte)2, true)]
    [InlineData((byte)3, false)]
    [InlineData((byte)4, true)]
    [InlineData((byte)5, true)]
    public void SettingsUseTheMapsWithPushSites(byte map, bool expected)
    {
        Assert.Equal(expected, SHRRoleTypes.IsAvailableOnMap(RoleId.Pusher, map));
        Assert.Equal(expected, SHRRoleTypes.ShouldShowInSettings(RoleId.Pusher, true, map));
    }

    [Theory]
    [InlineData(0f, 0f, 0f, true)]
    [InlineData(1f, 0f, 1f, true)]
    [InlineData(-1f, 0f, 1f, true)]
    [InlineData(1.01f, 0f, 1f, false)]
    [InlineData(0.5f, 0.5f, 0.5f, false)]
    [InlineData(0f, 0f, -1f, false)]
    public void PushSiteRadiusIncludesItsBoundary(float dx, float dy, float radius, bool expected)
        => Assert.Equal(expected, SHRPusherAbility.IsWithinPushSite(dx, dy, radius));
}
