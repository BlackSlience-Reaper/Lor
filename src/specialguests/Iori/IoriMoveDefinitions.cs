using System;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.specialguests.Iori;

internal enum IoriMoveIntentKind
{
    Hidden,
    Attack,
    AttackBuff,
    AttackDebuff,
    AttackDefend,
    Debuff,
    CardDebuff,
    Heal,
    StatusCard,
    DefendBuff,
    DefendDebuff,
    MultiAttack,
}

internal enum IoriMoveEffect
{
    None,
    SwitchStance,
    GainStrength,
    ApplyFrailAndWeak,
    ApplyBleedAndRapidWear,
    OfferPenetratingWoundChoice,
    HealPercentMaxHp,
    AddWounds,
    ApplyWeak,
    ApplyBlur,
    ApplyBleedPerHit,
}

internal readonly record struct IoriAscensionValue(
    int LowAscension,
    int HighAscension)
{
    internal int Resolve() =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            HighAscension,
            LowAscension);

    internal int ResolveForAscension(bool deadlyEnemies) =>
        deadlyEnemies ? HighAscension : LowAscension;
}

internal readonly record struct IoriAttackSegment(
    IoriAscensionValue Damage,
    LibraryDamageType DamageType,
    string Animation,
    string SfxFile)
{
    internal int ResolveDamage() => Damage.Resolve();
}

internal sealed record IoriMoveDefinition(
    string Id,
    IoriMoveIntentKind IntentKind,
    IReadOnlyList<IoriAttackSegment> Attacks,
    int HitCount = 0,
    int BlockAmount = 0,
    IoriMoveEffect Effect = IoriMoveEffect.None,
    IoriAscensionValue PrimaryEffect = default,
    IoriAscensionValue SecondaryEffect = default,
    int EffectTurns = 0)
{
    internal int ResolveIntentDamage() =>
        Attacks.Count == 0 ? 0 : Attacks[0].ResolveDamage();

    internal int ResolveIntentDamageForAscension(bool deadlyEnemies) =>
        Attacks.Count == 0
            ? 0
            : Attacks[0].Damage.ResolveForAscension(deadlyEnemies);

    internal int ResolveHitCount() =>
        HitCount > 0 ? HitCount : Attacks.Count;
}

internal static class IoriMoveDefinitions
{
    private static readonly IoriMoveDefinition Hidden = Move(
        "HIDDEN",
        IoriMoveIntentKind.Hidden,
        []);

