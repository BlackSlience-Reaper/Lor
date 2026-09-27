using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.powers.BigBadWolf;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

internal static class CounterIntentSupport
{
    public static AbstractIntent[] Add(AbstractIntent[] baseIntents, params ICounterIntent[] counterIntents)
    {
        if (!LibrarySecondAscensionState.HasCounterIntentLevel() || counterIntents.Length == 0)
        {
            return baseIntents;
        }

        return baseIntents.Concat(counterIntents.Cast<AbstractIntent>()).ToArray();
    }

    public static int Value(int upgradeI, int upgradeII) =>
        LibrarySecondAscensionState.HasCounterIntentUpgradeII() ? upgradeII : upgradeI;

    public static Task GainStrength(Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.Apply<StrengthPower>(owner, Value(upgradeI, upgradeII), owner, null);

    public static Task GainNextTurnStrength(Creature owner, int amount) =>
        PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(owner, amount, owner, null);

    public static Task GainDurationEndurance(Creature owner, int upgradeI, int upgradeII, int turns = 1) =>
        LibraryPowerCmd.Apply<LibraryEndurancePower>(
            owner,
            Value(upgradeI, upgradeII),
            turns,
            owner,
            null);

    public static Task GainDurationStrong(Creature owner, int upgradeI, int upgradeII, int turns = 1) =>
        LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            owner,
            Value(upgradeI, upgradeII),
            turns - 1,
            IsPermanent: false,
            owner,
            null);

    public static async Task GainTemporaryThorns(Creature owner, int upgradeI, int upgradeII)
    {
        int amount = Value(upgradeI, upgradeII);
        await PowerCmdCompat.Apply<ThornsPower>(owner, amount, owner, null);
        await PowerCmdCompat.Apply<BigBadWolfTemporaryThornsPower>(owner, amount, owner, null, silent: true);
    }

    public static Task ApplyBurn(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.Apply<LibraryBurnPower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyBleed(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.Apply<LibraryBleedingPower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyWeak(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.ApplyDebuff<WeakPower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyVulnerable(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.ApplyDebuff<VulnerablePower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyFrail(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.ApplyDebuff<FrailPower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyPoison(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        PowerCmdCompat.ApplyDebuff<PoisonPower>(target, Value(upgradeI, upgradeII), owner, null);

    public static Task ApplyStrengthDown(Creature target, Creature owner, int upgradeI, int upgradeII) =>
        LibraryPowerCmd.Apply<LibraryWeakPower>(
            target,
            Value(upgradeI, upgradeII),
            turns: 1,
            owner,
            null);

    public static async Task ApplyStrengthAndDexterityDown(Creature target, Creature owner, int upgradeI, int upgradeII)
    {
        int amount = Value(upgradeI, upgradeII);
        await PowerCmdCompat.Apply<StrengthPower>(target, -amount, owner, null);
        await PowerCmdCompat.Apply<DexterityPower>(target, -amount, owner, null);
    }

    public static Task AddBurnToDiscard(Creature target, int upgradeI, int upgradeII) =>
        CardPileCmdCompat.AddToCombatAndPreview<Burn>(
            target,
            PileType.Discard,
            Value(upgradeI, upgradeII),
            addedByPlayer: false);

    public static Task AddDazedToDraw(Creature target, int upgradeI, int upgradeII) =>
        CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
            target,
            PileType.Draw,
            Value(upgradeI, upgradeII),
            addedByPlayer: false);

    public static async Task ClearDebuffs(Creature owner)
    {
        IReadOnlyList<PowerModel> debuffs = owner.Powers
            .Where(static power => power.Type == PowerType.Debuff)
            .ToArray();
        if (debuffs.Count == 0)
        {
            return;
        }

        if (LibrarySecondAscensionState.HasCounterIntentUpgradeII())
        {
            foreach (PowerModel debuff in debuffs)
            {
                await PowerCmd.Remove(debuff);
            }

            return;
        }

        PowerModel selected = owner.CombatState?.RunState.Rng.Niche.NextItem(debuffs) ?? debuffs[0];
        await PowerCmd.Remove(selected);
    }
}
