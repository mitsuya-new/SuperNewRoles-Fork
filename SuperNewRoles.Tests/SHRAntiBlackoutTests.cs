using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRAntiBlackoutTests
{
    [Theory]
    [InlineData(true, false, true, MeetingHud.MeetingStates.NotVoted, true)]
    [InlineData(true, false, true, MeetingHud.MeetingStates.Voted, true)]
    [InlineData(true, false, true, MeetingHud.MeetingStates.Discussion, true)]
    [InlineData(true, true, true, MeetingHud.MeetingStates.NotVoted, false)]
    [InlineData(true, false, true, MeetingHud.MeetingStates.Results, false)]
    [InlineData(true, false, true, MeetingHud.MeetingStates.Proceeding, false)]
    [InlineData(false, false, true, MeetingHud.MeetingStates.NotVoted, false)]
    [InlineData(true, false, false, MeetingHud.MeetingStates.NotVoted, false)]
    public void ForcedCloseEntersProtectedVotingOnceWithoutInterceptingNormalResultClosure(
        bool host, bool active, bool vanilla, MeetingHud.MeetingStates state, bool expected)
        => Assert.Equal(expected, SHRAntiBlackout.ShouldRouteForcedClose(host, active, vanilla, state));

    [Theory]
    [InlineData(false, (byte)253)]
    [InlineData(true, (byte)252)]
    public void ForcedSkipPreservesDeadVotersAndAssignsSkipToLivingVoters(bool dead, byte expected)
        => Assert.Equal(expected, SHRAntiBlackout.ForcedVoteTarget(dead));

    [Theory]
    [InlineData(3, true, true)]
    [InlineData(4, true, false)]
    [InlineData(15, true, false)]
    [InlineData(3, false, false)]
    public void ExileIsDeferredOnlyWhenItWouldLeaveTooFewTemporaryCrewmates(int count, bool exile, bool expected)
        => Assert.Equal(expected, SHRAntiBlackout.ShouldDeferExile(count, exile));

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(15, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void TemporaryTableKeepsAnImpostorAndCrewMajorityThroughExile(int count, bool exile)
    {
        int impostors = 0;
        int crew = 0;
        bool applyExile = exile && !SHRAntiBlackout.ShouldDeferExile(count, exile);
        for (byte id = 0; id < count; id++)
        {
            if (applyExile && id == count - 1) continue;
            if (SHRAntiBlackout.TemporaryRole(id, 0) == RoleTypes.Impostor) impostors++;
            else crew++;
        }
        Assert.Equal(1, impostors);
        Assert.True(crew > impostors);
    }

}
