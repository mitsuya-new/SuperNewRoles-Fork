using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHREndCriteriaTests
{
    [Theory]
    [InlineData(1, 2, 2, false)]
    [InlineData(1, 3, 2, false)]
    [InlineData(1, 2, 1, true)]
    [InlineData(1, 3, 1, false)]
    [InlineData(2, 4, 2, true)]
    [InlineData(2, 4, 3, false)]
    [InlineData(0, 2, 1, false)]
    public void AnotherKillerTeamPreventsAParityWin(int team, int total, int killers, bool expected)
        => Assert.Equal(expected, PlayerStatistics.HasKillerMajority(team, total, killers));

    [Theory]
    [InlineData(CustomGameOverReason.JackalWin)]
    [InlineData(CustomGameOverReason.TaskerWin)]
    public void CustomWinsKeepTheModdedReasonButUseAStandardNetworkReasonForSHR(CustomGameOverReason reason)
    {
        Assert.Equal(GameOverReason.ImpostorsByKill, EndGameManagerSetUpPatch.NetworkEndReason((GameOverReason)reason, true));
        Assert.Equal((GameOverReason)reason, EndGameManagerSetUpPatch.NetworkEndReason((GameOverReason)reason, false));
    }

    [Theory]
    [InlineData(GameOverReason.CrewmatesByTask)]
    [InlineData(GameOverReason.CrewmatesByVote)]
    [InlineData(GameOverReason.ImpostorsByKill)]
    [InlineData(GameOverReason.ImpostorsBySabotage)]
    public void StandardEndReasonsRemainUnchanged(GameOverReason reason)
        => Assert.Equal(reason, EndGameManagerSetUpPatch.NetworkEndReason(reason, true));
}
