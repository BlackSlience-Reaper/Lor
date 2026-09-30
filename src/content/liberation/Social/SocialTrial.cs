using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// 社会层解放战一个试炼的规则：布置、敌方回合结束时的推进与判定、离开时的清理、读档后的恢复。
/// <para>
/// 策略只有行为没有状态，每个试炼一个单例。试炼状态（包括全部存档字段）仍在 <see cref="SocialFloorLiberationEncounter"/> 上，
/// 回合边界的调用顺序、存读档和试炼之间共用的步骤（同步试炼能力、强制计划行动、刷新选曲、恢复勇气与懦弱）也仍由遭遇负责；
/// 遭遇在原来按 <c>Trial</c> 分支的位置取当前试炼的策略并调用对应方法，每次都按调用时的 <c>Trial</c> 重新取。
/// </para>
/// <para>
/// 不继承 <c>AbstractModel</c>：非抽象的模型子类会被 ModelDb 登记成新的模型 ID。
/// </para>
/// </summary>
internal abstract class SocialTrial
{
    private static readonly SocialTrial InitialTrial = new SocialTrialInitial();
    private static readonly SocialTrial WoodsmanTrial = new SocialTrialWoodsman();
    private static readonly SocialTrial ScarecrowTrial = new SocialTrialScarecrow();
    private static readonly SocialTrial LionTrial = new SocialTrialLion();
    private static readonly SocialTrial HomeTrial = new SocialTrialHome();
    private static readonly SocialTrial RageTrial = new SocialTrialRage();
    private static readonly SocialTrial UndefinedTrial = new Undefined();

    internal static SocialTrial For(SocialFloorTrial trial) => trial switch
    {
        SocialFloorTrial.Initial => InitialTrial,
        SocialFloorTrial.Woodsman => WoodsmanTrial,
        SocialFloorTrial.Scarecrow => ScarecrowTrial,
        SocialFloorTrial.Lion => LionTrial,
        SocialFloorTrial.Home => HomeTrial,
        SocialFloorTrial.Rage => RageTrial,
        _ => UndefinedTrial
    };

    /// <summary>进入本试炼时（回合数为 0），或读档缺少计划行动键时按存档回合数回退的行动。</summary>
    internal abstract FalseThroneMove ResolveDefaultMove(int round);

    /// <summary>
    /// 房间按存档重建、布置已经完成时，由 <c>GenerateMonsters</c> 按摧毁掩码补上的召唤物。
    /// 这时还不能发战斗命令，只能往生成列表里加模型。
    /// </summary>
    internal virtual void AddRoomSummons(
        SocialFloorLiberationEncounter encounter,
        List<(MonsterModel, string?)> monsters)
    {
    }

    /// <summary>
    /// 第一次布置本试炼。调用前 <c>SetupComplete</c> 已置为 true；之后遭遇同步试炼能力、强制计划行动并刷新阶段选曲。
    /// </summary>
    internal virtual Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>布置已经完成时的重入（敌方回合开始、读档后开战），在补召唤物之前。</summary>
    internal virtual Task ResumeAsync(FalseThrone boss) => Task.CompletedTask;

    /// <summary>战斗进行中补上缺失的召唤物；遭遇已确认战斗仍在进行。</summary>
    internal virtual Task SpawnMissingSummonsAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>敌方回合开始时，已排入的下一试炼是否留到本回合结束再进入。</summary>
    internal virtual bool DefersPendingTrialToEnemyTurnEnd(
        SocialFloorLiberationEncounter encounter) => false;

    /// <summary>敌方回合结束，<c>TrialRound</c> 已经加一：判定是否进入下一试炼，否则决定下一回合的计划行动。</summary>
    internal virtual Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>玩家回合开始，读档恢复之后。</summary>
    internal virtual Task BeforePlayerTurnAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>
    /// 离开本试炼。调用时遭遇的 <c>Trial</c> 已经切到下一试炼，按当前试炼生效的限制（例如樵夫试炼的能量上限）已经解除。
    /// </summary>
    internal virtual Task CleanupAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>读档后第一个玩家回合：遭遇恢复勇气与懦弱之前。</summary>
    internal virtual Task RestoreBeforeSharedStateAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>读档后第一个玩家回合：遭遇恢复勇气与懦弱之后。</summary>
    internal virtual Task RestoreAfterSharedStateAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState) => Task.CompletedTask;

    /// <summary>玩家死亡且没有被阻止移出（<c>FalseThrone.AfterDeath</c>）。</summary>
    internal virtual Task OnPlayerDiedAsync(
        SocialFloorLiberationEncounter encounter,
        Player player,
        ulong netId) => Task.CompletedTask;

    protected static async Task RemoveTrialSummons<T>(
        CombatStateLike combatState) where T : MonsterModel
    {
        foreach (Creature summon in combatState.Enemies
                     .Where(static enemy => enemy.Monster is T)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                summon,
                combatState);
        }

        combatState.SortEnemiesBySlotName();
    }

    /// <summary>
    /// 枚举之外的值：原来按 <c>Trial</c> 分支的各处对它什么都不做，默认行动是初始序列。
    /// 读档和排入试炼都只写入已定义的值，这里只为保留同样的回退。
    /// </summary>
    private sealed class Undefined : SocialTrial
    {
        internal override FalseThroneMove ResolveDefaultMove(int round) =>
            FalseThroneMove.InitialSequence;
    }
}
