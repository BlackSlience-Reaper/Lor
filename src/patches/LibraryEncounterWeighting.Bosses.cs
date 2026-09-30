using System;
using System.Linq;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using LorActModel = LibraryOfRuina.content.acts.LibraryOfRuinaActModel;

namespace LibraryOfRuina.patches;

/// <summary>
/// 图书馆幕的 Boss 路由：解放楼层所在的幕在自己的幕序号上固定解放 Boss（高进阶第三幕固定双 Boss），
/// 其余图书馆幕在 Boss 池里按权重优先选本模组 Boss。逐层规则由 <see cref="LiberationFloors"/> 提供。
/// </summary>
internal static partial class LibraryEncounterWeighting
{
    // 固定 Boss 有三个入口，缺一不可：
    // - ActModel.GenerateRooms 之后：开局与 ActLikeIt2 岔路换幕都会重新生成房间，原版在这里随机选 Boss。
    // - ActModel.ValidateRoomsAfterLoad 之后：读档时房间来自存档，旧档里的 Boss 可能不是该幕的固定 Boss。
    // - RunManager.GenerateMap 之前：ActLikeIt2 岔路重建房间后，按它自己的第三幕双 Boss 规则
    //   用 UpFront 另抽第二 Boss，覆盖了这里给的固定第二 Boss；进入地图前再固定一次。
    // ActLikeIt2 的 ForcedBossOrder 只在 GenerateRooms 之后设第一 Boss、跳过本局已打过的 Boss、不受怪物扩展开关控制，
    // 不能替代这三个入口。

    internal static void AfterActRoomsGenerated(ActModel act, Rng rng)
    {
        if (act is not LorActModel)
        {
            return;
        }

        ReweightGeneratedRoomSet(act, rng);

        int actIndex = IndexInRun(CurrentRun.State, act);
        if (actIndex >= 0)
        {
            ForceHistoryFloorFirstActBoss(act, actIndex);
        }
    }

    internal static void AfterRunRoomsGenerated(RunState? state)
    {
        ReweightGeneratedRoomSets(state);
        ReweightBosses(state);
    }

    internal static void AfterRoomsValidatedAfterLoad(ActModel act)
    {
        if (act is not LorActModel)
        {
            return;
        }

        int actIndex = IndexInRun(CurrentRun.State, act);
        if (actIndex >= 0)
        {
            ForceHistoryFloorFirstActBoss(act, actIndex);
        }
    }

    internal static void BeforeMapGenerated(RunState? state)
    {
        if (state?.Act is not LorActModel libraryAct)
        {
            return;
        }

        ForceHistoryFloorFirstActBoss(
            libraryAct,
            state.CurrentActIndex);
    }

    // 按引用找幕在本局里的序号；不在本局（独立生成的幕）返回 -1。
    private static int IndexInRun(RunState? state, ActModel act)
    {
        if (state == null)
        {
            return -1;
        }

        for (int i = 0; i < state.Acts.Count; i++)
        {
            if (ReferenceEquals(state.Acts[i], act))
            {
                return i;
            }
        }

        return -1;
    }

    private const int DoubleBossActIndex = 2;

    public static void ReweightBosses(RunState? state)
    {
        if (state == null || !LibraryRunSettings.IsMonsterExtensionEnabled(state))
        {
            return;
        }

        for (int actIndex = 0; actIndex < state.Acts.Count; actIndex++)
        {
            ActModel act = state.Acts[actIndex];
            if (!IsOwnedAct(act))
            {
                continue;
            }

            Rng localRng = new(state.Rng.Seed, $"library_encounter_weight_boss_{actIndex}_{act.Id.Entry}");

            if (IsLiberationBossTarget(act, actIndex))
            {
                ForceHistoryFloorFirstActBoss(act, actIndex);
                LogBossSelection(act);
                continue;
            }

            EncounterModel? boss = PickModFirstIfModEncountersExist(act.AllBossEncounters, localRng);
            if (boss != null)
            {
                act.SetBossEncounter(boss);
            }

            if (act.SecondBossEncounter != null)
            {
                EncounterModel? secondBoss = PickModFirst(
                    act.AllBossEncounters.Where(encounter => encounter.Id != act.BossEncounter.Id),
                    localRng);
                if (secondBoss != null)
                {
                    act.SetSecondBossEncounter(secondBoss);
                }
            }

            LogBossSelection(act);
        }
    }

    public static void ForceHistoryFloorFirstActBoss(RunState? state)
    {
        if (state == null
            || !LibraryRunSettings.IsMonsterExtensionEnabled(state)
            || state.Acts.Count == 0)
        {
            return;
        }

        for (int i = 0; i < state.Acts.Count; i++)
        {
            if (IsOwnedAct(state.Acts[i]))
            {
                ForceHistoryFloorFirstActBoss(state.Acts[i], i);
            }
        }
    }

