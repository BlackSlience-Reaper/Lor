using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public abstract class NaturalFloorNihilMonster : NaturalFloorPhaseMonster
{
    private const string TurnId = "NIHIL_TURN";
    private static readonly string[] Ids = [TurnId];

    internal int RestoringStunTurns { get; set; }

    public string PlannedActions { get; private set; } = "";

    protected override string[] MoveIds => Ids;

    internal abstract string VisualId { get; }

    protected override IEnumerable<string> VisualAssets => NaturalFloorNihilVisuals.Assets(VisualId);

    protected override int SelectMove() => 0;

    protected abstract IEnumerable<(NaturalFloorNihilAction Action, string Target)> PlanActions();

    protected abstract IEnumerable<NaturalFloorNihilAction> AvailableActions { get; }

    public override IEnumerable<string> AssetPaths => base.AssetPaths
        .Concat(AvailableActions.SelectMany(action => CreateActionIntent(action, "B").AssetPaths))
        .Append("res://images/powers/natural_floor_nihil.png")
        .Append("res://images/powers/natural_floor_nihil_hatred.png")
        .Distinct();

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => moveStateId != TurnId;

    protected virtual Task AfterTurnPerformed() => Task.CompletedTask;

    protected static Task LoseLife(Creature target, int amount) =>
        CreatureCmd.SetCurrentHp(target, Math.Max(0, target.CurrentHp - amount));

    // 真实生命损失：按目标当前最大生命值计算并向上取整，包含多人生命缩放。
    protected static Task LoseLifePercent(Creature target, int percent) =>
        LoseLife(target, (int)Math.Ceiling(target.MaxHp * percent / 100m));

    internal static string SoundPath(string sound) =>
        "res://audio/sfx/natural_floor_nihil/" + sound + ".ogg";

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var router = new DelegatingMonsterRouterState("NIHIL_ROUTER", (_, _) =>
        {
            EnsurePlan();
            MoveState next = CreateTurn();
            MoveStateMachine!.States[TurnId] = next;
            return TurnId;
        });

        MoveState CreateTurn() => new(TurnId, targets => PerformMove(0, targets), CreateIntents(0).ToArray())
        {
            FollowUpStateId = "NIHIL_ROUTER",
            MustPerformOnceBeforeTransitioning = true
        };

        // 直接初始 MoveState 保证首回合混乱也有可恢复的 StateLog。
        MoveState initial = CreateTurn();
        IEnumerable<MonsterState> previews = AvailableActions.Select(action => new MoveState(
            action.ToString().ToUpperInvariant(), targets => PerformMove(0, targets), CreateActionIntent(action, "B"))
        {
            FollowUpStateId = "NIHIL_ROUTER"
        });
        return new MonsterMoveStateMachine(previews.Concat([initial, router]), initial);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        RestoringStunTurns = 0;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EnsurePlan();
        if (!Creature.IsStunned)
        {
            RefreshPlannedTurn();
        }
    }

    internal void EnsurePlan()
    {
        if (PlannedActions.Length == 0 && Creature?.CombatState != null && CanAct)
        {
            PlannedActions = string.Join(';', PlanActions().Select(entry => ((int)entry.Action) + ":" + entry.Target));
        }
    }

    internal void RefreshPlannedTurn(bool replacePlan = false)
    {
        if (replacePlan)
        {
            PlannedActions = "";
        }

        EnsurePlan();
        var move = new MoveState(TurnId, targets => PerformMove(0, targets), CreateIntents(0).ToArray())
        {
            FollowUpStateId = "NIHIL_ROUTER",
            MustPerformOnceBeforeTransitioning = true
        };
        MoveStateMachine!.States[TurnId] = move;
        SetMoveImmediate(move, forceTransition: true);
    }

    private IEnumerable<(NaturalFloorNihilAction Action, string Target)> ReadPlan()
    {
        foreach (string entry in PlannedActions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int delimiter = entry.IndexOf(':');
            if (delimiter > 0 && int.TryParse(entry[..delimiter], out int action)
                && Enum.IsDefined(typeof(NaturalFloorNihilAction), action))
            {
                yield return ((NaturalFloorNihilAction)action, entry[(delimiter + 1)..]);
            }
        }
    }

    protected override IEnumerable<AbstractIntent> CreateIntents(int move)
    {
        foreach ((NaturalFloorNihilAction action, string targetKey) in ReadPlan())
        {
            yield return CreateActionIntent(action, targetKey);
        }
    }

    internal AbstractIntent CreateActionIntent(NaturalFloorNihilAction action, string targetKey)
    {
        AbstractIntent intent = CreateActionIntentCore(action, targetKey);
        if (intent is CombinedTargetedAttackIntentBase targeted)
        {
            targeted.TargetLineResolver = _ => ResolveActionTargets(targetKey);
        }

        return intent;
    }

    private AbstractIntent CreateActionIntentCore(NaturalFloorNihilAction action, string targetKey)
    {
        NaturalFloorNihilMove spec = NaturalFloorNihilMoves.Get(action);
        string key = "NATURAL_NIHIL_" + action.ToString().ToUpperInvariant() + ".description";
        string playerKey = NaturalFloorNihilIntentText.PlayerDescriptionKey(key);
        IntentBadge[] badges = CreateBadges(spec, action).ToArray();
        Func<Creature, Creature?> target = owner => ResolveTarget(targetKey);

        if (action == NaturalFloorNihilAction.NihilWill)
        {
            return new NaturalFloorNihilWillIntent(spec, key, badges);
        }

        if (spec.IsGroup)
        {
            return new IndiscriminateAttackIntent(() => spec.Damage, () => spec.Hits, key,
                owner => Encounter?.NihilCombatTargets() ?? [], badges)
            {
                CombinedEffect = spec.Corrosion > 0 ? GroupAttackCombinedEffect.Debuff : GroupAttackCombinedEffect.None
            };
        }

        if (spec.Damage == 0)
        {
            if (spec.Block > 0)
            {
                return spec.Weak > 0
                    ? new CombinedDefendDebuffIntent(spec.Block, key, badges)
                    : new CombinedDefendBuffIntent(spec.Block, key, badges);
            }

            return new BadgedBuffIntent(badges, descriptionKey: key);
        }

        if (spec.Block > 0)
        {
            return new CombinedTargetedAttackDefendIntent(() => spec.Damage, () => spec.Hits,
                key, playerKey, true, target, spec.Block, badges);
        }

        if (spec.Strength > 0 || spec.Strong > 0)
        {
            return new CombinedTargetedAttackBuffIntent(() => spec.Damage, () => spec.Hits,
                key, playerKey, true, target, badges);
        }

        if (spec.Bleed > 0 || spec.Corrosion > 0 || spec.HatredTurns > 0 || spec.DirectHpLossPercent > 0)
        {
            return new CombinedTargetedAttackDebuffIntent(() => spec.Damage, () => spec.Hits,
                key, playerKey, true, target, badges);
        }

        return new NaturalFloorNihilAttackIntent(spec, _ => ResolveActionTargets(targetKey), key);
    }

    private static IEnumerable<IntentBadge> CreateBadges(NaturalFloorNihilMove spec, NaturalFloorNihilAction action)
    {
        if (action == NaturalFloorNihilAction.NihilWill)
        {
            yield return IntentBadge.FromPower<NaturalFloorNihilPower>(NaturalFloorNihilMoves.NihilDebuffStacks);
        }

        if (spec.Bleed > 0)
        {
            yield return IntentBadge.FromPower<LibraryBleedingPower>(spec.Bleed);
        }

        if (spec.Corrosion > 0)
        {
            yield return IntentBadge.FromPower<WrathServantNextTurnCorrosionPower>(spec.Corrosion);
        }

        if (spec.HatredTurns > 0)
        {
            yield return IntentBadge.FromPower<NaturalFloorNihilHatredStatus>(spec.HatredTurns);
        }

        if (spec.Strength > 0)
        {
            yield return IntentBadge.FromPower<StrengthPower>(spec.Strength);
        }

        if (spec.Strong > 0 || spec.OtherStrong > 0)
        {
            yield return IntentBadge.FromPower<LibraryStrongPower>(Math.Max(spec.Strong, spec.OtherStrong));
        }

        if (spec.HopeEndurance > 0)
        {
            yield return IntentBadge.FromPower<LibraryEndurancePower>(spec.HopeEndurance);
        }

        if (spec.Weak > 0)
        {
            yield return IntentBadge.FromPower<LibraryWeakPower>(spec.Weak);
        }
    }

    internal Creature? ResolveTarget(string key)
    {
        if (Encounter == null)
        {
            return null;
        }

        if (key == "B")
        {
            return Encounter.NihilBoss?.Creature;
        }

        if (key.StartsWith("G", StringComparison.Ordinal)
            && int.TryParse(key.AsSpan(1), out int kind))
        {
            Creature? girl = Encounter.FindNihilGirl((NaturalFloorGirlKind)kind)?.Creature;
            if (girl?.IsAlive == true)
            {
                return girl;
            }
        }

        if (key.StartsWith("P", StringComparison.Ordinal)
            && ulong.TryParse(key.AsSpan(1), out ulong netId))
        {
            Creature? player = Encounter.LivingPlayers().FirstOrDefault(player => player.Player?.NetId == netId);
            if (player != null)
            {
                return player;
            }
        }

        // 已预告的目标死亡时只按稳定顺序回退，不在显示路径消费随机数。
        return Encounter.LivingPlayers().OrderBy(player => player.Player!.NetId).FirstOrDefault();
    }

    protected string PlanPlayerTarget()
    {
        return "PLAYERS";
    }

    private Creature[] ResolveActionTargets(string targetKey)
    {
        Creature? target = ResolveTarget(targetKey);
        if (this is NaturalFloorNihilBoss && target?.IsPlayer == true)
        {
            // 虚无缥缈的玩家目标覆盖全体存活玩家，意图与结算共用稳定顺序。
            return Encounter?.LivingPlayers()
                .OrderBy(player => player.Player!.NetId)
                .ToArray() ?? [];
        }

        return target is { IsAlive: true } ? [target] : [];
    }

    protected string PlanGirlTarget(NaturalFloorGirlKind kind) =>
        Encounter?.FindNihilGirl(kind) is { Creature.IsAlive: true } ? "G" + (int)kind : PlanPlayerTarget();

    protected override async Task PerformMove(int move, IReadOnlyList<Creature> targets)
    {
        foreach ((NaturalFloorNihilAction action, string targetKey) in ReadPlan().ToArray())
        {
            if (!CanAct || Creature.IsStunned)
            {
                break;
            }

            Dictionary<Creature, int> losses = await PerformAction(action, targetKey);
            await AfterAction(action, losses);
        }

        await AfterTurnPerformed();
        PlannedActions = "";
    }

    protected virtual Task AfterAction(NaturalFloorNihilAction action, Dictionary<Creature, int> losses) => Task.CompletedTask;

    protected virtual void BeforeAction(NaturalFloorNihilAction action)
    {
    }

    private async Task<Dictionary<Creature, int>> PerformAction(NaturalFloorNihilAction action, string targetKey)
    {
        BeforeAction(action);
        NaturalFloorNihilMove spec = NaturalFloorNihilMoves.Get(action);
        Creature[] targets = [];
        if (action == NaturalFloorNihilAction.NihilWill)
        {
            targets = Encounter?.LivingPlayers()
                .OrderBy(player => player.Player!.NetId)
                .ToArray() ?? [];
        }
        else if (spec.IsGroup)
        {
            targets = Encounter?.NihilCombatTargets().ToArray() ?? [];
        }
        else
        {
            targets = ResolveActionTargets(targetKey);
        }
        var losses = targets.ToDictionary(target => target, _ => 0);
        LocalOggOneShotPlayer.Play(SoundPath(spec.Sound));
        using IDisposable? effect = NaturalFloorNihilEffects.Start(action, Creature, targets);

        if (spec.Damage > 0)
        {
            if (action == NaturalFloorNihilAction.TyrantPath)
            {
                await CreatureCmd.TriggerAnim(Creature, spec.Animation, NaturalFloorNihilVisuals.HitTime);
                foreach (Creature target in targets)
                {
                    int damage = target.IsPlayer ? spec.Damage : NaturalFloorNihilMoves.TyrantGirlDamage;
                    await Attack([target], damage, spec.Hits, animate: false);
                }
            }
            else
            {
                await Attack(targets, spec.Damage, spec.Hits, animate: true);
            }
        }
        else
        {
            await CreatureCmd.TriggerAnim(Creature, spec.Animation, NaturalFloorNihilVisuals.HitTime);
        }

        if (action == NaturalFloorNihilAction.WrathIncarnation)
        {
            LocalOggOneShotPlayer.Play(SoundPath("Angry_StrongAtk2"));
        }
        else if (action is NaturalFloorNihilAction.BossMagic or NaturalFloorNihilAction.LoveMagic)
        {
            LocalOggOneShotPlayer.Play(SoundPath("MagicalGirl_CastEnd"));
        }

        foreach (Creature target in targets.Where(target => target.IsAlive))
        {
            if (spec.Bleed > 0)
            {
                await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(target, spec.Bleed, Creature, null);
            }

            if (spec.Corrosion > 0)
            {
                await PowerCmdCompat.ApplyDebuff<WrathServantNextTurnCorrosionPower>(target, spec.Corrosion, Creature, null);
            }

            if (spec.HatredTurns > 0)
            {
                await PowerCmdCompat.ApplyDebuff<NaturalFloorNihilHatredStatus>(target, spec.HatredTurns, Creature, null);
            }

            if (spec.DirectHpLossPercent > 0 && losses[target] > 0)
            {
                await LoseLifePercent(target, spec.DirectHpLossPercent);
            }

            if (spec.Weak > 0)
            {
                await LibraryPowerCmd.Apply<LibraryWeakPower>(target, spec.Weak, spec.WeakTurns, Creature, null);
            }
        }

        if (action == NaturalFloorNihilAction.NihilWill)
        {
            foreach (Creature player in Encounter?.LivingPlayers() ?? [])
            {
                await PowerCmdCompat.Ensure<NaturalFloorNihilPower>(player, 1, Creature, null);
            }
        }

        if (CanAct)
        {
            if (spec.Block > 0)
            {
                Creature[] recipients = spec.IsTeamBlock ? Encounter?.NihilCombatTargets().ToArray() ?? [] : [Creature];
                foreach (Creature recipient in recipients)
                {
                    await CreatureCmd.GainBlock(recipient, spec.Block, ValueProp.Move, null);
                }
            }

            if (spec.Strength > 0)
            {
                await PowerCmdCompat.Apply<StrengthPower>(Creature, spec.Strength, Creature, null);
            }

            if (spec.Strong > 0)
            {
                await LibraryPowerCmd.Apply<LibraryStrongPower>(Creature, spec.Strong, spec.StrongTurns, Creature, null);
            }

            foreach (NaturalFloorMagicalGirl girl in Encounter?.LivingNihilGirls() ?? [])
            {
                if (girl.Creature == Creature)
                {
                    continue;
                }

                if (spec.OtherStrong > 0)
                {
                    await LibraryPowerCmd.Apply<LibraryStrongPower>(girl.Creature, spec.OtherStrong, spec.StrongTurns, Creature, null);
                }

                if (spec.HopeEndurance > 0)
                {
                    await LibraryPowerCmd.Apply<LibraryEndurancePower>(girl.Creature, spec.HopeEndurance, spec.StrongTurns, Creature, null);
                }
            }

            if (spec.SelfHpLoss > 0)
            {
                await LoseLife(Creature, spec.SelfHpLoss);
            }
        }

        return losses;

        async Task Attack(Creature[] victims, int damage, int hits, bool animate)
        {
            if (victims.Length == 0)
            {
                return;
            }

            using (TargetedMonsterAttackHelper.ForceTargets(Creature, victims))
            {
                var attack = DamageCmd.Attack(damage).FromMonster(this).WithHitCount(hits);
                if (spec.IsGroup)
                {
                    // 群体攻击每段先完成破格挡与停顿，再结算伤害；此回调独立执行，无需抑制后续段。
                    attack = attack.BeforeDamage(() =>
                        IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(
                            this,
                            damage,
                            victims,
                            suppressNextDamageHook: false));
                }

                if (animate)
                {
                    attack.WithAttackerAnim(spec.Animation, NaturalFloorNihilVisuals.HitTime);
                }

                var executed = await attack.Execute(null);
                foreach (DamageResult result in executed.Results.SelectMany(results => results))
                {
                    losses[result.Receiver] = losses.GetValueOrDefault(result.Receiver) + result.UnblockedDamage;
                }
            }
        }
    }
}
