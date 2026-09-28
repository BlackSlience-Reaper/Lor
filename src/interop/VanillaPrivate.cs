using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace LibraryOfRuina.interop;

/// <summary>
/// 本模组在运行期读写、调用的原版非公开成员，全部集中在这里，按名字对照游戏 0.111.0 核对过。
/// 类型初始化时逐个解析；初始化步骤 VanillaPrivate（<see cref="Report"/>）列出缺失项，调用方拿到“不可用”时
/// 走自己的降级路径。游戏更新后先看这里的汇总，再看原版拷贝守卫。
/// Harmony 补丁的目标（<c>[HarmonyPatch(type, "Name")]</c>、<c>TargetMethod</c>）不在这里，安装失败由 LibraryPatcher 汇总。
/// 标 optional 的只在部分游戏版本存在，属于兼容分支。
/// </summary>
internal static class VanillaPrivate
{
    // 战斗与指令
    internal static readonly VanillaPrivateField<AttackCommand, string> AttackCommandAttackerAnimName = new("_attackerAnimName");
    internal static readonly VanillaPrivateField<AttackCommand, bool> AttackCommandShouldPlayAnimation = new("_shouldPlayAnimation");
    internal static readonly VanillaPrivateField<AttackCommand, Creature> AttackCommandVisualAttacker = new("_visualAttacker");
    internal static readonly VanillaPrivateField<MonsterModel, bool> MonsterModelIsPerformingMove = new("_isPerformingMove");
    internal static readonly VanillaPrivateMethod<CardModel> CardModelOnPlay =
        new("OnPlay", [typeof(PlayerChoiceContext), typeof(CardPlay)]);
    internal static readonly VanillaPrivateProperty<OrbModel, string> OrbModelIconPath = new("IconPath");
    internal static readonly VanillaPrivateMethod<OrbModel> OrbModelPlayEvokeSfx = new("PlayEvokeSfx");
    internal static readonly VanillaPrivateMethod<RelicModel> RelicModelRelicIconChanged = new("RelicIconChanged");
    internal static readonly VanillaPrivateFieldRef<MegaCrit.Sts2.Core.Models.Relics.BeatingRemnant, decimal> BeatingRemnantDamageReceivedThisTurn =
        new("_damageReceivedThisTurn");

    // 怪物招式状态机
    internal static readonly VanillaPrivateProperty<ConditionalBranchState, IEnumerable> ConditionalBranchStateStates = new("States");
    /// <summary>ConditionalBranchState 的私有嵌套结构 ConditionalBranch 的公开字段 id。</summary>
    internal static readonly VanillaPrivateField<object, string> ConditionalBranchId =
        new(AccessTools.Inner(typeof(ConditionalBranchState), "ConditionalBranch"), "id");
    internal static readonly VanillaPrivateField<MonsterMoveStateMachine, MonsterState> MonsterMoveStateMachineInitialState = new("_initialState");
    /// <summary>较旧的游戏版本才有；0.111 没有，意图悬停对条件分支只列出候选、不预判结果。</summary>
    internal static readonly VanillaPrivateMethod<ConditionalBranchState> ConditionalBranchStateEvaluateStates =
        new("EvaluateStates", optional: true);

