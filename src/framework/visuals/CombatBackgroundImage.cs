using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// 自定义战斗背景场景里的贴图节点：查找与换图。
/// 各背景控制器只保留自己的节点名、贴图路径与阶段对应关系；阶段到贴图的映射各楼层写法不同（有的按等于、有的按大于等于），
/// 所以不在这里统一。
/// </summary>
internal static class CombatBackgroundImage
{
    /// <summary>
    /// 先按场景唯一名 <c>%nodeName</c> 找，找不到再在背景下递归按节点名找（不要求 owner）。
    /// 没有战斗房间、没有背景或节点不是 <see cref="TextureRect"/> 时返回 null。
    /// 本身不做异常保护；阶段切换前调用的查找要像原来一样在外面套 <c>PresentationGuard</c>。
    /// </summary>
    public static TextureRect? Find(string nodeName)
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        return background.GetNodeOrNull<TextureRect>("%" + nodeName)
            ?? background.FindChild(nodeName, recursive: true, owned: false) as TextureRect;
    }

    /// <summary>
    /// 节点为 null 时什么都不做，也不加载贴图；贴图加载失败时保留节点原来的贴图。
    /// </summary>
    public static void SetTexture(TextureRect? image, string texturePath)
    {
        if (image == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            image.Texture = texture;
        }
    }
}
