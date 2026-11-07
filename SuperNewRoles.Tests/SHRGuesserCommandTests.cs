using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class ShrGuesserCommandTests
{
    [Theory]
    [InlineData("A suddenly fainted...")]
    [InlineData("A suddenly fainted...\n")]
    [InlineData("\nA suddenly fainted...\r\n")]
    public void AnnouncementReservesOneBlankLineBelowTheLargeSenderTitle(string body)
        => Assert.Equal("\nA suddenly fainted...\n", ShrMeetingChat.FormatAnnouncementText(body));

    [Theory]
    [InlineData("/bt 赤 シェリフ", "赤", "シェリフ")]
    [InlineData("  /BT   red  Evil Guesser  ", "red", "Evil Guesser")]
    [InlineData("/bt\t0\tSheriff", "0", "Sheriff")]
    [InlineData("/bt　青　イビルゲッサー", "青", "イビルゲッサー")]
    public void ParseKeepsColorAndEntireRoleName(string text, string color, string role)
    {
        Assert.True(ShrGuesserCommands.TryParse(text, out var actualColor, out var actualRole));
        Assert.Equal(color, actualColor);
        Assert.Equal(role, actualRole);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/bt")]
    [InlineData("/bt 赤")]
    [InlineData("/bt 赤   ")]
    [InlineData("/btfoo 赤 Sheriff")]
    [InlineData("hello /bt 赤 Sheriff")]
    public void MalformedCommandsAreNotShots(string text)
        => Assert.False(ShrGuesserCommands.TryParse(text, out _, out _));

    [Theory]
    [InlineData("/bt", true)]
    [InlineData(" /BT 赤 Sheriff", true)]
    [InlineData("/btfoo", false)]
    [InlineData("通常の発言", false)]
    public void OnlyTheExactCommandTokenIsHidden(string text, bool expected)
        => Assert.Equal(expected, ShrGuesserCommands.IsCommand(text));

    [Theory]
    [InlineData("Evil Guesser", RoleId.EvilGuesser, "イビルゲッサー")]
    [InlineData("ＥｖｉｌＧｕｅｓｓｅｒ", RoleId.EvilGuesser, "イビルゲッサー")]
    [InlineData("イビルゲッサー", RoleId.EvilGuesser, "<color=red>イビルゲッサー</color>")]
    [InlineData("crew", RoleId.Crewmate, "クルーメイト")]
    [InlineData("インポ", RoleId.Impostor, "インポスター")]
    public void RoleNamesSupportTranslationsEnglishAndVanillaAliases(string input, RoleId role, string translated)
        => Assert.True(ShrGuesserCommands.MatchesRole(input, role, translated));

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Shr1DeathViewOnlyHidesOtherLivingSpeakersFromLivingViewers(
        bool viewerDead, bool speakerDead, bool self, bool expected)
        => Assert.Equal(expected, ShrMeetingChat.HideSpeaker(viewerDead, speakerDead, self));
}
