using SuperNewRoles.Patches;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class MadKillerTargetTests
{
    [Theory]
    [InlineData(true, RoleId.MadKiller, false, false, true)]
    [InlineData(true, RoleId.MadKiller, false, true, false)]
    [InlineData(true, RoleId.MadKiller, true, false, true)]
    [InlineData(true, RoleId.MadKiller, true, true, true)]
    [InlineData(false, RoleId.MadKiller, false, false, false)]
    [InlineData(false, RoleId.MadKiller, true, true, false)]
    [InlineData(true, RoleId.Crewmate, false, false, false)]
    [InlineData(true, RoleId.SideKiller, false, false, false)]
    [InlineData(true, RoleId.Madmate, false, false, false)]
    public void OnlyIdentifiableMadKillersAreProtectedFromImpostorAttacks(
        bool impostor, RoleId target, bool awakened, bool hideBeforePromotion, bool expected)
        => Assert.Equal(expected, SetTargetPatch.IsMadKillerTeammate(impostor, target, awakened, hideBeforePromotion));
}
