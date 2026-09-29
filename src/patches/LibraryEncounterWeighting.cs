using System;
using System.Linq;
using System.Reflection;
using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.abnormalities.BigBird;
using LibraryOfRuina.content.abnormalities.BlueStar;
using LibraryOfRuina.content.abnormalities.DeadButterfly;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.ForsakenMurderer;
using LibraryOfRuina.content.abnormalities.GalaxyChild;
using LibraryOfRuina.content.abnormalities.HappyTeddy;
using LibraryOfRuina.content.abnormalities.JudgementBird;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.content.abnormalities.Ozma;
using LibraryOfRuina.content.abnormalities.PriceOfSilence;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.content.abnormalities.SmilingBodies;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.abnormalities.SpinyBus;
using LibraryOfRuina.content.abnormalities.TodaysShyLook;
using LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.guests.WedgeOffice;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.core;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using LorActModel = LibraryOfRuina.content.acts.LibraryOfRuinaActModel;

namespace LibraryOfRuina.patches;

/// <summary>
/// 图书馆幕的遭遇排程：遭遇池复制份数、抽取权重、开局与岔路重建时的房间序列、运行期把原版遭遇换成本模组遭遇、
/// 同池循环规则，以及关闭怪物扩展后恢复原版遭遇。Boss 路由在 <c>LibraryEncounterWeighting.Bosses.cs</c>，
/// 解放楼层的逐层规则在 <see cref="LiberationFloors"/>。
/// 所有抽取都用新建的局部随机源（本局种子加标签，或由调用方随机源的状态派生），不消耗原版共享的随机流；
/// 标签字符串和抽取次序决定结果，改动会改变局面，联机两端也会分叉。
/// </summary>
internal static partial class LibraryEncounterWeighting
{
    private const int PriorityEncounterPoolCopies = 25;
    private const int StandardModEncounterPoolCopies = 2;
    private const double PriorityEncounterSelectionWeight = 8.5;
    private const double StandardModEncounterSelectionWeight = 1.0;
    private const double VanillaEncounterSelectionWeight = 0.5;

    private const string LogPrefix = "[LibraryEncounterWeight]";

    private static readonly Assembly ModAssembly = typeof(LibraryOfRuinaInitializer).Assembly;

    private static readonly HashSet<Type> PriorityAbnormalityEncounterTypes = new()
    {
        typeof(ScorchedGirl),
        typeof(LeticiaElite),
        typeof(HappyTeddyWeak),
        typeof(ForsakenMurdererWeak),
        typeof(AddictedEmployeeWeak),
        typeof(AddictedEmployeeStrong),
        typeof(QueenOfHatredStrong),
        typeof(KingOfGreedElite),
        typeof(AllAroundHelperWeak),
        typeof(AllAroundHelperStrong),
        typeof(TodaysShyLookStrong),
        typeof(FairyFestivalStrong),
        typeof(RedShoesStrong),
        typeof(SpiderBudStrong),
        typeof(DeadButterflyWeak),
        typeof(DeadButterflyStrong),
        typeof(GalaxyChildWeak),
        typeof(SpinyBusWeak),
        typeof(BigBadWolfWeak),
        typeof(LittleRedMercenaryElite),
        typeof(SmilingBodiesStrong),
        typeof(NosferatuElite),
        typeof(RedMistElite),
        typeof(ScarecrowSearchingForWisdomWeak),
        typeof(WarmheartedWoodsmanStrong),
        typeof(PriceOfSilenceStrong),
        typeof(BlueStarStrong),
        typeof(BigBirdStrong),
        typeof(PunishingBirdStrong),
        typeof(JudgementBirdElite),
        typeof(RoadHomeElite),
        typeof(OzmaElite)
    };

    public static void AddWeightedCopies<TEncounter>(ICollection<EncounterModel> destination)
        where TEncounter : EncounterModel
    {
        AddWeightedCopies(destination, ModelDb.Encounter<TEncounter>());
    }

    public static void AddWeightedCopies(ICollection<EncounterModel> destination, EncounterModel encounter)
    {
        if (encounter is IGuestReceptionEncounter)
        {
            destination.Add(encounter);
            return;
        }

        int copies = PoolCopiesFor(encounter);
        for (int i = 0; i < copies; i++)
        {
            destination.Add(encounter);
        }
    }

