using System;
using System.Collections.Generic;
using System.Globalization;

namespace K2
{
    // Локализация интерфейса: исходные строки — английские,
    // для русского языка используется словарь ниже.
    public static class Loc
    {
        static string lang = "en";

        public static string Lang { get { return lang; } }
        public static bool IsRu { get { return lang == "ru"; } }

        // Форматирование чисел и дат под текущий язык.
        public static CultureInfo Culture
        {
            get
            {
                return lang == "ru"
                    ? CultureInfo.GetCultureInfo("ru-RU")
                    : CultureInfo.GetCultureInfo("en-US");
            }
        }

        public static void Set(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            lang = v == "ru" ? "ru" : "en";
        }

        public static string T(string en)
        {
            if (en == null) return null;
            if (lang != "ru") return en;
            string ru;
            return Ru.TryGetValue(en, out ru) ? ru : en;
        }

        // Склонение/подстановка не нужны: все строки-шаблоны собираются в коде.

        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ---------- общие ----------
            { "GPU", "Видеокарта" },
            { "CPU", "Процессор" },
            { "RAM", "ОЗУ" },
            { "Statistics", "Статистика" },
            { "paused", "пауза" },
            { "Load", "Загрузка" },
            { "Temperature", "Температура" },
            { "Hotspot temperature", "Температура Hotspot" },
            { "Hotspot", "Hotspot" },
            { "Memory temperature", "Температура памяти" },
            { "Core temperatures", "Температуры ядер" },
            { "Clock", "Частота" },
            { "Memory clock", "Память GPU" },
            { "Clock (max.)", "Частота (макс.)" },
            { "Clock (eff.)", "Частота (эфф.)" },
            { "Voltage", "Напряжение" },
            { "Power", "Потребление" },
            { "Fan speed", "Обороты вент." },
            { "Fan speed %", "Обороты вент. в %" },
            { "VRAM", "Видеопамять" },
            { "Used", "Использовано" },
            { "Speed", "Скорость" },
            { "DIMM temperature", "Температура плашек" },
            { "Core ", "Ядро " },

            // ---------- статистика ----------
            { "Min/Max GPU", "Min/Max GPU" },
            { "Min/Max CPU", "Min/Max CPU" },
            { "Min/Max VRAM", "Min/Max VRAM" },
            { "Min/Max Hotspot", "Min/Max Hotspot" },
            { "Min/Max GPU voltage", "Min/Max напряж. GPU" },
            { "Min/Max GPU power", "Min/Max потр. GPU" },
            { "Min/Max CPU power", "Min/Max потр. CPU" },

            // ---------- FPS ----------
            { "Frame: ", "Кадр: " },
            { "Window: ", "Окно: " },
            { "Mode: ", "Режим: " },

            // ---------- единицы ----------
            { " ms", " мс" },
            { "— ms", "— мс" },
            { " s", " с" },
            { " min", " мин" },
            { " W", " Вт" },
            { " V", " В" },
            { " GHz", " ГГц" },
            { " MHz", " МГц" },
            { " GB", " ГБ" },
            { " MB", " МБ" },
            { " RPM", " об/мин" },

