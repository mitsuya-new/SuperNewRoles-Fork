using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHREvilSeerTests
{
    [Fact]
    public void SeerKeepsImpostorControlsAndDoesNotGainEngineerVenting()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.EvilSeer));
        Assert.Equal(RoleTypes.Impostor, SHRRoleTypes.GetInitialRole(RoleId.EvilSeer));
        Assert.False(SHRVentRecovery.UsesOwnerReplay(RoleId.EvilSeer));
    }

    [Theory]
    [InlineData(EvilSeerHauntMode.None, false)]
    [InlineData(EvilSeerHauntMode.Both, true)]
    [InlineData(EvilSeerHauntMode.OnlyOthersHaunt, true)]
    [InlineData(EvilSeerHauntMode.OnlyEvilSeerHaunt, false)]
    public void PetGrantIsAvailableOnlyForTheConfiguredHauntModes(EvilSeerHauntMode mode, bool expected)
        => Assert.Equal(expected, SHREvilSeerAbility.CanGiveHaunt(mode));

    [Theory]
    [InlineData(0f, null, true)]
    [InlineData(1000f, null, true)]
    [InlineData(29.9f, 30f, true)]
    [InlineData(30f, 30f, false)]
    [InlineData(31f, 30f, false)]
    public void SoulDirectionsRespectTheDurationLimit(float now, float? expiresAt, bool expected)
        => Assert.Equal(expected, SHREvilSeerAbility.SoulVisible(now, expiresAt));
}
