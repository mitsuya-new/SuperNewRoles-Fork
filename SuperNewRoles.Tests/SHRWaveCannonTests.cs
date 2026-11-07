using SuperNewRoles.Mode;
using Xunit;
using SuperNewRoles.Modules;

namespace SuperNewRoles.Tests;

public class SHRWaveCannonTests
{
    [Theory]
    [InlineData(CustomDeathType.Kill)]
    [InlineData(CustomDeathType.KillWithoutKillAnimation)]
    [InlineData(CustomDeathType.SuperWaveCannon)]
    [InlineData(CustomDeathType.Samurai)]
    [InlineData(CustomDeathType.BombBySelfBomb)]
    [InlineData(CustomDeathType.VampireKill)]
    [InlineData(CustomDeathType.RocketLauncher)]
    public void ActiveCannonBlocksIncomingKillsButNotSelfDeathOrInactiveAttacks(CustomDeathType death)
    {
        Assert.True(SHRWaveCannonController.BlocksAttack(true, false, death));
        Assert.False(SHRWaveCannonController.BlocksAttack(false, false, death));
        Assert.False(SHRWaveCannonController.BlocksAttack(true, true, death));
    }

    [Theory]
    [InlineData(CustomDeathType.Exile)]
    [InlineData(CustomDeathType.Suicide)]
    [InlineData(CustomDeathType.LoversSuicide)]
    [InlineData(CustomDeathType.VampireWithDead)]
    [InlineData(CustomDeathType.BuskerFakeDeath)]
    public void CannonProtectionDoesNotCancelVotesAndNonAttackDeaths(CustomDeathType death)
        => Assert.False(SHRWaveCannonController.BlocksAttack(true, false, death));

    [Theory]
    [InlineData(10f, 0f, false, true)]
    [InlineData(-10f, 0f, false, false)]
    [InlineData(-10f, 0f, true, true)]
    [InlineData(10f, 0f, true, false)]
    [InlineData(10f, 1.3f, false, true)]
    [InlineData(10f, -1.3f, false, true)]
    [InlineData(10f, 1.31f, false, false)]
    [InlineData(-10f, -1.31f, true, false)]
    [InlineData(0f, 0f, true, false)]
    [InlineData(0f, 0f, false, false)]
    [InlineData(100f, 0f, false, true)]
    public void BeamUsesPkoForwardHalfLineAndWidth(float x, float y, bool left, bool expected)
        => Assert.Equal(expected, SHRWaveCannonController.IsBeamHit(x, y, left));

}
