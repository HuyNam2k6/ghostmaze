using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ thống quản lý đa ngôn ngữ (Localization Manager) cho Ghost Maze.
/// Hỗ trợ 4 ngôn ngữ: Tiếng Việt (VI), Tiếng Anh (EN), Tiếng Tây Ban Nha (ES), Tiếng Trung (ZH).
/// Lưu trữ lựa chọn của người chơi qua PlayerPrefs.
/// </summary>
public static class GameLocalizationManager
{
    public enum Language
    {
        Vietnamese = 0, // Tiếng Việt (Mặc định)
        English = 1,    // English
        Spanish = 2,    // Español
        Chinese = 3     // 中文 (简体)
    }

    private const string PREF_KEY = "GhostMaze_SelectedLanguage";
    private static Language _currentLang = Language.Vietnamese;
    private static bool _initialized = false;

    public static event Action<Language> OnLanguageChanged;

    public static Language CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return _currentLang;
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        int saved = PlayerPrefs.GetInt(PREF_KEY, (int)Language.Vietnamese);
        if (Enum.IsDefined(typeof(Language), saved))
        {
            _currentLang = (Language)saved;
        }
        else
        {
            _currentLang = Language.Vietnamese;
        }
        _initialized = true;
    }

    public static void SetLanguage(Language lang)
    {
        EnsureInitialized();
        if (_currentLang == lang) return;

        _currentLang = lang;
        PlayerPrefs.SetInt(PREF_KEY, (int)lang);
        PlayerPrefs.Save();

        Debug.Log($"<color=#2ECC71>[Localization] Đã chuyển đổi ngôn ngữ sang: {GetLanguageNativeName(lang)}</color>");
        OnLanguageChanged?.Invoke(_currentLang);
    }

    public static void CycleNextLanguage()
    {
        EnsureInitialized();
        int next = ((int)_currentLang + 1) % 4;
        SetLanguage((Language)next);
    }

    public static string GetLanguageNativeName(Language lang)
    {
        switch (lang)
        {
            case Language.Vietnamese: return "Tiếng Việt";
            case Language.English: return "English";
            case Language.Spanish: return "Español";
            case Language.Chinese: return "中文 (简体)";
            default: return "Tiếng Việt";
        }
    }

    public static string GetLanguageCode(Language lang)
    {
        switch (lang)
        {
            case Language.Vietnamese: return "VI";
            case Language.English: return "EN";
            case Language.Spanish: return "ES";
            case Language.Chinese: return "ZH";
            default: return "VI";
        }
    }

    public static string GetLanguageFlag(Language lang)
    {
        switch (lang)
        {
            case Language.Vietnamese: return "🇻🇳";
            case Language.English: return "🇬🇧";
            case Language.Spanish: return "🇪🇸";
            case Language.Chinese: return "🇨🇳";
            default: return "🌐";
        }
    }

    // =========================================================================
    // TỪ ĐIỂN DỊCH THUẬT (LOCALIZATION DICTIONARY)
    // =========================================================================

    private static readonly Dictionary<string, string[]> Dict = new Dictionary<string, string[]>
    {
        // Tag 18+ & Cảnh báo độ tuổi
        { "AGE_TAG_18", new string[] {
            "🔞 18+ | KINH DỊ & TƯƠNG TÁC ONLINE",
            "🔞 18+ | HORROR & ONLINE MULTIPLAYER",
            "🔞 18+ | TERROR E INTERACCIÓN EN LÍNEA",
            "🔞 18+ | 恐怖惊悚与在线多人互动"
        } },
        { "AGE_TAG_PILL", new string[] {
            "🔞 18+ MATURE",
            "🔞 18+ MATURE",
            "🔞 18+ ADULTOS",
            "🔞 18+ 成人级"
        } },
        { "AGE_DISCLAIMER_FULL", new string[] {
            "🔞 CẢNH BÁO 18+: Trò chơi có yếu tố rượt đuổi ma quỷ giật mình, không gian tối và voice chat trực tuyến. Phù hợp chính sách Google Play & App Store.",
            "🔞 18+ NOTICE: Contains jumpscares, psychological horror, dark atmosphere, and live voice chat. Compliant with Google Play & App Store policies.",
            "🔞 AVISO 18+: Contiene sustos, terror psicológico, atmósfera oscura y chat de voz en vivo. Conforme a las políticas de Google Play y App Store.",
            "🔞 18+ 警告: 本游戏包含惊悚追逐、突发惊吓、暗黑环境以及实时在线语音交流。完全符合 Google Play 和 App Store 分级政策。"
        } },

        // Header Sảnh
        { "HEADER_SUBTITLE", new string[] {
            "⚡  MÊ CUNG HUYỀN BÍ  ●  SINH TỒN TỔ ĐỘI 4 NGƯỜI  ⚡",
            "⚡  MYSTIC LABYRINTH  ●  4-PLAYER CO-OP SURVIVAL  ⚡",
            "⚡  LABERINTO MÍSTICO  ●  SUPERVIVENCIA COOP DE 4  ⚡",
            "⚡  神秘迷宫  ●  四人组队联机生存  ⚡"
        } },
        { "STATUS_LABEL", new string[] {
            "Trạng thái kết nối:",
            "Connection Status:",
            "Estado de conexión:",
            "连接状态:"
        } },
        { "STATUS_CONNECTED", new string[] {
            "Đã kết nối",
            "Connected",
            "Conectado",
            "已连接"
        } },
        { "STATUS_CONNECTING", new string[] {
            "Đang kết nối...",
            "Connecting...",
            "Conectando...",
            "正在连接..."
        } },
        { "ROOM_ID_LABEL", new string[] {
            "ID Phòng:",
            "Room ID:",
            "ID de Sala:",
            "房间号:"
        } },
        { "ROOM_INPUT_PLACEHOLDER", new string[] {
            "Nhập ID phòng 5 số...",
            "Enter 5-digit room ID...",
            "Introduce ID de 5 dígitos...",
            "输入5位数房间号..."
        } },
        { "BTN_JOIN_ROOM", new string[] {
            "➔  Tham gia",
            "➔  Join Room",
            "➔  Unirse",
            "➔  加入房间"
        } },
        { "BTN_CREATE_ROOM", new string[] {
            "👑  Tạo phòng",
            "👑  Create Room",
            "👑  Crear Sala",
            "👑  创建房间"
        } },

        // 6 Thẻ chế độ chơi
        { "CARD_1_TITLE", new string[] {
            "ĐẤU ĐƠN / GHÉP TRẬN\n<color=#2EE87A><size=15>(Tự động tìm 4 người)</size></color>",
            "SOLO MATCHMAKING\n<color=#2EE87A><size=15>(Auto-find 4 players)</size></color>",
            "EMPAREJAMIENTO SOLITARIO\n<color=#2EE87A><size=15>(Auto buscar 4)</size></color>",
            "单人匹配 / 快速开始\n<color=#2EE87A><size=15>(自动寻找4人局)</size></color>"
        } },
        { "CARD_1_BADGE", new string[] {
            "⭐ GHÉP NHANH ⭐",
            "⭐ QUICK MATCH ⭐",
            "⭐ RÁPIDO ⭐",
            "⭐ 快速匹配 ⭐"
        } },
        { "CARD_1_BTN", new string[] {
            "⚡ TÌM TRẬN NGAY",
            "⚡ FIND MATCH",
            "⚡ BUSCAR PARTIDA",
            "⚡ 立即匹配"
        } },

        { "CARD_2_TITLE", new string[] {
            "ĐẤU TỔ ĐỘI\n<color=#64D2FF><size=15>(Ghép bạn bè & người lẻ)</size></color>",
            "PARTY CO-OP\n<color=#64D2FF><size=15>(Invite friends & fill)</size></color>",
            "MODO EQUIPO\n<color=#64D2FF><size=15>(Amigos y emparejar)</size></color>",
            "组队联机\n<color=#64D2FF><size=15>(邀请好友并补齐)</size></color>"
        } },
        { "CARD_2_BADGE", new string[] {
            "👥 TỔ ĐỘI",
            "👥 PARTY",
            "👥 EQUIPO",
            "👥 组队"
        } },
        { "CARD_2_BTN", new string[] {
            "👥 TẠO TỔ ĐỘI",
            "👥 CREATE PARTY",
            "👥 CREAR EQUIPO",
            "👥 创建小队"
        } },

        { "CARD_3_TITLE", new string[] {
            "BẮT ĐẦU\nMÁY CHỦ",
            "START\nSERVER",
            "INICIAR\nSERVIDOR",
            "启动\n专用服务器"
        } },
        { "CARD_3_BADGE", new string[] {
            "🌐 DEDICATED",
            "🌐 DEDICATED",
            "🌐 DEDICADO",
            "🌐 专属服"
        } },
        { "CARD_3_BTN", new string[] {
            "🌐 CHẠY SERVER",
            "🌐 RUN SERVER",
            "🌐 EJECUTAR",
            "🌐 运行服务器"
        } },

        { "CARD_4_TITLE", new string[] {
            "BẮT ĐẦU\nLÀM CHỦ PHÒNG",
            "START\nAS HOST",
            "INICIAR\nCOMO ANFITRIÓN",
            "房主开房\n主持游戏"
        } },
        { "CARD_4_BADGE", new string[] {
            "👑 CHỦ PHÒNG",
            "👑 HOST",
            "👑 ANFITRIÓN",
            "👑 房主"
        } },
        { "CARD_4_BTN", new string[] {
            "👑 TẠO PHÒNG",
            "👑 HOST ROOM",
            "👑 CREAR SALA",
            "👑 创建房间"
        } },

        { "CARD_5_TITLE", new string[] {
            "BẮT ĐẦU\nLÀM KHÁCH",
            "JOIN\nAS CLIENT",
            "ENTRAR\nCOMO CLIENTE",
            "作为访客\n加入对局"
        } },
        { "CARD_5_BADGE", new string[] {
            "🚪 THEO ID",
            "🚪 BY ID",
            "🚪 POR ID",
            "🚪 按房间号"
        } },
        { "CARD_5_BTN", new string[] {
            "🚪 THAM GIA",
            "🚪 JOIN GAME",
            "🚪 ENTRAR",
            "🚪 加入游戏"
        } },

        { "CARD_6_TITLE", new string[] {
            "TỰ ĐỘNG LÀM CHỦ\nHOẶC LÀM KHÁCH",
            "AUTO HOST\nOR CLIENT",
            "AUTO ANFITRIÓN\nO CLIENTE",
            "自动托管\n或作为访客"
        } },
        { "CARD_6_BADGE", new string[] {
            "⚡ TỰ ĐỘNG",
            "⚡ AUTO",
            "⚡ AUTOMÁTICO",
            "⚡ 自动模式"
        } },
        { "CARD_6_BTN", new string[] {
            "⚡ KẾT NỐI",
            "⚡ AUTO CONNECT",
            "⚡ AUTO CONECTAR",
            "⚡ 自动连接"
        } },

        // Tìm trận trực tiếp (Spinner Overlay)
        { "SPINNER_SEARCHING", new string[] {
            "🔍 ĐANG TÌM TRẬN ĐẤU...",
            "🔍 SEARCHING FOR MATCH...",
            "🔍 BUSCANDO PARTIDA...",
            "🔍 正在匹配玩家..."
        } },
        { "SPINNER_FOUND", new string[] {
            "⚡ ĐÃ TÌM THẤY TRẬN ĐẤU!",
            "⚡ MATCH FOUND!",
            "⚡ ¡PARTIDA ENCONTRADA!",
            "⚡ 比赛已找到！"
        } },
        { "SPINNER_PLAYERS_FOUND", new string[] {
            "👥 Đã tìm thấy: <color=#2EE87A>{0} / 4</color> người chơi (Đủ 4 tự vào)",
            "👥 Found: <color=#2EE87A>{0} / 4</color> players (Auto-start at 4)",
            "👥 Encontrados: <color=#2EE87A>{0} / 4</color> jugadores (Inicio con 4)",
            "👥 已找到: <color=#2EE87A>{0} / 4</color> 名玩家 (满4人自动开始)"
        } },
        { "SPINNER_STARTING", new string[] {
            "🎉 ĐỦ 4/4 NGƯỜI CHƠI! VÀO TRẬN TRONG: {0} GIÂY...",
            "🎉 FULL 4/4 PLAYERS! STARTING IN: {0}s...",
            "🎉 ¡4/4 JUGADORES! INICIANDO EN: {0}s...",
            "🎉 已集齐4/4玩家！对局将在 {0} 秒后开始..."
        } },
        { "SPINNER_BTN_CANCEL", new string[] {
            "🛑 HỦY TÌM TRẬN",
            "🛑 CANCEL SEARCH",
            "🛑 CANCELAR",
            "🛑 取消匹配"
        } },

        // Phòng chờ 4 người
        { "WAITING_TITLE", new string[] {
            "🏰 SẢNH PHÒNG CHỜ TRẬN ĐẤU (4 NGƯỜI CHƠI) 🏰",
            "🏰 MATCH WAITING LOBBY (4 PLAYERS) 🏰",
            "🏰 SALA DE ESPERA (4 JUGADORES) 🏰",
            "🏰 四人组队大厅 🏰"
        } },
        { "WAITING_COPY_BTN", new string[] {
            "📋 Sao chép ID",
            "📋 Copy ID",
            "📋 Copiar ID",
            "📋 复制房间号"
        } },
        { "WAITING_NAME_LABEL", new string[] {
            "Tên của tôi:",
            "My Nickname:",
            "Mi Nombre:",
            "我的昵称:"
        } },
        { "WAITING_SAVE_NAME", new string[] {
            "✔ Lưu tên",
            "✔ Save Name",
            "✔ Guardar",
            "✔ 保存"
        } },
        { "WAITING_YOU", new string[] {
            "⭐ BẠN ĐANG Ở ĐÂY (TÔI) ⭐",
            "⭐ YOU ARE HERE (ME) ⭐",
            "⭐ ESTÁS AQUÍ (TÚ) ⭐",
            "⭐ 你的位置 (自己) ⭐"
        } },
        { "WAITING_READY_YES", new string[] {
            "✔ Đã sẵn sàng",
            "✔ Ready",
            "✔ Listo",
            "✔ 已准备"
        } },
        { "WAITING_READY_NO", new string[] {
            "⏳ Chưa sẵn sàng",
            "⏳ Not Ready",
            "⏳ No listo",
            "⏳ 未准备"
        } },
        { "WAITING_SLOT_EMPTY", new string[] {
            "VỊ TRÍ CÒN TRỐNG",
            "EMPTY SLOT",
            "ESPACIO VACÍO",
            "空闲位置"
        } },
        { "WAITING_BTN_READY", new string[] {
            "✔ BẤM ĐỂ XÁC NHẬN SẴN SÀNG",
            "✔ CLICK TO CONFIRM READY",
            "✔ CLIC PARA CONFIRMAR LISTO",
            "✔ 点击确认准备"
        } },
        { "WAITING_BTN_START_SEARCH", new string[] {
            "⚔️ BẮT ĐẦU TÌM TRẬN (SẴN SÀNG)",
            "⚔️ START MATCHMAKING (READY)",
            "⚔️ INICIAR EMPAREJAMIENTO",
            "⚔️ 开始匹配 (准备)"
        } },
        { "WAITING_BTN_LEAVE", new string[] {
            "🚪 RỜI PHÒNG (F4)",
            "🚪 LEAVE ROOM (F4)",
            "🚪 SALIR DE SALA (F4)",
            "🚪 退出房间 (F4)"
        } },

        // Cài đặt (Settings Tabs)
        { "SETTINGS_TAB_GRAPHICS", new string[] {
            "HÌNH ẢNH",
            "GRAPHICS",
            "GRÁFICOS",
            "画面"
        } },
        { "SETTINGS_TAB_AUDIO", new string[] {
            "ÂM THANH",
            "AUDIO",
            "SONIDO",
            "声音"
        } },
        { "SETTINGS_TAB_CONTROLS", new string[] {
            "ĐIỀU KHIỂN",
            "CONTROLS",
            "CONTROLES",
            "控制"
        } },
        { "SETTINGS_TAB_LANGUAGE", new string[] {
            "NGÔN NGỮ",
            "LANGUAGE",
            "IDIOMA",
            "语言"
        } },
        { "SETTINGS_LANG_HEADER", new string[] {
            "CHỌN NGÔN NGỮ HIỂN THỊ",
            "SELECT DISPLAY LANGUAGE",
            "SELECCIONAR IDIOMA",
            "选择界面语言"
        } },
        { "SETTINGS_LANG_HINT", new string[] {
            "Thay đổi ngôn ngữ cho giao diện, phụ đề và thông báo trong toàn bộ trò chơi.",
            "Changes display language for UI, subtitles, and system notices across the game.",
            "Cambia el idioma para la interfaz, subtítulos y avisos del juego.",
            "更改游戏界面、字幕以及系统提示信息的语言。"
        } }
    };

    /// <summary>
    /// Lấy chuỗi dịch thuật theo từ khóa (Key).
    /// </summary>
    public static string T(string key, string fallback = "")
    {
        EnsureInitialized();
        if (Dict.TryGetValue(key, out string[] translations))
        {
            int idx = (int)_currentLang;
            if (idx >= 0 && idx < translations.Length)
            {
                return translations[idx];
            }
        }
        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }
}
