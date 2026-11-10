using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHREvilScientistTests
{
    [Fact]
    public void EndingOneEffectKeepsTheOtherEffectsVisibilityRules()
    {
        var sources = new SHRHiddenSources();
        var jammer = new object();
        var scientist = new object();
        bool lighterActive = false;
        sources.Set(jammer, _ => true);
        sources.Set(scientist, _ => !lighterActive);
        Assert.True(sources.Matches(null));
        lighterActive = true;
        Assert.True(sources.Matches(null));
        sources.Remove(jammer);
        Assert.False(sources.Matches(null));
        lighterActive = false;
        Assert.True(sources.Matches(null));
        sources.Set(scientist, _ => false);
        Assert.Equal(1, sources.Count);
        sources.Remove(scientist);
        Assert.Equal(0, sources.Count);
        Assert.False(sources.Matches(null));
    }

    [Fact]
    public void ScientistUsesPhantomWhileKeepingImpostorVenting()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.EvilScientist));
        Assert.Equal(RoleTypes.Phantom, SHRRoleTypes.GetInitialRole(RoleId.EvilScientist));
        Assert.True(SHRCrewBoostAbility.Supports(RoleId.EvilScientist));
        Assert.False(SHRCrewBoostAbility.UsesEngineer(RoleId.EvilScientist));
        Assert.False(SHRVentRecovery.UsesOwnerReplay(RoleId.EvilScientist));
    }

    [Theory]
    [InlineData(false, true, false, false, false, false, true)]
    [InlineData(false, true, false, false, true, false, true)]
    [InlineData(false, true, false, false, true, true, false)]
    [InlineData(false, true, false, false, false, true, true)]
    [InlineData(false, true, true, false, false, false, false)]
    [InlineData(false, true, false, true, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, false)]
    public void VisibilityHonorsTheLighterSettingAndObserverState(bool self, bool vanilla, bool impostor, bool dead,
        bool lighterCanSee, bool lighterActive, bool expected)
        => Assert.Equal(expected, SHRPrivatePlayerEffects.ScientistShouldHide(self, vanilla, impostor, dead, lighterCanSee, lighterActive));
}