            // ---------- окно настроек: общее ----------
            { " / Settings / ", " / Настройки / " },
            { "K2 — Settings", "K2 — настройки" },
            { "OVERLAY", "ОВЕРЛЕЙ" },
            { "METRICS", "ПОКАЗАТЕЛИ" },
            { "☕ Support the author", "☕ Автору на кофе" },
            { "Export", "Экспорт" },
            { "Import", "Импорт" },
            { "Reset", "Сброс" },
            { "Cancel", "Отмена" },
            { "Apply", "Применить" },
            { "Apply and close", "Применить и закрыть" },
            { "press keys…", "нажмите клавиши…" },
            { "PREVIEW", "ПРЕДПРОСМОТР" },
            { "PREVIEW · DEMO", "ПРЕДПРОСМОТР · ДЕМО" },
            { "Changed ", "Изменено " },
            { "Esc — cancel    Ctrl+S — apply", "Esc — отмена    Ctrl+S — применить" },
            { "K2 — overlay settings", "K2 — настройки оверлея" },
            { "Language", "Язык" },
            { "Restart K2 to apply the language.", "Перезапустите K2, чтобы применить язык." },
            { "Restart K2", "Перезагрузить K2" },
            { "Restart K2 to apply the language?", "Перезапустить K2, чтобы применить язык?" },
            { "Restart", "Перезагрузить" },
            { "Later", "Позже" },
            { "Could not restart K2. Start it manually.", "Не удалось перезапустить K2. Запустите его вручную." },
            { "Could not open link: ", "Не удалось открыть ссылку: " },
            { "Could not load file: ", "Не удалось загрузить файл: " },
            { "K2 settings (*.ini)|*.ini", "K2 настройки (*.ini)|*.ini" },
            { "General", "Общие" },
            { "Position", "Расположение" },
            { "Appearance", "Внешний вид" },
            { "Hotkeys", "Горячие клавиши" },
            { "Frame time graph", "График времени кадра" },

            // ---------- раздел «Общие» ----------
            { "Main settings", "Основные параметры" },
            { "Enable overlay", "Включить оверлей" },
            { "Run at Windows startup", "Запускать вместе с Windows" },
            { "Check sensors at startup", "Проверка при запуске" },
            { "\"Game only\" mode", "Режим «Только игра»" },
            { "Update interval", "Интервал обновления" },
            { "Units", "Единицы измерения" },
            { "Frequency", "Частота" },
            { "Memory units", "Единицы памяти" },
            { "250 ms", "250 мс" },
            { "500 ms", "500 мс" },
            { "1 s", "1 с" },
            { "2 s", "2 с" },
            { "5 s", "5 с" },
            { "°C (Celsius)", "°C (Цельсий)" },
            { "°F (Fahrenheit)", "°F (Фаренгейт)" },
            { "GHz", "ГГц" },
            { "MHz", "МГц" },
            { "GB", "ГБ" },
            { "MB", "МБ" },
            { "Game only exceptions are marked from the tray icon menu while the overlay is active.", "Исключения режима «только игра» помечаются из меню значка в трее, пока оверлей активен." },

            // ---------- раздел «Расположение» ----------
            { "Overlay position", "Положение оверлея" },
            { "Move overlay", "Переместить оверлей" },
            { "Reset position", "Сбросить положение" },
            { "Overlay anchoring", "Привязка оверлея" },
            { "Primary monitor", "Основной монитор" },
            { "Manual position", "Ручное положение" },
            { "Pin overlay to monitor", "Зафиксировать оверлей на мониторе" },
            { "Monitor ", "Монитор " },
            { "Monitor", "Монитор" },
            { "Screen corner", "Позиция" },
            { "Top right", "Сверху справа" },
            { "Top left", "Сверху слева" },
            { "Bottom right", "Снизу справа" },
            { "Bottom left", "Снизу слева" },
            { "Center", "По центру" },
            { "Offset X", "Отступ X" },
            { "Offset Y", "Отступ Y" },
            { "Block order", "Порядок блоков" },
            { "A pinned overlay stays on the selected monitor. You can still drag it with the mouse — \"Reset position\" removes the pin.", "Зафиксированный оверлей стоит на выбранном мониторе. Перенести можно мышью — «Сбросить положение» снимает фиксацию." },

