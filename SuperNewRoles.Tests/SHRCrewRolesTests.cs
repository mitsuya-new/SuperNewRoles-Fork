using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRCrewRolesTests
{
    [Fact]
    public void ToiletFanUsesEngineerControlsWithoutAllowingActualVentEntry()
    {
        Assert.Equal(RoleTypes.Engineer, SHRRoleTypes.GetInitialRole(RoleId.ToiletFan));
        Assert.False(SHRCrewRoleAbility.HasCrewImpostorControls(RoleId.ToiletFan));
        foreach (bool canVent in new[] { false, true })
        {
            Assert.True(SHRSamuraiRestrictions.RejectVent(RoleId.ToiletFan, canVent, VentilationSystem.Operation.Enter));
            Assert.True(SHRSamuraiRestrictions.RejectVent(RoleId.ToiletFan, canVent, VentilationSystem.Operation.Move));
            Assert.False(SHRSamuraiRestrictions.RejectVent(RoleId.ToiletFan, canVent, VentilationSystem.Operation.Exit));
        }
    }

    [Theory]
    [InlineData(true, 1, 1, RoleId.Sheriff, true, true)]
    [InlineData(false, 1, 1, RoleId.Sheriff, true, false)]
    [InlineData(true, null, 1, RoleId.Sheriff, true, false)]
    [InlineData(true, 1, 2, RoleId.Sheriff, true, false)]
    [InlineData(true, 1, 1, RoleId.Crewmate, true, false)]
    [InlineData(true, 1, 1, RoleId.Sheriff, false, false)]
    public void ChiefIdentifiesOnlyTheirLivingAppointedSheriff(bool enabled, int? appointedId, byte target,
        RoleId role, bool alive, bool expected)
        => Assert.Equal(expected, SuperNewRoles.Roles.Crewmate.ChiefAbility.CanIdentifyCreatedSheriff(enabled,
            appointedId.HasValue ? (byte?)appointedId.Value : null, target, role, alive));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void HamburgerShopIsAssignedAndShownOnlyOnAirshipInSHR(byte map, bool available)
    {
        Assert.Equal(available, SHRRoleTypes.IsAvailableOnMap(RoleId.HamburgerShop, map));
        Assert.Equal(available, SHRRoleTypes.ShouldShowInSettings(RoleId.HamburgerShop, true, map));
        Assert.True(SHRRoleTypes.ShouldShowInSettings(RoleId.HamburgerShop, false, map));
        Assert.True(SHRRoleTypes.ShouldShowInSettings(RoleId.NiceGuesser, true, map));
    }
    [Theory]
    [InlineData(RoleId.NiceGuesser, RoleTypes.Crewmate)]
    [InlineData(RoleId.Chief, RoleTypes.Impostor)]
    [InlineData(RoleId.Mayor, RoleTypes.Crewmate)]
    [InlineData(RoleId.Celebrity, RoleTypes.Crewmate)]
    [InlineData(RoleId.ElectionCommissioner, RoleTypes.Crewmate)]
    [InlineData(RoleId.ToiletFan, RoleTypes.Engineer)]
    [InlineData(RoleId.HamburgerShop, RoleTypes.Crewmate)]
    [InlineData(RoleId.Bait, RoleTypes.Crewmate)]
    [InlineData(RoleId.BestFalseCharge, RoleTypes.Crewmate)]
    [InlineData(RoleId.Seer, RoleTypes.Crewmate)]
    [InlineData(RoleId.NiceButtoner, RoleTypes.Crewmate)]
    [InlineData(RoleId.HomeSecurityGuard, RoleTypes.Crewmate)]
    [InlineData(RoleId.Stuntman, RoleTypes.Crewmate)]
    [InlineData(RoleId.Technician, RoleTypes.Engineer)]
    [InlineData(RoleId.SatsumaAndImo, RoleTypes.Crewmate)]
    [InlineData(RoleId.NiceNekomata, RoleTypes.Crewmate)]
    public void CrewControlsArePrivateAndAvailableInSHRSettings(RoleId role, RoleTypes controls)
    {
        Assert.True(SHRRoleTypes.ShouldShowInSettings(role, true));
        Assert.Equal(controls, SHRRoleAssignment.GetViewRole(role, RoleTypes.Crewmate, true, true, role, true, false));
        Assert.Equal(RoleTypes.Crewmate, SHRRoleAssignment.GetViewRole(role, RoleTypes.Crewmate, true, true, role, false, false));
    }

    [Theory]
    [InlineData(RoleId.Chief)]
    public void CrewImpostorControlsCannotVentOrSabotageAndCanTargetImpostors(RoleId role)
    {
        Assert.True(SHRSamuraiRestrictions.RejectSabotage(role, true));
        Assert.True(SHRSamuraiRestrictions.RejectVent(role, true, VentilationSystem.Operation.Enter));
        Assert.True(SHRSamuraiRestrictions.RejectVent(role, true, VentilationSystem.Operation.Move));
        Assert.False(SHRSamuraiRestrictions.RejectVent(role, true, VentilationSystem.Operation.Exit));
        Assert.Equal(RoleTypes.Scientist, SHRRoleAssignment.GetViewRole(RoleId.Impostor, RoleTypes.Impostor,
            false, true, role, true, true));
        Assert.Equal(RoleTypes.Scientist, SHRRoleAssignment.GetViewRole(role, RoleTypes.Crewmate,
            false, true, RoleId.Impostor, true, false));
    }

    [Theory]
    [InlineData(RoleId.NiceGuesser, true)]
    [InlineData(RoleId.EvilGuesser, true)]
    [InlineData(RoleId.Crewmate, false)]
    [InlineData(RoleId.Sheriff, false)]
    public void BothGuessersShareTheChatCommand(RoleId role, bool allowed)
        => Assert.Equal(allowed, SHRGuesserCommands.CanUse(role));

    [Theory]
    [InlineData(RoleId.Technician)]
    [InlineData(RoleId.SilverBullet)]
    public void EngineerControlsEnforceTheCustomVentCondition(RoleId role)
    {
        Assert.True(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Enter));
        Assert.False(SHRSamuraiRestrictions.RejectVent(role, true, VentilationSystem.Operation.Enter));
        Assert.False(SHRSamuraiRestrictions.RejectVent(role, false, VentilationSystem.Operation.Exit));
    }

    [Theory]
    [InlineData(0, SystemTypes.Reactor)]
    [InlineData(1, SystemTypes.Reactor)]
    [InlineData(2, SystemTypes.Laboratory)]
    [InlineData(4, SystemTypes.HeliSabotage)]
    [InlineData(5, SystemTypes.Reactor)]
    public void NativeFlashUsesTheAlarmPresentOnTheMap(int map, SystemTypes system)
        => Assert.Equal(system, SHRCrewRoleAbility.FlashSystem(map));

    [Fact]
    public void UnreleasedRolesAreNotEnabledByTheCrewPort()
    {
        Assert.False(SHRRoleTypes.ShouldShowInSettings(RoleId.PoliceSurgeon, true));
        Assert.False(SHRRoleTypes.ShouldShowInSettings(RoleId.Taskmaster, true));
        Assert.False(SHRRoleTypes.ShouldShowInSettings(RoleId.SilverBullet, true));
        Assert.False(SHRRoleTypes.IsSupported(RoleId.SilverBullet));
        Assert.True(SHRRoleTypes.ShouldShowInSettings(RoleId.SilverBullet, false));
        Assert.Null(SHRRoleTypes.GetInitialRole(RoleId.SilverBullet));
    }
}
