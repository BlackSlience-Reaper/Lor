namespace LibraryOfRuina.encounters;

/// <summary>
/// 遭遇自带的战斗 BGM。实现它的遭遇在第一个怪物登记时开始播放（见 <see cref="EncounterBgmController.RegisterMonster"/>），
/// 接待遭遇还会按抽到的背景层改用 <see cref="BgmRegistry"/> 里该层的配置。
/// <list type="bullet">
/// <item>实现类必须是 sealed。接口会被子类继承，而这些配置原来按遭遇的运行时类型精确查表，子类没有 BGM；
/// 验证套件 <c>lor-verify-encounter-bgm</c> 检查这一点。</item>
/// <item>用显式实现，写成表达式属性：每次读取都新建配置，不在遭遇实例上留状态，也不进入 <c>MemberwiseClone</c> 的字段。</item>
/// <item>读取发生在怪物登记时（每个怪物一次），<c>HasBgmForEncounter</c> 只判断是否实现接口，不读属性。</item>
/// </list>
/// </summary>
internal interface IEncounterBgmSource
{
    EncounterBgmConfig Bgm { get; }
}
