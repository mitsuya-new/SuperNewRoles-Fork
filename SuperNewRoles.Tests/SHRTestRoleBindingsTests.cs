using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class ShrTestRoleBindingsTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void OnlyUnmoddedGuestsNeedVanillaControlSubstitutions(bool isOwner, bool hasSnr, bool expected)
        => Assert.Equal(expected, ShrTestRoleAbility.NeedsVanillaControls(isOwner, hasSnr));

    [Theory]
    [InlineData(RoleId.SelfBomber, ShrAbilityTrigger.Phantom)]
    [InlineData(RoleId.Samurai, ShrAbilityTrigger.Phantom)]
    [InlineData(RoleId.Jackal, ShrAbilityTrigger.Phantom)]
    [InlineData(RoleId.Tasker, ShrAbilityTrigger.Pet)]
    [InlineData(RoleId.Sheriff, (ShrAbilityTrigger)0)]
    [InlineData(RoleId.Crewmate, (ShrAbilityTrigger)0)]
    [InlineData(RoleId.Impostor, (ShrAbilityTrigger)0)]
    public void OnlyRequestedRolesReceiveTestTriggers(RoleId role, ShrAbilityTrigger expected)
        => Assert.Equal(expected, ShrTestRoleAbility.TriggerFor(role));
}
