using SuperNewRoles.Mode;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRBakeryExileTests
{
    [Theory]
    [InlineData(true, new byte[] { 1, 1, 2, 2 }, true)]
    [InlineData(true, new byte[] { 1, 1, 253, 253 }, true)]
    [InlineData(false, new byte[] { 253, 253, 253, 1 }, false)]
    [InlineData(true, new byte[] { 253, 253, 253, 253 }, false)]
    [InlineData(true, new byte[] { 252, 254, 255 }, false)]
    [InlineData(true, new byte[] { 1, 1, 2, 2, 253, 252, 254, 255 }, true)]
    public void SkipReasonUsesFinalTieFlagAndIgnoresInvalidVotes(bool tie, byte[] votes, bool expected)
        => Assert.Equal(expected, SHRBakeryExile.IsTieSkip(tie, votes));

    [Theory]
    [InlineData(true, 5, (byte)3, (byte)0, (byte)3)]
    [InlineData(true, 5, null, (byte)0, (byte)0)]
    [InlineData(true, 4, null, (byte)2, (byte)2)]
    [InlineData(false, 5, (byte)3, (byte)0, null)]
    [InlineData(true, 3, (byte)2, (byte)0, null)]
    [InlineData(true, 3, null, (byte)0, null)]
    public void DisplayUsesRealExileOrHostAndPreservesSmallLobbyProtection(bool alive, int count, byte? exiled, byte host, byte? expected)
        => Assert.Equal(expected, SHRBakeryExile.SelectCarrier(alive, count, exiled, host));

    [Fact]
    public void NoExileDisplayIncludesNoticeAndHidesTheVanillaSuffix()
    {
        Assert.Equal("<size=3.75>投票スキップ(スキップ)<br>パン屋がパンを焼いたようだ...<br><sub>パン屋のメッセージ用です。実際に追放はされていません。</sub></size><size=0>",
            SHRBakeryExile.FormatName("投票スキップ(スキップ)", "パン屋がパンを焼いたようだ...", true,
                "パン屋のメッセージ用です。実際に追放はされていません。"));
    }

    [Fact]
    public void ActualExileDisplayDoesNotClaimTheExileWasFake()
    {
        Assert.Equal("<size=3.45>Crewが追放された...<br>パン屋がパンを焼いたようだ...</size><size=0>",
            SHRBakeryExile.FormatName("Crewが追放された...", "パン屋がパンを焼いたようだ...", false, "not shown"));
    }

    [Theory]
    [InlineData(false, "<size=2.3>")]
    [InlineData(true, "<size=2.5>")]
    public void FixedFontSizeUsesTheMapBaselineWithoutPercentageTags(bool fake, string expected)
    {
        string name = SHRBakeryExile.FormatName("result", "bread", fake, "notice", 2f);
        Assert.StartsWith(expected, name);
        Assert.DoesNotContain("%", name);
    }
}
