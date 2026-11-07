using System;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRAbilityTriggerTests
{
    [Fact]
    public void InitialCooldownBlocksActivationThenMeetingResetReplacesPreviousCooldown()
    {
        int calls = 0;
        var ability = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Phantom, _ => calls++, cooldown: 30f);
        ability.ResetCooldown(100f, 10f);
        Assert.Equal(10f, ability.RemainingCooldown(100f));
        Assert.False(ability.TryActivate(SHRAbilityTrigger.Phantom, 109f));
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Phantom, 110f));
        Assert.Equal(30f, ability.RemainingCooldown(110f));
        ability.ResetCooldown(115f, 0f);
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Phantom, 115f));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.Immediate, 0f)]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.OneThird, 9f)]
    [InlineData(SuperNewRoles.CustomOptions.Categories.InitialCooldownType.TenSeconds, 10f)]
    public void VanillaKillInitialCooldownUsesSharedGameSetting(
        SuperNewRoles.CustomOptions.Categories.InitialCooldownType setting, float expected)
        => Assert.Equal(expected, SuperNewRoles.Mode.SHRKillRequests.InitialCooldown(27f, setting));

    [Fact]
    public void ShapeshiftRevertIsDistinctFromTargetSelection()
    {
        Assert.Equal(SHRAbilityTrigger.Shapeshift, SuperNewRoles.Mode.SHRAbilityTriggerPatches.GetShapeshiftTrigger(false));
        Assert.Equal(SHRAbilityTrigger.ShapeshiftSelf, SuperNewRoles.Mode.SHRAbilityTriggerPatches.GetShapeshiftTrigger(true));
        int calls = 0;
        var ability = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Shapeshift, (_, target) =>
        {
            Assert.Null(target);
            calls++;
        });
        Assert.False(ability.TryActivate(SHRAbilityTrigger.ShapeshiftSelf, 1f));
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Shapeshift, 1f));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ShapeAndPetShareAbilityCooldown()
    {
        int calls = 0;
        var ability = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Shapeshift | SHRAbilityTrigger.Pet,
            _ => calls++, cooldown: 5f);
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Shapeshift, 1f));
        Assert.False(ability.TryActivate(SHRAbilityTrigger.Pet, 5f));
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Pet, 6f));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void PetAndPhantomShareCooldownAndBlockReentrantActivation()
    {
        int calls = 0;
        SHRAbilityTriggerAbility ability = null!;
        ability = new(SHRAbilityTrigger.Pet | SHRAbilityTrigger.Phantom, source =>
        {
            calls++;
            Assert.False(ability.TryActivate(source, 10f));
        }, cooldown: 5f);
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Pet, 10f));
        Assert.False(ability.TryActivate(SHRAbilityTrigger.Phantom, 14.9f));
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Phantom, 15f));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void UnavailableAbilityDoesNotConsumeCooldown()
    {
        bool available = false;
        int calls = 0;
        var ability = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => calls++, () => available, 10f);
        Assert.False(ability.TryActivate(SHRAbilityTrigger.Phantom, 1f));
        Assert.False(ability.TryActivate(SHRAbilityTrigger.Pet, 1f));
        available = true;
        Assert.True(ability.TryActivate(SHRAbilityTrigger.Pet, 1f));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DuplicateRequestsAreDebouncedWithoutSharingStateBetweenPlayers()
    {
        var first = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => { });
        var second = new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => { });
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
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => { }, cooldown: cooldown));
}
