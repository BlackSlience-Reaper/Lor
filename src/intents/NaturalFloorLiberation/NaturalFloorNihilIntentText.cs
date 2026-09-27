using System;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.intents.NaturalFloorLiberation;

internal static class NaturalFloorNihilIntentText
{
    private const string Prefix = "NATURAL_NIHIL_";

    internal static string PlayerDescriptionKey(string key) =>
        key.Replace(".description", ".playerDescription", StringComparison.Ordinal);

    internal static void AddVariables(LocString description, string? key, Creature owner)
    {
        if (key == null || !key.StartsWith(Prefix, StringComparison.Ordinal)
            || !Enum.TryParse(
                key[Prefix.Length..].Replace(".playerDescription", "").Replace(".description", ""),
                ignoreCase: true,
                out NaturalFloorNihilAction action))
        {
            return;
        }

        NaturalFloorNihilMove move = NaturalFloorNihilMoves.Get(action);
        description.Add("OwnerName", owner.Name);
        description.Add("Damage", move.Damage);
        if (action == NaturalFloorNihilAction.TyrantPath)
        {
            description.Add("GirlDamage", NaturalFloorNihilMoves.TyrantGirlDamage);
        }

        description.Add("Repeat", move.Hits);
        description.Add("Block", move.Block);
        description.Add("Bleed", move.Bleed);
        description.Add("Corrosion", move.Corrosion);
        description.Add("HatredTurns", move.HatredTurns);
        description.Add("Strength", move.Strength);
        description.Add("Strong", move.Strong);
        description.Add("StrongTurns", move.StrongTurns);
        description.Add("OtherStrong", move.OtherStrong);
        description.Add("Endurance", move.HopeEndurance);
        description.Add("Weak", move.Weak);
        description.Add("WeakTurns", move.WeakTurns);
        description.Add("HpLossPercent", move.DirectHpLossPercent);
        description.Add("SelfHpLoss", move.SelfHpLoss);
    }
}
