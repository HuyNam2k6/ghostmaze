using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Bảng Dashboard Bên Cạnh (Side Dashboard Panel)")]
    [Tooltip("Panel Dashboard cài đặt bên cạnh màn hình")]
    public GameObject dashboardPanel;

    [Tooltip("Nút bánh răng mở nhanh cài đặt ở góc màn hình")]
    public Button gearCornerButton;

    [Tooltip("Nút đóng Dashboard")]
    public Button closeDashboardButton;

    [Header("Các Tab Mục Lớn (Dashboard Tabs)")]
    public Button tabGraphicsButton;
    public Button tabAudioButton;
    public Button tabControlsButton;

    public GameObject tabGraphicsContent;
    public GameObject tabAudioContent;
    public GameObject tabControlsContent;

    [Header("1. Hình Ảnh (Graphics & FPS UI)")]
    public Text fpsCounterText;
    public Text graphicsStatusText;
    public Text fpsStatusText;

    [Header("Cấu Hình Mức Đồ Họa")]
    [Range(1.0f, 1.6f)] public float ultraRenderScale = 1.30f;
    public float ultraShadowDistance = 80f;
    [Range(0.8f, 1.2f)] public float mediumRenderScale = 1.0f;
    public float mediumShadowDistance = 50f;
    [Range(0.6f, 1.0f)] public float lowRenderScale = 0.85f;
    public float lowShadowDistance = 25f;

    [Header("Cấu Hình FPS")]
    public int maxUnlockedFPS = -1;
    public int highFPS = 120;
    public int standardFPS = 60;

    [Header("2. Âm Thanh (Audio UI)")]
    public Slider masterVolumeSlider;
    public Text masterVolumeLabel;
    public Slider musicVolumeSlider;
    public Text musicVolumeLabel;
    public Slider sfxVolumeSlider;
    public Text sfxVolumeLabel;
    public Button muteButton;
    public Text muteButtonLabel;

    [Header("3. Điều Khiển (Controls UI)")]
    public Slider mouseSensitivitySlider;
    public Text mouseSensitivityLabel;
    public Button invertYButton;
    public Text invertYButtonLabel;

    // Trạng thái nội bộ
    public enum GraphicsLevel { Low, Medium, Ultra }
    private GraphicsLevel currentGraphics = GraphicsLevel.Ultra;
    private int currentTargetFPS = -1;
    private bool isVSyncEnabled = false;

    private float masterVolume = 1.0f;
    private float musicVolume = 0.8f;
    private float sfxVolume = 0.9f;
    private bool isMuted = false;

    private float mouseSensitivity = 1.0f;
    private bool isInvertY = false;

    // Bộ đếm FPS mượt
    private float fpsUpdateTimer = 0f;
    private float fpsUpdateInterval = 0.25f;
    private float smoothedDeltaTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Tối ưu hóa Canvas hiển thị siêu sắc nét (ScreenSpaceOverlay + PixelPerfect + dynamicPixelsPerUnit 2.5)
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
        }

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.5f;
            scaler.referencePixelsPerUnit = 100f;
        }

        // Tránh quá tải GPU/VRAM gây D3D11 device reset khi chạy Android hoặc mở 4 cửa sổ test song song
        bool isPopupTestWindow = !Application.isEditor
            && (Screen.width <= 1024 || System.Array.Exists(System.Environment.GetCommandLineArgs(),
                a => string.Equals(a, "-popupwindow", System.StringComparison.OrdinalIgnoreCase)));

        if (isPopupTestWindow)
        {
            Apply60FPS();
            ApplyGraphicsLow();
        }
        else
        {
            Apply60FPS();
            ApplyGraphicsMedium();
        }

        EnsureCameraRenderSafety();

        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
        }
    }

    private void EnsureCameraRenderSafety()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var urpCam = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urpCam != null && urpCam.renderPostProcessing)
        {
            Shader uber = Shader.Find("Hidden/Universal Render Pipeline/UberPost");
            if (uber == null || !uber.isSupported)
            {
                // Khi build standalone bị strip UberPost, tắt post-processing trên Main Camera để URP FinalBlit vẽ 3D ra màn hình bình thường
                urpCam.renderPostProcessing = false;
            }
        }
    }

    private void Start()
    {
        // Gắn sự kiện mở/đóng
        if (gearCornerButton != null)
        {
            gearCornerButton.onClick.AddListener(ToggleDashboard);
            if (GhostMaze.UI.MiniMapManager.Instance != null)
            {
                gearCornerButton.gameObject.SetActive(false);
            }
        }
        if (closeDashboardButton != null) closeDashboardButton.onClick.AddListener(CloseDashboard);

        // Gắn sự kiện chuyển Tab
        if (tabGraphicsButton != null) tabGraphicsButton.onClick.AddListener(() => SwitchTab(0));
        if (tabAudioButton != null) tabAudioButton.onClick.AddListener(() => SwitchTab(1));
        if (tabControlsButton != null) tabControlsButton.onClick.AddListener(() => SwitchTab(2));

        // Gắn sự kiện Slider Âm Thanh
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);

        // Gắn sự kiện Slider Điều Khiển
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (invertYButton != null) invertYButton.onClick.AddListener(ToggleInvertY);

        // Tự động bổ sung mục Cài Đặt Bố Cục Nút Bấm (kéo thả như Free Fire) vào Tab Điều Khiển
        InjectCustomHUDSettingsSection();

        // Khởi tạo Tab đầu tiên
        SwitchTab(0);
        UpdateAudioLabels();
        UpdateControlLabels();
        UpdateStatusLabels();
    }

    private void Update()
    {
        HandleShortcutInput();
        UpdateFPSCounter();
    }

    private void HandleShortcutInput()
    {
        bool allowF1Settings = PhotonMultiplayerChatVoice.Instance == null;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame || (allowF1Settings && kb.f1Key.wasPressedThisFrame))
            {
                ToggleDashboard();
            }
            else if (kb.escapeKey.wasPressedThisFrame && dashboardPanel != null && dashboardPanel.activeSelf)
            {
                CloseDashboard();
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Tab) || (allowF1Settings && Input.GetKeyDown(KeyCode.F1)))
        {
            ToggleDashboard();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && dashboardPanel != null && dashboardPanel.activeSelf)
        {
            CloseDashboard();
        }
