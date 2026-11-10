using System.IO;
using AmongUs.GameOptions;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using SuperNewRoles.CustomOptions.Categories;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRNunTests
{
    [Fact]
    public void OnlyTheFirstCooldownUsesTheInitialCooldownSetting()
    {
        Assert.Equal(10f, SHRCrewActionAbility.CooldownAfterReset(RoleId.Nun, 30f, InitialCooldownType.TenSeconds, true));
        Assert.Equal(0f, SHRCrewActionAbility.CooldownAfterReset(RoleId.Nun, 30f, InitialCooldownType.Immediate, true));
        Assert.Equal(30f, SHRCrewActionAbility.CooldownAfterReset(RoleId.Nun, 30f, InitialCooldownType.TenSeconds, false));
        Assert.Equal(30f, SHRCrewActionAbility.CooldownAfterReset(RoleId.Nun, 30f, InitialCooldownType.Immediate, false));
    }

    [Fact]
    public void NunUsesPhantomAndOnlyAppearsOnAirship()
    {
        Assert.True(SHRRoleTypes.HasImplementedSupport(RoleId.Nun));
        Assert.Equal(RoleTypes.Phantom, SHRRoleTypes.GetInitialRole(RoleId.Nun));
        Assert.True(SHRCrewActionAbility.Supports(RoleId.Nun));
        Assert.False(SHRCrewActionAbility.UsesEngineer(RoleId.Nun));
        for (byte map = 0; map < 6; map++)
            Assert.Equal(map == 4, SHRRoleTypes.ShouldShowInSettings(RoleId.Nun, true, map));
    }

    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)255, false)]
    public void NativePlatformStateUsesTheActualSystemSerializationLayout(byte sequence, bool left)
    {
        using var stream = new MemoryStream(SHRNunPlatform.EncodeState(sequence, left));
        using var reader = new BinaryReader(stream);
        Assert.Equal(sequence, reader.ReadByte());
        Assert.Equal(uint.MaxValue, reader.ReadUInt32());
        Assert.Equal(left, reader.ReadBoolean());
        Assert.Equal(stream.Length, stream.Position);
    }

    [Theory]
    [InlineData((byte)0, (byte)1)]
    [InlineData((byte)254, (byte)255)]
    [InlineData((byte)255, (byte)0)]
    public void PlatformUseSequenceWrapsAsAByte(byte current, byte next)
        => Assert.Equal(next, SHRNunPlatform.NextSequence(current));

    [Theory]
    [InlineData(1.24f, true)]
    [InlineData(1.25f, true)]
    [InlineData(1.26f, false)]
    public void OnlyPlayersActuallyOnThePlatformAreCarried(float distance, bool expected)
        => Assert.Equal(expected, SHRNunPlatform.IsOnPlatform(distance));
}