    // 局与地图
    internal static readonly VanillaPrivateField<ActModel, RoomSet> ActModelRooms = new("_rooms");
    /// <summary>RunState.Modifiers 是只读自动属性，局中追加或临时替换 Modifier 只能写它的后备字段。</summary>
    internal static readonly VanillaPrivateField<RunState, IReadOnlyList<ModifierModel>> RunStateModifiers = new("<Modifiers>k__BackingField");
    internal static readonly VanillaPrivateField<RunState, List<List<MegaCrit.Sts2.Core.Runs.History.MapPointHistoryEntry>>> RunStateMapPointHistory =
        new("_mapPointHistory");
    internal static readonly VanillaPrivateMethod<RunManager> RunManagerClearScreens = new("ClearScreens");
    internal static readonly VanillaPrivateMethod<RunManager> RunManagerExitCurrentRooms = new("ExitCurrentRooms");
    internal static readonly VanillaPrivateMethod<RunManager> RunManagerEnterRoomInternal =
        new("EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)]);
    internal static readonly VanillaPrivateField<NMapScreen, CanvasItem> MapScreenStartingPointNode = new("_startingPointNode");
    internal static readonly VanillaPrivateField<NMapScreen, CanvasItem> MapScreenBossPointNode = new("_bossPointNode");
    internal static readonly VanillaPrivateField<NMapScreen, IDictionary> MapScreenPaths = new("_paths");
    internal static readonly VanillaPrivateField<MerchantEntry, Player> MerchantEntryPlayer = new("_player");

    // 奖励
    internal static readonly VanillaPrivateField<RelicReward, RelicModel> RelicRewardRelic = new("_relic");
    internal static readonly VanillaPrivateField<RelicReward, bool> RelicRewardWasTaken = new("_wasTaken");
    /// <summary>公开属性 ClaimedRelic 的私有 setter。</summary>
    internal static readonly VanillaPrivateProperty<RelicReward, RelicModel> RelicRewardClaimedRelic = new(nameof(RelicReward.ClaimedRelic));
    internal static readonly VanillaPrivateField<NRewardsScreen, RewardsSet> RewardsScreenRewardsSet = new("_rewardsSet");

    // 战斗界面
    internal static readonly VanillaPrivateField<NCombatUi, CombatState> CombatUiState = new("_state");
    internal static readonly VanillaPrivateField<NCombatUi, Control> CombatUiCombatPilesContainer = new("_combatPilesContainer");
    internal static readonly VanillaPrivateMethod<NCombatUi> CombatUiShowRewards = new("ShowRewards", [typeof(CombatRoom)]);
    internal static readonly VanillaPrivateProperty<NCombatRoom, Control> CombatRoomEncounterSlots = new("EncounterSlots");
    internal static readonly VanillaPrivateField<NCreature, CreatureAnimator> CreatureSpineAnimator = new("_spineAnimator");
    internal static readonly VanillaPrivateField<NCreatureStateDisplay, Vector2> CreatureStateDisplayOriginalPosition = new("_originalPosition");
    internal static readonly VanillaPrivateField<NHealthBar, Control> HealthBarBlockContainer = new("_blockContainer");
    internal static readonly VanillaPrivateField<NHealthBar, Control> HealthBarBlockLabel = new("_blockLabel");
    internal static readonly VanillaPrivateField<NHealthBar, Creature> HealthBarCreature = new("_creature");
    internal static readonly VanillaPrivateField<NPower, PowerModel> PowerNodeModel = new("_model");
    internal static readonly VanillaPrivateProperty<NTargetManager, Node> TargetManagerHoveredNode = new("HoveredNode");
    internal static readonly VanillaPrivateFieldRef<NPlayerHand, NCardPlay?> PlayerHandCurrentCardPlay = new("_currentCardPlay");
    internal static readonly VanillaPrivateFieldRef<NCreature, bool> CreatureIsInMultiselect = new("_isInMultiselect");
    internal static readonly VanillaPrivateFieldRef<NCreature, NCreatureStateDisplay> CreatureStateDisplay = new("_stateDisplay");
    internal static readonly VanillaPrivateFieldRef<NCreatureStateDisplay, NHealthBar> CreatureStateDisplayHealthBar = new("_healthBar");
    internal static readonly VanillaPrivateField<NEnergyCounter, Node> EnergyCounterBackVfx = new("_backVfx", optional: true);
    /// <summary>较旧的游戏版本里能量球背景粒子叫 _backParticles（CPU 粒子），0.111 改成 _backVfx。</summary>
    internal static readonly VanillaPrivateField<NEnergyCounter, Node> EnergyCounterBackParticles = new("_backParticles", optional: true);
    internal static readonly VanillaPrivateField<NParticlesContainer, Godot.Collections.Array<GpuParticles2D>> ParticlesContainerParticles =
        new("_particles");
    internal static readonly VanillaPrivateField<NCard, Control> CardTypePlaque = new("_typePlaque");
    internal static readonly VanillaPrivateField<NCard, Control> CardTypeLabel = new("_typeLabel");

    // 悬停提示
    internal static readonly VanillaPrivateStaticField<NHoverTipSet, IDictionary> HoverTipSetActiveHoverTips = new("_activeHoverTips");
    internal static readonly VanillaPrivateField<NHoverTipSet, Control> HoverTipSetOwner = new("_owner");
    internal static readonly VanillaPrivateField<NHoverTipSet, Control> HoverTipSetTextHoverTipContainer = new("_textHoverTipContainer");

    // 顶栏、选角、结算、图鉴
    internal static readonly VanillaPrivateField<NTopBar, Control> TopBarModifiersContainer = new("_modifiersContainer");
    internal static readonly VanillaPrivateField<NTopBar, Control> TopBarAscensionIcon = new("_ascensionIcon");
    internal static readonly VanillaPrivateField<NTopBarPortraitTip, IHoverTip> TopBarPortraitTipHoverTip = new("_hoverTip");
    /// <summary>公开属性 ShowTip 的私有 setter（旧版本是字段 _showTip）。</summary>
    internal static readonly VanillaPrivateProperty<NTopBarPortraitTip, bool> TopBarPortraitTipShowTip = new(nameof(NTopBarPortraitTip.ShowTip));
    internal static readonly VanillaPrivateField<NAscensionPanel, int> AscensionPanelMaxAscension = new("_maxAscension");
    internal static readonly VanillaPrivateField<NAscensionPanel, bool> AscensionPanelArrowsVisible = new("_arrowsVisible");
    internal static readonly VanillaPrivateField<NAscensionPanel, ShaderMaterial> AscensionPanelIconHsv = new("_iconHsv");
    internal static readonly VanillaPrivateField<NAscensionPanel, Label> AscensionPanelAscensionLevel = new("_ascensionLevel");
    internal static readonly VanillaPrivateField<NAscensionPanel, RichTextLabel> AscensionPanelInfo = new("_info");
    internal static readonly VanillaPrivateMethod<NAscensionPanel> AscensionPanelIncrementAscension = new("IncrementAscension");
    internal static readonly VanillaPrivateMethod<NAscensionPanel> AscensionPanelDecrementAscension = new("DecrementAscension");
    internal static readonly VanillaPrivateField<NGameOverScreen, GridContainer> GameOverScreenScoreLineContainer = new("_scoreLineContainer");
    internal static readonly VanillaPrivateField<NGameOverScreen, IList> GameOverScreenScoreLines = new("_scoreLines");
    internal static readonly VanillaPrivateField<NChooseACardSelectionScreen, IReadOnlyList<CardModel>> ChooseACardScreenCards = new("_cards");
    internal static readonly VanillaPrivateField<NCardLibrary, NCardPoolFilter> CardLibraryColorlessFilter = new("_colorlessFilter");
    internal static readonly VanillaPrivateField<NCardLibrary, Dictionary<NCardPoolFilter, Func<CardModel, bool>>> CardLibraryPoolFilters =
        new("_poolFilters");
    internal static readonly VanillaPrivateField<NCardLibrary, Control> CardLibraryLastHoveredControl = new("_lastHoveredControl");
    internal static readonly VanillaPrivateMethod<NCardLibrary> CardLibraryUpdateCardPoolFilter = new("UpdateCardPoolFilter");
    internal static readonly VanillaPrivateMethod<NBestiary> BestiaryAddEntries = new("AddEntries");

    // 主菜单与设置
    internal static readonly VanillaPrivateField<NPatchNotesScreen, List<string>> PatchNotesScreenPatchNotePaths = new("_patchNotePaths");
    internal static readonly VanillaPrivateField<NPatchNotesScreen, int> PatchNotesScreenIndex = new("_index");
    internal static readonly VanillaPrivateField<NSubmenu, NSubmenuStack> SubmenuStack = new("_stack");
    internal static readonly VanillaPrivateField<NClickableControl, bool> ClickableControlIsEnabled = new("_isEnabled");
    internal static readonly VanillaPrivateField<NDropdownPositioner, Control> DropdownPositionerDropdownNode = new("_dropdownNode");
    /// <summary>声明在基类 NDropdown 上。</summary>
    internal static readonly VanillaPrivateField<NSettingsDropdown, Control> SettingsDropdownDropdownContainer = new("_dropdownContainer");
    internal static readonly VanillaPrivateField<NCursorManager, Image> CursorManagerCursorTilted = new("_cursorTilted");
    internal static readonly VanillaPrivateField<NCursorManager, Image> CursorManagerCursorNotTilted = new("_cursorNotTilted");
    internal static readonly VanillaPrivateField<NCursorManager, Image> CursorManagerCursorInspect = new("_cursorInspect");

    // 音乐
    internal static readonly VanillaPrivateField<NRunMusicController, Node> RunMusicControllerProxy = new("_proxy");
    internal static readonly VanillaPrivateField<NRunMusicController, string> RunMusicControllerCurrentAmbience = new("_currentAmbience");

    /// <summary>初始化步骤：触发解析并汇总缺失项。缺失不中断初始化，用到它的功能各自降级。</summary>
    internal static void Report()
    {
        // 读一个字段即可触发本类型初始化，所有访问器随之解析。
        _ = AttackCommandAttackerAnimName.IsAvailable;
        List<string> missing = [];
        List<string> optionalMissing = [];
        foreach (VanillaPrivateMember member in VanillaPrivateMember.All)
        {
            if (!member.IsAvailable)
            {
                (member.Optional ? optionalMissing : missing).Add(member.Name);
            }
        }

        if (missing.Count > 0)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error("[LibraryOfRuina.VanillaPrivate] " + missing.Count
                + " vanilla member(s) not found; features using them are degraded: " + string.Join(", ", missing));
        }

        MegaCrit.Sts2.Core.Logging.Log.Info("[LibraryOfRuina.VanillaPrivate] "
            + (VanillaPrivateMember.All.Count - missing.Count - optionalMissing.Count) + " resolved"
            + (optionalMissing.Count > 0 ? "; version-specific, absent here: " + string.Join(", ", optionalMissing) : "") + ".");
    }

    /// <summary>LOR_DUMP_PATCHES 导出：每行“成员名 → 解析结果”。</summary>
    internal static IEnumerable<string> DumpLines()
    {
        foreach (VanillaPrivateMember member in VanillaPrivateMember.All)
        {
            yield return member.Name + "\t" + (member.IsAvailable ? "ok" : member.Optional ? "absent (optional)" : "MISSING");
        }
    }
}
