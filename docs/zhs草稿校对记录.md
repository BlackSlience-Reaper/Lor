# zhs 本地化草稿校对记录

以 v0.21.0 中文为底，逐条对照 v0.21.2 英文，共 4627 条，更新 361 条。草稿在 `LibraryOfRuina/localization/zhs/`，作者之后会提交官方中文，届时以官方版本为准。各块的更新原因、英文疑似有误（附代码依据）、拿不准项如下。

尚未处理的跨块统一：黄蜂→蜂后、坏巫师→坏女巫、终末鸟之蛋→卵、眩晕→晕眩、Laetitia 的中文写法。`relics.json::BIG_BIRD_PAGE_RELIC.description` 与英文一样含字面量 `\n`。

恢复 zhs 表后，`src/localization/PowerIconLocalizationPatch.cs` 会重新以 zhs 为参考语言解析能力名图标（没有 zhs 时它按当前语言直接解析），所以草稿里的能力名须与 `PowerNameMap.cs` 的 `Zhs` 字段一致；目前全部一致。


---

## 块 A

# 块 A 校对记录（cards / card_library / enchantments / afflictions / modifiers）

## 统计

- 审阅条数：411（cards 378、enchantments 20、afflictions 9、card_library 2、modifiers 2），逐条对照英文。
- UPDATE：21 条
  - 效果事实不一致或缺漏：17
  - 名称不符（原版术语 / 能力名）：2
  - BBCode 缺英文标签：1
  - eng_old→eng 改动：1
- 自检：`out_A.json` 可被 json.load 读取；所有键都在输入中；变量集合与英文一致；标签成对嵌套；没有字面量 `\n`。对整块最终文本（UPDATE 与 KEEP）也跑了变量和标签检查，没有问题（STRANGLING_VINE 的 `[img]res://…[/img]` 路径属正常）。

## UPDATE 列表

