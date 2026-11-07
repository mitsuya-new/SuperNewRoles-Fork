using SuperNewRoles.Mode;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRMadKillerTests
{
    [Theory]
    [InlineData(false, true, 2, 1, 1, false, false)]
    [InlineData(false, true, 2, 1, 1, true, true)]
    [InlineData(false, true, 2, 1, 3, true, false)]
    [InlineData(false, true, 2, 1, 2, true, false)]
    [InlineData(false, false, 2, 1, 1, true, false)]
    [InlineData(true, true, 2, 1, 1, true, false)]
    [InlineData(false, true, 2, null, 1, true, false)]
    public void PromotionRequiresTheOwnersActualDeathRatherThanJustTheVoteResult(
        bool awakened, bool alive, byte player, int? owner, byte exiled, bool ownerDead, bool expected)
        => Assert.Equal(expected, MadKillerAbility.ShouldAwakenForExile(awakened, alive, player, (byte?)owner, exiled, ownerDead));

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void MadKillerVisibilityIsHiddenOnlyBeforePromotion(bool awake, bool hide, bool expected)
        => Assert.Equal(expected, SHRMadKillerSupport.CanIdentify(awake, hide));

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    public void ImpostorVisibilityHonorsTheDependentOptionOnlyBeforePromotion(bool awake, bool hide, bool hideImpostors, bool expected)
        => Assert.Equal(expected, SHRMadKillerSupport.CanIdentifyImpostors(awake, hide, hideImpostors));
}
