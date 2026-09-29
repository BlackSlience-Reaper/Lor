using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 把怪物、能力、遭遇的战斗状态打成 <see cref="SavedProperties"/>，供“存下来再填回新实例”的套件使用。
/// <para>
/// 这些属性曾经带 <c>[SavedProperty]</c>，但原版只对卡牌、遗物、附魔、Modifier 调 <c>SavedProperties.From/Fill</c>，
/// 联机的 <c>NetFullCombatState</c> 也只带能力的 id 与层数，所以它们从未进过存档或同步，已经去掉特性。
/// 原版 <c>SavedProperties.From</c> 只认特性，这里按下表列出的属性名与原来的 <see cref="SerializationCondition"/>
/// 复刻它的取值规则（按属性名排序、按条件跳过缺省值、按值类型分桶），得到的对象与去掉特性前 <c>From</c> 的结果相同；
/// 填回仍用原版 <see cref="SavedProperties.Fill"/>，它按属性名找设置器，与特性无关。
/// </para>
/// 表按声明类型列出，与去掉特性前 <c>snapshots/saved_properties.txt</c> 里这些载体的行一一对应。
/// 自然层二至四阶段怪物与闪耀幸福能力仍带特性（遭遇存档经 <c>SavedProperties.From</c> 写它们），不在表里。
/// </summary>
internal static class CombatStateProperties
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // 声明类型 → (AlwaysSave 的属性, SaveIfNotTypeDefault 的属性)。原来的特性都没有写 order。
    private static readonly Dictionary<Type, (string[] Always, string[] IfNotTypeDefault)> Declared = new()
    {
        [typeof(global::LibraryOfRuina.encounters.LanguageFloorLiberation.LanguageFloorLiberationEncounter)] = new(
            ["CurrentPhase"],
            ["EndedByLethalDamage", "KilledBossCount", "PhaseComplete", "SettlementTriggered", "TransitionPending"]),
        [typeof(global::LibraryOfRuina.content.abnormalities.JudgementBird.JudgementBird)] = new(
            [],
            ["FullOfEvilPending", "PlannedJudgementTargetCombatIds"]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorBloodBat)] = new(
            ["LastHydrophobiaRound"],
            []),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorCobaltScar)] = new(
            [
                "PlannedMoveOne", "PlannedMoveThree", "PlannedMoveTwo", "ShadowCardsPlayedByPlayer", "SwallowedCards",
                "SwallowedOwnerIndexes"
            ],
            [
                "AccumulatedDamage", "BigWolfEntryHp", "ForceInstinctNextTurn", "ForceRoarNextTurn", "Form",
                "OpeningResolved", "PlayerTurnsSinceSwallow", "ShadowReleasePending", "ShadowTurnsRemaining",
                "SwallowWindowActive", "TurnsUntilInstinct"
            ]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorDipsia)] = new(
            ["LastHydrophobiaRound", "PreviousMove"],
            ["IsTransformed", "TransformPending", "TransformTriggered"]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorLostEverythingWolf)] = new(
            ["PlannedMoveOne", "PlannedMoveTwo"],
            ["LowHealthMode", "PlannedMoveFour", "PlannedMoveThree", "WolfTurnCount"]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorMimicry)] = new(
            ["Form", "PendingForm", "PreviousMove"],
            [
                "EvolutionPending", "FormOneMaxHp", "FormThreeMaxHp", "FormTwoActionsCompleted", "FormTwoMaxHp",
                "Initialized", "MimicStacks", "PlannedMoveEnhanced", "PlannedTargetCombatId", "RoundDamageTaken",
                "SkipCurrentFormThreeEnemyEndRecovery", "SkipCurrentFormTwoEnemyEnd"
            ]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorScarletScar)] = new(
            ["PlannedMoveOne", "PlannedMoveTwo"],
            ["EnemyTurnCount", "UnrelievedAnger"]),
        [typeof(global::LibraryOfRuina.monsters.LanguageFloorLiberation.LanguageFloorSmilingFace)] = new(
            [
                "Form", "PendingFormTransition", "PlannedMoveFour", "PlannedMoveOne", "PlannedMoveThree",
                "PlannedMoveTwo", "PlannedTargetFour", "PlannedTargetOne", "PlannedTargetThree", "PlannedTargetTwo",
                "PreviousNormalMove"
            ],
            [
                "CorpseTrialActive", "CorpseTrialPending", "CorpseTrialPlayerTurnsRemaining",
                "FakeDeathPlayerTurnsRemaining", "ForceKillable", "FormOneMaxHp", "FormThreeMaxHp", "FormTurnCount",
                "FormTwoMaxHp", "HpAtLastSpawnThreshold", "Initialized", "PendingCorpseSpawns",
                "PendingFormTransitionIsPromotion", "WaitingForDowngrade"
            ]),
        [typeof(global::LibraryOfRuina.monsters.LiteratureFloorLiberation.LiteratureFloorBlackSwanBoss)] = new(
            [],
            ["CompletedMoveCycleMask"]),
        [typeof(global::LibraryOfRuina.monsters.LiteratureFloorLiberation.LiteratureFloorRedEyesBoss)] = new(
            [],
            ["HuntPending"]),
        [typeof(global::LibraryOfRuina.monsters.NaturalFloorLiberation.NaturalFloorLoveAndHatredBoss)] = new(
            [],
            ["CompletedFormActions", "CompletedMoveCycleMask", "IsSnakeForm", "LastMoveNumber", "SnakeBaseHp"]),
        [typeof(global::LibraryOfRuina.monsters.NaturalFloorLiberation.NaturalFloorMagicalGirl)] = new(
            ["HasReceivedGreedBlock", "LoveHitCount", "PendingGreedBlock", "RotationIndex"],
            []),
        [typeof(global::LibraryOfRuina.monsters.NaturalFloorLiberation.NaturalFloorNihilBoss)] = new(
            [
                "CompletedFormTurns", "Form", "GreedGroupPending", "HatredHitCount", "NihilApplied", "NormalMoveIndex",
                "PendingForm", "SealedSwords", "WrathStaggered"
            ],
            []),
        [typeof(global::LibraryOfRuina.monsters.NaturalFloorLiberation.NaturalFloorNihilMonster)] = new(
            ["PlannedActions"],
            []),
        [typeof(global::LibraryOfRuina.monsters.NaturalFloorLiberation.NaturalFloorNihilStatue)] = new(
            ["SummonPending"],
            []),
        [typeof(global::LibraryOfRuina.content.abnormalities.Ozma.Ozma)] = new(
            ["ForgottenOriginalCards", "Mode"],
            [
                "ForgetCount", "ForgottenOriginalOwnerCombatIds", "ForgottenPlayerCombatIds",
                "ForgottenPlayerTurnsStarted", "NextInterferenceUsesPressure", "StunAfterForgottenRestorePending",
                "TrueJackDirection", "TrueJackHitsRemaining"
            ]),
        [typeof(global::LibraryOfRuina.content.abnormalities.Ozma.OzmaJack)] = new(
            [],
            ["IsAwake"]),
        [typeof(global::LibraryOfRuina.content.abnormalities.SpiderBud.SpiderBud)] = new(
            [],
            ["HuntPending"]),
        [typeof(global::LibraryOfRuina.framework.powers.NextTurnVigorPower)] = new(
            ["ActivationRound"],
            []),
        [typeof(global::LibraryOfRuina.powers.PhilosophyFloorLiberation.PhilosophyFloorTwilightBrokenEggPower)] = new(
            ["ReflectionRound", "ReflectionsThisRound"],
            []),
        [typeof(global::LibraryOfRuina.powers.SocialFloorLiberation.SocialFloorCouragePower)] = new(
            [
                "IsEnergyOverrideActive", "PendingTurnStartActivations", "RemoveAtNextPlayerTurnEnd",
                "SerializedHolderNetId"
            ],
            []),
        [typeof(global::LibraryOfRuina.powers.SocialFloorLiberation.SocialFloorCowardPower)] = new(
            ["SerializedHolderNetId"],
            []),
        [typeof(global::LibraryOfRuina.powers.SocialFloorLiberation.SocialFloorOzmaPower)] = new(
            ["IsHolderActive", "SerializedHolderNetId"],
            []),
        [typeof(global::LibraryOfRuina.powers.SocialFloorLiberation.SocialFloorScaredyCatPower)] = new(
            ["CardsSubmittedThisTurn", "CourageCardGranted", "IsHolderActive", "SerializedHolderNetId"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.CryingChildren.CryingChildMonsterBase)] = new(
            ["Overheated", "TargetCombatId"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.CryingChildren.CryingChildrenEncounter)] = new(
            ["LastChildrenPlanRound"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.CryingChildren.CryingSwiftPower)] = new(
            ["ActiveRound", "ObtainedRound"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.CryingChildren.ReverberationPhilip)] = new(
            ["ChildrenSpawned", "LastPreparedRound", "Phase", "TransitionPending"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.CryingChildren.UnspeakingChild)] = new(
            ["LastPreparedRound", "SpawnRound"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.GearChurch.EileenNuovoFabricPower)] = new(
            ["HitsReceived", "LastResetRound"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.GearChurch.GearChurchEncounter)] = new(
            ["LastFollowerPlanRound"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.GearChurch.GearChurchFollower)] = new(
            ["DeathRecorded"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.GearChurch.GearChurchMonsterBase)] = new(
            ["LastPerformedMove", "LastPreparedRound", "NextMoveSlot", "TargetCombatId"],
            []),
        [typeof(global::LibraryOfRuina.reverberation.GearChurch.ReverberationEileen)] = new(
            ["LastChaoRound", "LastFollowerDeathRound", "Phase", "PhaseFollowerDeaths", "TransitionPending"],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Iori.IoriMonsterBase)] = new(
            [
                "CurrentStance", "EscapeCompleted", "EscapeQueued", "HasExpandedRoundTwoCapacity", "LastPlannedRound",
                "LastRegularMove", "PlannedNextStance", "ReceptionRoundOffset", "StageSnapshotRestored"
            ],
            ["ChainsContributionByPlayerNetId", "SelectedStanceMask"]),
        [typeof(global::LibraryOfRuina.specialguests.Kali.Kali)] = new(
            [
                "EgoActive", "EgoManifestationPending", "EgoReturnCountdown", "EgoThresholdTurnLockConsumed",
                "EgoThresholdTurnLockRound", "EgoThresholdTurnLockSide", "EgoTriggered", "PersistedBloodMistStacks",
                "PersistedEnemyCardPlanNumber"
            ],
            ["PersistedQueuedExtraCardIds"]),
        [typeof(global::LibraryOfRuina.specialguests.Rnfmabj.Rnfmabj)] = new(
            [
                "BladeCooldown", "CurrentDirectiveCompleted", "CurrentDirectiveTaskIndex", "DirectivePlanSerial",
                "DirectiveProgressByPlayerNetId", "DirectiveRequiredPlayerNetIds", "DirectiveSequenceCodes",
                "DirectiveSequenceLength", "IsUnited", "LastActivatedPlanSerial", "Phase", "PhaseThreeInitialized",
                "PlannedTargetFive", "PlannedTargetFour", "PlannedTargetOne", "PlannedTargetThree", "PlannedTargetTwo"
            ],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Rnfmabj.RnfmabjHandBase)] = new(
            ["IsFakeDead", "PhaseThreePatternStep"],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Rnfmabj.RnfmabjMonsterBase)] = new(
            ["LastPlannedRound", "PlanSerial", "PlannedDamageValues"],
            []),
        [typeof(global::LibraryOfRuina.specialguests.SpecialGuestMonsterBase)] = new(
            [
                "EmotionLevel", "EmotionUnits", "HasCompletedFirstTurn", "IntentCapacity", "LastEmotionResolvedRound",
                "LevelFiveRoundCounter", "PatternIndex", "PlannedMoveFive", "PlannedMoveFour", "PlannedMoveOne",
                "PlannedMoveThree", "PlannedMoveTwo", "UnblockedDamageDealtThisRound"
            ],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Xiao.XiaoEgo)] = new(
            ["AllAttackResultsFullyBlocked", "HadAnyAttackResultThisEnemyTurn"],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Xiao.XiaoIgnitePower)] = new(
            ["BurnStackBeforeDecay", "TookBurnDamageThisTurn"],
            []),
        [typeof(global::LibraryOfRuina.specialguests.Xiao.XiaoReverseScalePassivePower)] = new(
            [],
            ["CardsPlayedByPlayerNetId"]),
        [typeof(global::LibraryOfRuina.specialguests.Xiao.XiaoStageOne)] = new(
            ["ForceTrueDeath", "IsFakeDead"],
            []),
    };

    private static Dictionary<(Type, string), SerializationCondition>? _conditions;

    /// <summary>表里全部属性（声明类型, 属性名）。</summary>
    public static IEnumerable<(Type DeclaringType, string Name)> All =>
        Conditions.Keys.Select(static key => (key.Item1, key.Item2));

    private static Dictionary<(Type, string), SerializationCondition> Conditions => _conditions ??= Build();

    /// <summary>
    /// 类型（含继承来的属性）不带 <c>[SavedProperty]</c>，也不在本模组的 SavedProperty 自动发现与 schema 指纹里。
    /// 重新加上特性会占用联机 net-id 却仍然不存档，套件用它守住。
    /// </summary>
    public static bool IsTransient(Type type) =>
        type.GetProperties(InstanceFlags).All(static property => property.GetCustomAttribute<SavedPropertyAttribute>() == null)
        && !SavedPropertiesTypeCacheCompat.GetAllModSavedPropertyTypes().Contains(type)
        && !SavedPropertiesTypeCacheCompat.BuildSchemaFingerprintMaterial()
            .Contains("saved|" + type.FullName + "|", StringComparison.Ordinal);

    /// <summary>属性存在、不带 <c>[SavedProperty]</c>，并且在上表里（套件的读档模拟会带上它）。</summary>
    public static bool IsListed(Type type, string name)
    {
        PropertyInfo? property = type.GetProperty(name, InstanceFlags);
        return property?.DeclaringType != null
               && property.GetCustomAttribute<SavedPropertyAttribute>() == null
               && Conditions.ContainsKey((property.DeclaringType, name));
    }

    /// <summary>
    /// 对 <paramref name="model"/> 做原版 <c>SavedProperties.From</c> 会做的事，只是属性集合取自上表而不是特性。
    /// 没有任何值要写时返回 null，与原版相同。
    /// </summary>
    public static SavedProperties? From(AbstractModel model)
    {
        var saved = new SavedProperties();
        bool any = false;
        // 原版对运行时类型取 GetProperties(Instance|Public|NonPublic)：基类的 private 属性不在其中，按 order、再按属性名排序。
        foreach (PropertyInfo property in model.GetType().GetProperties(InstanceFlags)
                     .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            if (property.DeclaringType == null
                || !Conditions.TryGetValue((property.DeclaringType, property.Name), out SerializationCondition condition))
            {
                continue;
            }

            object? value = property.GetValue(model);
            if (!condition.ShouldSerialize(value, property) || value == null)
            {
                continue;
            }

            any = true;
            switch (value)
            {
                case int number:
                    (saved.ints ??= []).Add(new SavedProperties.SavedProperty<int>(property.Name, number));
                    break;
                case int[] numbers:
                    (saved.intArrays ??= []).Add(new SavedProperties.SavedProperty<int[]>(property.Name, numbers));
                    break;
                case Enum enumValue:
                    (saved.ints ??= []).Add(new SavedProperties.SavedProperty<int>(property.Name, Convert.ToInt32(enumValue)));
                    break;
                case Enum[] enumValues:
                    (saved.intArrays ??= []).Add(new SavedProperties.SavedProperty<int[]>(
                        property.Name,
                        enumValues.Select(static item => Convert.ToInt32(item)).ToArray()));
                    break;
                case ModelId id:
                    (saved.modelIds ??= []).Add(new SavedProperties.SavedProperty<ModelId>(property.Name, id));
                    break;
                case bool flag:
                    (saved.bools ??= []).Add(new SavedProperties.SavedProperty<bool>(property.Name, flag));
                    break;
                case string text:
                    (saved.strings ??= []).Add(new SavedProperties.SavedProperty<string>(property.Name, text));
                    break;
                case SerializableCard card:
                    (saved.cards ??= []).Add(new SavedProperties.SavedProperty<SerializableCard>(property.Name, card));
                    break;
                case List<SerializableCard> cards:
                    (saved.cardArrays ??= []).Add(new SavedProperties.SavedProperty<SerializableCard[]>(
                        property.Name,
                        cards.ToArray()));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{property.DeclaringType.Name}.{property.Name} has type {value.GetType()}, which SavedProperties cannot hold.");
            }
        }

        return any ? saved : null;
    }

    private static Dictionary<(Type, string), SerializationCondition> Build()
    {
        var conditions = new Dictionary<(Type, string), SerializationCondition>();
        foreach ((Type type, (string[] always, string[] ifNotTypeDefault)) in Declared)
        {
            foreach ((string name, SerializationCondition condition) in always
                         .Select(static name => (name, SerializationCondition.AlwaysSave))
                         .Concat(ifNotTypeDefault.Select(static name => (name, SerializationCondition.SaveIfNotTypeDefault))))
            {
                // 表写错或属性被改名、挪走时立刻失败，不让套件悄悄少存一个字段。不在这里拒绝仍带特性的属性：
                // 数据级 A/B 要用同一个验证程序集去跑去掉特性之前的主模组，那边 From 的结果应与原版 From 相同。
                _ = type.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly)
                    ?? throw new MissingMemberException(type.FullName, name);
                conditions.Add((type, name), condition);
            }
        }

        return conditions;
    }
}
