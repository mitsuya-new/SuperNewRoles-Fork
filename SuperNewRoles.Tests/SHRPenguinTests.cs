using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRPenguinTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void NormalKillOptionStillAllowsKillingWhileHoldingATarget(bool canDefaultKill, bool holding, bool expected)
        => Assert.Equal(expected, PenguinAbility.CanKillNormally(canDefaultKill, holding));
}