            // ---------- раздел «Внешний вид» ----------
            { "Shape and size", "Форма и размер" },
            { "Overlay mode", "Режим оверлея" },
            { "Floating", "Плавающий" },
            { "Pinned to position", "Прижатый к позиции" },
            { "Overlay width", "Ширина оверлея" },
            { "Window scale", "Масштаб окна" },
            { "Row height (\"Row\")", "Высота полосы («Строка»)" },
            { "Overlay background", "Фон оверлея" },
            { "Background transparency", "Прозрачность фона" },
            { "Corner radius", "Скругление" },
            { "At 100% the background disappears completely — only the values and overlay elements remain. The corner radius is applied right after \"Apply\".", "При 100% фон полностью исчезает — остаются только показатели и элементы оверлея. Скругление применяется сразу после «Применить»." },
            { "Fonts", "Шрифты" },
            { "Font", "Шрифт" },
            { "Apply font to program windows too", "Применять шрифт и к окнам программы" },
            { "Font size", "Размер шрифта" },
            { "Overlay elements", "Элементы оверлея" },
            { "Show time", "Показывать время" },

            // ---------- раздел «Горячие клавиши» ----------
            { "Global hotkeys", "Глобальные горячие клавиши" },
            { "Show / hide overlay", "Показать / скрыть оверлей" },
            { "Reset statistics", "Сбросить статистику" },
            { "Toggle overlay mode", "Переключить режим оверлея" },
            { "Open settings", "Открыть настройки" },
            { "Start / stop measurement", "Начать / закончить замер" },
            { "If the shortcut is taken by another app, it simply won't register after \"Apply\".", "Если сочетание занято другим приложением, после «Применить» оно просто не зарегистрируется." },
            { "Click the key field and press a new shortcut; Esc cancels input.", "Клик по полю клавиши — нажать новое сочетание, Esc — отменить ввод." },

            // ---------- раздел FPS ----------
            { "Displayed metrics", "Отображать показатели" },
            { "Show FPS", "Показывать FPS" },
            { "Frame time", "Время кадра" },
            { "Process/window", "Процесс/окно" },
            { "API and resolution", "API и разрешение" },
            { "Hide metrics when FPS = N/A", "Скрывать показатели, если FPS = N/A" },
            { "FPS is measured via Desktop Duplication — the frames actually shown on the active window's monitor. With VSync it does not exceed the display refresh rate. A static image produces no new frames, so the counter stays silent and the block is hidden by \"Hide if N/A\". The preview always shows demo FPS values so you can see how the toggles work; the real overlay shows FPS only while it is actually being measured (a game is active).", "FPS считается через Desktop Duplication — кадры, реально показываемые на мониторе активного окна. При VSync не превышает частоту обновления экрана. На статичном изображении счётчик молчит (нет новых кадров), блок при этом скрыт настройкой «Скрывать, если N/A». Предпросмотр всегда показывает демонстрационные значения FPS, чтобы было видно, как работают переключатели; реальный оверлей показывает FPS только тогда, когда он действительно считается (активна игра)." },

            // ---------- раздел GPU ----------
            { "Metrics", "Показатели" },
            { "Show \"GPU\" block", "Показывать блок «Видеокарта»" },
            { "Fan speed, %", "Обороты вент. в %" },
            { "Warning thresholds", "Пороги предупреждений" },
            { "Color-code GPU load", "Цветовая индикация загрузки GPU" },
            { "Warning temperature", "Температура предупреждения" },
            { "Critical temperature", "Температура критическая" },

            // ---------- раздел CPU ----------
            { "Show \"CPU\" block", "Показывать блок «Процессор»" },
            { "Effective clock", "Эффективная частота" },
            { "Cores", "Ядра" },
            { "Per-core clocks", "Частоты по ядрам" },
            { "Per-core load graph", "График загрузки по ядрам" },
            { "Labels under graph", "Подписи под графиком" },
            { "Color-code CPU load", "Цветовая индикация загрузки CPU" },

