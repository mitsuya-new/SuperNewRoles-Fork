using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRRoleTypesTests
{
    [Theory]
    [InlineData(false, false, RoleTypes.Crewmate)]
    [InlineData(false, true, RoleTypes.Crewmate)]
    [InlineData(true, false, RoleTypes.Tracker)]
    [InlineData(true, true, RoleTypes.Impostor)]
    public void TaskerRequiresTheKillOptionToUseTheImpostorSelection(bool canKill, bool selected, RoleTypes expected)
        => Assert.Equal(expected, SHRRoleTypes.GetTaskerRole(canKill, selected));

    [Theory]
    [InlineData(true, RoleTypes.Engineer)]
    [InlineData(false, RoleTypes.Crewmate)]
    public void MadKillerUsesEngineerOnlyWhenVentingIsEnabled(bool canVent, RoleTypes expected)
        => Assert.Equal(expected, SHRRoleTypes.GetMadKillerRole(canVent));

    [Theory]
    [InlineData(true, RoleTypes.Phantom)]
    [InlineData(false, RoleTypes.Impostor)]
    public void VampireControlsFollowDependentCreationOption(bool canCreate, RoleTypes expected)
        => Assert.Equal(expected, SHRRoleTypes.GetVampireRole(canCreate));

    [Theory]
    [InlineData(RoleId.Banshee, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.Conjurer, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.Doppelganger, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.EvilHacker, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.ShiftActor, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.SuicideWisher, RoleTypes.Phantom)]
    [InlineData(RoleId.Camouflager, RoleTypes.Phantom)]
    [InlineData(RoleId.DoubleKiller, RoleTypes.Phantom)]
    [InlineData(RoleId.EvilButtoner, RoleTypes.Phantom)]
    [InlineData(RoleId.EvilDoorr, RoleTypes.Phantom)]
    [InlineData(RoleId.EvilMoving, RoleTypes.Phantom)]
    [InlineData(RoleId.EvilScientist, RoleTypes.Phantom)]
    [InlineData(RoleId.EvilSpeedBooster, RoleTypes.Phantom)]
    [InlineData(RoleId.Jammer, RoleTypes.Phantom)]
    [InlineData(RoleId.Magaziner, RoleTypes.Phantom)]
    [InlineData(RoleId.Nun, RoleTypes.Phantom)]
    [InlineData(RoleId.Penguin, RoleTypes.Phantom)]
    [InlineData(RoleId.Pusher, RoleTypes.Phantom)]
    [InlineData(RoleId.RemoteController, RoleTypes.Phantom)]
    [InlineData(RoleId.Robber, RoleTypes.Phantom)]
    [InlineData(RoleId.Rocket, RoleTypes.Phantom)]
    [InlineData(RoleId.Samurai, RoleTypes.Phantom)]
    [InlineData(RoleId.SelfBomber, RoleTypes.Phantom)]
    [InlineData(RoleId.SideKiller, RoleTypes.Phantom)]
    [InlineData(RoleId.Teleporter, RoleTypes.Phantom)]
    [InlineData(RoleId.WaveCannon, RoleTypes.Phantom)]
    [InlineData(RoleId.DarkKiller, RoleTypes.Impostor)]
    [InlineData(RoleId.EvilGambler, RoleTypes.Impostor)]
    [InlineData(RoleId.EvilGuesser, RoleTypes.Impostor)]
    [InlineData(RoleId.EvilNekomata, RoleTypes.Impostor)]
    [InlineData(RoleId.EvilSeer, RoleTypes.Impostor)]
    [InlineData(RoleId.Finder, RoleTypes.Impostor)]
    [InlineData(RoleId.Mafia, RoleTypes.Crewmate)]
    [InlineData(RoleId.NekoKabocha, RoleTypes.Impostor)]
    [InlineData(RoleId.OverKiller, RoleTypes.Impostor)]
    [InlineData(RoleId.Pursuer, RoleTypes.Impostor)]
    [InlineData(RoleId.SerialKiller, RoleTypes.Shapeshifter)]
    [InlineData(RoleId.VampireDependent, RoleTypes.Impostor)]
    [InlineData(RoleId.Impostor, RoleTypes.Impostor)]
    [InlineData(RoleId.Sheriff, RoleTypes.Impostor)]
    [InlineData(RoleId.MadKiller, RoleTypes.Crewmate)]
    public void RoleTableAppliesOnlyToTheUnmoddedOwnersView(RoleId role, RoleTypes expected)
    {
        Assert.True(SHRRoleTypes.IsSupported(role));
        Assert.Equal(expected, SHRRoleAssignment.GetViewRole(role, RoleTypes.Crewmate, true,
            true, role, true, false));
        Assert.Equal(RoleTypes.Crewmate, SHRRoleAssignment.GetViewRole(role, RoleTypes.Crewmate,
            true, true, role, false, false));
    }

    [Theory]
    [InlineData(RoleId.Crab)]
    [InlineData(RoleId.DimensionWalker)]
    [InlineData(RoleId.EvilHawk)]
    [InlineData(RoleId.EvilMechanic)]
    [InlineData(RoleId.Kunoichi)]
    [InlineData(RoleId.Matryoshka)]
    [InlineData(RoleId.Mushroomer)]
    [InlineData(RoleId.Necromancer)]
    [InlineData(RoleId.Orpheus)]
    [InlineData(RoleId.RocketLauncher)]
    [InlineData(RoleId.Slugger)]
    [InlineData(RoleId.TriggerHappy)]
    [InlineData(RoleId.Tasker)]
    public void UnsupportedRolesAreExcludedFromSHR(RoleId role)
        => Assert.False(SHRRoleTypes.IsSupported(role));
}
