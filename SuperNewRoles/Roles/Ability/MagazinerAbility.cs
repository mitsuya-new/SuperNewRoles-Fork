using System;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability.CustomButton;
using UnityEngine;

namespace SuperNewRoles.Roles.Ability;

public record MagazinerAbilityData(float setKillTime, bool ammoLimitEnabled, int maxAmmo);

public class MagazinerAbility : CustomButtonBase, IAbilityCount
{
    public MagazinerAbilityData Data { get; }
    private bool isConsumeMode = false;

    public override float DefaultTimer => 0f;

    public override string buttonText => isConsumeMode
        ? ModTranslation.GetString("MagazinerGetButtonName")
        : ModTranslation.GetString("MagazinerAddButtonName");

    public override Sprite Sprite => isConsumeMode
        ? AssetManager.GetAsset<Sprite>("MagazinerGetButton.png")
        : AssetManager.GetAsset<Sprite>("MagazinerAddButton.png");

    protected override KeyType keytype => KeyType.Ability1;

    public override ShowTextType showTextType => ShowTextType.ShowWithCount;

    public override string showText => isConsumeMode ? ModTranslation.GetString("RemainingText", Count) : "";

    public MagazinerAbility(MagazinerAbilityData data)
    {
        Data = data;
    }

    internal static bool CanChangeSHRAmmo(bool consume, int count, float remaining, bool limited, int maximum)
        => float.IsFinite(remaining) && count >= 0 && (consume ? count > 0 && remaining > 0f
            : remaining <= 0f && (!limited || count < maximum));

    internal bool TryChangeSHRAmmo(bool consume, float remaining)
    {
        if (AmongUsClient.Instance?.AmHost != true || !Mode.ModeManager.IsMode(ModeId.SuperHostRoles)
            || !CanChangeSHRAmmo(consume, Count, remaining, Data.ammoLimitEnabled, Data.maxAmmo)) return false;
        RpcSetSHRAmmo(Count + (consume ? -1 : 1));
        Logger.Debug($"Ammo player={Player.PlayerId} consume={consume} count={Count} killRemaining={remaining}", "SHR.Controls");
        return true;
    }

    [CustomRPC]
    public void RpcSetSHRAmmo(int count)
    {
        if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles)) Count = Math.Max(0, count);
    }

    public override bool CheckIsAvailable()
    {
        if (isConsumeMode)
        {
            // 弾薬が1以上あり、キルボタンがクールダウン中の場合のみ使用可能
            return Count >= 1 && PlayerControl.LocalPlayer.killTimer >= 0f;
        }
        else
        {
            // キルボタンがクールダウン中でない場合で、弾数制限がない場合または最大弾数未満の場合のみ使用可能
            bool killTimerReady = PlayerControl.LocalPlayer.killTimer <= 0f;
            bool canAddAmmo = !Data.ammoLimitEnabled || Count < Data.maxAmmo;
            return killTimerReady && canAddAmmo;
        }
    }

    public override void OnUpdate()
    {
        // モードの切り替え判定
        bool shouldBeConsumeMode = Count >= 1 && PlayerControl.LocalPlayer.killTimer > 0f;
        if (shouldBeConsumeMode != isConsumeMode)
            isConsumeMode = shouldBeConsumeMode;
        base.OnUpdate();
    }

    public override void OnClick()
    {
        if (isConsumeMode)
        {
            UseAmmunition();
        }
        else
        {
            AddAmmunition();
        }
    }

    public void UseAmmunition()
    {
        if (Count > 0)
        {
            this.UseAbilityCount();

            // クールダウンを短縮
            PlayerControl.LocalPlayer.SetKillTimer(Data.setKillTime);
        }
    }

    public void AddAmmunition()
    {
        // 弾数制限チェック
        if (Data.ammoLimitEnabled && Count >= Data.maxAmmo)
            return;
            
        RpcAddCount();
        // キルクールダウンをリセット
        Player.ResetKillCooldown();
    }

    [CustomRPC]
    public void RpcAddCount()
    {
        Count++;
    }
}