            // ---------- раздел RAM ----------
            { "Show \"RAM\" block", "Показывать блок ОЗУ" },
            { "Show memory speed in MHz", "Показывать скорость в мегагерцах" },
            { "Memory speed source", "Источник частоты памяти" },
            { "Auto (system data)", "Авто (по данным системы)" },
            { "Auto+ (CPU memory controller limit)", "Авто+ (лимит контроллера CPU)" },
            { "Manual", "Вручную" },
            { "Manual speed, MHz", "Частота вручную, МГц" },
            { "\"Auto+\" caps the SPD rating at the CPU memory controller limit (per Intel spec) — a Xeon v4 with 3200 modules will show the actual 2400. \"Manual\" lets you set the number yourself.", "«Авто+» снижает SPD-номинал до лимита контроллера памяти процессора (по спецификации Intel) — на Xeon v4 с плашками 3200 покажет фактические 2400. «Вручную» — если хочешь задать число самому." },
            { "RAM DIMM temperatures", "Температуры плашек ОЗУ" },
            { "Memory format", "Формат памяти" },
            { "Used / total", "Использовано / всего" },
            { "Used only", "Только использовано" },
            { "Percent", "Проценты" },

            // ---------- раздел «Статистика» ----------
            { "Block contents", "Состав блока" },
            { "Show statistics block", "Показывать блок статистики" },
            { "GPU temperature", "Температура GPU" },
            { "CPU temperature", "Температура CPU" },
            { "VRAM temperature", "Температура VRAM" },
            { "GPU voltage", "Напряжение GPU" },
            { "GPU power", "Потребление GPU" },
            { "CPU power", "Потребление CPU" },
            { "Auto-reset on game change", "Автосброс при смене игры" },
            { "Current session", "Текущая сессия" },

            // ---------- раздел «График времени кадра» ----------
            { "Show graph", "Показывать график" },
            { "Height", "Высота" },
            { "Fill opacity", "Прозрачность заливки" },
            { "Show frame time", "Показывать время кадра" },
            { "The frame time graph will appear once enough samples have been collected.", "График времени кадра появится, когда накопится достаточно замеров." },

            // ---------- трей и сообщения ----------
            { "Show/hide overlay", "Показать/скрыть оверлей" },
            { "Overlay enabled (master)", "Оверлей включён (мастер)" },
            { "Settings…", "Настройки…" },
            { "Always on top", "Поверх окон" },
            { "\"Game only\" exceptions", "Исключения «только игра»" },
            { "Mark active window: ALWAYS a game", "Пометить активное окно: ВСЕГДА игра" },
            { "Mark active window: NEVER a game", "Пометить активное окно: НИКОГДА не игра" },
            { "Exit", "Выход" },
            { "K2 — settings: F10 (double-click to show)", "K2 — настройки: F10 (двойной клик — показать)" },
            { "Could not detect the game window. Start the game and try again.", "Не удалось определить активное окно игры. Откройте игру и повторите." },
            { "core clocks", "частоты ядер" },
            { "K2: not all sensors are available", "K2: не все датчики доступны" },
            { "Missing: ", "Нет: " },
            { ". Check UAC/admin rights or Memory Integrity (HVCI) in Windows Security.", ". Проверь UAC/права админа или «Целостность памяти» (HVCI) в Windows Security." },
            { "initializing sensors…", "инициализация сенсоров…" },
            { "Drag to move. Click the graph for history. Right-click for menu", "Перетащите, чтобы двигать. Клик по графику — история. ПКМ — меню" },

            // ---------- история ----------
            { "K2 — History", "K2 — история" },
            { "History for ", "История за " },
            { "CPU load", "загр CPU" },
            { "GPU load", "загр GPU" },
            { "RAM %", "ОЗУ %" },

            // ---------- журнал и диагностика ----------
            { "Time;CPU load %;CPU t°C;CPU GHz;CPU W;GPU load %;GPU t°C;GPU W;GPU fan %;VRAM GB;RAM GB;RAM %", "Время;ЦПУ загр %;ЦПУ t°C;ЦПУ ГГц;ЦПУ Вт;ГПУ загр %;ГПУ t°C;ГПУ Вт;ГПУ вент %;VRAM ГБ;ОЗУ ГБ;ОЗУ %" },
            { "=== computer opened ===", "=== компьютер открыт ===" },
            { "K2 is already running — its icon is in the notification area.", "K2 уже запущен — его значок находится в области уведомлений." },
        };
    }
}
