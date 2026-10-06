using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

public sealed class HeartOfAspiration : AspirationMonsterBase
{
    private const string AspirationPulseMoveId = "ASPIRATION_PULSE";
    private const string PulseMoveId = "PULSE";
    private const int AspirationPulseMinDamage = 19;
    private const int AspirationPulseMaxDamage = 21;
    private const int QuicknessAmount = 3;
    private const int BuffTurns = 1;
    private const int PulseBlock = 32;
    private const int PulseRegen = 20;

    public const string TextureRoot = HeartOfAspirationAssets.HeartOfAspirationMonsterRoot;
    public const string IdleTexturePath = TextureRoot + "idle.png";
    public const string AttackTexturePath = TextureRoot + "attack.png";
    public const string HitTexturePath = TextureRoot + "hit.png";
    public const string GuardTexturePath = TextureRoot + "guard.png";
    public const string AttackSfxPath = HeartOfAspirationAssets.HeartAttackSfx;

    private static readonly string[] AdditionalAssetPaths =
    [
        AttackSfxPath,
        HeartOfAspirationAssets.DesirePassivePowerIcon
    ];

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<HeartOfAspirationPageRelic>().Entry}.title";

    private int? _aspirationPulseDamageRoll;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 326, 321);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 330, 324);

    public override int DefaultChaoResistance => 210;

    public override IEnumerable<string> AssetPaths =>
        HeartOfAspirationCreatureVisuals
            .Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    protected override Task ApplyAspirationPassive() =>
        PowerCmdCompat.Apply<HeartOfAspirationDesirePassivePower>(Creature, 1, Creature, null, silent: true);

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState aspirationPulse = new(
            AspirationPulseMoveId,
            AspirationPulseMove,
            new SingleAttackIntent(() => GetAspirationPulseDamageRoll()),
            new BuffIntent());
        MoveState pulse = new(
            PulseMoveId,
            PulseMove,
            new DefendIntent(),
            new BuffIntent());

        aspirationPulse.FollowUpState = pulse;
        pulse.FollowUpState = aspirationPulse;

        return new MonsterMoveStateMachine([aspirationPulse, pulse], aspirationPulse);
    }

    private async Task AspirationPulseMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        int damage = EnsureAspirationPulseDamageRoll();
        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", HeartOfAspirationCreatureVisuals.AttackImpactSeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        IReadOnlyList<Creature> enemies = Creature.CombatState!.LivingEnemies()
            .ToArray();
        foreach (Creature enemy in enemies)
        {
            await PowerCmdCompat.Apply<LibraryQuicknessPower>(
                enemy,
                QuicknessAmount,
                Creature,
                null);
        }

        _aspirationPulseDamageRoll = null;
    }

    private async Task PulseMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        var enemies = Creature.CombatState!.Enemies;
        foreach (var enemy in enemies)
        {
            await CreatureCmd.GainBlock(enemy, PulseBlock, ValueProp.Move, null);
            await PowerCmdCompat.Apply<RegenPower>(enemy, PulseRegen, Creature, null);
        }
    }

    private int GetAspirationPulseDamageRoll() =>
        GetDisplayDamageRoll(ref _aspirationPulseDamageRoll, AspirationPulseMaxDamage);

    private int EnsureAspirationPulseDamageRoll() =>
        EnsureDamageRoll(ref _aspirationPulseDamageRoll, AspirationPulseMinDamage, AspirationPulseMaxDamage);

    /// <summary>
    /// Intent display reads the cached roll and never consumes shared RNG.
    /// AttackIntent.GetSingleDamage invokes the damage lambda from local UI
    /// render paths (intent label, hover tips, previews) whose timing and call
    /// counts differ between host and clients; rolling there desynchronizes
    /// the shared MonsterAi stream and the move executes with a different
    /// cached value per peer (observed as StateDivergence after enemy turn).
    /// </summary>
    private int GetDisplayDamageRoll(ref int? cachedRoll, int maxInclusive) =>
        IsMutable ? (cachedRoll ?? maxInclusive) : maxInclusive;

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !IsHeartOfAspirationEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<HeartOfAspirationPageRelic>(room, PageRelicTitleLocKey);
    }

    private static bool IsHeartOfAspirationEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is HeartOfAspiration);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            AddPageRewardsFromDeathHook(creature);
        }

        return Task.CompletedTask;
    }
}
