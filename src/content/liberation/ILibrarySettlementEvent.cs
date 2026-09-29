namespace LibraryOfRuina.content.liberation;

/// <summary>
/// 楼层解放结算事件。结算事件不回血（各事件覆写 BeforeEventStarted）、不受先古出现规则限制、
/// 结束后直接进入下一幕；后两条由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 处理。
/// </summary>
internal interface ILibrarySettlementEvent;