- cards.json::CHORD_EGO_CARD.description — 两行"未击破格挡"缺英文的 [color=#ffffff80] 标签
- cards.json::DESPAIR_KNIGHT_TEAR_SWORD_CHOICE_CARD.description — 追加伤害基数补"目标"最大生命值
- cards.json::LITTLE_RED_PREY_CHOICE_CARD.description — 原句主宾错位，按英文重写猎物判定
- cards.json::MAGIC_BULLET_COMMISSION_CHOICE_CARD.description — 补"攻击伤害""战斗结束后"和目标死亡后重新指定
- cards.json::MAGIC_BULLET_SILENCE_EGO_CARD.description — Frail 是原版 FrailPower，"破防"改为"脆弱"
- cards.json::BLACK_SWAN_FILTH_CHOICE_CARD.description — "所有负面效果"改为"第一个负面效果"（代码只翻倍单个）
- cards.json::BLACK_SWAN_BROKEN_UMBRELLA_CHOICE_CARD.description — 补"回合计数在战斗之间保留"
- cards.json::SNOW_WHITE_POISON_STING_BARRIER_CHOICE_CARD.description — 补回合开始时、存活敌人、总层数和计数保留
- cards.json::QUEEN_BEE_LOYALTY_CHOICE_CARD.description — 补"（可叠加）"
- cards.json::SONG_MACHINE_MELODY_CHOICE_CARD.description — Dazed 原版译名是"晕眩"，不是"眩晕"
- cards.json::WRATH_SERVANT_FRIEND_CHOICE_CARD.description — "造成未被格挡的目标"语义残缺，已重写
- cards.json::HEART_OF_ASPIRATION_PULSE_CHOICE_CARD.description — 补"回合开始时"，删去错误的"永久"（代码 turns=0）
- cards.json::HEART_OF_ASPIRATION_VIOLENT_PULSE_CHOICE_CARD.description — 补"每场战斗一次"
- cards.json::SOUL_SNARE_STATUS_CARD.description — 补只能指定大鸟、必须先打出的条件和红色伤害
- cards.json::BIRD_LULLABY_CARD.description — 补"目标是大鸟时"条件和"移除其所有格挡"
- cards.json::OZMA_OLD_POWER_CHOICE_CARD.description — eng_old 改动：倍率也作用于混乱伤害
- cards.json::RNFMABJ_WILL_OF_THE_PRESCRIPT_CARD.status.failed — 补"本回合"
- cards.json::JUDGEMENT_BIRD_WEIGHT_OF_SIN_CHOICE_CARD.description — 补可被格挡、不受加成和"如果存活"
- cards.json::JUDGEMENT_BIRD_JUDGEMENT_CHOICE_CARD.description — 补不可转移、当前生命值、向下取整
- cards.json::JUDGEMENT_BIRD_TILTED_SCALE_CHOICE_CARD.description — 补"受击前"和"存活玩家"
- cards.json::BLUE_STAR_ATONEMENT_CHOICE_CARD.description — 失去生命的基数补"当前生命值"

## 英文疑似有误（中文已按代码保留，均为 KEEP）

- cards.json::ALL_AROUND_HELPER_RECOGNITION_FUNCTION_CHOICE_CARD.description：英文写"获得 {Swift} 层迅捷"，代码实际每打出 {Cards} 张牌施加 1 层原版 `DrawCardsNextTurnPower`（下回合多抽 1 张）。旧中文"下回合多抽"与代码一致。依据：src/relics/AllAroundHelper/AllAroundHelperPageRelic.cs:153（悬浮提示也是 DrawCardsNextTurnPower，见 :64）。
- cards.json::FUNERAL_COFFIN_CHOICE_CARD.description：英文只写 Strength/Dexterity，代码施加的是临时版本 `FlexPotionPower`/`AnticipatePower`（回合结束时失去）。旧中文"临时力量/临时敏捷"正确。依据：src/relics/FuneralOfTheDeadButterflies/FuneralOfTheDeadButterfliesPageRelic.cs:135、:137。
- cards.json::SMILING_BODIES_CORPSE_LAUGHS_CHOICE_CARD.description：英文写"whenever you are hit"，代码要求 `result.UnblockedDamage > 0`。旧中文"受到未格挡伤害时"正确。依据：src/relics/SmilingBodies/SmilingBodiesPageRelic.cs:307。
- cards.json::HEART_OF_ASPIRATION_VIOLENT_PULSE_CHOICE_CARD.description：英文没写"永久"，代码以 `turns: -1`（永久）施加强壮/忍耐/守护。UPDATE 时保留了中文的"永久"。依据：src/relics/HeartOfAspiration/HeartOfAspirationPageRelic.cs:250-252。
- 以下 5 条的英文把卡牌自带关键词又写了一遍。这些关键词已在 `CanonicalKeywords` 里，原版 `CardModel.GetDescriptionForPile` 会自动追加关键词行，所以英文实际显示会重复。旧中文省略是对的，均保持不变：
  - cards.json::PLEASURE_CARD.description（Unplayable）：src/monsters/ArtFloorLiberation/PleasureCard.cs:34
  - cards.json::LANGUAGE_FLOOR_FEAR_CARD.description（Unplayable）：src/cards/LanguageFloorLiberation/LanguageFloorFearCard.cs:46-47
  - cards.json::SCARECROW_WISDOM_STATUS_CARD.description（Exhaust）：src/cards/ScarecrowSearchingForWisdom/ScarecrowWisdomStatusCard.cs:30
  - cards.json::SILENCE_STATUS_CARD.description（Ethereal、Exhaust）：src/cards/PriceOfSilence/PriceOfSilenceCards.cs:35-38
  - cards.json::SOUL_SNARE_STATUS_CARD.description（Exhaust）：src/cards/BigBird/BigBirdCards.cs:35-37（这条另有内容缺漏，已 UPDATE，但没加"消耗"行）

## 拿不准（请人工确认）

- cards.json::XIAO_PULAO_BELL_EGO_CARD.title「蒲牢鳴鍾」、XIAO_TAOTIE_FEAST_EGO_CARD.title「饕餮饗食」：简中里用了繁体字（鳴、鍾、饗）。可能是作者有意保留的专名风格，按"不改作者专有名词"保持不变。如果要统一成简体，应为「蒲牢鸣钟」「饕餮飨食」。
- cards.json::SILENT_ORCHESTRA_FERVENT_ADORATION_CHOICE_CARD.title「狂热崇拜」：与能力 FANATIC_WORSHIP_POWER（PowerNameMap Zhs「狂热崇拜」）同名，英文分别是 Fervent Adoration 和 Fanatic Worship，jpn 也分成「熱烈な感動」和「狂熱崇拝」。标题目前不受图标补丁影响，保持不变。如需避免混淆，可改为「热烈崇敬」一类。
- enchantments.json::YANAMI_ETHEREAL_ENCHANTMENT.title「虚化」：英文标题是 Ethereal，但块 B 的 TEARDROP_PENDANT_RELIC 英文把它叫 [purple]Virtualize[/purple]，中文两处都是「虚化」。中文内部一致，保持不变；英文两处名称不一致，建议统一。
- 块外同类问题（不在本块输出）：块 B 的 relics.json::SONG_MACHINE_PAGE_RELIC.description 也把 Dazed 写成「眩晕」，应与本块一致改为原版「晕眩」。
- cards.json::SILENT_ORCHESTRA_FERVENT_ADORATION_CHOICE_CARD.description：英文说明百分比按拥有该效果的玩家数叠加，旧中文只写"(允许叠加)"，事实上不冲突，保持不变。需要更精确可改为"（每名拥有此效果的玩家各自叠加此百分比）"。

---

## 块 B

# 块 B 校对笔记（relics / ancients / acts / gameplay_ui / settings_ui / intentgraph / ftues）

## 统计

- 审阅：590 条（relics 215、ancients 182、ftues 69、settings_ui 38、gameplay_ui 32、intentgraph 28、acts 26），逐条对照英文，未抽样。
- UPDATE：73 条（relics 22、ancients 26、gameplay_ui 10、settings_ui 4、ftues 11；acts、intentgraph 全部 KEEP）。
- 按原因分类（每条只按主因计一次）：
  - 效果事实与英文不一致（条件1）：12
  - 名称所指变化（条件3）：3
  - 残缺、残留英文、明显误译或缺失（条件4，含“……”占位、`Nope`、Acts 误译为“层”、表格数据错乱）：12
  - 英文 v0.21.0→0.21.2 有改动（条件5，eng_old）：11（11 条 eng_old 全部处理）
  - 非效果文案与当前英文不一致，且日文佐证作者新版中文已改（叙述缺句、称号、选项名、对白、更新日志、不兼容提示）：35
- 变量与 BBCode：73 条新文本均机检通过（变量集合与英文一致，无多余变量；标签成对）。其余 517 条保留条目同样机检，未发现变量或标签问题。

## 判定口径（便于复核）

- 效果文本以英文为准，英文与代码冲突时以代码为准。
- 风味、对白、叙述：如果 `jpn_now` 与 `zhs_old` 一致（说明作者删表前的最新中文仍是这版原创），即使英文是另一句，也 KEEP，不用英文改写作者的原创中文。如果 `jpn_now` 与英文一致而与 `zhs_old` 不同（说明作者的最新中文已改），则按英文 UPDATE。旧中文只有“……”的，按英文补写。
- 保留条目中与英文措辞不同、但按上述口径 KEEP 的主要有：
  - 八奈见相关遗物的风味：FERRIS_WHEEL_TICKET、FIVE_HUNDRED_YEN_BILL、FRIES_KNUCKLES、GHOST_CLUB_MEMBER、LEFTOVER_SODA、LEOPARD_PLUSH、LOVE_BENTO、RESTAURANT_RECEIPT、SCHOOL_FESTIVAL_MAGIC、TEARDROP_PENDANT。
  - MAGIC_CURSE_RELIC.flavor、DESPAIR_KNIGHT(_ENHANCED)_PAGE_RELIC.flavor。
  - ancients 中 YANAMI 的整段文本，包括 INITIAL/DONE 描述、选项标题和全部对白。
  - HISTORY_FLOOR 与 TECHNOLOGY_FLOOR 结算事件的标题、History 称号、DONE、全部对白，以及直接写出阶段 Boss 名的 TIER 标题和 LOCKED 描述，例如“需要镇压翅振”。这些写法与“至少 N 个阶段”等价，因为阶段是按顺序推进的。
  - BURROWING_HEAVEN 的末尾提示“选择一张渗透天堂书页”及 selectionScreenPrompt，OZMA 的 selectionScreenPrompt。
- 条目中出现 Sephirah 名、MoonText、Doormaker Beta 等拉丁字母专名，是作者有意保留，日文也照写，因此 KEEP。
- `ALL_AROUND_HELPER_PAGE_RELIC` 的 Repeated Recognition 和 `FUNERAL_OF_THE_DEAD_BUTTERFLIES_PAGE_RELIC` 的 Coffin：代码支持旧中文，所以 KEEP，见下方“英文疑似有误”。
- `HEART_OF_ASPIRATION` 已查代码：脉动模式为 `LibraryPowerCmd.Apply(..., turns: 0)`，只在本回合生效，旧中文写的“永久”是错的，已改。剧烈脉动模式为 `turns: -1`，确实永久，英文只是没写，中文保留“永久”，并按英文补上“每场战斗一次”（每场战斗开始时会重置 `ViolentPulseActivated`）。
- `ROAD_HOME` 同行之路：代码 `src/relics/RoadHome/RoadHomePageRelic.cs:323-326` 显示，死亡时失去 50% 最大生命的是猫咪。中文已写明主体。

## UPDATE 列表

- relics.json::LETICIA_PAGE_RELIC.title — 名称：英文改为Little Witch，日文同
- relics.json::MATCH_MARK_RELIC.title — 名称：英文改为Match Mark，日文同
- relics.json::LITTLE_RED_MERCENARY_PAGE_RELIC.description — 猎物模式句子残缺，语义不清
- relics.json::MAGIC_BULLET_SHOOTER_PAGE_RELIC.description — 委托：缺战斗后发放与重新指定；黑焰缺“分别”
- relics.json::BLACK_SWAN_DREAM_PAGE_RELIC.description — 破伞缺“回合计数跨战斗保留”
- relics.json::SNOW_WHITE_APPLE_PAGE_RELIC.description — 毒刺屏障缺时机、存活范围与计数保留
- relics.json::QUEEN_BEE_PAGE_RELIC.description — 忠诚缺“可叠加”
- relics.json::QUEEN_OF_HATRED_PAGE_RELIC.description — 博爱触发条件歧义（首次击破）
- relics.json::RED_SHOES_PAGE_RELIC.description — 利斧缺“命中格挡”条件
- relics.json::HEART_OF_ASPIRATION_PAGE_RELIC.description — 脉动为每回合开始获得且非永久；右键每战一次
- relics.json::WARMHEARTED_WOODSMAN_PAGE_RELIC.description — 心脏缺“非X费”限制
- relics.json::ROAD_HOME_PAGE_RELIC.description — 同行之路主体不明（应为猫咪）
- relics.json::OZMA_PAGE_RELIC.description — 英文改动：倍率也作用于混乱伤害
- relics.json::SMILING_BODIES_PAGE_RELIC.flavor — 风味文本缺失（仅省略号）
- relics.json::JUDGEMENT_BIRD_PAGE_RELIC.flavor — 风味文本缺失（仅省略号）
- relics.json::BLUE_STAR_PAGE_RELIC.flavor — 风味文本缺失（仅省略号）
- relics.json::BURROWING_HEAVEN_PAGE_RELIC.flavor — 风味文本已改写（日文同英文）
- relics.json::HEART_OF_ASPIRATION_PAGE_RELIC.flavor — 风味文本已改写（日文同英文）
- relics.json::JUDGEMENT_BIRD_PAGE_RELIC.description — 缺可格挡/无加成、存活条件、不可转移、取整
- relics.json::BLUE_STAR_PAGE_RELIC.description — 赎罪缺“当前生命”；思念之声限定攻击命中
- relics.json::WRATH_SERVANT_ENHANCED_PAGE_RELIC.description — 朋友+句子残缺
- relics.json::DESPAIR_KNIGHT_ENHANCED_PAGE_RELIC.description — 泪剑+缺“目标”最大生命
- ancients.json::HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description — 缺开头叙述句
- ancients.json::HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_2_LOCKED.description — 残留英文“Nope”
- ancients.json::TECHNOLOGY_FLOOR_LIBERATION_SETTLEMENT_EVENT.epithet — 称号已改（日文同英文）
- ancients.json::TECHNOLOGY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description — 缺开头叙述句
- ancients.json::TECHNOLOGY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_2_LOCKED.description — 残留英文“Nope”
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.epithet — 称号已改（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description — 缺叙述与选择说明
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_2.title — 选项名已改为阶段数（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_4.title — 选项名已改为阶段数（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_5.title — 选项名已改为阶段数（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.DONE.description — 文案已改写（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.firstVisitEver.0-0.ancient — 对白已改写（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.0-0r.ancient — 对白已改写（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.0-1r.char — 对白已改写（日文同英文）
- ancients.json::ART_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.1-0r.ancient — 对白已改写（日文同英文）
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description — 缺叙述与选择说明
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.DONE.description — 文案已改写（日文同英文）
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.firstVisitEver.0-0.ancient — 对白已改写（日文同英文）
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.0-0r.ancient — 对白已改写（日文同英文）
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.0-1r.char — 对白已改写（日文同英文）
- ancients.json::LANGUAGE_FLOOR_LIBERATION_SETTLEMENT_EVENT.talk.ANY.1-0r.ancient — 对白已改写（日文同英文）
- ancients.json::LITERATURE_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description — 缺叙述与选择说明
- ancients.json::NATURAL_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_4.title — 选项名已改（日文同英文）
- ancients.json::NATURAL_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.TIER_4_LOCKED.title — 选项名已改（日文同英文）
- ancients.json::NATURAL_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.SPECIAL.title — 选项名与英文不符
- ancients.json::NATURAL_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.SPECIAL_LOCKED.title — 选项名与英文不符
- gameplay_ui.json::LIBRARY_SECOND_ASCENSION_MODIFIER.description — 英文改动
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_01.description — 英文改动：去掉等级I
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_02.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_03.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_05.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_06.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_07.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_08.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_09.description — 英文改动：效果重排
- gameplay_ui.json::SECOND_ASCENSION.LEVEL_10.description — 英文改动：TODO→TBD
- settings_ui.json::LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.title — 标题与英文不符
- settings_ui.json::LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.body — 内容与英文完全不同
- settings_ui.json::LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.item — 格式与英文不符
- settings_ui.json::LIBRARYOFRUINA-INCOMPATIBLE_MOD_REASON.HEXTECH_RUNES — 内容与英文完全不同
- ftues.json::LOR_FIRST_LIBERATION_FTUE_BODY_2 — 第二段句子残缺
- ftues.json::LOR_NEOW_SPECIAL_GUEST_FTUE_RELIC_BODY — Acts误译为“层”
- ftues.json::LOR_UPDATE_LOG_20260902_NOTICE_BODY — Acts误译为“层”
- ftues.json::LOR_UPDATE_LOG_20260902_SCOPE_BODY — 缺基准说明与多项内容
- ftues.json::LOR_UPDATE_LOG_20260902_LITERATURE_BODY — 缺阶段特色说明
- ftues.json::LOR_UPDATE_LOG_20260902_ENCOUNTERS_BODY — 内容大幅缺失
- ftues.json::LOR_UPDATE_LOG_20260902_PAGES_BODY — 缺第二段、多出“强力”
- ftues.json::LOR_UPDATE_LOG_20260902_BALANCE_BODY — Acts误译为“层”且缺多项内容
- ftues.json::LOR_UPDATE_LOG_20260903_OVERVIEW_BODY — Acts误译为“层”
- ftues.json::LOR_UPDATE_LOG_20260903_ENEMIES_BODY — 表格单元格数据错乱
- ftues.json::LOR_UPDATE_LOG_20260903_SYSTEM_BODY — 敌人名与英文所指不同

## 英文疑似有误

- relics.json::ALL_AROUND_HELPER_PAGE_RELIC.description（Repeated Recognition 模式）：英文写每打出 N 张牌获得 Swiftness；代码实际施加原版 `DrawCardsNextTurnPower` 1 层（下回合多抽 1 张），悬浮提示也是它。旧中文“下回合多抽{Swift}张牌”与代码一致，保持不改。代码依据 `src/relics/AllAroundHelper/AllAroundHelperPageRelic.cs:153`（悬浮提示 :64）。
- relics.json::FUNERAL_OF_THE_DEAD_BUTTERFLIES_PAGE_RELIC.description（Coffin 模式）：英文写获得 Strength/Dexterity，代码施加的是 `FlexPotionPower` / `AnticipatePower`（临时力量/临时敏捷）。旧中文“临时力量/临时敏捷”与代码一致，保持不改。代码依据 `src/relics/FuneralOfTheDeadButterflies/FuneralOfTheDeadButterfliesPageRelic.cs:135,137`。
- relics.json::HEART_OF_ASPIRATION_PAGE_RELIC.description（Violent Pulse 模式）：英文漏写“permanent”，代码为 `turns: -1`，即永久。这是遗漏，不是矛盾；中文保留“永久”。代码依据 `src/relics/HeartOfAspiration/HeartOfAspirationPageRelic.cs:250-252`。
- relics.json::MAGIC_CURSE_RELIC.flavor：英文 “but the contract is gone” 与中文原意“祭台会倒，契约不会”相反，疑似英文误译。这是风味文本，无代码依据，中文保持不改。

## 拿不准

- relics.json::LETICIA_PAGE_RELIC.title → “小魔女之页”：英文已改为 Little Witch Page，日文为「リトル・ウィッチのページ」，推断作者新版中文已改名。如果想保留“蕾蒂希娅之页”，请撤回。
- relics.json::MATCH_MARK_RELIC.title → “火柴印记”：英文 Match Mark 与日文「マッチマーク」一致，旧中文“焦化少女之页”显然已过时。但作者的新中文名无从得知，“火柴印记”是按英文拟的。
- ancients.json 中 LANGUAGE_FLOOR 结算事件的 4 句对白和 DONE 描述：旧中文是 Gebura 的原作台词（如“彷徉奈落之底者，吾魂兮”）。因为日文已与新英文一致，所以按英文替换。如果想保留原作台词，可以整组撤回。ART_FLOOR 同理。
- relics.json::JUDGEMENT_BIRD_PAGE_RELIC.description：“blockable unpowered damage”译作“可被格挡、不受加成影响的伤害”，原版的“unpowered”没有统一中文术语。
- ftues.json::LOR_UPDATE_LOG_20260902_ENCOUNTERS_BODY：碧蓝新星的 Voice / Return 机制名没有现成的中文，暂译“声音”“回归”。
- 保留未改的小差异：
  - relics.json::QUEEN_OF_HATRED_ENHANCED 等“+”页的“拾起时选择一种效果。”与英文 “Choose an effect upon pickup.” 一致。
  - SILENT_ORCHESTRA 狂热崇拜的“(允许叠加)”，是对英文 “stacks for each player with this effect” 的概括。
  - 以上两处判为等义，没有改。

---

## 块 C

# 块 C（powers.json 前半）校对记录

## 统计
- 审阅：633 条（全部为 powers.json；无 `eng_old`，无 `zhs_old` 为 null）
- UPDATE：43 条
- 按原因分类：
  - 效果事实与英文不一致（漏条件/漏效果/时机/对象/数量）：33
  - 以代码为准修正（英文或旧中文与代码不符）：5（GREEN_STEM 幕→回合、WRATH_SERVANT_CORROSION 幕→回合、VIOLENT_HEART.description、TREE_HEART.description、IMPROV_DRUMMING.description 去掉写死的“1层”）
  - 名称不一致：2（小红帽招式名、Heart 2 标题）
  - 术语/错误文本：3（混沌伤害→混乱伤害、description 误抄 smartDescription、半角句点）
- 能力标题与 PowerNameMap.cs `Zhs` 核对：本块 43 个有映射的 `.title` 全部一致，无需改动。

## UPDATE 列表
- powers.json::ADDICTED_EMPLOYEE_MELODY_CRAVING_POWER.smartDescription — 补“敌方”回合结束（代码 AfterSideTurnEnd 仅敌方）
- powers.json::CHORD_ENSEMBLE_POWER.description — 混沌伤害→混乱伤害（代码 ChaoDamage）
- powers.json::DESPAIR_KNIGHT_SORROW_POWER.smartDescription — 补“以显式意图”
- powers.json::DOORMAKER_GAZE_POWER.description — 旧文误抄 smart，按英文重译
- powers.json::FAIRY_QUEEN_STARVED_FRENZY_POWER.smartDescription — 补“每场战斗限一次”
- powers.json::FLICKERING_DESIRE_POWER.smartDescription — 补“重置行动循环”
- powers.json::FORGOTTEN_KNIGHT_SWORD_PIERCE_DESPAIR_POWER.smartDescription — 补“未被格挡”
- powers.json::GOLDEN_AMBER_POWER.smartDescription — 补无法行动/首次敌方行动时苏醒
- powers.json::GREEN_STEM_HERMIT_PROTECTION_POWER.smartDescription — 每一幕→每回合（英文+代码）
- powers.json::IMPROV_DRUMMING_POWER.description — 删写死“1层”（代码为2层，英文不写数）
- powers.json::LIBRARYOFRUINA_DOORMAKER_BETA_REWORK_GRASP_POWER.smartDescription — 每名玩家/X费与无法打出除外/虚无持续本场
- powers.json::LIBRARYOFRUINA_DOORMAKER_BETA_REWORK_HUNGER_POWER.description — 删“本回合”，牌组→战斗各牌堆
- powers.json::LIBRARYOFRUINA_DOORMAKER_BETA_REWORK_HUNGER_POWER.smartDescription — 同上
- powers.json::LIBRARY_OF_RUINA_MARK_POWER.smartDescription — 补“再次施加只刷新标记”
- powers.json::LITTLE_RED_ANGER_GAUGE_POWER.smartDescription — 补“多段攻击逐段结算”
- powers.json::LITTLE_RED_NIGHTMARE_END_POWER.smartDescription — 旧文大量缺失，全文重译
- powers.json::LITTLE_RED_UNRELIEVED_ANGER_POWER.smartDescription — 招式名改为毫不迟疑地挥砍/弹雨
- powers.json::RED_MIST_EGO_POWER.smartDescription — 补“未被格挡伤害总计”
- powers.json::RED_SHOES_BLOOD_ATTRACTION_POWER.smartDescription — 补选目标规则，多段攻击→欲望迸发
- powers.json::REGRET_END_BEGIN_END_POWER.smartDescription — 补“随后计数重置”
- powers.json::SHINING_HAPPINESS_POWER.smartDescription — 补“被破坏时立即移除加成”
- powers.json::SOLEMN_MOURNING_SEAL_ON_ENEMY_POWER.description — 意图→攻击意图
- powers.json::SOLEMN_MOURNING_SEAL_ON_ENEMY_POWER.smartDescription — 攻击意图+跳过攻击段
- powers.json::SPIDER_BUD_START_HUNTING_POWER.description — 补“小蜘蛛已死”
- powers.json::SPIDER_BUD_START_HUNTING_POWER.smartDescription — 攻击→猎食意图，删不存在的“狂暴”
- powers.json::SPIDER_BUD_UNTARGETABLE_POWER.smartDescription — 补“或命中”
- powers.json::TECHNOLOGY_FLOOR_MK4_MAX_CHARGE_POWER.smartDescription — 补“并重置充能”
- powers.json::WOLF_HOWLING_NIGHTMARE_POWER.smartDescription — 补“进入第二阶段”
- powers.json::WRATH_SERVANT_CORROSION_POWER.smartDescription — 每一幕→每回合（代码按回合）
- powers.json::ART_FLOOR_GALAXY_DO_NOT_LEAVE_ME_POWER.smartDescription — 半角句点改全角
- powers.json::ART_FLOOR_CLAY_DOLL_POWER.smartDescription — 补“死亡效果”
- powers.json::ART_FLOOR_FINAL_DA_CAPO_PERFORMER_PASSIVE_POWER.smartDescription — 无法行动→意图变为未知
- powers.json::BURROWING_HEAVEN_WINGS_TOWARD_OLD_GOD_PASSIVE_POWER.smartDescription — 玩家回合开始/对反击者
- powers.json::HEAVEN_THORN_DO_NOT_SHIFT_GAZE_PASSIVE_POWER.smartDescription — 补“多根不重复触发”
- powers.json::BURROWING_HEAVEN_SLEEP_POWER.description — 与天堂之刺沉眠描述互换错位
- powers.json::BURROWING_HEAVEN_SLEEP_POWER.smartDescription — 删多余“沉眠”
- powers.json::HEAVEN_THORN_SLEEP_POWER.description — 与渗透天堂沉眠描述互换错位
- powers.json::HEAVEN_THORN_SLEEP_POWER.smartDescription — 生成→苏醒为天堂之刺
- powers.json::WARMHEARTED_WOODSMAN_VIOLENT_HEART_PASSIVE_POWER.description — 按代码：树木死亡时触发
- powers.json::WARMHEARTED_WOODSMAN_EMPTY_HEART_PASSIVE_POWER.smartDescription — 补1回合/只触发一次需再获得
- powers.json::WOODSMAN_TREE_HEART_PASSIVE_POWER.title — 心脏→心脏2（英文、日文一致）
- powers.json::WOODSMAN_TREE_HEART_PASSIVE_POWER.description — 按代码：有温暖的心时以树为目标
- powers.json::PRICE_OF_SILENCE_YOUR_TIME_PASSIVE_POWER.smartDescription — 旧文机制不符，按英文重译

## 英文疑似有误
- powers.json::WARMHEARTED_WOODSMAN_VIOLENT_HEART_PASSIVE_POWER.description / .smartDescription — 英文“On kill”；代码在树木 AfterDeath 时给樵夫回血加力量（任何原因致树死亡都触发），smart 旧中文“树木死亡时”保留 — 代码依据 src/monsters/WarmheartedWoodsman/WarmheartedWoodsman.cs:925-955
- powers.json::WOODSMAN_TREE_HEART_PASSIVE_POWER.description — 英文“has no Warm Heart”；代码 `_targetTreeAsAttackTarget = HasWarmHeart`，与 smart 英文一致 — 代码依据 src/monsters/WarmheartedWoodsman/WarmheartedWoodsman.cs:585-595
- powers.json::WRATH_SERVANT_CORROSION_POWER.smartDescription — 英文“At the end of each act”；代码为持有方每回合结束（AfterSideTurnEnd） — 代码依据 src/powers/WrathServant/WrathServantPowers.cs:120-140
- powers.json::QUEEN_BEE_NEXT_TURN_STRONG_POWER.description — 英文“gain Strength”；代码施加 LibraryStrongPower（强壮），旧中文“等量强壮”保留 — 代码依据 src/powers/QueenBee/QueenBeeNextTurnStrongPower.cs:51
- powers.json::LUNG_OF_ASPIRATION_DESIRE_PASSIVE_POWER.description — 英文“gain Power Up”；代码施加 StrengthPower，旧中文“力量”保留 — 代码依据 src/powers/HeartOfAspiration/HeartOfAspirationPowers.cs:74,104
- powers.json::WEDGE_PIERCING_POWER.smartDescription — 英文“segment of an empowered move attack”；代码条件是 `IsPoweredAttack`（非 Unpowered 的攻击伤害，且目标为玩家），并非“强化招式”，旧中文“进行一段攻击后”保留 — 代码依据 src/powers/WedgeOffice/WedgePiercingPower.cs:35
- powers.json::FLUTTERING_HUNGER_FRENZY_POWER.smartDescription — 英文只写“insert Hunger Frenzy as the next action”；代码 `CreatureCmd.Stun(Creature, HungerFrenzyMoveId)` 先击晕再用 EGO 饥饿狂暴，旧中文“被击晕一回合，下回合使用EGO书页饥饿狂暴”保留；另外代码在生命下降（AfterCurrentHpChanged）时也会触发，并非只在敌方回合开始 — 代码依据 src/powers/HistoryFloorLiberation/HistoryFloorFlutteringPowers.cs:198-235、src/monsters/HistoryFloorLiberation/HistoryFloorFlutteringBoss.cs:213-222
- powers.json::HEAVEN_THORN_DO_NOT_SHIFT_GAZE_PASSIVE_POWER.smartDescription — 英文“all other players take damage”；代码对所有存活玩家（含攻击者）造成伤害，中文保留“所有玩家” — 代码依据 src/powers/BurrowingHeaven/BurrowingHeavenPowers.cs:100-110
- powers.json::FLUTTERING_HUNGER_FRENZY_POWER.description — 同上，英文“inserts Hunger Frenzy”未提击晕（中文 KEEP）

## 拿不准
- powers.json::HISTORY_FLOOR_LIBERATION_CONTROLLER_POWER.title — 英文“History Floor Liberation Record”，旧中文“历史层解放”；日文「歴史的解放」显示作者最新中文未改，按“同一对象、作者名称”KEEP，是否补“记录”请人工定。
- powers.json::WOLF_HOWL_PASSIVE_POWER.title — 英文“Moonlit Howl”，旧中文“恶狼咆哮”（日文「狼は唸った」同旧中文）；KEEP，若要贴英文可改“月下嚎叫”。
- powers.json::ADDICTED_EMPLOYEE_MELODY_CRAVING_POWER.title / CHORD_STAFF_MELODY_CRAVING_POWER.title — 英文“Melody Craving”，旧中文“我渴望那段旋律！”（日文同旧中文），KEEP。
- powers.json::WOODSMAN_TREE_HEART_PASSIVE_POWER.title — 已改“心脏2”（英/日均带 2），若该“2”只是内部区分用可改回“心脏”。
- powers.json::LITTLE_RED_UNRELIEVED_ANGER_POWER.smartDescription — 招式名取自 chunk_G 旧中文（毫不迟疑地挥砍 / 弹雨），若 G 块改了这两个招式名需同步。
- LIBRARYOFRUINA_DOORMAKER_BETA_REWORK_* / DOORMAKER_GAZE_POWER — src 中无对应代码（孤儿键），只能按英文校对。
- powers.json::EMERALD_BOUGH_STRANGLING_VINE_POWER.smartDescription — 英文“gain [blue]{Amount}[/blue] stack”（每回合增加当前层数？）读起来可疑，中文与英文一致故 KEEP，未查代码。
- 本块“幕”的用法：作者旧中文多处把 scene 译为“幕”（=回合），如 GALAXY_CHILD_PEBBLE、BURROWING_HEAVEN_IN_COGNITION、PRICE_OF_SILENCE、DA CAPO 周期第5/6幕等，均按作者用法 KEEP；仅英文明确写 turn 或代码按回合的两条改成“回合”。

---

## 块 D

# 块 D（powers.json 后半）校对记录

## 统计

- 审阅条数：633（逐条对照英文；含 eng_old 4 条，全部按新英文重译）
- UPDATE 条数：66
  - 效果事实与英文不一致（漏条件/主体/时机/数值基准等）：58
  - eng_old→eng 改动（OZMA 两个能力的共享计数）：4
  - 名称/术语错误或不一致：3
  - 句末标点残缺：1
- 能力标题：块内 210 个 .title 中与 PowerNameMap.cs 有对应条目的全部与 Zhs 字段一致；无对应条目的标题仅修正 1 条（SCAREDY_CAT_COMPANION_COWARD_POWER.title 多余半角问号）。
- 变量与 BBCode：对最终文本（KEEP 用旧中文、UPDATE 用新中文）逐条比对顶层变量集合与标签配对，唯一变量差异 EILEEN_FLESH_REBIRTH_POWER.smartDescription 缺 {Maximum}，已在 UPDATE 中补上。
- 仅缺句末句号的旧条目（如 NATURAL_FLOOR_FROZEN_*、NATURAL_FLOOR_EVERYTHING_IS_EMPTY_POWER.smartDescription 等）未单独改动。

## UPDATE 列表

- powers.json::PRICE_OF_SILENCE_SILENCE_POWER.smartDescription — 旧文为背景描述，未写实际效果
- powers.json::TICKING_ATTACK_POWER.smartDescription — 漏“基础”值限定
- powers.json::TICKING_GUARD_POWER.smartDescription — 漏“行动/基础值”限定
- powers.json::BIG_BIRD_SLEEP_POWER.smartDescription — 旧文为台词，未写跳过行动与移除魅惑
- powers.json::SCAREDY_CAT_COURAGE_POWER.smartDescription — “本回合”与持续回合矛盾
- powers.json::SCAREDY_CAT_COMPANION_COWARD_POWER.title — 标题多了半角问号
- powers.json::SCAREDY_CAT_COMPANION_COWARD_POWER.description — 变强的主体应为同行之路
- powers.json::FOREST_KEEPER_STOLEN_CHAINS_POWER.smartDescription — 漏存活期间/每名玩家
- powers.json::OZMA_FORGOTTEN_POWER.description — 英文新增命中进度共享
- powers.json::OZMA_FORGOTTEN_POWER.smartDescription — 英文新增命中进度共享
- powers.json::OZMA_LOST_MEMORY_POWER.smartDescription — 旧文未写暂停与移除后恢复
- powers.json::OZMA_EAST_JACK_POWER.smartDescription — 漏“苏醒时”触发时机
- powers.json::OZMA_PAIN_PASSIVE_POWER.smartDescription — 漏“首次”与敌人回合
- powers.json::OZMA_SORROW_PASSIVE_POWER.smartDescription — 漏“强力/反复”使用
- powers.json::OZMA_TAKE_OR_BE_TAKEN_POWER.description — 改为全体玩家共同计数
- powers.json::OZMA_TAKE_OR_BE_TAKEN_POWER.smartDescription — 改为全体计数且写死数字换变量
- powers.json::LANGUAGE_FLOOR_WOLF_HOWL_PASSIVE_POWER.smartDescription — 漏招式名咆哮
- powers.json::LANGUAGE_FLOOR_PUNISH_EVIL_PASSIVE_POWER.smartDescription — 抗性上限是“增加”而非“变为”
- powers.json::LANGUAGE_FLOOR_HIDE_IN_DARKNESS_PASSIVE_POWER.description — 旧文“单回合”与累计机制不符
- powers.json::LANGUAGE_FLOOR_HIDE_IN_DARKNESS_PASSIVE_POWER.smartDescription — 基准与潜狼期间不累计写错/漏写
- powers.json::LANGUAGE_FLOOR_SMILING_FACE_VOMIT_POWER.smartDescription — 时机应为玩家回合开始
- powers.json::LANGUAGE_FLOOR_DIPSIA_TRANSFORM_POWER.smartDescription — 漏锁定体力；时机为下个玩家回合
- powers.json::LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER.description — 漏“最大”体力
- powers.json::LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER.smartDescription — 漏“最大”体力
- powers.json::LANGUAGE_FLOOR_MIMICRY_FORM_THREE_REGENERATION_POWER.description — 漏“最大”体力
- powers.json::LANGUAGE_FLOOR_MIMICRY_FORM_THREE_REGENERATION_POWER.smartDescription — 漏“最大”体力
- powers.json::LANGUAGE_FLOOR_MIMICRY_MIMIC_POWER.description — 漏“未被格挡”条件
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER.smartDescription.bigEyes — 漏抗性；范围应为所有角色
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER.smartDescription.smallBeak — 漏抗性说明
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER.smartDescription.longArms — 漏抗性说明
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER.smartDescription.none — 漏卵全碎与物理抗性
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_BROKEN_EGG_POWER.smartDescription — 终末鸟之蛋→卵与同组统一
- powers.json::PHILOSOPHY_FLOOR_TWILIGHT_BROKEN_EGG_POWER.smartDescription.secondAscension — 薄冥错字→薄暝，卵名统一
- powers.json::FALSE_THRONE_WIZARDS_TRIAL_POWER.description — 漏常规阶段招式与进入时机
- powers.json::FALSE_THRONE_WIZARDS_TRIAL_POWER.smartDescription — 漏常规阶段招式与进入时机
- powers.json::FALSE_THRONE_WHAT_CAN_YOU_DO_POWER.description — 漏击晕意图/亲切致意/随机两招
- powers.json::FALSE_THRONE_WHAT_CAN_YOU_DO_POWER.smartDescription — 漏击晕意图/亲切致意/随机两招
- powers.json::FALSE_THRONE_RAGE_POWER.smartDescription — 句末误用分号
- powers.json::XIAO_EMBRACE_FIRE_PASSIVE_POWER.smartDescription — 漏引燃说明段
- powers.json::XIAO_AMPHIBIOUS_PASSIVE_POWER.smartDescription — “能力伤害”应为反击伤害
- powers.json::LITERATURE_FLOOR_LAETITIA_PLAY_WITH_ME_PASSIVE_POWER.smartDescription — “其”误指玩家；漏向下取整
- powers.json::LITERATURE_FLOOR_LAETITIA_LONELY_PASSIVE_POWER.description — 漏出现朋友时恢复
- powers.json::LITERATURE_FLOOR_LAETITIA_LONELY_PASSIVE_POWER.smartDescription — 漏出现朋友时恢复
- powers.json::LITERATURE_FLOOR_GIFT_BOX_BOOM_PASSIVE_POWER.smartDescription — 漏敌方回合/存活玩家限定
- powers.json::LITERATURE_FLOOR_SURPRISE_APPEARANCE_PASSIVE_POWER.smartDescription — 漏“原槽位”
- powers.json::LITERATURE_FLOOR_LITTLE_WITCH_FRIEND_HAND_IT_OVER_PASSIVE_POWER.smartDescription — “其攻击”误指玩家；漏每段
- powers.json::LITERATURE_FLOOR_RED_EYES_START_HUNTING_PASSIVE_POWER.smartDescription — 漏再次死亡重置计数
- powers.json::LITERATURE_FLOOR_RED_EYES_VIGILANCE_PASSIVE_POWER.smartDescription — 漏敌方回合/存活限定
- powers.json::LITERATURE_FLOOR_BLOODLUST_GIANT_AXE_PASSIVE_POWER.smartDescription — 漏“每段攻击”
- powers.json::LITERATURE_FLOOR_BLACK_SWAN_NETTLE_GARMENT_PASSIVE_POWER.smartDescription — 漏每位哥哥仅登场一次
- powers.json::LITERATURE_FLOOR_BLACK_SWAN_BROKEN_DREAM_PASSIVE_POWER.smartDescription — 漏加入随机池与循环重置
- powers.json::LITERATURE_FLOOR_BLACK_SWAN_VANISHING_FAMILY_POWER.smartDescription — “始终使用”应为加入随机池
- powers.json::JUDGEMENT_BIRD_UNJUST_SCALE_POWER.smartDescription — 漏玩家回合/存活/敌方限定
- powers.json::JUDGEMENT_BIRD_JUDGEMENT_POWER.smartDescription — 主体泛化为任意角色，漏全体目标
- powers.json::BLUE_STAR_NOVA_VOICE_POWER.smartDescription — 百分比基准应为最大混乱抗性
- powers.json::BLUE_STAR_FOLLOWER_VOICE_POWER.smartDescription — 漏特殊意图触发条件
- powers.json::NATURAL_FLOOR_TEARDROP_POWER.description — 漏交替招式与假死移除
- powers.json::NATURAL_FLOOR_FLICKERING_DESIRE_POWER.smartDescription — 漏变身目标贪婪国王
- powers.json::NATURAL_FLOOR_KING_MOMENTARY_HAPPINESS_POWER.smartDescription — 群体攻击应为特殊攻击
- powers.json::NATURAL_FLOOR_KING_OF_GREED_POWER.smartDescription — “所有玩家”与逐目标施加不符
- powers.json::NATURAL_FLOOR_GLUTTONY_POWER.smartDescription — 群体攻击应为特殊攻击；漏最大体力
- powers.json::NATURAL_FLOOR_SHINING_HAPPINESS_POWER.smartDescription — 漏移除时撤销加成
- powers.json::NATURAL_FLOOR_NIHIL_GREED_POWER.smartDescription — 漏“尚未领取过该奖励”限定
- powers.json::NATURAL_FLOOR_LOVE_POWER.smartDescription — “下回合使用”应为可免费打出
- powers.json::CRYING_SWIFT_POWER.description — “下一回合”应为本回合
- powers.json::EILEEN_FLESH_REBIRTH_POWER.smartDescription — 漏{Maximum}上限与常规行动

## 英文疑似有误

- powers.json::SCAREDY_CAT_COURAGE_POWER.description — 英文写 Strength，代码施加的是 LibraryStrongPower（强壮/Power Up），smartDescription 也写 Power Up；中文“强壮”保持。依据 src/monsters/ScaredyCat/ScaredyCat.cs:150-160
- powers.json::BIG_BIRD_CHARMED_POWER.smartDescription — 英文只写“taking at least N unblocked damage”，代码按单次伤害结果判断（单次未格挡伤害≥阈值才解除），旧中文“单次”更准确，保持。依据 src/powers/BigBird/BigBirdPowers.cs:74-89

## 拿不准

- powers.json::RNFMABJ_CORROSION_POWER.title — 英文 Erosion，旧中文“腐蚀”（PowerNameMap Zhs 也是“腐蚀”，与 WRATH_SERVANT_CORROSION_POWER 同名），日文作“侵食”暗示作者新中文可能改为“侵蚀”；因 PowerNameMap 为准保持“腐蚀”。
- powers.json::SCAREDY_CAT_COMPANION_COWARD_POWER.title — 旧中文“胆小?”，已去掉半角问号改为“胆小”（日文无问号）；若问号是作者有意区分两个同名能力，可回退。
- powers.json::LANGUAGE_FLOOR_RAGE_POWER.description / LANGUAGE_FLOOR_UNRELIEVED_ANGER_POWER.description — 英文 use Indiscriminate Shot first，旧中文“强制使用”；代码确为第一个意图固定为无差别射击（src/monsters/LanguageFloorLiberation/LanguageFloorScarletScar.cs:586-589），语义可接受，保持。
- powers.json::LANGUAGE_FLOOR_SMILING_FACE_FIND_CORPSES_POWER.smartDescription — 英文泛指“allied creature”，旧中文写死“溶解的死尸”；代码判定任意同阵营单位（src/powers/LanguageFloorLiberation/LanguageFloorSmilingFacePowers.cs:32-47），实战中友方只有溶解的死尸，保持。
- powers.json::LITERATURE_FLOOR_EXPRESSION_PASSIVE_POWER.smartDescription — 英文“After ... in total”，旧中文“合计每打出”（重复触发），日文也作“たび”；未查实现，保持旧中文。
- 本块终末鸟之卵旧中文“卵/蛋”混用，已把 BROKEN_EGG 两条统一为“终末鸟之卵”；其他块若有“终末鸟之蛋”需同步。

---

## 块 E

# 块 E（intents/encounters）zhs 校对记录

## 统计

- 审阅条数：579（intents.json 422，encounters.json 157），逐条对照当前英文
- UPDATE 条数：38
- 按原因：事实 24，名称 8，代码 2，变量 1，英文改动 1，其他 1，残留英文 1

原因分类说明：事实=效果事实与英文不一致；名称=标题/专名与英文所指不同；变量=写死数字；代码=英文与代码冲突时按代码写；英文改动=有 eng_old 的改写；残留英文；其他=标点笔误。

## UPDATE 列表

- `intents.json::COUNTER_BUFF.title` — 名称：标题笼统为“策略”，英文为Buff
- `intents.json::COUNTER_CARD_DEBUFF.title` — 名称：标题笼统为“策略”，英文为Erosion
- `intents.json::COUNTER_DEBUFF.title` — 名称：标题笼统为“策略”，英文为Debuff
- `intents.json::COUNTER_STATUS.title` — 名称：标题笼统为“策略”，英文为Status Cards
- `intents.json::COUNTER_SUMMON.title` — 名称：标题笼统为“策略”，英文为Summon
- `intents.json::FOX_DEFEND.description` — 事实：缺“本回合”
- `intents.json::FOX_ENERGY.description` — 事实：缺“下回合”
- `intents.json::I_WANT_MORE_DEBUFF.description` — 事实：缺“额外”
- `intents.json::KING_OF_GREED_OVERWHELMING_GLORY.description` — 变量：写死“少抽2张”，英文用{BadgeSignedMagnitude}
- `intents.json::QUEEN_ARCANA_BEATS.description` — 事实：缺“先移除之前的标记”
- `intents.json::TIME_TRACE_COPY_OFFENSE_TRIPLE.description` — 事实：缺复制上一玩家回合伤害的说明
- `intents.json::TIME_TRACE_COPY_DEFENSE_SINGLE.description` — 事实：缺复制上一玩家回合格挡的说明
- `intents.json::TIME_TRACE_COPY_DEFENSE_STRENGTH.description` — 事实：缺复制一半格挡的说明
- `intents.json::BIG_BIRD_RESCUE.description` — 代码：缺被魅惑时的伤害；代码为60(英文写50)
- `intents.json::ROAD_HOME_BAD_WIZARD_GROUP.description` — 事实：目标缺“其他”“存活”
- `intents.json::SCAREDY_CAT_GROWL.description` — 代码：代码施加的是虚弱(WeakPower)，非易伤
- `intents.json::OZMA_FADING_MEMORY.description` — 英文改动：英文改为对所有玩家施加
- `intents.json::LANGUAGE_FLOOR_COBALT_DO_NOT_PROVOKE.description` — 事实：“使玩家获得”应为施加
- `intents.json::LANGUAGE_FLOOR_DIPSIA_ELEGANT_DINNER.description` — 事实：缺未被格挡条件与“总共”
- `intents.json::PHILOSOPHY_FLOOR_TWILIGHT_SURVEILLANCE.description` — 事实：缺“永久”
- `intents.json::PHILOSOPHY_FLOOR_TWILIGHT_JUDGMENT.description` — 事实：缺“不受威力影响、可被格挡”
- `intents.json::LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SLENDER_WEB.description` — 事实：“使玩家获得”应为施加
- `intents.json::LITERATURE_FLOOR_BLOODLUST_UNBEARABLE_FINISHER.description` — 事实：缺“在之前的群体攻击后”时机
- `intents.json::BLUE_STAR_NOVA_VOICE.description` — 事实：缺攻击目标“所有玩家”
- `intents.json::NATURAL_FLOOR_HATRED_BRAND.description` — 事实：缺攻击后转移坏蛋标记
- `intents.json::NATURAL_FLOOR_LIGHT_OF_HATRED.description` — 事实：缺按未被格挡伤害总量回复生命
- `intents.json::NATURAL_HERMIT_MY_FRIEND.description` — 其他：半角句点笔误
- `intents.json::NATURAL_TEAR_GRANT.description` — 事实：缺“未处于假死”限制
- `intents.json::NATURAL_SWORD_PIERCING_HEART_SWORD.description` — 事实：缺“即使被格挡”“任何玩家”“直接”
- `intents.json::NATURAL_SWORD_RENDING_HEART_SWORD.description` — 事实：缺“总共”“即使被格挡”“任何玩家”
- `intents.json::NATURAL_SWORD_RUINING_HEART_SWORD.description` — 事实：缺“总共”“即使被格挡”“任何玩家”
- `intents.json::CRYING_CHILDREN_BURNINGCOURAGE` — 名称：原版Burn状态牌应为“灼伤”
- `encounters.json::LITERATURE_FLOOR_LIBERATION_ENCOUNTER.loss` — 残留英文：人名残留英文Laetitia
- `encounters.json::LITERATURE_FLOOR_LIBERATION_ENCOUNTER.topBarHover.description` — 事实：英文为推进解放战，非完成解放
- `encounters.json::NATURAL_FLOOR_LIBERATION_ENCOUNTER.title` — 名称：缺“（普通）”
- `encounters.json::NATURAL_FLOOR_LIBERATION_ENCOUNTER.topBarHover.title` — 名称：缺“（普通）”
- `encounters.json::NATURAL_FLOOR_LIBERATION_ENCOUNTER.loss` — 事实：英文为倒在爱与憎恨之下，内容不同
- `encounters.json::NATURAL_FLOOR_LIBERATION_ENCOUNTER.topBarHover.description` — 事实：英文为挑战普通解放战，非阶段末解放

## 英文疑似有误

- `intents.json::SCAREDY_CAT_GROWL.description` — 英文写“未被格挡时施加易伤”；代码意图徽章为 `IntentBadge.Weak(GrowlWeak)`，实际对所有被命中的玩家施加原版虚弱，无未格挡条件。src/monsters/ScaredyCat/ScaredyCat.cs:226、:317（HitPlayers 不过滤格挡，:355-360）。中文已按代码改为虚弱、无条件。
- `intents.json::SCAREDY_CAT_WARNING_SHOT.description` — 英文写“未被格挡时施加永久易损”；代码对所有被命中的玩家施加（不看是否被格挡），且招式开始时自身先获得 3 层永久强壮（中英都没写）。src/monsters/ScaredyCat/ScaredyCat.cs:321-328、:343-350。旧中文（无条件）与代码一致，KEEP。
- `intents.json::ROAD_HOME_FRIEND_HOME.description` — 英文写“未被格挡时施加混乱”；代码对所有被命中的玩家施加，无未格挡条件。src/monsters/RoadHome/RoadHome.cs:358-377。旧中文与代码一致，KEEP。
- `intents.json::LANGUAGE_FLOOR_SMILING_FACE_SIT.description` — 英文写 Vulnerable；代码施加原版 FrailPower（常量名 SitVulnerable 误导），徽章也是 FrailPower。src/monsters/LanguageFloorLiberation/LanguageFloorSmilingFace.cs:1279-1283、:1873。旧中文“脆弱”正确，KEEP。
- `intents.json::BIG_BIRD_RESCUE.description` — 英文写目标被魅惑时伤害变为 50；代码 RescueCharmedDamage = 60。src/monsters/BigBird/BigBird.cs:72、:405。中文按代码写 60。
- `intents.json::CRYING_CHILDREN_SEARINGPAIN` — 英文能力名写 “Piercing Wound”；代码施加 IoriCardPlayPainPower，其英文标题为 “Laceration”、中文“贯通创伤”。src/reverberation/CryingChildren/CryingChildrenMonsters.cs:239，src/localization/PowerNameMap.cs（贯通创伤 = Laceration）。旧中文用“贯通创伤”，KEEP。

## 拿不准

- `COUNTER_*.title`：只把旧中文笼统写成“反击：策略”的 5 条（Buff/Erosion/Debuff/Status Cards/Summon）按英文改成具体名；“反击：攻势/守势”语义与 Attack/Block 相符，保留。日文线索显示作者最新中文可能已统一改成“反击：攻击/格挡”等，是否统一请人工定。`COMBINED_*.title` 的“攻势/守势”同理保留。
- `LITERATURE_FLOOR_LIBERATION_ENCOUNTER.loss`：人名已改为“蕾蒂希娅”（与 LETICIA 遭遇战一致）；但 monsters.json 的 `LITERATURE_FLOOR_LAETITIA_BOSS.name` 旧中文也残留英文 “Laetitia”（在 G/H 块），需与之统一。
- `GEAR_CHURCH_BRAINWASH`：Ringing 旧中文“昏眩”，施加的是原版 RingingPower（不在 PowerNameMap），原版简中名未能核对。
- `LITERATURE_FLOOR_BLACK_SWAN_OLD_UMBRELLA`：Reflect 旧中文“倒映”，徽章是原版 ReflectPower，原版简中名未能核对。
- `NATURAL_FLOOR_LIBERATION_ENCOUNTER.title/.topBarHover.title` 加了“（普通）”，`.topBarHover.description` 改为“挑战自然层的普通解放战”；如作者无困难版计划可再去掉。
- `BIG_BIRD_RESCUE.description` 写死 60 来自代码常量；英文写 50，需确认以哪个为准。
- `FOX_DEFEND` / `FOX_ENERGY`：src 内找不到这两个意图的实例化，无法用代码核对，按英文补了“本回合”“下回合”。
- `LANGUAGE_FLOOR_COBALT_DO_NOT_PROVOKE`、`LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SLENDER_WEB`：英文为 apply，旧中文“使玩家获得”，按操作性质改为“施加”；英文用 give 的 `LANGUAGE_FLOOR_DIPSIA_OMINOUS_AURA`、`LANGUAGE_FLOOR_DIPSIA_UNBEARABLE_THIRST` 保留“获得”。
- `NATURAL_NIHIL_*`（多段攻击的 description/playerDescription 共 23 条）旧中文用“这名敌人”代替英文的 `{OwnerName}`，不影响显示与事实，保留；若希望多魔法少女场景下主语明确，可统一改回 `{OwnerName}`。

---

## 块 F

# 块 F（events.json）校对记录

## 统计
- 审阅：477 条（全部为 events.json；无 `eng_old`、无 `zhs_old` 为 null 的条目）
- UPDATE：25 条
  - 仅修字面量 `\n`（旧中文写成了反斜杠加 n，游戏里会原样显示；正文与英文一致）：11
  - 字面量 `\n` 连带其他问题：4（引号方向错 1、缺英文第三段 1、按英文单段合并并修结尾引号 2）
  - 内容与当前英文不一致（过时说明、测试提示、缺信息）：8
  - 名称不一致（说话人名 / 同人异译）：2
- 其余 452 条 KEEP。剧情对白（邵/阳与莫伊莱/伊織等）是废墟图书馆官方中文文本，与英文措辞差异属官方译法，不改。

## UPDATE 列表
- events.json::ANCIENT_MAGIC_ALTAR_EVENT.pages.INITIAL.description — 字面量\n改真换行
- events.json::ANCIENT_MAGIC_ALTAR_EVENT.pages.AGREE_LITTLE.description — 字面量\n改真换行
- events.json::ANCIENT_MAGIC_ALTAR_EVENT.pages.AGREE_MUCH.description — 字面量\n改真换行
- events.json::ANCIENT_MAGIC_ALTAR_EVENT.pages.REFUSE.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_4.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_5.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_6.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_7.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_9.description — 字面量\n改真换行
- events.json::WARP_TRAIN_EVENT.pages.PAGE_10.description — 字面量\n改真换行；第一句“杀了他”开引号误为”
- events.json::WARP_TRAIN_EVENT.pages.PAGE_11.description — 字面量\n改真换行
- events.json::SINGING_MACHINE_EVENT.pages.INITIAL.description — 缺英文第三段“实验结论”；字面量\n
- events.json::SINGING_MACHINE_EVENT.pages.APPROACH_FOLLOWUP.description — 字面量\n，按英文合为一段；结尾引号方向错
- events.json::SINGING_MACHINE_EVENT.pages.OBSERVE_FOLLOWUP.description — 字面量\n，按英文合为一段；结尾引号方向错
- events.json::FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.INITIAL.description — 字面量\n改真换行（英文同样有此问题）
- events.json::FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.INITIAL.options.FIGHT.description — 旧为“进入战斗”，缺亡蝶之书/联机全员条件
- events.json::FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.INITIAL.options.FIGHT_LOCKED.description — 旧为模糊“特殊信物”，补遗物名与联机条件
- events.json::XIAO_SPECIAL_GUEST_EVENT.pages.COMPLETE.description — 旧文本与英文结算描述完全不同
- events.json::KALI_SPECIAL_GUEST_EVENT.title — 英文为“Reception —”，非“都市之星邀请函”
- events.json::SPECIAL_GUEST_EVENT.pages.INITIAL.description — 删除英文已无的“测试阶段”提示
- events.json::SPECIAL_GUEST_EVENT.pages.INITIAL.options.RECEIVE_WITH_LIBRARIAN_LOCKED.description — “已锁定”改为“尚未开放”
- events.json::RNFMABJ_SPECIAL_GUEST_EVENT.pages.TOOK_DIRECTIVE.description — 缺“放弃接待”
- events.json::RNFMABJ_SPECIAL_GUEST_EVENT.pages.FLED_WITH_GUILT.description — 缺“伤口愈合”（回满生命）
- events.json::RNFMABJ_SPECIAL_GUEST_STORY.speakers.line_25 — 英文说话人为 Moirai，旧为？？？
- events.json::IORI_SPECIAL_GUEST_STORY.speakers.TanyaLine100 — 同一人“塔尼亚/塔尼娅”统一为塔尼娅

## 英文疑似有误
- events.json::FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.INITIAL.description — 英文里是字面量 `\\n\\n`（`LibraryOfRuina/localization/eng/events.json:71`），游戏会显示反斜杠；jpn/kor 同表也各有 11/12 处字面量 `\n`。中文已用真换行。
- events.json::RNFMABJ_SPECIAL_GUEST_EVENT.pages.INITIAL.options.FLEE_WITH_GUILT.description — 英文删掉了“该来宾不会再次出现”，但代码 `src/specialguests/Rnfmabj/RnfmabjSpecialGuestRegistration.cs:126` 走 `FinishWithoutReceptionAsync`，它在 `src/specialguests/SpecialGuestEventBase.cs:309` 写入 `resolved.<guest>=true`，与“逃跑”选项效果相同，来宾确实不会再出现。中文保留该句（KEEP）。

## 拿不准
- events.json::IORI_SPECIAL_GUEST_STORY.speakers.TanyaLine100 — 统一成“塔尼娅”（与 speakers.Tanya 一致）；若官方译名为“塔尼亚”，两条应一起改。
- events.json::IORI_SPECIAL_GUEST.name 及全表“伊織” — 作者全表一致用日文字形“織”（共 13 处），未改；简中通常写“伊织”，是否统一需作者确认。
- events.json::SINGING_MACHINE_EVENT.pages.APPROACH_FOLLOWUP / OBSERVE_FOLLOWUP.description — 按英文单段结构去掉了作者原本想要的分行（原为字面量 `\n`，游戏内本就显示错误）；若想保留分行，可改回真换行。
- events.json::SPECIAL_GUEST_EVENT.pages.INITIAL.options.RECEIVE_ALONE.description — 旧“本次需要接待N个舞台”与英文“Reception stages: N.”同义，KEEP；仅缺句号。

---

## 块 G

# 块 G 校对笔记（monsters.json 前半）

## 统计
- 审阅：652 条（全部 monsters.json，均逐条对照英文）
- UPDATE：64 条
  - 旧中文留有英文（规则4）：43
  - 名称与英文不一致（规则3）：10
  - 台词含义与英文不一致（规则1）：11
- 变量/BBCode 问题：0（本块描述类条目 8 条均无变量，语义一致）

## UPDATE 列表
- monsters.json::ARNOLD.moves.CHARGE_UP.title — 旧中文为英文原文
- monsters.json::ARNOLD.moves.CHOP_IT_OFF.title — 旧中文为英文原文
- monsters.json::ARNOLD.moves.ENDURE.title — 旧中文为英文原文
- monsters.json::CONSTA.moves.DRIED_UP.title — 旧中文为英文原文
- monsters.json::CONSTA.moves.ENDURE.title — 旧中文为英文原文
- monsters.json::CONSTA.moves.YOU_ONLY_LIVE_ONCE.title — 旧中文为英文原文
- monsters.json::ERI.moves.FEELIN_GOOD.title — 旧中文为英文原文
- monsters.json::ERI.moves.TIME_FOR_A_LITTLE_TEST.title — 旧中文为英文原文
- monsters.json::ERI.moves.WALLOP.title — 旧中文为英文原文（沿用芬恩的“猛击”）
- monsters.json::MCCULLIN.moves.OVERPOWER.title — 旧中文为英文原文
- monsters.json::MCCULLIN.moves.PREEMPTIVE_STRIKE.title — 旧中文为英文原文
- monsters.json::MCCULLIN.moves.TRACK.title — 旧中文为英文原文
- monsters.json::MO.moves.BLOW_IT_UP.title — 旧中文为英文原文
- monsters.json::MO.moves.DODGE_AND_STRIKE.title — 旧中文为英文原文
- monsters.json::MO.moves.ENDURE.title — 旧中文为英文原文
- monsters.json::NAOKI.moves.FEND_THIS_OFF_IF_YOU_CAN.title — 旧中文为英文原文
- monsters.json::NAOKI.moves.MUTILATE.title — 旧中文为英文原文
- monsters.json::NAOKI.moves.QUICKNESS.title — 旧中文为英文原文（沿用能力名“迅捷”）
- monsters.json::OSCAR.moves.HIGH_SPEED_STABBING.title — 旧中文为英文原文
- monsters.json::OSCAR.moves.SPARKING_SPEAR.title — 旧中文为英文原文
- monsters.json::OSCAR.moves.TRANSPIERCE.title — 旧中文为英文原文
- monsters.json::PAMELA.moves.COLLISION.title — 旧中文为英文原文
- monsters.json::PAMELA.moves.HIGH_SPEED_STABBING.title — 旧中文为英文原文
- monsters.json::PAMELA.moves.SPEARED_SWEEP.title — 旧中文为英文原文
- monsters.json::PAMELI.moves.COLLISION.title — 旧中文为英文原文
- monsters.json::PAMELI.moves.HIGH_SPEED_STABBING.title — 旧中文为英文原文
- monsters.json::PAMELI.moves.SPEARED_SWEEP.title — 旧中文为英文原文
- monsters.json::TAEIN.moves.GOIN_FIRST.title — 旧中文为英文原文
- monsters.json::TAEIN.moves.MUTILATE.title — 旧中文为英文原文
- monsters.json::TAEIN.moves.RAMPAGE.title — 旧中文为英文原文
- monsters.json::TODAYS_SHY_LOOK.moves.SHYNESS_1.title — 旧中文为英文；沿用卡牌名“害羞”
- monsters.json::TODAYS_SHY_LOOK.moves.SHYNESS_2.title — 旧中文为英文；沿用卡牌名“害羞”
- monsters.json::TODAYS_SHY_LOOK.moves.TODAYS_EXPRESSION_1.title — 旧中文为英文；沿用卡牌名“今日的表情”
- monsters.json::TODAYS_SHY_LOOK.moves.TODAYS_EXPRESSION_2.title — 同上
- monsters.json::TODAYS_SHY_LOOK.moves.TODAYS_EXPRESSION_3.title — 同上
- monsters.json::TOMERRY.moves.LETS_PLAY.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.LETS_PLAY_1.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.LETS_PLAY_2.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.LETS_PLAY_3.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.LOVE_TOWN_WELCOMES_ALL.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.RUCKUS.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.TRIANGLE_SOUNDS_BETTER.title — 旧中文为英文原文
- monsters.json::TOMERRY.moves.WOULDA_SQUARE_LOOK_NICE.title — 旧中文为英文原文
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.moves.ATTACK.title — 招式名改为 Page Slash（旧：历史突刺）
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.moves.GUARD.title — 招式名改为 Sort Pages（旧：书架守势）
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.moves.SPECIAL.title — 招式名改为 Runaway Chapter（旧：焰色书页）
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.phase1.name — 阶段名改为焦化少女之页
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.phase2.name — 阶段名改为快乐泰迪之页
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.phase3.name — 阶段名改为小帮手之页
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.phase4.name — 阶段名改为精灵女王之页
- monsters.json::HISTORY_FLOOR_PHASE_BOSS.phase5.name — 阶段名改为红舞鞋之页（沿用遗物名）
- monsters.json::HISTORY_FLOOR_WASP_BOSS.name — 英文 Queen Bee，旧中文“黄蜂”，改为蜂后
- monsters.json::THE_FOURTH_MATCH_FLAME.name — 英文第四根火柴，旧中文“最后的火柴”
- monsters.json::SCORCHED_GIRL_MONSTER.backgroundText.2 — 旧中文多出壁炉/晚餐/装饰三句
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.attack.0 — 台词已改写，含义不同
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.attack.1 — 台词已改写，含义不同
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.attack.2 — 台词已改写，含义不同
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.battleStart.0 — 旧中文误抄 backgroundText
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.battleStart.1 — 旧中文误抄 backgroundText
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.battleStart.2 — 旧中文误抄 backgroundText
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.battleStart.3 — 旧中文误抄 backgroundText
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.battleStart.4 — 旧中文误抄 backgroundText
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.lostHope.0 — 台词已改写，含义不同
- monsters.json::SCORCHED_GIRL_MONSTER.dialogue.lostHope.1 — 台词已改写，含义不同

## 英文疑似有误
- 无。本块描述类条目（DESPAIR_KNIGHT 覆甲、FLUTTERING 流血、FORGOTTEN_KNIGHT_SWORD 刺入、HERMIT/STAFF 攻击愤怒侍从、RED_MIST_CARD_SEQUENCE）中英文一致，未发现需要以代码裁决的冲突。

## 拿不准
- 楔子事务所（Arnold/Consta/Eri/Oscar/Pamela/Pameli）、钩子事务所（Naoki/Taein/McCullin）、Mo、Tomerry、今天也很害羞的 43 个招式名：旧中文与作者最新中文（从日文反推）都是英文，本次为新译；如果要对齐《废墟图书馆》官方中文书页名，需要人工替换。“Endure”（只获得格挡）译为“挺住”，没用“忍耐”，避免与能力“忍耐”（Endurance）混淆。
- HISTORY_FLOOR_WASP_BOSS.name 改为“蜂后”后，ancients.json 里仍有旧称“镇压黄蜂”（HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT TIER_4 / TIER_4_LOCKED，在块 B）。那两条英文已改为“Defeat 4 phases”，由块 B 处理；需要确认全表不再残留“黄蜂”。
- SCORCHED_GIRL_MONSTER.backgroundText.2：作者最新中文（日文可见）仍保留“温暖的壁炉……丰盛的晚餐……美美的装饰……”，英文只剩最后一问。已按英文删去，如果想保留人物语气可以恢复。
- THE_FOURTH_MATCH_FLAME.name：作者最新中文仍是“最后的火柴”（日文“決勝戦”），与 HISTORY_FLOOR_LAST_MATCH“Last Match / 最后的火柴”同名；已按英文改为“第四根火柴”，与招式 FOURTH_MATCH_FLAME“第四根火柴”一致。
- ADDICTED_EMPLOYEE.name（Addicted Employee）和 TECHNOLOGY_FLOOR_CHORD_STAFF.name（Bewitched Employee）的中文都是“着魔的职员”，encounters 也用这个名字。含义相近，因此 KEEP；如果需要区分，可以把前者改为“沉迷的职员”。
- GIN.name 旧中文为繁体/日文汉字“銀”，属于作者起的专有名，已 KEEP；如果需要统一简体，可以改为“银”。
- RED_SHOES_LEFT/RIGHT.name 为“左鞋/右鞋”，英文是“Red Shoes - Left/Right”。所指相同，因此 KEEP；如果需要对齐遭遇名“红舞鞋”，可以改为“红舞鞋-左/右”。
- 以下几条与英文用字略有出入，但所指相同，均 KEEP：HISTORY_FLOOR_END_LIGHT_BOSS 的 Rekindle 为“回燃”（其他 Boss 为“复燃”）；SALVADOR 的 Crack of Dawn 为“黎明之火”；GREEN_STEM_HERMIT_YOU_WILL_CRUMBLE 为“手杖标记”（能力名为“手杖”，逐字包含，与 intents.json 写法一致）。

---

## 块 H

# 块 H（monsters.json 后半）校对记录

## 统计
- 审阅条数：652（均为 monsters.json；无 `eng_old`，无 `zhs_old` 为 null 的条目）
- UPDATE 条数：31
- 按原因分类：
  - 残留英文（条件4）：3（YUN 三个招式）
  - 名称/招式名与英文所指不一致（条件3）：25（演奏者一至四 4、坏女巫 2、迪普西亚手势 1、薄暝利爪 1、虚伪王座台词式招式名 3、无法行动 3、伊织招式 11）
  - 台词含义与英文不一致（条件1）：2（此刻的神情背景台词、碧蓝新星信徒开场台词）

## UPDATE 列表
- monsters.json::YUN.moves.COMMANDEERING.title — 残留英文，译为征用
- monsters.json::YUN.moves.PREPARATION.title — 残留英文，沿用芬恩同名招式“备战”
- monsters.json::YUN.moves.YOU_RE_TOO_SLOW.title — 残留英文
- monsters.json::ART_FLOOR_DA_CAPO_PERFORMER.variant1.name — Performer I 与 First Performer 撞名“第一演奏者”
- monsters.json::ART_FLOOR_DA_CAPO_PERFORMER.variant2.name — 同上，按 Performer II 编号
- monsters.json::ART_FLOOR_DA_CAPO_PERFORMER.variant3.name — 同上，按 Performer III 编号
- monsters.json::ART_FLOOR_DA_CAPO_PERFORMER.variant4.name — 同上，按 Performer IV 编号
- monsters.json::ROAD_HOME.moves.ROAD_HOME_PATTERN_THREE.title — wicked witch 是女巫，非巫师（韩日同）
- monsters.json::ROAD_HOME.moves.ROAD_HOME_BAD_WIZARD_GROUP.title — 同上
- monsters.json::LANGUAGE_FLOOR_DIPSIA.moves.COLD_CLAWS.title — Merciless Gesture 是手势非魔爪
- monsters.json::PHILOSOPHY_FLOOR_TWILIGHT.moves.PHILOSOPHY_FLOOR_TWILIGHT_TALON.title — Talon 利爪，旧译“横扫”不符
- monsters.json::FALSE_THRONE.moves.FALSE_THRONE_INSOLENCE.title — How Noisy，旧译“安敢放肆”不符
- monsters.json::FALSE_THRONE.moves.FALSE_THRONE_ALL_SILENT.title — Please Be Gentle，旧译“全员肃静”不符
- monsters.json::FALSE_THRONE.moves.FALSE_THRONE_MANNERS.title — Behave Yourself, Will You? 语义调整
- monsters.json::RNFMABJ.moves.RNFMABJ_HIDDEN.title — Unable to Act 是无法行动
- monsters.json::RNFMABJ_LEFT_HAND.moves.RNFMABJ_HIDDEN.title — 同上
- monsters.json::RNFMABJ_RIGHT_HAND.moves.RNFMABJ_HIDDEN.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.PREY_LOCK.title — Snake’s Prey，旧译“猎物锁定”
- monsters.json::IORI_STAGE_TWO.moves.PREY_LOCK.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.FANG_PENETRATION.title — Venomous Fangs，旧译“尖牙穿透”
- monsters.json::IORI_STAGE_TWO.moves.FANG_PENETRATION.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.PENETRATING_WOUND.title — Laceration，旧译“贯通创伤”
- monsters.json::IORI_STAGE_TWO.moves.PENETRATING_WOUND.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.ENDLESS_FLOW.title — Parry，旧译“流转不息”
- monsters.json::IORI_STAGE_TWO.moves.ENDLESS_FLOW.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.NO_ESCAPE.title — Identify Weakpoint，旧译“无所遁形”
- monsters.json::IORI_STAGE_TWO.moves.NO_ESCAPE.title — 同上
- monsters.json::IORI_STAGE_ONE.moves.PHANTOM_DANCE.title — Mirage Storm，按日韩“幻影乱舞”
- monsters.json::IORI_STAGE_TWO.moves.PHANTOM_DANCE.title — 同上
- monsters.json::LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.2 — generosity 是宽容，非“特权”
- monsters.json::BLUE_STAR_FOLLOWER.banter.START_1 — 苦苦寻觅的主体是“你”，旧译为“我”

伊织招式的依据：英文、韩文（뱀의 표적/독니/열상/흘려보내기/약점 파악/환영난무）、日文三者一致，只有旧中文不同，说明作者后来改过这些招式名。

## 英文疑似有误
- 无能用代码证实的条目。怪物招式名和台词不进 DynamicVars，代码只能确认招式身份，无法裁定措辞。

## 拿不准（均保留旧中文，需人工确认）
- monsters.json::YUNA.moves.EJECT.title — 英文 Eject，韩文“부식”、日文“浸食”、旧中文“侵蚀”都是侵蚀义。代码 `src/guests/DawnOffice/Yuna.cs:218` 的效果是给玩家施加 `LibraryOfRuinaCostReductionPower`，看不出该用哪个名字。英文可能是作者误写，也可能是有意改名。
- monsters.json::NATURAL_FLOOR_NIHIL_BOSS.moves.TYRANTPATH.title — 英文 Road of the King，韩日两版仍是“暴君之路”。同一招式在 GOLD_RUSH 的英文是 Tyrant's Path，英文自身前后不一，所以保留“暴君之路”。
- monsters.json::LITERATURE_FLOOR_LAETITIA_BOSS.name — 旧中文保留英文 Laetitia，其他块（事件、更新日志、遭遇战）的中文也都写 Laetitia，看得出是作者有意为之，因此保留。如果要汉化，需要全表统一。
- monsters.json::LITERATURE_FLOOR_SURPRISE_GIFT_BOX.name — 英文 Gift-wrapped Friend，中文“惊喜礼盒”（韩文“깜짝 상자”、日文“びっくり箱”同旧中文）。块 D 的能力描述也用“惊喜礼盒”，因此保留。
- monsters.json::NATURAL_FLOOR_*::MAGIC_HUMAN/MAGIC_SNAKE/BOSSMAGIC/LOVEMAGIC — 英文都是 Arcana Slave，中文写“魔法之力”，而块 G 的憎恨女王写“奥术奴仆”。块 D 的能力正文也引用“魔法之力！”，所以本块保留。是否全表统一，需要人工决定。
- monsters.json::ROAD_HOME.* — 本块已改为“坏女巫”。块 D 的 `ROAD_HOME_BAD_WIZARD_PASSIVE_POWER.title` 旧中文是“你这个……坏巫师……！”，需要同步改成“坏女巫”。
- monsters.json::YUN.name / YANG.name — 旧中文用繁体“潤”“楊”。块 E 的“潤事务所”和“楊、纱世与銀”也是繁体，看得出是作者的用字风格，因此保留。韩文 달리다（跑）暗示作者最新中文可能已改为简体“润”。
- monsters.json::ART_FLOOR_FINAL_DA_CAPO_BOSS.moves.REVIVE_AND_EMPOWER.title — 英文 Da Capo Returns，中文“返始咏叹调”（日韩两版同为 aria），保留。
- monsters.json::REVERBERATION_EILEEN/GEAR_CHURCH_FOLLOWER.moves.THOUGHTREVEAL.title — 英文 Preach，中文“道破”（韩“설파”、日“説破”同旧中文），保留。
- monsters.json::FALSE_THRONE.moves.FALSE_THRONE_STUN_TRIAL.title — 英文 Stunned，中文“击晕”；韩“흐트러짐”、日“混乱”对应本模组的“混乱”（Stagger）。旧中文与英文一致，所以保留。
