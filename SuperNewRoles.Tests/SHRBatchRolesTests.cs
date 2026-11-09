using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRBatchRolesTests
{
    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void JammerHidesOnlyFromOtherUnmoddedNonImpostors(bool self, bool vanilla, bool impostor, bool expected)
        => Assert.Equal(expected, SHRPrivatePlayerEffects.ShouldHideFrom(self, vanilla, impostor));

    [Fact]
    public void FailedDelayedEffectIsNotRetriedOnSubsequentFrames()
    {
        float until = 5f;
        int attempts = 0;
        foreach (float now in new[] { 4.9f, 5f, 5.02f, 5.04f })
        {
            if (!SHRBatchRoleAbility.TryConsumeExpiredEffect(ref until, now)) continue;
            try
            {
                attempts++;
                throw new System.InvalidOperationException("Teleport or result display failed");
            }
            catch (System.InvalidOperationException) { }
        }
        Assert.Equal(1, attempts);
        Assert.Equal(0f, until);
    }

    [Fact]
    public void CancelledDelayedEffectNeverFires()
    {
        float until = 0f;
        Assert.False(SHRBatchRoleAbility.TryConsumeExpiredEffect(ref until, 30f));
    }

    [Theory]
    [InlineData(1f, true, 1f)]
    [InlineData(1f, false, 1f)]
    [InlineData(0f, true, 10f)]
    [InlineData(0.5f, true, 10f)]
    [InlineData(0f, false, 0.5f)]
    [InlineData(0.5f, false, 1.5f)]
    public void DependentReverseVisionIsDarkInDaylightAndUsesTheConfiguredNightVision(float light, bool impostorInDark, float expected)
        => Assert.Equal(expected, SHRVampireRestrictions.DependentVisionRadius(light, 1f, 5f, 0.5f, 2f, impostorInDark));

    [Theory]
    [InlineData(RoleId.EvilNekomata)]
    [InlineData(RoleId.Pursuer)]
    [InlineData(RoleId.EvilDoorr)]
    [InlineData(RoleId.DoubleKiller)]
    [InlineData(RoleId.Mafia)]
    [InlineData(RoleId.EvilButtoner)]
    [InlineData(RoleId.DarkKiller)]
    [InlineData(RoleId.Magaziner)]
    [InlineData(RoleId.ShiftActor)]
    [InlineData(RoleId.Jammer)]
    [InlineData(RoleId.EvilMoving)]
    [InlineData(RoleId.Teleporter)]
    [InlineData(RoleId.Vampire)]
    public void RequestedRolesAreAvailableInSHRSettingsAndDescriptions(RoleId role)
    {
        Assert.True(SHRBatchRoleAbility.Supports(role));
        Assert.True(SHRRoleTypes.HasImplementedSupport(role));
        Assert.True(SHRRoleTypes.ShouldShowInSettings(role, true));
        Assert.True(SHRRoleTypes.IsSupported(role));
    }

    [Fact]
    public void TaskerRemainsUnsupported()
    {
        Assert.False(SHRBatchRoleAbility.Supports(RoleId.Tasker));
        Assert.False(SHRRoleTypes.HasImplementedSupport(RoleId.Tasker));
        Assert.False(SHRRoleTypes.ShouldShowInSettings(RoleId.Tasker, true));
    }

    [Theory]
    [InlineData(1f, 0f, "→")]
    [InlineData(1f, 1f, "↗")]
    [InlineData(0f, 1f, "↑")]
    [InlineData(-1f, 1f, "↖")]
    [InlineData(-1f, 0f, "←")]
    [InlineData(-1f, -1f, "↙")]
    [InlineData(0f, -1f, "↓")]
    [InlineData(1f, -1f, "↘")]
    [InlineData(0f, 0f, "●")]
    public void PursuerArrowCoversEveryDirection(float x, float y, string expected)
        => Assert.Equal(expected, SHRBatchRoleAbility.DirectionArrow(x, y));

    [Theory]
    [InlineData(RoleId.Vampire, true)]
    [InlineData(RoleId.VampireDependent, true)]
    [InlineData(RoleId.Impostor, false)]
    [InlineData(RoleId.Jackal, false)]
    public void DeviceRestrictionsApplyOnlyToVampiresAndTheirDependents(RoleId role, bool expected)
        => Assert.Equal(expected, SHRVampireRestrictions.IsVampire(role));
}
