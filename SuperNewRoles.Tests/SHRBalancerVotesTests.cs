using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using AmongUs.GameOptions;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRBalancerVotesTests
{
    [Theory]
    [InlineData(32f, 10, 30f, 52)]
    [InlineData(100f, 15, 30f, 115)]
    [InlineData(32.2f, 10, 30f, 53)]
    [InlineData(10f, 10, 36.5f, 37)]
    [InlineData(0f, 0, 0f, 1)]
    public void NativeTimerIncludesElapsedMeetingTime(float elapsed, int discussion, float remaining, int expected)
        => Assert.Equal(expected, SHRBalancerVotes.VotingTimeForElapsed(elapsed, discussion, remaining));

    [Fact]
    public void BalancerIsCrewmateAndCanBeGivenBySanta()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Balancer));
        Assert.Equal(RoleTypes.Crewmate, SHRRoleTypes.GetInitialRole(RoleId.Balancer));
        Assert.False(SantaAbility.IsUnavailableSHROption("SantaBalancerPercentage"));
        Assert.Equal(new[] { RoleId.Balancer }, SantaAbility.BuildTickets(new[] { (RoleId.Balancer, 5) }, true));
    }

    [Theory]
    [InlineData(false, (byte)2, false)]
    [InlineData(false, (byte)3, false)]
    [InlineData(true, (byte)2, true)]
    [InlineData(true, (byte)3, true)]
    [InlineData(true, (byte)1, false)]
    [InlineData(true, (byte)253, false)]
    [InlineData(true, (byte)254, false)]
    public void OnlyTheSelectedPairCanReceiveVotesAfterAnimation(bool waiting, byte target, bool expected)
        => Assert.Equal(expected, SHRBalancerVotes.AcceptVote(waiting, target, 2, 3));

    [Theory]
    [InlineData(10f, 40f, 30f)]
    [InlineData(39.9f, 40f, 0.1f)]
    [InlineData(40f, 40f, 0f)]
    [InlineData(41f, 40f, 0f)]
    public void DeadlineExpiresOnceTheConfiguredVotingTimeHasElapsed(float now, float deadline, float expected)
        => Assert.Equal(expected, SHRBalancerVotes.Remaining(now, deadline), 3);

    [Fact]
    public void OrdinaryVotesRemainVotesUntilSelfVoting()
    {
        var state = new SHRBalancerSelection();
        Assert.Equal(SHRBalancerSelection.Action.Vote, state.Receive(1, 2));
        Assert.Equal(SHRBalancerSelection.Action.Arm, state.Receive(1, 1));
        Assert.Equal(SHRBalancerSelection.Action.First, state.Receive(1, 2));
        Assert.Equal((byte)2, state.First);
        Assert.Equal(SHRBalancerSelection.Action.Start, state.Receive(1, 3));
        Assert.False(state.Armed);
        Assert.Equal(SHRBalancerSelection.Action.Vote, state.Receive(1, 4));
    }

    [Fact]
    public void DuplicateTargetCannotStartAndSelfVoteRestartsSelection()
    {
        var state = new SHRBalancerSelection();
        state.Receive(1, 1);
        state.Receive(1, 2);
        Assert.Equal(SHRBalancerSelection.Action.Duplicate, state.Receive(1, 2));
        Assert.True(state.Armed);
        state.Receive(1, 1);
        Assert.Null(state.First);
        Assert.Equal(SHRBalancerSelection.Action.First, state.Receive(1, 3));
        Assert.Equal(SHRBalancerSelection.Action.Start, state.Receive(1, 2));
    }

    [Theory]
    [InlineData((byte)252)]
    [InlineData((byte)253)]
    [InlineData((byte)254)]
    [InlineData((byte)255)]
    public void SkipAndMissingTargetsCancelSelection(byte target)
    {
        var state = new SHRBalancerSelection();
        state.Receive(1, 1);
        state.Receive(1, 2);
        Assert.Equal(SHRBalancerSelection.Action.Cancel, state.Receive(1, target));
        Assert.Null(state.First);
        Assert.False(state.Armed);
    }
}
