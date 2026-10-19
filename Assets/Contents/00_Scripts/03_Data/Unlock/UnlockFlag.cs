using System;

[Flags]
public enum UnlockFlag
{
    None = 0,
    BeatVolcanoBiom    = 1 << 0,
    Spend7777GoldInRun = 1 << 1,
    MaxHPThreshold300  = 1 << 2,
    BiomeClear         = 1 << 3,
}