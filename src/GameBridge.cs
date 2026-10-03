using System;
using System.Reflection;
using UnityEngine;

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

        /// <summary>Текущее количество осколков панциря.</summary>
        public static int GetShards()
        {
            try { return PlayerData.instance != null ? PlayerData.instance.ShellShards : 0; }
            catch { return 0; }
        }

        /// <summary>Изменить запас осколков через игровой HeroController.</summary>
        public static void AddShards(int amount)
        {
            if (amount == 0 || HeroController.instance == null) return;
            try { HeroController.instance.AddShards(amount); }
            catch (Exception e) { RosarySharePlugin.LogError("Failed to change shell shards: " + e); }
        }

        // ------------------------------------------------------------------
        //  Блокировка игрового ввода, пока открыто меню мода
        //  (иначе Хорнет бегает и атакует, пока игрок кликает по окну).
        //  Делается рефлексией по классу InputHandler игры — если его нет
        //  или сигнатуры изменились, мод просто продолжает работать без блокировки.
        // ------------------------------------------------------------------

        private static bool _inputResolveTried;
        private static object _inputHandler;
        private static MethodInfo _stopAcceptingInput;
        private static MethodInfo _startAcceptingInput;
        private static bool _inputBlocked;
        private static float _reassertAt;
        private static float _unblockRetryUntil;

        /// <summary>Заблокирован ли сейчас игровой ввод нашим меню.</summary>
        public static bool InputBlocked
        {
            get { return _inputBlocked; }
        }

        /// <summary>Включить или снять блокировку игрового ввода.</summary>
        public static void SetInputBlocked(bool blocked)
        {
            if (blocked && !ModConfig.BlockGameInput)
                blocked = false;

            if (_inputBlocked == blocked) return;
            _inputBlocked = blocked;

            if (ApplyInputBlock(blocked))
            {
                _reassertAt = Time.unscaledTime + 0.5f;
                _unblockRetryUntil = 0f;
                return;
            }

            _inputBlocked = false;

            // если снять блокировку не удалось (сменилась сцена, объект пересоздан),
            // будем пытаться ещё несколько секунд: игрок не должен остаться без управления
            if (!blocked)
                _unblockRetryUntil = Time.unscaledTime + 5f;
        }

        /// <summary>
        /// Повторно подтверждает блокировку: игра может сама включить ввод обратно
        /// (смена сцены, выход из паузы), поэтому раз в полсекунды напоминаем.
        /// </summary>
        public static void TickInputBlock()
        {
            float now = Time.unscaledTime;

            if (!_inputBlocked)
            {
                if (_unblockRetryUntil > 0f && now < _unblockRetryUntil)
                {
                    if (ApplyInputBlock(false)) _unblockRetryUntil = 0f;
                }
                else if (_unblockRetryUntil > 0f)
                {
                    _unblockRetryUntil = 0f;
                }
                return;
            }

            if (now < _reassertAt) return;
            _reassertAt = now + 0.5f;
            ApplyInputBlock(true);
        }

        private static bool ApplyInputBlock(bool blocked)
        {
            if (!ResolveInputHandler()) return false;

            try
            {
                MethodInfo method = blocked ? _stopAcceptingInput : _startAcceptingInput;
                if (method == null) return false;
                method.Invoke(_inputHandler, null);
                return true;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("InputHandler call failed: " + e.Message);
                _inputHandler = null;
                return false;
            }
        }

        private static bool ResolveInputHandler()
        {
            // объект мог быть уничтожен при смене сцены — ищем заново
            if (_inputHandler != null)
            {
                UnityEngine.Object asUnityObject = _inputHandler as UnityEngine.Object;
                // == null у Unity-объектов истинно и для уже уничтоженных компонентов
                if (!ReferenceEquals(asUnityObject, null) && asUnityObject == null)
                    _inputHandler = null;
            }

            if (_inputHandler != null) return true;

            try
            {
                Type type = FindInputHandlerType();
                if (type == null)
                {
                    if (!_inputResolveTried)
                    {
                        _inputResolveTried = true;
                        RosarySharePlugin.LogDebug("InputHandler type was not found: the game input will stay active while the menu is open.");
                    }
                    return false;
                }

                _stopAcceptingInput = type.GetMethod("StopAcceptingInput", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _startAcceptingInput = type.GetMethod("StartAcceptingInput", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (_stopAcceptingInput == null || _startAcceptingInput == null) return false;

                object instance = ReadStaticMember(type, "Instance") ?? ReadStaticMember(type, "instance");
                if (instance == null)
                    instance = UnityEngine.Object.FindObjectOfType(type);

                if (instance == null) return false;

                _inputHandler = instance;
                return true;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("InputHandler lookup failed: " + e.Message);
                return false;
            }
        }

        private static Type FindInputHandlerType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type direct;
                try
                {
                    direct = assemblies[i].GetType("InputHandler", false);
                }
                catch
                {
                    continue;
                }

                if (direct != null &&
                    direct.GetMethod("StopAcceptingInput", BindingFlags.Public | BindingFlags.Instance) != null)
                    return direct;
            }

            return null;
        }

        private static object ReadStaticMember(Type type, string name)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (property != null && property.CanRead)
                    return property.GetValue(null, null);

                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                    return field.GetValue(null);
            }
            catch
            {
                // приватные статики могут бросать в момент загрузки сцены — не страшно
            }

            return null;
        }
    }
}
