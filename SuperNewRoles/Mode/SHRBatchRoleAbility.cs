using System;
using System.Linq;
using AmongUs.GameOptions;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRBatchRoleAbility : AbilityBase
{
    private bool initialized;
    private float readyAt;
    private float effectUntil;
    private float nextInformation;
    private Vector2? storedPosition;
    private string information = "";
    private float informationUntil;
    private bool shaped;
    private int discoveries;
    private ExPlayerControl invisibleTarget;
    private bool? conditionalKillReady;
    private bool grantedPet;

    internal static bool Supports(RoleId role) => role is RoleId.EvilNekomata or RoleId.Pursuer or RoleId.EvilDoorr
        or RoleId.DoubleKiller or RoleId.Mafia or RoleId.EvilButtoner or RoleId.DarkKiller or RoleId.Magaziner
        or RoleId.ShiftActor or RoleId.Jammer or RoleId.EvilMoving or RoleId.Teleporter or RoleId.Vampire;

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !SHRPlayerOptions.IsVanillaGuest(player.Player) || !Supports(player.Role)
            || player.GetAbility<SHRBatchRoleAbility>() != null) return;
        player.AttachAbility(new SHRBatchRoleAbility(), new AbilityParentRole(player, player.roleBase));
    }

    internal static float DefaultCooldown(RoleId role) => role switch
    {
        RoleId.EvilDoorr => EvilDoorr.EvilDoorrCoolTime,
        RoleId.DoubleKiller => DoubleKiller.DoubleKillerSubKillCooldown,
        RoleId.EvilButtoner => EvilButtoner.EvilButtonerCooldown,
        RoleId.Magaziner => 0f,
        RoleId.ShiftActor => ShiftActor.ShiftActorCooldown,
        RoleId.Jammer => Jammer.JammerCoolTime,
        RoleId.EvilMoving => EvilMoving.EvilMovingCooldown,
        RoleId.Teleporter => Teleporter.TeleporterCooldown,
        RoleId.Vampire => Vampire.VampireNightFallCooldown,
        _ => 0f,
    };

    internal static bool HasPhantom(RoleId role) => role is RoleId.EvilDoorr or RoleId.DoubleKiller
        or RoleId.EvilButtoner or RoleId.Magaziner or RoleId.Jammer or RoleId.EvilMoving or RoleId.Teleporter
        || role == RoleId.Vampire && Vampire.VampireCreateDependents;

    internal bool Exhausted => Player.Role switch
    {
        RoleId.DoubleKiller => Player.GetAbility<DoubleKillerAbility>() is { } doubleKiller
            && doubleKiller.DoubleKillerAbilityData.DoubleKillerCount.HasValue && !doubleKiller.HasCount,
        RoleId.EvilButtoner => Player.GetAbility<ButtonerAbility>()?.HasCount == false,
        RoleId.Jammer => Player.GetAbility<JammerAbility>()?.SHRUsesRemaining == 0,
        RoleId.Vampire => Player.GetAbility<VampireAbility>()?.SidekickAbility?.Count == 0,
        RoleId.ShiftActor => ShiftActor.ShiftActorIsLimitUses && discoveries <= 0 && !ShiftActor.ShiftActorCanShapeshiftAfterUsesExhausted,
        _ => false,
    };

    internal float RemainingCooldown => Math.Max(0f, (effectUntil > Time.unscaledTime ? effectUntil : readyAt) - Time.unscaledTime);

    internal static bool TryConsumeExpiredEffect(ref float until, float now)
    {
        if (until <= 0f || now < until) return false;
        until = 0f;
        return true;
    }

    public override void AttachToAlls()
    {
        discoveries = ShiftActor.ShiftActorMaxUseCount;
        if (HasPhantom(Player.Role))
            Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Phantom, _ => Activate(), CanUse), new AbilityParentAbility(this));
        if (Player.Role == RoleId.Magaziner)
            Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => StoreAmmo(), CanStoreAmmo), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(WrapUpEvent.Instance, _ => ResetCooldown());
        SubscribeWithAbility(PreCalledMeetingEvent.Instance, _ => StopEffect());
    }

    private void ResetCooldown()
    {
        if (Player.Role == RoleId.DarkKiller) conditionalKillReady = null;
        if (Exhausted && Player.IsAlive()) SHRTestRoleAbility.SetSelfRole(Player.Player, RoleTypes.Impostor);
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(DefaultCooldown(Player.Role),
            CustomOptions.Categories.GameSettingOptions.InitialCooldown);
        if ((HasPhantom(Player.Role) || Player.Role == RoleId.ShiftActor) && !Exhausted)
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
    }

    private ExPlayerControl FindTarget(bool onlyCrewmates = true, Func<ExPlayerControl, bool> predicate = null)
        => TargetCustomButtonBase.SetTarget(onlyCrewmates: onlyCrewmates, targetingPlayer: Player.Player,
            isTargetable: target => !SHRWaveCannonController.IsProtected(target)
                && (!onlyCrewmates || !Patches.SetTargetPatch.IsMadKillerTeammate(Player, target))
                && (predicate?.Invoke(target) ?? true));

    private bool CanUse()
    {
        if (!initialized || !Player.IsAlive() || Exhausted || Time.unscaledTime < readyAt || effectUntil > 0f) return false;
        return Player.Role switch
        {
            RoleId.DoubleKiller => FindTarget() != null,
            RoleId.EvilDoorr => Player.GetAbility<DoorrAbility>()?.CanUseSHR == true,
            RoleId.EvilButtoner => !ModHelpers.IsSabotageAvailable(),
            RoleId.Magaziner => CanChangeAmmo(consume: true),
            RoleId.Jammer => FindTarget(!Jammer.JammerCanUseAbilitiesAgainstImposter) != null,
            RoleId.Vampire => FindTarget(false, target => Player.GetAbility<VampireAbility>().SidekickAbility.CanCreateSidekickFor(target)) != null,
            _ => true,
        };
    }

    private bool CanChangeAmmo(bool consume)
    {
        var ammo = Player.GetAbility<MagazinerAbility>();
        return initialized && Player.IsAlive() && ammo != null
            && MagazinerAbility.CanChangeSHRAmmo(consume, ammo.Count, SHRKillRequests.RemainingCooldown(Player.Player),
                ammo.Data.ammoLimitEnabled, ammo.Data.maxAmmo);
    }

    private bool CanStoreAmmo() => CanChangeAmmo(consume: false);

    private void StoreAmmo()
    {
        if (!CanStoreAmmo()) return;
        if (!Player.GetAbility<MagazinerAbility>().TryChangeSHRAmmo(false, SHRKillRequests.RemainingCooldown(Player.Player))) return;
        SHRKillRequests.SetRemainingCooldown(Player.Player, SHRPlayerOptions.KillCooldown(Player));
    }

    private void Activate()
    {
        bool used = false;
        switch (Player.Role)
        {
            case RoleId.EvilDoorr:
                used = Player.GetAbility<DoorrAbility>().TryUseSHR();
                break;
            case RoleId.DoubleKiller:
                var target = FindTarget();
                if (target == null) break;
                SHRMurderSync.Murder(Player, target);
                used = true;
                var doubleKiller = Player.GetAbility<DoubleKillerAbility>();
                if (target.IsDead() && doubleKiller.DoubleKillerAbilityData.DoubleKillerCount.HasValue) doubleKiller.UseAbilityCount();
                break;
            case RoleId.EvilButtoner:
                var buttoner = Player.GetAbility<ButtonerAbility>();
                buttoner.UseAbilityCount();
                ButtonerAbility.ButtonerStartMeeting(buttoner);
                used = true;
                break;
            case RoleId.Magaziner:
                var ammo = Player.GetAbility<MagazinerAbility>();
                if (!ammo.TryChangeSHRAmmo(true, SHRKillRequests.RemainingCooldown(Player.Player))) break;
                SHRKillRequests.SetRemainingCooldown(Player.Player, ammo.Data.setKillTime);
                used = true;
                break;
            case RoleId.EvilMoving:
                if (storedPosition.HasValue)
                {
                    if (!SHRVanillaRpcRouter.Teleport(Player, storedPosition.Value)) break;
                    storedPosition = null;
                }
                else storedPosition = Player.transform.position;
                used = true;
                break;
            case RoleId.Teleporter:
                effectUntil = Time.unscaledTime + Math.Max(0.001f, Teleporter.TeleporterWaitingTime);
                used = true;
                break;
            case RoleId.Jammer:
                invisibleTarget = FindTarget(!Jammer.JammerCanUseAbilitiesAgainstImposter);
                used = invisibleTarget != null && Player.GetAbility<JammerAbility>().TryUseSHR(invisibleTarget);
                if (used)
                {
                    effectUntil = Time.unscaledTime + Jammer.JammerDurationTime;
                }
                break;
            case RoleId.Vampire:
                var vampire = Player.GetAbility<VampireAbility>();
                var dependent = FindTarget(false, vampire.SidekickAbility.CanCreateSidekickFor);
                used = dependent != null && vampire.SidekickAbility.TryCreateSidekick(dependent);
                if (used) SHRTestRoleAbility.SetSelfRole(Player.Player, RoleTypes.Impostor);
                break;
        }
        if (used) readyAt = (effectUntil > 0f ? effectUntil : Time.unscaledTime) + DefaultCooldown(Player.Role);
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || AmongUsClient.Instance.IsGameOver || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || Player.Data == null || !Player.IsAlive() || Player.Data.Disconnected)
        {
            StopEffect();
            return;
        }
        if (SHRAntiBlackout.Active || MeetingHud.Instance != null || ExileController.Instance != null || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
        if (!initialized)
        {
            initialized = true;
            ResetCooldown();
            string pet = Player.Data.DefaultOutfit.PetId;
            if (Player.Role == RoleId.Magaziner && (string.IsNullOrEmpty(pet)
                || pet.Contains("empty", StringComparison.OrdinalIgnoreCase) || pet is "pet_none" or "none"))
            {
                grantedPet = true;
                Player.Player.RpcSetPet("pet_test");
            }
        }
        if (shaped && effectUntil <= Time.unscaledTime) RevertShape();
        else if (TryConsumeExpiredEffect(ref effectUntil, Time.unscaledTime))
        {
            try
            {
                if (Player.Role == RoleId.Teleporter) Player.GetAbility<TeleporterAbility>()?.TryTeleportSHR();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SHR.Controls");
                Logger.Debug($"Delayed ability failed player={Player.PlayerId} role={Player.Role}: {ex}", "SHR.Controls");
            }
            finally
            {
                StopEffect();
                SHRPlayerOptions.Send(Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
            }
        }
        if (Player.Role is RoleId.Mafia or RoleId.DarkKiller)
        {
            bool allowed = Player.Role == RoleId.Mafia ? Mafia.IsKillFlag() : ModHelpers.IsElectrical();
            if (conditionalKillReady != allowed)
            {
                bool changed = conditionalKillReady.HasValue;
                conditionalKillReady = allowed;
                if (Player.Role == RoleId.Mafia)
                    SHRTestRoleAbility.SetSelfRole(Player.Player, allowed ? RoleTypes.Impostor : RoleTypes.Crewmate);
                if (Player.Role == RoleId.DarkKiller && changed)
                    SHRKillRequests.SetRemainingCooldown(Player.Player, SHRPlayerOptions.KillCooldown(Player));
                else
                    SHRPlayerOptions.Send(Player.Player, killCooldown: SHRKillRequests.RemainingCooldown(Player.Player), resetKill: true);
            }
        }
        if (Time.unscaledTime < nextInformation) return;
        nextInformation = Time.unscaledTime + Math.Max(0.1f, Player.Role == RoleId.Pursuer ? Pursuer.PursuerUpdateInterval : 0.25f);
        if (Player.Role == RoleId.Pursuer)
        {
            var target = ExPlayerControl.ExPlayerControls.Where(p => p != Player && p.IsAlive() && !p.Data.Disconnected && !p.IsImpostor())
                .OrderBy(p => Vector2.Distance(Player.transform.position, p.transform.position)).FirstOrDefault();
            information = target == null ? "" : DirectionArrow(target.transform.position.x - Player.transform.position.x,
                target.transform.position.y - Player.transform.position.y);
        }
        else if (informationUntil > 0f && Time.unscaledTime >= informationUntil) information = "";
    }

    internal static string DirectionArrow(float x, float y)
    {
        if (x == 0f && y == 0f) return "●";
        int index = ((int)Math.Round(Math.Atan2(y, x) * 4d / Math.PI) + 8) % 8;
        return new[] { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" }[index];
    }

    internal string Status => Player.Role switch
    {
        RoleId.DoubleKiller when Player.GetAbility<DoubleKillerAbility>()?.DoubleKillerAbilityData.DoubleKillerCount.HasValue == true
            => $" [サブ残り:{Math.Max(0, Player.GetAbility<DoubleKillerAbility>().Count)}]",
        RoleId.EvilButtoner => $" [残り:{Math.Max(0, Player.GetAbility<ButtonerAbility>()?.Count ?? 0)}]",
        RoleId.Jammer => $" [残り:{Player.GetAbility<JammerAbility>()?.SHRUsesRemaining ?? 0}]",
        RoleId.ShiftActor => (ShiftActor.ShiftActorIsLimitUses ? $" [調査残り:{Math.Max(0, discoveries)}]" : "")
            + (information.Length == 0 ? "" : "\n" + information),
        RoleId.Magaziner => $" [弾:{Player.GetAbility<MagazinerAbility>()?.Count ?? 0}]",
        RoleId.EvilMoving => storedPosition.HasValue ? " [位置登録済み]" : " [位置未登録]",
        RoleId.Teleporter when effectUntil > Time.unscaledTime => $" [転送まで:{Mathf.CeilToInt(effectUntil - Time.unscaledTime)}]",
        _ => information.Length == 0 ? "" : "\n" + information,
    };

    internal bool HandleShape(PlayerControl target)
    {
        if (Player.Role != RoleId.ShiftActor) return false;
        bool reverting = target.PlayerId == Player.PlayerId;
        if (reverting)
        {
            if (shaped) RevertShape(animate: true);
            else Player.Player.RpcRejectShapeshift();
            return true;
        }
        if (!SHRAbilityTriggerPatches.CanActivate(Player.Player) || Time.unscaledTime < readyAt || shaped
            || target.Data == null || target.Data.Disconnected
            || (ShiftActor.ShiftActorIsLimitUses && discoveries <= 0 && !ShiftActor.ShiftActorCanShapeshiftAfterUsesExhausted))
        {
            Player.Player.RpcRejectShapeshift();
            return true;
        }
        if (!ShiftActor.ShiftActorIsLimitUses || discoveries > 0)
        {
            discoveries--;
            ExPlayerControl subject = target;
            string role = ModTranslation.GetString(subject.Role.ToString());
            if (ShiftActor.ShiftActorCanSeeSharedRoles)
                foreach (var modifier in subject.ModifierRoleBases) role = modifier.ModifierMark(subject).Replace("{0}", role);
            information = ModTranslation.GetString("ShiftActorRoleMessage", subject.Data.PlayerName, role);
            informationUntil = Time.unscaledTime + ShiftActorAbility.SHIFTACTOR_MESSAGE_DURATION;
        }
        shaped = true;
        effectUntil = Time.unscaledTime + ShiftActor.ShiftActorDuration;
        Player.Player.RpcShapeshift(target, true);
        return true;
    }

    private void RevertShape(bool animate = false)
    {
        if (!shaped) return;
        shaped = false;
        effectUntil = 0f;
        if (Player.Data == null || Player.Data.Disconnected || !Player.IsAlive()) return;
        Player.Player.RpcShapeshift(Player.Player, animate);
        if (Exhausted && !animate) SHRTestRoleAbility.SetSelfRole(Player.Player, RoleTypes.Impostor);
        else if (Exhausted)
            new LateTask(() =>
            {
                if (Player?.Data != null && Player.IsAlive() && !Player.Data.Disconnected && !shaped
                    && Player.Role == RoleId.ShiftActor && Exhausted
                    && AmongUsClient.Instance?.AmHost == true && ModeManager.IsMode(ModeId.SuperHostRoles)
                    && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                    SHRTestRoleAbility.SetSelfRole(Player.Player, RoleTypes.Impostor);
            }, 1.5f, "SHR complete shift actor revert");
        readyAt = Time.unscaledTime + ShiftActor.ShiftActorCooldown;
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
    }

    private void StopEffect()
    {
        if (shaped) RevertShape();
        if (invisibleTarget != null)
        {
            Player.GetAbility<JammerAbility>()?.OnEffectEnds();
            invisibleTarget = null;
        }
        effectUntil = 0f;
    }

    public override void DetachToAlls()
    {
        StopEffect();
        if (grantedPet && Player.Data?.DefaultOutfit.PetId == "pet_test") Player.Player.RpcSetPet("");
    }
}
