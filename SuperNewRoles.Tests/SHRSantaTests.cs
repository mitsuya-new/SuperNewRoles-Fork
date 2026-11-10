using System.Linq;
using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRSantaTests
{
    [Fact]
    public void SantaUsesPetAndEngineerCooldownWithoutVenting()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Santa));
        Assert.Equal(RoleTypes.Engineer, SHRRoleTypes.GetInitialRole(RoleId.Santa));
        Assert.True(SHRCrewActionAbility.UsesEngineer(RoleId.Santa));
        Assert.True(SHRVentRecovery.UsesOwnerReplay(RoleId.Santa));
        Assert.True(SHRSamuraiRestrictions.RejectVent(RoleId.Santa, false, VentilationSystem.Operation.Enter));
    }

    [Fact]
    public void WeightedTicketsExcludeUnsupportedGiftsOnlyInSHR()
    {
        var roles = new[] { (RoleId.Sheriff, 10), (RoleId.Medium, 100), (RoleId.NiceGuesser, 5) };
        var shr = SantaAbility.BuildTickets(roles, true);
        Assert.Equal(3, shr.Count);
        Assert.Equal(2, shr.Count(role => role == RoleId.Sheriff));
        Assert.DoesNotContain(RoleId.Medium, shr);
        Assert.Equal(23, SantaAbility.BuildTickets(roles, false).Count);
    }

    [Fact]
    public void ZeroWeightFallbackCannotSelectUnsupportedRoles()
    {
        var roles = new[] { (RoleId.Sheriff, 0), (RoleId.Medium, 0), (RoleId.NiceGuesser, 0) };
        Assert.Equal(new[] { RoleId.Sheriff, RoleId.NiceGuesser }, SantaAbility.BuildTickets(roles, true));
        Assert.Empty(SantaAbility.BuildTickets(new[] { (RoleId.Medium, 100) }, true));
        Assert.True(SantaAbility.IsUnavailableSHROption("SantaMediumPercentage"));
        Assert.False(SantaAbility.IsUnavailableSHROption("SantaSheriffPercentage"));
    }
}
