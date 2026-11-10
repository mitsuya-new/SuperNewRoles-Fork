using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRPetsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("pet_EmptyPet", true)]
    [InlineData("pet_EnmptyPet", true)]
    [InlineData("pet_empty", true)]
    [InlineData("pet_none", true)]
    [InlineData("none", true)]
    [InlineData("pet_test", false)]
    [InlineData("pet_Hamster", false)]
    [InlineData("custom_pet", false)]
    public void OnlyPlayersWithoutAnEquippedPetReceiveTheDefault(string? pet, bool grant)
        => Assert.Equal(grant, SHRPets.NeedsPet(pet));

    [Theory]
    [InlineData(true, "pet_test", "pet_test")]
    [InlineData(true, "pet_Hamster", "pet_Hamster")]
    [InlineData(false, "pet_test", "")]
    [InlineData(false, "pet_Hamster", "")]
    [InlineData(true, null, "")]
    public void PetsRemainVisibleOnlyToTheirOwner(bool self, string? pet, string expected)
        => Assert.Equal(expected, SHRPets.PetForViewer(self, pet));

    [Fact]
    public void NiceButtonerUsesCrewmateWithoutImpostorControls()
    {
        Assert.Equal(AmongUs.GameOptions.RoleTypes.Crewmate, SHRRoleTypes.GetInitialRole(RoleId.NiceButtoner));
        Assert.False(SHRCrewRoleAbility.HasCrewImpostorControls(RoleId.NiceButtoner));
    }
}
