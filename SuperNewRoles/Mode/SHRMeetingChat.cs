using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using AmongUs.GameOptions;

namespace SuperNewRoles.Mode;

public static class SHRMeetingChat
{
    private static MeetingHud meeting;
    private static int generation;
    private static int privateSerialization;
    internal static bool IsSerializingPrivateState => privateSerialization != 0;
    private static bool showingLocalSystemMessage;
    public static bool Ready { get; private set; }
    private static bool IsHostSHR => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && ModeManager.IsMode(ModeId.SuperHostRoles);
    private static bool Active => IsHostSHR && meeting != null && MeetingHud.Instance == meeting;

    internal static bool HideSpeaker(bool viewerDead, bool speakerDead, bool isSelf)
        => !viewerDead && !speakerDead && !isSelf;

    internal static string FormatAnnouncementText(string text)
        => "\n" + text.Trim('\r', '\n') + "\n";

    public static void ShowLocalSystemMessage(string text, string title = null, bool textInName = false)
    {
        var player = PlayerControl.LocalPlayer;
        if (player?.Data == null || HudManager.Instance?.Chat == null) return;
        string originalName = player.Data.PlayerName;
        bool previous = showingLocalSystemMessage;
        try
        {
            showingLocalSystemMessage = true;
            if (title != null) player.SetName(title);
            HudManager.Instance.Chat.AddChat(player, title == null || textInName ? text : FormatAnnouncementText(text));
        }
        finally
        {
            if (title != null) player.SetName(originalName);
            showingLocalSystemMessage = previous;
        }
    }

    private static void Begin(MeetingHud instance)
    {
        if (!IsHostSHR) return;
        meeting = instance;
        Ready = false;
        int current = ++generation;
        new LateTask(() => Initialize(announce: true), 4.5f, "SHR hide meeting chat");
        new LateTask(() => Initialize(announce: false), 6.5f, "SHR refresh hidden meeting chat");
        void Initialize(bool announce)
        {
            if (current != generation || !Active) return;
            Ready = true;
            Refresh();
            if (announce) SHRGuesserCommands.SendMeetingInstructions();
        }
    }

