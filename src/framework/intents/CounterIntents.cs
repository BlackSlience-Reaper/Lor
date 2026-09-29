using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.intents;

public interface ICounterIntentVisual
{
    string CounterAnimation { get; }

    string CounterAnimationFramePath { get; }
}

public interface ICounterIntent : ICounterIntentVisual
{
    Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget);
}

public interface ICounterIntentTurnStartEffect
{
    Task ActivateCounterIntent(PlayerChoiceContext choiceContext, Creature owner);
}

public sealed class CounterAttackIntent : AttackIntent, ICounterIntent
{
    private const string AssetRoot = "intents/counter/";

    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;
    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterAttackIntent(int damage, int repeats = 1, string? descriptionKey = null)
        : this(() => damage, () => repeats, descriptionKey)
    {
    }

    public CounterAttackIntent(
        int damage,
        Func<PlayerChoiceContext, Creature, Task> perform,
        int repeats = 1,
        string? descriptionKey = null)
        : this(() => damage, () => repeats, descriptionKey, (ctx, owner, _) => perform(ctx, owner))
    {
    }

    public CounterAttackIntent(
        int damage,
        Func<PlayerChoiceContext, Creature, Creature, Task> perform,
        int repeats = 1,
        string? descriptionKey = null)
        : this(() => damage, () => repeats, descriptionKey, perform)
    {
    }

    public CounterAttackIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        Func<PlayerChoiceContext, Creature, Creature, Task>? perform = null)
    {
        DamageCalc = damageCalc ?? throw new ArgumentNullException(nameof(damageCalc));
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
        _perform = perform;
    }

    public override int Repeats => Math.Max(1, _repeatCalc());

    protected override string IntentPrefix => "COUNTER_ATTACK";

    public override IEnumerable<string> AssetPaths =>
        Enumerable.Range(1, 5).Select(static tier => ImageHelper.GetImagePath(AssetRoot + $"counter_attack_{tier}.png"))
            .Concat(CounterIntentAnimData.AssetPaths);

    public string CounterAnimationFramePath => GetCounterAttackPathForDamage(0);

    public string CounterAnimation => IntentAnimData.attack1;

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(GetCounterAttackPathForDamage(GetTotalDamage(targets, owner)));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return GetCounterAttackAnimationForDamage(GetTotalDamage(targets, owner));
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        if (string.IsNullOrWhiteSpace(_descriptionKey))
        {
            return base.GetIntentDescription(targets, owner);
        }

        LocString desc = new("intents", _descriptionKey);
        desc.Add("Damage", GetSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        return desc;
    }

    public string GetCounterAnimationFramePath(IEnumerable<Creature> targets, Creature owner)
    {
        return GetCounterAttackPathForDamage(GetTotalDamage(targets, owner));
    }

    public string GetCounterAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return GetCounterAttackAnimationForDamage(GetTotalDamage(targets, owner));
    }

    public async Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);

        if (_perform != null)
        {
            await _perform(choiceContext, owner, counterTarget);
            return;
        }

        if (owner.IsDead || owner.Monster == null || counterTarget.IsDead)
        {
            return;
        }

        using (TargetedMonsterAttackHelper.ForceTargets(owner, [counterTarget]))
        {
            await DamageCmd.Attack(DamageCalc?.Invoke() ?? 0m)
                .FromMonster(owner.Monster)
                .WithHitCount(Repeats)
                .Execute(choiceContext);
        }
    }

    private static string GetCounterAttackPathForDamage(int totalDamage)
    {
        int tier = totalDamage < 5 ? 1
            : totalDamage < 10 ? 2
            : totalDamage < 20 ? 3
            : totalDamage < 40 ? 4
            : 5;
        return ImageHelper.GetImagePath(AssetRoot + $"counter_attack_{tier}.png");
    }

    private static string GetCounterAttackAnimationForDamage(int totalDamage)
    {
        return totalDamage < 5 ? IntentAnimData.attack1
            : totalDamage < 10 ? IntentAnimData.attack2
            : totalDamage < 20 ? IntentAnimData.attack3
            : totalDamage < 40 ? IntentAnimData.attack4
            : IntentAnimData.attack5;
    }
}

public sealed class CounterBuffIntent : BuffIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_buff.png";

    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterBuffIntent(Func<PlayerChoiceContext, Creature, Task>? perform = null)
    {
        _perform = perform == null ? null : (ctx, owner, _) => perform(ctx, owner);
    }

    public CounterBuffIntent(Func<PlayerChoiceContext, Creature, Creature, Task> perform)
    {
        _perform = perform;
    }

    protected override string IntentPrefix => "COUNTER_BUFF";

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.buff;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return _perform?.Invoke(choiceContext, owner, counterTarget) ?? Task.CompletedTask;
    }
}

