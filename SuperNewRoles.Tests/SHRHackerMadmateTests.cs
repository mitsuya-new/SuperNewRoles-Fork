using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRHackerMadmateTests
{
    [Theory]
    [InlineData(RoleId.EvilHacker, RoleId.Madmate, true)]
    [InlineData(RoleId.Santa, RoleId.Madmate, false)]
    [InlineData(RoleId.SideKiller, RoleId.MadKiller, false)]
    [InlineData(RoleId.EvilHacker, RoleId.Crewmate, false)]
    public void NativeSupportIsBoundOnlyToHackerMadmateCreation(RoleId source, RoleId target, bool expected)
        => Assert.Equal(expected, SHRHackerMadmateAbility.IsCreation(source, target));

    [Theory]
    [InlineData(false, RoleTypes.Crewmate)]
    [InlineData(true, RoleTypes.Engineer)]
    public void VentControlDoesNotGiveTheMadmateAnImpostorRole(bool vent, RoleTypes expected)
        => Assert.Equal(expected, SHRHackerMadmateAbility.VentRole(vent));

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void ImpostorVisionKeepsTheSameRadiusThroughLightsSabotage(float light)
    {
        float crew = SHRHackerMadmateAbility.CrewVision(true, 1f, 1.5f, 0.5f, 5f, light);
        Assert.Equal(7.5f, (0.5f + 4.5f * light) * crew, 4);
        Assert.Equal(1f, SHRHackerMadmateAbility.CrewVision(false, 1f, 1.5f, 0.5f, 5f, light));
    }

    [Theory]
    [InlineData(SystemTypes.Electrical, (byte)0, true)]
    [InlineData(SystemTypes.Electrical, (byte)4, true)]
    [InlineData(SystemTypes.Electrical, (byte)5, false)]
    [InlineData(SystemTypes.Electrical, (byte)128, false)]
    [InlineData(SystemTypes.Comms, (byte)0, true)]
    [InlineData(SystemTypes.Comms, (byte)16, true)]
    [InlineData(SystemTypes.Comms, (byte)17, true)]
    [InlineData(SystemTypes.Comms, (byte)64, true)]
    [InlineData(SystemTypes.Comms, (byte)32, false)]
    [InlineData(SystemTypes.Comms, (byte)128, false)]
    [InlineData(SystemTypes.Reactor, (byte)64, true)]
    [InlineData(SystemTypes.Reactor, (byte)16, true)]
    [InlineData(SystemTypes.Reactor, (byte)32, false)]
    [InlineData(SystemTypes.Reactor, (byte)128, false)]
    [InlineData(SystemTypes.Laboratory, (byte)65, true)]
    [InlineData(SystemTypes.Laboratory, (byte)33, false)]
    [InlineData(SystemTypes.LifeSupp, (byte)64, true)]
    [InlineData(SystemTypes.LifeSupp, (byte)16, true)]
    [InlineData(SystemTypes.LifeSupp, (byte)128, false)]
    [InlineData(SystemTypes.HeliSabotage, (byte)16, true)]
    [InlineData(SystemTypes.HeliSabotage, (byte)65, true)]
    [InlineData(SystemTypes.HeliSabotage, (byte)33, false)]
    [InlineData(SystemTypes.HeliSabotage, (byte)128, false)]
    [InlineData(SystemTypes.Doors, (byte)16, false)]
    public void RepairBanRejectsRepairOperationsButAllowsActivationAndHandRelease(SystemTypes system, byte amount, bool expected)
        => Assert.Equal(expected, SHRHackerMadmateAbility.RejectRepair(system, amount, true, true, true));

    [Theory]
    [InlineData(SystemTypes.Electrical, (byte)0)]
    [InlineData(SystemTypes.Comms, (byte)0)]
    [InlineData(SystemTypes.Reactor, (byte)64)]
    [InlineData(SystemTypes.LifeSupp, (byte)64)]
    [InlineData(SystemTypes.HeliSabotage, (byte)16)]
    public void DisabledRepairRestrictionsLeaveRepairsAvailable(SystemTypes system, byte amount)
        => Assert.False(SHRHackerMadmateAbility.RejectRepair(system, amount, false, false, false));

    [Fact]
    public void HackerIsAvailableWithShapeshifterControls()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.EvilHacker));
        Assert.Equal(RoleTypes.Shapeshifter, SHRRoleTypes.GetInitialRole(RoleId.EvilHacker));
    }
}
