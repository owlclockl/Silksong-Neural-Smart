using System.Globalization;

namespace RosaryShare
{
    /// <summary>
    /// Миниатюрная локализация интерфейса: Russian / English.
    /// Выбирается настройкой Language (Auto — по системной культуре).
    /// </summary>
    internal static class Texts
    {
        public enum Lang
        {
            Ru,
            En,
        }

        public static Lang Current { get; private set; }

        static Texts()
        {
            Current = Lang.Ru;
        }

        public static void Reload()
        {
            string wanted = (ModConfig.Language ?? "Auto").Trim();

            if (wanted.Equals("Russian", System.StringComparison.OrdinalIgnoreCase) ||
                wanted.Equals("ru", System.StringComparison.OrdinalIgnoreCase))
            {
                Current = Lang.Ru;
                return;
            }

            if (wanted.Equals("English", System.StringComparison.OrdinalIgnoreCase) ||
                wanted.Equals("en", System.StringComparison.OrdinalIgnoreCase))
            {
                Current = Lang.En;
                return;
            }

            // Auto — по системной локали
            Current = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ru" ? Lang.Ru : Lang.En;
        }

        /// <summary>Выбор строки по текущему языку.</summary>
        public static string T(string ru, string en)
        {
            return Current == Lang.Ru ? ru : en;
        }

        // ---- Частые строки ----

        public static string WindowTitle
        {
            get { return T("RosaryShare — обмен бусинами", "RosaryShare — bead sharing"); }
        }

        public static string StatusNoMod
        {
            get { return T("Мультиплеерный мод не найден", "Multiplayer mod not detected"); }
        }

        public static string StatusNoLobby
        {
            get { return T("Вы не в лобби. Создайте лобби в меню мультиплеерного мода.", "Not in a lobby. Create/join a lobby via the multiplayer mod menu."); }
        }

        public static string PlayersHeader
        {
            get { return T("Игроки", "Players"); }
        }

        public static string AmountHeader
        {
            get { return T("Сумма", "Amount"); }
        }

        public static string HistoryHeader
        {
            get { return T("Журнал", "History"); }
        }

        public static string NoPlayers
        {
            get { return T("Других игроков в лобби пока нет", "No other players in the lobby yet"); }
        }

        public static string AllBeads
        {
            get { return T("Всё", "All"); }
        }

        public static string Close
        {
            get { return T("Закрыть", "Close"); }
        }

        public static string NeedSave
        {
            get { return T("Загрузите сохранение, чтобы передавать бусины", "Load a save file to transfer beads"); }
        }

        public static string BalanceFormat
        {
            get { return T("Ваши бусины: {0}", "Your beads: {0}"); }
        }

        public static string LobbyCountFormat
        {
            get { return T("В лобби: {0} чел.", "In lobby: {0}"); }
        }

        // ---- Меню в стиле игры ----

        /// <summary>Заголовок меню (выводится вразрядку заглавными).</summary>
        public static string MenuTitle
        {
            get { return T("ОБМЕН РЕСУРСАМИ", "RESOURCE SHARING"); }
        }

        public static string MenuSubtitle
        {
            get { return T("Бусины и осколки странников", "Beads and shards of the wanderers"); }
        }

        public static string SendVerb
        {
            get { return T("Передать", "Bestow"); }
        }

        public static string SendFormat
        {
            get { return T("Передать {0} → {1}", "Bestow {0} → {1}"); }
        }

        public static string ChoosePrompt
        {
            get { return T("Выберите игрока и сумму", "Choose a player and an amount"); }
        }

        public static string CustomAmount
        {
            get { return T("Своё", "Custom"); }
        }

        public static string AvailableFormat
        {
            get { return T("Доступно: {0}", "Available: {0}"); }
        }

        public static string PendingFormat
        {
            get { return T("в пути: {0}", "pending: {0}"); }
        }

        public static string EmptyJournal
        {
            get { return T("Переводов ещё не было", "No transfers yet"); }
        }

        public static string HintSelect
        {
            get { return T("Выбор", "Select"); }
        }

        public static string HintConfirm
        {
            get { return T("Выбрать", "Choose"); }
        }

        public static string HintClose
        {
            get { return T("Закрыть", "Close"); }
        }

        public static string HintAmount
        {
            get { return T("Сумма", "Amount"); }
        }

        public static string HintJournal
        {
            get { return T("Журнал", "Journal"); }
        }

        public static string HintQuickSend
        {
            get { return T("Быстрая отправка", "Quick send"); }
        }

        public static string HintOpen
        {
            get { return T("Открыть окно", "Open the window"); }
        }

        public static string GamepadReady
        {
            get { return T("Геймпад подключён", "Gamepad connected"); }
        }
    }
}
