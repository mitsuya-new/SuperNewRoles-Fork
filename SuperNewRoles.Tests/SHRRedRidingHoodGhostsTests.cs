using System.Linq;
using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRRedRidingHoodGhostsTests
{
    [Theory]
    [InlineData(true, true, 1, true)]
    [InlineData(false, true, 1, false)]
    [InlineData(true, false, 1, false)]
    [InlineData(true, true, 0, false)]
    public void InformationIsHiddenOnlyWhileWaitingForAnotherRevival(bool dead, bool revivable, int remaining, bool expected)
        => Assert.Equal(expected, SHRRedRidingHoodGhosts.ShouldHide(dead, revivable, remaining));

    [Fact]
    public void WaitingSequenceCreatesGhostPhysicsBeforeRemovingTheGhostAbilityWithoutReviving()
    {
        var steps = SHRRedRidingHoodGhosts.WaitingSteps().ToArray();
        Assert.Equal(new[]
        {
            new SHRRedRidingHoodGhosts.Step(RoleTypes.CrewmateGhost, false),
            new SHRRedRidingHoodGhosts.Step(RoleTypes.CrewmateGhost, null),
            new SHRRedRidingHoodGhosts.Step(RoleTypes.Crewmate, false),
            new SHRRedRidingHoodGhosts.Step(RoleTypes.Crewmate, null),
            new SHRRedRidingHoodGhosts.Step(RoleTypes.Crewmate, true),
        }, steps);
    }

    [Theory]
    [InlineData(true, true, RoleTypes.Crewmate)]
    [InlineData(true, false, RoleTypes.Crewmate)]
    [InlineData(false, true, RoleTypes.CrewmateGhost)]
    [InlineData(false, false, RoleTypes.Crewmate)]
    public void PrivateViewsDoNotRevealAnyImpostors(bool self, bool dead, RoleTypes expected)
        => Assert.Equal(expected, SHRRedRidingHoodGhosts.HiddenRole(self, dead));

    [Fact]
    public void HoodUsesCrewmateWithoutAnActiveAbilityTrigger()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.NiceRedRidingHood));
        Assert.Equal(RoleTypes.Crewmate, SHRRoleTypes.GetInitialRole(RoleId.NiceRedRidingHood));
        Assert.False(SHRCrewActionAbility.Supports(RoleId.NiceRedRidingHood));
    }
}