    internal static bool ShouldApplyThirdActDoubleBossRule(
        int actIndex,
        int ascensionLevel) =>
        actIndex == DoubleBossActIndex
        && ascensionLevel >= (int)AscensionLevel.DoubleBoss;

    /// <summary>
    /// 从已登记的第三幕解放遭遇里按种子洗牌选出双 Boss；不足两个时返回空。运行期路由不用它
    /// （第三幕的两场由 <see cref="LiberationFloorDescriptor.DoubleBossSecondEncounter"/> 固定），
    /// 只有验证套件调用。
    /// </summary>
    internal static IReadOnlyList<EncounterModel>
        ChooseThirdActDoubleBossSelection(
            ulong seed,
            string label)
    {
        List<EncounterModel> candidates = LiberationFloors.ThirdActDoubleBossCandidates
            .Where(static descriptor => descriptor.Registered)
            .Select(static descriptor => descriptor.Encounter)
            .ToList();

        if (candidates.Count < 2)
        {
            return Array.Empty<EncounterModel>();
        }

        Rng selectionRng = new(seed, label);
        selectionRng.Shuffle(candidates);
        return candidates.Take(2).ToArray();
    }

    // 图书馆幕只在自己楼层的幕序号上固定解放 Boss；放到别的幕序号上走普通 Boss 重排。
    private static bool IsLiberationBossTarget(ActModel act, int actIndex) =>
        LiberationFloors.ForAct(act)?.BossActIndex == actIndex;

    internal static void ForceHistoryFloorFirstActBoss(ActModel act, int actIndex)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !IsOwnedAct(act)
            || !LiberationBossRegistry.AnyLiberationRegistered)
        {
            return;
        }

        if (!IsLiberationBossTarget(act, actIndex))
        {
            Log.Info($"{LogPrefix} Skip: act={act.Id.Entry} index={actIndex} type={act.GetType().Name} NOT_TARGET");
            return;
        }

        Log.Info($"{LogPrefix} ForceBoss start: act={act.Id.Entry} index={actIndex}");

        // IsOwnedAct 已保证是图书馆幕；这里只取出类型。
        if (act is not LorActModel libraryAct)
        {
            return;
        }

        EncounterModel expectedBoss = libraryAct.ExpectedBoss;
        if (act.BossEncounter.Id != expectedBoss.Id)
        {
            act.SetBossEncounter(expectedBoss);
        }

        EncounterModel? expectedSecondBoss = ResolveLibraryActSecondBoss(
            libraryAct,
            actIndex);
        if (act.SecondBossEncounter?.Id != expectedSecondBoss?.Id)
        {
            act.SetSecondBossEncounter(expectedSecondBoss);
        }

        Log.Info(
            $"{LogPrefix} {act.Id.Entry}: fixed boss {FormatEncounter(expectedBoss)}, "
            + $"secondBoss {(expectedSecondBoss == null ? "none" : FormatEncounter(expectedSecondBoss))}.");
    }

    internal static EncounterModel? ChooseLiberationEncounter(ActModel act, int actIndex)
    {
        if (act is not LorActModel libraryAct)
        {
            return null;
        }

        return libraryAct.ExpectedBoss;
    }

    private static EncounterModel? ResolveLibraryActSecondBoss(
        LorActModel act,
        int actIndex)
    {
        RunState? state = CurrentRun.State;
        EncounterModel? secondBoss = LiberationFloors.ForAct(act)?.DoubleBossSecondEncounter;
        if (secondBoss == null
            || state == null
            || !ShouldApplyThirdActDoubleBossRule(
                actIndex,
                state.AscensionLevel))
        {
            return null;
        }

        return secondBoss;
    }

    private static EncounterModel? PickModFirstIfModEncountersExist(
        IEnumerable<EncounterModel> pool,
        Rng rng)
    {
        List<EncounterModel> candidates = DistinctById(pool);
        List<EncounterModel> modCandidates = candidates.Where(IsModEncounter).ToList();
        if (modCandidates.Count == 0)
        {
            return null;
        }

        return PickWeighted(modCandidates, rng);
    }

    private static EncounterModel? PickModFirst(
        IEnumerable<EncounterModel> pool,
        Rng rng)
    {
        List<EncounterModel> candidates = DistinctById(pool);
        if (candidates.Count == 0)
        {
            return null;
        }

        List<EncounterModel> modCandidates = candidates.Where(IsModEncounter).ToList();
        return PickWeighted(modCandidates.Count > 0 ? modCandidates : candidates, rng);
    }

    private static void LogBossSelection(ActModel act)
    {
        string boss = FormatEncounter(act.BossEncounter);
        string secondBoss = act.SecondBossEncounter == null ? "none" : FormatEncounter(act.SecondBossEncounter);
        Log.Info($"{LogPrefix} {act.Id.Entry}: boss {boss}, secondBoss {secondBoss}");
    }
}
