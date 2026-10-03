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
    }
}
