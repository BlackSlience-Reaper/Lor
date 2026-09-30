namespace LibraryOfRuina.content.events.WarpTrain;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class WarpTrainAssets
{
    internal const string FromAPlaceOfLoveBgm = "res://audio/bgm/warp_train/from_a_place_of_love.ogg";
    internal const string LoveTownEventBgm = "res://audio/bgm/warp_train/love_town_event_bgm.ogg";
    internal const string WarpTrainBgm = "res://audio/bgm/warp_train/warp_train_bgm.ogg";
    internal const string CrowdDialogue1Sfx = "res://audio/sfx/warp_train_event/crowd_dialogue_1.ogg";
    internal const string CrowdDialogue2Sfx = "res://audio/sfx/warp_train_event/crowd_dialogue_2.ogg";
    internal const string MaryDialogueSfx = "res://audio/sfx/warp_train_event/mary_dialogue.ogg";
    internal const string TommyDialogueSfx = "res://audio/sfx/warp_train_event/tommy_dialogue.ogg";
    internal const string TownsfolkDialogue1Sfx = "res://audio/sfx/warp_train_event/townsfolk_dialogue_1.ogg";
    internal const string TownsfolkDialogue2Sfx = "res://audio/sfx/warp_train_event/townsfolk_dialogue_2.ogg";
    internal const string BlackScreenTexture = "res://images/events/warp_train_event/black_screen.webp";
    internal const string LoveTownBackgroundTexture = "res://images/events/warp_train_event/love_town_background.webp";
    internal const string WarpTrainBackgroundTexture = "res://images/events/warp_train_event/warp_train_background.webp";
    internal const string TomerryMonsterPrefix = "res://images/monsters/tomerry";
}
