using System.Collections.Generic;
using System.Linq;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.CrewMate;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.Mode;

public static class SHRDatahackerInformation
{
    private static readonly Dictionary<(byte Viewer, byte Subject), string> Sent = new();

    internal static string GetName(PlayerControl viewer, PlayerControl subject)
    {
        if (viewer == subject || !SHRPlayerOptions.IsVanillaGuest(viewer) || viewer.Data.IsDead
            || subject?.Data == null || subject.Data.Disconnected || SHRAntiBlackout.Active
            || SHRCamouflagerController.IsActive && MeetingHud.Instance == null) return null;
        ExPlayerControl observer = viewer;
        ExPlayerControl target = subject;
        string name = SHRCamouflagerController.OriginalName(subject);
        if (observer.GetAbility<FinderAbility>()?.CanIdentifySHR(target) == true)
            return ModHelpers.Cs(target.roleBase.RoleColor, name);
        if (observer.GetAbility<DatahackerAbility>() is { } hacker && hacker.CanSeeSHRRole(target, out var color, out bool showRole))
        {
            name = ModHelpers.Cs(color, name);
            if (showRole && target.IsAlive())
            {
                string role = NameText.GetRoleInfoText(target);
                foreach (var hide in target.GetAbilities<HideMyRoleWhenAliveAbility>()) hide.DisplayFalseRoleNameWhileAlive(target, ref role);
                name = SHRRoleNameDisplay.FormatName(role, name, MeetingHud.Instance != null);
            }
            return name;
        }
        if (target.GetAbility<DatahackerAbility>()?.CanSeeOtherPlayer().exposedToImpostors == true && observer.IsNonCrewKiller())
            return ModHelpers.Cs(Datahacker.Instance.RoleColor, name);
        return null;
    }

    internal static string SelfArrows(ExPlayerControl viewer)
    {
        if (viewer == null || viewer.IsDead() || !viewer.IsNonCrewKiller() || MeetingHud.Instance != null
            || SHRCamouflagerController.IsActive || SHRAntiBlackout.Active) return "";
        string arrows = string.Concat(ExPlayerControl.ExPlayerControls
            .Where(target => target != viewer && target.Data != null && !target.Data.Disconnected && target.IsAlive()
                && target.GetAbility<DatahackerAbility>() is { ShowSHRExposureArrow: true } hacker
                && hacker.CanSeeOtherPlayer().exposedToImpostors)
            .Select(target => SHRBatchRoleAbility.DirectionArrow(target.transform.position.x - viewer.transform.position.x,
                target.transform.position.y - viewer.transform.position.y)));
        return arrows.Length == 0 ? "" : " " + ModHelpers.Cs(Datahacker.Instance.RoleColor, arrows);
    }

    internal static void Update()
    {
        var current = new Dictionary<(byte Viewer, byte Subject), string>();
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            foreach (var subject in PlayerControl.AllPlayerControls)
            {
                string name = GetName(viewer, subject);
                if (name == null) continue;
                var key = (viewer.PlayerId, subject.PlayerId);
                current[key] = name;
                if (!Sent.TryGetValue(key, out string previous) || previous != name) subject.RpcSetNamePrivate(name, viewer);
            }
        }
        foreach (var key in Sent.Keys.Where(key => !current.ContainsKey(key)).ToArray()) Restore(key);
        Sent.Clear();
        foreach (var pair in current) Sent[pair.Key] = pair.Value;
    }

    private static void Restore((byte Viewer, byte Subject) key)
    {
        var viewer = ExPlayerControl.ById(key.Viewer)?.Player;
        var subject = ExPlayerControl.ById(key.Subject)?.Player;
        if (!SHRPlayerOptions.IsVanillaGuest(viewer) || subject?.Data == null || subject.Data.Disconnected) return;
        string name = SHRCamouflagerController.IsMasked(subject) && MeetingHud.Instance == null
            ? SHRCamouflagerController.AnonymousName : SHRCamouflagerController.OriginalName(subject);
        subject.RpcSetNamePrivate(name, viewer);
    }

    internal static void Clear()
    {
        if (AmongUsClient.Instance?.AmHost == true)
            foreach (var key in Sent.Keys) Restore(key);
        Sent.Clear();
    }
}
