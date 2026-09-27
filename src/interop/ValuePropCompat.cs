using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.interop;

internal static class ValuePropCompat
{
    public static bool IsPoweredAttack(ValueProp props)
    {
        return props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
    }

    public static bool IsPoweredCardOrMonsterMoveBlock(ValueProp props)
    {
        return props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
    }

    public static bool IsCardOrMonsterMove(ValueProp props)
    {
        return props.HasFlag(ValueProp.Move);
    }

    public static bool IsUnpowered(ValueProp props)
    {
        return props.HasFlag(ValueProp.Unpowered);
    }
}
