using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRAdditionalSimpleRolesTests
{
    [Theory]
    [InlineData(RoleId.WiseMan, RoleTypes.Engineer)]
    [InlineData(RoleId.EvilSpeedBooster, RoleTypes.Phantom)]
    [InlineData(RoleId.Finder, RoleTypes.Impostor)]
    public void SupportedRolesUseTheirNativeControls(RoleId role, RoleTypes expected)
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(role));
        Assert.Equal(expected, SHRRoleTypes.GetInitialRole(role));
    }

    [Fact]
    public void WiseManEngineerDoesNotGrantVentingAndEvilSpeedBoosterKeepsVenting()
    {
        Assert.True(SHRCrewBoostAbility.UsesEngineer(RoleId.WiseMan));
        Assert.True(SHRVentRecovery.UsesOwnerReplay(RoleId.WiseMan));
        Assert.True(SHRSamuraiRestrictions.RejectVent(RoleId.WiseMan, false, VentilationSystem.Operation.Enter));
        Assert.False(SHRCrewBoostAbility.UsesEngineer(RoleId.EvilSpeedBooster));
        Assert.False(SHRVentRecovery.UsesOwnerReplay(RoleId.EvilSpeedBooster));
        Assert.False(SHRSamuraiRestrictions.RejectVent(RoleId.EvilSpeedBooster, true, VentilationSystem.Operation.Enter));
    }

    [Fact]
    public void FinderUnlocksOnlyAtTheConfiguredSuccessfulKillCount()
    {
        var progress = new FinderKillProgress(2);
        Assert.False(progress.Record(false));
        Assert.False(progress.Revealed);
        Assert.False(progress.Record(true));
        Assert.False(progress.Record(false));
        Assert.False(progress.Revealed);
        Assert.True(progress.Record(true));
        Assert.True(progress.Revealed);
        Assert.False(progress.Record(true));
        Assert.True(progress.Revealed);
    }
}
