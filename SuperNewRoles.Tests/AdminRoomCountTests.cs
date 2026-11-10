using SuperNewRoles.Modules;
using Xunit;

namespace SuperNewRoles.Tests;

public class AdminRoomCountTests
{
    [Fact]
    public void DuplicateCollidersAndHiddenPlayersDoNotInflateTheRoomCount()
    {
        var count = new AdminRoomCount();
        Assert.True(count.AddPlayer(1, false, true));
        Assert.False(count.AddPlayer(1, false, true));
        Assert.False(count.AddPlayer(2, true, true));
        Assert.True(count.AddPlayer(3, false, false));
        Assert.Equal(2, count.Total);
        Assert.Equal(1, count.Impostors);
        Assert.Equal(0, count.Corpses);
    }

    [Fact]
    public void MultipleBodiesRemainSeparateIconsWithoutCountingAsLivingImpostors()
    {
        var count = new AdminRoomCount();
        count.AddPlayer(1, false, true);
        count.AddCorpse();
        count.AddCorpse();
        Assert.Equal(3, count.Total);
        Assert.Equal(1, count.Impostors);
        Assert.Equal(2, count.Corpses);
    }

    [Theory]
    [InlineData(false, true, true, 0, 0)]
    [InlineData(true, false, false, 0, 0)]
    [InlineData(true, true, false, 1, 0)]
    [InlineData(true, false, true, 0, 1)]
    [InlineData(true, true, true, 1, 1)]
    public void DetailsRespectBothEnhancedAdminAndEachDisplayOption(bool enhanced, bool impostors, bool corpses, int expectedImpostors, int expectedCorpses)
    {
        var count = new AdminRoomCount();
        count.AddPlayer(1, false, true);
        count.AddCorpse();
        Assert.Equal((expectedImpostors, expectedCorpses), count.VisibleDetails(enhanced, impostors, corpses));
        Assert.Equal(2, count.Total);
    }
}
