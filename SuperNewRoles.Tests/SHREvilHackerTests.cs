using SuperNewRoles.Mode;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHREvilHackerTests
{
    [Fact]
    public void PetOpensEachPageThenClosesTheAdminView()
    {
        var pages = new SHRAdminPages();
        Assert.False(pages.Open);
        pages.Next(2);
        Assert.Equal(0, pages.Page);
        pages.Next(2);
        Assert.Equal(1, pages.Page);
        pages.Next(2);
        Assert.False(pages.Open);
    }

    [Fact]
    public void ShrinkingTheSnapshotKeepsTheActivePageWithinTheNewRoomList()
    {
        var pages = new SHRAdminPages();
        pages.Next(3);
        pages.Next(3);
        pages.Next(3);
        pages.Clamp(1);
        Assert.Equal(0, pages.Page);
        pages.Clamp(0);
        Assert.False(pages.Open);
    }

    [Fact]
    public void MeetingRotationIncludesEveryPageWithoutClosingTheView()
    {
        var pages = new SHRAdminPages();
        pages.Rotate(3);
        Assert.False(pages.Open);
        pages.Next(3);
        pages.Rotate(3);
        Assert.Equal(1, pages.Page);
        pages.Rotate(3);
        Assert.Equal(2, pages.Page);
        pages.Rotate(3);
        Assert.Equal(0, pages.Page);
        pages.Close();
        Assert.False(pages.Open);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void CommsInformationMatchesTheNormalAdvancedAdminGate(bool comms, bool enhanced, bool duringComms, bool expected)
        => Assert.Equal(expected, SHRAdminPages.CanRead(comms, enhanced, duringComms));

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void MeetingAdminRequiresBothSettings(bool enhanced, bool duringMeeting, bool expected)
        => Assert.Equal(expected, SHRAdminPages.CanReadInMeeting(enhanced, duringMeeting));
}
