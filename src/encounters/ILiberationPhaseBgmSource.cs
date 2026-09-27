namespace LibraryOfRuina.encounters;

internal interface ILiberationPhaseBgmSource
{
    int CurrentPhase { get; }

    void RefreshLiberationPhaseBgm();
}