    private static readonly IReadOnlyDictionary<IoriMove, IoriMoveDefinition>
        Definitions = new Dictionary<IoriMove, IoriMoveDefinition>
        {
            [IoriMove.StanceShift] = Move(
                "STANCE_SHIFT",
                IoriMoveIntentKind.AttackBuff,
                [Segment(17, 19, LibraryDamageType.None, "Slash", "Purple_Warp.ogg")],
                effect: IoriMoveEffect.SwitchStance),
            [IoriMove.SnakeSwordplay] = Move(
                "SNAKE_SWORDPLAY",
                IoriMoveIntentKind.Attack,
                [
                    Segment(5, 7, LibraryDamageType.Slash, "Slash", "Purple_Slash_Hori.ogg"),
                    Segment(5, 7, LibraryDamageType.Slash, "SlashS1", "Purple_Slash_VertDown.ogg"),
                ]),
            [IoriMove.SlitheringCut] = Move(
                "SLITHERING_CUT",
                IoriMoveIntentKind.AttackBuff,
                [Segment(11, 12, LibraryDamageType.Slash, "Slash", "Purple_Slash_VertDown.ogg")],
                effect: IoriMoveEffect.GainStrength,
                primaryEffect: new(1, 2)),
            [IoriMove.VioletSword] = Move(
                "VIOLET_SWORD",
                IoriMoveIntentKind.Debuff,
                [],
                effect: IoriMoveEffect.ApplyFrailAndWeak,
                primaryEffect: new(3, 3),
                secondaryEffect: new(3, 3)),
            [IoriMove.PreyLock] = Move(
                "PREY_LOCK",
                IoriMoveIntentKind.Attack,
                [
                    Segment(4, 6, LibraryDamageType.Pierce, "Pierce", "Purple_Stab_Stab1.ogg"),
                    Segment(4, 6, LibraryDamageType.Pierce, "PierceS1", "Purple_Stab_Stab2.ogg"),
                    Segment(4, 6, LibraryDamageType.Pierce, "PierceS2", "Purple_Stab_Stab1.ogg"),
                ]),
            [IoriMove.FangPenetration] = Move(
                "FANG_PENETRATION",
                IoriMoveIntentKind.AttackDebuff,
                [Segment(12, 13, LibraryDamageType.Pierce, "Pierce", "Purple_Stab_Stab2.ogg")],
                effect: IoriMoveEffect.ApplyBleedAndRapidWear,
                primaryEffect: new(12, 12),
                secondaryEffect: new(6, 6),
                effectTurns: 3),
            [IoriMove.PenetratingWound] = Move(
                "PENETRATING_WOUND",
                IoriMoveIntentKind.CardDebuff,
                [],
                effect: IoriMoveEffect.OfferPenetratingWoundChoice,
                primaryEffect: new(2, 3),
                secondaryEffect: new(1, 2)),
            [IoriMove.SwiftDownwardStrike] = Move(
                "SWIFT_DOWNWARD_STRIKE",
                IoriMoveIntentKind.Heal,
                [],
                effect: IoriMoveEffect.HealPercentMaxHp,
                primaryEffect: new(8, 8)),
            [IoriMove.PythonImpact] = Move(
                "PYTHON_IMPACT",
                IoriMoveIntentKind.AttackDefend,
                [Segment(13, 15, LibraryDamageType.Blunt, "BluntStance", "Purple_Hit_Hori.ogg")],
                blockAmount: 11),
            [IoriMove.DuelDance] = Move(
                "DUEL_DANCE",
                IoriMoveIntentKind.StatusCard,
                [],
                effect: IoriMoveEffect.AddWounds,
                primaryEffect: new(2, 3)),
            [IoriMove.EndlessFlow] = Move(
                "ENDLESS_FLOW",
                IoriMoveIntentKind.DefendDebuff,
                [],
                blockAmount: 24,
                effect: IoriMoveEffect.ApplyWeak,
                primaryEffect: new(5, 5)),
            [IoriMove.ScaledBarrier] = Move(
                "SCALED_BARRIER",
                IoriMoveIntentKind.DefendBuff,
                [],
                blockAmount: 40,
                effect: IoriMoveEffect.ApplyBlur,
                primaryEffect: new(1, 2)),
            [IoriMove.NoEscape] = Move(
                "NO_ESCAPE",
                IoriMoveIntentKind.AttackDefend,
                [Segment(16, 17, LibraryDamageType.Slash, "Slash", "Purple_Slash_VertUp.ogg")],
                blockAmount: 30),
            [IoriMove.PhantomDance] = Move(
                "PHANTOM_DANCE",
                IoriMoveIntentKind.MultiAttack,
                [
                    Segment(5, 6, LibraryDamageType.Slash, "PhantomDanceSlashA", "Purple_Slash_Hori.ogg"),
                    Segment(5, 6, LibraryDamageType.Blunt, "PhantomDanceBlunt", "Purple_Hit_Vert.ogg"),
                    Segment(5, 6, LibraryDamageType.Pierce, "PhantomDancePierce", "Purple_Stab_Stab2.ogg"),
                    Segment(5, 6, LibraryDamageType.Slash, "PhantomDanceSlashB", "Purple_Slash_VertUp.ogg"),
                ],
                effect: IoriMoveEffect.ApplyBleedPerHit,
                primaryEffect: new(6, 6)),
        };

    internal static IoriMoveDefinition Get(IoriMove move) =>
        Definitions.TryGetValue(move, out IoriMoveDefinition? definition)
            ? definition
            : Hidden;

    internal static IReadOnlyList<IoriMove> GetStanceMoves(IoriStance stance) =>
        stance switch
        {
            IoriStance.Slash =>
                [IoriMove.SnakeSwordplay, IoriMove.SlitheringCut, IoriMove.VioletSword],
            IoriStance.Pierce =>
                [IoriMove.PreyLock, IoriMove.FangPenetration, IoriMove.PenetratingWound],
            IoriStance.Blunt =>
                [IoriMove.SwiftDownwardStrike, IoriMove.PythonImpact, IoriMove.DuelDance],
            IoriStance.Defense =>
                [IoriMove.EndlessFlow, IoriMove.ScaledBarrier, IoriMove.NoEscape],
            _ => Array.Empty<IoriMove>(),
        };

    private static IoriMoveDefinition Move(
        string id,
        IoriMoveIntentKind intentKind,
        IReadOnlyList<IoriAttackSegment> attacks,
        int hitCount = 0,
        int blockAmount = 0,
        IoriMoveEffect effect = IoriMoveEffect.None,
        IoriAscensionValue primaryEffect = default,
        IoriAscensionValue secondaryEffect = default,
        int effectTurns = 0) =>
        new(
            id,
            intentKind,
            attacks,
            hitCount,
            blockAmount,
            effect,
            primaryEffect,
            secondaryEffect,
            effectTurns);

    private static IoriAttackSegment Segment(
        int lowAscensionDamage,
        int highAscensionDamage,
        LibraryDamageType damageType,
        string animation,
        string sfxFile) =>
        new(
            new IoriAscensionValue(
                lowAscensionDamage,
                highAscensionDamage),
            damageType,
            animation,
            sfxFile);
}
