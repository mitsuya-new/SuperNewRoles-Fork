using System;
using System.Collections.Generic;
using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Impostor;
using AmongUs.GameOptions;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Events;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Mode;

namespace SuperNewRoles.Roles.Ability;

public class DoppelgangerAbility : AbilityBase
{
    public float DurationTime;
    public float CoolTime;
    public float SucCool;
    public float NotSucCool;

    // ShapeshiftButtonAbilityのインスタンスを保持
    private ShapeshiftButtonAbility _shapeshiftButtonAbility;
    private PlayerControl shrShapeTarget;
    internal float? SHRKillCooldown { get; private set; }
    private bool IsVanillaHost => AmongUsClient.Instance?.AmHost == true
        && ModeManager.IsMode(ModeId.SuperHostRoles) && SHRPlayerOptions.IsVanillaGuest(Player.Player);

    internal static float SelectCooldown(bool shifted, bool killedShapeTarget, float success, float failure)
        => shifted && killedShapeTarget ? success : failure;

    public DoppelgangerAbility(DoppelgangerAbilityOption option)
    {
        DurationTime = option.DurationTime;
        CoolTime = option.CoolTime;
        SucCool = option.SucCool;
        NotSucCool = option.NotSucCool;
    }

    public void OnMurderPlayer(MurderEventData data)
    {
        if (data == null || data.killer != Player) return;
        if (!data.resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
        if (data.target == null) return;
        if (data.target.IsAlive()) return;
        if (IsVanillaHost)
        {
            if (!Player.IsAlive() || data.target == Player) return;
            float cooldown = SelectCooldown(shrShapeTarget != null, data.target.Player == shrShapeTarget, SucCool, NotSucCool);
            SHRKillCooldown = cooldown;
            SHRKillRequests.SetRemainingCooldown(Player.Player, cooldown);
            return;
        }
        if (_shapeshiftButtonAbility == null) return;

        PlayerControl currentTarget = _shapeshiftButtonAbility.isEffectActive
            ? _shapeshiftButtonAbility.ShapeTarget
            : null;

        float timer = data.target == currentTarget ?
                         SucCool :
                         NotSucCool;
        new LateTask(() => ExPlayerControl.LocalPlayer.SetKillTimerUnchecked(timer, timer), 0.02f, "DoppelgangerAbility");
    }


    public override void AttachToLocalPlayer()
    {
        SubscribeWithAbility(MurderEvent.Instance, OnMurderPlayer);
    }

    public override void AttachToOthers()
    {
        if (!IsVanillaHost) return;
        SubscribeWithAbility(MurderEvent.Instance, OnMurderPlayer);
        SubscribeWithAbility(ShapeshiftEvent.Instance, OnSHRShapeshift);
        SubscribeWithAbility(WrapUpEvent.Instance, OnSHRWrapUp);
    }

    private void OnSHRShapeshift(ShapeshiftEventData data)
    {
        if (data.shapeshifter != Player.Player) return;
        shrShapeTarget = data.target == Player.Player ? null : data.target;
    }

    private void OnSHRWrapUp(WrapUpEventData data)
    {
        shrShapeTarget = null;
        new LateTask(() =>
        {
            if (!IsVanillaHost || !Player.IsAlive() || AmongUsClient.Instance.IsGameOver
                || Player.GetAbility<DoppelgangerAbility>() != this) return;
            SHRKillCooldown = null;
            SHRKillRequests.SetRemainingCooldown(Player.Player, SHRPlayerOptions.KillCooldown(Player));
        }, 0.5f, "SHR Doppelganger Reset Kill Cooldown After Meeting");
    }

    public override void AttachToAlls()
    {
        base.AttachToAlls();

        _shapeshiftButtonAbility = new ShapeshiftButtonAbility(CoolTime, DurationTime, spriteName: "DoppelgangerButton.png");
        Player.AttachAbility(_shapeshiftButtonAbility, new AbilityParentAbility(this));
    }

    public class DoppelgangerAbilityOption
    {
        public float DurationTime;
        public float CoolTime;
        public float SucCool;
        public float NotSucCool;

        public DoppelgangerAbilityOption(float durationTime, float coolTime, float sucCool, float notSucCool)
        {
            DurationTime = durationTime;
            CoolTime = coolTime;
            SucCool = sucCool;
            NotSucCool = notSucCool;
        }
    }
}
