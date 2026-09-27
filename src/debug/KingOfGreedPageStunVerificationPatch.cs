using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.KingOfGreed;
using LibraryOfRuina.relics.KingOfGreed;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class KingOfGreedPageStunVerificationPatch
{
    private const string VerifyArg = "lor-verify-king-greed-page-stun";
    private const string LogPrefix = "[LibraryOfRuina.KingOfGreedPageStun.Verify] ";
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _started;

    private static void Postfix()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => TaskHelper.RunSafely(RunAsync())).CallDeferred();
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
            Log.Info(LogPrefix + "KING_OF_GREED_PAGE_STUN_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "KING_OF_GREED_PAGE_STUN_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");

        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "KINGOFGREEDPAGESTUNVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Elite,
            MapPointType.Elite,
            ModelDb.Encounter<KingOfGreedElite>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "King of Greed page stun combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        Player owner = combatState.Players.Single();
        LibraryCreature attacker = combatState.Enemies.Single() as LibraryCreature
            ?? throw new InvalidOperationException("King of Greed attacker is not a LibraryCreature.");
        var relic = (KingOfGreedPageRelic)ModelDb
            .Relic<KingOfGreedPageRelic>()
            .ToMutable();
        owner.AddRelicInternal(relic, silent: true);
        SetProperty(relic, nameof(KingOfGreedPageRelic.Mode), KingOfGreedPageMode.Indulgence);
        SetProperty(relic, nameof(KingOfGreedPageRelic.StunNextAttackerPending), true);
        PendingAttackers(relic).Add(attacker);

        Require(attacker.CurrentChaoValue == attacker.MaxChaoValue,
            "Regression setup did not begin at full chao resistance.");
        LibraryResistanceLevel initialChaoSlash =
            attacker.GetChaosResistanceLevel(LibraryDamageType.Slash);
        LibraryResistanceLevel initialPhysicalSlash =
            attacker.GetPhysicalResistanceLevel(LibraryDamageType.Slash);

        await relic.AfterSideTurnStart(
            CombatSide.Player,
            combatState.Creatures,
            combatState);

        Require(attacker.IsStunned,
            "Indulgence did not install the STUNNED move.");
        Require(attacker.CurrentChaoValue == attacker.MaxChaoValue,
            "Vanilla Indulgence stun incorrectly depleted chao resistance.");
        Require(!attacker.IsStunPending,
            "Vanilla Indulgence stun incorrectly scheduled chao recovery.");
        Require(attacker.GetChaosResistanceLevel(LibraryDamageType.Slash)
                    == initialChaoSlash
                && attacker.GetPhysicalResistanceLevel(LibraryDamageType.Slash)
                    == initialPhysicalSlash,
            "Vanilla Indulgence stun incorrectly changed resistances.");

        var context = new ThrowingPlayerChoiceContext();
        await LibraryCreatureCmd.ChaoDamage(
            context,
            attacker,
            attacker.CurrentChaoValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            owner.Creature,
            cardSource: null,
            type: LibraryDamageType.None);

        Require(attacker.CurrentChaoValue == 0,
            "Chao damage did not deplete a vanilla-stunned enemy.");
        Require(attacker.IsStunPending,
            "A vanilla-stunned enemy did not enter Library stun at zero chao.");
        RequireAllVisibleResistances(attacker, LibraryResistanceLevel.Fatal);

        await LibraryCreatureCmd.SetChaoResistance(
            context,
            attacker,
            owner.Creature,
            LibraryDamageType.Slash,
            LibraryResistanceLevel.Normal);
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            attacker,
            owner.Creature,
            LibraryDamageType.Slash,
            LibraryResistanceLevel.Vulnerable);

        Require(attacker.GetChaosResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Fatal
                && attacker.GetPhysicalResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Fatal,
            "Changing post-stun resistances replaced the visible Fatal stun layer.");
        Require(attacker.GetPostStunChaosResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Normal
                && attacker.GetPostStunPhysicalResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Vulnerable,
            "Resistance changes made while stunned were not recorded for recovery.");

        await ((LibraryMonsterModel)attacker.Monster!).AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            combatState.Enemies);

        Require(!attacker.IsStunPending,
            "Stun recovery remained pending after the enemy turn ended.");
        Require(attacker.CurrentChaoValue == attacker.MaxChaoValue,
            "Enemy did not recover full chao resistance after the stunned turn.");
        Require(attacker.GetChaosResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Normal
                && attacker.GetPhysicalResistanceLevel(LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Vulnerable,
            "Post-stun resistance changes were not restored after recovery.");

        RunManager.Instance.CleanUp(graceful: true);
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState() == null,
            "King of Greed page stun verifier cleanup");
    }

    private static List<Creature> PendingAttackers(KingOfGreedPageRelic relic) =>
        typeof(KingOfGreedPageRelic)
            .GetField("_pendingStunAttackers", InstanceFlags)
            ?.GetValue(relic) as List<Creature>
        ?? throw new MissingFieldException(
            typeof(KingOfGreedPageRelic).FullName,
            "_pendingStunAttackers");

    private static void SetProperty<T>(
        KingOfGreedPageRelic relic,
        string propertyName,
        T value)
    {
        PropertyInfo property = typeof(KingOfGreedPageRelic).GetProperty(
                propertyName,
                InstanceFlags)
            ?? throw new MissingMemberException(
                typeof(KingOfGreedPageRelic).FullName,
                propertyName);
        property.SetValue(relic, value);
    }

    private static void RequireAllVisibleResistances(
        LibraryCreature creature,
        LibraryResistanceLevel expected)
    {
        foreach (LibraryDamageType type in new[]
                 {
                     LibraryDamageType.Slash,
                     LibraryDamageType.Pierce,
                     LibraryDamageType.Blunt
                 })
        {
            Require(creature.GetChaosResistanceLevel(type) == expected,
                "Visible chao resistance was not " + expected + " for " + type + ".");
            Require(creature.GetPhysicalResistanceLevel(type) == expected,
                "Visible physical resistance was not " + expected + " for " + type + ".");
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
}
