using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRCamouflagerTests
{
    [Theory]
    [InlineData(0, 1, 15)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 17, 17)]
    [InlineData(2, 6, 15)]
    public void TemplateUsesGrayUnlessAColorIsSelected(int mode, int selected, int expected)
        => Assert.Equal(expected, SHRCamouflagerController.SelectColor(mode, selected));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaskedSelfNameKeepsRoleAndTaskProgressWithoutThePlayerName(bool meeting)
        => Assert.Equal("<size=75%>Crewmate (2/5)</size>",
            SHRRoleNameDisplay.FormatName("Crewmate (2/5)", "", meeting));

    [Fact]
    public void ModdedActivationIsRoutedToTheHost()
        => Assert.Equal(SHRVanillaRpcRouter.Operation.Camouflage,
            SHRVanillaRpcRouter.Classify(typeof(CamouflagerAbility).GetMethod(nameof(CamouflagerAbility.RpcRequestSHRStart))!, []));

    [Fact]
    public void CamouflagerAppearsInSHRSettings()
        => Assert.True(SHRRoleTypes.ShouldShowInSettings(RoleId.Camouflager, true));
}
