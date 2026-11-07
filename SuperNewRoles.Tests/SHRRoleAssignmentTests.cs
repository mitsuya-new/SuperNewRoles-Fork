using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class ShrRoleAssignmentTests
{
    [Theory]
    [InlineData(RoleId.Jackal, RoleTypes.Crewmate, RoleTypes.Phantom)]
    [InlineData(RoleId.Sheriff, RoleTypes.Crewmate, RoleTypes.Impostor)]
    [InlineData(RoleId.SelfBomber, RoleTypes.Impostor, RoleTypes.Phantom)]
    [InlineData(RoleId.Crewmate, RoleTypes.Engineer, RoleTypes.Engineer)]
    public void UnmoddedPlayersReceiveTheirControlRoleInTheirFirstAssignment(
        RoleId custom, RoleTypes vanilla, RoleTypes expected)
        => Assert.Equal(expected, ShrRoleAssignment.GetViewRole(custom, vanilla, true, true,
            custom, true, vanilla == RoleTypes.Impostor));

    [Theory]
    [InlineData(RoleId.Jackal)]
    [InlineData(RoleId.Sheriff)]
    public void ImpostorButtonsDoNotRevealRealImpostors(RoleId viewer)
        => Assert.Equal(RoleTypes.Scientist, ShrRoleAssignment.GetViewRole(RoleId.SelfBomber,
            RoleTypes.Impostor, false, true, viewer, true, true));

    [Fact]
    public void OtherUnmoddedPlayersDoNotSeeJackalAsImpostor()
        => Assert.Equal(RoleTypes.Scientist, ShrRoleAssignment.GetViewRole(RoleId.Jackal,
            RoleTypes.Crewmate, false, true, RoleId.Crewmate, true, false));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ModdedViewerKeepsCanonicalVanillaRole(bool isSelf)
        => Assert.Equal(RoleTypes.Crewmate, ShrRoleAssignment.GetViewRole(RoleId.Jackal,
            RoleTypes.Crewmate, isSelf, true, RoleId.Jackal, false, false));

    [Fact]
    public void VanillaImpostorStillSeesTheirOwnImpostorRole()
        => Assert.Equal(RoleTypes.Impostor, ShrRoleAssignment.GetViewRole(RoleId.Impostor,
            RoleTypes.Impostor, true, true, RoleId.Impostor, true, true));
}
