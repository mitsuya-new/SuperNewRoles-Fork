using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRStuntmanTests
{
    [Fact]
    public void OneShieldBlocksOnlyOneAttemptEvenWhenRequestsAreRepeated()
    {
        var ability = new StuntmanAbility(1);
        Assert.True(ability.TryConsumeShield());
        Assert.Equal(0, ability.Count);
        for (int i = 0; i < 10; i++) Assert.False(ability.TryConsumeShield());
        Assert.Equal(0, ability.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void CountSynchronizationDoesNotConsumeAnotherShield(int count)
    {
        var ability = new StuntmanAbility(count);
        if (count > 0) Assert.True(ability.TryConsumeShield());
        int remaining = ability.Count;
        StuntmanAbility.RpcSetSHRShieldCount(ability, remaining);
        StuntmanAbility.RpcSetSHRShieldCount(ability, remaining);
        Assert.Equal(remaining, ability.Count);
    }
}
