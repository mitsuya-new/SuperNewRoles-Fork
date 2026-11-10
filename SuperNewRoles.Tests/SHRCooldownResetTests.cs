using System.Collections.Generic;
using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRCooldownResetTests
{
    [Fact]
    public void ShieldFeedbackTargetsTheDefenderWhileOrdinaryCooldownResetsKeepTheAttacker()
    {
        Assert.Equal(71u, SHRPlayerOptions.KillResetTarget(17u, 71u));
        Assert.Equal(17u, SHRPlayerOptions.KillResetTarget(17u, null));
    }

    [Theory]
    [InlineData(false, 30f, 5f, 30f)]
    [InlineData(true, 30f, 5f, 5f)]
    [InlineData(false, 15f, 60f, 15f)]
    [InlineData(true, 15f, 60f, 60f)]
    public void DarkKillerUsesDedicatedCooldownOnlyDuringElectricalSabotage(bool electrical, float vanilla, float dark, float expected)
        => Assert.Equal(expected, SHRPlayerOptions.DarkKillerCooldown(electrical, vanilla, dark));

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(7f)]
    [InlineData(30f)]
    public void PhantomRoleResetPreservesMainCooldownAndRestoresDefaultSettings(float remaining)
    {
        float configured = 30f;
        float killTimer = remaining;
        RoleTypes role = RoleTypes.Phantom;
        var operations = new List<string>();
        SHRPlayerOptions.WriteResetSequence(true, true, RoleTypes.Phantom,
            () => { configured = remaining * 2f; operations.Add("options"); },
            value => { role = value; killTimer = 10f; operations.Add("role"); },
            () => { killTimer = configured / 2f; operations.Add("kill"); },
            () => operations.Add("ability"),
            () => { configured = 30f; operations.Add("restore"); });
        Assert.Equal(remaining, killTimer);
        Assert.Equal(30f, configured);
        Assert.Equal(RoleTypes.Phantom, role);
        Assert.Equal(new[] { "options", "role", "role", "kill", "ability", "restore" }, operations);
    }

    [Fact]
    public void ExhaustedAbilityKeepsImpostorControlsAndMainCooldownWithoutAbilityReset()
    {
        var operations = new List<string>();
        SHRPlayerOptions.WriteResetSequence(true, false, RoleTypes.Impostor,
            () => operations.Add("options"), value => operations.Add(value.ToString()),
            () => operations.Add("kill"), () => operations.Add("ability"), () => operations.Add("restore"));
        Assert.Equal(new[] { "options", "Impostor", "kill", "restore" }, operations);
    }

    [Theory]
    [InlineData(false, 0, 0f, true, 3, true)]
    [InlineData(false, 0, 1f, true, 3, false)]
    [InlineData(false, 3, 0f, true, 3, false)]
    [InlineData(false, 3, 0f, false, 3, true)]
    [InlineData(true, 0, 15f, true, 3, false)]
    [InlineData(true, 1, 15f, true, 3, true)]
    [InlineData(true, 1, 0f, true, 3, false)]
    [InlineData(true, 1, float.NaN, false, 3, false)]
    public void AmmoActionsRequireReadyKillForStorageAndRemainingKillCooldownForConsumption(
        bool consume, int count, float remaining, bool limited, int maximum, bool expected)
        => Assert.Equal(expected, MagazinerAbility.CanChangeSHRAmmo(consume, count, remaining, limited, maximum));
}
