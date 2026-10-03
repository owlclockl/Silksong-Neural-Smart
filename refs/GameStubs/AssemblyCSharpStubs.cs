// Reference stub for Silksong's Assembly-CSharp — signatures copied from the
// decompiled game (CurrencyManager.cs / PlayerData.cs / HeroController.cs).
// Bodies throw: this assembly is used at compile time only and is never shipped.

using System;

// Типы валюты игры (глобальное пространство имён, как в Assembly-CSharp).
public enum CurrencyType
{
    Money = 0,
    Shard = 1,
}

public sealed class PlayerData
{
    public static PlayerData instance
    {
        get { throw new NotImplementedException(); }
        set { throw new NotImplementedException(); }
    }

    public int geo;
    public int ShellShards;
    public bool isInventoryOpen;

    public void AddGeo(int amount) { throw new NotImplementedException(); }
    public void TakeGeo(int amount) { throw new NotImplementedException(); }
}

public sealed class CurrencyManager
{
    public static void AddGeo(int amount) { throw new NotImplementedException(); }
    public static void TakeGeo(int amount) { throw new NotImplementedException(); }
    public static void AddGeoQuietly(int amount) { throw new NotImplementedException(); }

    public static int GetCurrencyAmount(CurrencyType type) { throw new NotImplementedException(); }
}

public sealed class HeroController
{
    public static HeroController instance
    {
        get { throw new NotImplementedException(); }
    }

    public void AddShards(int amount) { throw new NotImplementedException(); }
}
