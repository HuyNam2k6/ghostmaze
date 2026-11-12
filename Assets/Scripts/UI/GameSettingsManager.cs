using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameSettingsManager : MonoBehaviour
{
    private static GameSettingsManager _instance;
    public static GameSettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameSettingsManager>();
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    /// <summary>
    /// Đảm bảo Singleton GameSettingsManager luôn tồn tại và sẵn sàng mở ở bất kỳ Scene nào (Sảnh Chờ lẫn Trong Trận).
    /// </summary>
    public static GameSettingsManager EnsureInstance()
    {
        if (Instance != null)
        {
            DontDestroyOnLoad(Instance.gameObject);
            Instance.ApplyCrispnessToSettingsUI();
            return Instance;
        }

        _instance = FindAnyObjectByType<GameSettingsManager>();
        if (_instance != null)
        {
            DontDestroyOnLoad(_instance.gameObject);
            _instance.ApplyCrispnessToSettingsUI();
            return _instance;
        }

        GameObject canvasGo = new GameObject("Canvas_GameSettings");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false; // Tắt pixelPerfect để font không bị subpixel blur
        canvas.sortingOrder = 150;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        UIFontHelper.ConfigureCrispCanvas(canvas, scaler);

        canvasGo.AddComponent<GraphicRaycaster>();
        _instance = canvasGo.AddComponent<GameSettingsManager>();
        DontDestroyOnLoad(canvasGo);

        _instance.BuildRuntimeDashboardUI();
        _instance.ApplyCrispnessToSettingsUI();
        return _instance;
    }

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
    [Tooltip("Tab Cài đặt Ngôn ngữ & Pháp lý 18+")]
    public Button tabLanguageButton;

    public GameObject tabGraphicsContent;
    public GameObject tabAudioContent;
    public GameObject tabControlsContent;
    [Tooltip("Nội dung Tab Ngôn ngữ")]
    public GameObject tabLanguageContent;

    private readonly Button[] langOptionButtons = new Button[4];
    private readonly Text[] langOptionStatusTexts = new Text[4];
    private readonly Image[] langOptionBgs = new Image[4];
    private readonly Outline[] langOptionOutlines = new Outline[4];
    private Text tabGraphicsBtnText;
    private Text tabAudioBtnText;
    private Text tabControlsBtnText;
    private Text tabLanguageBtnText;

    // Toàn bộ text labels để cập nhật đa ngôn ngữ toàn cầu
    private Text dashTitleText;
    private Text dashSubtitleText;
    private Text gHeader1Text;
    private Text gBoostBtnText;
    private Text btnUltraText;
    private Text btnMedText;
    private Text btnLowText;
    private Text gHeader2Text;
    private Text gBoostFPSText;
    private Text fpsUnlimitedBtnText;
    private Text fps120BtnText;
    private Text fps60BtnText;
    private Text gHeader3Text;
    private Text vsyncBtnText;
    private Text aHeaderText;
    private Text cHeaderText;
    private Text cKeybindingsHeaderText;
    private Text cKeybindingsBodyText;
    private Text closeBottomBtnText;
    private Text langHeaderText;
    private Text langSubtitleText;

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

    // Chế độ hiển thị Cài Đặt (Sảnh Chờ đầy đủ vs Trong Trận hạn chế)
    public enum SettingsMode { Auto, LobbyFull, InMatch }
    private SettingsMode currentMode = SettingsMode.LobbyFull;
    private int currentActiveTab = 0;
    private Text inMatchNoticeBanner;

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

    // Khóa lưu trữ cấu hình người chơi (PlayerPrefs)
    private const string PREF_GRAPHICS = "GhostMaze_GraphicsLevel";
    private const string PREF_TARGET_FPS = "GhostMaze_TargetFPS";
    private const string PREF_VSYNC = "GhostMaze_VSync";
    private const string PREF_MASTER_VOL = "GhostMaze_MasterVol";
    private const string PREF_MUSIC_VOL = "GhostMaze_MusicVol";
    private const string PREF_SFX_VOL = "GhostMaze_SFXVol";
    private const string PREF_IS_MUTED = "GhostMaze_IsMuted";
    private const string PREF_MOUSE_SENS = "GhostMaze_MouseSens";
    private const string PREF_INVERT_Y = "GhostMaze_InvertY";

    // Bộ đếm FPS mượt
    private float fpsUpdateTimer = 0f;
    private float fpsUpdateInterval = 0.25f;
    private float smoothedDeltaTime = 0f;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Tối ưu hóa Canvas và Font hiển thị siêu sắc nét (ScreenSpaceOverlay + dynamicPixelsPerUnit 2.5)
        ApplyCrispnessToSettingsUI();

        // Tải và khôi phục toàn bộ cài đặt người chơi đã lưu từ trước (Graphics, FPS, Audio, Controls)
        LoadAllSettings();

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

        // Tự động tiêm thêm Tab Ngôn Ngữ (Language) & Bảo Hộ Pháp Lý 18+
        InjectLanguageSettingsTab();

        BindDashboardTexts();

        GameLocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        HandleLanguageChanged(GameLocalizationManager.CurrentLanguage);

        // Khởi tạo Tab đầu tiên
        SwitchTab(0);
        UpdateAudioLabels();
        UpdateControlLabels();
        UpdateStatusLabels();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedForHUD;
        UpdateInGameHUDVisibility(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedForHUD;
    }

    private void OnSceneLoadedForHUD(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UpdateInGameHUDVisibility(scene.name);
    }

    public void SetInMatchHUDVisible(bool visible)
    {
        Transform hud = transform.Find("HUD_Container");
        if (hud == null)
        {
            GameObject g = GameObject.Find("HUD_Container");
            if (g != null) hud = g.transform;
        }
        if (hud != null)
        {
            hud.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            GameObject endBanner = GameObject.Find("EndGameResultBanner");
            if (endBanner != null) Destroy(endBanner);

            GameObject f1Btn = GameObject.Find("Btn_QuickTestF1");
            if (f1Btn != null) Destroy(f1Btn);
        }
    }

    public void UpdateInGameHUDVisibility(string sceneName)
    {
        bool isLobby = (sceneName == "LobbyScene");
        SetInMatchHUDVisible(!isLobby);

        if (isLobby)
        {
            if (gearCornerButton != null) gearCornerButton.gameObject.SetActive(false);
        }
        else
        {
            if (gearCornerButton != null && GhostMaze.UI.MiniMapManager.Instance == null)
            {
                gearCornerButton.gameObject.SetActive(true);
            }
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedForHUD;
        GameLocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void Update()
    {
        HandleShortcutInput();
        UpdateFPSCounter();
    }

    private void HandleShortcutInput()
    {
        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsTypingChat())
        {
            return;
        }
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame)
            {
                ToggleDashboard();
            }
            else if (kb.escapeKey.wasPressedThisFrame && dashboardPanel != null && dashboardPanel.activeSelf)
            {
                CloseDashboard();
            }
            else if (kb.f1Key.wasPressedThisFrame)
            {
                ToggleDashboard();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F1))
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
            if (fpsCounterText != null)
            {
                float fps = 1.0f / Mathf.Max(0.0001f, smoothedDeltaTime);
                float ms = smoothedDeltaTime * 1000f;

                string colorTag = fps >= 55f ? "<color=#2ECC71>" : (fps >= 30f ? "<color=#F1C40F>" : "<color=#E74C3C>");
                fpsCounterText.text = $"{colorTag}{Mathf.RoundToInt(fps)} FPS</color>  ({ms:F1} ms)";
            }
        }
    }

    // =========================================================================
    // LƯU TRỮ VÀ KHÔI PHỤC CÀI ĐẶT NGƯỜI CHƠI (PLAYER PREFS PERSISTENCE)
    // =========================================================================

    private void SaveGraphicsSettings()
    {
        PlayerPrefs.SetInt(PREF_GRAPHICS, (int)currentGraphics);
        PlayerPrefs.SetInt(PREF_TARGET_FPS, currentTargetFPS);
        PlayerPrefs.SetInt(PREF_VSYNC, isVSyncEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SaveAudioSettings()
    {
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, masterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, musicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
        PlayerPrefs.SetInt(PREF_IS_MUTED, isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SaveControlsSettings()
    {
        PlayerPrefs.SetFloat(PREF_MOUSE_SENS, mouseSensitivity);
        PlayerPrefs.SetInt(PREF_INVERT_Y, isInvertY ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadAllSettings()
    {
        // 1. Tải Cài Đặt Đồ Họa & FPS
        int savedGraphics = PlayerPrefs.GetInt(PREF_GRAPHICS, (int)GraphicsLevel.Ultra);
        currentGraphics = (GraphicsLevel)Mathf.Clamp(savedGraphics, 0, 2);

        int savedFPS = PlayerPrefs.GetInt(PREF_TARGET_FPS, -1);
        currentTargetFPS = savedFPS;

        isVSyncEnabled = PlayerPrefs.GetInt(PREF_VSYNC, 0) == 1;

        // Áp dụng Đồ họa
        switch (currentGraphics)
        {
            case GraphicsLevel.Ultra: ApplyGraphicsUltra(false); break;
            case GraphicsLevel.Medium: ApplyGraphicsMedium(false); break;
            case GraphicsLevel.Low: ApplyGraphicsLow(false); break;
        }

        // Áp dụng FPS & VSync
        QualitySettings.vSyncCount = isVSyncEnabled ? 1 : 0;
        Application.targetFrameRate = isVSyncEnabled ? -1 : currentTargetFPS;

        // 2. Tải Cài Đặt Âm Thanh
        masterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 1.0f);
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.9f);
        isMuted = PlayerPrefs.GetInt(PREF_IS_MUTED, 0) == 1;

        AudioListener.volume = isMuted ? 0f : masterVolume;

        if (masterVolumeSlider != null) masterVolumeSlider.value = masterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = musicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVolume;

        // 3. Tải Cài Đặt Điều Khiển
        mouseSensitivity = PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 1.0f);
        isInvertY = PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1;

        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = mouseSensitivity;

        var cam = Camera.main;
        if (cam != null)
        {
            var tpCam = cam.GetComponent<ThirdPersonCamera>();
            if (tpCam != null)
            {
                tpCam.mouseSensitivityMultiplier = mouseSensitivity;
                tpCam.invertY = isInvertY;
            }
        }

        Debug.Log($"<color=#2ECC71>[Cài Đặt] Đã khôi phục cài đặt lưu: Đồ họa={currentGraphics}, FPS={currentTargetFPS}, VSync={isVSyncEnabled}, Vol={masterVolume:F2}, Sens={mouseSensitivity:F1}</color>");
    }

    // =========================================================================
    // MỞ / ĐÓNG DASHBOARD BÊN CẠNH
    // =========================================================================

    public bool IsInMatch()
    {
        if (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.isRunning) return true;
        if (FusionVietnameseLobbyUI.Instance != null && !FusionVietnameseLobbyUI.Instance.IsLobbyShowing()) return true;
        return false;
    }

    public void ToggleDashboard()
    {
        if (dashboardPanel == null) BuildRuntimeDashboardUI();
        if (dashboardPanel == null) return;
        if (dashboardPanel.activeSelf) CloseDashboard();
        else OpenDashboard(SettingsMode.Auto);
    }

    public void OpenDashboard() => OpenDashboard(SettingsMode.Auto);

    public void OpenDashboard(SettingsMode mode)
    {
        if (mode == SettingsMode.Auto)
        {
            mode = IsInMatch() ? SettingsMode.InMatch : SettingsMode.LobbyFull;
        }
        currentMode = mode;

        if (dashboardPanel == null)
        {
            BuildRuntimeDashboardUI();
        }

        ApplyCrispnessToSettingsUI();
        BindDashboardTexts();

        if (dashboardPanel != null) dashboardPanel.SetActive(true);
        ThirdPersonCamera.isUIPanelOpen = true;
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        ApplySettingsModeVisibility();
        HandleLanguageChanged(GameLocalizationManager.CurrentLanguage);
        UpdateStatusLabels();
        UpdateAudioLabels();
        UpdateControlLabels();
    }

    private void ApplySettingsModeVisibility()
    {
        bool isMatch = (currentMode == SettingsMode.InMatch);

        // Trong trận: Ẩn Tab Âm thanh và Tab Ngôn ngữ, chỉ giữ Đồ họa & Điều khiển
        if (tabAudioButton != null) tabAudioButton.gameObject.SetActive(!isMatch);
        if (tabLanguageButton != null) tabLanguageButton.gameObject.SetActive(!isMatch);

        // Nếu đang ở Tab bị ẩn thì lập tức chuyển về Tab 0 (Đồ họa)
        if (isMatch && (currentActiveTab == 1 || currentActiveTab == 3))
        {
            SwitchTab(0);
        }

        EnsureInMatchNoticeBanner();
        if (inMatchNoticeBanner != null)
        {
            inMatchNoticeBanner.gameObject.SetActive(isMatch);
            if (isMatch)
            {
                inMatchNoticeBanner.text = GameLocalizationManager.T("SETTINGS_INMATCH_NOTICE");
            }
        }
    }

    private void EnsureInMatchNoticeBanner()
    {
        if (dashboardPanel == null || inMatchNoticeBanner != null) return;

        Transform existing = dashboardPanel.transform.Find("InMatchNoticeBanner");
        if (existing != null)
        {
            inMatchNoticeBanner = existing.GetComponent<Text>();
            return;
        }

        GameObject bannerGo = new GameObject("InMatchNoticeBanner");
        bannerGo.transform.SetParent(dashboardPanel.transform, false);

        RectTransform rt = bannerGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -145f);
        rt.sizeDelta = new Vector2(560f, 32f);

        Image bg = bannerGo.AddComponent<Image>();
        bg.color = new Color(0.85f, 0.45f, 0.08f, 0.25f);

        Outline ol = bannerGo.AddComponent<Outline>();
        ol.effectColor = new Color(1.0f, 0.65f, 0.15f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(bannerGo.transform, false);
        RectTransform trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        inMatchNoticeBanner = txtGo.AddComponent<Text>();
        inMatchNoticeBanner.font = UIFontHelper.GetCrispFont(14);
        inMatchNoticeBanner.fontSize = 14;
        inMatchNoticeBanner.fontStyle = FontStyle.Bold;
        inMatchNoticeBanner.alignment = TextAnchor.MiddleCenter;
        inMatchNoticeBanner.color = new Color(1.0f, 0.90f, 0.45f);
        inMatchNoticeBanner.raycastTarget = false;
        inMatchNoticeBanner.text = GameLocalizationManager.T("SETTINGS_INMATCH_NOTICE");
    }

    public void CloseDashboard()
    {
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
        ThirdPersonCamera.isUIPanelOpen = false;

        // Chỉ khóa chuột khi đang thực sự trong trận đấu
        if (!Application.isMobilePlatform && IsInMatch())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SwitchTab(int tabIndex)
    {
        currentActiveTab = tabIndex;
        if (tabGraphicsContent != null) tabGraphicsContent.SetActive(tabIndex == 0);
        if (tabAudioContent != null) tabAudioContent.SetActive(tabIndex == 1);
        if (tabControlsContent != null) tabControlsContent.SetActive(tabIndex == 2);
        if (tabLanguageContent != null) tabLanguageContent.SetActive(tabIndex == 3);

        // Highlight tab đang chọn
        SetTabButtonStyle(tabGraphicsButton, tabIndex == 0, new Color(0.18f, 0.45f, 0.75f));
        SetTabButtonStyle(tabAudioButton, tabIndex == 1, new Color(0.68f, 0.42f, 0.15f));
        SetTabButtonStyle(tabControlsButton, tabIndex == 2, new Color(0.20f, 0.60f, 0.40f));
        SetTabButtonStyle(tabLanguageButton, tabIndex == 3, new Color(0.52f, 0.22f, 0.72f));
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

    public void ApplyGraphicsUltra() => ApplyGraphicsUltra(true);
    public void ApplyGraphicsUltra(bool save)
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
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#48C5FF>[Cài Đặt] Đồ họa SIÊU ĐẸP (Scale: {ultraRenderScale * 100}%, Bóng: {ultraShadowDistance}m, MSAA: 4x)</color>");
    }

    public void ApplyGraphicsMedium() => ApplyGraphicsMedium(true);
    public void ApplyGraphicsMedium(bool save)
    {
        currentGraphics = GraphicsLevel.Medium;
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = mediumRenderScale;
            urpAsset.shadowDistance = mediumShadowDistance;
            urpAsset.msaaSampleCount = 2;
        }

        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        UpdateStatusLabels();
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#F1C40F>[Cài Đặt] Đồ họa CÂN BẰNG (Scale: {mediumRenderScale * 100}%, Bóng: {mediumShadowDistance}m, MSAA: 2x)</color>");
    }

    public void ApplyGraphicsLow() => ApplyGraphicsLow(true);
    public void ApplyGraphicsLow(bool save)
    {
        currentGraphics = GraphicsLevel.Low;
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = lowRenderScale;
            urpAsset.shadowDistance = lowShadowDistance;
            urpAsset.msaaSampleCount = 1;
        }

        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        UpdateStatusLabels();
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#E67E22>[Cài Đặt] Đồ họa TIẾT KIỆM PIN (Scale: {lowRenderScale * 100}%, Bóng: {lowShadowDistance}m, Tắt MSAA)</color>");
    }

    public void ApplyMaxFPS() => ApplyMaxFPS(true);
    public void ApplyMaxFPS(bool save)
    {
        currentTargetFPS = maxUnlockedFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxUnlockedFPS;
        UpdateStatusLabels();
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#2ECC71>[Cài Đặt] Mở khóa FPS không giới hạn (Max FPS)!</color>");
    }

    public void Apply120FPS() => Apply120FPS(true);
    public void Apply120FPS(bool save)
    {
        currentTargetFPS = highFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = highFPS;
        UpdateStatusLabels();
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#2ECC71>[Cài Đặt] Khóa 120 FPS Siêu Mượt!</color>");
    }

    public void Apply60FPS() => Apply60FPS(true);
    public void Apply60FPS(bool save)
    {
        currentTargetFPS = standardFPS;
        isVSyncEnabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = standardFPS;
        UpdateStatusLabels();
        if (save) SaveGraphicsSettings();
        Debug.Log($"<color=#F1C40F>[Cài Đặt] Khóa 60 FPS!</color>");
    }

    public void ToggleVSync()
    {
        isVSyncEnabled = !isVSyncEnabled;
        QualitySettings.vSyncCount = isVSyncEnabled ? 1 : 0;
        Application.targetFrameRate = isVSyncEnabled ? -1 : currentTargetFPS;
        UpdateStatusLabels();
        SaveGraphicsSettings();
    }

    private void UpdateStatusLabels()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        float scale = urpAsset != null ? urpAsset.renderScale : 1.0f;

        if (graphicsStatusText != null)
        {
            string gName = currentGraphics == GraphicsLevel.Ultra ? "<color=#48C5FF>" + GameLocalizationManager.T("SETTINGS_GRAPHICS_ULTRA") + "</color>" :
                          (currentGraphics == GraphicsLevel.Medium ? "<color=#F1C40F>" + GameLocalizationManager.T("SETTINGS_GRAPHICS_MED") + "</color>" : "<color=#E67E22>" + GameLocalizationManager.T("SETTINGS_GRAPHICS_LOW") + "</color>");
            graphicsStatusText.text = $"Chất Lượng: {gName} | Độ Nét: {Mathf.RoundToInt(scale * 100)}%";
        }

        if (fpsStatusText != null)
        {
            string fpsStr = isVSyncEnabled ? "<color=#3498DB>V-Sync</color>" :
                           (currentTargetFPS <= 0 ? "<color=#2ECC71>" + GameLocalizationManager.T("SETTINGS_FPS_UNLIMITED") + "</color>" : $"<color=#2ECC71>{currentTargetFPS} FPS</color>");
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
        SaveAudioSettings();
    }

    public void OnMusicVolumeChanged(float val)
    {
        musicVolume = val;
        UpdateAudioLabels();
        SaveAudioSettings();
    }

    public void OnSFXVolumeChanged(float val)
    {
        sfxVolume = val;
        UpdateAudioLabels();
        SaveAudioSettings();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        AudioListener.volume = isMuted ? 0f : masterVolume;
        UpdateAudioLabels();
        SaveAudioSettings();
    }

    private void UpdateAudioLabels()
    {
        if (masterVolumeLabel != null)
            masterVolumeLabel.text = $"{GameLocalizationManager.T("SETTINGS_AUDIO_MASTER_LABEL")}: {Mathf.RoundToInt(masterVolume * 100)}%";
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = $"{GameLocalizationManager.T("SETTINGS_AUDIO_MUSIC_LABEL")}: {Mathf.RoundToInt(musicVolume * 100)}%";
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = $"{GameLocalizationManager.T("SETTINGS_AUDIO_SFX_LABEL")}: {Mathf.RoundToInt(sfxVolume * 100)}%";

        if (muteButtonLabel != null)
            muteButtonLabel.text = isMuted ? GameLocalizationManager.T("SETTINGS_AUDIO_MUTE") : GameLocalizationManager.T("SETTINGS_AUDIO_UNMUTE");
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
        SaveControlsSettings();
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
        SaveControlsSettings();
    }

    private void UpdateControlLabels()
    {
        if (mouseSensitivityLabel != null)
            mouseSensitivityLabel.text = $"{GameLocalizationManager.T("SETTINGS_CONTROLS_SENS_LABEL")}: {mouseSensitivity:F1}x";

        if (invertYButtonLabel != null)
            invertYButtonLabel.text = isInvertY ? "🔄 Invert Y: [ ON ]" : "🔄 Invert Y: [ OFF ]";
    }

    // =========================================================================
    // 4. TÙY CHỈNH GIAO DIỆN NÚT BẤM (CUSTOM HUD)
    // =========================================================================

    private void InjectCustomHUDSettingsSection()
    {
        if (tabControlsContent == null) return;
        Transform tRoot = tabControlsContent.transform;
        if (tRoot.Find("Btn_CustomizeHUD") != null) return;

        Font font = UIFontHelper.GetCrispFont(15);

        CreateControlActionButton(tRoot, "Btn_CustomizeHUD", new Vector2(0f, -385f), new Vector2(560f, 48f),
            "🛠️ TÙY CHỈNH BỐ CỤC NÚT (KÉO THẢ TỰ DO)", 15,
            new Color(0.18f, 0.58f, 0.42f, 0.95f), new Color(0.35f, 0.98f, 0.72f, 0.90f), font,
            () =>
            {
                CloseDashboard();
                if (VirtualJoystickTouchUI.Instance != null)
                {
                    VirtualJoystickTouchUI.Instance.EnterHUDCustomizationMode();
                }
                else
                {
                    var touchUI = FindAnyObjectByType<VirtualJoystickTouchUI>();
                    if (touchUI != null) touchUI.EnterHUDCustomizationMode();
                    else Debug.LogWarning("[GameSettingsManager] Không tìm thấy VirtualJoystickTouchUI trong scene!");
                }
            });

        CreateControlActionButton(tRoot, "Btn_ResetHUDLayout", new Vector2(0f, -440f), new Vector2(560f, 42f),
            "🔄 KHÔI PHỤC BỐ CỤC NÚT MẶC ĐỊNH", 14,
            new Color(0.45f, 0.22f, 0.22f, 0.95f), new Color(0.85f, 0.45f, 0.45f, 0.85f), font,
            () =>
            {
                if (VirtualJoystickTouchUI.Instance != null)
                {
                    VirtualJoystickTouchUI.Instance.ResetSavedHUDLayoutToDefault();
                    VirtualJoystickTouchUI.Instance.ResetAllCustomHUDToDefault();
                }
                else
                {
                    var touchUI = FindAnyObjectByType<VirtualJoystickTouchUI>();
                    if (touchUI != null)
                    {
                        touchUI.ResetSavedHUDLayoutToDefault();
                        touchUI.ResetAllCustomHUDToDefault();
                    }
                }
            });
    }

    private void CreateControlLabel(Transform parent, string name, Vector2 pos, float height, string text, int fontSize, FontStyle style, Color color, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(-40f, height);

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
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
        bg.raycastTarget = true;

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

    // =========================================================================
    // 5. CÀI ĐẶT NGÔN NGỮ & BẢO HỘ PHÁP LÝ 18+ (LANGUAGE & 18+ MATURE RATING)
    // =========================================================================

    private void InjectLanguageSettingsTab()
    {
        if (tabGraphicsButton == null || tabGraphicsContent == null) return;
        Transform tabParent = tabGraphicsButton.transform.parent;
        if (tabParent == null) return;

        var hlg = tabParent.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = tabParent.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        hlg.spacing = 6f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        if (tabGraphicsButton != null)
        {
            tabGraphicsBtnText = tabGraphicsButton.GetComponentInChildren<Text>();
            if (tabGraphicsBtnText != null) tabGraphicsBtnText.raycastTarget = false;
        }
        if (tabAudioButton != null)
        {
            tabAudioBtnText = tabAudioButton.GetComponentInChildren<Text>();
            if (tabAudioBtnText != null) tabAudioBtnText.raycastTarget = false;
        }
        if (tabControlsButton != null)
        {
            tabControlsBtnText = tabControlsButton.GetComponentInChildren<Text>();
            if (tabControlsBtnText != null) tabControlsBtnText.raycastTarget = false;
        }

        if (tabLanguageButton == null)
        {
            Transform existingBtn = tabParent.Find("Tab_Language");
            if (existingBtn != null)
            {
                tabLanguageButton = existingBtn.GetComponent<Button>();
                tabLanguageBtnText = existingBtn.GetComponentInChildren<Text>();
                if (tabLanguageBtnText != null) tabLanguageBtnText.raycastTarget = false;
            }
            else
            {
                GameObject tabLangGo = new GameObject("Tab_Language");
                tabLangGo.transform.SetParent(tabParent, false);

                RectTransform rt = tabLangGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(135f, 46f);

                Image bg = tabLangGo.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.16f, 0.24f, 0.90f);
                bg.raycastTarget = true;

                Outline ol = tabLangGo.AddComponent<Outline>();
                ol.effectColor = new Color(1f, 1f, 1f, 0.4f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);

                tabLanguageButton = tabLangGo.AddComponent<Button>();
                tabLanguageButton.targetGraphic = bg;
                Navigation nav = tabLanguageButton.navigation;
                nav.mode = Navigation.Mode.None;
                tabLanguageButton.navigation = nav;

                GameObject txtGo = new GameObject("Text");
                txtGo.transform.SetParent(tabLangGo.transform, false);
                RectTransform trt = txtGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                tabLanguageBtnText = txtGo.AddComponent<Text>();
                tabLanguageBtnText.font = UIFontHelper.GetCrispFont(15);
                tabLanguageBtnText.text = GameLocalizationManager.T("SETTINGS_TAB_LANGUAGE", "🌐 Ngôn Ngữ");
                tabLanguageBtnText.fontSize = 15;
                tabLanguageBtnText.fontStyle = FontStyle.Bold;
                tabLanguageBtnText.alignment = TextAnchor.MiddleCenter;
                tabLanguageBtnText.color = Color.white;
                tabLanguageBtnText.raycastTarget = false;
            }
            if (tabLanguageButton != null)
            {
                tabLanguageButton.onClick.RemoveAllListeners();
                tabLanguageButton.onClick.AddListener(() => SwitchTab(3));
            }
        }

        Transform contentParent = tabGraphicsContent.transform.parent;
        if (contentParent != null && tabLanguageContent == null)
        {
            Transform existingContent = contentParent.Find("TabContent_Language");
            if (existingContent != null)
            {
                tabLanguageContent = existingContent.gameObject;
            }
            else
            {
                tabLanguageContent = new GameObject("TabContent_Language");
                tabLanguageContent.transform.SetParent(contentParent, false);

                RectTransform cRt = tabLanguageContent.AddComponent<RectTransform>();
                cRt.anchorMin = Vector2.zero;
                cRt.anchorMax = Vector2.one;
                cRt.offsetMin = Vector2.zero;
                cRt.offsetMax = Vector2.zero;
                cRt.pivot = new Vector2(0.5f, 0.5f);

                BuildLanguageTabElements(tabLanguageContent.transform);
                tabLanguageContent.SetActive(false);
            }
        }
    }

    private void BuildLanguageTabElements(Transform root)
    {
        Font font = UIFontHelper.GetCrispFont(16);

        // 1. Tiêu đề mục Ngôn Ngữ
        CreateControlLabel(root, "Lang_Header", new Vector2(0f, -10f), 28f,
            "🌐 CÀI ĐẶT NGÔN NGỮ (LANGUAGE SETTINGS)", 16, FontStyle.Bold, new Color(0.35f, 0.85f, 1.0f), font);

        CreateControlLabel(root, "Lang_Subtitle", new Vector2(0f, -40f), 26f,
            "Chọn ngôn ngữ hiển thị trong game / Select display language:", 14, FontStyle.Italic, new Color(0.78f, 0.88f, 0.98f), font);

        string[] langLabels = new string[]
        {
            "🇻🇳  <b>Tiếng Việt</b>  <color=#88AACC>(Vietnamese)</color>",
            "🇬🇧  <b>English</b>  <color=#88AACC>(English)</color>",
            "🇪🇸  <b>Spanish</b>  <color=#88AACC>(Español)</color>",
            "🇨🇳  <b>Chinese</b>  <color=#88AACC>(中文)</color>"
        };

        float startY = -76f;
        float cardH = 54f;
        float spacingY = 12f;

        for (int i = 0; i < 4; i++)
        {
            int langIndex = i;
            float yPos = startY - i * (cardH + spacingY);

            GameObject cardGo = new GameObject($"LangOption_{i}");
            cardGo.transform.SetParent(root, false);
            RectTransform crt = cardGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, yPos);
            crt.sizeDelta = new Vector2(560f, cardH);

            Image cbg = cardGo.AddComponent<Image>();
            cbg.color = new Color(0.08f, 0.12f, 0.20f, 0.92f);
            cbg.raycastTarget = true;
            langOptionBgs[i] = cbg;

            Outline col = cardGo.AddComponent<Outline>();
            col.effectColor = new Color(0.22f, 0.32f, 0.50f, 0.70f);
            col.effectDistance = new Vector2(1.5f, -1.5f);
            langOptionOutlines[i] = col;

            Button cbtn = cardGo.AddComponent<Button>();
            cbtn.targetGraphic = cbg;
            Navigation cnav = cbtn.navigation;
            cnav.mode = Navigation.Mode.None;
            cbtn.navigation = cnav;
            cbtn.onClick.AddListener(() =>
            {
                Debug.Log($"[GameSettingsManager] Người chơi chọn ngôn ngữ: {(GameLocalizationManager.Language)langIndex}");
                GameLocalizationManager.SetLanguage((GameLocalizationManager.Language)langIndex);
            });
            langOptionButtons[i] = cbtn;

            GameObject lTxtGo = new GameObject("LabelText");
            lTxtGo.transform.SetParent(cardGo.transform, false);
            RectTransform ltrt = lTxtGo.AddComponent<RectTransform>();
            ltrt.anchorMin = new Vector2(0f, 0f);
            ltrt.anchorMax = new Vector2(1f, 1f);
            ltrt.offsetMin = new Vector2(20f, 0f);
            ltrt.offsetMax = new Vector2(-120f, 0f);

            Text lt = lTxtGo.AddComponent<Text>();
            lt.font = font;
            lt.fontSize = 16;
            lt.fontStyle = FontStyle.Normal;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = Color.white;
            lt.supportRichText = true;
            lt.text = langLabels[i];
            lt.raycastTarget = false;

            GameObject sTxtGo = new GameObject("StatusText");
            sTxtGo.transform.SetParent(cardGo.transform, false);
            RectTransform strt = sTxtGo.AddComponent<RectTransform>();
            strt.anchorMin = new Vector2(1f, 0f);
            strt.anchorMax = new Vector2(1f, 1f);
            strt.offsetMin = new Vector2(-110f, 0f);
            strt.offsetMax = new Vector2(-15f, 0f);

            Text st = sTxtGo.AddComponent<Text>();
            st.font = font;
            st.fontSize = 14;
            st.fontStyle = FontStyle.Bold;
            st.alignment = TextAnchor.MiddleRight;
            st.color = new Color(0.25f, 0.95f, 0.65f);
            st.supportRichText = true;
            st.raycastTarget = false;
            langOptionStatusTexts[i] = st;
        }

        CreateControlDivider(root, "Divider_Lang", -345f, 560f);

        CreateControlLabel(root, "Legal_Header", new Vector2(0f, -360f), 26f,
            "🔞 BẢO HỘ PHÁP LÝ & PHÂN LOẠI 18+ (MATURE RATING)", 15, FontStyle.Bold, new Color(1.0f, 0.45f, 0.45f), font);

        GameObject legalBox = new GameObject("LegalNoticeBox");
        legalBox.transform.SetParent(root, false);
        RectTransform lbrt = legalBox.AddComponent<RectTransform>();
        lbrt.anchorMin = new Vector2(0.5f, 1f);
        lbrt.anchorMax = new Vector2(0.5f, 1f);
        lbrt.pivot = new Vector2(0.5f, 1f);
        lbrt.anchoredPosition = new Vector2(0f, -395f);
        lbrt.sizeDelta = new Vector2(560f, 115f);

        Image lbbg = legalBox.AddComponent<Image>();
        lbbg.color = new Color(0.18f, 0.05f, 0.07f, 0.88f);

        Outline lbol = legalBox.AddComponent<Outline>();
        lbol.effectColor = new Color(0.85f, 0.18f, 0.22f, 0.75f);
        lbol.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject descGo = new GameObject("DescriptionText");
        descGo.transform.SetParent(legalBox.transform, false);
        RectTransform drt2 = descGo.AddComponent<RectTransform>();
        drt2.anchorMin = Vector2.zero;
        drt2.anchorMax = Vector2.one;
        drt2.offsetMin = new Vector2(14f, 10f);
        drt2.offsetMax = new Vector2(-14f, -10f);

        Text dt = descGo.AddComponent<Text>();
        dt.font = font;
        dt.fontSize = 13;
        dt.lineSpacing = 1.25f;
        dt.alignment = TextAnchor.UpperLeft;
        dt.color = new Color(0.94f, 0.95f, 0.97f, 0.95f);
        dt.supportRichText = true;
        dt.text = "• Trò chơi chứa các phân cảnh kinh dị ma quái, hù dọa hồi hộp và âm thanh căng thẳng, không phù hợp cho trẻ vị thành niên.\n" +
                  "• Đã gắn tag 18+ quốc tế để tuân thủ 100% Chính Sách Gia Đình (Families Policy) của Google Play và Apple App Store, loại bỏ hoàn toàn rủi ro bị từ chối.";
        dt.raycastTarget = false;

        CreateControlLabel(root, "Lang_FooterNotice", new Vector2(0f, -525f), 24f,
            "Ghost Maze Studio © 2026 • Trò Chơi Đã Được Đăng Ký Bản Quyền", 13, FontStyle.Italic, new Color(0.55f, 0.65f, 0.78f), font);

        RefreshLanguageTabVisuals();
    }

    private void RefreshLanguageTabVisuals()
    {
        GameLocalizationManager.Language cur = GameLocalizationManager.CurrentLanguage;
        string activeStr = GameLocalizationManager.T("STATUS_ACTIVE", "✔ ĐANG DÙNG");
        string chooseStr = GameLocalizationManager.T("STATUS_CHOOSE", "CHỌN");

        for (int i = 0; i < 4; i++)
        {
            if (langOptionBgs[i] == null) continue;
            bool isSelected = (int)cur == i;
            if (isSelected)
            {
                langOptionBgs[i].color = new Color(0.10f, 0.35f, 0.28f, 0.96f);
                if (langOptionOutlines[i] != null)
                {
                    langOptionOutlines[i].effectColor = new Color(0.28f, 0.95f, 0.75f, 0.95f);
                }
                if (langOptionStatusTexts[i] != null)
                {
                    langOptionStatusTexts[i].text = $"<color=#2EE87A>{activeStr}</color>";
                }
            }
            else
            {
                langOptionBgs[i].color = new Color(0.08f, 0.12f, 0.20f, 0.92f);
                if (langOptionOutlines[i] != null)
                {
                    langOptionOutlines[i].effectColor = new Color(0.22f, 0.32f, 0.50f, 0.70f);
                }
                if (langOptionStatusTexts[i] != null)
                {
                    langOptionStatusTexts[i].text = $"<color=#7F8C8D>{chooseStr}</color>";
                }
            }
        }
    }

    private void HandleLanguageChanged(GameLocalizationManager.Language lang)
    {
        RefreshLanguageTabVisuals();

        if (tabGraphicsBtnText != null) tabGraphicsBtnText.text = GameLocalizationManager.T("SETTINGS_TAB_GRAPHICS", "🎨 Hình Ảnh");
        if (tabAudioBtnText != null) tabAudioBtnText.text = GameLocalizationManager.T("SETTINGS_TAB_AUDIO", "🔊 Âm Thanh");
        if (tabControlsBtnText != null) tabControlsBtnText.text = GameLocalizationManager.T("SETTINGS_TAB_CONTROLS", "🎮 Điều Khiển");
        if (tabLanguageBtnText != null) tabLanguageBtnText.text = GameLocalizationManager.T("SETTINGS_TAB_LANGUAGE", "🌐 Ngôn Ngữ");

        if (dashTitleText != null) dashTitleText.text = GameLocalizationManager.T("SETTINGS_DASHBOARD_TITLE");
        if (dashSubtitleText != null) dashSubtitleText.text = GameLocalizationManager.T("SETTINGS_DASHBOARD_SUB");
        if (gHeader1Text != null) gHeader1Text.text = GameLocalizationManager.T("SETTINGS_GRAPHICS_HEADER");
        if (gBoostBtnText != null) gBoostBtnText.text = GameLocalizationManager.T("SETTINGS_GRAPHICS_BOOST_BTN");
        if (btnUltraText != null) btnUltraText.text = GameLocalizationManager.T("SETTINGS_GRAPHICS_ULTRA");
        if (btnMedText != null) btnMedText.text = GameLocalizationManager.T("SETTINGS_GRAPHICS_MED");
        if (btnLowText != null) btnLowText.text = GameLocalizationManager.T("SETTINGS_GRAPHICS_LOW");

        if (gHeader2Text != null) gHeader2Text.text = GameLocalizationManager.T("SETTINGS_FPS_HEADER");
        if (fpsUnlimitedBtnText != null) fpsUnlimitedBtnText.text = GameLocalizationManager.T("SETTINGS_FPS_UNLIMITED");
        if (fps120BtnText != null) fps120BtnText.text = GameLocalizationManager.T("SETTINGS_FPS_120");
        if (fps60BtnText != null) fps60BtnText.text = GameLocalizationManager.T("SETTINGS_FPS_60");

        if (gHeader3Text != null) gHeader3Text.text = GameLocalizationManager.T("SETTINGS_VSYNC_HEADER");
        if (aHeaderText != null) aHeaderText.text = GameLocalizationManager.T("SETTINGS_AUDIO_HEADER");
        if (cHeaderText != null) cHeaderText.text = GameLocalizationManager.T("SETTINGS_CONTROLS_HEADER");
        if (cKeybindingsHeaderText != null) cKeybindingsHeaderText.text = GameLocalizationManager.T("SETTINGS_CONTROLS_KEYBINDINGS_HEADER");
        if (cKeybindingsBodyText != null) cKeybindingsBodyText.text = GameLocalizationManager.T("SETTINGS_CONTROLS_KEYBINDINGS_BODY");
        if (closeBottomBtnText != null) closeBottomBtnText.text = GameLocalizationManager.T("SETTINGS_BTN_CLOSE");
        if (langHeaderText != null) langHeaderText.text = GameLocalizationManager.T("SETTINGS_LANG_HEADER");
        if (langSubtitleText != null) langSubtitleText.text = GameLocalizationManager.T("SETTINGS_LANG_HINT");

        if (inMatchNoticeBanner != null && inMatchNoticeBanner.gameObject.activeSelf)
        {
            inMatchNoticeBanner.text = GameLocalizationManager.T("SETTINGS_INMATCH_NOTICE");
        }

        UpdateAudioLabels();
        UpdateControlLabels();
        UpdateStatusLabels();
    }

    private void BindDashboardTexts()
    {
        if (dashboardPanel == null) return;
        Transform d = dashboardPanel.transform;

        dashTitleText = FindText(d, "DashTitle");
        dashSubtitleText = FindText(d, "DashSubtitle");
        gHeader1Text = FindText(d, "TabContentContainer/TabContent_Graphics/G_Header1");
        gBoostBtnText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_BoostGraphics/Text");
        btnUltraText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_G_Ultra/Text");
        btnMedText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_G_Med/Text");
        btnLowText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_G_Low/Text");
        gHeader2Text = FindText(d, "TabContentContainer/TabContent_Graphics/G_Header2");
        fpsUnlimitedBtnText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_F_Max/Text");
        fps120BtnText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_F_120/Text");
        fps60BtnText = FindText(d, "TabContentContainer/TabContent_Graphics/Btn_F_60/Text");
        gHeader3Text = FindText(d, "TabContentContainer/TabContent_Graphics/G_Header3");
        aHeaderText = FindText(d, "TabContentContainer/TabContent_Audio/A_Header");
        cHeaderText = FindText(d, "TabContentContainer/TabContent_Controls/C_Header");
        cKeybindingsHeaderText = FindText(d, "TabContentContainer/TabContent_Controls/C_KeysHeader");
        cKeybindingsBodyText = FindText(d, "TabContentContainer/TabContent_Controls/C_KeysBody");
        closeBottomBtnText = FindText(d, "Btn_CloseBottom/Text");
        langHeaderText = FindText(d, "TabContentContainer/TabContent_Language/Lang_Header");
        langSubtitleText = FindText(d, "TabContentContainer/TabContent_Language/Lang_Subtitle");
    }

    private Text FindText(Transform parent, string path)
    {
        Transform t = parent.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    public void ApplyCrispnessToSettingsUI()
    {
        var canvas = GetComponent<Canvas>();
        var scaler = GetComponent<CanvasScaler>();
        UIFontHelper.ConfigureCrispCanvas(canvas, scaler);

        if (dashboardPanel != null)
        {
            RectTransform rt = dashboardPanel.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta.x < 620f)
            {
                rt.sizeDelta = new Vector2(620f, 0f);
            }
        }

        UIFontHelper.ApplyCrispnessRecursively(gameObject, 15);
    }

    // =========================================================================
    // XÂY DỰNG GIAO DIỆN RUNTIME NẾU THIẾU (RUNTIME DASHBOARD BUILDER)
    // =========================================================================

    public void BuildRuntimeDashboardUI()
    {
        if (dashboardPanel != null) return;

        Font font = UIFontHelper.GetCrispFont(36);

        // 1. SideDashboardPanel (Glassmorphism dark background, width 620px)
        GameObject dGo = new GameObject("SideDashboardPanel");
        dGo.transform.SetParent(transform, false);
        RectTransform rtDash = dGo.AddComponent<RectTransform>();
        rtDash.anchorMin = new Vector2(1f, 0f);
        rtDash.anchorMax = new Vector2(1f, 1f);
        rtDash.pivot = new Vector2(1f, 0.5f);
        rtDash.anchoredPosition = Vector2.zero;
        rtDash.sizeDelta = new Vector2(620f, 0f);

        Image bg = dGo.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);
        bg.raycastTarget = true;

        Outline ol = dGo.AddComponent<Outline>();
        ol.effectColor = new Color(0.20f, 0.55f, 0.95f, 0.85f);
        ol.effectDistance = new Vector2(-2.5f, 0f);

        dashboardPanel = dGo;

        // Header: Title & Subtitle
        CreateRuntimeText("DashTitle", dGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(-40f, 36f), "⚙️ BẢNG CÀI ĐẶT (DASHBOARD)", 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.78f, 0.25f), font);

        CreateRuntimeText("DashSubtitle", dGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -62f), new Vector2(-40f, 24f), "Ghost Maze • Tùy chỉnh hệ thống (F1 / Tab / Esc)", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.88f), font);

        // Close Button '✕'
        GameObject closeBtnGo = CreateRuntimeButton("CloseDashboardButton", dGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -22f), new Vector2(42f, 42f), "✕", 20, FontStyle.Bold, new Color(0.65f, 0.15f, 0.15f, 0.95f), Color.white, font, CloseDashboard);
        closeDashboardButton = closeBtnGo.GetComponent<Button>();

        CreateControlDivider(dGo.transform, "DividerHeader", -85f, 560f);

        // Tab Navigation Group
        GameObject tabGroup = new GameObject("TabNavigationGroup");
        tabGroup.transform.SetParent(dGo.transform, false);
        RectTransform rtTabs = tabGroup.AddComponent<RectTransform>();
        rtTabs.anchorMin = new Vector2(0.5f, 1f);
        rtTabs.anchorMax = new Vector2(0.5f, 1f);
        rtTabs.pivot = new Vector2(0.5f, 1f);
        rtTabs.anchoredPosition = new Vector2(0f, -98f);
        rtTabs.sizeDelta = new Vector2(560f, 48f);

        float tabW = 135f;
        GameObject btnG = CreateRuntimeButton("Tab_Graphics", tabGroup.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 46f), "🎨 Hình Ảnh", 15, FontStyle.Bold, new Color(0.18f, 0.45f, 0.75f), Color.white, font, () => SwitchTab(0));
        tabGraphicsButton = btnG.GetComponent<Button>();
        tabGraphicsBtnText = btnG.GetComponentInChildren<Text>();

        GameObject btnA = CreateRuntimeButton("Tab_Audio", tabGroup.transform, new Vector2(0.33f, 0.5f), new Vector2(0.33f, 0.5f), new Vector2(0.33f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 46f), "🔊 Âm Thanh", 15, FontStyle.Bold, new Color(0.12f, 0.16f, 0.24f), Color.white, font, () => SwitchTab(1));
        tabAudioButton = btnA.GetComponent<Button>();
        tabAudioBtnText = btnA.GetComponentInChildren<Text>();

        GameObject btnC = CreateRuntimeButton("Tab_Controls", tabGroup.transform, new Vector2(0.66f, 0.5f), new Vector2(0.66f, 0.5f), new Vector2(0.66f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 46f), "🎮 Điều Khiển", 15, FontStyle.Bold, new Color(0.12f, 0.16f, 0.24f), Color.white, font, () => SwitchTab(2));
        tabControlsButton = btnC.GetComponent<Button>();
        tabControlsBtnText = btnC.GetComponentInChildren<Text>();

        GameObject btnL = CreateRuntimeButton("Tab_Language", tabGroup.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(tabW, 46f), "🌐 Ngôn Ngữ", 15, FontStyle.Bold, new Color(0.12f, 0.16f, 0.24f), Color.white, font, () => SwitchTab(3));
        tabLanguageButton = btnL.GetComponent<Button>();
        tabLanguageBtnText = btnL.GetComponentInChildren<Text>();

        // Tab Content Container
        GameObject contentContainer = new GameObject("TabContentContainer");
        contentContainer.transform.SetParent(dGo.transform, false);
        RectTransform rtContent = contentContainer.AddComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0.5f, 1f);
        rtContent.anchorMax = new Vector2(0.5f, 1f);
        rtContent.pivot = new Vector2(0.5f, 1f);
        rtContent.anchoredPosition = new Vector2(0f, -158f);
        rtContent.sizeDelta = new Vector2(560f, 780f);

        // Tab 1: Graphics
        GameObject tabGContent = new GameObject("TabContent_Graphics");
        tabGContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtG = tabGContent.AddComponent<RectTransform>();
        rtG.anchorMin = Vector2.zero;
        rtG.anchorMax = Vector2.one;
        rtG.sizeDelta = Vector2.zero;
        tabGraphicsContent = tabGContent;

        CreateRuntimeText("G_Header1", tabGContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f), "🎨 1. CHẤT LƯỢNG ĐỒ HỌA (GRAPHICS)", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.78f, 1.0f), font);

        GameObject gStatusGo = CreateRuntimeText("G_Status", tabGContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -38f), new Vector2(0f, 24f), "Chất Lượng: Siêu Đẹp (Ultra) | Độ Nét: 130%", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.85f, 0.90f, 1.0f), font);
        graphicsStatusText = gStatusGo.GetComponent<Text>();

        CreateRuntimeButton("Btn_BoostGraphics", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -78f), new Vector2(560f, 46f), "🌟 TĂNG ĐỒ HỌA CỰC ĐẠI (SIÊU NÉT 130%)", 15, FontStyle.Bold, new Color(0.14f, 0.44f, 0.74f), Color.white, font, ApplyGraphicsUltra);

        float w3 = 182f;
        CreateRuntimeButton("Btn_G_Ultra", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-188f, -132f), new Vector2(w3, 40f), "🌟 Siêu Đẹp (130%)", 14, FontStyle.Bold, new Color(0.16f, 0.30f, 0.48f), Color.white, font, ApplyGraphicsUltra);
        CreateRuntimeButton("Btn_G_Med", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -132f), new Vector2(w3, 40f), "⚖️ Cân Bằng (100%)", 14, FontStyle.Bold, new Color(0.18f, 0.24f, 0.35f), Color.white, font, ApplyGraphicsMedium);
        CreateRuntimeButton("Btn_G_Low", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(188f, -132f), new Vector2(w3, 40f), "⚡ Tiết Kiệm (85%)", 14, FontStyle.Bold, new Color(0.18f, 0.24f, 0.35f), Color.white, font, ApplyGraphicsLow);

        CreateControlDivider(tabGContent.transform, "DividerG", -165f, 560f);

        CreateRuntimeText("G_Header2", tabGContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -188f), new Vector2(0f, 26f), "🚀 2. TỐC ĐỘ KHUNG HÌNH (FPS BOOST)", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.92f, 0.55f), font);

        GameObject fpsStatusGo = CreateRuntimeText("FPS_Status", tabGContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -216f), new Vector2(0f, 24f), "Mục Tiêu FPS: Không Giới Hạn (Max FPS)", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.85f, 1.0f, 0.90f), font);
        fpsStatusText = fpsStatusGo.GetComponent<Text>();

        CreateRuntimeButton("Btn_BoostFPS", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -256f), new Vector2(560f, 46f), "🔥 TĂNG TỐI ĐA FPS (MỞ KHÓA KHUNG HÌNH)", 15, FontStyle.Bold, new Color(0.12f, 0.56f, 0.32f), Color.white, font, ApplyMaxFPS);

        float w4 = 136f;
        CreateRuntimeButton("Btn_F_Max", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-208f, -310f), new Vector2(w4, 40f), "🔥 Max FPS", 13, FontStyle.Bold, new Color(0.14f, 0.40f, 0.26f), Color.white, font, ApplyMaxFPS);
        CreateRuntimeButton("Btn_F_120", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-70f, -310f), new Vector2(w4, 40f), "⚡ 120 FPS", 13, FontStyle.Bold, new Color(0.18f, 0.28f, 0.35f), Color.white, font, Apply120FPS);
        CreateRuntimeButton("Btn_F_60", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(70f, -310f), new Vector2(w4, 40f), "🔒 60 FPS", 13, FontStyle.Bold, new Color(0.18f, 0.28f, 0.35f), Color.white, font, Apply60FPS);
        CreateRuntimeButton("Btn_F_VSync", tabGContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(208f, -310f), new Vector2(w4, 40f), "🖥️ V-Sync", 13, FontStyle.Bold, new Color(0.20f, 0.26f, 0.35f), Color.white, font, ToggleVSync);

        // Tab 2: Audio
        GameObject tabAContent = new GameObject("TabContent_Audio");
        tabAContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtA = tabAContent.AddComponent<RectTransform>();
        rtA.anchorMin = Vector2.zero;
        rtA.anchorMax = Vector2.one;
        rtA.sizeDelta = Vector2.zero;
        tabAContent.SetActive(false);
        tabAudioContent = tabAContent;

        CreateRuntimeText("A_Header", tabAContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f), "🔊 ĐIỀU CHỈNH ÂM LƯỢNG HỆ THỐNG", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.78f, 0.35f), font);

        GameObject lblMaster = CreateRuntimeText("Lbl_Master", tabAContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -45f), new Vector2(0f, 24f), "Âm Lượng Tổng: 100%", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, font);
        masterVolumeLabel = lblMaster.GetComponent<Text>();
        GameObject sldMaster = CreateRuntimeSlider("Slider_Master", tabAContent.transform, new Vector2(0f, -80f), new Vector2(560f, 26f), 0f, 1f, masterVolume);
        masterVolumeSlider = sldMaster.GetComponent<Slider>();
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        GameObject lblMusic = CreateRuntimeText("Lbl_Music", tabAContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -120f), new Vector2(0f, 24f), "Nhạc Nền Ma Mị: 80%", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, font);
        musicVolumeLabel = lblMusic.GetComponent<Text>();
        GameObject sldMusic = CreateRuntimeSlider("Slider_Music", tabAContent.transform, new Vector2(0f, -155f), new Vector2(560f, 26f), 0f, 1f, musicVolume);
        musicVolumeSlider = sldMusic.GetComponent<Slider>();
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        GameObject lblSFX = CreateRuntimeText("Lbl_SFX", tabAContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -195f), new Vector2(0f, 24f), "Hiệu Ứng & Tiếng Bước Chân: 90%", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, font);
        sfxVolumeLabel = lblSFX.GetComponent<Text>();
        GameObject sldSFX = CreateRuntimeSlider("Slider_SFX", tabAContent.transform, new Vector2(0f, -230f), new Vector2(560f, 26f), 0f, 1f, sfxVolume);
        sfxVolumeSlider = sldSFX.GetComponent<Slider>();
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        CreateControlDivider(tabAContent.transform, "DividerA", -275f, 560f);

        GameObject btnMute = CreateRuntimeButton("Btn_Mute", tabAContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -310f), new Vector2(560f, 46f), "🔊 ĐANG BẬT TIẾNG (BẤM ĐỂ TẮT)", 15, FontStyle.Bold, new Color(0.25f, 0.35f, 0.50f), Color.white, font, ToggleMute);
        muteButton = btnMute.GetComponent<Button>();
        muteButtonLabel = btnMute.transform.Find("Text").GetComponent<Text>();

        // Tab 3: Controls
        GameObject tabCContent = new GameObject("TabContent_Controls");
        tabCContent.transform.SetParent(contentContainer.transform, false);
        RectTransform rtC = tabCContent.AddComponent<RectTransform>();
        rtC.anchorMin = Vector2.zero;
        rtC.anchorMax = Vector2.one;
        rtC.sizeDelta = Vector2.zero;
        tabCContent.SetActive(false);
        tabControlsContent = tabCContent;

        CreateRuntimeText("C_Header", tabCContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 26f), "🎮 ĐỘ NHẠY & THAO TÁC CHUỘT", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.90f, 0.65f), font);

        GameObject lblSens = CreateRuntimeText("Lbl_Sens", tabCContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -45f), new Vector2(0f, 24f), "Độ Nhạy Chuột: 1.0x", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, font);
        mouseSensitivityLabel = lblSens.GetComponent<Text>();
        GameObject sldSens = CreateRuntimeSlider("Slider_Sens", tabCContent.transform, new Vector2(0f, -80f), new Vector2(560f, 26f), 0.2f, 3.5f, mouseSensitivity);
        mouseSensitivitySlider = sldSens.GetComponent<Slider>();
        mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        GameObject btnInvertY = CreateRuntimeButton("Btn_InvertY", tabCContent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -125f), new Vector2(560f, 44f), "🔄 Đảo Trục Y: ĐANG TẮT", 14, FontStyle.Bold, new Color(0.18f, 0.26f, 0.38f), Color.white, font, ToggleInvertY);
        invertYButton = btnInvertY.GetComponent<Button>();
        invertYButtonLabel = btnInvertY.transform.Find("Text").GetComponent<Text>();

        CreateControlDivider(tabCContent.transform, "DividerC", -160f, 560f);

        CreateRuntimeText("C_KeysHeader", tabCContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -180f), new Vector2(0f, 26f), "⌨️ HƯỚNG DẪN BÀN PHÍM (KEYBINDINGS)", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.85f, 0.35f), font);

        string keysInfo = "• [W] [A] [S] [D] / Mũi tên : Di chuyển nhân vật\n" +
                          "• [Left Shift] : Chạy nước rút (Sprint)\n" +
                          "• [Space] : Nhảy cao tiếp đất\n" +
                          "• [Di Chuột] : Xoay góc nhìn thứ ba 360°\n" +
                          "• [Con Lăn Chuột] : Thu phóng Camera gần/xa\n" +
                          "• [F1] / [Tab] / [Esc] : Bật / Tắt nhanh Dashboard";
        CreateRuntimeText("C_KeysBody", tabCContent.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -220f), new Vector2(0f, 150f), keysInfo, 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.90f, 0.95f), font);

        InjectCustomHUDSettingsSection();
        InjectLanguageSettingsTab();

        CreateRuntimeButton("Btn_CloseBottom", dGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 35f), new Vector2(560f, 50f), "✕ ĐÓNG CÀI ĐẶT (TIẾP TỤC CHƠI)", 16, FontStyle.Bold, new Color(0.68f, 0.18f, 0.18f), Color.white, font, CloseDashboard);

        dGo.SetActive(false);
        SwitchTab(0);
    }

    private GameObject CreateRuntimeText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Text txt = go.AddComponent<Text>();
        txt.font = font;
        txt.text = text;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.alignment = alignment;
        txt.color = color;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return go;
    }

    private GameObject CreateRuntimeButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, string buttonText, int fontSize, FontStyle fontStyle, Color bgColor, Color textColor, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = btnGo.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        Outline ol = btnGo.AddComponent<Outline>();
        ol.effectColor = new Color(1f, 1f, 1f, 0.25f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        CreateRuntimeText("Text", btnGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, buttonText, fontSize, fontStyle, TextAnchor.MiddleCenter, textColor, font);
        return btnGo;
    }

    private GameObject CreateRuntimeSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, float min, float max, float defaultVal)
    {
        GameObject sliderGo = new GameObject(name);
        sliderGo.transform.SetParent(parent, false);
        RectTransform rt = sliderGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultVal;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderGo.transform, false);
        RectTransform rtBg = bg.AddComponent<RectTransform>();
        rtBg.anchorMin = Vector2.zero;
        rtBg.anchorMax = Vector2.one;
        rtBg.sizeDelta = Vector2.zero;
        Image imgBg = bg.AddComponent<Image>();
        imgBg.color = new Color(0.12f, 0.16f, 0.24f, 0.90f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform rtFa = fillArea.AddComponent<RectTransform>();
        rtFa.anchorMin = Vector2.zero;
        rtFa.anchorMax = Vector2.one;
        rtFa.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform rtF = fill.AddComponent<RectTransform>();
        rtF.anchorMin = Vector2.zero;
        rtF.anchorMax = Vector2.one;
        rtF.sizeDelta = Vector2.zero;
        Image imgFill = fill.AddComponent<Image>();
        imgFill.color = new Color(0.18f, 0.55f, 0.92f);

        slider.targetGraphic = imgBg;
        slider.fillRect = rtF;
        return sliderGo;
    }

    private void CreateControlDivider(Transform parent, string name, float posY, float width)
    {
        GameObject divGo = new GameObject(name);
        divGo.transform.SetParent(parent, false);
        RectTransform rt = divGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, posY);
        rt.sizeDelta = new Vector2(width, 2f);

        Image img = divGo.AddComponent<Image>();
        img.color = new Color(0.20f, 0.35f, 0.55f, 0.50f);
        img.raycastTarget = false;
    }
}
