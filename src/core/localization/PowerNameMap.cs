using System;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.core.localization;

// 各语言正式名称与具体 Power 的显式映射；同名能力保留各自模型和本地化键。
// 名称以当前语言 powers.json（本模组或基础库）的 .title 为准，由脚本核对后写入，勿按位置或近似词推断。
internal static class PowerNameMap
{
    internal sealed record Entry(string TitleKey, Func<PowerModel> GetPower, string Zhs, string Eng, string Jpn, string Kor)
    {
        internal string? Name(string language) => language switch
        {
            "zhs" => Zhs,
            "eng" => Eng,
            "jpn" => Jpn,
            "kor" => Kor,
            _ => null
        };
    }

    internal static readonly Entry[] Entries =
    [
        // Buff；复用原版活力图标，下一回合开始时转化。
        new("NEXT_TURN_VIGOR_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.NextTurnVigorPower>(),
            Zhs: "下回合活力", Eng: "Vigor Next Turn", Jpn: "次ターンの活力", Kor: "다음 턴 활력"),
        // Buff
        new("ALL_RETURNS_TO_VOID_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.NaturalFloorLiberation.AllReturnsToVoidPower>(),
            Zhs: "万物归虚", Eng: "All Returns to Void", Jpn: "万物は虚無へ", Kor: "만물은 허무로"),
        // Buff
        new("NEXT_TURN_STRENGTH_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.guests.DawnOffice.LibraryOfRuinaNextTurnStrength>(),
            Zhs: "下回合力量", Eng: "Next-Turn Strength", Jpn: "次ターン筋力", Kor: "다음 턴 힘"),
        // Debuff
        new("LIBRARY_OF_RUINA_DRAW_CARDS_NEXT_TURN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.LibraryOfRuinaDrawCardsNextTurnPower>(),
            Zhs: "下回合少抽牌", Eng: "Fewer Cards Next Turn", Jpn: "次ターンのドロー減少", Kor: "다음 턴에 카드를 덜 뽑으세요"),
        // Debuff
        new("ART_FLOOR_NEXT_TURN_COLLAPSE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorNextTurnCollapsePower>(),
            Zhs: "下回合崩溃", Eng: "Next Turn Collapse", Jpn: "次ターン崩壊", Kor: "다음 턴 붕괴"),
        // Buff
        new("QUEEN_BEE_NEXT_TURN_STRONG_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.QueenBee.QueenBeeNextTurnStrongPower>(),
            Zhs: "下回合强壮", Eng: "Next-Turn Power Up", Jpn: "次ターン筋力", Kor: "다음 턴 강화"),
        // Debuff
        new("WRATH_SERVANT_NEXT_TURN_CORROSION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.WrathServant.WrathServantNextTurnCorrosionPower>(),
            Zhs: "下回合腐蚀", Eng: "Next-Turn Corrosion", Jpn: "次ターン腐食", Kor: "다음 턴 부식"),
        // Buff
        new("QUEEN_BEE_NEXT_TURN_QUICKNESS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.QueenBee.QueenBeeNextTurnQuicknessPower>(),
            Zhs: "下回合迅捷", Eng: "Next-Turn Quickness", Jpn: "次ターン迅速", Kor: "다음 턴 신속"),
        // Buff
        new("BIG_BAD_WOLF_TEMPORARY_THORNS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.BigBadWolf.BigBadWolfTemporaryThornsPower>(),
            Zhs: "临时荆棘", Eng: "Temporary Thorns", Jpn: "一時的な棘", Kor: "일시적인 가시"),
        // Buff
        new("WARMHEARTED_WOODSMAN_TEMPORARY_THORNS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.WarmheartedWoodsman.WarmheartedWoodsmanTemporaryThornsPower>(),
            Zhs: "临时荆棘", Eng: "Temporary Thorns", Jpn: "一時的な棘", Kor: "임시 가시"),
        // Debuff
        new("ART_FLOOR_FRAGRANCE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorFragrancePower>(),
            Zhs: "余香", Eng: "Fragrance", Jpn: "余香", Kor: "잔향"),
        // Buff
        new("PRESERVED_DAMAGE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.PreservedDamagePower>(),
            Zhs: "保留伤害", Eng: "Preserved Damage", Jpn: "ダメージを保存する", Kor: "피해를 보존하다"),
        // Buff
        new("LIBRARY_CHARGE_POWER_DEFAULT.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryChargePower>(),
            Zhs: "充能", Eng: "Charge", Jpn: "充電", Kor: "충전"),
        // Buff；基础库 eng 表尚无该 title，英文名沿用 jpn/kor 的“充电：R社”格式，当前正文也没有引用。
        new("LIBRARY_CHARGE_POWER_R_CORP.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryChargePower>(),
            Zhs: "充能：R公司", Eng: "Charge: R Corp", Jpn: "充電：R社", Kor: "충전: R사"),
        // Buff
        new("LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.LanguageFloorLiberation.LanguageFloorMimicryFormTwoRegenerationPower>(),
            Zhs: "再生", Eng: "Regenerative", Jpn: "再生", Kor: "재생"),
        // Buff
        new("BIG_BAD_WOLF_CRUEL_CLAWS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.BigBadWolf.BigBadWolfCruelClawsPower>(),
            Zhs: "凶残利爪", Eng: "Cruel Claws", Jpn: "凶暴な爪", Kor: "잔혹한 발톱"),
        // Debuff
        new("LANGUAGE_FLOOR_SCAR_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.LanguageFloorLiberation.LanguageFloorScarPower>(),
            Zhs: "创痕", Eng: "Scar", Jpn: "傷痕", Kor: "상흔"),
        // Buff
        new("SOCIAL_FLOOR_COURAGE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.SocialFloorLiberation.SocialFloorCouragePower>(),
            Zhs: "勇气", Eng: "Courage", Jpn: "勇気", Kor: "용기"),
        // Debuff
        new("NATURAL_FLOOR_BAD_GUY_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.NaturalFloorLiberation.NaturalFloorBadGuyPower>(),
            Zhs: "坏蛋", Eng: "Villain", Jpn: "悪党", Kor: "악당"),
        // Buff
        new("LANGUAGE_FLOOR_MIMICRY_HARDEN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.LanguageFloorLiberation.LanguageFloorMimicryHardenPower>(),
            Zhs: "坚硬", Eng: "Hardness", Jpn: "堅牢", Kor: "단단함"),
        // Debuff
        new("BOUNDARY_THORN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.BoundaryThornPower>(),
            Zhs: "境界之刺", Eng: "Boundary Thorn", Jpn: "境界の棘", Kor: "왕국의 가시"),
        // Buff
        new("INK_OVER_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.DawnOffice.LibraryOfRuinaInkOverPower>(),
            Zhs: "墨蚀", Eng: "Ink Erosion", Jpn: "インク・エロディオン", Kor: "잉크 에칭"),
        // Debuff
        new("ART_FLOOR_IMBALANCED_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorImbalancedPower>(),
            Zhs: "失衡", Eng: "Imbalanced", Jpn: "不均衡", Kor: "불균형"),
        // Debuff
        new("MAGIC_BULLET_COMMISSION_TARGET_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.TechnologyFloorLiberation.MagicBulletCommissionTargetPower>(),
            Zhs: "委托目标", Eng: "Commission Target", Jpn: "依頼対象", Kor: "의뢰 대상"),
        // Debuff
        new("QUEEN_BEE_THREAT_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.QueenBee.QueenBeeThreatPower>(),
            Zhs: "威胁", Eng: "Threat", Jpn: "脅威", Kor: "위협"),
        // Debuff
        new("HISTORY_FLOOR_WASP_SPORE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.HistoryFloorLiberation.HistoryFloorWaspSporePower>(),
            Zhs: "孢子", Eng: "Spore", Jpn: "胞子", Kor: "포자"),
        // Buff
        new("LIBRARY_PROTECTION_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryProtectionPower>(),
            Zhs: "守护", Eng: "Protection", Jpn: "守護", Kor: "보호"),
        // Debuff
        new("SOLEMN_MOURNING_SEAL_ON_ENEMY_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.TechnologyFloorLiberation.SolemnMourningSealOnEnemyPower>(),
            Zhs: "封印", Eng: "Seal", Jpn: "封印", Kor: "봉인"),
        // Debuff
        new("ART_FLOOR_COLLAPSE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorCollapsePower>(),
            Zhs: "崩溃", Eng: "Collapse", Jpn: "崩壊", Kor: "붕괴"),
        // Debuff
        new("XIAO_IGNITE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.specialguests.Xiao.XiaoIgnitePower>(),
            Zhs: "引燃", Eng: "Ignite", Jpn: "引火", Kor: "발화"),
        // Buff
        new("LIBRARY_STRONG_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryStrongPower>(),
            Zhs: "强壮", Eng: "Power Up", Jpn: "筋力", Kor: "강화"),
        // Buff
        new("LIBRARY_ENDURANCE_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryEndurancePower>(),
            Zhs: "忍耐", Eng: "Endurance", Jpn: "忍耐", Kor: "인내"),
        // Debuff
        new("PHILOSOPHY_FLOOR_TWILIGHT_FEAR_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.PhilosophyFloorLiberation.PhilosophyFloorTwilightFearPower>(),
            Zhs: "恐惧", Eng: "Fear", Jpn: "恐怖", Kor: "공포"),
        // Debuff
        new("NOSFERATU_HYDROPHOBIA_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.Nosferatu.NosferatuHydrophobiaPower>(),
            Zhs: "恐水症", Eng: "Hydrophobia", Jpn: "恐水症", Kor: "공수병"),
        // Debuff
        new("NATURAL_FLOOR_NIHIL_HATRED_STATUS.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.NaturalFloorLiberation.NaturalFloorNihilHatredStatus>(),
            Zhs: "憎恶", Eng: "Hatred", Jpn: "憎悪", Kor: "증오"),
        // Debuff
        new("WRATH_SERVANT_STAFF_MARK_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.WrathServant.WrathServantStaffMarkPower>(),
            Zhs: "手杖", Eng: "Staff Mark", Jpn: "ケイン", Kor: "지팡이"),
        // Buff
        new("LIBRARY_STRONG_BLUNT_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryStrongBluntPower>(),
            Zhs: "打击威力增强", Eng: "Blunt Power Up", Jpn: "打撃威力増加", Kor: "타격 위력 증가"),
        // Buff
        new("LIBRARY_BREAK_PROTECTION_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryBreakProtectionPower>(),
            Zhs: "振奋", Eng: "Break Protection", Jpn: "奮起", Kor: "고양"),
        // Buff
        new("LIBRARY_STRONG_SLASH_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryStrongSlashPower>(),
            Zhs: "斩击威力增强", Eng: "Slash Power Up", Jpn: "斬撃威力増加", Kor: "참격 위력 증가"),
        // Debuff
        new("LIBRARY_VULNERABLE_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryVulnerablePower>(),
            Zhs: "易损", Eng: "Vulnerable", Jpn: "脆弱", Kor: "취약"),
        // Debuff
        new("XIAO_STARFIRE_STATUS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.specialguests.Xiao.XiaoStarfireStatusPower>(),
            Zhs: "星火", Eng: "Spark", Jpn: "火種", Kor: "불씨"),
        // Buff
        new("SCARECROW_WISDOM_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom.ScarecrowWisdomPower>(),
            Zhs: "智慧", Eng: "Wisdom", Jpn: "知恵", Kor: "지혜"),
        // Debuff
        new("WRATH_SERVANT_FRIEND_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.WrathServant.WrathServantFriendPower>(),
            Zhs: "朋友", Eng: "Friend", Jpn: "友", Kor: "친구"),
        // Debuff
        new("LIBRARY_BINDING_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryBindingPower>(),
            Zhs: "束缚", Eng: "Binding", Jpn: "束縛", Kor: "속박"),
        // Debuff
        new("QUEEN_BIND_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.QueenOfHatred.LibraryOfRuinaQueenBindPower>(),
            Zhs: "束缚", Eng: "Bind", Jpn: "抑制", Kor: "속박"),
        // Buff
        new("WEDGE_PIERCING_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.WedgeOffice.WedgePiercingPower>(),
            Zhs: "极锐之楔", Eng: "Razor Wedge", Jpn: "非常に鋭いくさび", Kor: "매우 날카로운 웨지"),
        // Debuff
        new("LIBRARY_OF_RUINA_MARK_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.LibraryOfRuinaMarkPower>(),
            Zhs: "标记", Eng: "Mark", Jpn: "マーク", Kor: "표시"),
        // Debuff
        new("LIBRARY_BLEEDING_POWER_DEFAULT.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryBleedingPower>(),
            Zhs: "流血", Eng: "Bleed", Jpn: "出血", Kor: "출혈"),
        // Debuff
        new("LITERATURE_FLOOR_DEEP_WOUND_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.LiteratureFloorLiberation.LiteratureFloorDeepWoundPower>(),
            Zhs: "深度创伤", Eng: "Deep Wound", Jpn: "深い傷", Kor: "깊은 상처"),
        // Debuff
        new("LIBRARY_OF_RUINA_CONFUSION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.LibraryOfRuinaConfusionPower>(),
            Zhs: "混乱", Eng: "Confusion", Jpn: "混乱", Kor: "착란"),
        // Debuff
        new("LIBRARY_BREAK_VULNERABLE_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryBreakVulnerablePower>(),
            Zhs: "混乱易伤", Eng: "Break Vulnerable", Jpn: "混乱脆弱", Kor: "흐트러짐 취약"),
        // Buff
        new("BLOOD_THIRST_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.RedShoes.LibraryOfRuinaBloodThirstPower>(),
            Zhs: "渴血", Eng: "Blood Thirst", Jpn: "血に渇望している", Kor: "피에 목말라"),
        // Debuff
        new("LIBRARY_BURN_POWER_DEFAULT.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryBurnPower>(),
            Zhs: "烧伤", Eng: "Burn", Jpn: "火傷", Kor: "화상"),
        // Buff
        new("FORGOTTEN_AFFECTION_ATTACK_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.HistoryFloorLiberation.ForgottenAffectionAttackPower>(),
            Zhs: "爱意", Eng: "Affection", Jpn: "愛を込めて", Kor: "사랑"),
        // Debuff
        new("FORGOTTEN_AFFECTION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.HistoryFloorLiberation.ForgottenAffectionPower>(),
            Zhs: "爱意", Eng: "Affection", Jpn: "愛を込めて", Kor: "사랑"),
        // Debuff
        new("FANATIC_WORSHIP_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.FanaticWorshipPower>(),
            Zhs: "狂热崇拜", Eng: "Fanatic Worship", Jpn: "狂熱崇拝", Kor: "광신적인 숭배"),
        // Debuff
        new("LITTLE_RED_PREY_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.LittleRedMercenary.LittleRedPreyPower>(),
            Zhs: "猎物", Eng: "Prey", Jpn: "獲物", Kor: "먹이"),
        // Buff
        new("WEDGE_PERSEVERANCE_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.WedgeOffice.WedgePerseverancePower>(),
            Zhs: "百折不挠", Eng: "Perseverance", Jpn: "数えきれない挫折の後も屈しなかった", Kor: "굽히지 않는"),
        // Debuff
        new("LIBRARY_DISARM_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryDisarmPower>(),
            Zhs: "破绽", Eng: "Disarm", Jpn: "隙", Kor: "허점"),
        // Buff
        new("LIBRARY_STRONG_PIERCE_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryStrongPiercePower>(),
            Zhs: "突刺威力增强", Eng: "Pierce Power Up", Jpn: "貫通威力増加", Kor: "관통 위력 증가"),
        // Debuff
        new("JUDGEMENT_BIRD_SIN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.JudgementBird.JudgementBirdSinPower>(),
            Zhs: "罪孽", Eng: "Sin", Jpn: "罪", Kor: "죄"),
        // Debuff
        new("PHILOSOPHY_FLOOR_TWILIGHT_SIN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.PhilosophyFloorLiberation.PhilosophyFloorTwilightSinPower>(),
            Zhs: "罪痕", Eng: "Sin", Jpn: "罪", Kor: "죄"),
        // Debuff
        new("BIG_BIRD_ENERGY_SEAL_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.BigBird.BigBirdEnergySealPower>(),
            Zhs: "能量减少", Eng: "Reduced Energy", Jpn: "エナジー減少", Kor: "에너지 감소"),
        // Debuff
        new("WRATH_SERVANT_CORROSION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.WrathServant.WrathServantCorrosionPower>(),
            Zhs: "腐蚀", Eng: "Corrosion", Jpn: "腐食", Kor: "부식"),
        // Debuff
        new("RNFMABJ_CORROSION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.specialguests.Rnfmabj.RnfmabjCorrosionPower>(),
            Zhs: "腐蚀", Eng: "Erosion", Jpn: "侵食", Kor: "침식"),
        // Buff
        new("ART_FLOOR_PETAL_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorPetalPower>(),
            Zhs: "花瓣", Eng: "Petal", Jpn: "花びら", Kor: "꽃잎"),
        // Debuff
        new("LITERATURE_FLOOR_COCOON_BIND_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.LiteratureFloorLiberation.LiteratureFloorCocoonBindPower>(),
            Zhs: "茧缚", Eng: "Cocoon Bind", Jpn: "繭縛り", Kor: "고치 속박"),
        // Debuff
        new("LIBRARY_WEAK_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryWeakPower>(),
            Zhs: "虚弱", Eng: "Weak", Jpn: "虚弱", Kor: "허약"),
        // Debuff
        new("BIG_BIRD_CHARMED_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.BigBird.BigBirdCharmedPower>(),
            Zhs: "被魅惑", Eng: "Charmed", Jpn: "魅惑", Kor: "매혹"),
        // Debuff
        new("IORI_CARD_PLAY_PAIN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.specialguests.Iori.IoriCardPlayPainPower>(),
            Zhs: "贯通创伤", Eng: "Laceration", Jpn: "裂傷", Kor: "열상"),
        // Debuff
        new("COST_REDUCTION_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.LibraryOfRuinaCostReductionPower>(),
            Zhs: "费用降低", Eng: "Cost Reduction", Jpn: "コスト削減", Kor: "비용 절감"),
        // Buff
        new("ART_FLOOR_ATONEMENT_CROWN_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorAtonementCrownPower>(),
            Zhs: "赎罪之冠", Eng: "Crown of Atonement", Jpn: "贖罪の冠", Kor: "속죄의 왕관"),
        // Buff
        new("LIBRARY_QUICKNESS_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryQuicknessPower>(),
            Zhs: "迅捷", Eng: "Quickness", Jpn: "迅速", Kor: "신속"),
        // Buff
        new("ALL_AROUND_HELPER_SWIFT_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.AllAroundHelper.LibraryOfRuinaAllAroundHelperSwiftPower>(),
            Zhs: "迅捷", Eng: "Swiftness", Jpn: "迅速", Kor: "스위프트"),
        // Buff
        new("ART_FLOOR_QUICKNESS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorQuicknessPower>(),
            Zhs: "迅捷", Eng: "Quickness", Jpn: "迅速", Kor: "스위프트"),
        // Buff
        new("LIBRARY_DEFENSE_POWER_UP_POWER.title",
            static () => ModelDb.Power<global::LibraryLib.Powers.LibraryDefensePowerUpPower>(),
            Zhs: "防御强化", Eng: "Defense Power Up", Jpn: "防御型ダイス威力強化", Kor: "방어형 주사위 위력 강화"),
        // Debuff
        new("ART_FLOOR_DA_CAPO_SOUL_BINDING_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.ArtFloorLiberation.ArtFloorDaCapoSoulBindingPower>(),
            Zhs: "魂缚", Eng: "Soul Binding", Jpn: "魂縛", Kor: "영혼이 묶인"),
        // Debuff
        new("FLUTTERING_FRESH_MEAT_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.HistoryFloorLiberation.FlutteringFreshMeatPower>(),
            Zhs: "鲜肉", Eng: "Fresh Meat", Jpn: "新鮮な肉", Kor: "신선한 고기"),
        // Buff
        new("NOSFERATU_BLOOD_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.content.abnormalities.Nosferatu.NosferatuBloodPower>(),
            Zhs: "鲜血", Eng: "Blood", Jpn: "鮮血", Kor: "선혈"),
        // Debuff
        new("HISTORY_FLOOR_WASP_PARALYSIS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.powers.HistoryFloorLiberation.HistoryFloorWaspParalysisPower>(),
            Zhs: "麻痹", Eng: "Paralysis", Jpn: "麻痺", Kor: "마비"),
        // Debuff
        new("LIBRARY_OF_RUINA_PARALYSIS_POWER.title",
            static () => ModelDb.Power<global::LibraryOfRuina.framework.powers.LibraryOfRuinaParalysisPower>(),
            Zhs: "麻痹", Eng: "Paralysis", Jpn: "麻痺", Kor: "마비"),
    ];
}