    public static bool IsPriorityEncounter(EncounterModel encounter)
        => IsAbnormalityEncounter(encounter);

    private static bool IsAbnormalityEncounter(EncounterModel encounter)
    {
        if (encounter is IGuestReceptionEncounter)
        {
            return false;
        }

        Type encounterType = encounter.GetType();
        if (PriorityAbnormalityEncounterTypes.Contains(encounterType))
        {
            return true;
        }

        try
        {
            return encounter.AllPossibleMonsters.Any(static monster => monster is LibraryMonsterModel);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsStandardModEncounter(EncounterModel encounter) =>
        IsModEncounter(encounter) && !IsPriorityEncounter(encounter);

    public static bool IsModEncounter(EncounterModel encounter) =>
        encounter.GetType().Assembly == ModAssembly;

    internal static bool IsNormalEncounterPoolCandidate(EncounterModel encounter) =>
        !IsModEncounter(encounter) || IsAbnormalityEncounter(encounter);

    private static bool IsWeightedModEncounter(EncounterModel encounter) =>
        IsModEncounter(encounter) && IsAbnormalityEncounter(encounter);

    private static bool IsOwnedAct(ActModel? act) =>
        LorActModel.IsLibraryAct(act);

    public static void ReweightGeneratedRoomSet(ActModel act, Rng sourceRng)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !IsOwnedAct(act))
        {
            return;
        }

        RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
        Rng localRng = CreateLocalRng(sourceRng, act, "rooms");

        int weakCount = rooms.normalEncounters.Count(static encounter => encounter.IsWeak);
        int totalNormalCount = rooms.normalEncounters.Count;

        var scheduledNormalIds = new HashSet<ModelId>();
        var normalEncounters = new List<EncounterModel>(totalNormalCount);
        AppendModFirstSequence(
            normalEncounters,
            act.AllWeakEncounters.Where(IsNormalEncounterPoolCandidate),
            weakCount,
            localRng,
            recordSelectedIds: scheduledNormalIds,
            act: act);
        AppendModFirstSequence(
            normalEncounters,
            act.AllRegularEncounters.Where(IsNormalEncounterPoolCandidate),
            totalNormalCount - normalEncounters.Count,
            localRng,
            skipFirstCycleIds: scheduledNormalIds,
            recordSelectedIds: scheduledNormalIds,
            act: act);

        if (normalEncounters.Count == totalNormalCount)
        {
            rooms.normalEncounters.Clear();
            rooms.normalEncounters.AddRange(normalEncounters);
        }

        var eliteEncounters = BuildModFirstSequence(
            act.AllEliteEncounters.Where(IsNormalEncounterPoolCandidate),
            rooms.eliteEncounters.Count,
            localRng,
            act);
        if (eliteEncounters.Count == rooms.eliteEncounters.Count)
        {
            rooms.eliteEncounters.Clear();
            rooms.eliteEncounters.AddRange(eliteEncounters);
        }

        ReapplyExistingEncounterRules(act);
        LogGeneratedRoomSet(act, rooms);
    }

    public static bool TrySelectRuntimeModReplacement(
        ActModel act,
        RoomType roomType,
        EncounterModel current,
        out EncounterModel replacement)
    {
        replacement = current;
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !IsOwnedAct(act)
            || (IsModEncounter(current) && current is not IGuestReceptionEncounter)
            || roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
        {
            return false;
        }

        RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
        IReadOnlyList<EncounterModel> candidates = GetRuntimeReplacementCandidates(act, roomType, current);
        if (candidates.Count == 0)
        {
            return false;
        }

        (EncounterModel? previous, EncounterModel? next, int index) = GetRuntimeReplacementContext(rooms, roomType);
        if (index >= candidates.Count)
        {
            return false;
        }

        List<EncounterModel> preferredCandidates = candidates
            .Where(candidate =>
                !SharesId(candidate, previous)
                && !SharesId(candidate, next)
                && !candidate.SharesTagsWith(previous)
                && !candidate.SharesTagsWith(next))
            .ToList();

        ulong seed = RunManager.Instance.DebugOnlyGetState()?.Rng.Seed
            ?? StringHelper.GetDeterministicHashCode(act.Id.Entry);
        Rng localRng = new(seed, $"library_encounter_runtime_replace_{act.Id.Entry}_{roomType}_{index}_{current.Id.Entry}");
        IReadOnlyList<EncounterModel> selectionPool = preferredCandidates.Count > index
            ? preferredCandidates
            : candidates;
        replacement = PickRuntimeReplacementAtIndex(selectionPool, index, localRng, act);
        Log.Info($"{LogPrefix} Runtime replaced vanilla {FormatEncounter(current)} with {FormatEncounter(replacement)}.");
        return true;
    }

