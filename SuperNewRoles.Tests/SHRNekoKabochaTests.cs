using SuperNewRoles.Roles.Impostor;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRNekoKabochaTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void RevengeOptionsIndependentlyControlEachKillerTeam(bool crew, bool neutral, bool impostor)
    {
        for (int mask = 0; mask < 8; mask++)
        {
            var settings = new NekoKabochaData((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, false);
            bool expected = crew ? settings.CanRevengeCrewmate : neutral ? settings.CanRevengeNeutral : settings.CanRevengeImpostor;
            Assert.Equal(expected, NekoKabochaRevenge.CanRevenge(settings, crew, neutral, impostor));
        }
    }
}
