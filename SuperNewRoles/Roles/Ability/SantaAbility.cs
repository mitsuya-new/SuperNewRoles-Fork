using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using SuperNewRoles.Modules;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Crewmate;
using UnityEngine;

namespace SuperNewRoles.Roles.Ability;

public record SantaAbilityData(int InitialCount, float Cooldown, bool TryLoversToDeath, bool TryMadRolesToDeath, bool TryJackalFriendsToDeath, bool ForImpostor, List<(RoleId role, int percentage)> Roles);
public class SantaAbility : AbilityBase
{
    private CustomSidekickButtonAbility ButtonAbility;
    private SantaAbilityData _data;
    private List<RoleId> _tickets;
    private RoleId _selectedRole;
    private Sprite _buttonSprite;
    public int _count;
    public SantaAbility(SantaAbilityData data, string buttonSpritePath)
    {
        _data = data;
        _tickets = BuildTickets(data.Roles, ModeManager.IsMode(ModeId.SuperHostRoles));
        _buttonSprite = AssetManager.GetAsset<Sprite>(buttonSpritePath);
        _count = data.InitialCount;
    }

    internal static List<RoleId> BuildTickets(IEnumerable<(RoleId role, int percentage)> roles, bool shr)
    {
        var available = roles.Where(role => !shr || SHRRoleTypes.HasImplementedSupport(role.role)).ToArray();
        var tickets = new List<RoleId>();
        foreach (var role in available)
            for (int i = 0; i < role.percentage / 5; i++) tickets.Add(role.role);
        if (tickets.Count == 0) tickets.AddRange(available.Select(role => role.role));
        return tickets;
    }

    internal static bool IsUnavailableSHROption(string field) => field switch
    {
        nameof(Santa.SantaBalancerPercentage) => !SHRRoleTypes.HasImplementedSupport(RoleId.Balancer),
        nameof(Santa.SantaMediumPercentage) => !SHRRoleTypes.HasImplementedSupport(RoleId.Medium),
        nameof(Santa.SantaWellBehaverPercentage) => !SHRRoleTypes.HasImplementedSupport(RoleId.WellBehaver),
        _ => false,
    };

    internal bool CanGiveSHR => _tickets.Count > 0 && ButtonAbility?.HasCount == true;
    internal ExPlayerControl FindTargetSHR() => TargetCustomButtonBase.SetTarget(targetingPlayer: Player.Player,
        isTargetable: target => CanGiveSHR && ButtonAbility.CanCreateSidekickFor(target));
    internal bool TryGiveSHR(ExPlayerControl target)
    {
        if (!CanGiveSHR || target == null) return false;
        int before = ButtonAbility.Count;
        ButtonAbility.TryCreateSidekick(target);
        return ButtonAbility.Count < before;
    }

    public override void AttachToAlls()
    {
        ButtonAbility = new(new(
            canCreateSidekick: (_) => _tickets.Count > 0 && ButtonAbility.HasCount,
            sidekickCooldown: () => _data.Cooldown,
            sidekickRole: () => _selectedRole,
            sidekickRoleVanilla: () => CustomRoleManager.GetRoleById(_selectedRole)?.AssignedTeam == AssignedTeamType.Impostor ? (RoleTypes?)null : RoleTypes.Crewmate,
            sidekickSprite: _buttonSprite,
            sidekickText: ModTranslation.GetString("SantaButtonText"),
            sidekickCount: () => _count,
            isTargetable: (player) => player.IsAlive(),
            sidekickSuccess: (player) =>
            {
                if (player.IsMadRoles() && _data.TryMadRolesToDeath)
                    return false;
                else if (player.IsLovers() && _data.TryLoversToDeath)
                    return false;
                else if (player.IsFriendRoles() && _data.TryJackalFriendsToDeath)
                    return false;
                _selectedRole = SelectRole();
                return _data.ForImpostor ? player.IsImpostor() : player.IsCrewmateOrMadRoles();
            },
            onSidekickCreated: (player) =>
            {
                Logger.Info($"SelectedRole: {_selectedRole}");
                bool successed = false;
                if (player.IsMadRoles() && _data.TryMadRolesToDeath)
                    successed = false;
                else if (player.IsLovers() && _data.TryLoversToDeath)
                    successed = false;
                else if (player.IsFriendRoles() && _data.TryJackalFriendsToDeath)
                    successed = false;
                else
                    successed = _data.ForImpostor ? player.IsImpostor() : player.IsCrewmateOrMadRoles();
                RpcSantaAssigndRole(Player, player, successed);
            },
            showSidekickLimitText: () => true
        ));

        Player.AttachAbility(ButtonAbility, new AbilityParentAbility(this));
    }
    private RoleId SelectRole()
    {
        return _selectedRole = _tickets[Random.Range(0, _tickets.Count)];
    }
    [CustomRPC]
    public void RpcSantaAssigndRole(ExPlayerControl source, ExPlayerControl target, bool successed)
    {
        if (!successed)
        {
            // サンタの自爆処理
            if (ModeManager.IsMode(ModeId.SuperHostRoles))
            {
                if (AmongUsClient.Instance?.AmHost == true) SHRMurderSync.Murder(source, source, explosion: true, finalStatus: FinalStatus.Suicide);
            }
            else source.CustomDeath(CustomDeathType.Suicide);
            return;
        }
        _count--;
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance?.AmHost == true)
        {
            SHRTestRoleAbility.SendRoleToAll(target.Player, RoleTypes.Crewmate);
            if (SHRRoleTypes.GetInitialRole(target.Role) is { } control)
                SHRTestRoleAbility.SetSelfRole(target.Player, control);
            SHRPlayerOptions.Send(target.Player, resetKill: true, resetAbility: true);
            SHRRoleNameDisplay.Refresh();
        }
    }
}