    public static void ReweightGeneratedRoomSets(RunState? state)
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

            // 开局和岔路重建共用种子规则；本方法不消耗 UpFront。
            ReweightGeneratedRoomSet(act, state.Rng.UpFront);
        }
    }

    public static void RestoreVanillaEncounters(RunState? state)
    {
        if (state == null)
        {
            return;
        }

        foreach (ActModel act in state.Acts)
        {
            if (!IsOwnedAct(act))
            {
                continue;
            }

            List<EncounterModel> vanillaEncounters = act.GenerateAllEncounters()
                .Where(encounter => !IsModEncounter(encounter))
                .DistinctBy(encounter => encounter.Id)
                .ToList();

            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            bool changed = RestoreEncounterSequence(
                rooms.normalEncounters,
                vanillaEncounters.Where(encounter => encounter.RoomType == RoomType.Monster).ToList());
            changed |= RestoreEncounterSequence(
                rooms.eliteEncounters,
                vanillaEncounters.Where(encounter => encounter.RoomType == RoomType.Elite).ToList());

            List<EncounterModel> vanillaBosses = vanillaEncounters
                .Where(encounter => encounter.RoomType == RoomType.Boss)
                .ToList();
            EncounterModel restoredBoss = ChooseVanillaReplacement(rooms.Boss, vanillaBosses, 0);
            changed |= restoredBoss.Id != rooms.Boss.Id;
            rooms.Boss = restoredBoss;
            if (rooms.SecondBoss != null)
            {
                EncounterModel restoredSecondBoss = ChooseVanillaReplacement(rooms.SecondBoss, vanillaBosses, 1, rooms.Boss);
                changed |= restoredSecondBoss.Id != rooms.SecondBoss.Id;
                rooms.SecondBoss = restoredSecondBoss;
            }

            if (changed)
            {
                Log.Info($"{LogPrefix} Restored vanilla encounter schedule for {act.Id.Entry} after injection was disabled.");
            }
        }
    }

    public static bool TryGetScheduledVanillaEncounter(
        ActModel act,
        RoomType roomType,
        out EncounterModel encounter)
    {
        if (!IsOwnedAct(act))
        {
            encounter = null!;
            return false;
        }

        RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
        encounter = roomType switch
        {
            RoomType.Monster when rooms.normalEncounters.Count > 0 => rooms.NextNormalEncounter,
            RoomType.Elite when rooms.eliteEncounters.Count > 0 => rooms.NextEliteEncounter,
            RoomType.Boss => rooms.NextBossEncounter,
            _ => null!
        };

        return encounter != null && !IsModEncounter(encounter);
    }

    private static int PoolCopiesFor(EncounterModel encounter)
    {
        if (IsPriorityEncounter(encounter))
        {
            return PriorityEncounterPoolCopies;
        }

        return IsModEncounter(encounter) ? StandardModEncounterPoolCopies : 1;
    }

    private static bool RestoreEncounterSequence(
        IList<EncounterModel> sequence,
        IReadOnlyList<EncounterModel> vanillaCandidates)
    {
        if (vanillaCandidates.Count == 0)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < sequence.Count; i++)
        {
            if (!IsModEncounter(sequence[i]))
            {
                continue;
            }

            IReadOnlyList<EncounterModel> matchingCandidates = vanillaCandidates
                .Where(candidate => candidate.IsWeak == sequence[i].IsWeak)
                .ToList();
            sequence[i] = ChooseVanillaReplacement(
                sequence[i],
                matchingCandidates.Count > 0 ? matchingCandidates : vanillaCandidates,
                i,
                i > 0 ? sequence[i - 1] : null);
            changed = true;
        }

        return changed;
    }

    private static EncounterModel ChooseVanillaReplacement(
        EncounterModel current,
        IReadOnlyList<EncounterModel> candidates,
        int index,
        EncounterModel? excluded = null)
    {
        if (!IsModEncounter(current) || candidates.Count == 0)
        {
            return current;
        }

        for (int offset = 0; offset < candidates.Count; offset++)
        {
            EncounterModel candidate = candidates[(index + offset) % candidates.Count];
            if (excluded == null || candidate.Id != excluded.Id)
            {
                return candidate;
            }
        }

        return candidates[index % candidates.Count];
    }

    public static EncounterModel? SelectModFirstReplacement(
        IEnumerable<EncounterModel> pool,
        IReadOnlyList<EncounterModel> scheduledEncounters,
        int replacementIndex,
        Func<EncounterModel, bool> isExcluded)
    {
        List<EncounterModel> candidates = DistinctById(pool)
            .Where(IsNormalEncounterPoolCandidate)
            .Where(encounter => !isExcluded(encounter))
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        EncounterModel? previous = replacementIndex > 0 && replacementIndex - 1 < scheduledEncounters.Count
            ? scheduledEncounters[replacementIndex - 1]
            : null;
        EncounterModel? next = replacementIndex + 1 < scheduledEncounters.Count
            ? scheduledEncounters[replacementIndex + 1]
            : null;

        var visitedIds = new HashSet<ModelId>(scheduledEncounters
            .Take(Math.Clamp(replacementIndex, 0, scheduledEncounters.Count))
            .Select(static encounter => encounter.Id));
        var scheduledIds = new HashSet<ModelId>(scheduledEncounters.Select(static encounter => encounter.Id));
        return SelectReplacementCandidate(
                candidates.Where(encounter => IsModEncounter(encounter) && !visitedIds.Contains(encounter.Id)),
                previous,
                next)
            ?? SelectReplacementCandidate(
                candidates.Where(encounter => !IsModEncounter(encounter) && !visitedIds.Contains(encounter.Id)),
                previous,
                next)
            ?? SelectReplacementCandidate(
                candidates.Where(encounter => IsModEncounter(encounter) && !scheduledIds.Contains(encounter.Id)),
                previous,
                next)
            ?? SelectReplacementCandidate(
                candidates.Where(encounter => !IsModEncounter(encounter) && !scheduledIds.Contains(encounter.Id)),
                previous,
                next)
            ?? SelectReplacementCandidate(candidates.Where(IsModEncounter), previous, next)
            ?? SelectReplacementCandidate(candidates.Where(encounter => !IsModEncounter(encounter)), previous, next)
            ?? candidates[0];
    }

    public static EncounterModel ApplyEncounterCycleRule(
        ActModel act,
        RoomType roomType,
        EncounterModel current)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !IsOwnedAct(act)
            || roomType is not (RoomType.Monster or RoomType.Elite))
        {
            return current;
        }

        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state == null
            || state.CurrentActIndex < 0
            || state.CurrentActIndex >= state.Acts.Count
            || !ReferenceEquals(state.Acts[state.CurrentActIndex], act)
            || state.CurrentActIndex >= state.MapPointHistory.Count)
        {
            return current;
        }

        List<EncounterModel> unfilteredCandidates = GetEncounterCyclePool(
            act,
            roomType,
            current);
        if (!unfilteredCandidates.Any(candidate => candidate.Id == current.Id))
        {
            return current;
        }

        List<ModelId> allEncounterHistory = state.MapPointHistory[state.CurrentActIndex]
            .SelectMany(static entry => entry.Rooms)
            .Where(static room =>
                room.RoomType is RoomType.Monster or RoomType.Elite
                && room.ModelId != null)
            .Select(static room => room.ModelId!)
            .ToList();
        var allEncounterHistoryIds = new HashSet<ModelId>(allEncounterHistory);
        List<EncounterModel> candidates = unfilteredCandidates
            .Where(candidate => !IsExcludedByMutualEncounterHistory(
                candidate,
                allEncounterHistoryIds))
            .ToList();
        if (candidates.Count == 0)
        {
            return current;
        }

        var candidateIds = new HashSet<ModelId>(
            candidates.Select(static candidate => candidate.Id));
        List<ModelId> poolHistory = allEncounterHistory
            .Where(candidateIds.Contains)
            .ToList();
        HashSet<ModelId> seenInCurrentCycle = BuildCurrentEncounterCycle(
            poolHistory,
            candidateIds);
        EncounterModel? previous = poolHistory.Count == 0
            ? null
            : candidates.FirstOrDefault(candidate => candidate.Id == poolHistory[^1]);

        List<EncounterModel> unseenCandidates = candidates
            .Where(candidate => !seenInCurrentCycle.Contains(candidate.Id))
            .ToList();
        if (unseenCandidates.Count == 0)
        {
            unseenCandidates.AddRange(candidates);
        }

        List<EncounterModel> preferredCandidates = unseenCandidates
            .Where(candidate => previous == null
                || (candidate.Id != previous.Id
                    && !candidate.SharesTagsWith(previous)))
            .ToList();
        IReadOnlyList<EncounterModel> allowedCandidates = preferredCandidates.Count > 0
            ? preferredCandidates
            : unseenCandidates;
        if (allowedCandidates.Any(candidate => candidate.Id == current.Id))
        {
            return current;
        }

        RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
        IReadOnlyList<EncounterModel> scheduled = roomType == RoomType.Elite
            ? rooms.eliteEncounters
            : rooms.normalEncounters;
        int visited = roomType == RoomType.Elite
            ? rooms.eliteEncountersVisited
            : rooms.normalEncountersVisited;
        EncounterModel replacement = SelectUpcomingAllowedEncounter(
            scheduled,
            visited,
            allowedCandidates)
            ?? allowedCandidates[0];

        Log.Info(
            $"{LogPrefix} Replaced repeated {FormatEncounter(current)} with "
            + $"{FormatEncounter(replacement)} before the {roomType} pool cycle was exhausted "
            + $"({seenInCurrentCycle.Count}/{candidates.Count} seen)." );
        return replacement;
    }

    private static List<EncounterModel> GetEncounterCyclePool(
        ActModel act,
        RoomType roomType,
        EncounterModel current)
    {
        IEnumerable<EncounterModel> pool = roomType switch
        {
            RoomType.Monster when current.IsWeak => act.AllWeakEncounters,
            RoomType.Monster => act.AllRegularEncounters,
            RoomType.Elite => act.AllEliteEncounters,
            _ => Array.Empty<EncounterModel>()
        };
        return DistinctById(pool)
            .Where(IsNormalEncounterPoolCandidate)
            .ToList();
    }

    private static bool IsExcludedByMutualEncounterHistory(
        EncounterModel candidate,
        ISet<ModelId> historyIds)
    {
        return candidate switch
        {
            AddictedEmployeeStrong => historyIds.Contains(
                ModelDb.Encounter<AddictedEmployeeWeak>().Id),
            AddictedEmployeeWeak => historyIds.Contains(
                ModelDb.Encounter<AddictedEmployeeStrong>().Id),
            DeadButterflyStrong => historyIds.Contains(
                ModelDb.Encounter<DeadButterflyWeak>().Id),
            DeadButterflyWeak => historyIds.Contains(
                ModelDb.Encounter<DeadButterflyStrong>().Id),
            AllAroundHelperStrong => historyIds.Contains(
                ModelDb.Encounter<AllAroundHelperWeak>().Id),
            AllAroundHelperWeak => historyIds.Contains(
                ModelDb.Encounter<AllAroundHelperStrong>().Id),
            _ => false
        };
    }

    private static HashSet<ModelId> BuildCurrentEncounterCycle(
        IEnumerable<ModelId> history,
        ISet<ModelId> candidateIds)
    {
        var seen = new HashSet<ModelId>();
        foreach (ModelId encounterId in history)
        {
            if (!candidateIds.Contains(encounterId))
            {
                continue;
            }

            seen.Add(encounterId);
            if (seen.Count == candidateIds.Count)
            {
                seen.Clear();
            }
        }

        return seen;
    }

    private static EncounterModel? SelectUpcomingAllowedEncounter(
        IReadOnlyList<EncounterModel> scheduled,
        int visited,
        IReadOnlyList<EncounterModel> allowedCandidates)
    {
        if (scheduled.Count == 0)
        {
            return null;
        }

        var allowedById = allowedCandidates.ToDictionary(
            static candidate => candidate.Id);
        int currentIndex = visited % scheduled.Count;
        for (int offset = 1; offset <= scheduled.Count; offset++)
        {
            ModelId scheduledId = scheduled[(currentIndex + offset) % scheduled.Count].Id;
            if (allowedById.TryGetValue(scheduledId, out EncounterModel? candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Rng CreateLocalRng(Rng sourceRng, ActModel act, string label)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState != null)
        {
            for (int actIndex = 0; actIndex < runState.Acts.Count; actIndex++)
            {
                if (ReferenceEquals(runState.Acts[actIndex], act))
                {
                    // 房间序列只由共享的本局种子和章节身份决定，避免生成入口、
                    // 调用次数及其他流程对 UpFront 的消耗改变同一章节的遭遇。
                    return new Rng(
                        runState.Rng.Seed,
                        $"library_encounter_weight_{label}_{actIndex}_{act.Id.Entry}");
                }
            }
        }

        // 独立生成且未挂入本局的章节保留调用方提供的确定性随机源。
        var state = sourceRng.ToSerializable();
        ulong baseSeed = state.state0;
        baseSeed = unchecked((baseSeed * 1099511628211UL) ^ state.state1);
        baseSeed = unchecked((baseSeed * 1099511628211UL) ^ state.state2);
        baseSeed = unchecked((baseSeed * 1099511628211UL) ^ state.state3);
        baseSeed = unchecked((baseSeed * 1099511628211UL) ^ (uint)state.counter);
        return new Rng(baseSeed, $"library_encounter_weight_{label}_{act.Id.Entry}");
    }

    private static void AppendModFirstSequence(
        List<EncounterModel> destination,
        IEnumerable<EncounterModel> pool,
        int count,
        Rng rng,
        ISet<ModelId>? skipFirstCycleIds = null,
        ISet<ModelId>? recordSelectedIds = null,
        ActModel? act = null)
    {
        if (count <= 0)
        {
            return;
        }

        List<EncounterModel> source = DistinctById(pool);
        if (source.Count == 0)
        {
            return;
        }

        bool firstCycle = true;
        while (count > 0)
        {
            List<EncounterModel> cycleSource = firstCycle && skipFirstCycleIds != null
                ? source.Where(encounter => !skipFirstCycleIds.Contains(encounter.Id)).ToList()
                : new List<EncounterModel>(source);
            if (cycleSource.Count == 0)
            {
                cycleSource.AddRange(source);
            }

            int added = AppendModFirstCycle(destination, cycleSource, count, rng, recordSelectedIds, act);
            if (added == 0)
            {
                return;
            }

            count -= added;
            firstCycle = false;
        }
    }

    private static List<EncounterModel> BuildModFirstSequence(
        IEnumerable<EncounterModel> pool,
        int count,
        Rng rng,
        ActModel? act)
    {
        var result = new List<EncounterModel>(Math.Max(0, count));
        AppendModFirstSequence(result, pool, count, rng, act: act);
        return result;
    }

    private static int AppendModFirstCycle(
        List<EncounterModel> destination,
        IEnumerable<EncounterModel> source,
        int count,
        Rng rng,
        ISet<ModelId>? recordSelectedIds,
        ActModel? act)
    {
        int startCount = destination.Count;
        List<EncounterModel> modEncounters = source.Where(IsWeightedModEncounter).ToList();
        if (modEncounters.Count > 0)
        {
            AppendBucket(destination, modEncounters, ref count, rng, recordSelectedIds, act);
        }

        if (count > 0)
        {
            List<EncounterModel> vanillaEncounters = source
                .Where(encounter => !IsModEncounter(encounter))
                .ToList();
            AppendBucket(destination, vanillaEncounters, ref count, rng, recordSelectedIds, act);
        }

        return destination.Count - startCount;
    }

    private static IReadOnlyList<EncounterModel> GetRuntimeReplacementCandidates(
        ActModel act,
        RoomType roomType,
        EncounterModel current)
    {
        IEnumerable<EncounterModel> primary = roomType switch
        {
            RoomType.Monster when current.IsWeak => act.AllWeakEncounters,
            RoomType.Monster => act.AllRegularEncounters,
            RoomType.Elite => act.AllEliteEncounters,
            RoomType.Boss => act.AllBossEncounters,
            _ => Enumerable.Empty<EncounterModel>()
        };

        Func<EncounterModel, bool> isEligible = roomType == RoomType.Boss
            ? IsModEncounter
            : IsWeightedModEncounter;
        List<EncounterModel> candidates = DistinctById(primary)
            .Where(isEligible)
            .ToList();
        if (candidates.Count > 0 || roomType != RoomType.Monster)
        {
            return candidates;
        }

        return DistinctById(act.AllWeakEncounters.Concat(act.AllRegularEncounters))
            .Where(IsWeightedModEncounter)
            .ToList();
    }

    private static (EncounterModel? Previous, EncounterModel? Next, int Index) GetRuntimeReplacementContext(
        RoomSet rooms,
        RoomType roomType)
    {
        IReadOnlyList<EncounterModel> scheduled = roomType switch
        {
            RoomType.Monster => rooms.normalEncounters,
            RoomType.Elite => rooms.eliteEncounters,
            _ => Array.Empty<EncounterModel>()
        };
        if (scheduled.Count == 0)
        {
            return (null, null, 0);
        }

        int visited = roomType == RoomType.Elite
            ? rooms.eliteEncountersVisited
            : rooms.normalEncountersVisited;
        int index = visited % scheduled.Count;
        EncounterModel? previous = index > 0 ? scheduled[index - 1] : null;
        EncounterModel? next = index + 1 < scheduled.Count ? scheduled[index + 1] : null;
        return (previous, next, index);
    }

    private static void AppendBucket(
        List<EncounterModel> destination,
        List<EncounterModel> remaining,
        ref int count,
        Rng rng,
        ISet<ModelId>? recordSelectedIds,
        ActModel? act)
    {
        while (count > 0 && remaining.Count > 0)
        {
            EncounterModel? previous = destination.LastOrDefault();
            EncounterModel selected = PickWithoutRepeatingTags(remaining, previous, rng, act);
            remaining.Remove(selected);
            destination.Add(selected);
            recordSelectedIds?.Add(selected.Id);
            count--;
        }
    }

    private static EncounterModel PickWithoutRepeatingTags(
        List<EncounterModel> remaining,
        EncounterModel? previous,
        Rng rng,
        ActModel? act)
    {
        List<EncounterModel> candidates = remaining
            .Where(encounter => previous == null || (encounter != previous && !encounter.SharesTagsWith(previous)))
            .ToList();

        return PickWeighted(candidates.Count > 0 ? candidates : remaining, rng, act);
    }

    private static EncounterModel PickRuntimeReplacementAtIndex(
        IReadOnlyList<EncounterModel> candidates,
        int index,
        Rng rng,
        ActModel? act)
    {
        var remaining = new List<EncounterModel>(candidates);
        var ordered = new List<EncounterModel>(Math.Min(index + 1, candidates.Count));
        while (ordered.Count <= index && remaining.Count > 0)
        {
            EncounterModel? previous = ordered.LastOrDefault();
            EncounterModel selected = PickWithoutRepeatingTags(remaining, previous, rng, act);
            remaining.Remove(selected);
            ordered.Add(selected);
        }

        return ordered[index];
    }

    private static List<EncounterModel> DistinctById(IEnumerable<EncounterModel> pool)
    {
        var result = new List<EncounterModel>();
        var seen = new HashSet<ModelId>();
        foreach (EncounterModel encounter in pool)
        {
            if (encounter != null && seen.Add(encounter.Id))
            {
                result.Add(encounter);
            }
        }

        // 加权抽取依赖列表位置，按模型 ID 固定顺序以隔离注册及枚举顺序差异。
        return result
            .OrderBy(static encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    private static EncounterModel? SelectReplacementCandidate(
        IEnumerable<EncounterModel> candidates,
        EncounterModel? previous,
        EncounterModel? next)
    {
        List<EncounterModel> list = candidates.ToList();
        return list.FirstOrDefault(candidate =>
                !SharesId(candidate, previous)
                && !SharesId(candidate, next)
                && !candidate.SharesTagsWith(previous)
                && !candidate.SharesTagsWith(next))
            ?? list.FirstOrDefault();
    }

    private static bool SharesId(EncounterModel encounter, EncounterModel? other) =>
        other != null && encounter.Id == other.Id;

    private static double SelectionWeightFor(EncounterModel encounter, ActModel? act)
    {
        if (IsPriorityEncounter(encounter))
        {
            double multiplier = 1.0;
            // 图书馆章节使用固定 Boss；生成房间时 Boss 字段可能尚未校正。
            EncounterModel? boss = act is LorActModel libraryAct
                ? libraryAct.ExpectedBoss
                : act?.BossEncounter;
            LiberationFloorDescriptor? floor = LiberationFloors.ForEncounter(boss);
            if (floor != null)
            {
                multiplier = floor.PriorityWeightMultiplierFor(encounter);
            }

            return PriorityEncounterSelectionWeight * multiplier;
        }

        return IsModEncounter(encounter) ? StandardModEncounterSelectionWeight : VanillaEncounterSelectionWeight;
    }

    private static EncounterModel PickWeighted(
        IReadOnlyList<EncounterModel> candidates,
        Rng rng,
        ActModel? act = null)
    {
        double totalWeight = candidates.Sum(candidate => SelectionWeightFor(candidate, act));
        double roll = rng.NextDouble() * totalWeight;

        for (int i = 0; i < candidates.Count; i++)
        {
            EncounterModel candidate = candidates[i];
            roll -= SelectionWeightFor(candidate, act);
            if (roll <= 0.0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static void ReapplyExistingEncounterRules(ActModel act)
    {
        GloryWedgeRoomSequenceNormalizer.Normalize(act, preserveVisitedPrefix: false);
        if (LorActModel.IsFirstFamily(act))
        {
            RoomSet? employeeRooms = AddictedEmployeeEncounterMutualExclusion.GetRoomSet(act);
            if (employeeRooms != null)
            {
                AddictedEmployeeEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(act, employeeRooms);
            }

            RoomSet? butterflyRooms = DeadButterflyEncounterMutualExclusion.GetRoomSet(act);
            if (butterflyRooms != null)
            {
                DeadButterflyEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(act, butterflyRooms);
            }

            return;
        }

        if (LorActModel.IsSecondFamily(act))
        {
            RoomSet? rooms = AllAroundHelperEncounterMutualExclusion.GetRoomSet(act);
            if (rooms != null)
            {
                AllAroundHelperEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(act, rooms);
            }
        }
    }

    private static void LogGeneratedRoomSet(ActModel act, RoomSet rooms)
    {
        Log.Info(
            $"{LogPrefix} {act.Id.Entry}: " +
            $"weak {FormatTierCounts(rooms.normalEncounters.Where(static encounter => encounter.IsWeak))}; " +
            $"regular {FormatTierCounts(rooms.normalEncounters.Where(static encounter => !encounter.IsWeak))}; " +
            $"elite {FormatTierCounts(rooms.eliteEncounters)}");
    }

    private static string FormatTierCounts(IEnumerable<EncounterModel> encounters)
    {
        List<EncounterModel> list = encounters.ToList();
        int priorityCount = list.Count(IsPriorityEncounter);
        int standardCount = list.Count(IsStandardModEncounter);
        int vanillaCount = list.Count - priorityCount - standardCount;

        return $"priority {priorityCount}, standard {standardCount}, vanilla {vanillaCount}, total {list.Count}";
    }

    private static string FormatEncounter(EncounterModel encounter)
    {
        string tier = IsPriorityEncounter(encounter)
            ? "priority"
            : IsStandardModEncounter(encounter)
                ? "standard"
                : "vanilla";
        return $"{encounter.Id.Entry}({tier})";
    }
}
