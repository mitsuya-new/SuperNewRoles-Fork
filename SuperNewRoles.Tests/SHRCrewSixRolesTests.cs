using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRCrewSixRolesTests
{
    [Theory]
    [InlineData(RoleId.Datahacker, RoleTypes.Crewmate)]
    [InlineData(RoleId.SpeedBooster, RoleTypes.Engineer)]
    [InlineData(RoleId.Bakery, RoleTypes.Crewmate)]
    [InlineData(RoleId.MeetingSheriff, RoleTypes.Crewmate)]
    [InlineData(RoleId.Lighter, RoleTypes.Engineer)]
    [InlineData(RoleId.HauntedWolf, RoleTypes.Crewmate)]
    public void CrewRolesUseTheExpectedNativeControls(RoleId role, RoleTypes expected)
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(role));
        Assert.Equal(expected, SHRRoleTypes.GetInitialRole(role));
    }

    [Theory]
    [InlineData(RoleId.SpeedBooster)]
    [InlineData(RoleId.Lighter)]
    public void BoostersRejectVentEntryButAllowExit(RoleId role)
    {
        Assert.True(SHRVentRecovery.UsesOwnerReplay(role));
        Assert.True(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Enter));
        Assert.True(SHRSamuraiRestrictions.RejectVent(role, true, VentilationSystem.Operation.Move));
        Assert.False(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Exit));
    }

    [Fact]
    public void EffectCooldownStartsAfterDurationAndMeetingResetStopsEffect()
    {
        var timer = new SHRCrewEffectTimer();
        timer.Reset(0f, 10f);
        Assert.False(timer.Activate(9f, 5f));
        Assert.True(timer.Activate(10f, 5f));
        Assert.Equal(3f, timer.Remaining(12f));
        Assert.False(timer.Activate(12f, 5f));
        Assert.False(timer.Expire(14f, 30f));
        Assert.True(timer.Expire(15f, 30f));
        Assert.False(timer.Active);
        Assert.Equal(30f, timer.Remaining(15f));
        Assert.True(timer.Activate(45f, 5f));
        timer.Reset(46f, 10f);
        Assert.False(timer.Active);
        Assert.Equal(10f, timer.Remaining(46f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void LighterKeepsTheSameRadiusDuringLightsSabotage(float fraction)
    {
        const float minimum = 0.2f, maximum = 5f, multiplier = 2.5f;
        float vision = SHRCrewBoostAbility.LighterCrewVision(multiplier, minimum, maximum, fraction);
        float radius = minimum + (maximum - minimum) * fraction;
        Assert.Equal(maximum * multiplier, radius * vision, 4);
    }

    [Fact]
    public void SheriffVotesArmShootCancelAndReturnToNormalVoting()
    {
        var state = new SHRMeetingSheriffVoteState();
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Vote, state.Receive(1, 2));
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Arm, state.Receive(1, 1));
        Assert.True(state.Armed);
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Shoot, state.Receive(1, 2));
        Assert.False(state.Armed);
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Vote, state.Receive(1, 3));
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Arm, state.Receive(1, 1));
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Cancel, state.Receive(1, 253));
        Assert.Equal(SHRMeetingSheriffVoteState.Action.Vote, state.Receive(1, 253));
    }
}