public sealed class CounterDebuffIntent : DebuffIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_debuff.png";

    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterDebuffIntent(bool strong = false, Func<PlayerChoiceContext, Creature, Task>? perform = null)
        : base(strong)
    {
        _perform = perform == null ? null : (ctx, owner, _) => perform(ctx, owner);
    }

    public CounterDebuffIntent(Func<PlayerChoiceContext, Creature, Task> perform, bool strong = false)
        : this(strong, perform)
    {
    }

    public CounterDebuffIntent(Func<PlayerChoiceContext, Creature, Creature, Task> perform, bool strong = false)
        : base(strong)
    {
        _perform = perform;
    }

    protected override string IntentPrefix => "COUNTER_DEBUFF";

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.debuff;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.debuff;
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return _perform?.Invoke(choiceContext, owner, counterTarget) ?? Task.CompletedTask;
    }
}

public sealed class CounterCardDebuffIntent : CardDebuffIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_card_debuff.png";

    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterCardDebuffIntent(Func<PlayerChoiceContext, Creature, Task>? perform = null)
    {
        _perform = perform == null ? null : (ctx, owner, _) => perform(ctx, owner);
    }

    public CounterCardDebuffIntent(Func<PlayerChoiceContext, Creature, Creature, Task> perform)
    {
        _perform = perform;
    }

    protected override string IntentPrefix => "COUNTER_CARD_DEBUFF";

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.cardDebuff;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.cardDebuff;
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return _perform?.Invoke(choiceContext, owner, counterTarget) ?? Task.CompletedTask;
    }
}

public sealed class CounterDefendIntent : DefendIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_defend.png";

    private readonly Func<decimal> _blockCalc;
    private readonly Func<PlayerChoiceContext, Creature, Task>? _perform;

    public CounterDefendIntent(int block = 0, Func<PlayerChoiceContext, Creature, Task>? perform = null)
        : this(() => block, perform)
    {
    }

    public CounterDefendIntent(Func<decimal> blockCalc, Func<PlayerChoiceContext, Creature, Task>? perform = null)
    {
        _blockCalc = blockCalc ?? throw new ArgumentNullException(nameof(blockCalc));
        _perform = perform;
    }

    public CounterDefendIntent(Func<PlayerChoiceContext, Creature, Task> perform)
        : this(0, perform)
    {
    }

    protected override string IntentPrefix => "COUNTER_DEFEND";

    public decimal BlockAmount => Math.Max(0m, _blockCalc());

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.defend;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.defend;
    }

    public async Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);

        if (_perform != null)
        {
            await _perform(choiceContext, owner);
            return;
        }

        decimal block = BlockAmount;
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(owner, block, ValueProp.Move, null);
        }
    }
}

public class DodgeIntent : DefendIntent, ICounterIntentVisual
{
    public const string Sprite = "intents/dodge/dodge.png";

    private readonly Func<int> _dodgeCalc;

    public DodgeIntent(int dodge)
        : this(() => dodge)
    {
    }

    public DodgeIntent(Func<int> dodgeCalc)
    {
        _dodgeCalc = dodgeCalc ?? throw new ArgumentNullException(nameof(dodgeCalc));
    }

    public int DodgeValue => Math.Max(0, _dodgeCalc());

    protected override string IntentPrefix => "DODGE";

    public override IEnumerable<string> AssetPaths =>
        new[] { CounterAnimationFramePath }.Concat(CounterIntentAnimData.GetAssetPaths(CounterAnimation));

    public virtual string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public virtual string CounterAnimation => CounterIntentAnimData.Dodge;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = base.GetIntentDescription(targets, owner);
        desc.Add("Dodge", DodgeValue);
        return desc;
    }

    public Task ActivateDodgeIntent(PlayerChoiceContext choiceContext, Creature owner)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        return LibraryOfRuinaDodgeDicePower.ApplyDodge(
            choiceContext,
            owner,
            DodgeValue,
            owner,
            null);
    }
}

public sealed class CounterDodgeIntent : DodgeIntent, ICounterIntent, ICounterIntentTurnStartEffect
{
    public new const string Sprite = "intents/counter/counter_dodge.png";

    public CounterDodgeIntent(int dodge)
        : base(dodge)
    {
    }

    public CounterDodgeIntent(Func<int> dodgeCalc)
        : base(dodgeCalc)
    {
    }

    protected override string IntentPrefix => "COUNTER_DODGE";

    public override IEnumerable<string> AssetPaths =>
        new[] { CounterAnimationFramePath }.Concat(CounterIntentAnimData.GetAssetPaths(CounterAnimation));

