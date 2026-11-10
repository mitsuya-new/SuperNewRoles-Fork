using System.Linq;
using System.Reflection;
using SuperNewRoles.Mode;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRVanillaRpcRouterTests
{
    [Theory]
    [InlineData(CustomDeathType.Kill, "Kill")]
    [InlineData(CustomDeathType.Exile, "Exile")]
    [InlineData(CustomDeathType.FalseCharge, "FalseCharge")]
    [InlineData(CustomDeathType.Suicide, "Suicide")]
    [InlineData(CustomDeathType.Revange, "Revenge")]
    [InlineData(CustomDeathType.VampireWithDead, "VampireLinkedDeath")]
    [InlineData(CustomDeathType.VampireWithDeadNonDeadbody, "VampireLinkedExile")]
    [InlineData(CustomDeathType.KillWithoutKillAnimation, "None")]
    [InlineData(CustomDeathType.BuskerFakeDeath, "None")]
    public void StandardDeathOverloadsAreRoutedWithoutFlatteningSpecialRoleBehavior(
        CustomDeathType death, string expected)
    {
        foreach (var method in typeof(CustomDeathExtensions).GetMethods().Where(m => m.Name == "RpcCustomDeath"))
        {
            var args = new object[method.GetParameters().Length];
            args[^1] = death;
            Assert.Equal(expected, SHRVanillaRpcRouter.Classify(method, args).ToString());
        }
    }

    [Fact]
    public void TasksAndStandardPlayerOperationsHaveNativeRoutes()
    {
        Assert.Equal(SHRVanillaRpcRouter.Operation.Shapeshift,
            SHRVanillaRpcRouter.Classify(typeof(SuperNewRoles.Events.PCEvents.TryShapeshiftPatch).GetMethod("RpcCustomShapeshift")!, []));
        Assert.Equal(SHRVanillaRpcRouter.Operation.SetTasks,
            SHRVanillaRpcRouter.Classify(typeof(CustomTaskAbility).GetMethod("RpcUncheckedSetTasks")!, []));
        AssertRoute("RpcExiledCustom", SHRVanillaRpcRouter.Operation.Exile);
        AssertRoute("RpcCustomMurderPlayer", SHRVanillaRpcRouter.Operation.MurderResult);
        AssertRoute("RpcCustomReportDeadBody", SHRVanillaRpcRouter.Operation.Report);
        AssertRoute("RpcCustomSnapTo", SHRVanillaRpcRouter.Operation.SnapTo);
        AssertRoute("RpcShapeshiftModded", SHRVanillaRpcRouter.Operation.Shapeshift);
        AssertRoute("RpcCustomSetRole", SHRVanillaRpcRouter.Operation.None);
        AssertRoute("RpcCustomSetModifierRole", SHRVanillaRpcRouter.Operation.None);
    }

    private static void AssertRoute(string method, SHRVanillaRpcRouter.Operation expected)
        => Assert.Equal(expected, SHRVanillaRpcRouter.Classify(typeof(PlayerControlRpcExtensions).GetMethod(method)!, []));
}
