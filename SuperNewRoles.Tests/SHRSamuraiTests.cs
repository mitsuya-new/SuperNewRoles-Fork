using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRSamuraiTests
{
    [Theory]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.Enter, true)]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.Move, true)]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.Exit, false)]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.StartCleaning, false)]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.StopCleaning, false)]
    [InlineData(RoleId.Samurai, false, VentilationSystem.Operation.BootImpostors, false)]
    [InlineData(RoleId.Samurai, true, VentilationSystem.Operation.Enter, false)]
    [InlineData(RoleId.Samurai, true, VentilationSystem.Operation.Move, false)]
    [InlineData(RoleId.Impostor, false, VentilationSystem.Operation.Enter, false)]
    [InlineData(RoleId.MadKiller, false, VentilationSystem.Operation.Enter, true)]
    [InlineData(RoleId.MadKiller, false, VentilationSystem.Operation.Move, true)]
    [InlineData(RoleId.MadKiller, false, VentilationSystem.Operation.Exit, false)]
    [InlineData(RoleId.MadKiller, true, VentilationSystem.Operation.Enter, false)]
    [InlineData(RoleId.DarkKiller, false, VentilationSystem.Operation.Enter, true)]
    [InlineData(RoleId.DoubleKiller, false, VentilationSystem.Operation.Enter, true)]
    [InlineData(RoleId.DoubleKiller, true, VentilationSystem.Operation.Enter, false)]
    [InlineData(RoleId.VampireDependent, false, VentilationSystem.Operation.Enter, true)]
    [InlineData(RoleId.Crewmate, false, VentilationSystem.Operation.StartCleaning, false)]
    public void VentRestrictionBlocksOnlyDisabledSamuraiEntryAndMovement(
        RoleId role, bool canVent, VentilationSystem.Operation operation, bool expected)
        => Assert.Equal(expected, SHRSamuraiRestrictions.RejectVent(role, canVent, operation));

    [Theory]
    [InlineData(RoleId.Samurai, false, true)]
    [InlineData(RoleId.Samurai, true, false)]
    [InlineData(RoleId.Impostor, false, false)]
    [InlineData(RoleId.DoubleKiller, false, true)]
    [InlineData(RoleId.DoubleKiller, true, false)]
    [InlineData(RoleId.Crewmate, false, false)]
    public void SabotageRestrictionFollowsSamuraiSettingOnly(RoleId role, bool canSabotage, bool expected)
        => Assert.Equal(expected, SHRSamuraiRestrictions.RejectSabotage(role, canSabotage));
}