#endif
    }

    private void UpdateFPSCounter()
    {
        smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
        fpsUpdateTimer += Time.unscaledDeltaTime;

        if (fpsUpdateTimer >= fpsUpdateInterval)
        {
            fpsUpdateTimer = 0f;
            float currentFps = 1.0f / Mathf.Max(0.0001f, smoothedDeltaTime);
            float ms = smoothedDeltaTime * 1000f;

            if (fpsCounterText != null)
            {
                string fpsColor = currentFps >= 90f ? "#2ECC71" : (currentFps >= 50f ? "#F1C40F" : "#E74C3C");
                fpsCounterText.text = $"<color={fpsColor}>{Mathf.RoundToInt(currentFps)} FPS</color>  ({ms:F1} ms)";
            }
        }
    }

    // =========================================================================
    // MỞ / ĐÓNG DASHBOARD BÊN CẠNH
    // =========================================================================

    public void ToggleDashboard()
    {
        if (dashboardPanel == null) return;
        if (dashboardPanel.activeSelf) CloseDashboard();
        else OpenDashboard();
    }

    public void OpenDashboard()
    {
        if (dashboardPanel != null) dashboardPanel.SetActive(true);
        ThirdPersonCamera.isUIPanelOpen = true;
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        UpdateStatusLabels();
        UpdateAudioLabels();
        UpdateControlLabels();
    }

    public void CloseDashboard()
    {
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
        bool typing = PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsTypingChat();
        bool editingHUD = VirtualJoystickTouchUI.Instance != null && VirtualJoystickTouchUI.Instance.IsEditingHUDLayout;
        ThirdPersonCamera.isUIPanelOpen = typing || editingHUD;
        if (!Application.isMobilePlatform && !editingHUD)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // =========================================================================
    // CHUYỂN ĐỔI TAB (DASHBOARD TABS)
    // =========================================================================

    public void SwitchTab(int tabIndex)
    {
        if (tabGraphicsContent != null) tabGraphicsContent.SetActive(tabIndex == 0);
        if (tabAudioContent != null) tabAudioContent.SetActive(tabIndex == 1);
        if (tabControlsContent != null) tabControlsContent.SetActive(tabIndex == 2);

        // Highlight tab đang chọn
        SetTabButtonStyle(tabGraphicsButton, tabIndex == 0, new Color(0.18f, 0.45f, 0.75f));
        SetTabButtonStyle(tabAudioButton, tabIndex == 1, new Color(0.68f, 0.42f, 0.15f));
        SetTabButtonStyle(tabControlsButton, tabIndex == 2, new Color(0.20f, 0.60f, 0.40f));
    }

    private void SetTabButtonStyle(Button btn, bool isSelected, Color activeColor)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isSelected ? activeColor : new Color(0.12f, 0.16f, 0.24f, 0.90f);
        }
    }

    // =========================================================================
    // 1. HÌNH ẢNH & TĂNG FPS (GRAPHICS CONTROLS)
    // =========================================================================

    public void ApplyGraphicsUltra()
    {
        currentGraphics = GraphicsLevel.Ultra;
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = ultraRenderScale;
            urpAsset.shadowDistance = ultraShadowDistance;
            urpAsset.msaaSampleCount = 4;
        }

        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        UpdateStatusLabels();
        Debug.Log($"<color=#48C5FF>[Cài Đặt] Đồ họa SIÊU ĐẸP (Scale: {ultraRenderScale * 100}%, Bóng: {ultraShadowDistance}m, MSAA: 4x)</color>");
    }

    public void ApplyGraphicsMedium()
    {
        currentGraphics = GraphicsLevel.Medium;
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = mediumRenderScale;
            urpAsset.shadowDistance = mediumShadowDistance;
            urpAsset.msaaSampleCount = 2;
        }

        UpdateStatusLabels();
        Debug.Log($"<color=#F1C40F>[Cài Đặt] Đồ họa CÂN BẰNG (Scale: {mediumRenderScale * 100}%, Bóng: {mediumShadowDistance}m)</color>");
    }

    public void ApplyGraphicsLow()
    {
        currentGraphics = GraphicsLevel.Low;
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = lowRenderScale;
            urpAsset.shadowDistance = lowShadowDistance;
            urpAsset.msaaSampleCount = 1;
        }

        UpdateStatusLabels();
        Debug.Log($"<color=#E67E22>[Cài Đặt] Đồ họa HIỆU NĂNG MƯỢT (Scale: {lowRenderScale * 100}%, Bóng: {lowShadowDistance}m)</color>");
    }

    public void ApplyMaxFPS()
    {
        currentTargetFPS = maxUnlockedFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxUnlockedFPS;
        UpdateStatusLabels();
        Debug.Log("<color=#2ECC71>[Cài Đặt] MỞ KHÓA TỐI ĐA FPS: Không giới hạn, V-Sync TẮT!</color>");
    }

    public void Apply120FPS()
    {
        currentTargetFPS = highFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = highFPS;
        UpdateStatusLabels();
        Debug.Log($"<color=#2ECC71>[Cài Đặt] Mục tiêu 120 FPS!</color>");
    }

    public void Apply60FPS()
    {
        currentTargetFPS = standardFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = standardFPS;
        UpdateStatusLabels();
        Debug.Log($"<color=#F1C40F>[Cài Đặt] Khóa 60 FPS!</color>");
    }

    public void ToggleVSync()
    {
        isVSyncEnabled = !isVSyncEnabled;
        QualitySettings.vSyncCount = isVSyncEnabled ? 1 : 0;
        Application.targetFrameRate = isVSyncEnabled ? -1 : currentTargetFPS;
        UpdateStatusLabels();
    }

    private void UpdateStatusLabels()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        float scale = urpAsset != null ? urpAsset.renderScale : 1.0f;

        if (graphicsStatusText != null)
        {
            string gName = currentGraphics == GraphicsLevel.Ultra ? "<color=#48C5FF>Siêu Đẹp (Ultra)</color>" :
                          (currentGraphics == GraphicsLevel.Medium ? "<color=#F1C40F>Cân Bằng</color>" : "<color=#E67E22>Hiệu Năng</color>");
            graphicsStatusText.text = $"Chất Lượng: {gName} | Độ Nét: {Mathf.RoundToInt(scale * 100)}%";
        }

        if (fpsStatusText != null)
        {
            string fpsStr = isVSyncEnabled ? "<color=#3498DB>V-Sync (Đồng bộ màn hình)</color>" :
                           (currentTargetFPS <= 0 ? "<color=#2ECC71>Không Giới Hạn (Max FPS)</color>" : $"<color=#2ECC71>{currentTargetFPS} FPS</color>");
            fpsStatusText.text = $"Mục Tiêu FPS: {fpsStr}";
        }
    }

    // =========================================================================
    // 2. ÂM THANH (AUDIO CONTROLS)
    // =========================================================================

    public void OnMasterVolumeChanged(float val)
    {
        masterVolume = val;
        if (!isMuted) AudioListener.volume = masterVolume;
        UpdateAudioLabels();
    }

    public void OnMusicVolumeChanged(float val)
    {
        musicVolume = val;
        UpdateAudioLabels();
    }

    public void OnSFXVolumeChanged(float val)
    {
        sfxVolume = val;
        UpdateAudioLabels();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        AudioListener.volume = isMuted ? 0f : masterVolume;
        UpdateAudioLabels();
    }

    private void UpdateAudioLabels()
    {
        if (masterVolumeLabel != null)
            masterVolumeLabel.text = $"Âm Lượng Tổng: {Mathf.RoundToInt(masterVolume * 100)}%";
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = $"Nhạc Nền Ma Mị: {Mathf.RoundToInt(musicVolume * 100)}%";
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = $"Hiệu Ứng & Tiếng Bước Chân: {Mathf.RoundToInt(sfxVolume * 100)}%";

        if (muteButtonLabel != null)
            muteButtonLabel.text = isMuted ? "🔇 ĐANG TẮT TIẾNG (MUTE)" : "🔊 ĐANG BẬT TIẾNG";
    }

    // =========================================================================
    // 3. ĐIỀU KHIỂN & ĐỘ NHẠY CHUỘT (CONTROLS)
    // =========================================================================

    public void OnSensitivityChanged(float val)
    {
        mouseSensitivity = val;
        var cam = Camera.main;
        if (cam != null)
        {
            var tpCam = cam.GetComponent<ThirdPersonCamera>();
            if (tpCam != null) tpCam.mouseSensitivityMultiplier = mouseSensitivity;
        }
        UpdateControlLabels();
    }

    public void ToggleInvertY()
    {
        isInvertY = !isInvertY;
        var cam = Camera.main;
        if (cam != null)
        {
            var tpCam = cam.GetComponent<ThirdPersonCamera>();
            if (tpCam != null) tpCam.invertY = isInvertY;
        }
        UpdateControlLabels();
    }

    private void UpdateControlLabels()
    {
        if (mouseSensitivityLabel != null)
            mouseSensitivityLabel.text = Application.isMobilePlatform
                ? $"Độ Nhạy Xoay Camera: {mouseSensitivity:F1}x"
                : $"Độ Nhạy Chuột / Cảm Ứng: {mouseSensitivity:F1}x";

        if (invertYButtonLabel != null)
            invertYButtonLabel.text = isInvertY ? "Đảo Trục Y: ĐANG BẬT" : "Đảo Trục Y: ĐANG TẮT";
    }

    // =========================================================================
    // 4. TÙY CHỈNH GIAO DIỆN NÚT BẤM (CUSTOM HUD KIỂU FREE FIRE)
    // =========================================================================

    private void InjectCustomHUDSettingsSection()
    {
        if (tabControlsContent == null) return;
        Transform tRoot = tabControlsContent.transform;
        if (tRoot.Find("Btn_CustomizeHUD") != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Đẩy nhóm hướng dẫn bàn phím / cảm ứng xuống dưới để nhường chỗ cho mục Tùy Chỉnh Nút
        Transform divC = tRoot.Find("DividerC");
        if (divC != null)
        {
            var r = divC.GetComponent<RectTransform>();
            if (r != null) r.anchoredPosition = new Vector2(0f, -178f);
        }
        Transform keysHeader = tRoot.Find("C_KeysHeader");
        if (keysHeader != null)
        {
            var r = keysHeader.GetComponent<RectTransform>();
            if (r != null) r.anchoredPosition = new Vector2(0f, -365f);
            var ht = keysHeader.GetComponent<Text>();
            if (ht != null && Application.isMobilePlatform) ht.text = "HƯỚNG DẪN CẢM ỨNG (ANDROID)";
        }
        Transform keysBody = tRoot.Find("C_KeysBody");
        if (keysBody != null)
        {
            var r = keysBody.GetComponent<RectTransform>();
            if (r != null) r.anchoredPosition = new Vector2(0f, -402f);
            var bt = keysBody.GetComponent<Text>();
            if (bt != null && Application.isMobilePlatform)
            {
                bt.text = "• Joystick (Góc trái): Kéo 360° để di chuyển\n" +
                          "• Nửa màn hình phải: Vuốt để xoay góc nhìn 360°\n" +
                          "• Nút FPP/TPP: Đổi góc nhìn thứ 1 & thứ 3\n" +
                          "• Nút Nhảy / Đấm: Thực hiện hành động theo vai trò\n" +
                          "• Thanh Chat / Voice: Bấm Mic [V] hoặc 🔊 Voice để chỉnh";
            }
        }
        Transform cHeader = tRoot.Find("C_Header");
        if (cHeader != null)
        {
            var ct = cHeader.GetComponent<Text>();
            if (ct != null && Application.isMobilePlatform) ct.text = "ĐỘ NHẠY XOAY CAMERA (CẢM ỨNG)";
        }

        // Tiêu đề mục Tùy Chỉnh Nút Bấm
        CreateControlLabel(tRoot, "HUD_CustomHeader", new Vector2(0f, -196f), 26f,
            "TÙY CHỈNH NÚT BẤM (KIỂU FREE FIRE)", 15, FontStyle.Bold, new Color(1.0f, 0.84f, 0.28f), font);

        // Nút chính mở chế độ Kéo Thả Tùy Chỉnh Nút
        CreateControlActionButton(tRoot, "Btn_CustomizeHUD", new Vector2(0f, -228f), new Vector2(460f, 48f),
            "TÙY CHỈNH VỊ TRÍ & CỠ NÚT (KÉO THẢ)", 14,
            new Color(0.12f, 0.56f, 0.42f, 0.96f), new Color(0.28f, 0.92f, 0.85f, 0.9f), font, OpenHUDCustomizationEditor);

        // Nút phụ Khôi phục nhanh vị trí mặc định
        CreateControlActionButton(tRoot, "Btn_ResetHUDQuick", new Vector2(0f, -284f), new Vector2(460f, 36f),
            "KHÔI PHỤC BỐ CỤC NÚT MẶC ĐỊNH", 12,
            new Color(0.18f, 0.25f, 0.36f, 0.95f), new Color(0.35f, 0.52f, 0.72f, 0.7f), font, ResetHUDLayoutToDefault);

        // Mô tả hướng dẫn ngắn gọn
        CreateControlLabel(tRoot, "HUD_CustomHint", new Vector2(0f, -326f), 28f,
            "Vào giao diện kéo thả từng nút (Joystick, Nhảy, Đấm, FPP, Chat) cho vừa tay bạn.", 12, FontStyle.Italic, new Color(0.75f, 0.86f, 0.96f), font);
    }

    public void OpenHUDCustomizationEditor()
    {
        if (VirtualJoystickTouchUI.Instance == null)
        {
            Debug.LogWarning("[GameSettingsManager] Không tìm thấy VirtualJoystickTouchUI trong scene!");
            return;
        }
        CloseDashboard();
        VirtualJoystickTouchUI.Instance.EnterHUDCustomizationMode();
    }

    public void ResetHUDLayoutToDefault()
    {
        if (VirtualJoystickTouchUI.Instance != null)
        {
            VirtualJoystickTouchUI.Instance.ResetSavedHUDLayoutToDefault();
        }
    }

    private void CreateControlLabel(Transform parent, string name, Vector2 pos, float height, string txt, int fontSize, FontStyle style, Color color, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(0f, height);

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = txt;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = color;
        t.raycastTarget = false;
    }

    private void CreateControlActionButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, int fontSize, Color bgColor, Color outlineColor, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.color = bgColor;

        Outline ol = go.AddComponent<Outline>();
        ol.effectColor = outlineColor;
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        RectTransform trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        Text t = txtGo.AddComponent<Text>();
        t.font = font;
        t.text = label;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
    }
}
