using AmongUs.GameOptions;
using InnerNet;
using SuperNewRoles.Modules;

namespace SuperNewRoles.Mode;

internal static class SHRReviveSync
{
    internal static bool CanRestore(bool connected, bool alive, bool started, bool gameOver)
        => connected && alive && started && !gameOver;

    internal static void NotifyRevived(ExPlayerControl player)
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles) || player?.Data == null
            || !CanRestore(!player.Data.Disconnected, player.IsAlive(),
                client.GameState == InnerNetClient.GameStates.Started, client.IsGameOver)) return;
        if (SHRAntiBlackout.DeferRevive(player.Player)) return;
        foreach (var viewer in PlayerControl.AllPlayerControls)
            if (SHRPlayerOptions.IsVanillaGuest(viewer)) RestoreViewer(player.Player, viewer);
        SHRPlayerOptions.Send(player.Player);
        SHRRoleNameDisplay.Refresh();
        SHRRedRidingHoodGhosts.Refresh(player.Player);
    }

    internal static void RestoreViewer(PlayerControl subject, PlayerControl viewer)
    {
        if (!SHRPlayerOptions.IsVanillaGuest(viewer) || subject?.Data == null
            || subject.Data.Disconnected || subject.Data.IsDead) return;
        var role = SHRMeetingChat.GetCurrentViewRole(viewer, subject);
        SHRMeetingChat.SendState(viewer, subject, hide: false, overrideRole: role, overrideDead: true);
        SHRTestRoleAbility.SendRoleView(subject, viewer, role, allowDuringTransition: true);
    }
}
