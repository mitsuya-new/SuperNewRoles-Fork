using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hazel;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;

namespace SuperNewRoles.Mode;

public static class SHRGuesserCommands
{
    internal static bool CanUse(RoleId role) => role is RoleId.EvilGuesser or RoleId.NiceGuesser;
    private const string Usage = "【ゲッサー】/bt [色] [役職名]\n例：/bt 赤 シェリフ（色番号・英語の役職名も使用可能）";

    internal static bool IsCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string command = text.TrimStart().Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0];
        return command.Equals("/bt", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryParse(string text, out string color, out string role)
    {
        color = role = null;
        if (!IsCommand(text)) return false;
        var parts = text.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        color = parts[1];
        role = parts[2].Trim();
        return role.Length != 0;
    }

    internal static string Normalize(string text)
        => Regex.Replace((text ?? "").Normalize(NormalizationForm.FormKC), "<[^>]*>|[\\s_\\-]", "").ToLowerInvariant();

    internal static bool MatchesRole(string input, RoleId id, string translatedName)
    {
        string value = Normalize(input);
        if (value == Normalize(id.ToString()) || value == Normalize(translatedName)) return true;
        return id switch
        {
            RoleId.Crewmate => value is "crew" or "クルー",
            RoleId.Impostor => value is "impo" or "インポ",
            _ => false,
        };
    }

    private static int? ResolveColor(string text)
    {
        if (int.TryParse(text.Normalize(NormalizationForm.FormKC), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            && number >= 0 && number < Palette.PlayerColors.Length) return number;
        string[] english = { "red", "blue", "green", "pink", "orange", "yellow", "black", "white", "purple",
            "brown", "cyan", "lime", "maroon", "rose", "banana", "gray", "tan", "coral" };
        string[] japanese = { "赤", "青", "緑", "ピンク", "オレンジ", "黄", "黒", "白", "紫",
            "茶", "水", "ライム", "マルーン", "ローズ", "バナナ", "灰", "タン", "コーラル" };
        string value = Normalize(text);
        for (int i = 0; i < Palette.PlayerColors.Length; i++)
        {
            string translated = DestroyableSingleton<TranslationController>.Instance.GetString(Palette.ColorNames[i]);
            if (value == Normalize(translated) || (i < english.Length && (value == english[i] || value == japanese[i]))) return i;
        }
        return null;
    }

    public static void Handle(PlayerControl source, string text)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost
            || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        ExPlayerControl player = source;
        var ability = player.GetAbility<GuesserAbility>();
        string error = null;
        if (!CanUse(player.Role) || ability == null)
            error = "このコマンドはゲッサー専用です。";
        else if (source.Data.IsDead) return;
        else if (MeetingHud.Instance == null) error = "会議中に使用してください。";
        else if (!SHRMeetingChat.Ready) error = "会議開始後の案内が表示されてから入力してください。";
        else if (!TryParse(text, out var colorText, out var roleText)) error = Usage;
        else
        {
            int? color = ResolveColor(colorText);
            var targets = ExPlayerControl.ExPlayerControls.Where(p => p.Data != null && !p.Data.Disconnected
                && p.Data.DefaultOutfit.ColorId == color).ToArray();
            var roles = CustomRoleManager.AllRoles.Where(r => MatchesRole(roleText, r.Role,
                ModTranslation.GetString(r.Role.ToString()))).Select(r => r.Role).Distinct().ToArray();
            if (color == null || targets.Length != 1) error = "対象の色が見つからないか、色が重複しています。";
            else if (roles.Length != 1) error = "役職名が見つからないか、複数の役職に一致します。";
            else if (ability.TryShootFromCommand(targets[0], roles[0], out error))
            {
                Logger.Debug($"Shot requested player={player.PlayerId} target={targets[0].PlayerId} guess={roles[0]}", "SHR.Guesser");
                return;
            }
        }
        error ??= "コマンドを実行できませんでした。";
        SHRMeetingChat.Reply(source, error.StartsWith("【") ? error : "【ゲッサー】" + error);
    }

    public static void SendMeetingInstructions()
    {
        foreach (var player in ExPlayerControl.ExPlayerControls)
            if (CanUse(player.Role) && player.IsAlive() && !player.Data.Disconnected)
                SHRMeetingChat.Reply(player.Player, Usage + $"\n残り弾数：{player.GetAbility<GuesserAbility>()?.Count ?? 0}");
    }

    public static void SendVanillaShot(PlayerControl killer, ExPlayerControl victim)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            int id = client.GetClientIdFromCharacter(viewer);
            if (id < 0) continue;
            var writer = client.StartRpcImmediately(victim.Player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, id);
            client.FinishRpcImmediately(writer);
            SHRMeetingChat.Reply(viewer,
                ModTranslation.GetString("GuesserPlayerWasDead", Regex.Replace(victim.Data.DefaultOutfit.PlayerName, "<[^>]*>", "")) + "\n",
                ModTranslation.GetString("GuesserBigNewsTitle"));
        }
        RpcBigNews(Regex.Replace(victim.Data.DefaultOutfit.PlayerName, "<[^>]*>", ""));
    }

    [CustomRPC]
    public static void RpcBigNews(string playerName)
    {
        if (!ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        SHRMeetingChat.ShowLocalSystemMessage(ModTranslation.GetString("GuesserPlayerWasDead", playerName),
            ModTranslation.GetString("GuesserBigNewsTitle"));
    }
}
