using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRTestRoleBindingsTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void OnlyUnmoddedGuestsNeedVanillaControlSubstitutions(bool isOwner, bool hasSnr, bool expected)
        => Assert.Equal(expected, SHRTestRoleAbility.NeedsVanillaControls(isOwner, hasSnr));

    [Theory]
    [InlineData(RoleId.SelfBomber, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.Samurai, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.WaveCannon, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.Jackal, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.SideKiller, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.Penguin, SHRAbilityTrigger.Phantom)]
    [InlineData(RoleId.Tasker, SHRAbilityTrigger.Pet)]
    [InlineData(RoleId.Sheriff, (SHRAbilityTrigger)0)]
    [InlineData(RoleId.Crewmate, (SHRAbilityTrigger)0)]
    [InlineData(RoleId.Impostor, (SHRAbilityTrigger)0)]
    public void OnlyRequestedRolesReceiveTestTriggers(RoleId role, SHRAbilityTrigger expected)
        => Assert.Equal(expected, SHRTestRoleAbility.TriggerFor(role));
}
