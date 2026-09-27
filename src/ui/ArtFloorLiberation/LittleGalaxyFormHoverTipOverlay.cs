// #nullable enable
// using System;
// using System.Linq;
// using Godot;
// using HarmonyLib;
// using MegaCrit.Sts2.Core.Combat;
// using MegaCrit.Sts2.Core.Entities.Creatures;
// using MegaCrit.Sts2.Core.Helpers;
// using MegaCrit.Sts2.Core.HoverTips;
// using MegaCrit.Sts2.Core.Localization;
// using MegaCrit.Sts2.Core.Nodes.Combat;
// using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
// using MegaCrit.Sts2.Core.Nodes.HoverTips;
// using MegaCrit.Sts2.Core.Nodes.Rooms;

// namespace LibraryOfRuina;

// [HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
// internal static class LittleGalaxyFormHoverTipOverlayPatch
// {
//     private const string OverlayNodeName = "LibraryOfRuinaLittleGalaxyFormHoverTipOverlay";

//     private static void Postfix(NCombatRoom __instance, CombatState state)
//     {
//         if (state.Encounter is not ArtFloorLiberationEncounter
//             || __instance.Ui.GetNodeOrNull<LittleGalaxyFormHoverTipOverlay>(OverlayNodeName) != null)
//         {
//             return;
//         }

//         __instance.Ui.AddChildSafely(new LittleGalaxyFormHoverTipOverlay(state)
//         {
//             Name = OverlayNodeName
//         });
//     }
// }

// internal partial class LittleGalaxyFormHoverTipOverlay : Control
// {
//     private const float BadgeGap = 10f;
//     private const float HeadFallbackY = 0.12f;

//     private readonly CombatState _combatState;
//     private readonly LittleGalaxyFormBadge _badge = new();
//     private int _frameCounter;

//     public LittleGalaxyFormHoverTipOverlay(CombatState combatState)
//     {
//         _combatState = combatState;
//         MouseFilter = MouseFilterEnum.Ignore;
//         TopLevel = false;
//     }

//     public override void _Ready()
//     {
//         this.AddChildSafely(_badge);
//         Visible = false;
//     }

//     public override void _Process(double delta)
//     {
//         _frameCounter++;
//         if ((_frameCounter & 3) != 0)
//         {
//             return;
//         }

//         Refresh();
//     }

//     private void Refresh()
//     {
//         Creature? bossCreature = _combatState.Enemies.FirstOrDefault(static creature =>
//             creature is { IsAlive: true, Monster: ArtFloorLittleGalaxyBoss });
//         if (bossCreature?.Monster is not ArtFloorLittleGalaxyBoss boss
//             || !TryGetBadgePosition(bossCreature, out Vector2 position))
//         {
//             Visible = false;
//             _badge.HideHoverTip();
//             return;
//         }

//         Visible = true;
//         _badge.UpdateFromBoss(boss);
//         _badge.GlobalPosition = position;
//     }

//     private static bool TryGetBadgePosition(Creature boss, out Vector2 position)
//     {
//         position = Vector2.Zero;
//         NCombatRoom? room = NCombatRoom.Instance;
//         NCreature? creatureNode = room?.GetCreatureNode(boss);
//         Rect2 anchorRect = creatureNode?.Hitbox.GetGlobalRect() ?? default;
//         if (room == null || anchorRect.Size.X <= 0f || anchorRect.Size.Y <= 0f)
//         {
//             return false;
//         }

//         Vector2 headAnchor = creatureNode?.Visuals?.TalkPosition?.GlobalPosition
//             ?? new Vector2(
//                 anchorRect.Position.X + anchorRect.Size.X * 0.5f,
//                 anchorRect.Position.Y + anchorRect.Size.Y * HeadFallbackY);
//         Vector2 viewportSize = room.GetViewportRect().Size;
//         float x = headAnchor.X - LittleGalaxyFormBadge.BadgeSize * 0.5f;
//         float y = headAnchor.Y - LittleGalaxyFormBadge.BadgeSize - BadgeGap;
//         position = new Vector2(
//             Math.Clamp(x, 0f, Math.Max(0f, viewportSize.X - LittleGalaxyFormBadge.BadgeSize)),
//             Math.Clamp(y, 0f, Math.Max(0f, viewportSize.Y - LittleGalaxyFormBadge.BadgeSize)));
//         return true;
//     }
// }

