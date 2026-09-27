namespace LibraryOfRuina.specialguests.Rnfmabj;

public enum RnfmabjMove
{
    None = -1,
    ExecuteAttack,
    ExecuteGuard,
    ExecuteAlert,
    ExecuteRepair,
    ExecuteHold,
    RepairAll,
    TwistedBlade,
    GiantPunch,
    GiantPalm,
    Flurry,
    OminousBrand,
    LockTarget,
}

internal enum RnfmabjMoveIntentKind
{
    Hidden,
    Buff,
    Debuff,
    CardDebuff,
    Defend,
    Heal,
    AttackDefend,
    AttackDebuff,
    AttackCardDebuff,
    MultiAttack,
    GroupAttackDebuff,
}

internal enum RnfmabjMoveEffect
{
    None,
    ExecuteAttack,
    ExecuteGuard,
    ExecuteAlert,
    RepairTarget,
    BlockAllGuests,
    RepairAllGuests,
    ApplyParalysisAndWeak,
    ApplyCorrosion,
    TransformDrawPileCardToWound,
}

internal sealed record RnfmabjMoveDefinition(
    RnfmabjMoveIntentKind IntentKind,
    int MinimumDamage,
    int MaximumDamage,
    int Hits,
    LibraryDamageType DamageType,
    string Animation,
    float AnimationDelaySeconds,
    int LeftHandBlockAmount = 0,
    int RightHandBlockAmount = 0,
    RnfmabjMoveEffect Effect = RnfmabjMoveEffect.None,
    int EffectAmount = 0,
    int EffectDurationTurns = 0,
    string? WindupSfx = null,
    string? ImpactSfx = null,
    string? AlternateImpactSfx = null,
    string? BlockSfx = null)
{
    public bool IsAttack => Hits > 0;

    public int GetHandBlockAmount(bool isLeftHand) =>
        isLeftHand ? LeftHandBlockAmount : RightHandBlockAmount;
}

internal static class RnfmabjMoveDefinitions
{
    private static readonly RnfmabjMoveDefinition Hidden = new(
        RnfmabjMoveIntentKind.Hidden,
        0,
        0,
        0,
        LibraryDamageType.None,
        "Idle",
        0f);

    private static readonly IReadOnlyDictionary<RnfmabjMove, RnfmabjMoveDefinition>
        Definitions = new Dictionary<RnfmabjMove, RnfmabjMoveDefinition>
        {
            [RnfmabjMove.ExecuteAttack] = Utility(
                RnfmabjMoveIntentKind.Buff,
                RnfmabjMoveEffect.ExecuteAttack),
            [RnfmabjMove.ExecuteGuard] = Utility(
                RnfmabjMoveIntentKind.Buff,
                RnfmabjMoveEffect.ExecuteGuard),
            [RnfmabjMove.ExecuteAlert] = Utility(
                RnfmabjMoveIntentKind.Debuff,
                RnfmabjMoveEffect.ExecuteAlert),
            [RnfmabjMove.ExecuteRepair] = Utility(
                RnfmabjMoveIntentKind.Heal,
                RnfmabjMoveEffect.RepairTarget,
                effectAmount: 40),
            [RnfmabjMove.ExecuteHold] = Utility(
                RnfmabjMoveIntentKind.Defend,
                RnfmabjMoveEffect.BlockAllGuests,
                effectAmount: 32),
            [RnfmabjMove.RepairAll] = Utility(
                RnfmabjMoveIntentKind.Heal,
                RnfmabjMoveEffect.RepairAllGuests,
                effectAmount: 80),
            [RnfmabjMove.TwistedBlade] = new(
                RnfmabjMoveIntentKind.GroupAttackDebuff,
                49,
                51,
                1,
                LibraryDamageType.Slash,
                "TwistedBlade",
                1.25f,
                Effect: RnfmabjMoveEffect.ApplyCorrosion,
                EffectAmount: 4,
                WindupSfx: Sfx("Yan_GreatSword_Start.ogg"),
                ImpactSfx: Sfx("Yan_GreatSword_Finish.ogg")),
            [RnfmabjMove.GiantPunch] = new(
                RnfmabjMoveIntentKind.AttackDefend,
                9,
                12,
                1,
                LibraryDamageType.Blunt,
                "Punch",
                0.72f,
                LeftHandBlockAmount: 14,
                RightHandBlockAmount: 18,
                ImpactSfx: Sfx("Yan_Stab.ogg"),
                BlockSfx: Sfx("Yan_Guard.ogg")),
            [RnfmabjMove.GiantPalm] = new(
                RnfmabjMoveIntentKind.Debuff,
                0,
                0,
                0,
                LibraryDamageType.Blunt,
                "Palm",
                0.78f,
                Effect: RnfmabjMoveEffect.ApplyParalysisAndWeak,
                EffectAmount: 2,
                EffectDurationTurns: 1,
                ImpactSfx: Sfx("Yan_Vert.ogg")),
            [RnfmabjMove.Flurry] = new(
                RnfmabjMoveIntentKind.MultiAttack,
                6,
                9,
                2,
                LibraryDamageType.Blunt,
                "MultiPunch",
                0.96f,
                ImpactSfx: Sfx("Yan_Lib_Hori.ogg"),
                AlternateImpactSfx: Sfx("Yan_Lib_Vert.ogg")),
            [RnfmabjMove.OminousBrand] = new(
                RnfmabjMoveIntentKind.AttackDebuff,
                13,
                18,
                1,
                LibraryDamageType.Slash,
                "Brand",
                0.92f,
                Effect: RnfmabjMoveEffect.ApplyCorrosion,
                EffectAmount: 2,
                WindupSfx: Sfx("Yan_Stigma_Start.ogg"),
                ImpactSfx: Sfx("Yan_Stigma_Atk.ogg")),
            [RnfmabjMove.LockTarget] = new(
                RnfmabjMoveIntentKind.CardDebuff,
                0,
                0,
                0,
                LibraryDamageType.Blunt,
                "Lock",
                1f,
                Effect: RnfmabjMoveEffect.TransformDrawPileCardToWound,
                EffectAmount: 1,
                WindupSfx: Sfx("Yan_Typing_Start.ogg"),
                ImpactSfx: Sfx("Yan_Typing_Atk.ogg")),
        };

    public static RnfmabjMoveDefinition Get(RnfmabjMove move) =>
        Definitions.GetValueOrDefault(move, Hidden);

    private static RnfmabjMoveDefinition Utility(
        RnfmabjMoveIntentKind kind,
        RnfmabjMoveEffect effect,
        int effectAmount = 0) => new(
        kind,
        0,
        0,
        0,
        LibraryDamageType.None,
        "Guard",
        0f,
        Effect: effect,
        EffectAmount: effectAmount,
        ImpactSfx: Sfx("Yan_Guard.ogg"));

    private static string Sfx(string file) =>
        "res://audio/special_guests/rnfmabj/combat/" + file;
}
