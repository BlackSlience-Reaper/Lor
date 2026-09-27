using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.combat;
using LibraryOfRuina.combat.WrathServant;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LittleRedMercenary;
using LibraryOfRuina.monsters.LittleRedMercenary;
using LibraryOfRuina.powers.LittleRedMercenary;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class AllyTurnProviderVerificationPatch
{
    private const string VerifyArg = "lor-verify-ally-types";
    private const string LogPrefix = "[LibraryOfRuina.AllyTypes.Verify] ";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static bool _started;

    private static void Postfix()
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
            Log.Info(LogPrefix + "ALLY_TYPES_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "ALLY_TYPES_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        Require(new WrathServantAllyTurnProvider().AllyType == AllyType.Friendly,
            "Wrath Servant was not classified as Friendly for player target exclusion.");

        Player[] players = await StartFakeMultiplayerFight();
        TestAllyProvider? friendlyProvider = null;
        try
        {
            CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is null.");
            Creature player = players[0].Creature;
            Creature littleRedCreature = LittleRedMercenaryEncounterHelper
                .FindLittleRed(combatState)
                ?? throw new InvalidOperationException("Little Red is missing.");
            Creature wolf = LittleRedMercenaryEncounterHelper.FindWolf(combatState)
                ?? throw new InvalidOperationException("Wolf is missing.");
            var littleRed = (LittleRedRidingHoodedMercenary)littleRedCreature.Monster!;

            friendlyProvider = new TestAllyProvider(
                combatState,
                wolf,
                AllyType.Friendly);
            AllyTurnRegistry.RegisterProvider(friendlyProvider);

            VerifyFriendlyTargeting(
                combatState,
                players[0],
                wolf,
                littleRedCreature);
            await VerifyFriendlyDamageFinalGuards(
                players[0],
                wolf,
                littleRedCreature);

            Require(AllyTurnRegistry.GetAllyType(littleRedCreature) == AllyType.Neutral,
                "Little Red did not begin as Neutral.");
            Require(AllyTurnRegistry.IsAllyCreature(littleRedCreature),
                "Neutral Little Red lost ally-turn identity.");
            Require(AllyTurnRegistry.ShouldUseAllyTurn(littleRedCreature),
                "Neutral Little Red lost the ally-turn route.");
            Require(AllyTurnRegistry.CanTransferBlockWith(littleRedCreature),
                "Neutral Little Red lost block-transfer eligibility.");
            Require(BlockTransferEncounterTargetHelper.FindPartner(combatState)
                    == littleRedCreature,
                "Neutral Little Red was not the block-transfer partner.");

            await littleRed.EnterRage();
            Require(AllyTurnRegistry.GetAllyType(littleRedCreature) == AllyType.Hostile,
                "Raging Little Red was not Hostile.");
            Require(AllyTurnRegistry.IsHostileAlly(littleRedCreature),
                "Raging Little Red failed the Hostile classifier.");
            Require(!AllyTurnRegistry.IsAllyCreature(littleRedCreature),
                "Raging Little Red retained ally intent/turn identity.");
            Require(!AllyTurnRegistry.ShouldUseAllyTurn(littleRedCreature),
                "Raging Little Red retained the pre-enemy ally turn.");
            Require(!AllyTurnRegistry.CanTransferBlockWith(littleRedCreature),
                "Raging Little Red retained block-transfer eligibility.");
            Require(BlockTransferEncounterTargetHelper.FindPartner(combatState) == null,
                "Raging Little Red remained a block-transfer partner.");
            Require(combatState.HittableEnemies.Contains(littleRedCreature),
                "Raging Little Red was missing from player enemy candidates.");
            Require(combatState.GetOpponentsOf(littleRedCreature)
                    .Where(static target => target.IsAlive)
                    .All(static target => target.IsPlayer),
                "Raging Little Red did not use the ordinary enemy opponent set.");

            StrikeIronclad rageTargetCard = combatState.CreateCard<StrikeIronclad>(
                players[0]);
            Require(rageTargetCard.IsValidTarget(littleRedCreature),
                "Raging Little Red could not be selected by an enemy-target card.");

            LittleRedRagePower ragePower = littleRedCreature
                .GetPower<LittleRedRagePower>()
                ?? throw new InvalidOperationException("Little Red rage power is missing.");
            await PowerCmd.Remove(ragePower);
            Require(AllyTurnRegistry.GetAllyType(littleRedCreature) == AllyType.Neutral,
                "Little Red did not return to Neutral after rage.");
            Require(AllyTurnRegistry.ShouldUseAllyTurn(littleRedCreature),
                "Little Red did not recover the ally-turn route after rage.");
            Require(BlockTransferEncounterTargetHelper.FindPartner(combatState)
                    == littleRedCreature,
                "Little Red did not recover block-transfer eligibility after rage.");

            _ = player;
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
                "ally-type verifier cleanup");
        }
    }

    private static void VerifyFriendlyTargeting(
        CombatState combatState,
        Player owner,
        Creature friendly,
        Creature hostileCandidate)
    {
        Require(!friendly.IsPlayer && friendly.Monster != null,
            "Friendly test creature lost its monster runtime identity.");
        Require(AllyTurnRegistry.IsFriendlyAlly(friendly),
            "Friendly provider classification failed.");
        Require(AllyTurnRegistry.IsPlayerAlignedForTargeting(friendly),
            "Friendly creature was not player-aligned for targeting.");
        Require(!combatState.HittableEnemies.Contains(friendly)
                && combatState.HittableEnemies.Contains(hostileCandidate),
            "HittableEnemies did not exclude only Friendly creatures.");
        Require(!combatState.GetOpponentsOf(owner.Creature).Contains(friendly)
                && combatState.GetOpponentsOf(owner.Creature).Contains(hostileCandidate),
            "Player opponents retained a Friendly creature.");
        Require(combatState.GetOpponentsOf(friendly).Contains(hostileCandidate)
                && !combatState.GetOpponentsOf(friendly).Contains(friendly),
            "Friendly creature did not use the player enemy set.");

        StrikeIronclad card = combatState.CreateCard<StrikeIronclad>(owner);
        Require(!card.IsValidTarget(friendly)
                && card.IsValidTarget(hostileCandidate),
            "AnyEnemy card target validation retained a Friendly creature.");

        var potion = (FirePotion)ModelDb.Potion<FirePotion>().ToMutable();
        potion.Owner = owner;
        Require(!potion.IsValidTarget(friendly)
                && potion.IsValidTarget(hostileCandidate),
            "AnyEnemy potion target validation retained a Friendly creature.");

        AttackCommand attack = DamageCmd.Attack(1m)
            .FromCard(card, null)
            .TargetingAllOpponents(combatState);
        RequireOnlyHostileTargets(
            GetPossibleTargets(attack),
            friendly,
            hostileCandidate,
            "AttackCommand");

        LibraryAttackCommand libraryAttack = new LibraryAttackCommand(1m)
            .FromCard(card)
            .TargetingAllOpponents(combatState);
        RequireOnlyHostileTargets(
            GetPossibleTargets(libraryAttack),
            friendly,
            hostileCandidate,
            "LibraryAttackCommand");

        VerifyTargetManager(friendly, hostileCandidate);
    }

    private static async Task VerifyFriendlyDamageFinalGuards(
        Player owner,
        Creature friendly,
        Creature hostileCandidate)
    {
        var choiceContext = new BlockingPlayerChoiceContext();
        IEnumerable<DamageResult> vanillaResults = await CreatureCmdCompat.Damage(
            choiceContext,
            [friendly, hostileCandidate],
            1m,
            ValueProp.Unpowered,
            owner.Creature);
        Require(vanillaResults.All(result => result.Receiver != friendly)
                && vanillaResults.Any(result => result.Receiver == hostileCandidate),
            "CreatureCmd.Damage retained a Friendly target.");

        IEnumerable<DamageResult> libraryResults = await LibraryCreatureCmd.Damage(
            choiceContext,
            [friendly, hostileCandidate],
            1m,
            ValueProp.Unpowered,
            owner.Creature);
        Require(libraryResults.All(result => result.Receiver != friendly)
                && libraryResults.Any(result => result.Receiver == hostileCandidate),
            "LibraryCreatureCmd.Damage retained a Friendly target.");

        IEnumerable<LibraryChaoResult>? chaoResults = await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            [friendly, hostileCandidate],
            1m,
            ValueProp.Unpowered,
            owner.Creature,
            null,
            null);
        Require(chaoResults != null
                && chaoResults.All(result => result.Receiver != friendly)
                && chaoResults.Any(result => result.Receiver == hostileCandidate),
            "LibraryCreatureCmd.ChaoDamage retained a Friendly target.");
    }

    private static void VerifyTargetManager(
        Creature friendly,
        Creature hostileCandidate)
    {
        NTargetManager targetManager = NTargetManager.Instance;
        FieldInfo targetTypeField = typeof(NTargetManager).GetField(
                "_validTargetsType",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "NTargetManager._validTargetsType is unavailable.");
        FieldInfo nodeFilterField = typeof(NTargetManager).GetField(
                "_nodeFilter",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "NTargetManager._nodeFilter is unavailable.");
        object? oldTargetType = targetTypeField.GetValue(targetManager);
        object? oldNodeFilter = nodeFilterField.GetValue(targetManager);
        try
        {
            targetTypeField.SetValue(targetManager, TargetType.AnyEnemy);
            nodeFilterField.SetValue(targetManager, null);
            NCreature friendlyNode = NCombatRoom.Instance?.GetCreatureNode(friendly)
                ?? throw new InvalidOperationException("Friendly creature node is missing.");
            NCreature hostileNode = NCombatRoom.Instance?.GetCreatureNode(hostileCandidate)
                ?? throw new InvalidOperationException("Hostile creature node is missing.");
            Require(!targetManager.AllowedToTargetNode(friendlyNode)
                    && targetManager.AllowedToTargetNode(hostileNode),
                "NTargetManager retained a Friendly AnyEnemy target.");
        }
        finally
        {
            targetTypeField.SetValue(targetManager, oldTargetType);
            nodeFilterField.SetValue(targetManager, oldNodeFilter);
        }
    }

    private static IReadOnlyList<Creature> GetPossibleTargets(object command)
    {
        MethodInfo method = command.GetType().GetMethod(
                "GetPossibleTargets",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                command.GetType().FullName + ".GetPossibleTargets is unavailable.");
        return method.Invoke(command, null) as IReadOnlyList<Creature>
            ?? throw new InvalidOperationException(
                command.GetType().FullName + ".GetPossibleTargets returned null.");
    }

    private static void RequireOnlyHostileTargets(
        IReadOnlyList<Creature> targets,
        Creature friendly,
        Creature hostileCandidate,
        string source)
    {
        Require(!targets.Contains(friendly) && targets.Contains(hostileCandidate),
            source + " retained a Friendly target or removed a hostile target.");
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
            "ALLYTYPESVERIFY");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
        ArmActLikeIt2OneShotVanillaEntry();

        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException(
                "NGame.StartRun did not return a Task.");
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
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "ally-type verifier combat start");
        return players;
    }

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "ActLikeIt2.Runtime.ActSelectionGate",
                throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
            Log.Info(LogPrefix
                + "Armed ActLikeIt2 one-shot vanilla EnterAct gate.");
        }
    }

    private static async Task WaitUntil(
        Func<bool> predicate,
        string description,
        int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree()
                ?? (SceneTree)Engine.GetMainLoop();
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

    private sealed class TestAllyProvider : IAllyTurnProvider
    {
        private readonly ICombatState? _combatState;
        private readonly Creature? _ally;

        public TestAllyProvider()
        {
            AllyType = AllyType.Neutral;
        }

        public TestAllyProvider(
            ICombatState combatState,
            Creature ally,
            AllyType allyType)
        {
            _combatState = combatState;
            _ally = ally;
            AllyType = allyType;
        }

        public string AllyId => "ALLY_TYPES_VERIFY_FRIENDLY";

        public AllyType AllyType { get; }

        public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

        public bool IsActiveEncounter(ICombatState combatState) =>
            _combatState != null && ReferenceEquals(_combatState, combatState);

        public Creature? FindAlly(ICombatState combatState) =>
            IsActiveEncounter(combatState) ? _ally : null;

        public bool CanTransferBlock => false;

        public void OnCombatReset(Creature? creature)
        {
        }
    }
}
