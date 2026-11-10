using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SuperNewRoles.HelpMenus;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;

namespace SuperNewRoles.Mode;

public static class SHRChatCommands
{
    internal enum Command { None, List, EnabledRoles, RoleInfo }
    internal const string CommanderTitle = "<size=200%><color=#ffa500>Super</color><color=#ff0000>Host</color><color=#00ff00>Roles</color></size>";
    private const string Separator = "<color=#4d4398><size=80%>|-----------------------------------------------------------------------------|</size></color>\n";
    private static readonly Dictionary<byte, (int GameId, Queue<(string Title, string Text)> Messages)> pendingReplies = new();

    internal static Command Parse(string text, out string argument)
    {
        argument = "";
        if (string.IsNullOrWhiteSpace(text)) return Command.None;
        string value = text.Trim();
        int separator = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '　' });
        string name = separator < 0 ? value : value[..separator];
        if (separator >= 0) argument = value[(separator + 1)..].Trim();
        return name.Normalize(NormalizationForm.FormKC).ToLowerInvariant() switch
        {
            "/cmd" or "/commands" => Command.List,
            "/gr" or "/getinroles" => Command.EnabledRoles,
            "/ri" or "/roleinfo" => Command.RoleInfo,
            _ => Command.None,
        };
    }

    internal static bool IsCommand(string text) => Parse(text, out _) != Command.None;

    internal static bool IsEnabled(bool supported, bool hidden, bool availableOnMap, int count, int percentage)
        => supported && !hidden && availableOnMap && count > 0 && percentage > 0;

    internal static string FormatCommandList(string help, string heading)
    {
        var lines = help.Replace("\r", "").Split('\n');
        int firstCommand = Array.FindIndex(lines, line => line.StartsWith('/'));
        if (firstCommand < 0) firstCommand = lines.Length;
        var result = new StringBuilder("<align=left><size=60%>");
        result.Append(Separator).Append(string.Join("\n", lines.Take(firstCommand))).Append("\n\n").Append(Separator)
            .Append($"<size=100%><b>{heading}</b></size>\n");
        foreach (var line in lines.Skip(firstCommand))
        {
            int colon = line.IndexOf(':');
            result.Append(colon < 0 ? line : $"<color=#4d4398><size=80%>{line[..colon]}</size></color> : \n{line[(colon + 1)..].TrimStart()}")
                .Append("\n\n");
        }
        return result.Append(Separator).Append("</size></align>").ToString();
    }

    internal static string FormatRoleTitle(string coloredName)
        => $"<align=left><size=180%>{coloredName}</size></align>";

    internal static string FormatRoleInfo(string team, string intro, string description, string settingsTitle, string settings, string roleKey)
        => $"<align=left><size=80%>\n{team}\n</size>"
            + (string.IsNullOrWhiteSpace(intro) ? "" : $"<size=100%>「{intro}」</size>\n\n")
            + $"<size=80%>{description}\n</size>"
            + $"<size=70%>{settingsTitle}:\n{settings}\n<color=#00000000>{roleKey}</color></size></align>";

    public static void Handle(PlayerControl source, string text)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || source?.Data == null || source.Data.Disconnected) return;
        var command = Parse(text, out string argument);
        switch (command)
        {
            case Command.List:
                Reply(source, CommanderTitle, FormatCommandList(ModTranslation.GetString("SHR.Commands.Help"),
                    ModTranslation.GetString("SHR.Commands.RoleCommandsHeading")));
                break;
            case Command.EnabledRoles:
                Reply(source, CommanderTitle, EnabledRoles());
                break;
            case Command.RoleInfo:
                RoleInfo(source, argument);
                break;
        }
    }

    internal static void Reply(PlayerControl source, string title, string text)
    {
        var platform = AmongUsClient.Instance.GetClientFromCharacter(source)?.PlatformData?.Platform
            ?? AmongUsClient.Instance.GetClient(source.OwnerId)?.PlatformData?.Platform;
        bool isSwitch = platform == null || platform == Platforms.Switch;
        if (isSwitch) text = WrapLongReplyLines(text);
        Logger.Info($"Command reply: player={source.PlayerId}, platform={platform?.ToString() ?? "Unknown"}, shortLines={isSwitch}, longestLine={text.Split('\n').Max(VisibleReplyLength)}", "SHRChatCommands");
        int gameId = AmongUsClient.Instance.GameId;
        bool running = pendingReplies.TryGetValue(source.PlayerId, out var pending) && pending.GameId == gameId;
        var messages = running ? pending.Messages : new Queue<(string Title, string Text)>();
        bool first = true;
        foreach (string part in SplitReply(title, text))
        {
            messages.Enqueue((first ? title : null, part));
            first = false;
        }
        if (running || messages.Count == 0) return;
        pendingReplies[source.PlayerId] = (gameId, messages);
        SendNext();

        void SendNext()
        {
            if (!pendingReplies.TryGetValue(source.PlayerId, out var current) || current.Messages != messages) return;
            if (AmongUsClient.Instance?.AmHost != true || AmongUsClient.Instance.GameId != gameId
                || !ModeManager.IsMode(ModeId.SuperHostRoles) || source?.Data == null || source.Data.Disconnected)
            {
                pendingReplies.Remove(source.PlayerId);
                return;
            }
            var message = messages.Dequeue();
            SHRMeetingChat.Reply(source, message.Text, message.Title, textInName: true);
            if (messages.Count == 0) pendingReplies.Remove(source.PlayerId);
            else new LateTask(SendNext, 0.5f, "SHR command reply");
        }
    }

    internal static string FormatReplyName(string title, string text)
        => "<align=left>" + ((string.IsNullOrEmpty(title) ? "" : title + "\n") + text)
            .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");

    internal static string WrapLongReplyLines(string text)
    {
        var result = new StringBuilder();
        bool firstLine = true;
        foreach (string line in text.Split('\n'))
        {
            if (!firstLine) result.Append('\n');
            firstLine = false;
            var remaining = new StringBuilder();
            int count = 0, boundary = 0;
            foreach (Match token in Regex.Matches(line, @"<[^>]*>|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]"))
            {
                remaining.Append(token.Value);
                if (token.Value.StartsWith('<')) continue;
                count++;
                if ("。、，,.!?！？；;：: \t".Contains(token.Value) && count >= 16) boundary = remaining.Length;
                if (count < 24) continue;
                int length = boundary > 0 ? boundary : remaining.Length;
                result.Append(remaining.ToString(0, length)).Append('\n');
                remaining.Remove(0, length);
                count = VisibleReplyLength(remaining.ToString());
                boundary = 0;
            }
            result.Append(remaining);
        }
        return result.ToString();
    }

    private static int VisibleReplyLength(string text)
        => Regex.Matches(Regex.Replace(text, "<[^>]*>", ""), @"[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]").Count;

    internal static IEnumerable<string> SplitReply(string title, string text)
    {
        const int maximumBytes = 1000;
        var openTags = new List<(string Name, string Text)>();
        var result = new List<string>();
        var current = new StringBuilder();
        bool hasContent = false;
        int lineEnd = 0;
        int nextLineToken = 0;
        var lineTags = new List<(string Name, string Text)>();
        string ClosingTags() => string.Concat(openTags.AsEnumerable().Reverse().Select(tag => $"</{tag.Name}>"));
        bool ExceedsLimit(string body) => Encoding.UTF8.GetByteCount(FormatReplyName(result.Count == 0 ? title : null, body)) + 1 > maximumBytes;
        var tokens = Regex.Matches(text, @"<[^>]*>|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]");
        for (int index = 0; index < tokens.Count; index++)
        {
            string value = tokens[index].Value;
            var nextTags = new List<(string Name, string Text)>(openTags);
            var tag = Regex.Match(value, @"^<(/?)(align|size|color|b|i|u|s|mark|voffset|indent|line-height|cspace|mspace)(?:=[^>]*)?>$", RegexOptions.IgnoreCase);
            if (tag.Success)
            {
                string name = tag.Groups[2].Value.ToLowerInvariant();
                if (tag.Groups[1].Length == 0) nextTags.Add((name, value));
                else if (nextTags.Count > 0 && nextTags[^1].Name == name) nextTags.RemoveAt(nextTags.Count - 1);
            }
            string nextClosing = string.Concat(nextTags.AsEnumerable().Reverse().Select(entry => $"</{entry.Name}>"));
            if (ExceedsLimit(current.ToString() + value + nextClosing))
            {
                if (!hasContent) throw new ArgumentException("Chat title or formatting exceeds the reply byte limit.");
                if (lineEnd > 0)
                {
                    current.Length = lineEnd;
                    openTags = lineTags;
                    result.Add(current.ToString() + ClosingTags());
                    current.Clear();
                    foreach (var entry in openTags) current.Append(entry.Text);
                    hasContent = false;
                    lineEnd = 0;
                    index = nextLineToken - 1;
                    continue;
                }
                result.Add(current.ToString() + ClosingTags());
                current.Clear();
                foreach (var entry in openTags) current.Append(entry.Text);
                hasContent = false;
                if (ExceedsLimit(current.ToString() + value + nextClosing))
                    throw new ArgumentException("Chat title or formatting exceeds the reply byte limit.");
            }
            current.Append(value);
            openTags = nextTags;
            hasContent = true;
            if (value == "\n")
            {
                lineEnd = current.Length;
                nextLineToken = index + 1;
                lineTags = new List<(string Name, string Text)>(openTags);
            }
        }
        if (hasContent) result.Add(current.ToString() + ClosingTags());
        return result;
    }

    private static string EnabledRoles()
    {
        byte map = GameOptionsManager.Instance.CurrentGameOptions.MapId;
        var entries = RoleOptionManager.RoleOptions.Select(option =>
            (Option: option, Role: CustomRoleManager.AllRoles.FirstOrDefault(role => role.Role == option.RoleId)))
            .Where(entry => entry.Role != null && IsEnabled(SHRRoleTypes.HasImplementedSupport(entry.Option.RoleId),
                entry.Role.HiddenOption, SHRRoleTypes.IsAvailableOnMap(entry.Option.RoleId, map)
                    && (entry.Role.AvailableMaps.Length == 0 || entry.Role.AvailableMaps.Any(value => (byte)value == map)),
                entry.Option.NumberOfCrews, entry.Option.Percentage))
            .OrderBy(entry => entry.Option.AssignTeam == AssignedTeamType.Impostor ? 0
                : entry.Option.AssignTeam == AssignedTeamType.Crewmate ? 1 : 2)
            .ThenByDescending(entry => entry.Option.Percentage).ToArray();
        if (entries.Length == 0) return ModTranslation.GetString("SHR.Commands.NoneEnabled");
        var result = new StringBuilder();
        result.AppendLine(ModTranslation.GetString("SHR.Commands.LegacyEnabledRoles")).AppendLine();
        foreach (var team in entries.GroupBy(entry => entry.Option.AssignTeam))
        {
            string name = TeamName(team.Key);
            int maximum = team.Key == AssignedTeamType.Impostor ? AssignRoles.MaxImpostors
                : team.Key == AssignedTeamType.Crewmate ? AssignRoles.MaxCrews : AssignRoles.MaxNeutrals;
            result.AppendLine(ModTranslation.GetString("SHR.Commands.TeamRoles", name)
                + ModTranslation.GetString("SHR.Commands.MaximumRoles", maximum));
            foreach (var entry in team)
                result.AppendLine(ModTranslation.GetString("SHR.Commands.RoleEntry",
                    ModTranslation.GetString(entry.Option.RoleId.ToString()), entry.Option.NumberOfCrews, entry.Option.Percentage));
            result.AppendLine("　");
        }
        return result.ToString().TrimEnd();
    }

    private static void RoleInfo(PlayerControl source, string name)
    {
        string title = CommanderTitle;
        if (string.IsNullOrWhiteSpace(name))
        {
            Reply(source, title, ModTranslation.GetString("SHR.Commands.RoleInfoUsage"));
            return;
        }
        bool hackerMadmate = ((ExPlayerControl)source).GetAbility<SHRHackerMadmateAbility>() != null;
        var matches = CustomRoleManager.AllRoles.Where(role => (SHRRoleTypes.HasImplementedSupport(role.Role)
                || hackerMadmate && role.Role == RoleId.Madmate)
            && !RoleReleaseLock.IsLocked(role.Role)
            && SHRGuesserCommands.MatchesRole(name, role.Role, ModTranslation.GetString(role.Role.ToString()))).ToArray();
        if (matches.Length != 1)
        {
            Reply(source, title, ModTranslation.GetString("SHR.Commands.RoleNotFound"));
            return;
        }
        var role = matches[0];
        string description = RoleDetailMenu.GetDescriptionKey(role.RoleName, true,
            key => ModTranslation.TryGetString(key, out var value) && !string.IsNullOrWhiteSpace(value));
        if (AmongUsClient.Instance.GetClientFromCharacter(source)?.PlatformData?.Platform == Platforms.Switch
            && ModTranslation.TryGetString(description + ".Switch", out var switchDescription)
            && !string.IsNullOrWhiteSpace(switchDescription)) description += ".Switch";
        var lines = new List<string>();
        var assignment = RoleOptionManager.RoleOptions.FirstOrDefault(option => option.RoleId == role.Role);
        if (assignment != null)
        {
            lines.Add($"  {ModTranslation.GetString("NumberOfCrews")}:{assignment.NumberOfCrews}");
            lines.Add($"  {ModTranslation.GetString("AssignPer")}:{assignment.Percentage}%");
        }
        foreach (var option in role.Options.Where(option => option.ShouldDisplay()))
        {
            int depth = 1;
            for (var parent = option.ParentOption; parent != null && role.Options.Contains(parent); parent = parent.ParentOption) depth++;
            lines.Add($"{new string(' ', depth * 2)}{ModTranslation.GetString(option.Name)}:{option.GetCurrentSelectionString()}");
        }
        string intro = ModTranslation.TryGetString($"{role.RoleName}Intro1", out var quote)
            ? ModHelpers.Cs(role.RoleColor, quote) : "";
        Reply(source, FormatRoleTitle(ModHelpers.Cs(role.RoleColor, ModTranslation.GetString(role.RoleName))),
            FormatRoleInfo(ModTranslation.GetString("SHR.Commands.Team", TeamName(role.AssignedTeam)), intro,
                ModTranslation.GetString(description), ModTranslation.GetString("SHR.Commands.SettingsTitle"),
                string.Join("\n", lines), role.RoleName));
    }

    private static string TeamName(AssignedTeamType team)
        => ModTranslation.GetString(team.ToString()).Replace("陣営", "").Replace("阵营", "").Replace("陣營", "");
}
