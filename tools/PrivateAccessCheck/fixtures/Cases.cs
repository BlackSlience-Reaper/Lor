// Regression cases for PrivateAccessCheck (never compiled). expected.txt lists what must be reported.
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;

internal static class Reported
{
    private const string RoomsFieldName = "_rooms";

    // Member name held in a constant.
    private static readonly FieldInfo? ConstantName = AccessTools.Field(typeof(ActModel), RoomsFieldName);

    // Name only reaches the call four lines later.
    private static readonly FieldInfo? Wrapped =
        AccessTools.Field(
            typeof(ActModel),
            // comment in between
            "_rooms");

    // Name passed in from elsewhere; still a lookup by name.
    private static FieldInfo? ByVariable(string member) => AccessTools.Field(typeof(ActModel), member);

    private static MemberInfo[] Members() => typeof(ActModel).GetMember("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    private static Expression Slots(Expression node) => Expression.Property(node, "EncounterSlots");

    private static Expression CallByName(Expression node) => Expression.Call(node, nameof(ActModel.ToString), null);

    private static object? ViaTraverse(object target) => Traverse.Create(target).Field("_x").GetValue();

    private static bool Prepare() => true;

    // A method after Prepare without access modifiers is still its own method, not part of Prepare.
    static FieldInfo? AfterPrepare() => typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    // Null-conditional call.
    private static MethodInfo? Conditional(Type? type) => type?.GetMethod("MoveNext");
}

internal static class NotReported
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(ActModel), "PullNextEncounter");

    private static IEnumerable<MethodBase> TargetMethods() =>
        new[] { "A", "B" }.Select(name => AccessTools.Method(typeof(ActModel), name));

    [HarmonyTargetMethod]
    private static MethodBase PickTarget() => AccessTools.Method(typeof(ActModel), "PullNextEvent");

    private static Type Runtime(object value) => value.GetType();

    private static Expression WithInfo(Expression node, PropertyInfo info) => Expression.Property(node, info);
}
