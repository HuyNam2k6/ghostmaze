using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ thống quản lý đa ngôn ngữ toàn diện (Global Localization Manager) cho Ghost Maze.
/// Hỗ trợ 4 ngôn ngữ: Tiếng Việt (VI), Tiếng Anh (EN), Tiếng Tây Ban Nha (ES), Tiếng Trung (ZH).
/// Lưu trữ lựa chọn của người chơi qua PlayerPrefs.
/// Phát sự kiện OnLanguageChanged khi đổi ngôn ngữ để toàn bộ màn hình (Lobby, HUD, Settings, In-game) đồng loạt cập nhật.
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

        Debug.Log($"<color=#2ECC71>[Localization] Đã chuyển đổi ngôn ngữ toàn cầu sang: {GetLanguageNativeName(lang)}</color>");
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
            case Language.Spanish: return "Spanish (Español)";
            case Language.Chinese: return "Chinese (中文)";
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
    // TỪ ĐIỂN DỊCH THUẬT TOÀN DIỆN (GLOBAL LOCALIZATION DICTIONARY)
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
        { "PLAYER_ME", new string[] {
            "👤 Tôi",
            "👤 Me",
            "👤 Yo",
            "👤 我"
        } },

        // 6 Thẻ chế độ chơi Sảnh
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
        { "WAITING_LEAVE_BTN", new string[] {
            "⬅  Rời Phòng / Quay Lại Sảnh",
            "⬅  Leave Room / Back to Lobby",
            "⬅  Salir / Volver a la Sala",
            "⬅  退出房间 / 返回大厅"
        } },
        { "LOBBY_TAB_MULTIPLAYER", new string[] {
            "👥 Đa Người Chơi",
            "👥 Multiplayer",
            "👥 Multijugador",
            "👥 多人游戏"
        } },
        { "LOBBY_TAB_SOLO", new string[] {
            "👤 Chơi Đơn",
            "👤 Single Player",
            "👤 Un Jugador",
            "👤 单人游戏"
        } },
        { "LOBBY_TAB_SETTINGS", new string[] {
            "⚙️ Cài Đặt",
            "⚙️ Settings",
            "⚙️ Ajustes",
            "⚙️ 设置"
        } },
        { "LOBBY_CARD1_TITLE", new string[] {
            "Đấu đơn",
            "Solo Match",
            "Duelo Individual",
            "单人对决"
        } },
        { "LOBBY_CARD1_SUB", new string[] {
            "Ghép trận\nmột người chơi",
            "Matchmaking\nfor 1 player",
            "Emparejamiento\npara 1 jugador",
            "单人匹配\n快速对决"
        } },
        { "LOBBY_CARD1_BTN", new string[] {
            "Tham gia ngay ❯",
            "Join Now ❯",
            "Unirse Ahora ❯",
            "立即加入 ❯"
        } },
        { "LOBBY_CARD2_TITLE", new string[] {
            "Đấu đội",
            "Team Match",
            "Duelo en Equipo",
            "组队竞技"
        } },
        { "LOBBY_CARD2_SUB", new string[] {
            "Cùng bạn bè\nchơi nhiều người",
            "Play multiplayer\nwith friends",
            "Juega en grupo\ncon amigos",
            "与好友组队\n畅玩多人"
        } },
        { "LOBBY_CARD2_BTN", new string[] {
            "Tạo phòng ➔",
            "Create Room ➔",
            "Crear Sala ➔",
            "创建房间 ➔"
        } },
        { "LOBBY_CARD3_TITLE", new string[] {
            "Máy chủ",
            "Server",
            "Servidor",
            "专属服务器"
        } },
        { "LOBBY_CARD3_SUB", new string[] {
            "Tạo máy chủ\nriêng của bạn",
            "Host your own\ndedicated server",
            "Crea tu propio\nservidor dedicado",
            "开启属于你的\n独立服务器"
        } },
        { "LOBBY_CARD3_BTN", new string[] {
            "Tạo máy chủ ➔",
            "Host Server ➔",
            "Crear Servidor ➔",
            "创建服务器 ➔"
        } },
        { "LOBBY_CARD4_TITLE", new string[] {
            "Làm chủ phòng",
            "Host Room",
            "Ser Anfitrión",
            "主持房间"
        } },
        { "LOBBY_CARD4_SUB", new string[] {
            "Quản lý phòng\ncủa bạn",
            "Manage and lead\nyour room",
            "Administra tu\npropia sala",
            "管理并掌控\n你的房间"
        } },
        { "LOBBY_CARD4_BTN", new string[] {
            "Bắt đầu ❯",
            "Start ❯",
            "Comenzar ❯",
            "开始 ❯"
        } },
        { "LOBBY_CARD5_TITLE", new string[] {
            "Làm khách",
            "Join Room",
            "Ser Invitado",
            "加入房间"
        } },
        { "LOBBY_CARD5_SUB", new string[] {
            "Tham gia phòng\ncủa người khác",
            "Join another\nplayer's room",
            "Únete a la sala\nde otro jugador",
            "进入其他玩家\n的组队房间"
        } },
        { "LOBBY_CARD5_BTN", new string[] {
            "Tham gia ❯",
            "Join ❯",
            "Unirse ❯",
            "加入 ❯"
        } },
        { "LOBBY_CARD6_TITLE", new string[] {
            "Tự động",
            "Auto Match",
            "Automático",
            "自动匹配"
        } },
        { "LOBBY_CARD6_SUB", new string[] {
            "Tự động làm chủ\nhoặc làm khách",
            "Auto host\nor join room",
            "Ser anfitrión o\ninvitado automático",
            "自动主持或\n加入可用房间"
        } },
        { "LOBBY_CARD6_BTN", new string[] {
            "Bắt đầu ❯",
            "Start ❯",
            "Comenzar ❯",
            "开始 ❯"
        } },
        { "LOBBY_STATUS_GOOD", new string[] {
            "Trạng thái kết nối: Kết nối tốt",
            "Connection status: Connected",
            "Estado de conexión: Conectado",
            "连接状态: 良好"
        } },
        { "LOBBY_STATUS_CONNECTING", new string[] {
            "Trạng thái kết nối: Đang kết nối...",
            "Connection status: Connecting...",
            "Estado de conexión: Conectando...",
            "连接状态: 正在连接..."
        } },
        { "LOBBY_STATUS_DISCONNECTED", new string[] {
            "Trạng thái kết nối: Mất kết nối",
            "Connection status: Disconnected",
            "Estado de conexión: Desconectado",
            "连接状态: 未连接"
        } },
        { "LOBBY_FOOTER_SLOGAN", new string[] {
            "PLAY TOGETHER • HAVE FUN",
            "PLAY TOGETHER • HAVE FUN",
            "JUEGA EN EQUIPO • DIVIÉRTETE",
            "组队开黑 • 畅享乐趣"
        } },
        { "LOBBY_CONN_STATUS_LABEL", new string[] {
            "Trạng thái kết nối:",
            "Connection Status:",
            "Estado de conexión:",
            "连接状态:"
        } },
        { "LOBBY_CONN_CONNECTED", new string[] {
            "Đã kết nối",
            "Connected",
            "Conectado",
            "已连接"
        } },
        { "LOBBY_CONN_CONNECTING", new string[] {
            "Đang kết nối...",
            "Connecting...",
            "Conectando...",
            "正在连接..."
        } },
        { "LOBBY_ROOM_ID_LABEL", new string[] {
            "ID Phòng:",
            "Room ID:",
            "ID de Sala:",
            "房间号:"
        } },
        { "LOBBY_ROOM_INPUT_PH", new string[] {
            "Nhập ID phòng 5 số...",
            "Enter 5-digit room ID...",
            "Introduce ID de 5 dígitos...",
            "输入5位数房间号..."
        } },
        { "LOBBY_ME_PROFILE", new string[] {
            "👤 Tôi",
            "👤 Me",
            "👤 Yo",
            "👤 我"
        } },
        { "WAITING_HEADER_TITLE", new string[] {
            "🏰 SẢNH PHÒNG CHỜ TRẬN ĐẤU (4 NGƯỜI CHƠI) 🏰",
            "🏰 MATCH WAITING LOBBY (4 PLAYERS) 🏰",
            "🏰 SALA DE ESPERA (4 JUGADORES) 🏰",
            "🏰 四人组队大厅 🏰"
        } },
        { "WAITING_MY_NAME_LABEL", new string[] {
            "👤 Tên của bạn:",
            "👤 Your Nickname:",
            "👤 Tu Nombre:",
            "👤 你的昵称:"
        } },
        { "WAITING_SAVE_NAME_BTN", new string[] {
            "✔ Lưu tên",
            "✔ Save Name",
            "✔ Guardar",
            "✔ 保存"
        } },
        { "SPINNER_CANCEL_BTN", new string[] {
            "🛑 HỦY TÌM TRẬN",
            "🛑 CANCEL SEARCH",
            "🛑 CANCELAR",
            "🛑 取消匹配"
        } },
        { "SPINNER_TITLE", new string[] {
            "🔍 ĐANG TÌM TRẬN ĐẤU...",
            "🔍 SEARCHING FOR MATCH...",
            "🔍 BUSCANDO PARTIDA...",
            "🔍 正在匹配玩家..."
        } },
        { "WAITING_ROLE_HOST", new string[] {
            "👑 Chủ Phòng",
            "👑 Host",
            "👑 Anfitrión",
            "👑 房主"
        } },
        { "WAITING_ROLE_MEMBER", new string[] {
            "🟢 Thành Viên",
            "🟢 Member",
            "🟢 Miembro",
            "🟢 队员"
        } },
        { "WAITING_SLOT_HOST", new string[] {
            "VỊ TRÍ #{0} (CHỦ PHÒNG)",
            "SLOT #{0} (HOST)",
            "POSICIÓN #{0} (ANFITRIÓN)",
            "位置 #{0} (房主)"
        } },
        { "WAITING_SLOT_TEAMMATE", new string[] {
            "VỊ TRÍ #{0} (ĐỒNG ĐỘI)",
            "SLOT #{0} (TEAMMATE)",
            "POSICIÓN #{0} (COMPAÑERO)",
            "位置 #{0} (队友)"
        } },
        { "WAITING_SLOT_SEARCHING", new string[] {
            "🔍 Đang ghép người...",
            "🔍 Matchmaking...",
            "🔍 Buscando jugador...",
            "🔍 正在匹配玩家..."
        } },
        { "WAITING_SLOT_WAITING", new string[] {
            "Đang chờ người chơi...",
            "Waiting for player...",
            "Esperando jugador...",
            "等待玩家加入..."
        } },
        { "WAITING_BTN_READY_CANCEL", new string[] {
            "✔ BẠN ĐÃ SẴN SÀNG (Click để hủy)",
            "✔ YOU ARE READY (Click to cancel)",
            "✔ ESTÁS LISTO (Clic para cancelar)",
            "✔ 你已准备 (点击取消)"
        } },
        { "WAITING_BTN_READY_CONFIRM", new string[] {
            "✔ BẤM ĐỂ XÁC NHẬN SẴN SÀNG",
            "✔ CLICK TO CONFIRM READY",
            "✔ CLIC PARA CONFIRMAR LISTO",
            "✔ 点击确认准备"
        } },

        // Cài đặt (Settings Dashboard Tabs & Headers)
        { "SETTINGS_DASHBOARD_TITLE", new string[] {
            "⚙️ BẢNG CÀI ĐẶT (DASHBOARD)",
            "⚙️ SETTINGS DASHBOARD",
            "⚙️ PANEL DE AJUSTES",
            "⚙️ 设置面板"
        } },
        { "SETTINGS_DASHBOARD_SUB", new string[] {
            "Ghost Maze • Tùy chỉnh hệ thống (F1 / Tab / Esc)",
            "Ghost Maze • System Preferences (F1 / Tab / Esc)",
            "Ghost Maze • Preferencias del Sistema (F1 / Tab / Esc)",
            "Ghost Maze • 系统偏好设置 (F1 / Tab / Esc)"
        } },
        { "SETTINGS_TAB_GRAPHICS", new string[] {
            "🎨 Hình Ảnh",
            "🎨 Graphics",
            "🎨 Gráficos",
            "🎨 画面"
        } },
        { "SETTINGS_TAB_AUDIO", new string[] {
            "🔊 Âm Thanh",
            "🔊 Audio",
            "🔊 Sonido",
            "🔊 声音"
        } },
        { "SETTINGS_TAB_CONTROLS", new string[] {
            "🎮 Điều Khiển",
            "🎮 Controls",
            "🎮 Controles",
            "🎮 控制"
        } },
        { "SETTINGS_TAB_LANGUAGE", new string[] {
            "🌐 Ngôn Ngữ",
            "🌐 Language",
            "🌐 Idioma",
            "🌐 语言"
        } },
        { "SETTINGS_LANG_HEADER", new string[] {
            "CHỌN NGÔN NGỮ HIỂN THỊ TOÀN BỘ GAME",
            "SELECT GLOBAL DISPLAY LANGUAGE",
            "SELECCIONAR IDIOMA GLOBAL DEL JUEGO",
            "选择全游戏显示语言"
        } },
        { "SETTINGS_LANG_HINT", new string[] {
            "Thay đổi ngôn ngữ tức thì cho giao diện, phụ đề, đếm giờ và thông báo trong toàn bộ trò chơi.",
            "Instantly changes display language for UI, subtitles, timers, and notices across the entire game.",
            "Cambia instantáneamente el idioma para la interfaz, subtítulos, temporizadores y avisos de todo el juego.",
            "即时更改全游戏界面、字幕、计时器以及系统提示语言。"
        } },
        { "BTN_LOBBY_SETTINGS", new string[] {
            "⚙️ CÀI ĐẶT",
            "⚙️ SETTINGS",
            "⚙️ AJUSTES",
            "⚙️ 设置"
        } },
        { "STATUS_ACTIVE", new string[] {
            "✔ ĐANG DÙNG",
            "✔ ACTIVE",
            "✔ ACTIVO",
            "✔ 当前使用"
        } },
        { "STATUS_CHOOSE", new string[] {
            "CHỌN",
            "SELECT",
            "ELEGIR",
            "选择"
        } },
        { "SETTINGS_INMATCH_NOTICE", new string[] {
            "⚠️ Đang trong trận: Chỉ hỗ trợ điều chỉnh Đồ họa & Độ nhạy",
            "⚠️ In-Match: Only Graphics & Sensitivity are adjustable",
            "⚠️ En Partida: Solo Gráficos y Sensibilidad ajustables",
            "⚠️ 对局中: 仅可调节画面与灵敏度"
        } },
        { "SETTINGS_SAVED_NOTICE", new string[] {
            "✔ Đã lưu cài đặt tự động",
            "✔ Settings saved automatically",
            "✔ Ajustes guardados automáticamente",
            "✔ 设置已自动保存"
        } },
        { "SETTINGS_GRAPHICS_HEADER", new string[] {
            "🎨 1. ĐỒ HỌA 3D (3D GRAPHICS - UI/UX LUÔN SIÊU NÉT)",
            "🎨 1. 3D GRAPHICS (UI/UX ALWAYS CRISP 100%)",
            "🎨 1. GRÁFICOS 3D (UI/UX SIEMPRE NÍTIDO 100%)",
            "🎨 1. 3D画面质量 (UI/UX 始终100%高清锐利)"
        } },
        { "SETTINGS_GRAPHICS_BOOST_BTN", new string[] {
            "🌟 ĐỒ HỌA 3D TỐI ĐA (BÓNG ĐỔ & MSAA 4X)",
            "🌟 MAX 3D GRAPHICS (FULL SHADOWS & 4X MSAA)",
            "🌟 GRÁFICOS 3D ULTRA (SOMBRAS Y 4X MSAA)",
            "🌟 顶级3D画质 (全阴影与4倍抗锯齿)"
        } },
        { "SETTINGS_GRAPHICS_ULTRA", new string[] {
            "🌟 3D Siêu Đẹp",
            "🌟 3D Ultra",
            "🌟 3D Ultra",
            "🌟 3D 极限"
        } },
        { "SETTINGS_GRAPHICS_MED", new string[] {
            "⚖️ 3D Cân Bằng",
            "⚖️ 3D Balanced",
            "⚖️ 3D Equilibrado",
            "⚖️ 3D 平衡"
        } },
        { "SETTINGS_GRAPHICS_LOW", new string[] {
            "⚡ 3D Tiết Kiệm",
            "⚡ 3D Power Save",
            "⚡ 3D Ahorro",
            "⚡ 3D 流畅省电"
        } },
        { "SETTINGS_FPS_HEADER", new string[] {
            "⚡ 2. TỐC ĐỘ KHUNG HÌNH (FPS)",
            "⚡ 2. TARGET FRAME RATE (FPS)",
            "⚡ 2. TASA DE FOTOGRAMAS (FPS)",
            "⚡ 2. 目标帧率 (FPS)"
        } },
        { "SETTINGS_FPS_UNLIMITED", new string[] {
            "🚀 Mở Khóa FPS (Max)",
            "🚀 Unlimited FPS (Max)",
            "🚀 FPS Ilimitado (Max)",
            "🚀 解锁帧率上限"
        } },
        { "SETTINGS_FPS_120", new string[] {
            "⚡ 120 FPS Siêu Mượt",
            "⚡ 120 FPS Smooth",
            "⚡ 120 FPS Fluido",
            "⚡ 120 FPS 极速流畅"
        } },
        { "SETTINGS_FPS_60", new string[] {
            "🔋 60 FPS Chuẩn Mát Máy",
            "🔋 60 FPS Standard",
            "🔋 60 FPS Estándar",
            "🔋 60 FPS 标准"
        } },
        { "SETTINGS_VSYNC_HEADER", new string[] {
            "🔄 3. ĐỒNG BỘ DỌC (V-SYNC)",
            "🔄 3. VERTICAL SYNC (V-SYNC)",
            "🔄 3. SINCRONIZACIÓN VERTICAL",
            "🔄 3. 垂直同步 (V-SYNC)"
        } },
        { "SETTINGS_AUDIO_HEADER", new string[] {
            "🔊 ĐIỀU CHỈNH ÂM LƯỢNG HỆ THỐNG",
            "🔊 AUDIO VOLUME SETTINGS",
            "🔊 AJUSTES DE VOLUMEN",
            "🔊 系统音量调节"
        } },
        { "SETTINGS_AUDIO_MASTER_LABEL", new string[] {
            "Âm lượng tổng (Master)",
            "Master Volume",
            "Volumen General",
            "主音量"
        } },
        { "SETTINGS_AUDIO_MUSIC_LABEL", new string[] {
            "Nhạc nền rùng rợn (Music)",
            "Atmospheric Music",
            "Música de Fondo",
            "背景音乐"
        } },
        { "SETTINGS_AUDIO_SFX_LABEL", new string[] {
            "Hiệu ứng âm thanh & Ma (SFX)",
            "Sound Effects & Ghost (SFX)",
            "Efectos de Sonido (SFX)",
            "音效与鬼怪声效"
        } },
        { "SETTINGS_AUDIO_MUTE", new string[] {
            "🔇 Tắt Toàn Bộ Âm Thanh",
            "🔇 Mute All Audio",
            "🔇 Silenciar Todo",
            "🔇 静音所有音效"
        } },
        { "SETTINGS_AUDIO_UNMUTE", new string[] {
            "🔊 Bật Âm Thanh",
            "🔊 Unmute Audio",
            "🔊 Activar Sonido",
            "🔊 开启声音"
        } },
        { "SETTINGS_CONTROLS_HEADER", new string[] {
            "🎮 ĐIỀU CHỈNH ĐIỀU KHIỂN & GÓC NHÌN",
            "🎮 CONTROLS & CAMERA SENSITIVITY",
            "🎮 CONTROLES Y SENSIBILIDAD",
            "🎮 操作控制与视角灵敏度"
        } },
        { "SETTINGS_CONTROLS_SENS_LABEL", new string[] {
            "Độ nhạy góc nhìn 360°",
            "Camera Sensitivity (360°)",
            "Sensibilidad de Cámara (360°)",
            "视角灵敏度 (360°)"
        } },
        { "SETTINGS_CONTROLS_KEYBINDINGS_HEADER", new string[] {
            "⌨️ HƯỚNG DẪN BÀN PHÍM (KEYBINDINGS)",
            "⌨️ KEYBOARD CONTROLS GUIDE",
            "⌨️ GUÍA DE CONTROLES TECLADO",
            "⌨️ 键盘键位指南"
        } },
        { "SETTINGS_CONTROLS_KEYBINDINGS_BODY", new string[] {
            "• [W] [A] [S] [D] / Mũi tên : Di chuyển nhân vật\n• [Left Shift] : Chạy nước rút (Sprint)\n• [Space] : Nhảy cao tiếp đất\n• [Di Chuột] : Xoay góc nhìn thứ ba 360°\n• [Con Lăn Chuột] : Thu phóng Camera gần/xa\n• [F1] / [Tab] / [Esc] : Bật / Tắt nhanh Dashboard",
            "• [W] [A] [S] [D] / Arrows : Character Movement\n• [Left Shift] : Sprint\n• [Space] : Jump\n• [Mouse Move] : Orbit Camera 360°\n• [Scroll Wheel] : Camera Zoom In/Out\n• [F1] / [Tab] / [Esc] : Toggle Settings Dashboard",
            "• [W] [A] [S] [D] / Flechas : Mover personaje\n• [Left Shift] : Correr (Sprint)\n• [Espacio] : Saltar\n• [Mover Ratón] : Rotar cámara 360°\n• [Rueda Ratón] : Acercar / Alejar cámara\n• [F1] / [Tab] / [Esc] : Abrir / Cerrar Ajustes",
            "• [W] [A] [S] [D] / 方向键 : 角色移动\n• [Left Shift] : 疾跑冲刺\n• [空格键] : 跳跃\n• [滑动鼠标] : 360° 旋转第三人称视角\n• [滚轮] : 镜头远近缩放\n• [F1] / [Tab] / [Esc] : 快速开启/关闭设置"
        } },
        { "SETTINGS_BTN_CLOSE", new string[] {
            "ĐÓNG CÀI ĐẶT",
            "CLOSE SETTINGS",
            "CERRAR AJUSTES",
            "关闭设置"
        } },

        // IN-MATCH HUD & SURVIVAL TIMERS (SurvivalTimerUI)
        { "HUD_WAITING_FOR_ROOM", new string[] {
            "⚔️ ĐANG CHỜ PHÒNG XÁC NHẬN BẮT ĐẦU",
            "⚔️ WAITING FOR ROOM CONFIRMATION",
            "⚔️ ESPERANDO CONFIRMACIÓN DE SALA",
            "⚔️ 等待房间确认开始"
        } },
        { "HUD_GHOST_RANDOM_NOTICE", new string[] {
            "🎃 BẠN ĐƯỢC RANDOM LÀM MA BÍ NGÔ! CHỜ TRONG VÒNG TRÒN:",
            "🎃 YOU ARE CHOSEN AS PUMPKIN GHOST! WAIT IN CIRCLE:",
            "🎃 ¡ERES EL FANTASMA CALABAZA! ESPERA EN EL CÍRCULO:",
            "🎃 你被随机选为南瓜鬼！请在圈内等待:"
        } },
        { "HUD_HUMAN_RUN_PREPARE", new string[] {
            "⏳ CHUẨN BỊ XUẤT PHÁT CHẠY TRỐN (TRONG VÒNG TRÒN)",
            "⏳ PREPARE TO RUN & HIDE (IN CIRCLE)",
            "⏳ PREPÁRATE PARA CORRER (EN EL CÍRCULO)",
            "⏳ 准备出发躲藏 (在圈内)"
        } },
        { "HUD_GHOST_COUNTDOWN_HINT", new string[] {
            "🎃 BẠN SẼ HÓA THÂN THÀNH MA BÍ NGÔ SAU:",
            "🎃 YOU WILL TRANSFORM INTO GHOST IN:",
            "🎃 TE TRANSFORMARÁS EN FANTASMA EN:",
            "🎃 你将在倒计时后化身南瓜鬼:"
        } },
        { "HUD_HUMAN_COUNTDOWN_HINT", new string[] {
            "🏃 HÃY CHẠY TRỐN! 2 MA BÍ NGÔ XUẤT HIỆN SAU:",
            "🏃 RUN AND HIDE! 2 GHOSTS SPAWN IN:",
            "🏃 ¡CORRE! 2 FANTASMAS APARECEN EN:",
            "🏃 快逃！两只南瓜鬼将在倒计时后出现:"
        } },
        { "HUD_CIRCLE_OPENED_HUMAN", new string[] {
            "🏃 VÒNG TRÒN ĐÃ MỞ! HÃY TỎA RA MÊ CUNG CHẠY & TRỐN HÚ!\n(2 Người chơi Ma Bí Ngô sẽ hóa thân đi săn sau 20s)",
            "🏃 CIRCLE OPENED! SPREAD OUT AND HIDE IN THE MAZE!\n(2 Pumpkin Ghosts will awaken in 20s)",
            "🏃 ¡CÍRCULO ABIERTO! ¡ESCÓNDETE EN EL LABERINTO!\n(Los 2 Fantasmas despertarán en 20s)",
            "🏃 结界已解开！快分散到迷宫中躲藏！\n(两只南瓜鬼将在20秒后苏醒追猎)"
        } },
        { "HUD_CIRCLE_OPENED_GHOST", new string[] {
            "🎃 BẠN ĐƯỢC CHỌN LÀM MA BÍ NGÔ!\nHãy đợi trong vòng tròn 20s để người khác chạy trốn trước khi đi săn!",
            "🎃 YOU ARE THE PUMPKIN GHOST!\nWait in circle 20s before hunting!",
            "🎃 ¡ERES EL FANTASMA CALABAZA!\n¡Espera 20s en el círculo antes de cazar!",
            "🎃 你被选为南瓜鬼！\n请在圈内等待20秒让幸存者躲藏！"
        } },
        { "HUD_HUNTING_STARTED", new string[] {
            "🔥 MA BÍ NGÔ ĐÃ XUẤT HIỆN ĐI SĂN!\nHÃY SINH TỒN TRONG 3 PHÚT!",
            "🔥 PUMPKIN GHOSTS ARE AWAKE! SURVIVE FOR 3 MINUTES!",
            "🔥 ¡LOS FANTASMAS HAN DESPERTADO! ¡SOBREVIVE 3 MINUTOS!",
            "🔥 南瓜鬼已苏醒！在迷宫中生存3分钟！"
        } },
        { "HUD_SURVIVAL_TIME_TITLE", new string[] {
            "⏱️ THỜI GIAN SINH TỒN",
            "⏱️ SURVIVAL TIME",
            "⏱️ TIEMPO DE SUPERVIVENCIA",
            "⏱️ 剩余生存时间"
        } },
        { "HUD_HUMAN_VICTORY", new string[] {
            "🏆 CON NGƯỜI CHIẾN THẮNG!",
            "🏆 SURVIVORS WIN!",
            "🏆 ¡LOS HUMANOS GANAN!",
            "🏆 幸存者获胜！"
        } },
        { "HUD_GHOST_VICTORY", new string[] {
            "💀 2 MA BÍ NGÔ CHIẾN THẮNG!",
            "💀 PUMPKIN GHOSTS WIN!",
            "💀 ¡LOS FANTASMAS GANAN!",
            "💀 南瓜鬼获胜！"
        } },
        { "HUD_ALL_HUMANS_DOWN", new string[] {
            "TẤT CẢ CON NGƯỜI ĐÃ BỊ HẠ GỤC!",
            "ALL SURVIVORS HAVE BEEN ELIMINATED!",
            "¡TODOS LOS HUMANOS HAN SIDO DERROTADOS!",
            "所有幸存者已被击败！"
        } },
        { "HUD_HUMANS_SURVIVED", new string[] {
            "HẾT 3 PHÚT! CON NGƯỜI ĐÃ SINH TỒN THÀNH CÔNG!",
            "3 MINUTES UP! SURVIVORS SUCCESSFULLY SURVIVED!",
            "¡3 MINUTOS CUMPLIDOS! ¡LOS HUMANOS SOBREVIVIERON!",
            "3分钟结束！幸存者成功生还！"
        } },
        { "HUD_REPLAY_AGREE_BTN", new string[] {
            "👍 ĐỒNG Ý CHƠI LẠI [ P ]",
            "👍 AGREE TO REPLAY [ P ]",
            "👍 ACEPTAR REVANCHA [ P ]",
            "👍 同意再来一局 [ P ]"
        } },
        { "HUD_REPLAY_CONFIRMED", new string[] {
            "✅ ĐÃ XÁC NHẬN CHƠI LẠI — Nhấn [P] để hủy",
            "✅ REPLAY CONFIRMED — Press [P] to cancel",
            "✅ REVANCHA CONFIRMADA — Pulsa [P] para cancelar",
            "✅ 已确认再来一局 — 按 [P] 取消"
        } },
        { "HUD_REPLAY_SOLO_BTN", new string[] {
            "🔄 CHƠI LẠI NGAY [ P ]",
            "🔄 PLAY AGAIN NOW [ P ]",
            "🔄 JUGAR DE NUEVO [ P ]",
            "🔄 立即重开 [ P ]"
        } },
        { "HUD_LEAVE_LOBBY_BTN", new string[] {
            "🚪 RỜI VỀ SẢNH [ ESC ]",
            "🚪 LEAVE TO LOBBY [ ESC ]",
            "🚪 SALIR AL LOBBY [ ESC ]",
            "🚪 返回大厅 [ ESC ]"
        } },
        { "HUD_WAITING_REPLAY_PLAYERS", new string[] {
            "⏳ ĐỢI CẢ PHÒNG XÁC NHẬN CHƠI LẠI: <color=#00FF9D>{0} / {1}</color>",
            "⏳ WAITING FOR ALL PLAYERS: <color=#00FF9D>{0} / {1}</color>",
            "⏳ ESPERANDO A TODOS LOS JUGADORES: <color=#00FF9D>{0} / {1}</color>",
            "⏳ 等待所有玩家确认: <color=#00FF9D>{0} / {1}</color>"
        } },
        { "HUD_REPLAY_STARTING", new string[] {
            "<color=#00FF9D>🚀 ĐÃ ĐỦ NGƯỜI ĐỒNG Ý! ĐANG TẠO LẠI TRÒ CHƠI...</color>",
            "<color=#00FF9D>🚀 ALL PLAYERS AGREED! RESTARTING GAME...</color>",
            "<color=#00FF9D>🚀 ¡TODOS DE ACUERDO! REINICIANDO PARTIDA...</color>",
            "<color=#00FF9D>🚀 全员同意！正在重新创建对局...</color>"
        } },
        { "HUD_TOP_ROOM_ID", new string[] {
            "🏠 PHÒNG: {0}",
            "🏠 ROOM: {0}",
            "🏠 SALA: {0}",
            "🏠 房间: {0}"
        } },

        // PLAYER HEALTH & SPECTATE (PlayerHealth)
        { "HUD_PLAYER_HP_TITLE", new string[] {
            "❤️ MÁU PLAYER",
            "❤️ PLAYER HP",
            "❤️ SALUD",
            "❤️ 生命值"
        } },
        { "HUD_PLAYER_DEAD", new string[] {
            "💀 ĐÃ TỬ TRẬN",
            "💀 DEFEATED",
            "💀 ELIMINADO",
            "💀 已阵亡"
        } },
        { "HUD_GHOST_IMMORTAL", new string[] {
            "👻 MA BÍ NGÔ (BẤT TỬ)",
            "👻 PUMPKIN GHOST (IMMORTAL)",
            "👻 FANTASMA (INMORTAL)",
            "👻 南瓜鬼 (无敌)"
        } },
        { "HUD_SPECTATE_TITLE", new string[] {
            "👁 ĐANG XEM ĐỒNG ĐỘI: {0} ({1}/{2})",
            "👁 SPECTATING: {0} ({1}/{2})",
            "👁 ESPECTANDO: {0} ({1}/{2})",
            "👁 正在观战队友: {0} ({1}/{2})"
        } },
        { "HUD_SPECTATE_NO_TEAMMATES", new string[] {
            "💀 ĐÃ TỬ TRẬN — KHÔNG CÒN ĐỒNG ĐỘI CÙNG PHE",
            "💀 ELIMINATED — NO REMAINING TEAMMATES",
            "💀 ELIMINADO — NO HAY COMPAÑEROS VIVOS",
            "💀 已阵亡 — 无剩余队友"
        } },

        // BOT & WAITING ROOM & PENTACLE SEAL PICKUP (VI, EN, ES, ZH)
        { "WAITING_BTN_ADD_BOT", new string[] {
            "🤖 Thêm Máy ({0}/{1})",
            "🤖 Add Bot ({0}/{1})",
            "🤖 Añadir Bot ({0}/{1})",
            "🤖 添加人机 ({0}/{1})"
        } },
        { "WAITING_BTN_BOT_FULL", new string[] {
            "🤖 Đủ Máy ({0}/{1}) - Xóa",
            "🤖 Bots Full ({0}/{1}) - Clear",
            "🤖 Bots Llenos ({0}/{1}) - Quitar",
            "🤖 人机已满 ({0}/{1}) - 清除"
        } },
        { "WAITING_BTN_READY_ENTER", new string[] {
            "✔ SẴN SÀNG VÀO GAME ➔",
            "✔ READY TO PLAY ➔",
            "✔ LISTO PARA JUGAR ➔",
            "✔ 准备进入游戏 ➔"
        } },
        { "WAITING_SLOT_BOT_BADGE", new string[] {
            "VỊ TRÍ #{0} (MÁY / BOT)",
            "SLOT #{0} (BOT)",
            "ESPACIO #{0} (BOT)",
            "位置 #{0} (人机)"
        } },
        { "WAITING_SLOT_BOT_READY", new string[] {
            "✔ Sẵn sàng  ●  Random Ma / Người",
            "✔ Ready  ●  Random Ghost / Human",
            "✔ Listo  ●  Fantasma / Humano",
            "✔ 已准备 ● 随机鬼/人"
        } },
        { "WAITING_BANNER_BOTS_READY", new string[] {
            "🤖 ĐÃ THÊM {0} MÁY ({1}/4): Vai trò Ma/Người ngẫu nhiên! Bấm [✔ SẴN SÀNG] để vào game!",
            "🤖 ADDED {0} BOTS ({1}/4): Random Ghost/Human roles! Press [✔ READY] to start!",
            "🤖 {0} BOTS AÑADIDOS ({1}/4): ¡Rol aleatorio! ¡Pulsa [✔ LISTO] para jugar!",
            "🤖 已添加 {0} 名人机 ({1}/4)：随机分配鬼/人！点击 [✔ 准备] 开始！"
        } },
        { "WAITING_BANNER_EMPTY_ROOM", new string[] {
            "🎮 PHÒNG CHỜ: Bấm [🤖 Thêm Máy] hoặc chờ bạn bè, rồi bấm [✔ SẴN SÀNG] để vào game!",
            "🎮 WAITING ROOM: Click [Add Bot] or wait for friends, then press [READY] to play!",
            "🎮 SALA DE ESPERA: Pulsa [Añadir Bot] o espera amigos, y pulsa [LISTO]!",
            "🎮 准备房间：点击 [添加人机] 或等待好友，然后点击 [准备] 开始！"
        } },
        { "MATCH_BTN_FILL_BOTS", new string[] {
            "⚡ Ghép Máy Đủ 4/4 Ngay ➔",
            "⚡ Fill Bots 4/4 Now ➔",
            "⚡ Llenar Bots 4/4 Ahora ➔",
            "⚡ 立即补齐4/4人机 ➔"
        } },
        { "MATCH_COUNT_SEARCHING", new string[] {
            "👥 Đã tìm thấy: <color=#2EE87A>{0} / 4</color> người chơi (Tự ghép Máy đủ 4/4 sau 12s)",
            "👥 Found: <color=#2EE87A>{0} / 4</color> players (Auto-fill Bots in 12s)",
            "👥 Encontrados: <color=#2EE87A>{0} / 4</color> jugadores (Bots en 12s)",
            "👥 已找到：<color=#2EE87A>{0} / 4</color> 玩家（12秒后自动补齐人机）"
        } },
        { "HUD_PICKUP_HAND", new string[] {
            "NHẶT DẤU ẤN [{0}]",
            "PICK UP SEAL [{0}]",
            "RECOGER SELLO [{0}]",
            "拾取封印 [{0}]"
        } },
        { "HUD_SEAL_COUNTER", new string[] {
            "Dấu Ấn [E]: {0} / {1}",
            "Seals [E]: {0} / {1}",
            "Sellos [E]: {0} / {1}",
            "封印 [E]: {0} / {1}"
        } },
        { "NOTICE_SEAL_PICKED", new string[] {
            "Nhặt được Dấu Ấn Pentacle! ({0}/{1})",
            "Picked up Pentacle Seal! ({0}/{1})",
            "¡Sello Pentáculo recogido! ({0}/{1})",
            "已拾取五芒星封印！({0}/{1})"
        } },
        { "NOTICE_SEAL_FULL", new string[] {
            "Đã đầy {0}/{1} Dấu Ấn!",
            "Seals full ({0}/{1})!",
            "¡Sellos llenos ({0}/{1})!",
            "封印已满 ({0}/{1})！"
        } },
        { "NOTICE_NEED_SEAL", new string[] {
            "Chưa có Dấu Ấn Pentacle! Hãy tìm nhặt dưới sàn!",
            "No Pentacle Seal! Find & pick one up on the floor!",
            "¡Sin Sello Pentáculo! ¡Búscalo en el suelo!",
            "暂无五芒星封印！请在地面寻找拾取！"
        } },
        { "NOTICE_OUT_OF_SEALS", new string[] {
            "Hết Dấu Ấn! Tìm nhặt quanh bản đồ ({0}/{1})",
            "Out of Seals! Find more on the map ({0}/{1})",
            "¡Sin Sellos! Busca más en el mapa ({0}/{1})",
            "封印已用完！请在地图中寻找 ({0}/{1})"
        } },
        { "NOTICE_NO_GHOST_TARGET", new string[] {
            "Không tìm thấy mục tiêu!",
            "No target found!",
            "¡Objetivo no encontrado!",
            "未找到目标！"
        } },
        { "HUD_EDIT_HAND_BTN", new string[] {
            "Nút Bàn Tay Nhặt [F]",
            "Pickup Hand Button [F]",
            "Botón Recoger [F]",
            "拾取手势按钮 [F]"
        } },
        { "HUD_EDIT_SEAL_BTN", new string[] {
            "Nút Dấu Ấn & Xích [E]",
            "Seal & Chain Button [E]",
            "Botón Sello y Cadena [E]",
            "封印与锁链按钮 [E]"
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
