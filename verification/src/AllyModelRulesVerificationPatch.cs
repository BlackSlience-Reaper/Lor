using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// LorMonsterModel 上的盟友规则：盟友保留格挡（格挡转移搭档、本回合已在盟友回合行动）与玩家方敌方范围卡的能力作废。
/// 直接调用原版 <see cref="Hook.ShouldClearBlock"/> 与 <see cref="Hook.ModifyPowerAmountGiven"/>，检查结果与 preventer/修正者。
/// 布置沿用 ally-types：LittleRed 精英战，用测试 provider 把狼临时登记为友方盟友。
/// </summary>
internal static class AllyModelRulesVerificationPatch
{
    private const string VerifyArg = "lor-verify-ally-model-rules";
    private const string LogPrefix = "[LibraryOfRuina.AllyModelRules.Verify] ";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "ALLY_MODEL_RULES_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "ALLY_MODEL_RULES_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        Player[] players = await StartFakeMultiplayerFight();
        TestAllyProvider? friendlyProvider = null;
        try
        {
            CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is null.");
            Creature player = players[0].Creature;
            Creature littleRed = LittleRedMercenaryEncounterHelper.FindLittleRed(combatState)
                ?? throw new InvalidOperationException("Little Red is missing.");
            Creature wolf = LittleRedMercenaryEncounterHelper.FindWolf(combatState)
                ?? throw new InvalidOperationException("Wolf is missing.");
            var littleRedModel = (LittleRedRidingHoodedMercenary)littleRed.Monster!;
            PowerModel vulnerable = ModelDb.Power<VulnerablePower>();
            CardModel enemyCard = combatState.CreateCard<StrikeIronclad>(players[0]);
            CardModel selfCard = combatState.CreateCard<DefendIronclad>(players[0]);

            CheckBlockClear(combatState, wolf, shouldClear: true, null, "unregistered wolf");
            CheckPowerGiven(combatState, vulnerable, player, wolf, enemyCard, 2m, null, "enemy card on unregistered wolf");

            friendlyProvider = new TestAllyProvider(combatState, wolf, AllyType.Friendly);
            AllyTurnRegistry.RegisterProvider(friendlyProvider);

            // 小红帽不暴怒时是友方盟友、格挡转移搭档：保留格挡，preventer 是它自己。测试 provider 不允许狼转移格挡。
            CheckBlockClear(combatState, littleRed, shouldClear: false, littleRed.Monster, "friendly Little Red");
            CheckBlockClear(combatState, wolf, shouldClear: true, null, "friendly wolf before the ally turn");

            // 玩家用敌方范围卡给友方盟友的能力作废，修正者是盟友怪物；非敌方范围的卡、没有卡、非玩家方给予者不变。
            CheckPowerGiven(combatState, vulnerable, player, wolf, enemyCard, 0m, wolf.Monster, "enemy card on friendly wolf");
            CheckPowerGiven(combatState, vulnerable, player, wolf, selfCard, 2m, null, "self card on friendly wolf");
            CheckPowerGiven(combatState, vulnerable, player, wolf, null, 2m, null, "no card on friendly wolf");
            CheckPowerGiven(combatState, vulnerable, player, littleRed, enemyCard, 0m, littleRed.Monster, "enemy card on friendly Little Red");

            // 暴怒的小红帽是敌对：照常清格挡，也照常吃能力；它作为给予者不算玩家方。
            await littleRedModel.EnterRage();
            CheckBlockClear(combatState, littleRed, shouldClear: true, null, "raging Little Red");
            CheckPowerGiven(combatState, vulnerable, player, littleRed, enemyCard, 2m, null, "enemy card on raging Little Red");
            CheckPowerGiven(combatState, vulnerable, littleRed, wolf, enemyCard, 2m, null, "raging Little Red giving to friendly wolf");
            LittleRedRagePower ragePower = littleRed.GetPower<LittleRedRagePower>()
                ?? throw new InvalidOperationException("Little Red rage power is missing.");
            await PowerCmd.Remove(ragePower);
            CheckBlockClear(combatState, littleRed, shouldClear: false, littleRed.Monster, "Little Red after rage");

            // 本回合在盟友回合行动过的友方盟友，敌方回合开始时保留格挡。
            await AllyTurnRegistry.ExecuteAllyTurn(CombatManager.Instance);
            Check(AllyTurnRegistry.HasActedThisRound(wolf), "friendly wolf did not act in the ally turn");
            CheckBlockClear(combatState, wolf, shouldClear: false, wolf.Monster, "friendly wolf after the ally turn");

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(Failures.Count + " check(s) failed: " + string.Join("; ", Failures));
            }
        }
        finally
        {
            if (friendlyProvider != null)
            {
                AllyTurnRegistry.UnRegisterProvider(friendlyProvider);
            }

            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "ally model rules verifier cleanup");
        }
    }

    // 每项检查都记日志，失败项收集到最后一起报告，便于与改动前的构建逐项对照。
    private static readonly List<string> Failures = [];

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            Failures.Add(message);
            Log.Error(LogPrefix + "CHECK FAILED: " + message);
        }
    }

    private static void CheckBlockClear(
        CombatState combatState,
        Creature creature,
        bool shouldClear,
        AbstractModel? expectedPreventer,
        string label)
    {
        bool clear = Hook.ShouldClearBlock(combatState, creature, out AbstractModel? preventer);
        Log.Info(LogPrefix + label + ": ShouldClearBlock=" + clear + " preventer=" + (preventer?.GetType().Name ?? "null"));
        Check(clear == shouldClear, $"{label}: ShouldClearBlock returned {clear}, expected {shouldClear}");
        Check(ReferenceEquals(preventer, expectedPreventer),
            $"{label}: preventer was {preventer?.GetType().Name ?? "null"}, expected {expectedPreventer?.GetType().Name ?? "null"}");
    }

    private static void CheckPowerGiven(
        CombatState combatState,
        PowerModel power,
        Creature giver,
        Creature target,
        CardModel? cardSource,
        decimal expected,
        AbstractModel? expectedModifier,
        string label)
    {
        decimal amount = Hook.ModifyPowerAmountGiven(
            combatState, power, giver, 2m, target, cardSource, out var modifiers);
        bool recorded = expectedModifier != null && modifiers.Contains(expectedModifier);
        Log.Info(LogPrefix + label + ": amount=" + amount + (expectedModifier != null ? " modifierRecorded=" + recorded : ""));
        Check(amount == expected, $"{label}: amount was {amount}, expected {expected}");
        if (expectedModifier != null)
        {
            Check(recorded, $"{label}: {expectedModifier.GetType().Name} was not recorded as a modifier");
        }
    }

    private static async Task<Player[]> StartFakeMultiplayerFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        Player[] players =
        [
            Player.CreateForNewRun(
                ModelDb.Character<Ironclad>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                1uL),
            Player.CreateForNewRun(
                ModelDb.Character<Silent>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                2uL)
        ];
        RunState runState = RunState.CreateForNewRun(
            players,
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel: 0,
            "ALLYMODELRULES");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
        ArmActLikeIt2OneShotVanillaEntry();

        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
            ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task.");
        await startRunTask;

        foreach (Player player in players)
        {
            foreach (RelicModel relic in player.Relics.ToArray())
            {
                await RelicCmd.Remove(relic);
            }
        }

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Elite,
            MapPointType.Unassigned,
            ModelDb.Encounter<LittleRedMercenaryElite>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "ally model rules verifier combat start");
        return players;
    }

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ActLikeIt2.Runtime.ActSelectionGate", throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static async Task WaitUntil(Func<bool> predicate, string description, int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestAllyProvider(ICombatState combatState, Creature ally, AllyType allyType)
        : IAllyTurnProvider
    {
        public string AllyId => "ALLY_MODEL_RULES_VERIFY";

        public AllyType AllyType => allyType;

        public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

        public bool IsActiveEncounter(ICombatState state) => ReferenceEquals(combatState, state);

        public Creature? FindAlly(ICombatState state) => IsActiveEncounter(state) ? ally : null;

        public bool CanTransferBlock => false;

        public void OnCombatReset(Creature? creature)
        {
        }
    }
}
