using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SuperNewRoles.Mode;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRChatCommandsTests
{
    [Fact]
    public void SwitchLongRepliesUseShortLinesAndByteLimitedMessagesWithoutChangingTextOrTags()
    {
        string text = "<size=80%>\n" + string.Concat(Enumerable.Repeat("自陣営以外のキルができる陣営が全滅した上で、自陣営とクルーが同数以下になれば勝利します。", 10)) + "\n人数:1\n</size>";
        string wrapped = SHRChatCommands.WrapLongReplyLines(text);
        Assert.Equal(text.Replace("\n", ""), wrapped.Replace("\n", ""));
        Assert.StartsWith("<size=80%>\n", wrapped);
        foreach (string line in Regex.Replace(wrapped, "<[^>]*>", "").Split('\n')) Assert.True(line.Length <= 24);
        var parts = SHRChatCommands.SplitReply(SHRChatCommands.CommanderTitle, wrapped).ToArray();
        Assert.True(parts.Length > 1);
        for (int index = 0; index < parts.Length; index++)
        {
            string name = SHRChatCommands.FormatReplyName(index == 0 ? SHRChatCommands.CommanderTitle : null, parts[index]);
            Assert.True(Encoding.UTF8.GetByteCount(name + " ") <= 1000);
        }
        Assert.Equal(Regex.Replace(wrapped, "<[^>]*>", ""), string.Concat(parts.Select(part => Regex.Replace(part, "<[^>]*>", ""))));
    }

    [Fact]
    public void ShortSwitchSettingsKeepTheirLineBreaks()
    {
        const string text = "\n\n<size=70%>無限ジャッカル:オン\n  サイドキックタイプ:サイドキック</size>\n";
        Assert.Equal(text, SHRChatCommands.WrapLongReplyLines(text));
    }

    [Fact]
    public void RepliesRespectCombinedUtf8BudgetAndPreserveRichTextAndUnicode()
    {
        string content = string.Concat(Enumerable.Repeat("役職説明😀\n", 200));
        string text = "<align=left><size=80%><color=#ff0000>" + content + "</color></size></align>";
        var parts = SHRChatCommands.SplitReply(SHRChatCommands.CommanderTitle, text).ToArray();
        Assert.True(parts.Length > 1);
        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index];
            Assert.True(Encoding.UTF8.GetByteCount(SHRChatCommands.FormatReplyName(index == 0 ? SHRChatCommands.CommanderTitle : null, part) + " ") <= 1000);
            Assert.StartsWith("<align=left><size=80%><color=#ff0000>", part);
            Assert.EndsWith("</color></size></align>", part);
            Assert.DoesNotContain("\uFFFD", Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(part)));
        }
        Assert.Equal(content, string.Concat(parts.Select(part => Regex.Replace(part, "<[^>]*>", ""))));
    }

    [Fact]
    public void ShortRepliesAreUnchanged()
        => Assert.Equal(new[] { "<b>説明</b>" }, SHRChatCommands.SplitReply(SHRChatCommands.CommanderTitle, "<b>説明</b>"));

    [Fact]
    public void NameReplyContainsTitleAndNumericBody()
        => Assert.Equal("<align=left>SHR<br>人数:12345", SHRChatCommands.FormatReplyName("SHR", "人数:12345"));

    [Fact]
    public void SettingsSplitAtLineBoundariesAndContinuationHasNoTitleOrLeadingBlankLine()
    {
        string[] lines = Enumerable.Repeat("  無限ジャッカル:オン\n  サイドキックタイプ:サイドキック\n", 30)
            .SelectMany(value => value.Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        string text = "<size=70%>" + string.Join("\n", lines) + "</size>";
        var parts = SHRChatCommands.SplitReply(SHRChatCommands.CommanderTitle, text).ToArray();
        Assert.True(parts.Length > 1);
        foreach (string part in parts)
            foreach (string line in Regex.Replace(part, "<[^>]*>", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(line, lines);
        Assert.Equal("<align=left>本文", SHRChatCommands.FormatReplyName(null, "本文"));
        Assert.Equal(string.Join("\n", lines), string.Concat(parts.Select(part => Regex.Replace(part, "<[^>]*>", ""))));
    }

    [Theory]
    [InlineData("/cmd", "List", "")]
    [InlineData(" /CMD ", "List", "")]
    [InlineData("/commands", "List", "")]
    [InlineData("/gr", "EnabledRoles", "")]
    [InlineData("/getinroles", "EnabledRoles", "")]
    [InlineData("/ri トイレファン", "RoleInfo", "トイレファン")]
    [InlineData("/ri Nice Guesser", "RoleInfo", "Nice Guesser")]
    [InlineData("/ｒｉ　Nice Guesser", "RoleInfo", "Nice Guesser")]
    [InlineData("/roleinfo Sheriff", "RoleInfo", "Sheriff")]
    [InlineData("/ri", "RoleInfo", "")]
    [InlineData("/bt 赤 シェリフ", "None", "赤 シェリフ")]
    [InlineData("/cmdfoo", "None", "")]
    [InlineData("hello", "None", "")]
    [InlineData(null, "None", "")]
    public void CommandsPreserveRoleNamesAndDoNotInterceptOtherChat(string? text, string command, string argument)
    {
        Assert.Equal(command, SHRChatCommands.Parse(text, out var actual).ToString());
        Assert.Equal(argument, actual);
    }

    [Theory]
    [InlineData(true, false, true, 1, 100, true)]
    [InlineData(true, false, true, 1, 10, true)]
    [InlineData(true, false, true, 0, 100, false)]
    [InlineData(true, false, true, 1, 0, false)]
    [InlineData(false, false, true, 1, 100, false)]
    [InlineData(true, true, true, 1, 100, false)]
    [InlineData(true, false, false, 1, 100, false)]
    public void EnabledListUsesVisibleSupportedAssignmentsForTheCurrentMap(bool supported, bool hidden,
        bool map, int count, int probability, bool expected)
        => Assert.Equal(expected, SHRChatCommands.IsEnabled(supported, hidden, map, count, probability));

    [Fact]
    public void CommandListPreservesLegacyColorsSizingAndSeparators()
    {
        string text = SHRChatCommands.FormatCommandList("コマンド一覧\n/cmd: 一覧\n/ri 役職名: 役職情報", "役職情報");
        Assert.StartsWith("<align=left><size=60%>", text);
        Assert.Contains("<color=#4d4398><size=80%>|", text);
        Assert.Contains("<size=100%><b>役職情報</b></size>", text);
        Assert.Contains("<color=#4d4398><size=80%>/cmd</size></color>", text);
        Assert.EndsWith("</size></align>", text);
        Assert.False(SHRChatCommands.IsCommand(text));
        Assert.DoesNotContain("【SHR】", text);
    }

    [Fact]
    public void RoleInformationUsesTheLegacyTitleTeamIntroDescriptionAndSettingsOrder()
    {
        Assert.Equal("<align=left><size=180%><color=yellow>シェリフ</color></size></align>",
            SHRChatCommands.FormatRoleTitle("<color=yellow>シェリフ</color>"));
        string text = SHRChatCommands.FormatRoleInfo("【クルーメイト陣営】", "一言", "説明", "役職設定", "設定値", "Sheriff");
        Assert.Equal("<align=left><size=80%>\n【クルーメイト陣営】\n</size><size=100%>「一言」</size>\n\n"
            + "<size=80%>説明\n</size><size=70%>役職設定:\n設定値\n<color=#00000000>Sheriff</color></size></align>", text);
        Assert.DoesNotContain("【SHR】", text);
    }
}
