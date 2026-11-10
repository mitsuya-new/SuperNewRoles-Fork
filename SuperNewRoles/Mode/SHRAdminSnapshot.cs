using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal static class SHRAdminSnapshot
{
    private static Il2CppReferenceArray<Collider2D> buffer;

    internal static Dictionary<SystemTypes, AdminRoomCount> Capture(ExPlayerControl viewer, bool showSelf, bool includeCorpses)
    {
        var result = new Dictionary<SystemTypes, AdminRoomCount>();
        if (ShipStatus.Instance?.FastRooms == null || viewer?.Player == null) return result;
        buffer ??= new Il2CppReferenceArray<Collider2D>(45);
        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = Constants.LivingPlayersOnlyMask,
        };
        foreach (var pair in ShipStatus.Instance.FastRooms)
        {
            if (pair.Value?.roomArea == null) continue;
            var count = new AdminRoomCount();
            int hits = pair.Value.roomArea.OverlapCollider(filter, buffer);
            for (int i = 0; i < hits; i++)
            {
                var collider = buffer[i];
                if (collider == null) continue;
                if (collider.CompareTag("DeadBody"))
                {
                    if (includeCorpses && collider.GetComponent<DeadBody>() != null) count.AddCorpse();
                    continue;
                }
                ExPlayerControl player = collider.GetComponent<PlayerControl>();
                if (player?.Data == null || player.Data.Disconnected || player.Data.IsDead || !showSelf && player == viewer) continue;
                count.AddPlayer(player.PlayerId, player.GetAbility<HideInAdminAbility>()?.IsHideInAdmin == true, player.IsImpostor());
            }
            result[pair.Key] = count;
        }
        return result;
    }
}
