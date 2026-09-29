using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

public sealed class QueenBeeElite : EncounterModel, ISporeWorkerSpawner
{
    public const string QueenSlot = "queen_bee";
    public const string WorkerSlotOne = "worker_bee_1";
    public const string WorkerSlotTwo = "worker_bee_2";
    public const string EncounterScenePath =
        "res://scenes/encounters/queen_bee_elite.tscn";
    public const string BackgroundLayerScenePath =
        "res://scenes/backgrounds/queen_bee_elite/layers/queen_bee_elite_bg_00_a.tscn";

    private const int MaxWorkerCount = 2;

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
        // Attack order follows Slot order (CombatState.SortEnemiesBySlotName):
        // scene markers are worker_bee_1 (x=930) -> worker_bee_2 (x=1310) -> queen (x=1810).
        [WorkerSlotOne, WorkerSlotTwo, QueenSlot];

    public bool CanSpawnSporeWorkers => true;

    public override float GetCameraScaling() => 0.88f;

    public override Vector2 GetCameraOffset() => new(-80f, 60f);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<QueenBee>(),
        ModelDb.Monster<QueenBeeWorker>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<QueenBee>().AssetPaths
            .Concat(ModelDb.Monster<QueenBeeWorker>().AssetPaths)
            .Append(EncounterScenePath)
            .Append(BackgroundLayerScenePath)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var workerOne =
            (QueenBeeWorker)ModelDb.Monster<QueenBeeWorker>().ToMutable();
        workerOne.ConfigurePattern(0);
        var workerTwo =
            (QueenBeeWorker)ModelDb.Monster<QueenBeeWorker>().ToMutable();
        workerTwo.ConfigurePattern(1);

        return
        [
            (ModelDb.Monster<QueenBee>().ToMutable(), QueenSlot),
            (workerOne, WorkerSlotOne),
            (workerTwo, WorkerSlotTwo)
        ];
    }

    public async Task TrySpawnSporeWorker(CombatStateLike combatState)
    {
        await TrySpawnWorkerIfNeeded(combatState);
    }

    public async Task TrySpawnWorkerIfNeeded(CombatStateLike combatState)
    {
        int workerCount = combatState.Enemies.Count(
            static enemy => enemy.IsAlive
                && enemy.Monster is QueenBeeWorker);
        if (workerCount >= MaxWorkerCount)
        {
            return;
        }

        string[] workerSlots = [WorkerSlotOne, WorkerSlotTwo];
        string? slot = workerSlots.FirstOrDefault(
            slotName => combatState.Enemies.All(
                enemy => enemy.SlotName != slotName || !enemy.IsAlive));
        if (string.IsNullOrWhiteSpace(slot))
        {
            slot = GetNextSlot(combatState);
        }

        if (string.IsNullOrWhiteSpace(slot))
        {
            return;
        }

        var worker = (QueenBeeWorker)ModelDb.Monster<QueenBeeWorker>().ToMutable();
        worker.ConfigurePattern(
            slot == WorkerSlotOne ? 0 : 1);
        Creature spawned = await CreatureCmd.Add(
            worker,
            combatState,
            CombatSide.Enemy,
            slot);
        spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        combatState.SortEnemiesBySlotName();
        LocalOggOneShotPlayer.Play(QueenBee.SpawnSfxPath, -2f);
    }

    internal static void AddPageRewards(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not QueenBeeElite)
        {
            return;
        }

        string titleLocKey = $"{ModelDb.GetId<QueenBeePageRelic>().Entry}.title";
        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<QueenBeePageRelic>(
                    room,
                    player,
                    titleLocKey))
            {
                continue;
            }

            room.AddExtraReward(
                player,
                new RelicReward(ModelDb.Relic<QueenBeePageRelic>().ToMutable(), player));
        }
    }
}
