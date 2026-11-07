using SuperNewRoles.Modules;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRSettingsDisplayTests
{
    [Theory]
    [InlineData(DisplayModeId.Default, true)]
    [InlineData(DisplayModeId.SuperHostRolesOnly, true)]
    [InlineData(DisplayModeId.All, true)]
    [InlineData(DisplayModeId.None, false)]
    [InlineData(DisplayModeId.BattleRoyal, false)]
    [InlineData(DisplayModeId.WCBattleRoyal, false)]
    public void SHRShowsNormalSettingsAndItsOwnSettings(DisplayModeId target, bool expected)
    {
        Assert.Equal(expected, DisplayMode.HasMode(DisplayModeId.SuperHostRolesOnly, target));
    }

    [Fact]
    public void NormalModeStillHidesSHROnlySettings()
    {
        Assert.False(DisplayMode.HasMode(DisplayModeId.Default, DisplayModeId.SuperHostRolesOnly));
        Assert.True(DisplayMode.HasMode(DisplayModeId.Default, DisplayModeId.Default));
    }
}
