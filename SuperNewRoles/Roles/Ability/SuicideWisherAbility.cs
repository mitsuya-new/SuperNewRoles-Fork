using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability.CustomButton;
using UnityEngine;
using SuperNewRoles.Mode;

namespace SuperNewRoles.Roles.Ability;

public class SuicideWisherAbility : CustomButtonBase
{
    public float CoolTime;
    public SuicideWisherAbility(float coolTime)
    {
        CoolTime = coolTime;
    }
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("ShermansServantSuicideButton.png");
    public override string buttonText => ModTranslation.GetString("SuicideWisherButtonText");
    protected override KeyType keytype => KeyType.Ability1;
    public override float DefaultTimer => CoolTime;

    public override bool CheckIsAvailable()
    {
        return PlayerControl.LocalPlayer.CanMove;
    }

    public override void OnClick()
    {
        // 自殺処理の実行
        RpcSuicide(ExPlayerControl.LocalPlayer);
    }

    [CustomRPC]
    public static void RpcSuicide(ExPlayerControl player)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles))
        {
            if (AmongUsClient.Instance?.AmHost == true)
                SHRMurderSync.Murder(player, player, finalStatus: FinalStatus.Suicide);
            return;
        }
        // 自殺処理のRPC
        player.CustomDeath(CustomDeathType.Suicide);
    }
}
