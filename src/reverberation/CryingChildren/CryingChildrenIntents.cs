using System;
using System.Collections.Generic;
using System.Linq;
using LibraryOfRuina.intents;
using LibraryOfRuina.specialguests.Iori;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using static LibraryOfRuina.reverberation.CryingChildren.CryingChildrenRules;

namespace LibraryOfRuina.reverberation.CryingChildren;

internal static class CryingChildrenIntents
{
    internal static string Key(CryingMove move) => "CRYING_CHILDREN_" + move.ToString().ToUpperInvariant();

    internal static AbstractIntent Create(CryingChildMonsterBase monster, CryingMove move)
    {
        string key = Key(move);
        switch (move)
        {
            case CryingMove.ColdSun:
                return new CryingBlockIntent(ColdSunBlock, key);
            case CryingMove.DespairBrand:
                return new CombinedAttackBuffIntent(() => Damage(move), () => BrandHits,
                    key,
                    // 保留说明所需的情感数值；意图不显示额外的情感 badge。
                    IntentBadge.Custom("powers/library_passive_purple.png", BrandEmotion)
                        .WithoutVisual());
            case CryingMove.EmotionalTurbulence:
                return new CryingAttackIntent(Damage(move), () => TurbulenceHits, key);
            case CryingMove.BurningCourage:
                return new CryingStatusIntent<Burn>(DamageValue(CourageCards, CourageHighCards), PileType.Hand, key);
            case CryingMove.SelfRestraint:
                return new CombinedAttackDebuffIntent(() => Damage(move), () => RestraintHits,
                    key,
                    IntentBadge.FromPower<LibraryWeakPower>(RestraintWeak),
                    IntentBadge.FromPower<LibraryBindingPower>(RestraintBinding),
                    IntentBadge.FromPower<LibraryDisarmPower>(RestraintDisarm));
            case CryingMove.SearingPain:
                return new CryingDebuffIntent(key, PainStacks, IntentBadge.FromPower<IoriCardPlayPainPower>(PainStacks));
            case CryingMove.FierceMomentum:
                return new CombinedDefendBuffIntent(MomentumBlock, key, IntentBadge.FromPower<CryingSwiftPower>(MomentumSwift).WithoutVisual());
            case CryingMove.BlazingWill:
                return new CryingAttackIntent(Damage(move), monster.WillHits, key);
            case CryingMove.ScorchedAsh:
                return new IndiscriminateAttackIntent(() => Damage(move), () => AshHits, key,
                    _ => monster.LivingPlayers(), IntentBadge.FromPower<LibraryBurnPower>(() => monster.BurnApplied(AshBurn)))
                {
                    CombinedEffect = GroupAttackCombinedEffect.Debuff
                };
            case CryingMove.Murmur:
                return new CombinedAttackDefendIntent(() => Damage(move), () => MurmurHits,
                    key, MurmurBlock);
            case CryingMove.FoulWings:
                return new CryingDebuffIntent(key, WingsStrengthLoss,
                    IntentBadge.FromPower<StrengthPower>(-WingsStrengthLoss),
                    IntentBadge.FromPower<DexterityPower>(-WingsDexterityLoss));
            case CryingMove.EndlessTorment:
                return new CryingStatusIntent<Dazed>(DamageValue(TormentCards, TormentHighCards), PileType.Discard, key);
            default:
                return new HiddenIntent();
        }
    }

    internal static IReadOnlyList<IntentTargetLineTarget> PlayerLines(Creature owner) =>
        owner.CombatState?.PlayerCreatures
            .Where(target => target.IsAlive)
            .Select(target => new IntentTargetLineTarget(target))
            .ToArray() ?? [];
}

internal sealed class CryingAttackIntent(int damage, Func<int> repeats, string key)
    : MultiAttackIntent(damage, repeats)
{
    protected override LocString IntentLabelFormat =>
        new("intents", Repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("Damage", GetSingleDamage(targets, owner));
        description.Add("Repeat", Repeats);
        return description;
    }
}

internal sealed class CryingBlockIntent(int block, string key) : DefendIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("BlockAmount", block);
        return description;
    }
}

internal sealed class CryingDebuffIntent(string key, int amount, params IntentBadge[] badges)
    : BadgedDebuffIntent(badges, amount, key), IIntentTargetLineProvider
{
    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) => CryingChildrenIntents.PlayerLines(owner);
}

internal sealed class CryingStatusIntent<TCard>(int count, PileType pile, string key)
    : DetailedStatusCardIntent<TCard>(count, pile, scopeText: null, showSingleTargetMarker: false),
      IIntentTargetLineProvider
    where TCard : CardModel
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("Amount", CardCount);
        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) => CryingChildrenIntents.PlayerLines(owner);
}
