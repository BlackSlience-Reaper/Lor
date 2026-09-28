namespace LibraryOfRuina.events;

/// <summary>
/// 楼层解放结算事件。结算事件不回血、不受先古出现规则限制，结束后直接进入下一幕；
/// 这三条由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 统一处理。
/// </summary>
internal interface ILibrarySettlementEvent;
