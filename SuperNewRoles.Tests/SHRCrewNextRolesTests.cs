using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRCrewNextRolesTests
{
    [Theory]
    [InlineData(RoleId.Moving, RoleTypes.Engineer)]
    [InlineData(RoleId.NiceTeleporter, RoleTypes.Engineer)]
    [InlineData(RoleId.Clergy, RoleTypes.Engineer)]
    [InlineData(RoleId.Doorr, RoleTypes.Phantom)]
    [InlineData(RoleId.DyingMessenger, RoleTypes.Crewmate)]
    public void NewCrewRolesUseTheirSpecifiedNativeControls(RoleId role, RoleTypes expected)
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(role));
        Assert.Equal(expected, SHRRoleTypes.GetInitialRole(role));
        if (expected == RoleTypes.Engineer)
        {
            Assert.True(SHRVentRecovery.UsesOwnerReplay(role));
            Assert.True(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Enter));
            Assert.False(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Exit));
        }
    }

    [Fact]
    public void DoorrIsCrewDespiteItsNativePhantomControl()
    {
        Assert.True(SHRSamuraiRestrictions.RejectSabotage(RoleId.Doorr, true));
        Assert.True(SHRSamuraiRestrictions.RejectVent(RoleId.Doorr, true, VentilationSystem.Operation.Enter));
        Assert.Equal(RoleTypes.Scientist, SHRRoleAssignment.GetViewRole(RoleId.Doorr, RoleTypes.Crewmate, false,
            true, RoleId.Impostor, true, false));
    }

    [Fact]
    public void DelayedActionCannotFireTwiceOrRepeatAfterItsCooldown()
    {
        var timer = new SHRCrewActionTimer();
        timer.Reset(0f, 10f);
        Assert.False(timer.Queue(9f, 3f));
        Assert.True(timer.Queue(10f, 3f));
        Assert.False(timer.Queue(11f, 3f));
        Assert.False(timer.Consume(12f, 45f));
        Assert.True(timer.Consume(13f, 45f));
        Assert.False(timer.Pending);
        Assert.Equal(45f, timer.Remaining(13f));
        Assert.False(timer.Consume(14f, 45f));
        Assert.False(timer.Consume(100f, 45f));
        Assert.True(timer.Queue(100f, 3f));
    }

    [Fact]
    public void MeetingCancelsPendingTeleportAndZeroDelayRunsOnlyOnce()
    {
        var timer = new SHRCrewActionTimer();
        Assert.True(timer.Queue(1f, 3f));
        timer.Reset(2f, 10f);
        Assert.False(timer.Consume(4f, 45f));
        Assert.False(timer.Pending);
        Assert.True(timer.Queue(12f, 0f));
        Assert.True(timer.Consume(12f, 45f));
        Assert.False(timer.Consume(12f, 45f));
    }

    [Theory]
    [InlineData(-1f, 1f)]
    [InlineData(0f, 1f)]
    [InlineData(0.5f, 0.65f)]
    [InlineData(1f, 0.3f)]
    [InlineData(2f, 0.3f)]
    public void ClergyUsesTheNormalSmoothVisionTransition(float progress, float expected)
        => Assert.Equal(expected, SHRClergyVision.VisionMultiplier(progress, 0.3f), 5);
}
