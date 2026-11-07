using System.Linq;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHREvilGamblerTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(90)]
    public void EveryPossibleRollMatchesTheConfiguredSuccessPercentage(int chance)
    {
        var settings = new EvilGamblerData(15f, 45f, chance);
        float[] results = Enumerable.Range(0, 100).Select(roll => EvilGamblerAbility.SelectCooldown(settings, roll)).ToArray();
        Assert.Equal(chance, results.Count(value => value == 15f));
        Assert.Equal(100 - chance, results.Count(value => value == 45f));
        Assert.Equal(15f, results[chance - 1]);
        Assert.Equal(45f, results[chance]);
    }

    [Theory]
    [InlineData(2.5f, 60f)]
    [InlineData(60f, 2.5f)]
    [InlineData(30f, 30f)]
    public void BothOutcomesUseTheirConfiguredCooldowns(float success, float failure)
    {
        var settings = new EvilGamblerData(success, failure, 50);
        Assert.Equal(success, EvilGamblerAbility.SelectCooldown(settings, 0));
        Assert.Equal(failure, EvilGamblerAbility.SelectCooldown(settings, 99));
    }
}
