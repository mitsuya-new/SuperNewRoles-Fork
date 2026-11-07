using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRStartPolicyTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", SyncErrorType.NotInstalled)]
    [InlineData("", SyncErrorType.NotMismatch)]
    public void UnmoddedGuestCanStartOnlyInHostModes(string? version, SyncErrorType? error)
    {
        Assert.True(SyncVersion.CanPlayerStartGame(ModeId.SuperHostRoles, version!, error));
        Assert.True(SyncVersion.CanPlayerStartGame(ModeId.BattleRoyal, version!, error));
        Assert.False(SyncVersion.CanPlayerStartGame(ModeId.Default, version!, error));
        Assert.False(SyncVersion.CanPlayerStartGame(ModeId.WCBattleRoyal, version!, error));
    }

    [Theory]
    [InlineData(SyncErrorType.NotMismatch, true)]
    [InlineData(SyncErrorType.HashMismatch, true)]
    [InlineData(SyncErrorType.VersionMismatch, false)]
    [InlineData(SyncErrorType.RpcMapMismatch, false)]
    [InlineData(SyncErrorType.AmongUsVersionMismatch, false)]
    [InlineData(null, false)]
    public void SHRStillChecksModdedGuests(SyncErrorType? error, bool expected)
    {
        Assert.Equal(expected, SyncVersion.CanPlayerStartGame(ModeId.SuperHostRoles, "3.3.0.0", error));
    }
}
