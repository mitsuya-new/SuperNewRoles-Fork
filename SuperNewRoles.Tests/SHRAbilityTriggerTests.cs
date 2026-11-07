using System;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class ShrAbilityTriggerTests
{
    [Fact]
    public void InitialCooldownBlocksActivationThenMeetingResetReplacesPreviousCooldown()
    {
        int calls = 0;
        var ability = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Phantom, _ => calls++, cooldown: 30f);
        ability.ResetCooldown(100f, 10f);
        Assert.Equal(10f, ability.RemainingCooldown(100f));
        Assert.False(ability.TryActivate(ShrAbilityTrigger.Phantom, 109f));
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Phantom, 110f));
        Assert.Equal(30f, ability.RemainingCooldown(110f));
        ability.ResetCooldown(115f, 0f);
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Phantom, 115f));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.Immediate, 0f)]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.OneThird, 9f)]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.TenSeconds, 10f)]
    public void VanillaKillInitialCooldownUsesSharedGameSetting(
        SuperNewRoles.CustomOptions.Categories.InitialCooldownType setting, float expected)
        => Assert.Equal(expected, SuperNewRoles.Mode.ShrKillRequests.InitialCooldown(27f, setting));

    [Fact]
    public void ShapeshiftRevertIsDistinctFromTargetSelection()
    {
        Assert.Equal(ShrAbilityTrigger.Shapeshift, SuperNewRoles.Mode.ShrAbilityTriggerPatches.GetShapeshiftTrigger(false));
        Assert.Equal(ShrAbilityTrigger.ShapeshiftSelf, SuperNewRoles.Mode.ShrAbilityTriggerPatches.GetShapeshiftTrigger(true));
        int calls = 0;
        var ability = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Shapeshift, (_, target) =>
        {
            Assert.Null(target);
            calls++;
        });
        Assert.False(ability.TryActivate(ShrAbilityTrigger.ShapeshiftSelf, 1f));
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Shapeshift, 1f));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ShapeAndPetShareAbilityCooldown()
    {
        int calls = 0;
        var ability = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Shapeshift | ShrAbilityTrigger.Pet,
            _ => calls++, cooldown: 5f);
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Shapeshift, 1f));
        Assert.False(ability.TryActivate(ShrAbilityTrigger.Pet, 5f));
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Pet, 6f));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void PetAndPhantomShareCooldownAndBlockReentrantActivation()
    {
        int calls = 0;
        ShrAbilityTriggerAbility ability = null!;
        ability = new(ShrAbilityTrigger.Pet | ShrAbilityTrigger.Phantom, source =>
        {
            calls++;
            Assert.False(ability.TryActivate(source, 10f));
        }, cooldown: 5f);
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Pet, 10f));
        Assert.False(ability.TryActivate(ShrAbilityTrigger.Phantom, 14.9f));
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Phantom, 15f));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void UnavailableAbilityDoesNotConsumeCooldown()
    {
        bool available = false;
        int calls = 0;
        var ability = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Pet, _ => calls++, () => available, 10f);
        Assert.False(ability.TryActivate(ShrAbilityTrigger.Phantom, 1f));
        Assert.False(ability.TryActivate(ShrAbilityTrigger.Pet, 1f));
        available = true;
        Assert.True(ability.TryActivate(ShrAbilityTrigger.Pet, 1f));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DuplicateRequestsAreDebouncedWithoutSharingStateBetweenPlayers()
    {
        var first = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Pet, _ => { });
        var second = new ShrAbilityTriggerAbility(ShrAbilityTrigger.Pet, _ => { });
        Assert.True(first.TryConsumeInput(10f));
        Assert.False(first.TryConsumeInput(10.1f));
        Assert.True(second.TryConsumeInput(10.1f));
        Assert.True(first.TryConsumeInput(10.4f));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidCooldownIsRejected(float cooldown)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ShrAbilityTriggerAbility(ShrAbilityTrigger.Pet, _ => { }, cooldown: cooldown));
}