    public static void Refresh()
    {
        if (!Active || !Ready) return;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            foreach (var subject in PlayerControl.AllPlayerControls)
                SendState(viewer, subject, hide: true);
        }
    }

    public static void Restore()
    {
        bool restore = IsHostSHR && meeting != null;
        meeting = null;
        Ready = false;
        generation++;
        if (!restore || SHRAntiBlackout.Active) return;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            foreach (var subject in PlayerControl.AllPlayerControls)
                SendState(viewer, subject, hide: false);
        }
    }

    internal static RoleTypes GetCurrentViewRole(PlayerControl viewer, PlayerControl subject)
    {
        if (SHRRedRidingHoodGhosts.HidesInformation(viewer))
            return SHRRedRidingHoodGhosts.HiddenRole(viewer == subject, subject.Data.IsDead);
        ExPlayerControl ex = subject;
        var originalRole = subject.Data.Role;
        var role = SHRRoleAssignment.GetViewRole(ex.Role, originalRole.Role,
            viewer.PlayerId == subject.PlayerId, SHRPlayerOptions.IsVanillaGuest(subject),
            ((ExPlayerControl)viewer).Role, true, originalRole.IsImpostor);
        if (viewer.PlayerId == subject.PlayerId)
        {
                if (ex.GetAbility<SHRHackerMadmateAbility>() is { } hackerMadmate) role = hackerMadmate.ControlRole;
                if (ex.GetAbility<SHREvilHackerAbility>() is { HasMadmateUse: false }) role = RoleTypes.Impostor;
            if (ex.GetAbility<SuperNewRoles.Roles.Crewmate.ChiefAbility>()?.CanAppointSHR == false)
                role = RoleTypes.Crewmate;
            if (ex.Role == RoleId.Mafia)
                role = SuperNewRoles.Roles.Impostor.Mafia.IsKillFlag() ? RoleTypes.Impostor : RoleTypes.Crewmate;
            if (ex.GetAbility<SHRBatchRoleAbility>()?.Exhausted == true)
                role = RoleTypes.Impostor;
            if (ex.Role is RoleId.Jackal or RoleId.Samurai or RoleId.SideKiller && ex.GetAbility<SHRTestRoleAbility>()?.PhantomAbilityCompleted == true)
                role = RoleTypes.Impostor;
            if (ex.Role == RoleId.Tasker && ex.GetAbility<SHRTestRoleAbility>()?.TaskerImpostor == true)
                role = SHRRoleTypes.GetTaskerRole(SuperNewRoles.Roles.Impostor.Tasker.TaskerCanKill, true);
            if (ex.Role == RoleId.MadKiller && ex.GetAbility<MadKillerAbility>()?.IsAwakened == true)
                role = RoleTypes.Impostor;
        }
        return subject.Data.IsDead ? originalRole.IsImpostor ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost : role;
    }

    private static void WriteState(MessageWriter writer, PlayerControl viewer, PlayerControl subject, bool hide,
        RoleTypes? overrideRole = null, bool? overrideDead = null)
    {
        var data = subject.Data;
        bool dead = data.IsDead;
        var originalRole = data.Role;
        try
        {
            var role = overrideRole ?? GetCurrentViewRole(viewer, subject);
            if (hide && HideSpeaker(viewer.Data.IsDead, dead, viewer.PlayerId == subject.PlayerId)) data.IsDead = true;
            if (overrideDead.HasValue) data.IsDead = overrideDead.Value;
            data.Role = RoleManager.Instance.GetRole(role);
            writer.StartMessage(1);
            writer.WritePacked(data.NetId);
            privateSerialization++;
            try { data.Serialize(writer, false); }
            finally { privateSerialization--; }
            writer.EndMessage();
        }
        finally
        {
            data.IsDead = dead;
            data.Role = originalRole;
        }
        string chiefName = SHRDatahackerInformation.GetName(viewer, subject) ?? SHRCrewRoleAbility.GetChiefDisplayName(viewer, subject);
        SHRPets.AppendPetState(writer, subject, viewer);
        if (chiefName != null)
        {
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(subject.NetId);
            writer.Write((byte)RpcCalls.SetName);
            new RpcSetNameMessage(subject.NetId, subject.Data.NetId, chiefName).SerializeRpcValues(writer);
            writer.EndMessage();
        }
    }

    internal static void SendState(PlayerControl viewer, PlayerControl subject, bool hide,
        RoleTypes? overrideRole = null, bool? overrideDead = null)
    {
        if (subject?.Data?.Role == null || viewer?.Data == null) return;
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0) return;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            Start(writer, id);
            WriteState(writer, viewer, subject, hide, overrideRole, overrideDead);
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }
    }

    private static void Start(MessageWriter writer, int id)
    {
        writer.StartMessage(Tags.GameDataTo);
        writer.Write(AmongUsClient.Instance.GameId);
        writer.WritePacked(id);
    }

    internal static void SendDisconnectedState(PlayerControl viewer, NetworkedPlayerInfo data)
    {
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0 || data == null || !data.Disconnected) return;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            Start(writer, id);
            writer.StartMessage(1);
            writer.WritePacked(data.NetId);
            privateSerialization++;
            try { data.Serialize(writer, false); }
            finally { privateSerialization--; }
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }
    }

    private static void Relay(PlayerControl speaker, string text)
    {
        if (!Active || !Ready || speaker.Data.IsDead) return;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer) || viewer == speaker || viewer.Data.IsDead) continue;
            int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
            if (id < 0) continue;
            var writer = MessageWriter.Get(SendOption.Reliable);
            try
            {
                Start(writer, id);
                WriteState(writer, viewer, speaker, hide: false);
                writer.StartMessage((byte)GameDataTypes.RpcFlag);
                writer.WritePacked(speaker.NetId);
                writer.Write((byte)RpcCalls.SendChat);
                writer.Write(text);
                writer.EndMessage();
                WriteState(writer, viewer, speaker, hide: true);
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
            }
            finally { writer.Recycle(); }
        }
    }

    public static void Reply(PlayerControl viewer, string text, string title = null, bool textInName = false)
    {
        if (!IsHostSHR || viewer?.Data == null || viewer.Data.Disconnected) return;
        if (textInName)
        {
            title = SHRChatCommands.FormatReplyName(title, text);
            text = " ";
        }
        if (viewer.AmOwner)
        {
            ShowLocalSystemMessage(text, title, textInName);
            return;
        }
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0) return;
        if (title != null)
        {
            var packet = MessageWriter.Get(SendOption.Reliable);
            try
            {
                Start(packet, id);
                WriteName(title);
                BeginRpc(RpcCalls.SendChat);
                packet.Write(textInName ? text : FormatAnnouncementText(text));
                packet.EndMessage();
                WriteName(SHRRoleNameDisplay.GetSelfDisplayName(viewer));
                packet.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(packet);
            }
            finally { packet.Recycle(); }
            return;

            void BeginRpc(RpcCalls call)
            {
                packet.StartMessage((byte)GameDataTypes.RpcFlag);
                packet.WritePacked(viewer.NetId);
                packet.Write((byte)call);
            }
            void WriteName(string name)
            {
                BeginRpc(RpcCalls.SetName);
                packet.Write(viewer.Data.NetId);
                packet.Write(name);
                packet.EndMessage();
            }
        }
        var writer = AmongUsClient.Instance.StartRpcImmediately(viewer.NetId, (byte)RpcCalls.SendChat, SendOption.Reliable, id);
        writer.Write(text);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    public static class ChatPatch
    {
        public static bool Prefix(PlayerControl sourcePlayer, string chatText)
        {
            if (showingLocalSystemMessage) return true;
            if (!ModeManager.IsMode(ModeId.SuperHostRoles) || sourcePlayer?.Data == null || chatText == null) return true;
            if (SHRChatCommands.IsCommand(chatText))
            {
                if (IsHostSHR) SHRChatCommands.Handle(sourcePlayer, chatText);
                return false;
            }
            if (SHRGuesserCommands.IsCommand(chatText))
            {
                if (IsHostSHR) SHRGuesserCommands.Handle(sourcePlayer, chatText);
                return false;
            }
            if (IsHostSHR) Relay(sourcePlayer, chatText);
            return true;
        }
    }

    [HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Serialize))]
    public static class SerializePatch
    {
        public static bool Prefix(bool initialState, ref bool __result)
        {
            if (!IsHostSHR || (!Active && !SHRAntiBlackout.Active) || initialState || privateSerialization != 0) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartPatch { public static void Postfix(MeetingHud __instance) => Begin(__instance); }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
    public static class VotingPatch { public static void Prefix() => Restore(); }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class ClosePatch { public static void Prefix() => Restore(); }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
    public static class DestroyPatch { public static void Prefix() => Restore(); }
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch { public static void Prefix() => Restore(); }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    public static class DeathPatch { public static void Postfix() => Refresh(); }
}
