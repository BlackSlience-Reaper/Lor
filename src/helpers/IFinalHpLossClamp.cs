using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.helpers;

/// <summary>
/// 所有普通生命损失修正完成后执行的锁血。预览也会调用，禁止修改战斗状态。
/// </summary>
public interface IFinalHpLossClamp
{
    decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource);
}
