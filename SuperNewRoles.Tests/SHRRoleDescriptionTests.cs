using System;
using AmongUs.GameOptions;
using SuperNewRoles.HelpMenus;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

[Collection("ModTranslation")]
public class SHRRoleDescriptionTests
{
    [Fact]
    public void DescriptionSelectionUsesSHROnlyInSHRAndFallsBackWhenMissing()
    {
        Assert.Equal("ToiletFan.Description", RoleDetailMenu.GetDescriptionKey("ToiletFan", false,
            _ => throw new InvalidOperationException("Normal mode must not probe SHR descriptions.")));
        Assert.Equal("ToiletFan.Description.SHR", RoleDetailMenu.GetDescriptionKey("ToiletFan", true,
            key => key == "ToiletFan.Description.SHR"));
        Assert.Equal("SilverBullet.Description", RoleDetailMenu.GetDescriptionKey("SilverBullet", true, _ => false));
    }

    [Theory]
    [InlineData(SupportedLangs.Japanese)]
    [InlineData(SupportedLangs.English)]
    [InlineData(SupportedLangs.SChinese)]
    [InlineData(SupportedLangs.TChinese)]
    public void EveryImplementedSHRRoleHasANonemptyLocalizedDescription(SupportedLangs language)
    {
        try
        {
            ModTranslation.ClearTestTranslationCsv();
            ModTranslation.SetTestLanguage(language);
            ModTranslation.Cleanup();
            ModTranslation.Load();
            foreach (var role in Enum.GetValues<RoleId>())
            {
                if (!SHRRoleTypes.HasImplementedSupport(role)) continue;
                string key = $"{role}.Description.SHR";
                Assert.True(ModTranslation.TryGetString(key, out var text), key);
                Assert.False(string.IsNullOrWhiteSpace(text), key);
                Assert.DoesNotContain("\\n", text);
                if (role != RoleId.HomeSecurityGuard)
                    Assert.StartsWith(ModTranslation.GetString($"{role}.Description").Split('\n')[0].Split('。', '．')[0], text);
                if (role is RoleId.DarkKiller or RoleId.Mafia or RoleId.Celebrity or RoleId.Doppelganger
                    or RoleId.Technician or RoleId.Stuntman)
                    Assert.Equal(ModTranslation.GetString($"{role}.Description"), text);
                Assert.Equal(key, RoleDetailMenu.GetDescriptionKey(role.ToString(), true,
                    candidate => ModTranslation.TryGetString(candidate, out _)));
            }
        }
        finally
        {
            ModTranslation.SetTestLanguage(SupportedLangs.English);
            ModTranslation.Cleanup();
        }
    }

    [Fact]
    public void JapaneseSHRDescriptionsExplainCurrentControlsAndKeepTheNormalDescriptions()
    {
        try
        {
            ModTranslation.ClearTestTranslationCsv();
            ModTranslation.SetTestLanguage(SupportedLangs.Japanese);
            ModTranslation.Cleanup();
            ModTranslation.Load();
            Assert.Contains("ペット", ModTranslation.GetString("ToiletFan.Description.SHR"));
            Assert.Contains("ベント", ModTranslation.GetString("ToiletFan.Description.SHR"));
            Assert.Contains("ペット", ModTranslation.GetString("NiceButtoner.Description.SHR"));
            Assert.Contains("/bt", ModTranslation.GetString("NiceGuesser.Description.SHR"));
            Assert.Contains("ファントム", ModTranslation.GetString("SelfBomber.Description.SHR"));
            Assert.Contains("勝利にカウントされません", ModTranslation.GetString("Sheriff.Description.SHR"));
            Assert.Contains("ライン精査など、白黒つけたいとき", ModTranslation.GetString("Sheriff.Description.SHR"));
            Assert.Contains(ModTranslation.GetString("Jackal.Description"), ModTranslation.GetString("Jackal.Description.SHR"));
            Assert.Contains("「トイレ！」ボタン", ModTranslation.GetString("ToiletFan.Description"));
            Assert.False(ModTranslation.TryGetString("SilverBullet.Description.SHR", out _));
        }
        finally
        {
            ModTranslation.SetTestLanguage(SupportedLangs.English);
            ModTranslation.Cleanup();
        }
    }
}
