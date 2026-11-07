using System.Linq;
using System.Reflection;
using SuperNewRoles.Mode;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class ShrVanillaRpcRouterTests
{
    [Theory]
    [InlineData(CustomDeathType.Kill, "Kill")]
    [InlineData(CustomDeathType.Exile, "Exile")]
    [InlineData(CustomDeathType.Suicide, "Suicide")]
    [InlineData(CustomDeathType.KillWithoutKillAnimation, "None")]
    [InlineData(CustomDeathType.BuskerFakeDeath, "None")]
    public void StandardDeathOverloadsAreRoutedWithoutFlatteningSpecialRoleBehavior(
        CustomDeathType death, string expected)
    {
        foreach (var method in typeof(CustomDeathExtensions).GetMethods().Where(m => m.Name == "RpcCustomDeath"))
        {
            var args = new object[method.GetParameters().Length];
            args[^1] = death;
            Assert.Equal(expected, ShrVanillaRpcRouter.Classify(method, args).ToString());
        }
    }

    [Fact]
    public void TasksAndStandardPlayerOperationsHaveNativeRoutes()
    {
        Assert.Equal(ShrVanillaRpcRouter.Operation.SetTasks,
            ShrVanillaRpcRouter.Classify(typeof(CustomTaskAbility).GetMethod("RpcUncheckedSetTasks")!, []));
        AssertRoute("RpcExiledCustom", ShrVanillaRpcRouter.Operation.Exile);
        AssertRoute("RpcCustomMurderPlayer", ShrVanillaRpcRouter.Operation.MurderResult);
        AssertRoute("RpcCustomReportDeadBody", ShrVanillaRpcRouter.Operation.Report);
        AssertRoute("RpcCustomSnapTo", ShrVanillaRpcRouter.Operation.SnapTo);
        AssertRoute("RpcShapeshiftModded", ShrVanillaRpcRouter.Operation.Shapeshift);
        AssertRoute("RpcCustomSetRole", ShrVanillaRpcRouter.Operation.None);
        AssertRoute("RpcCustomSetModifierRole", ShrVanillaRpcRouter.Operation.None);
    }

    private static void AssertRoute(string method, ShrVanillaRpcRouter.Operation expected)
        => Assert.Equal(expected, ShrVanillaRpcRouter.Classify(typeof(PlayerControlRpcExtensions).GetMethod(method)!, []));
}
