using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Xiao;

public sealed class XiaoStageOne : XiaoSpecialGuestMonsterBase, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    public bool IsFakeDead { get; private set; }

    public bool ForceTrueDeath { get; private set; }

    private bool _completingReception;
    private bool _fakeDeathQueued;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            397,
            392);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            400,
            394);

    public override int DefaultChaoResistance => 220;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure,
        };

    protected override int InitialIntentCapacity => 2;

    protected override int PatternLength => 4;

    protected override bool HasStarfirePassive => true;

    protected override bool CanPerformMoves => !IsFakeDead;

    public override IEnumerable<string> AssetPaths =>
        XiaoSpecialGuestAssets.StageOneXiao;

    public override bool ShouldStopCombatFromEnding() =>
        IsFakeDead && IsMirisAlive;

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Creature || !IsFakeDead;

    public override bool ShouldAllowTargeting(Creature target) =>
        target != Creature || !IsFakeDead;

    protected override IReadOnlyList<XiaoGuestMove> GetPattern(
        int patternIndex,
        bool firstTurn) => patternIndex switch
    {
        0 =>
        [
            firstTurn ? XiaoGuestMove.BlazingDance : XiaoGuestMove.HotBlood,
            XiaoGuestMove.FieryDragonSlash,
            XiaoGuestMove.FervidEmotion,
        ],
        1 =>
        [
            XiaoGuestMove.FieryDragonSlash,
            XiaoGuestMove.ThroatPierce,
            IsMirisAlive ? XiaoGuestMove.DoubleFlank : XiaoGuestMove.BlazingDance,
        ],
        2 =>
        [
            XiaoGuestMove.LongDrive,
            XiaoGuestMove.BlazingDance,
            XiaoGuestMove.HotBlood,
        ],
        _ => [XiaoGuestMove.GreatFlame, XiaoGuestMove.HotBlood],
    };

    private bool IsMirisAlive =>
        Creature.CombatState?.Enemies.Any(
            static enemy => enemy.IsAlive && enemy.Monster is Miris) == true;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (target != Creature || amount <= 0m || ForceTrueDeath)
        {
            return amount;
        }

        decimal threshold = ScaleThreshold(100);
        return ClampToFakeDeathThreshold(target, amount, threshold);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        await TryEnterFakeDeath(creature, delta);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta,
        LibraryDamageType type)
    {
        await base.AfterCurrentHpChanged(creature, delta, type);
        await TryEnterFakeDeath(creature, delta);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        await base.AfterDamageReceived(
            choiceContext,
            target,
            result,
            props,
            dealer,
            cardSource,
            type);
        if (target != Creature || IsFakeDead || ForceTrueDeath)
        {
            return;
        }

        if (_fakeDeathQueued || Creature.CurrentHp <= ScaleThreshold(100))
        {
            await EnterFakeDeath();
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await base.AfterDeath(
            choiceContext,
            creature,
            wasRemovalPrevented,
            deathAnimLength);
        if (!wasRemovalPrevented && creature.Monster is Miris)
        {
            await TryCompleteReception();
        }
    }

    private async Task TryEnterFakeDeath(Creature creature, decimal delta)
    {
        if (creature != Creature
            || delta >= 0m
            || IsFakeDead
            || ForceTrueDeath
            || creature.CurrentHp > ScaleThreshold(100))
        {
            return;
        }

        await EnterFakeDeath();
    }

    private async Task EnterFakeDeath()
    {
        if (IsFakeDead || ForceTrueDeath)
        {
            return;
        }

        IsFakeDead = true;
        _fakeDeathQueued = false;

        decimal threshold = ScaleThreshold(100);
        if (Creature.CurrentHp != threshold)
        {
            await CreatureCmd.SetCurrentHp(Creature, threshold);
        }

        HideFakeDeathUi();
        EnterHiddenIntent();
        await TryCompleteReception();
    }

    private decimal ClampToFakeDeathThreshold(
        Creature target,
        decimal amount,
        decimal threshold)
    {
        decimal maxLoss = Math.Max(0m, target.CurrentHp - threshold);
        if (maxLoss <= 0m || amount >= maxLoss)
        {
            QueueFakeDeath();
        }

        return Math.Min(amount, maxLoss);
    }

    private void QueueFakeDeath()
    {
        if (!IsFakeDead && !ForceTrueDeath)
        {
            _fakeDeathQueued = true;
        }
    }

    private void HideFakeDeathUi()
    {
        NCombatRoom.Instance?.SetCreatureIsInteractable(Creature, false);
    }

    private async Task TryCompleteReception()
    {
        if (!IsFakeDead
            || IsMirisAlive
            || ForceTrueDeath
            || _completingReception)
        {
            return;
        }

        _completingReception = true;
        ForceTrueDeath = true;
        try
        {
            await CreatureCmd.Kill(Creature, force: true);
        }
        finally
        {
            _completingReception = false;
        }
    }
}

