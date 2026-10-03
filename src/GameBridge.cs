using System;

namespace RosaryShare
{
    /// <summary>
    /// Безопасная обёртка над игровой валютой Silksong.
    /// Бусины (розарии) в коде игры — это поле PlayerData.geo, а начислением
    /// и списанием управляет CurrencyManager (с анимацией счётчика в HUD).
    /// </summary>
    internal static class GameBridge
    {
        /// <summary>Находимся ли мы в загруженном сохранении (есть играбельная Хорнет).</summary>
        public static bool InGame
        {
            get
            {
                try
                {
                    return HeroController.instance != null && PlayerData.instance != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Текущее количество бусин у локального игрока.</summary>
        public static int GetGeo()
        {
            try
            {
                return PlayerData.instance != null ? PlayerData.instance.geo : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Начислить бусины (с анимацией счётчика, с запасными путями).</summary>
        public static void AddGeo(int amount)
        {
            if (amount <= 0 || PlayerData.instance == null) return;

            try
            {
                CurrencyManager.AddGeo(amount);
                return;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogWarning("CurrencyManager.AddGeo failed: " + e.Message);
            }

            try
            {
                CurrencyManager.AddGeoQuietly(amount);
                return;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogWarning("CurrencyManager.AddGeoQuietly failed: " + e.Message);
            }

            try
            {
                PlayerData.instance.AddGeo(amount);
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogError("Failed to add beads: " + e);
            }
        }

        /// <summary>Списать бусины. Вызывающий код обязан заранее проверить баланс.</summary>
        public static void TakeGeo(int amount)
        {
            if (amount <= 0 || PlayerData.instance == null) return;

            try
            {
                CurrencyManager.TakeGeo(amount);
                return;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogWarning("CurrencyManager.TakeGeo failed: " + e.Message);
            }

            try
            {
                PlayerData.instance.TakeGeo(amount);
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogError("Failed to take beads: " + e);
            }
        }
    }
}