// internal sealed partial class LittleGalaxyFormBadge : Control
// {
//     public const float BadgeSize = 56f;
//     private const float IconSize = 42f;
//     private static readonly Vector2 BadgeSizeVector = new(BadgeSize, BadgeSize);
//     private static readonly Vector2 IconOffset = new((BadgeSize - IconSize) * 0.5f, (BadgeSize - IconSize) * 0.5f);
//     private static readonly Color BackgroundColor = new(0.035f, 0.030f, 0.055f, 0.88f);
//     private static readonly Color BorderColor = new(1.0f, 0.78f, 0.18f, 1f);

//     private readonly TextureRect _icon = new();
//     private string _titleKey = ArtFloorLittleGalaxyBoss.GetFormTitleKey(ArtFloorLittleGalaxyForm.Healing);
//     private string _descriptionKey = ArtFloorLittleGalaxyBoss.GetFormDescriptionKey(ArtFloorLittleGalaxyForm.Healing);
//     private string _iconPath = ArtFloorLittleGalaxyBoss.GetFormIconPath(ArtFloorLittleGalaxyForm.Healing);
//     private bool _isHovering;

//     public LittleGalaxyFormBadge()
//     {
//         CustomMinimumSize = BadgeSizeVector;
//         Size = BadgeSizeVector;
//         PivotOffset = Size * 0.5f;
//         MouseFilter = MouseFilterEnum.Pass;
//         FocusMode = FocusModeEnum.None;
//     }

//     public override void _Ready()
//     {
//         _icon.Name = "Icon";
//         _icon.Position = IconOffset;
//         _icon.Size = new Vector2(IconSize, IconSize);
//         _icon.MouseFilter = MouseFilterEnum.Ignore;
//         _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
//         _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
//         AddChild(_icon);

//         Connect(Control.SignalName.FocusEntered, Callable.From(OnFocus));
//         Connect(Control.SignalName.FocusExited, Callable.From(OnUnfocus));
//         Connect(Control.SignalName.MouseEntered, Callable.From(OnFocus));
//         Connect(Control.SignalName.MouseExited, Callable.From(OnUnfocus));
//         RefreshTexture();
//     }

//     public override bool _HasPoint(Vector2 point)
//     {
//         Vector2 center = BadgeSizeVector * 0.5f;
//         return point.DistanceSquaredTo(center) <= MathF.Pow(BadgeSize * 0.5f, 2f);
//     }

//     public override void _Draw()
//     {
//         Vector2 center = BadgeSizeVector * 0.5f;
//         DrawCircle(center, BadgeSize * 0.5f, BackgroundColor);
//         DrawArc(center, BadgeSize * 0.5f - 1.5f, 0f, Mathf.Tau, 80, BorderColor, 3f, antialiased: true);
//     }

//     public void UpdateFromBoss(ArtFloorLittleGalaxyBoss boss)
//     {
//         string nextTitleKey = boss.CurrentFormTitleKey;
//         string nextDescriptionKey = boss.CurrentFormDescriptionKey;
//         string nextIconPath = boss.CurrentFormIconPath;
//         if (_titleKey == nextTitleKey
//             && _descriptionKey == nextDescriptionKey
//             && _iconPath == nextIconPath)
//         {
//             return;
//         }

//         _titleKey = nextTitleKey;
//         _descriptionKey = nextDescriptionKey;
//         _iconPath = nextIconPath;
//         RefreshTexture();
//         if (_isHovering)
//         {
//             ShowHoverTip();
//         }
//     }

//     public void HideHoverTip()
//     {
//         _isHovering = false;
//         NHoverTipSet.Remove(this);
//     }

//     private void OnFocus()
//     {
//         _isHovering = true;
//         ShowHoverTip();
//     }

//     private void OnUnfocus()
//     {
//         HideHoverTip();
//     }

//     private void ShowHoverTip()
//     {
//         NHoverTipSet.Remove(this);
//         NHoverTipSet? tipSet = NHoverTipSet.CreateAndShow(
//             this,
//             new HoverTip(new LocString("monsters", _titleKey), new LocString("monsters", _descriptionKey)),
//             HoverTipAlignment.Center);
//         if (tipSet != null)
//         {
//             tipSet.GlobalPosition = GlobalPosition + Vector2.Down * (BadgeSize + 8f);
//         }
//     }

//     private void RefreshTexture()
//     {
//         _icon.Texture = ResourceLoader.Load<Texture2D>(_iconPath, null, ResourceLoader.CacheMode.Reuse);
//     }
// }