public sealed class Miris : XiaoSpecialGuestMonsterBase
{
    public override int MinInitialHp => 196;

    public override int MaxInitialHp => 196;

    public override int DefaultChaoResistance => 143;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure,
        };

    protected override int InitialIntentCapacity => 2;

    protected override int PatternLength => 4;

    public override IEnumerable<string> AssetPaths =>
        XiaoSpecialGuestAssets.Miris;

    protected override IReadOnlyList<XiaoGuestMove> GetPattern(
        int patternIndex,
        bool firstTurn) => patternIndex switch
    {
        0 =>
        [
            firstTurn ? XiaoGuestMove.BixueDanxin : XiaoGuestMove.HotBlood,
            XiaoGuestMove.DoubleFlank,
            XiaoGuestMove.FervidEmotion,
        ],
        1 =>
        [
            XiaoGuestMove.FlameDragonFist,
            XiaoGuestMove.BixueDanxin,
            XiaoGuestMove.HotBlood,
        ],
        2 =>
        [
            XiaoGuestMove.BixueDanxin,
            XiaoGuestMove.FervidEmotion,
            XiaoGuestMove.DoubleFlank,
        ],
        _ =>
        [
            XiaoGuestMove.BixueDanxin,
            XiaoGuestMove.FervidEmotion,
            XiaoGuestMove.FlameDragonFist,
        ],
    };
}

public sealed class XiaoEgo : XiaoSpecialGuestMonsterBase
{
    public bool HadAnyAttackResultThisEnemyTurn { get; private set; }

    public bool AllAttackResultsFullyBlocked { get; private set; } = true;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            698,
            690);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            700,
            694);

    public override int DefaultChaoResistance => 350;

    public override decimal ChaoRecoveryRatio => 0.5m;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Endure,
        };

    protected override int InitialIntentCapacity => 4;

    protected override int PatternLength => 6;

    protected override bool HasStarfirePassive => true;

    protected override bool IsSecondStageXiao => true;

    public override IEnumerable<string> AssetPaths =>
        XiaoSpecialGuestAssets.StageTwoXiao;

    protected override IReadOnlyList<XiaoGuestMove> GetPattern(
        int patternIndex,
        bool firstTurn) => patternIndex switch
    {
        0 => firstTurn
            ?
            [
                XiaoGuestMove.FieryDragonSlash,
                XiaoGuestMove.FervidEmotion,
                XiaoGuestMove.JiaotuSuppressEvil,
            ]
            :
            [
                XiaoGuestMove.SuanniSoaringCloud,
                XiaoGuestMove.ChiwenSwallowRidge,
                XiaoGuestMove.FervidEmotion,
                XiaoGuestMove.JiaotuSuppressEvil,
            ],
        1 =>
        [
            XiaoGuestMove.FieryDragonSlash,
            XiaoGuestMove.ChiwenSwallowRidge,
            XiaoGuestMove.HotBlood,
        ],
        2 =>
        [
            XiaoGuestMove.SuanniSoaringCloud,
            XiaoGuestMove.YaziVengeance,
            XiaoGuestMove.JiaotuSuppressEvil,
        ],
        3 =>
        [
            XiaoGuestMove.FervidEmotion,
            XiaoGuestMove.HotBlood,
            XiaoGuestMove.SuanniSoaringCloud,
        ],
        4 =>
        [
            XiaoGuestMove.ChiwenSwallowRidge,
            XiaoGuestMove.JiaotuSuppressEvil,
            XiaoGuestMove.BianDispute,
        ],
        _ =>
        [
            XiaoGuestMove.TaotieFeast,
            XiaoGuestMove.SuanniSoaringCloud,
            XiaoGuestMove.JiaotuSuppressEvil,
        ],
    };

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Enemy)
        {
            HadAnyAttackResultThisEnemyTurn = false;
            AllAttackResultsFullyBlocked = true;
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
        if (side == CombatSide.Player && Creature.IsAlive)
        {
            await ApplyBurnToAllPlayers(
                XiaoDragonBornPassivePower.BurnStacksPerRound);
        }
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndLate(choiceContext, side, participants);
        if (side != CombatSide.Enemy
            || !Creature.IsAlive
            || !HadAnyAttackResultThisEnemyTurn
            || !AllAttackResultsFullyBlocked
            || Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        bool wasChaoed = libraryCreature.IsChaoed;
        decimal targetChao = Math.Max(
            0m,
            libraryCreature.CurrentChaoValue
            - ScaleThreshold(XiaoReverseScalePassivePower.ChaoLossOnFullBlock));
        await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, targetChao);

        // Reverse Scale fires at the very end of the enemy turn, after this
        // turn's stun-recovery pass has already run. The generic stun counter
        // counts the enemy turn it is applied in, which would keep Xiao's
        // resistances Fatal and her confusion bar empty for one extra turn.
        // Normalize a freshly applied stun so she recovers at the end of her
        // next (skipped) enemy turn instead.
        if (!wasChaoed
            && libraryCreature.IsChaoed
            && libraryCreature.StunPlayerTurnsRemaining > 1)
        {
            libraryCreature.DecrementStunTurns();
        }

        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Creature,
            XiaoReverseScalePassivePower.FullBlockBuffStacks,
            XiaoReverseScalePassivePower.FullBlockBuffDurationTurns,
            Creature,
            null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            XiaoReverseScalePassivePower.FullBlockBuffStacks,
            XiaoReverseScalePassivePower.FullBlockBuffDurationTurns,
            Creature,
            null);
    }

    internal void RecordAttackResults(IReadOnlyList<DamageResult> results)
    {
        foreach (DamageResult result in results)
        {
            HadAnyAttackResultThisEnemyTurn = true;
            if (!result.WasFullyBlocked)
            {
                AllAttackResultsFullyBlocked = false;
            }
        }
    }
}