    public override string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public override string CounterAnimation => CounterIntentAnimData.CounterDodge;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public Task ActivateCounterIntent(PlayerChoiceContext choiceContext, Creature owner)
    {
        return ActivateDodgeIntent(choiceContext, owner);
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return Task.CompletedTask;
    }
}

public sealed class CounterSummonIntent : SummonIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_summon.png";

    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterSummonIntent(Func<PlayerChoiceContext, Creature, Task>? perform = null)
    {
        _perform = perform == null ? null : (ctx, owner, _) => perform(ctx, owner);
    }

    public CounterSummonIntent(Func<PlayerChoiceContext, Creature, Creature, Task> perform)
    {
        _perform = perform;
    }

    protected override string IntentPrefix => "COUNTER_SUMMON";

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.summon;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.summon;
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return _perform?.Invoke(choiceContext, owner, counterTarget) ?? Task.CompletedTask;
    }
}

public sealed class CounterStatusIntent : StatusIntent, ICounterIntent
{
    public const string Sprite = "intents/counter/counter_status_card.png";

    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    public CounterStatusIntent(int count, Func<PlayerChoiceContext, Creature, Task>? perform = null)
        : base(count)
    {
        _perform = perform == null ? null : (ctx, owner, _) => perform(ctx, owner);
    }

    public CounterStatusIntent(int count, Func<PlayerChoiceContext, Creature, Creature, Task> perform)
        : base(count)
    {
        _perform = perform;
    }

    protected override string IntentPrefix => "COUNTER_STATUS";

    public override IEnumerable<string> AssetPaths => [CounterAnimationFramePath];

    public string CounterAnimationFramePath => ImageHelper.GetImagePath(Sprite);

    public string CounterAnimation => IntentAnimData.status;

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CounterAnimationFramePath);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.status;
    }

    public Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);
        return _perform?.Invoke(choiceContext, owner, counterTarget) ?? Task.CompletedTask;
    }
}

public static class CounterIntentVisuals
{
    public static IEnumerable<string> AssetPaths =>
    new[]
    {
        ImageHelper.GetImagePath("intents/counter/counter_attack_1.png"),
        ImageHelper.GetImagePath("intents/counter/counter_attack_2.png"),
        ImageHelper.GetImagePath("intents/counter/counter_attack_3.png"),
        ImageHelper.GetImagePath("intents/counter/counter_attack_4.png"),
        ImageHelper.GetImagePath("intents/counter/counter_attack_5.png"),
        ImageHelper.GetImagePath(CounterBuffIntent.Sprite),
        ImageHelper.GetImagePath(CounterDebuffIntent.Sprite),
        ImageHelper.GetImagePath(CounterCardDebuffIntent.Sprite),
        ImageHelper.GetImagePath(CounterDefendIntent.Sprite),
        ImageHelper.GetImagePath(DodgeIntent.Sprite),
        ImageHelper.GetImagePath(CounterDodgeIntent.Sprite),
        ImageHelper.GetImagePath(CounterSummonIntent.Sprite),
        ImageHelper.GetImagePath(CounterStatusIntent.Sprite)
    }.Concat(CounterIntentAnimData.AssetPaths);

    public static AbstractIntent ToCounterIntent(AbstractIntent intent)
    {
        return intent switch
        {
            CounterAttackIntent or CounterBuffIntent or CounterDebuffIntent or CounterCardDebuffIntent
                or CounterDefendIntent or CounterDodgeIntent or CounterSummonIntent or CounterStatusIntent => intent,
            SingleAttackIntent single => new CounterAttackIntent(
                single.DamageCalc ?? (() => 0m),
                () => single.Repeats),
            MultiAttackIntent multi => new CounterAttackIntent(
                multi.DamageCalc ?? (() => 0m),
                () => multi.Repeats),
            DynamicAttackIntent dynamic => new CounterAttackIntent(
                dynamic.DamageCalc ?? (() => 0m),
                () => dynamic.Repeats),
            BuffIntent when intent.GetType() == typeof(BuffIntent) => new CounterBuffIntent(),
            DebuffIntent debuff when intent.GetType() == typeof(DebuffIntent) => new CounterDebuffIntent(debuff.IntentType == IntentType.DebuffStrong),
            CardDebuffIntent when intent.GetType() == typeof(CardDebuffIntent) => new CounterCardDebuffIntent(),
            DodgeIntent dodge => new CounterDodgeIntent(() => dodge.DodgeValue),
            DefendIntent when intent.GetType() == typeof(DefendIntent) => new CounterDefendIntent(),
            SummonIntent when intent.GetType() == typeof(SummonIntent) => new CounterSummonIntent(),
            StatusIntent status when intent.GetType() == typeof(StatusIntent) => new CounterStatusIntent(status.CardCount),
            _ => intent
        };
    }
}
