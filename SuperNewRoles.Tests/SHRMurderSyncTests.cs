using SuperNewRoles.Mode;
using SuperNewRoles.Modules;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRMurderSyncTests
{
    [Fact]
    public void SuccessfulDeathIsMarkedAsHostDecision()
    {
        var flags = SHRMurderSync.ResultFlags(false);
        Assert.True(flags.HasFlag(MurderResultFlags.Succeeded));
        Assert.True(flags.HasFlag(MurderResultFlags.DecisionByHost));
        Assert.False(flags.HasFlag(MurderResultFlags.FailedProtected));
    }

    [Fact]
    public void GuardianProtectionNeverBecomesASuccessfulDeath()
    {
        var flags = SHRMurderSync.ResultFlags(true);
        Assert.True(flags.HasFlag(MurderResultFlags.FailedProtected));
        Assert.True(flags.HasFlag(MurderResultFlags.DecisionByHost));
        Assert.False(flags.HasFlag(MurderResultFlags.Succeeded));
    }

    [Theory]
    [InlineData(ModeId.SuperHostRoles, true, true)]
    [InlineData(ModeId.SuperHostRoles, false, false)]
    [InlineData(ModeId.Default, true, false)]
    [InlineData(ModeId.BattleRoyal, true, false)]
    [InlineData(ModeId.WCBattleRoyal, true, false)]
    public void OnlySHRHostReplacesCustomDeathTransport(ModeId mode, bool isHost, bool expected)
        => Assert.Equal(expected, SHRMurderSync.UsesVanillaResult(mode, isHost));
}
