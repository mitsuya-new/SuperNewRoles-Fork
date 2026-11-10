using SuperNewRoles.Roles.CrewMate;
using Xunit;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using AmongUs.GameOptions;

namespace SuperNewRoles.Tests;

public class SHRPteranodonTests
{
    [Fact]
    public void FlightUsesEngineerPetAndVentControlsOnlyOnAirship()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Pteranodon));
        Assert.True(SHRCrewActionAbility.Supports(RoleId.Pteranodon));
        Assert.True(SHRCrewActionAbility.UsesEngineer(RoleId.Pteranodon));
        Assert.Equal(RoleTypes.Engineer, SHRRoleTypes.GetInitialRole(RoleId.Pteranodon));
        for (byte map = 0; map < 6; map++)
            Assert.Equal(map == 4, SHRRoleTypes.ShouldShowInSettings(RoleId.Pteranodon, true, map));
    }

    [Fact]
    public void FlightFramesStopAfterTwoSecondsAndDoNotReplayMissedFrames()
    {
        var frames = new SHRFlightFrames();
        Assert.False(frames.Advance(-1));
        Assert.False(frames.Advance(0));
        Assert.True(frames.Advance(0.1f));
        Assert.False(frames.Advance(0.1f));
        Assert.True(frames.Advance(1.9f));
        Assert.False(frames.Advance(1f));
        Assert.True(frames.Advance(2f));
        Assert.False(frames.Advance(100f));
        frames.Reset();
        Assert.True(frames.Advance(0.1f));
    }

    [Fact]
    public void FlightNeverSendsMoreThanTwentyIntermediateUpdates()
    {
        var frames = new SHRFlightFrames();
        int count = 0;
        for (int tick = 0; tick < 10000; tick++)
            if (frames.Advance(tick * 0.01f)) count++;
        Assert.Equal(SHRFlightFrames.MaximumFrames, count);
    }
    [Theory]
    [InlineData(0f, 10f, false, true)]
    [InlineData(0.9f, 10f, false, true)]
    [InlineData(10f, 0.9f, false, false)]
    [InlineData(0.901f, 10f, false, null)]
    [InlineData(10f, 0.901f, false, null)]
    [InlineData(0f, 10f, true, null)]
    [InlineData(10f, 0f, true, null)]
    [InlineData(0.5f, 0.5f, false, true)]
    public void FlightDeparturePreservesTheNormalRangeAndRejectsTheCurrentPassenger(
        float left, float right, bool riding, bool? expected)
        => Assert.Equal(expected, PteranodonAbility.SelectDeparture(left, right, riding));
}
