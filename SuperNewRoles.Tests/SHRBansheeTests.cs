using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRBansheeTests
{
    [Fact]
    public void BansheeUsesShapeshifterControls()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Banshee));
        Assert.Equal(RoleTypes.Shapeshifter, SHRRoleTypes.GetInitialRole(RoleId.Banshee));
    }

    [Fact]
    public void ReleaseDoesNotRestartTheIndependentlyRunningWhisperCooldown()
    {
        var clock = new SHRBansheeCooldowns();
        clock.Reset(0f, 10f, 10f);
        Assert.Equal(1f, clock.Remaining(9f, false));
        clock.Released(10f, 25f);
        Assert.Equal(0f, clock.Remaining(10f, true));
        Assert.Equal(25f, clock.Remaining(10f, false));
    }

    [Fact]
    public void ResolvingWhisperKeepsTheReleaseDeadlineInsteadOfAddingAnotherCooldown()
    {
        var clock = new SHRBansheeCooldowns();
        clock.Reset(0f, 0f, 0f);
        clock.Released(10f, 25f);
        clock.Whispered(12f, 10f);
        Assert.Equal(10f, clock.Remaining(12f, true));
        Assert.Equal(23f, clock.Remaining(12f, false));
        Assert.Equal(0f, clock.Remaining(36f, false));
    }

    [Fact]
    public void NormalKillAndMeetingResetBothAbilityDeadlines()
    {
        var clock = new SHRBansheeCooldowns();
        clock.Reset(0f, 0f, 0f);
        clock.Released(0f, 25f);
        clock.Whispered(5f, 10f);
        clock.Reset(20f, 25f, 10f);
        Assert.Equal(25f, clock.Remaining(20f, false));
        Assert.Equal(10f, clock.Remaining(20f, true));
    }
}
