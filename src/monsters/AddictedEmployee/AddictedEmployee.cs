using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.SongMachine;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.AddictedEmployee;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.SongMachine;
using LibraryOfRuina.visuals.AddictedEmployee;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.AddictedEmployee;

internal enum AddictedEmployeeInitialMove
{
    Move1 = 1,
    Move2 = 2,
    Move3 = 3
}

public sealed class AddictedEmployee : LorMonsterModel
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "ADDICTED_EMPLOYEE.backgroundText.normal.0",
        "ADDICTED_EMPLOYEE.backgroundText.normal.1",
        "ADDICTED_EMPLOYEE.backgroundText.normal.2",
        "ADDICTED_EMPLOYEE.backgroundText.normal.3",
        "ADDICTED_EMPLOYEE.backgroundText.normal.4",
    ];

    private static readonly string[] MelodyCravingBackgroundTextLineKeys =
    [
        "ADDICTED_EMPLOYEE.backgroundText.melodyCraving.0",
        "ADDICTED_EMPLOYEE.backgroundText.melodyCraving.1",
        "ADDICTED_EMPLOYEE.backgroundText.melodyCraving.2",
        "ADDICTED_EMPLOYEE.backgroundText.melodyCraving.3",
        "ADDICTED_EMPLOYEE.backgroundText.melodyCraving.4",
    ];

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private static readonly Random MoonTextRng = new();

    private const string TremblingStrikeMoveId = "TREMBLING_STRIKE";
    private const string ShiveringMoveId = "SHIVERING";
    private const string FeelMelodyMoveId = "FEEL_THE_MELODY";

    private const int TremblingStrikeBlock = 8;
    private const int TremblingStrikeBind = 3;

    private const string AttackSfxPath = "res://audio/sfx/song_machine/song_machine_attack.ogg";
    private const string AttackSfxSlot = "SongMachineAttack";

    private static readonly string SongMachinePageRelicTitleLocKey =
        $"{ModelDb.GetId<SongMachinePageRelic>().Entry}.title";

    private AddictedEmployeeInitialMove _initialMove = AddictedEmployeeInitialMove.Move1;
    private bool _backgroundMoonTextLoopStarted;

    private int ShiveringDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int MelodyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 42, 29);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 31);

    public override int DefaultChaoResistance => 20;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        AddictedEmployeeCreatureVisuals.Profile.AssetPaths
        .Concat(base.AssetPaths.Skip(1))
        .Concat(
        [
            AttackSfxPath,
            SongMachineAttackOverlayController.OverlayTexturePath
        ])
        .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        _backgroundMoonTextLoopStarted = false;
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<LibraryOfRuinaAddictedEmployeeMelodyCravingPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: false);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead && !_backgroundMoonTextLoopStarted)
        {
            _backgroundMoonTextLoopStarted = true;
            StartBackgroundMoonTextLoop(NormalBackgroundTextLineKeys);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddSongMachinePageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    internal void ConfigureInitialMove(AddictedEmployeeInitialMove initialMove)
    {
        AssertMutable();
        _initialMove = initialMove;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move1 = new MoveState(
            TremblingStrikeMoveId,
            TremblingStrikeMove,
            new DefendIntent(),
            new BadgedDebuffIntent(IntentBadge.FromPower<LibraryBindingPower>(3, "1", "3")));

        var move2 = new MoveState(
            ShiveringMoveId,
            ShiveringMove,
            new MultiAttackIntent(ShiveringDamage, 2));

        var move3 = new MoveState(
            FeelMelodyMoveId,
            FeelTheMelodyMove,
            new MultiAttackIntent(MelodyDamage, 3),
            new BadgedDebuffIntent(IntentBadge.Bleed(1), amount: 1));

        move1.FollowUpState = move2;
        move2.FollowUpState = move3;
        move3.FollowUpState = move1;

        List<MonsterState> states = [move1, move2, move3];

        MonsterState initialState = _initialMove switch
        {
            AddictedEmployeeInitialMove.Move2 => move2,
            AddictedEmployeeInitialMove.Move3 => move3,
            _ => move1
        };

        return new MonsterMoveStateMachine(states, initialState);
    }

    private async Task TremblingStrikeMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoop(NormalBackgroundTextLineKeys);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.075f);
        await CreatureCmd.GainBlock(Creature, TremblingStrikeBlock, ValueProp.Move, null);

        if (targets.Count == 0)
        {
            return;
        }

        await LibraryPowerCmd.Apply<LibraryBindingPower>(new ThrowingPlayerChoiceContext(),
            targets, TremblingStrikeBind, 1, false, Creature, null);
    }

    private async Task ShiveringMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoop(NormalBackgroundTextLineKeys);
        SongMachineAttackOverlayController.PlayOverlay();

        for (int hit = 0; hit < 2; hit++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.PlayExclusive(AttackSfxSlot, AttackSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, ShiveringDamage);
            LocalOggOneShotPlayer.StopExclusive(AttackSfxSlot);
            await Cmd.CustomScaledWait(0.09f, 0.18f);
        }
    }

    private async Task FeelTheMelodyMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoop(NormalBackgroundTextLineKeys);
        SongMachineAttackOverlayController.PlayOverlay();

        for (int hit = 0; hit < 3; hit++)
        {
            if (Creature.IsDead) return;
            Dictionary<Creature, int> hpBefore = SnapshotPlayerHp(targets);

            LocalOggOneShotPlayer.PlayExclusive(AttackSfxSlot, AttackSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, MelodyDamage);
            LocalOggOneShotPlayer.StopExclusive(AttackSfxSlot);
            await Cmd.CustomScaledWait(0.09f, 0.18f);

            foreach (Creature playerCreature in targets)
            {
                if (!playerCreature.IsAlive)
                {
                    continue;
                }

                int before = hpBefore.TryGetValue(playerCreature, out int v) ? v : playerCreature.CurrentHp;
                if (playerCreature.CurrentHp < before)
                {
                    await PowerCmdCompat.Apply<LibraryBleedingPower>(
                        playerCreature,
                        1m,
                        Creature,
                        null,
                        silent: false);
                }
            }
        }
    }

    private static bool IsAddictedEmployeeEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is AddictedEmployee);
    }

    private void AddSongMachinePageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsAddictedEmployeeEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<SongMachinePageRelic>(room, SongMachinePageRelicTitleLocKey);
    }

    private static Dictionary<Creature, int> SnapshotPlayerHp(IReadOnlyList<Creature> targets)
    {
        var result = new Dictionary<Creature, int>(targets.Count);
        foreach (Creature creature in targets)
        {
            result[creature] = creature.CurrentHp;
        }
        return result;
    }

    internal void TriggerMelodyCravingMoonText()
    {
        if (Creature.IsDead || MelodyCravingBackgroundTextLineKeys.Length == 0)
        {
            return;
        }

        int count = Math.Min(3, MelodyCravingBackgroundTextLineKeys.Length);
        List<int> indices = new List<int>(MelodyCravingBackgroundTextLineKeys.Length);
        for (int i = 0; i < MelodyCravingBackgroundTextLineKeys.Length; i++)
        {
            indices.Add(i);
        }

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int swapIndex;
            lock (MoonTextRng)
            {
                swapIndex = MoonTextRng.Next(0, i + 1);
            }

            (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
        }

        var entries = new List<MoonTextSequenceEntry>(count);
        for (int i = 0; i < count; i++)
        {
            string key = MelodyCravingBackgroundTextLineKeys[indices[i]];
            entries.Add(new MoonTextSequenceEntry(L10NMonsterLookup(key), absoluteTriggerTimeSeconds: i * 0.6f));
        }

        MoonTextService.StartSequence(entries);
    }

    private static void StartBackgroundMoonTextLoop(IReadOnlyList<string> lineKeys)
    {
        if (lineKeys.Count == 0)
        {
            return;
        }

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }
}

