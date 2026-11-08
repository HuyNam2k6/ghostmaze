using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class GameSettingsUISetup
{
    private static Font defaultFont;

    [MenuItem("Tools/Setup Settings Dashboard (Sidebar & Gear Button)")]
    public static void SetupSettingsUI()
    {
        // 1. Lấy Font chữ chuẩn của Unity
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 2. Thiết lập EventSystem tương thích New Input System
        SetupEventSystem();

        // 3. Xóa Canvas cũ nếu đã tồn tại để tạo mới đồng bộ
        GameObject oldCanvas = GameObject.Find("Canvas_GameSettings");
        if (oldCanvas != null)
        {
            Object.DestroyImmediate(oldCanvas);
        }

        // 4. Tạo Canvas chính (ScreenSpaceOverlay siêu sắc nét, không bị mờ nhòe bởi Post-Processing)
        GameObject canvasGo = new GameObject("Canvas_GameSettings");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;
        scaler.referencePixelsPerUnit = 100f;

        canvasGo.AddComponent<GraphicRaycaster>();

        GameSettingsManager manager = canvasGo.AddComponent<GameSettingsManager>();

        // 5. HUD TRÊN MÀN HÌNH (Góc Trái: FPS & Thanh Máu Player, Giữa Màn: Đồng Hồ 3 Phút, Góc Phải: Nút Bánh Răng)
        GameObject hudContainer = new GameObject("HUD_Container");
        hudContainer.transform.SetParent(canvasGo.transform, false);
        RectTransform rtHud = hudContainer.AddComponent<RectTransform>();
        rtHud.anchorMin = Vector2.zero;
        rtHud.anchorMax = Vector2.one;
        rtHud.sizeDelta = Vector2.zero;

        // 5.0 HIỆU ỨNG CHỚP ĐỎ KHI TRÚNG ĐÒN (DAMAGE VIGNETTE)
        GameObject vignetteGo = new GameObject("PlayerDamageVignette");
        vignetteGo.transform.SetParent(hudContainer.transform, false);
        RectTransform rtVig = vignetteGo.AddComponent<RectTransform>();
        rtVig.anchorMin = Vector2.zero;
        rtVig.anchorMax = Vector2.one;
        rtVig.sizeDelta = Vector2.zero;
        Image vigImg = vignetteGo.AddComponent<Image>();
        vigImg.color = new Color(0.85f, 0.05f, 0.08f, 0f);
        vigImg.raycastTarget = false;

        // 5.1 HUD FPS Badge (Góc trên bên trái)
        GameObject fpsBadge = new GameObject("FPSBadge");
        fpsBadge.transform.SetParent(hudContainer.transform, false);
        RectTransform rtFpsBadge = fpsBadge.AddComponent<RectTransform>();
        rtFpsBadge.anchorMin = new Vector2(0f, 1f);
        rtFpsBadge.anchorMax = new Vector2(0f, 1f);
        rtFpsBadge.pivot = new Vector2(0f, 1f);
        rtFpsBadge.anchoredPosition = new Vector2(25f, -22f);
        rtFpsBadge.sizeDelta = new Vector2(210f, 40f);

        Image fpsBadgeImg = fpsBadge.AddComponent<Image>();
        fpsBadgeImg.color = new Color(0.06f, 0.08f, 0.14f, 0.90f);
        fpsBadgeImg.raycastTarget = false;

        Outline fpsOutline = fpsBadge.AddComponent<Outline>();
        fpsOutline.effectColor = new Color(0.18f, 0.80f, 0.44f, 0.50f);
        fpsOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject fpsCounterGo = CreateTextObject("FPSCounter", fpsBadge.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "<color=#2ECC71>120 FPS</color>  (8.3 ms)", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        manager.fpsCounterText = fpsCounterGo.GetComponent<Text>();

        // 5.2 THANH MÁU NGƯỜI CHƠI (PLAYER HP BAR - Ngay dưới FPS Badge)
        GameObject hpPanel = new GameObject("PlayerHealthPanel");
        hpPanel.transform.SetParent(hudContainer.transform, false);
        RectTransform rtHpPanel = hpPanel.AddComponent<RectTransform>();
        rtHpPanel.anchorMin = new Vector2(0f, 1f);
        rtHpPanel.anchorMax = new Vector2(0f, 1f);
        rtHpPanel.pivot = new Vector2(0f, 1f);
        rtHpPanel.anchoredPosition = new Vector2(25f, -72f);
        rtHpPanel.sizeDelta = new Vector2(285f, 62f);

        Image hpBgImg = hpPanel.AddComponent<Image>();
        hpBgImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f);
        hpBgImg.raycastTarget = false;

        Outline hpOutline = hpPanel.AddComponent<Outline>();
        hpOutline.effectColor = new Color(0.90f, 0.22f, 0.28f, 0.65f);
        hpOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Nhãn '❤️ MÁU PLAYER'
        CreateTextObject("PlayerHP_Title", hpPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -6f), new Vector2(-24f, 22f),
            "❤️ MÁU PLAYER", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.82f, 0.85f));

        // Số máu '100 / 100 HP'
        GameObject hpValTxtGo = CreateTextObject("PlayerHP_ValueText", hpPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -6f), new Vector2(-24f, 22f),
            "100 / 100 HP", 12, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);

        // Khung rãnh thanh máu (Track)
        GameObject hpTrack = new GameObject("PlayerHP_Track");
        hpTrack.transform.SetParent(hpPanel.transform, false);
        RectTransform rtHpTrack = hpTrack.AddComponent<RectTransform>();
        rtHpTrack.anchorMin = new Vector2(0f, 0f);
        rtHpTrack.anchorMax = new Vector2(1f, 0f);
        rtHpTrack.pivot = new Vector2(0.5f, 0f);
        rtHpTrack.anchoredPosition = new Vector2(0f, 10f);
        rtHpTrack.sizeDelta = new Vector2(-24f, 20f);
        Image hpTrackImg = hpTrack.AddComponent<Image>();
        hpTrackImg.color = new Color(0.16f, 0.06f, 0.08f, 0.95f);
        hpTrackImg.raycastTarget = false;

        // Phần lõi thanh máu co giãn (Fill)
        GameObject hpFill = new GameObject("PlayerHP_Fill");
        hpFill.transform.SetParent(hpTrack.transform, false);
        RectTransform rtHpFill = hpFill.AddComponent<RectTransform>();
        rtHpFill.anchorMin = Vector2.zero;
        rtHpFill.anchorMax = Vector2.one;
        rtHpFill.offsetMin = Vector2.zero;
        rtHpFill.offsetMax = Vector2.zero;
        Image hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.18f, 0.85f, 0.45f, 1f);
        hpFillImg.raycastTarget = false;

        // Tự động gắn tham chiếu vào PlayerHealth nếu có trong Scene
        PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.healthFillRect = rtHpFill;
            playerHealth.healthFillImage = hpFillImg;
            playerHealth.healthValueText = hpValTxtGo.GetComponent<Text>();
            playerHealth.damageVignette = vigImg;
        }

        // 5.3 BỘ ĐẾM THỜI GIAN 3 PHÚT (Ở GIỮA MÀN HÌNH PHÍA TRÊN)
        GameObject timerPanel = new GameObject("SurvivalTimerPanel");
        timerPanel.transform.SetParent(hudContainer.transform, false);
        RectTransform rtTimer = timerPanel.AddComponent<RectTransform>();
        rtTimer.anchorMin = new Vector2(0.5f, 1f);
        rtTimer.anchorMax = new Vector2(0.5f, 1f);
        rtTimer.pivot = new Vector2(0.5f, 1f);
        rtTimer.anchoredPosition = new Vector2(0f, -20f);
        rtTimer.sizeDelta = new Vector2(260f, 80f);

        Image timerBgImg = timerPanel.AddComponent<Image>();
        timerBgImg.color = new Color(0.05f, 0.07f, 0.13f, 0.93f);
        timerBgImg.raycastTarget = false;

        Outline timerOutline = timerPanel.AddComponent<Outline>();
        timerOutline.effectColor = new Color(0.28f, 0.68f, 1.0f, 0.70f);
        timerOutline.effectDistance = new Vector2(2.0f, -2.0f);

        GameObject timerLblGo = CreateTextObject("TimerLabelText", timerPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -6f), new Vector2(-16f, 20f),
            "⏱ THỜI GIAN SINH TỒN", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1.0f));

        GameObject timerDigitsGo = CreateTextObject("TimerDigitsText", timerPanel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -26f), new Vector2(-16f, 40f),
            "03:00", 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);

        // Thanh tiến trình nhỏ dưới đáy đồng hồ
        GameObject timerTrack = new GameObject("TimerProgressTrack");
        timerTrack.transform.SetParent(timerPanel.transform, false);
        RectTransform rtTimerTrack = timerTrack.AddComponent<RectTransform>();
        rtTimerTrack.anchorMin = new Vector2(0f, 0f);
        rtTimerTrack.anchorMax = new Vector2(1f, 0f);
        rtTimerTrack.pivot = new Vector2(0.5f, 0f);
        rtTimerTrack.anchoredPosition = new Vector2(0f, 6f);
        rtTimerTrack.sizeDelta = new Vector2(-24f, 6f);
        Image timerTrackImg = timerTrack.AddComponent<Image>();
        timerTrackImg.color = new Color(0.12f, 0.16f, 0.25f, 0.95f);
        timerTrackImg.raycastTarget = false;

        GameObject timerFill = new GameObject("TimerProgressFill");
        timerFill.transform.SetParent(timerTrack.transform, false);
        RectTransform rtTimerFill = timerFill.AddComponent<RectTransform>();
        rtTimerFill.anchorMin = Vector2.zero;
        rtTimerFill.anchorMax = Vector2.one;
        rtTimerFill.offsetMin = Vector2.zero;
        rtTimerFill.offsetMax = Vector2.zero;
        Image timerFillImg = timerFill.AddComponent<Image>();
        timerFillImg.color = new Color(0.28f, 0.75f, 1.0f, 1f);
        timerFillImg.raycastTarget = false;

        SurvivalTimerUI timerComp = canvasGo.AddComponent<SurvivalTimerUI>();
        timerComp.durationSeconds = 180f; // Đúng 3 phút (180 giây)
        timerComp.autoStart = true;
        timerComp.timerLabelText = timerLblGo.GetComponent<Text>();
        timerComp.timerDigitsText = timerDigitsGo.GetComponent<Text>();
        timerComp.timerProgressFill = rtTimerFill;

        // 5.4 NÚT BÁNH RĂNG CÀI ĐẶT (Góc trên bên phải)
        GameObject gearBtnGo = CreateButton("GearCornerButton", hudContainer.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-25f, -25f), new Vector2(54f, 54f),
            "⚙", 28, FontStyle.Bold,
            new Color(0.08f, 0.12f, 0.20f, 0.94f), new Color(1.0f, 0.76f, 0.25f, 1f));
        manager.gearCornerButton = gearBtnGo.GetComponent<Button>();

        // 6. BẢNG DASHBOARD XUẤT HIỆN BÊN CẠNH (SIDE DASHBOARD DRAWER)
        GameObject dashboardGo = new GameObject("SideDashboardPanel");
        dashboardGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rtDash = dashboardGo.AddComponent<RectTransform>();
        rtDash.anchorMin = new Vector2(1f, 0f);
        rtDash.anchorMax = new Vector2(1f, 1f);
        rtDash.pivot = new Vector2(1f, 0.5f);
        rtDash.anchoredPosition = Vector2.zero;
        rtDash.sizeDelta = new Vector2(500f, 0f); // Chiếm trọn chiều cao, rộng 500px bên phải

        Image dashBg = dashboardGo.AddComponent<Image>();
        dashBg.color = new Color(0.06f, 0.08f, 0.13f, 0.97f);
        dashBg.raycastTarget = true; // Chặn bấm xuyên qua panel

        Outline dashOutline = dashboardGo.AddComponent<Outline>();
        dashOutline.effectColor = new Color(0.20f, 0.45f, 0.75f, 0.70f);
        dashOutline.effectDistance = new Vector2(-2.5f, 0f); // Viền sáng bên mép trái panel

        manager.dashboardPanel = dashboardGo;

        // 6.1 Header của Dashboard
        CreateTextObject("DashTitle", dashboardGo.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(-40f, 36f),
            "⚙️ BẢNG CÀI ĐẶT (DASHBOARD)", 21, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.78f, 0.25f));

        CreateTextObject("DashSubtitle", dashboardGo.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -62f), new Vector2(-40f, 22f),
            "Ghost Maze • Tùy chỉnh hệ thống (F1 / Tab / Esc)", 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.65f, 0.72f, 0.82f));

        // Nút '✕' đóng Dashboard ở góc trên panel
        GameObject closeBtnGo = CreateButton("CloseDashboardButton", dashboardGo.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -22f), new Vector2(38f, 38f),
            "✕", 18, FontStyle.Bold,
            new Color(0.55f, 0.15f, 0.15f, 0.90f), Color.white);
        manager.closeDashboardButton = closeBtnGo.GetComponent<Button>();

        CreateDivider("DividerHeader", dashboardGo.transform, -85f, 460f);

        // 6.2 THANH ĐIỀU HƯỚNG TAB (3 MỤC LỚN: HÌNH ẢNH | ÂM THANH | ĐIỀU KHIỂN)
        GameObject tabGroup = new GameObject("TabNavigationGroup");
        tabGroup.transform.SetParent(dashboardGo.transform, false);
        RectTransform rtTabs = tabGroup.AddComponent<RectTransform>();
        rtTabs.anchorMin = new Vector2(0.5f, 1f);
        rtTabs.anchorMax = new Vector2(0.5f, 1f);
        rtTabs.pivot = new Vector2(0.5f, 1f);
        rtTabs.anchoredPosition = new Vector2(0f, -98f);
        rtTabs.sizeDelta = new Vector2(460f, 44f);

        float tabW = 150f;
        GameObject btnTabG = CreateButton("Tab_Graphics", tabGroup.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 42f),
            "🎨 Hình Ảnh", 14, FontStyle.Bold,
            new Color(0.18f, 0.45f, 0.75f), Color.white);
        manager.tabGraphicsButton = btnTabG.GetComponent<Button>();

        GameObject btnTabA = CreateButton("Tab_Audio", tabGroup.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 42f),
            "🔊 Âm Thanh", 14, FontStyle.Bold,
            new Color(0.12f, 0.16f, 0.24f), Color.white);
        manager.tabAudioButton = btnTabA.GetComponent<Button>();

        GameObject btnTabC = CreateButton("Tab_Controls", tabGroup.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 42f),
            "🎮 Điều Khiển", 14, FontStyle.Bold,
            new Color(0.12f, 0.16f, 0.24f), Color.white);
        manager.tabControlsButton = btnTabC.GetComponent<Button>();

        // Container chung cho nội dung các Tab
        GameObject contentContainer = new GameObject("TabContentContainer");
        contentContainer.transform.SetParent(dashboardGo.transform, false);
        RectTransform rtContent = contentContainer.AddComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0.5f, 1f);
        rtContent.anchorMax = new Vector2(0.5f, 1f);
        rtContent.pivot = new Vector2(0.5f, 1f);
        rtContent.anchoredPosition = new Vector2(0f, -155f);
        rtContent.sizeDelta = new Vector2(460f, 780f);

        // =====================================================================
        // TAB 1: 🎨 HÌNH ẢNH (GRAPHICS & FPS BOOST)
        // =====================================================================
        GameObject tabGContent = new GameObject("TabContent_Graphics");
        tabGContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtG = tabGContent.AddComponent<RectTransform>();
        rtG.anchorMin = Vector2.zero;
        rtG.anchorMax = Vector2.one;
        rtG.sizeDelta = Vector2.zero;
        manager.tabGraphicsContent = tabGContent;

        CreateTextObject("G_Header1", tabGContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f),
            "🎨 1. CHẤT LƯỢNG ĐỒ HỌA (GRAPHICS)", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.78f, 1.0f));

        GameObject gStatusGo = CreateTextObject("G_Status", tabGContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -38f), new Vector2(0f, 22f),
            "Chất Lượng: Siêu Đẹp (Ultra) | Độ Nét: 130%", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.85f, 0.90f, 1.0f));
        manager.graphicsStatusText = gStatusGo.GetComponent<Text>();

        // Nút lớn Tăng Đồ Họa Cực Đại
        GameObject btnBoostG = CreateButton("Btn_BoostGraphics", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -78f), new Vector2(460f, 44f),
            "🌟 TĂNG ĐỒ HỌA CỰC ĐẠI (SIÊU NÉT 130%)", 14, FontStyle.Bold,
            new Color(0.14f, 0.44f, 0.74f), Color.white);
        btnBoostG.GetComponent<Button>().onClick.AddListener(manager.ApplyGraphicsUltra);

        // 3 nút lựa chọn chi tiết
        float w3 = 148f;
        GameObject btnG_U = CreateButton("Btn_G_Ultra", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-154f, -132f), new Vector2(w3, 38f),
            "🌟 Siêu Đẹp (130%)", 12, FontStyle.Bold,
            new Color(0.16f, 0.30f, 0.48f), Color.white);
        btnG_U.GetComponent<Button>().onClick.AddListener(manager.ApplyGraphicsUltra);

        GameObject btnG_M = CreateButton("Btn_G_Med", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -132f), new Vector2(w3, 38f),
            "⚖️ Cân Bằng (100%)", 12, FontStyle.Bold,
            new Color(0.18f, 0.24f, 0.35f), new Color(0.9f, 0.9f, 0.9f));
        btnG_M.GetComponent<Button>().onClick.AddListener(manager.ApplyGraphicsMedium);

        GameObject btnG_L = CreateButton("Btn_G_Low", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(154f, -132f), new Vector2(w3, 38f),
            "⚡ Tiết Kiệm (85%)", 12, FontStyle.Bold,
            new Color(0.18f, 0.24f, 0.35f), new Color(0.9f, 0.9f, 0.9f));
        btnG_L.GetComponent<Button>().onClick.AddListener(manager.ApplyGraphicsLow);

        CreateDivider("DividerG", tabGContent.transform, -165f, 460f);

        // Khu vực FPS Boost
        CreateTextObject("FPS_Header", tabGContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -188f), new Vector2(0f, 26f),
            "🚀 2. TỐC ĐỘ KHUNG HÌNH (FPS BOOST)", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.92f, 0.55f));

        GameObject fpsStatusGo = CreateTextObject("FPS_Status", tabGContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -216f), new Vector2(0f, 22f),
            "Mục Tiêu FPS: Không Giới Hạn (Max FPS)", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.85f, 1.0f, 0.90f));
        manager.fpsStatusText = fpsStatusGo.GetComponent<Text>();

        GameObject btnBoostFPS = CreateButton("Btn_BoostFPS", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -256f), new Vector2(460f, 44f),
            "🔥 TĂNG TỐI ĐA FPS (MỞ KHÓA KHUNG HÌNH)", 14, FontStyle.Bold,
            new Color(0.12f, 0.56f, 0.32f), Color.white);
        btnBoostFPS.GetComponent<Button>().onClick.AddListener(manager.ApplyMaxFPS);

        float w4 = 110f;
        GameObject btnF_Max = CreateButton("Btn_F_Max", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-172f, -310f), new Vector2(w4, 38f),
            "🔥 Max FPS", 12, FontStyle.Bold,
            new Color(0.14f, 0.40f, 0.26f), Color.white);
        btnF_Max.GetComponent<Button>().onClick.AddListener(manager.ApplyMaxFPS);

        GameObject btnF_120 = CreateButton("Btn_F_120", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-58f, -310f), new Vector2(w4, 38f),
            "⚡ 120 FPS", 12, FontStyle.Bold,
            new Color(0.18f, 0.28f, 0.35f), Color.white);
        btnF_120.GetComponent<Button>().onClick.AddListener(manager.Apply120FPS);

        GameObject btnF_60 = CreateButton("Btn_F_60", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(58f, -310f), new Vector2(w4, 38f),
            "🔒 60 FPS", 12, FontStyle.Bold,
            new Color(0.18f, 0.28f, 0.35f), Color.white);
        btnF_60.GetComponent<Button>().onClick.AddListener(manager.Apply60FPS);

        GameObject btnF_VSync = CreateButton("Btn_F_VSync", tabGContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(172f, -310f), new Vector2(w4, 38f),
            "🖥️ V-Sync", 12, FontStyle.Bold,
            new Color(0.20f, 0.26f, 0.35f), Color.white);
        btnF_VSync.GetComponent<Button>().onClick.AddListener(manager.ToggleVSync);

        // =====================================================================
        // TAB 2: 🔊 ÂM THANH (AUDIO CONTROLS)
        // =====================================================================
        GameObject tabAContent = new GameObject("TabContent_Audio");
        tabAContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtA = tabAContent.AddComponent<RectTransform>();
        rtA.anchorMin = Vector2.zero;
        rtA.anchorMax = Vector2.one;
        rtA.sizeDelta = Vector2.zero;
        tabAContent.SetActive(false);
        manager.tabAudioContent = tabAContent;

        CreateTextObject("A_Header", tabAContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f),
            "🔊 ĐIỀU CHỈNH ÂM LƯỢNG HỆ THỐNG", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.78f, 0.35f));

        // Slider 1: Master Volume
        GameObject lblMaster = CreateTextObject("Lbl_Master", tabAContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -45f), new Vector2(0f, 22f),
            "Âm Lượng Tổng: 100%", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        manager.masterVolumeLabel = lblMaster.GetComponent<Text>();

        GameObject sldMaster = CreateSimpleSlider("Slider_Master", tabAContent.transform,
            new Vector2(0f, -80f), new Vector2(460f, 24f), 0f, 1f, 1.0f);
        manager.masterVolumeSlider = sldMaster.GetComponent<Slider>();

        // Slider 2: Music Volume
        GameObject lblMusic = CreateTextObject("Lbl_Music", tabAContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -120f), new Vector2(0f, 22f),
            "Nhạc Nền Ma Mị: 80%", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        manager.musicVolumeLabel = lblMusic.GetComponent<Text>();

        GameObject sldMusic = CreateSimpleSlider("Slider_Music", tabAContent.transform,
            new Vector2(0f, -155f), new Vector2(460f, 24f), 0f, 1f, 0.8f);
        manager.musicVolumeSlider = sldMusic.GetComponent<Slider>();

        // Slider 3: SFX Volume
        GameObject lblSFX = CreateTextObject("Lbl_SFX", tabAContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -195f), new Vector2(0f, 22f),
            "Hiệu Ứng & Tiếng Bước Chân: 90%", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        manager.sfxVolumeLabel = lblSFX.GetComponent<Text>();

        GameObject sldSFX = CreateSimpleSlider("Slider_SFX", tabAContent.transform,
            new Vector2(0f, -230f), new Vector2(460f, 24f), 0f, 1f, 0.9f);
        manager.sfxVolumeSlider = sldSFX.GetComponent<Slider>();

        CreateDivider("DividerA", tabAContent.transform, -275f, 460f);

        // Nút Tắt / Bật Toàn Bộ Tiếng
        GameObject btnMute = CreateButton("Btn_Mute", tabAContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -310f), new Vector2(460f, 44f),
            "🔊 ĐANG BẬT TIẾNG (BẤM ĐỂ TẮT)", 14, FontStyle.Bold,
            new Color(0.25f, 0.35f, 0.50f), Color.white);
        manager.muteButton = btnMute.GetComponent<Button>();
        manager.muteButtonLabel = btnMute.transform.Find("Text").GetComponent<Text>();

        // =====================================================================
        // TAB 3: 🎮 ĐIỀU KHIỂN (CONTROLS & KEYBINDINGS)
        // =====================================================================
        GameObject tabCContent = new GameObject("TabContent_Controls");
        tabCContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtC = tabCContent.AddComponent<RectTransform>();
        rtC.anchorMin = Vector2.zero;
        rtC.anchorMax = Vector2.one;
        rtC.sizeDelta = Vector2.zero;
        tabCContent.SetActive(false);
        manager.tabControlsContent = tabCContent;

        CreateTextObject("C_Header", tabCContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f),
            "🎮 ĐỘ NHẠY & THAO TÁC CHUỘT", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.90f, 0.65f));

        GameObject lblSens = CreateTextObject("Lbl_Sens", tabCContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -45f), new Vector2(0f, 22f),
            "Độ Nhạy Chuột: 1.0x", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        manager.mouseSensitivityLabel = lblSens.GetComponent<Text>();

        GameObject sldSens = CreateSimpleSlider("Slider_Sens", tabCContent.transform,
            new Vector2(0f, -80f), new Vector2(460f, 24f), 0.2f, 3.5f, 1.0f);
        manager.mouseSensitivitySlider = sldSens.GetComponent<Slider>();

        GameObject btnInvertY = CreateButton("Btn_InvertY", tabCContent.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -125f), new Vector2(460f, 40f),
            "🔄 Đảo Trục Y: ĐANG TẮT", 13, FontStyle.Bold,
            new Color(0.18f, 0.26f, 0.38f), Color.white);
        manager.invertYButton = btnInvertY.GetComponent<Button>();
        manager.invertYButtonLabel = btnInvertY.transform.Find("Text").GetComponent<Text>();

        CreateDivider("DividerC", tabCContent.transform, -160f, 460f);

        CreateTextObject("C_KeysHeader", tabCContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -180f), new Vector2(0f, 26f),
            "⌨️ HƯỚNG DẪN BÀN PHÍM (KEYBINDINGS)", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.85f, 0.35f));

        string keysInfo = "• [W] [A] [S] [D] / Mũi tên : Di chuyển nhân vật\n" +
                          "• [Left Shift] : Chạy nước rút (Sprint)\n" +
                          "• [Space] : Nhảy cao tiếp đất\n" +
                          "• [Di Chuột] : Xoay góc nhìn thứ ba 360°\n" +
                          "• [Con Lăn Chuột] : Thu phóng Camera gần/xa\n" +
                          "• [F1] / [Tab] / [Esc] : Bật / Tắt nhanh Dashboard";

        CreateTextObject("C_KeysBody", tabCContent.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -220f), new Vector2(0f, 150f),
            keysInfo, 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.90f, 0.95f));

        // =====================================================================
        // FOOTER: NÚT ĐÓNG DASHBOARD Ở ĐÁY PANEL
        // =====================================================================
        GameObject btnCloseBottom = CreateButton("Btn_CloseBottom", dashboardGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 35f), new Vector2(460f, 48f),
            "✕ ĐÓNG CÀI ĐẶT (TIẾP TỤC CHƠI)", 15, FontStyle.Bold,
            new Color(0.68f, 0.18f, 0.18f), Color.white);
        btnCloseBottom.GetComponent<Button>().onClick.AddListener(manager.CloseDashboard);

        // Mặc định ẩn dashboard khi bắt đầu
        dashboardGo.SetActive(false);

        // Lưu Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=green>[GameSettingsUISetup] ĐÃ TẠO THÀNH CÔNG BẢNG DASHBOARD BÊN CẠNH VÀ NÚT BÁNH RĂNG!</color>");
    }

    private static void SetupEventSystem()
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
        }

        var oldModule = es.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
        {
            Object.DestroyImmediate(oldModule);
        }

        var newModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (newModule == null)
        {
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private static GameObject CreateTextObject(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta,
        string textContent, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Text txt = go.AddComponent<Text>();
        txt.font = defaultFont;
        txt.text = textContent;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.alignment = alignment;
        txt.color = color;
        txt.raycastTarget = false;

        return go;
    }

    private static GameObject CreateButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta,
        string buttonText, int fontSize, FontStyle fontStyle,
        Color normalColor, Color textColor)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Image img = btnGo.AddComponent<Image>();
        img.color = normalColor;
        img.raycastTarget = true;

        Outline outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.40f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGo.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = new Color(normalColor.r * 1.35f, normalColor.g * 1.35f, normalColor.b * 1.35f, 1f);
        cb.pressedColor = new Color(normalColor.r * 0.7f, normalColor.g * 0.7f, normalColor.b * 0.7f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        CreateTextObject("Text", btnGo.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            buttonText, fontSize, fontStyle, TextAnchor.MiddleCenter, textColor);

        return btnGo;
    }

    private static GameObject CreateSimpleSlider(string name, Transform parent,
        Vector2 anchoredPosition, Vector2 sizeDelta, float min, float max, float defaultVal)
    {
        GameObject sliderGo = new GameObject(name);
        sliderGo.transform.SetParent(parent, false);

        RectTransform rt = sliderGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        // Background
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform rtBg = bgGo.AddComponent<RectTransform>();
        rtBg.anchorMin = Vector2.zero;
        rtBg.anchorMax = Vector2.one;
        rtBg.sizeDelta = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.16f, 0.24f, 1f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform rtFillArea = fillArea.AddComponent<RectTransform>();
        rtFillArea.anchorMin = new Vector2(0f, 0f);
        rtFillArea.anchorMax = new Vector2(1f, 1f);
        rtFillArea.sizeDelta = new Vector2(-20f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform rtFill = fill.AddComponent<RectTransform>();
        rtFill.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.20f, 0.65f, 0.95f, 1f);

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform rtHandleArea = handleArea.AddComponent<RectTransform>();
        rtHandleArea.anchorMin = new Vector2(0f, 0f);
        rtHandleArea.anchorMax = new Vector2(1f, 1f);
        rtHandleArea.sizeDelta = new Vector2(-20f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform rtHandle = handle.AddComponent<RectTransform>();
        rtHandle.sizeDelta = new Vector2(22f, 0f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1.0f, 0.85f, 0.35f, 1f);

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = rtFill;
        slider.handleRect = rtHandle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultVal;

        return sliderGo;
    }

    private static void CreateDivider(string name, Transform parent, float yPos, float width)
    {
        GameObject div = new GameObject(name);
        div.transform.SetParent(parent, false);

        RectTransform rt = div.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(width, 2f);

        Image img = div.AddComponent<Image>();
        img.color = new Color(0.20f, 0.32f, 0.48f, 0.50f);
        img.raycastTarget = false;
    }
}