internal static class XiaoSpecialGuestAssets
{
    private const string Root = "res://images/special_guests/xiao/monsters/";

    public static readonly string[] PassiveIcons =
    [
        "res://images/powers/library_passive_orange.png",
        "res://images/powers/library_passive_purple.png",
        "res://images/powers/library_passive_blue.png",
        "res://images/powers/library_passive_green.png",
        "res://images/powers/nullify_power.png",
    ];

    private static readonly string[] CombatAudio =
        new[]
    {
        "Xiao_Hori.ogg", "Xiao_Vert.ogg", "Xiao_Stab.ogg",
        "Xiao_Stong_Hori.ogg", "Xiao_Stong_Upper.ogg",
        "Xiao_LandHit_Charge.ogg", "Xiao_LandHit_Hit.ogg",
        "Xiao_DrangonStab.ogg", "Xiao_DragonUp_Start.ogg", "Xiao_DragonUp_End.ogg",
        "Cry_Main_Guard_Win.ogg",
        "Riu_Hori.ogg", "Riu_Vert.ogg", "Riu_Stab.ogg",
        "Riu_Strong.ogg", "Riu_Guard_1.ogg", "Riu_Shao_UpperAtk.ogg",
        "Philip_Hori.ogg", "Philip_Vert.ogg", "Philip_Stab.ogg",
        "Philip_Strong.ogg", "Riu_Guard.ogg",
    }.Select(file => XiaoSpecialGuestIds.CombatAudioRoot + file).ToArray();

    public static readonly string[] StageOneXiao =
        Enumerate("xiao_stage_one", "XiaoDistort", 11)
            .Prepend(XiaoStageOneCreatureVisuals.ScenePath)
            .Concat(CombatAudio)
            .Concat(PassiveIcons)
            .ToArray();

    public static readonly string[] Miris =
        Enumerate("miris", "Miris", 10)
            .Concat(CombatAudio)
            .ToArray();

    public static readonly string[] StageTwoXiao =
        Enumerate("xiao_ego", "XiaoEgo", 15)
            .Prepend(XiaoEgoCreatureVisuals.ScenePath)
            .Concat(CombatAudio)
            .Concat(PassiveIcons)
            .Append(XiaoSpecialGuestIds.StageTwoBackground)
            .Append(XiaoSpecialGuestIds.IronLotusBgm)
            .ToArray();

    public static readonly string[] StageOneReceptionBgms =
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks
            .Concat(GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> Enumerate(
        string folder,
        string prefix,
        int expectedCount)
    {
        _ = expectedCount;
        string[] suffixes = folder switch
        {
            "miris" =>
            ["Default", "Damaged", "Hit", "Guard", "Evade", "Penetrate", "Slash", "Strike", "S1", "S2"],
            "xiao_stage_one" =>
            ["Default", "Damaged", "Hit", "Guard", "Evade", "Move", "Penetrate", "Slash", "Strike", "S1", "S2"],
            _ =>
            ["Default", "Damaged", "Hit", "Guard", "Evade", "Move", "Penetrate", "Slash", "Strike", "S1", "S2", "S3", "S4", "S5", "Special"],
        };
        return suffixes.Select(
            suffix => $"{Root}{folder}/{prefix}_{suffix}.png");
    }
}
