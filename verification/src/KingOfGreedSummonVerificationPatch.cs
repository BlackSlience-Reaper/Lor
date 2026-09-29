using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Powers;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class KingOfGreedSummonVerificationPatch
{
    private const string KingVerifyArg = "lor-verify-king-greed-summon-king";
    private const string MagicalGirlVerifyArg =
        "lor-verify-king-greed-summon-magical-girl";
    private const string SummonMoveId = "SUMMON_SHINING_HAPPINESS";
    private const string LogPrefix = "[LibraryOfRuina.KingOfGreedSummon.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        bool verifyKing = HasArg(KingVerifyArg);
        bool verifyMagicalGirl = HasArg(MagicalGirlVerifyArg);
        if (_started || (!verifyKing && !verifyMagicalGirl))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync(magicalGirl: verifyMagicalGirl));
        }).CallDeferred();
    }

    private static bool HasArg(string expected) =>
        CommandLineHelper.HasArg(expected)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            expected,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync(bool magicalGirl)
    {
        string form = magicalGirl ? "MAGICAL_GIRL" : "KING";
        try
        {
            VerifyEncounterPreloadsAllPossibleMonsterAssets();
            await VerifySummonMoveAsync(magicalGirl);
            Log.Info(LogPrefix + "KING_OF_GREED_SUMMON_" + form + "_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix + "KING_OF_GREED_SUMMON_" + form + "_FAILED: "
                + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyEncounterPreloadsAllPossibleMonsterAssets()
    {
        var encounter = (KingOfGreedElite)ModelDb
            .Encounter<KingOfGreedElite>()
            .ToMutable();
        HashSet<string> preloadedAssets = encounter.ExtraAssetPaths
            .ToHashSet(StringComparer.Ordinal);

        foreach (string path in encounter.AllPossibleMonsters
                     .SelectMany(static monster => monster.AssetPaths)
                     .Distinct(StringComparer.Ordinal))
        {
            Require(preloadedAssets.Contains(path),
                "Encounter preload omitted possible-monster asset " + path + ".");
            Require(ResourceLoader.Exists(path),
                "Possible-monster resource does not exist: " + path + ".");
        }
    }

    private static async Task VerifySummonMoveAsync(bool magicalGirl)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");

        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            magicalGirl ? "KINGGREEDSUMMONMAGICALVERIFY" : "KINGGREEDSUMMONKINGVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Elite,
            MapPointType.Elite,
            ModelDb.Encounter<KingOfGreedElite>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "King of Greed combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        ShiningHappiness summonDefinition = ModelDb.Monster<ShiningHappiness>();
        Require(summonDefinition.MinInitialHp <= summonDefinition.MaxInitialHp,
            "Shining Happiness HP range is invalid: "
            + summonDefinition.MinInitialHp + "-" + summonDefinition.MaxInitialHp + ".");
        Creature amber = combatState.Enemies.Single(static enemy =>
            enemy.Monster is GoldenAmber);
        await CreatureCmd.Kill(amber, force: true);

        var boss = (KingOfGreed)ModelDb.Monster<KingOfGreed>().ToMutable();
        boss.ConfigureInitialForm(magicalGirl);
        Creature bossCreature = await CreatureCmd.Add(
            boss,
            combatState,
            CombatSide.Enemy,
            KingOfGreedElite.BossSlot);

        Require(boss.IsMagicalGirl == magicalGirl,
            "Spawned boss form did not match the requested scenario.");
        Require(boss.NextMove?.Id == SummonMoveId,
            "Initial planned move was " + (boss.NextMove?.Id ?? "null")
            + " instead of " + SummonMoveId + ".");

        int damagedHp = Math.Max(1, bossCreature.MaxHp - 100);
        await CreatureCmd.SetCurrentHp(bossCreature, damagedHp);
        int expectedHp = (int)Math.Min(
            bossCreature.MaxHp,
            damagedHp + bossCreature.MaxHp * 0.10m);

        await boss.PerformMove();

        Require(bossCreature.CurrentHp == expectedHp,
            "Summon move healed boss to " + bossCreature.CurrentHp
            + ", expected " + expectedHp + ".");
        Creature summon = combatState.Enemies.SingleOrDefault(static enemy =>
                enemy.IsAlive && enemy.Monster is ShiningHappiness)
            ?? throw new InvalidOperationException(
                "Summon move did not add a living Shining Happiness.");
        Require(KingOfGreedElite.HappinessSlots.Contains(
                summon.SlotName,
                StringComparer.Ordinal),
            "Shining Happiness used invalid slot "
            + (summon.SlotName ?? "null") + ".");
        Require(summon.GetPower<MinionPower>() != null,
            "Shining Happiness did not receive Minion Power.");
        Require(summon.GetPower<LibraryOfRuinaShiningHappinessPower>() != null,
            "Shining Happiness did not receive its passive Power.");

        LibraryStrongPower strong = bossCreature.GetPower<LibraryStrongPower>()
            ?? throw new InvalidOperationException(
                "Shining Happiness did not grant permanent Strong to the boss.");
        LibraryEndurancePower endurance =
            bossCreature.GetPower<LibraryEndurancePower>()
            ?? throw new InvalidOperationException(
                "Shining Happiness did not grant permanent Endurance to the boss.");
        Require(strong.Amount == ShiningHappiness.KingStrongStacks,
            "Boss Strong aura was " + strong.Amount + ".");
        Require(endurance.Amount == ShiningHappiness.KingEnduranceStacks,
            "Boss Endurance aura was " + endurance.Amount + ".");
        Require(boss.ResolvePlannedMoveId() != SummonMoveId,
            "Boss remained routed to the summon move after performing it.");

        bossCreature.PrepareForNextTurn(combatState.PlayerCreatures);
        Require(boss.NextMove?.Id != SummonMoveId,
            "Boss rolled the summon move again on its next preparation.");
        Require(summon.Monster?.NextMove != null,
            "Shining Happiness did not obtain a valid next move.");

        RunManager.Instance.CleanUp(graceful: true);
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState() == null,
            "King of Greed summon verifier cleanup");
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
