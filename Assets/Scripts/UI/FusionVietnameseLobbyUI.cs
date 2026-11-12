using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Fusion;

/// <summary>
/// Hệ thống Sảnh Chờ & Phòng Chờ 4 Người (Thiết kế Gothic Ma Mị bằng UGUI & Vector Procedural Icons):
/// - Trước khi vào phòng và khi chưa đủ 4 người trong phòng: Game KHÔNG chạy (nhân vật, ma, đồng hồ đều tạm dừng).
/// - Màn hình 1 (Sảnh Chính): Thanh Trạng Thái Kết Nối + Thanh ID Phòng (Tham gia / Tạo phòng) + 6 Thẻ Bài Chế Độ Chơi.
/// - Màn hình 2 (Bên Trong Phòng Chờ): Khi nhấn Tạo Phòng hoặc Tham Gia, chuyển vào Phòng Chờ hiển thị rõ:
///     + ID Phòng to rõ (kèm nút Sao chép ID).
///     + Danh sách 4 vị trí người chơi trong phòng: Hiển thị rõ "BẠN (TÔI Ở ĐÂY)" và những người chơi nào đã vào phòng.
///     + Tự động bắt đầu trận đấu ngay khi đủ 4/4 người chơi vào phòng (kèm nút Test nhanh nếu muốn test 1 mình).
/// </summary>
public class FusionVietnameseLobbyUI : MonoBehaviour
{
    public static FusionVietnameseLobbyUI Instance { get; private set; }

    [Header("1. Tham Chiếu Hệ Thống")]
    public FusionBootstrap bootstrap;

    // Trạng thái màn hình: False = Sảnh 6 thẻ bài, True = Bên trong Phòng Chờ (Waiting Room)
    private bool isInsideWaitingRoom = false;
    private string currentRoomId = "";

    // Các thành phần UI gốc
    private Canvas lobbyCanvas;
    private GameObject lobbyRootObj;
    private GameObject mainCardsViewObj;
    private GameObject waitingRoomViewObj;

    // UI Sảnh Chính (Main View)
    private Text connectionStatusBadgeText;
    private Image connectionStatusDotImage;
    private InputField roomIdInputField;

    // UI Bên Trong Phòng Chờ (Waiting Room View)
    private Text waitingRoomIdText;
    private Text waitingCopyBtnText;
    private InputField waitingPlayerNameInput;
    private Text waitingPlayerCountText;
    private Text waitingStatusBannerText;
    private Image waitingProgressFill;
    private GameObject waitingForceStartBtnObj;
    private Text waitingReadyBtnText;
    private Image waitingReadyBtnBg;
    private Outline waitingReadyBtnOutline;

    // UI Ghép Trận Xoay Trực Tiếp (Direct Spinning Matchmaking Overlay)
    private GameObject matchmakingSpinnerOverlayObj;
    private Image matchmakingSpinnerArcImage;
    private Text matchmakingSpinnerTitleText;
    private Text matchmakingSpinnerTimerText;
    private Text matchmakingSpinnerCountText;
    private Button matchmakingSpinnerCancelBtn;
    private Sprite sprSpinnerArc;
    private bool isDirectMatchmakingActive = false;
    private Text topRightPlayerNameText;

    // Quản lý Đa Ngôn Ngữ & Tag 18+ (Localization & 18+ Age Rating)
    private Text headerLanguageBtnText;
    private Text lobbySettingsBtnText;
    private Text headerAgeRatingText;
    private Text headerSubtitleText;
    private Text bottomDisclaimerText;
    private readonly Text[] cardTitleTexts = new Text[6];
    private readonly Text[] cardSubtitleTexts = new Text[6];
    private readonly Text[] cardBadgeTexts = new Text[6];
    private readonly Text[] cardBtnTexts = new Text[6];
    private Text tabMultiplayerText;
    private Text tabSoloText;
    private Text tabSettingsText;
    private GameObject tabMultiplayerUnderline;
    private Text bottomConnectionStatusText;
    private Text bottomSloganText;
    private Text connectionStatusLabelText;
    private Text roomIdLabelText;
    private Text roomIdInputPlaceholderText;
    private Text joinRoomBtnText;
    private Text createRoomBtnText;
    private Text waitingHeaderTitleText;
    private Text waitingMyNameLabelText;
    private Text waitingSaveNameBtnText;
    private Text waitingLeaveBtnText;
    private Text matchmakingSpinnerCancelBtnText;

    private struct PlayerSlotUI
    {
        public GameObject rootObj;
        public Image bgImage;
        public Outline cardOutline;
        public Image avatarBg;
        public Image avatarIcon;
        public Text slotBadgeText;
        public Text playerNameText;
        public Text playerRoleStatusText;
    }

    private readonly PlayerSlotUI[] playerSlots = new PlayerSlotUI[4];

    // UI HUD nhỏ gọn góc trên phải khi đã vào trận
    private GameObject inGameRoomBannerObj;
    private Text inGameRoomBannerText;

    // Bảng màu chủ đạo
    private readonly Color colDisconnected = new Color(0.95f, 0.35f, 0.38f, 1f);
    private readonly Color colConnecting   = new Color(1.00f, 0.78f, 0.22f, 1f);
    private readonly Color colConnected    = new Color(0.18f, 0.95f, 0.52f, 1f);

    // Bộ nhớ đệm Sprite procedural để không phải tạo lại
    private Sprite sprBackdropSky;
    private Sprite sprCardGradient;
    private Sprite sprCircleBadge;
    private Sprite sprLinkIcon;
    private Sprite sprDoorIcon;
    private Sprite[] sprCardIcons = new Sprite[6];
    private Sprite sprGraveyardBottom;

    private static bool hasRegisteredLobbySceneHook = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (!hasRegisteredLobbySceneHook)
        {
            hasRegisteredLobbySceneHook = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLobbySceneLoadedCallback;
        }
        EnsureLobbyUIIfInLobbyScene();
    }

    private static void OnLobbySceneLoadedCallback(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "LobbyScene")
        {
            EnsureLobbyUIIfInLobbyScene();
        }
    }

    public static void EnsureLobbyUIIfInLobbyScene()
    {
        // CHá»ˆ KHá»žI Táº O á»ž Sáº¢NH CHá»œ (LobbyScene) â€” HOÃ€N TOÃ€N KHÃ”NG HIá»†N á»ž TRANG GAME (SampleScene) TRÃNH Bá»Š LáºªN!
        string curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (curScene != "LobbyScene") return;

        if (FindAnyObjectByType<FusionVietnameseLobbyUI>() == null)
        {
            GameObject go = new GameObject("Photon_VietnameseLobbyUI");
            go.AddComponent<FusionVietnameseLobbyUI>();
        }
    }

    private void Awake()
    {
        // Náº¿u khÃ´ng pháº£i LobbyScene (vÃ­ dá»¥ Ä‘ang á»Ÿ SampleScene) thÃ¬ tá»± há»§y ngay Ä‘á»ƒ khÃ´ng bao giá» che mÃ n hÃ¬nh trong game
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene")
        {
            Destroy(gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Ẩn bảng IMGUI tiếng Anh mặc định của Fusion
        FusionBootstrapDebugGUI.HideDefaultGUI = true;

        if (bootstrap == null)
        {
            bootstrap = GetComponent<FusionBootstrap>();
            if (bootstrap == null) bootstrap = FindAnyObjectByType<FusionBootstrap>();
        }

        GenerateProceduralSprites();
        BuildCompleteLobbyUI();

        // Đăng ký nhận thông báo thay đổi ngôn ngữ
        GameLocalizationManager.OnLanguageChanged += HandleLobbyLanguageChanged;
    }

    /// <summary>
    /// Kiểm tra giao diện Sảnh Chờ có đang hiển thị hay không (dùng để phân biệt chế độ cài đặt Trong Trận vs Ở Sảnh).
    /// </summary>
    public bool IsLobbyShowing()
    {
        if (lobbyCanvas == null) return false;
        if (!lobbyCanvas.gameObject.activeInHierarchy) return false;
        if (lobbyRootObj != null && !lobbyRootObj.activeInHierarchy) return false;
        return true;
    }

    private void OnDestroy()
    {
        GameLocalizationManager.OnLanguageChanged -= HandleLobbyLanguageChanged;
        if (Instance == this)
        {
            Instance = null;
            FusionBootstrapDebugGUI.HideDefaultGUI = false;
        }
    }

    private void OnEnable()
    {
        PhotonMultiplayerChatVoice.OnRoomJoinFailedAction += HandleRoomJoinFailedFromPhoton;
    }

    private void OnDisable()
    {
        PhotonMultiplayerChatVoice.OnRoomJoinFailedAction -= HandleRoomJoinFailedFromPhoton;
    }

    private void HandleRoomJoinFailedFromPhoton(short returnCode, string message)
    {
        if (isDirectMatchmakingActive)
        {
            CancelDirectMatchmaking();
        }
        if (isInsideWaitingRoom)
        {
            SwitchToMainCardsView();
        }
    }

    private void Start()
    {
        if (bootstrap != null && !string.IsNullOrEmpty(bootstrap.DefaultRoomName) && roomIdInputField != null)
        {
            roomIdInputField.text = bootstrap.DefaultRoomName;
        }
    }

    private void Update()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene")
        {
            if (lobbyCanvas != null) Destroy(lobbyCanvas.gameObject);
            Destroy(gameObject);
            return;
        }

        UpdateLobbyAndWaitingState();
        HandleInGameShortcuts();
    }

    // =========================================================================
    // 1. TẠO CÁC VECTOR SPRITE & HỌA TIẾT GOTHIC BẰNG CODE (KHÔNG DÙNG ẢNH NỀN BÊ VỀ)
    // =========================================================================
    private void GenerateProceduralSprites()
    {
        sprBackdropSky = CreateGothicNightSkySprite(1024, 576);
        sprCardGradient = CreateVerticalCardGradientSprite(128, 512);
        sprCircleBadge = CreateGlowingCircleSprite(128);
        sprLinkIcon = CreateChainLinkIconSprite(128);
        sprDoorIcon = CreatePortalDoorIconSprite(128);
        sprGraveyardBottom = CreateGraveyardSilhouetteSprite(512, 96);

        for (int i = 0; i < 6; i++)
        {
            sprCardIcons[i] = CreateModeIconSprite(i, 256);
        }
        sprSpinnerArc = CreateSpinnerArcSprite(256);
    }

    private Sprite CreateSpinnerArcSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.46f;
        float innerR = size * 0.34f;
        float thickness = (outerR - innerR) * 0.5f;
        float midR = (outerR + innerR) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(p, center);
                if (dist >= innerR - 1.5f && dist <= outerR + 1.5f)
                {
                    float angle = (Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg + 360f) % 360f;
                    if (angle < 285f)
                    {
                        float edgeDist = Mathf.Abs(dist - midR);
                        float radialAlpha = Mathf.Clamp01(thickness - edgeDist + 0.75f);
                        float angularAlpha = Mathf.Clamp01(angle / 285f);
                        float alpha = radialAlpha * angularAlpha;

                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateGothicNightSkySprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Mặt trăng ở góc trên bên phải
        Vector2 moonCenter = new Vector2(w * 0.73f, h * 0.77f);
        float moonRadius = h * 0.17f;

        // Vị trí đèn lồng đỏ cổ kính góc bên trái
        Vector2 lanternCenter = new Vector2(w * 0.08f, h * 0.68f);
        float lanternRadius = h * 0.20f;

        Color topSky = new Color(0.02f, 0.04f, 0.10f, 0.98f);
        Color midSky = new Color(0.06f, 0.08f, 0.18f, 0.96f);
        Color botMist = new Color(0.03f, 0.05f, 0.12f, 0.98f);

        for (int y = 0; y < h; y++)
        {
            float ty = y / (float)(h - 1);
            Color baseRow = ty > 0.42f
                ? Color.Lerp(midSky, topSky, (ty - 0.42f) / 0.58f)
                : Color.Lerp(botMist, midSky, ty / 0.42f);

            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);
                Color c = baseRow;

                // 1. Hào quang mặt trăng với SmoothStep
                float distMoon = Vector2.Distance(new Vector2(x, y), moonCenter);
                if (distMoon < moonRadius)
                {
                    float edge = Mathf.SmoothStep(0f, 1f, (moonRadius - distMoon) / 2.5f);
                    float crater = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.10f;
                    Color moonCol = new Color(0.92f - crater, 0.94f - crater, 1.0f, 0.98f);
                    c = Color.Lerp(c, moonCol, edge * 0.88f);
                }
                else if (distMoon < moonRadius * 3.5f)
                {
                    float glow = Mathf.Pow(1f - (distMoon - moonRadius) / (moonRadius * 2.5f), 2.2f);
                    c += new Color(0.18f, 0.32f, 0.68f, 0f) * (glow * 0.52f);
                }

                // 2. Ánh đèn lồng ma mị ấm áp bên trái
                float distLantern = Vector2.Distance(new Vector2(x, y), lanternCenter);
                if (distLantern < lanternRadius)
                {
                    float lglow = Mathf.Pow(1f - distLantern / lanternRadius, 2.0f);
                    c += new Color(0.95f, 0.22f, 0.08f, 0f) * (lglow * 0.55f);
                }

                // 3. Bóng đen mái chùa cổ kính cong vút ở phía bên phải
                if (tx > 0.65f)
                {
                    float relX = (tx - 0.65f) / 0.35f;
                    // Đường cong mái chùa uốn vút
                    float roofY1 = 0.52f + Mathf.Sin(relX * Mathf.PI * 3.2f) * 0.06f + relX * 0.22f;
                    float roofY2 = 0.34f + Mathf.Sin(relX * Mathf.PI * 2.8f) * 0.05f + relX * 0.16f;
                    if (ty < roofY1 || ty < roofY2)
                    {
                        Color pagodaDark = new Color(0.015f, 0.022f, 0.05f, 1f);
                        c = Color.Lerp(c, pagodaDark, 0.82f);
                    }
                }

                // 4. Sương mù và nền sân gạch đá cổ ở nửa dưới
                if (ty < 0.30f)
                {
                    float groundFactor = (0.30f - ty) / 0.30f;
                    Color groundCol = new Color(0.025f, 0.038f, 0.08f, 1f);
                    float stoneNoise = Mathf.PerlinNoise(tx * 25f, ty * 18f) * 0.05f;
                    groundCol += new Color(stoneNoise * 0.4f, stoneNoise * 0.7f, stoneNoise * 1.1f, 0f);
                    c = Color.Lerp(c, groundCol, groundFactor * 0.92f);
                }

                // 5. Vignette 4 góc tối sâu
                float vx = (tx - 0.5f) * 2f;
                float vy = (ty - 0.5f) * 2f;
                float vig = Mathf.Clamp01((vx * vx + vy * vy) * 0.30f);
                c.r *= (1f - vig * 0.40f);
                c.g *= (1f - vig * 0.40f);
                c.b *= (1f - vig * 0.30f);

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateVerticalCardGradientSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            float brightness = Mathf.Lerp(0.55f, 0.95f, Mathf.Abs(t - 0.45f) * 1.3f);
            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);
                float edgeGlow = Mathf.Pow(Mathf.Abs(tx - 0.5f) * 2f, 2.5f) * 0.28f;
                float val = Mathf.Clamp01(brightness + edgeGlow);
                tex.SetPixel(x, y, new Color(val, val, val, 0.96f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateGlowingCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.SmoothStep(0f, 1f, (maxR - d + 0.5f) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateChainLinkIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        ClearTex(tex, size, size);
        DrawRotatedCapsuleRing(tex, size, new Vector2(size * 0.40f, size * 0.40f), size * 0.14f, size * 0.08f, 45f, size * 0.055f);
        DrawRotatedCapsuleRing(tex, size, new Vector2(size * 0.60f, size * 0.60f), size * 0.14f, size * 0.08f, 45f, size * 0.055f);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreatePortalDoorIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        ClearTex(tex, size, size);
        DrawRectRing(tex, size, new Rect(size * 0.22f, size * 0.12f, size * 0.52f, size * 0.76f), size * 0.06f);
        DrawFilledRect(tex, size, new Rect(size * 0.26f, size * 0.14f, size * 0.34f, size * 0.72f), new Color(1f, 1f, 1f, 0.92f));
        DrawFilledCircle(tex, size, new Vector2(size * 0.52f, size * 0.50f), size * 0.04f, Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateGraveyardSilhouetteSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        ClearTex(tex, w, h);

        Color silCol = new Color(0.02f, 0.04f, 0.08f, 0.92f);

        for (int x = 0; x < w; x++)
        {
            float groundH = 14f + Mathf.Sin(x * 0.05f) * 5f;
            bool isFenceSpike = (x % 24 >= 10 && x % 24 <= 14);
            float spikeH = isFenceSpike ? 38f : 20f;
            bool isRail1 = true;
            bool isRail2 = true;

            bool isTomb1 = (x >= 60 && x <= 95);
            bool isTomb2 = (x >= w - 100 && x <= w - 65);

            for (int y = 0; y < h; y++)
            {
                bool fill = false;
                if (y <= groundH) fill = true;
                else if (isFenceSpike && y <= spikeH) fill = true;
                else if (isRail1 && Mathf.Abs(y - 25f) <= 2.2f) fill = true;
                else if (isRail2 && Mathf.Abs(y - 15f) <= 2.2f) fill = true;
                else if (isTomb1 && y <= 52f) fill = true;
                else if (isTomb2 && y <= 48f) fill = true;

                if ((Mathf.Abs(x - 77) <= 3 && y >= 52 && y <= 74) || (Mathf.Abs(x - 77) <= 12 && Mathf.Abs(y - 64) <= 3.5f))
                    fill = true;
                if ((Mathf.Abs(x - (w - 82)) <= 3 && y >= 48 && y <= 68) || (Mathf.Abs(x - (w - 82)) <= 11 && Mathf.Abs(y - 59) <= 3.5f))
                    fill = true;

                if (fill)
                {
                    tex.SetPixel(x, y, silCol);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateModeIconSprite(int modeIndex, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        ClearTex(tex, size, size);
        Color white = new Color(1f, 1f, 1f, 0.96f);

        switch (modeIndex)
        {
            case 0:
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.45f, size * 0.52f), size * 0.36f, white);
                DrawFilledCircle(tex, size, new Vector2(size * 0.72f, size * 0.28f), size * 0.16f, white);
                DrawPlayTriangle(tex, size, new Vector2(size * 0.73f, size * 0.28f), size * 0.08f, Color.clear);
                break;

            case 1:
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.26f, size * 0.54f), size * 0.25f, new Color(1f, 1f, 1f, 0.75f));
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.72f, size * 0.54f), size * 0.25f, new Color(1f, 1f, 1f, 0.75f));
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.48f, size * 0.50f), size * 0.34f, white);
                DrawFilledCircle(tex, size, new Vector2(size * 0.76f, size * 0.24f), size * 0.14f, white);
                DrawPlusSign(tex, size, new Vector2(size * 0.76f, size * 0.24f), size * 0.08f, size * 0.03f, Color.clear);
                break;

            case 2:
                for (int r = 0; r < 3; r++)
                {
                    float ry = size * (0.68f - r * 0.20f);
                    DrawFilledRect(tex, size, new Rect(size * 0.18f, ry - size * 0.065f, size * 0.56f, size * 0.13f), white);
                    DrawFilledCircle(tex, size, new Vector2(size * 0.26f, ry), size * 0.025f, Color.clear);
                    DrawFilledCircle(tex, size, new Vector2(size * 0.34f, ry), size * 0.025f, Color.clear);
                }
                DrawGearBadge(tex, size, new Vector2(size * 0.74f, size * 0.26f), size * 0.16f, white);
                break;

            case 3:
                DrawRoyalCrown(tex, size, white);
                break;

            case 4:
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.42f, size * 0.50f), size * 0.36f, white);
                DrawPlusSign(tex, size, new Vector2(size * 0.76f, size * 0.52f), size * 0.13f, size * 0.045f, white);
                break;

            case 5:
                DrawSyncArrows(tex, size, new Vector2(size * 0.50f, size * 0.52f), size * 0.28f, size * 0.075f, white);
                break;
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Các hàm vẽ hình học Vector có khử răng cưa (Anti-aliased Procedural Drawing)
    private static void ClearTex(Texture2D tex, int w, int h)
    {
        Color[] cols = new Color[w * h];
        tex.SetPixels(cols);
    }

    private static void DrawFilledCircle(Texture2D tex, int size, Vector2 center, float radius, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 2));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius + 2));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 2));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius + 2));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(radius - d + 0.75f);
                if (a > 0f)
                {
                    if (col.a == 0f)
                    {
                        Color cur = tex.GetPixel(x, y);
                        cur.a *= (1f - a);
                        tex.SetPixel(x, y, cur);
                    }
                    else
                    {
                        Color cur = tex.GetPixel(x, y);
                        tex.SetPixel(x, y, Color.Lerp(cur, col, a));
                    }
                }
            }
        }
    }

    private static void DrawFilledRect(Texture2D tex, int size, Rect r, Color col)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, size - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax), 0, size - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, size - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax), 0, size - 1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                tex.SetPixel(x, y, col);
            }
        }
    }

    private static void DrawRectRing(Texture2D tex, int size, Rect r, float thickness)
    {
        DrawFilledRect(tex, size, r, Color.white);
        Rect inner = new Rect(r.xMin + thickness, r.yMin + thickness, r.width - thickness * 2f, r.height - thickness * 2f);
        DrawFilledRect(tex, size, inner, Color.clear);
    }

    private static void DrawRotatedCapsuleRing(Texture2D tex, int size, Vector2 center, float halfLen, float radius, float angleDeg, float thickness)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 a = center - dir * (halfLen * 0.5f);
        Vector2 b = center + dir * (halfLen * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 pa = p - a, ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                float distSeg = (pa - ba * h).magnitude;
                float ringDist = Mathf.Abs(distSeg - radius);
                float alpha = Mathf.Clamp01((thickness * 0.5f - ringDist) + 0.75f);
                if (alpha > 0f)
                {
                    Color cur = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(cur.a, alpha)));
                }
            }
        }
    }

    private static void DrawPersonSilhouette(Texture2D tex, int size, Vector2 center, float scale, Color col)
    {
        // Đầu tròn phía trên
        Vector2 headCenter = new Vector2(center.x, center.y + scale * 0.36f);
        DrawFilledCircle(tex, size, headCenter, scale * 0.28f, col);

        // Vai & Thân nửa hình elip phía dưới
        Vector2 bodyCenter = new Vector2(center.x, center.y - scale * 0.42f);
        float rx = scale * 0.52f;
        float ry = scale * 0.42f;
        for (int y = 0; y < size; y++)
        {
            if (y < bodyCenter.y) continue;
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - bodyCenter.x) / rx;
                float dy = (y + 0.5f - bodyCenter.y) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - d) * rx * 0.5f);
                if (a > 0f)
                {
                    Color cur = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, Color.Lerp(cur, col, a * col.a));
                }
            }
        }
    }

    private static void DrawPlayTriangle(Texture2D tex, int size, Vector2 center, float r, Color col)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = Mathf.Abs(y + 0.5f - center.y);
                if (dx >= -r * 0.55f && dx <= r * 0.85f)
                {
                    float maxDy = (r * 0.85f - dx) * 0.62f;
                    if (dy <= maxDy)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }
    }

    private static void DrawPlusSign(Texture2D tex, int size, Vector2 center, float armLen, float halfThick, Color col)
    {
        DrawFilledRect(tex, size, new Rect(center.x - armLen, center.y - halfThick, armLen * 2f, halfThick * 2f), col);
        DrawFilledRect(tex, size, new Rect(center.x - halfThick, center.y - armLen, halfThick * 2f, armLen * 2f), col);
    }

    private static void DrawGearBadge(Texture2D tex, int size, Vector2 center, float outerR, Color col)
    {
        DrawFilledCircle(tex, size, center, outerR * 0.78f, col);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 0.25f;
            Vector2 toothPos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (outerR * 0.78f);
            DrawFilledCircle(tex, size, toothPos, outerR * 0.22f, col);
        }
        DrawFilledCircle(tex, size, center, outerR * 0.34f, Color.clear);
    }

    private static void DrawRoyalCrown(Texture2D tex, int size, Color col)
    {
        // Đế vương miện
        DrawFilledRect(tex, size, new Rect(size * 0.20f, size * 0.22f, size * 0.60f, size * 0.08f), col);

        // Thân vương miện 3 đỉnh nhọn lớn
        float baseBot = size * 0.33f;
        float topPeakY = size * 0.74f;
        float sidePeakY = size * 0.66f;

        for (int y = Mathf.RoundToInt(baseBot); y <= Mathf.RoundToInt(topPeakY); y++)
        {
            float ty = (y - baseBot) / (topPeakY - baseBot);
            float leftX = Mathf.Lerp(size * 0.20f, size * 0.14f, ty);
            float rightX = Mathf.Lerp(size * 0.80f, size * 0.86f, ty);

            for (int x = Mathf.RoundToInt(leftX); x <= Mathf.RoundToInt(rightX); x++)
            {
                float nx = (x - leftX) / Mathf.Max(1f, rightX - leftX); // 0 -> 1
                // Ba đỉnh nhọn tại nx = 0, 0.5, 1.0; hai hõm tại nx = 0.25, 0.75
                float wave = Mathf.Abs(Mathf.Cos(nx * Mathf.PI * 2f));
                float maxH = Mathf.Lerp(baseBot + (topPeakY - baseBot) * 0.38f, (nx > 0.3f && nx < 0.7f) ? topPeakY : sidePeakY, wave);
                if (y <= maxH)
                {
                    tex.SetPixel(x, y, col);
                }
            }
        }

        // 3 viên ngọc tròn trên đỉnh 3 chóp vương miện
        DrawFilledCircle(tex, size, new Vector2(size * 0.14f, sidePeakY + size * 0.04f), size * 0.045f, col);
        DrawFilledCircle(tex, size, new Vector2(size * 0.50f, topPeakY + size * 0.045f), size * 0.055f, col);
        DrawFilledCircle(tex, size, new Vector2(size * 0.86f, sidePeakY + size * 0.04f), size * 0.045f, col);
    }

    private static void DrawSyncArrows(Texture2D tex, int size, Vector2 center, float radius, float thickness, Color col)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - center;
                float r = d.magnitude;
                float ringDist = Mathf.Abs(r - radius);
                if (ringDist <= thickness * 0.5f)
                {
                    float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    // Tạo 2 khe hở đối xứng cho 2 mũi tên vòng cung
                    if (!((ang > -25f && ang < 15f) || (ang > 155f || ang < -165f)))
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }
        // Hai đầu mũi tên
        DrawFilledCircle(tex, size, center + new Vector2(radius * 0.95f, radius * 0.28f), thickness * 0.95f, col);
        DrawFilledCircle(tex, size, center + new Vector2(-radius * 0.95f, -radius * 0.28f), thickness * 0.95f, col);
    }

    // =========================================================================
    // 2. KHỞI TẠO TOÀN BỘ GIAO DIỆN UGUI (SẢNH CHÍNH & PHÒNG CHỜ 4 NGƯỜI)
    // =========================================================================
    private void BuildCompleteLobbyUI()
    {
        GameObject cGo = new GameObject("Canvas_FusionVietnameseLobby");
        cGo.transform.SetParent(transform, false);
        lobbyCanvas = cGo.AddComponent<Canvas>();
        lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lobbyCanvas.sortingOrder = 42;

        CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
        UIFontHelper.ConfigureCrispCanvas(lobbyCanvas, scaler);
        cGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystemExists();

        Font font = UIFontHelper.GetCrispFont(24);

        // Khung gốc chứa Sảnh Chờ & Phòng Chờ
        lobbyRootObj = new GameObject("LobbyRoot");
        lobbyRootObj.transform.SetParent(lobbyCanvas.transform, false);
        RectTransform rootRect = lobbyRootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // 1. Nền bầu trời đêm trăng máu & sương mù dựng bằng Procedural Sprite
        GameObject bgObj = new GameObject("ProceduralGothicBackdrop");
        bgObj.transform.SetParent(lobbyRootObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = sprBackdropSky;
        bgImg.color = Color.white;

        // 2. Logo Game: GHOST MAZE (Góc trên bên trái)
        BuildTopLeftGameLogo(lobbyRootObj.transform, font);

        // 2.1. Thanh Tab Điều Hướng (Chính giữa phía trên: Đa Người Chơi / Chơi Đơn / Cài Đặt)
        BuildTopCenterNavTabs(lobbyRootObj.transform, font);

        // 2.2. Hồ Sơ Người Chơi & Nút Cài Đặt, Nút Nguồn Thoát Game (Góc trên bên phải)
        BuildTopRightPlayerBadge(lobbyRootObj.transform, font);

        // 3. MÀN HÌNH 1: SẢNH CHÍNH (Thanh ID Phòng + 6 Thẻ Chế Độ Chơi Gothic)
        mainCardsViewObj = new GameObject("MainCardsView");
        mainCardsViewObj.transform.SetParent(lobbyRootObj.transform, false);
        RectTransform mvRect = mainCardsViewObj.AddComponent<RectTransform>();
        mvRect.anchorMin = Vector2.zero;
        mvRect.anchorMax = Vector2.one;
        mvRect.offsetMin = Vector2.zero;
        mvRect.offsetMax = Vector2.zero;

        // Thanh ID Phòng & Nút [ ➔ Tham gia ] cam rực rỡ
        BuildTopCenterRoomBar(mainCardsViewObj.transform, font);

        // 6 Thẻ Chế Độ Chơi Gothic (Chuẩn màu, icon, subtitle, nút pill, và Vương Miện Vàng 👑 trên thẻ Làm chủ phòng)
        BuildSixGothicModeCards(mainCardsViewObj.transform, font);

        // 4. MÀN HÌNH 2: BÊN TRONG PHÒNG CHỜ (Waiting Room View - Hiển thị ID Phòng + 4 Người Chơi & Tôi Ở Đây)
        BuildWaitingRoomView(lobbyRootObj.transform, font);

        // 4.1. Overlay Vòng Xoay Tìm Trận Trực Tiếp (Không cần vào phòng chờ)
        BuildMatchmakingSpinnerOverlay(lobbyRootObj.transform, font);

        // 5. Thanh HUD ID Phòng Nhỏ Gọn Góc Trên Phải Khi Đã Bắt Đầu Trận Đấu
        BuildInGameRoomHUD(lobbyCanvas.transform, font);

        // 6. Thanh Trạng Thái Dưới Cùng (Trái: ● Trạng thái kết nối: Kết nối tốt | Phải: PLAY TOGETHER • HAVE FUN)
        BuildBottomStatusBar(lobbyRootObj.transform, font);

        // Khởi tạo text ngôn ngữ đồng bộ
        UpdateAllLobbyLocalizedTexts();

        // Tối ưu độ nét vector toàn diện cho UI sảnh
        UIFontHelper.ApplyCrispnessRecursively(lobbyRootObj, 15);

        // Khởi đầu ở Sảnh Chính
        SwitchToMainCardsView();
    }

    private void EnsureEventSystemExists()
    {
        EventSystem[] allSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        if (allSystems == null || allSystems.Length == 0)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
        else if (allSystems.Length > 1)
        {
            // Giữ lại duy nhất 1 EventSystem đang bật để tránh xung đột click UI khi quay về Sảnh
            EventSystem keep = allSystems[0];
            for (int i = 0; i < allSystems.Length; i++)
            {
                if (allSystems[i].gameObject.scene == gameObject.scene)
                {
                    keep = allSystems[i];
                    break;
                }
            }
            for (int i = 0; i < allSystems.Length; i++)
            {
                if (allSystems[i] != keep) Destroy(allSystems[i].gameObject);
            }
        }
    }

    // =========================================================================
    // 3. TOP BAR: LOGO (TRÁI), TAB ĐIỀU HƯỚNG (GIỮA), HỒ SƠ & TIỆN ÍCH (PHẢI)
    // =========================================================================
    private void BuildTopLeftGameLogo(Transform parent, Font font)
    {
        GameObject logoObj = new GameObject("TopLeftGameLogo");
        logoObj.transform.SetParent(parent, false);
        RectTransform rt = logoObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(50f, -24f);
        rt.sizeDelta = new Vector2(260f, 72f);

        GameObject titleObj = new GameObject("LogoText");
        titleObj.transform.SetParent(logoObj.transform, false);
        RectTransform trt = titleObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        Text titleTxt = titleObj.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.fontSize = 28;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.lineSpacing = 0.88f;
        titleTxt.supportRichText = true;
        titleTxt.text = "<size=24><color=#E6F0FF>GHOST</color></size>\n<size=34><color=#FF2A3A>MAZE</color></size>";

        Outline outline = titleObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.04f, 0.09f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = titleObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.85f, 0.12f, 0.18f, 0.45f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    private void BuildTopCenterNavTabs(Transform parent, Font font)
    {
        GameObject tabsObj = new GameObject("TopCenterNavTabs");
        tabsObj.transform.SetParent(parent, false);
        RectTransform rt = tabsObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(490f, 48f);

        // Tab 1: Đa Người Chơi (Active - Có vạch phát sáng gầm)
        GameObject tab1 = new GameObject("Tab_Multiplayer");
        tab1.transform.SetParent(tabsObj.transform, false);
        RectTransform rt1 = tab1.AddComponent<RectTransform>();
        rt1.anchorMin = new Vector2(0f, 0f);
        rt1.anchorMax = new Vector2(0.33f, 1f);
        rt1.offsetMin = new Vector2(4f, 2f);
        rt1.offsetMax = new Vector2(-4f, -2f);

        Image bg1 = tab1.AddComponent<Image>();
        bg1.color = new Color(0.05f, 0.18f, 0.32f, 0.92f);
        Outline ol1 = tab1.AddComponent<Outline>();
        ol1.effectColor = new Color(0.00f, 0.82f, 1.00f, 0.85f);
        ol1.effectDistance = new Vector2(1.5f, 1.5f);

        Button btn1 = tab1.AddComponent<Button>();
        btn1.onClick.AddListener(() => SwitchToMainCardsView());

        GameObject uline = new GameObject("UnderlineGlow");
        uline.transform.SetParent(tab1.transform, false);
        RectTransform urt = uline.AddComponent<RectTransform>();
        urt.anchorMin = new Vector2(0.05f, 0f);
        urt.anchorMax = new Vector2(0.95f, 0f);
        urt.pivot = new Vector2(0.5f, 0f);
        urt.anchoredPosition = new Vector2(0f, -1f);
        urt.sizeDelta = new Vector2(0f, 3.5f);
        Image uimg = uline.AddComponent<Image>();
        uimg.color = new Color(0.00f, 0.90f, 1.00f, 1.0f);
        tabMultiplayerUnderline = uline;

        GameObject t1Obj = new GameObject("Text");
        t1Obj.transform.SetParent(tab1.transform, false);
        RectTransform t1rt = t1Obj.AddComponent<RectTransform>();
        t1rt.anchorMin = Vector2.zero;
        t1rt.anchorMax = Vector2.one;
        t1rt.offsetMin = Vector2.zero;
        t1rt.offsetMax = Vector2.zero;
        tabMultiplayerText = t1Obj.AddComponent<Text>();
        tabMultiplayerText.font = font;
        tabMultiplayerText.fontSize = 15;
        tabMultiplayerText.fontStyle = FontStyle.Bold;
        tabMultiplayerText.alignment = TextAnchor.MiddleCenter;
        tabMultiplayerText.color = new Color(0.92f, 0.98f, 1.00f);
        tabMultiplayerText.text = GameLocalizationManager.T("LOBBY_TAB_MULTIPLAYER", "👥 Đa Người Chơi");

        // Tab 2: Chơi Đơn
        GameObject tab2 = new GameObject("Tab_Solo");
        tab2.transform.SetParent(tabsObj.transform, false);
        RectTransform rt2 = tab2.AddComponent<RectTransform>();
        rt2.anchorMin = new Vector2(0.34f, 0f);
        rt2.anchorMax = new Vector2(0.67f, 1f);
        rt2.offsetMin = new Vector2(4f, 2f);
        rt2.offsetMax = new Vector2(-4f, -2f);

        Image bg2 = tab2.AddComponent<Image>();
        bg2.color = new Color(0.05f, 0.08f, 0.16f, 0.80f);
        Outline ol2 = tab2.AddComponent<Outline>();
        ol2.effectColor = new Color(0.20f, 0.32f, 0.55f, 0.50f);
        ol2.effectDistance = new Vector2(1f, 1f);

        Button btn2 = tab2.AddComponent<Button>();
        btn2.onClick.AddListener(() => OnCard1_StartSinglePlayer());

        GameObject t2Obj = new GameObject("Text");
        t2Obj.transform.SetParent(tab2.transform, false);
        RectTransform t2rt = t2Obj.AddComponent<RectTransform>();
        t2rt.anchorMin = Vector2.zero;
        t2rt.anchorMax = Vector2.one;
        t2rt.offsetMin = Vector2.zero;
        t2rt.offsetMax = Vector2.zero;
        tabSoloText = t2Obj.AddComponent<Text>();
        tabSoloText.font = font;
        tabSoloText.fontSize = 15;
        tabSoloText.fontStyle = FontStyle.Bold;
        tabSoloText.alignment = TextAnchor.MiddleCenter;
        tabSoloText.color = new Color(0.72f, 0.80f, 0.92f);
        tabSoloText.text = GameLocalizationManager.T("LOBBY_TAB_SOLO", "👤 Chơi Đơn");

        // Tab 3: Cài Đặt
        GameObject tab3 = new GameObject("Tab_Settings");
        tab3.transform.SetParent(tabsObj.transform, false);
        RectTransform rt3 = tab3.AddComponent<RectTransform>();
        rt3.anchorMin = new Vector2(0.68f, 0f);
        rt3.anchorMax = new Vector2(1f, 1f);
        rt3.offsetMin = new Vector2(4f, 2f);
        rt3.offsetMax = new Vector2(-4f, -2f);

        Image bg3 = tab3.AddComponent<Image>();
        bg3.color = new Color(0.05f, 0.08f, 0.16f, 0.80f);
        Outline ol3 = tab3.AddComponent<Outline>();
        ol3.effectColor = new Color(0.20f, 0.32f, 0.55f, 0.50f);
        ol3.effectDistance = new Vector2(1f, 1f);

        Button btn3 = tab3.AddComponent<Button>();
        btn3.onClick.AddListener(() => GameSettingsManager.EnsureInstance().OpenDashboard(GameSettingsManager.SettingsMode.LobbyFull));

        GameObject t3Obj = new GameObject("Text");
        t3Obj.transform.SetParent(tab3.transform, false);
        RectTransform t3rt = t3Obj.AddComponent<RectTransform>();
        t3rt.anchorMin = Vector2.zero;
        t3rt.anchorMax = Vector2.one;
        t3rt.offsetMin = Vector2.zero;
        t3rt.offsetMax = Vector2.zero;
        tabSettingsText = t3Obj.AddComponent<Text>();
        tabSettingsText.font = font;
        tabSettingsText.fontSize = 15;
        tabSettingsText.fontStyle = FontStyle.Bold;
        tabSettingsText.alignment = TextAnchor.MiddleCenter;
        tabSettingsText.color = new Color(0.72f, 0.80f, 0.92f);
        tabSettingsText.text = GameLocalizationManager.T("LOBBY_TAB_SETTINGS", "⚙️ Cài Đặt");
    }

    private void BuildTopRightPlayerBadge(Transform parent, Font font)
    {
        GameObject rightBar = new GameObject("TopRightHeaderActions");
        rightBar.transform.SetParent(parent, false);
        RectTransform rrt = rightBar.AddComponent<RectTransform>();
        rrt.anchorMin = new Vector2(1f, 1f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(1f, 1f);
        rrt.anchoredPosition = new Vector2(-45f, -24f);
        rrt.sizeDelta = new Vector2(370f, 48f);

        // 1. Profile Capsule: 👤 Player_001 ▾
        GameObject profilePill = new GameObject("PlayerProfilePill");
        profilePill.transform.SetParent(rightBar.transform, false);
        RectTransform prt = profilePill.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0.5f);
        prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(0f, 0f);
        prt.sizeDelta = new Vector2(215f, 44f);

        Image pbg = profilePill.AddComponent<Image>();
        pbg.color = new Color(0.06f, 0.09f, 0.18f, 0.92f);
        Outline pol = profilePill.AddComponent<Outline>();
        pol.effectColor = new Color(0.25f, 0.42f, 0.75f, 0.75f);
        pol.effectDistance = new Vector2(1.5f, 1.5f);

        Button pbtn = profilePill.AddComponent<Button>();
        pbtn.onClick.AddListener(() =>
        {
            if (waitingRoomViewObj != null && !waitingRoomViewObj.activeSelf)
            {
                EnterWaitingRoomWithId("SOLO_EDIT", true);
            }
        });

        GameObject ptxtObj = new GameObject("Text");
        ptxtObj.transform.SetParent(profilePill.transform, false);
        RectTransform ptrt = ptxtObj.AddComponent<RectTransform>();
        ptrt.anchorMin = Vector2.zero;
        ptrt.anchorMax = Vector2.one;
        ptrt.offsetMin = new Vector2(12f, 0f);
        ptrt.offsetMax = new Vector2(-12f, 0f);

        topRightPlayerNameText = ptxtObj.AddComponent<Text>();
        topRightPlayerNameText.font = font;
        topRightPlayerNameText.fontSize = 15;
        topRightPlayerNameText.fontStyle = FontStyle.Bold;
        topRightPlayerNameText.alignment = TextAnchor.MiddleCenter;
        topRightPlayerNameText.color = new Color(0.92f, 0.96f, 1.0f);
        topRightPlayerNameText.text = "👤 Player_001 ▾";

        // 2. Divider |
        GameObject divObj = new GameObject("Divider");
        divObj.transform.SetParent(rightBar.transform, false);
        RectTransform drt = divObj.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0.5f);
        drt.anchorMax = new Vector2(0f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(230f, 0f);
        drt.sizeDelta = new Vector2(1.5f, 24f);
        Image dimg = divObj.AddComponent<Image>();
        dimg.color = new Color(0.35f, 0.48f, 0.75f, 0.60f);

        // 3. Settings Gear Button ⚙️
        GameObject setBtn = new GameObject("QuickSettingsIconBtn");
        setBtn.transform.SetParent(rightBar.transform, false);
        RectTransform srt = setBtn.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.5f);
        srt.anchorMax = new Vector2(0f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(262f, 0f);
        srt.sizeDelta = new Vector2(44f, 44f);

        Image sbg = setBtn.AddComponent<Image>();
        sbg.color = new Color(0.08f, 0.12f, 0.22f, 0.90f);
        Outline sol = setBtn.AddComponent<Outline>();
        sol.effectColor = new Color(0.30f, 0.50f, 0.85f, 0.70f);
        sol.effectDistance = new Vector2(1.5f, 1.5f);

        Button sButton = setBtn.AddComponent<Button>();
        sButton.onClick.AddListener(() => GameSettingsManager.EnsureInstance().OpenDashboard(GameSettingsManager.SettingsMode.LobbyFull));

        GameObject stxt = new GameObject("Text");
        stxt.transform.SetParent(setBtn.transform, false);
        RectTransform strt = stxt.AddComponent<RectTransform>();
        strt.anchorMin = Vector2.zero;
        strt.anchorMax = Vector2.one;
        strt.offsetMin = Vector2.zero;
        strt.offsetMax = Vector2.zero;
        Text stxtC = stxt.AddComponent<Text>();
        stxtC.font = font;
        stxtC.fontSize = 20;
        stxtC.alignment = TextAnchor.MiddleCenter;
        stxtC.color = new Color(1f, 0.92f, 0.40f);
        stxtC.text = "⚙️";

        // 4. Power/Quit Button ⏻
        GameObject quitBtn = new GameObject("QuickQuitIconBtn");
        quitBtn.transform.SetParent(rightBar.transform, false);
        RectTransform qrt = quitBtn.AddComponent<RectTransform>();
        qrt.anchorMin = new Vector2(0f, 0.5f);
        qrt.anchorMax = new Vector2(0f, 0.5f);
        qrt.pivot = new Vector2(0.5f, 0.5f);
        qrt.anchoredPosition = new Vector2(318f, 0f);
        qrt.sizeDelta = new Vector2(44f, 44f);

        Image qbg = quitBtn.AddComponent<Image>();
        qbg.color = new Color(0.18f, 0.06f, 0.08f, 0.90f);
        Outline qol = quitBtn.AddComponent<Outline>();
        qol.effectColor = new Color(0.85f, 0.25f, 0.30f, 0.80f);
        qol.effectDistance = new Vector2(1.5f, 1.5f);

        Button qButton = quitBtn.AddComponent<Button>();
        qButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        GameObject qtxt = new GameObject("Text");
        qtxt.transform.SetParent(quitBtn.transform, false);
        RectTransform qtrt = qtxt.AddComponent<RectTransform>();
        qtrt.anchorMin = Vector2.zero;
        qtrt.anchorMax = Vector2.one;
        qtrt.offsetMin = Vector2.zero;
        qtrt.offsetMax = Vector2.zero;
        Text qtxtC = qtxt.AddComponent<Text>();
        qtxtC.font = font;
        qtxtC.fontSize = 20;
        qtxtC.alignment = TextAnchor.MiddleCenter;
        qtxtC.color = new Color(1f, 0.45f, 0.45f);
        qtxtC.text = "⏻";
    }

    // =========================================================================
    // 4. THANH ID PHÒNG (SCI-FI CHAMFERED BOX & NÚT CAM RỰC RỠ)
    // =========================================================================
    private void BuildTopCenterRoomBar(Transform parent, Font font)
    {
        GameObject barObj = new GameObject("RoomIdBar");
        barObj.transform.SetParent(parent, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -95f);
        barRect.sizeDelta = new Vector2(1100f, 68f);

        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.04f, 0.07f, 0.14f, 0.94f);
        Outline barOut = barObj.AddComponent<Outline>();
        barOut.effectColor = new Color(0.25f, 0.45f, 0.85f, 0.85f);
        barOut.effectDistance = new Vector2(2f, 2f);

        // 1. Icon Cánh Cửa + Chữ "ID Phòng:"
        GameObject doorIconObj = new GameObject("DoorIcon");
        doorIconObj.transform.SetParent(barObj.transform, false);
        RectTransform diRect = doorIconObj.AddComponent<RectTransform>();
        diRect.anchorMin = new Vector2(0f, 0.5f);
        diRect.anchorMax = new Vector2(0f, 0.5f);
        diRect.pivot = new Vector2(0.5f, 0.5f);
        diRect.anchoredPosition = new Vector2(40f, 0f);
        diRect.sizeDelta = new Vector2(34f, 34f);
        Image diImg = doorIconObj.AddComponent<Image>();
        diImg.sprite = sprDoorIcon;
        diImg.color = new Color(0.75f, 0.88f, 1.0f, 0.95f);

        GameObject labelObj = new GameObject("RoomIdLabel");
        labelObj.transform.SetParent(barObj.transform, false);
        RectTransform lRect = labelObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = new Vector2(72f, 0f);
        lRect.sizeDelta = new Vector2(140f, 50f);

        Text lTxt = labelObj.AddComponent<Text>();
        lTxt.font = font;
        lTxt.fontSize = 20;
        lTxt.fontStyle = FontStyle.Bold;
        lTxt.alignment = TextAnchor.MiddleLeft;
        lTxt.color = new Color(0.95f, 0.97f, 1.0f);
        lTxt.text = GameLocalizationManager.T("LOBBY_ROOM_ID_LABEL", "ID Phòng:");
        roomIdLabelText = lTxt;

        // 2. Ô Nhập ID Phòng (InputField) kèm Nút Copy/Paste ❐
        GameObject inputObj = new GameObject("RoomInputField");
        inputObj.transform.SetParent(barObj.transform, false);
        RectTransform inRect = inputObj.AddComponent<RectTransform>();
        inRect.anchorMin = new Vector2(0f, 0.5f);
        inRect.anchorMax = new Vector2(0f, 0.5f);
        inRect.pivot = new Vector2(0f, 0.5f);
        inRect.anchoredPosition = new Vector2(220f, 0f);
        inRect.sizeDelta = new Vector2(580f, 48f);

        Image inBg = inputObj.AddComponent<Image>();
        inBg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);
        Outline inOut = inputObj.AddComponent<Outline>();
        inOut.effectColor = new Color(0.22f, 0.35f, 0.65f, 0.85f);
        inOut.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject inTextObj = new GameObject("Text");
        inTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform itRect = inTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = new Vector2(18f, 0f);
        itRect.offsetMax = new Vector2(-54f, 0f);

        Text inText = inTextObj.AddComponent<Text>();
        inText.font = font;
        inText.fontSize = 18;
        inText.fontStyle = FontStyle.Bold;
        inText.alignment = TextAnchor.MiddleLeft;
        inText.color = new Color(1f, 0.92f, 0.40f);
        inText.supportRichText = false;

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(18f, 0f);
        phRect.offsetMax = new Vector2(-54f, 0f);

        Text phText = phObj.AddComponent<Text>();
        phText.font = font;
        phText.fontSize = 17;
        phText.fontStyle = FontStyle.Italic;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.40f, 0.50f, 0.70f, 0.75f);
        phText.text = GameLocalizationManager.T("LOBBY_ROOM_INPUT_PH", "Nhập ID phòng...");
        roomIdInputPlaceholderText = phText;

        roomIdInputField = inputObj.AddComponent<InputField>();
        roomIdInputField.textComponent = inText;
        roomIdInputField.placeholder = phText;
        roomIdInputField.characterLimit = 25;
        roomIdInputField.onValueChanged.AddListener(OnRoomIdInputChanged);

        // Nút Sao Chép / Dán ❐ ở mép phải của InputField
        GameObject copyBtnObj = new GameObject("CopyPasteBtn");
        copyBtnObj.transform.SetParent(inputObj.transform, false);
        RectTransform cprt = copyBtnObj.AddComponent<RectTransform>();
        cprt.anchorMin = new Vector2(1f, 0.5f);
        cprt.anchorMax = new Vector2(1f, 0.5f);
        cprt.pivot = new Vector2(1f, 0.5f);
        cprt.anchoredPosition = new Vector2(-8f, 0f);
        cprt.sizeDelta = new Vector2(36f, 36f);

        Button cpBtn = copyBtnObj.AddComponent<Button>();
        GameObject cpTxtObj = new GameObject("Text");
        cpTxtObj.transform.SetParent(copyBtnObj.transform, false);
        RectTransform cptrt = cpTxtObj.AddComponent<RectTransform>();
        cptrt.anchorMin = Vector2.zero;
        cptrt.anchorMax = Vector2.one;
        cptrt.offsetMin = Vector2.zero;
        cptrt.offsetMax = Vector2.zero;
        Text cpTxt = cpTxtObj.AddComponent<Text>();
        cpTxt.font = font;
        cpTxt.fontSize = 20;
        cpTxt.alignment = TextAnchor.MiddleCenter;
        cpTxt.color = new Color(0.65f, 0.78f, 1.0f);
        cpTxt.text = "❐";

        cpBtn.onClick.AddListener(() =>
        {
            if (roomIdInputField != null)
            {
                string clip = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clip) && string.IsNullOrEmpty(roomIdInputField.text))
                {
                    roomIdInputField.text = clip.Trim();
                }
                else if (!string.IsNullOrEmpty(roomIdInputField.text))
                {
                    GUIUtility.systemCopyBuffer = roomIdInputField.text.Trim();
                }
            }
        });

        // 3. Nút Orange Gradient Glowing: [ ➔ Tham gia ]
        Button joinBtn = CreateStyledActionButton(
            barObj.transform,
            "JoinRoomButton",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f),
            new Vector2(250f, 50f),
            new Color(0.95f, 0.52f, 0.05f, 0.98f),
            new Color(1.00f, 0.82f, 0.28f, 1.00f),
            GameLocalizationManager.T("BTN_JOIN_ROOM", "➔  Tham gia"),
            font,
            20,
            OnJoinRoomClicked
        );
        joinRoomBtnText = joinBtn != null ? joinBtn.GetComponentInChildren<Text>() : null;
    }

    private Button CreateStyledActionButton(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color borderColor,
        string label,
        Font font,
        int fontSize,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;

        Outline ol = btnObj.AddComponent<Outline>();
        ol.effectColor = borderColor;
        ol.effectDistance = new Vector2(2f, 2f);

        Button btn = btnObj.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;

        return btn;
    }

    // =========================================================================
    // 5. 6 THẺ BÀI GOTHIC NỔI BẬT THEO ĐÚNG THIẾT KẾ CONCEPT
    // =========================================================================
    private void BuildSixGothicModeCards(Transform parent, Font font)
    {
        GameObject cardsContainer = new GameObject("SixGothicCardsContainer");
        cardsContainer.transform.SetParent(parent, false);
        RectTransform cRect = cardsContainer.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(0f, -55f);
        cRect.sizeDelta = new Vector2(1740f, 460f);

        float[] cardX = new float[] { -625f, -375f, -125f, 125f, 375f, 625f };

        Color[] cardThemeColors = new Color[]
        {
            new Color(0.00f, 0.82f, 1.00f, 1f), // 1. Cyan (Đấu đơn)
            new Color(0.00f, 0.90f, 0.46f, 1f), // 2. Mint Green (Đấu đội)
            new Color(0.61f, 0.20f, 0.85f, 1f), // 3. Purple (Máy chủ)
            new Color(1.00f, 0.70f, 0.00f, 1f), // 4. Royal Gold (Làm chủ phòng)
            new Color(0.01f, 0.53f, 0.82f, 1f), // 5. Azure Blue (Làm khách)
            new Color(0.88f, 0.25f, 0.98f, 1f)  // 6. Magenta (Tự động)
        };

        string[] cardTitles = new string[]
        {
            "Đấu đơn",
            "Đấu đội",
            "Máy chủ",
            "Làm chủ phòng",
            "Làm khách",
            "Tự động"
        };

        string[] cardSubtitles = new string[]
        {
            "Ghép trận\nmột người chơi",
            "Cùng bạn bè\nchơi nhiều người",
            "Tạo máy chủ\nriêng của bạn",
            "Quản lý phòng\ncủa bạn",
            "Tham gia phòng\ncủa người khác",
            "Tự động làm chủ\nhoặc làm khách"
        };

        string[] cardBtnLabels = new string[]
        {
            "Tham gia ngay ❯",
            "Tạo phòng ➔",
            "Tạo máy chủ ➔",
            "Bắt đầu ❯",
            "Tham gia ❯",
            "Bắt đầu ❯"
        };

        Action[] cardActions = new Action[]
        {
            OnCard1_StartSinglePlayer,
            OnCreateRoomClicked,
            OnCard3_StartServer,
            OnCard4_StartHost,
            OnCard5_StartClient,
            OnCard6_StartAutoClient
        };

        for (int i = 0; i < 6; i++)
        {
            bool isRoyalHostCard = (i == 3);
            Vector2 cardSize = isRoyalHostCard ? new Vector2(246f, 415f) : new Vector2(236f, 395f);

            BuildSingleGothicCard(
                cardsContainer.transform,
                i,
                cardX[i],
                cardSize,
                cardThemeColors[i],
                sprCardIcons[i],
                cardTitles[i],
                cardSubtitles[i],
                cardBtnLabels[i],
                isRoyalHostCard,
                font,
                cardActions[i]
            );
        }
    }

    private void BuildSingleGothicCard(
        Transform parent,
        int index,
        float posX,
        Vector2 cardSize,
        Color themeColor,
        Sprite iconSprite,
        string titleText,
        string subtitleText,
        string btnLabel,
        bool isRoyalCenterCard,
        Font font,
        Action onClickAction)
    {
        GameObject cardObj = new GameObject($"GothicCard_{index + 1}");
        cardObj.transform.SetParent(parent, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(posX, isRoyalCenterCard ? 8f : 0f);
        cardRect.sizeDelta = cardSize;

        // 1. Khung viền ngoài phát sáng Neon theo màu chủ đạo của thẻ
        Image outerFrame = cardObj.AddComponent<Image>();
        outerFrame.color = new Color(themeColor.r * 0.90f, themeColor.g * 0.90f, themeColor.b * 0.90f, 0.95f);

        Outline glowOutline = cardObj.AddComponent<Outline>();
        glowOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.70f);
        glowOutline.effectDistance = new Vector2(3f, 3f);

        // Vương miện vàng 👑 đậu trên đỉnh viền thẻ Làm chủ phòng
        if (isRoyalCenterCard)
        {
            GameObject crownDecor = new GameObject("PerchedRoyalCrown");
            crownDecor.transform.SetParent(cardObj.transform, false);
            RectTransform crt = crownDecor.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, 14f);
            crt.sizeDelta = new Vector2(56f, 34f);

            Text ctxt = crownDecor.AddComponent<Text>();
            ctxt.font = font;
            ctxt.fontSize = 28;
            ctxt.alignment = TextAnchor.MiddleCenter;
            ctxt.color = new Color(1f, 0.84f, 0.05f);
            ctxt.text = "👑";

            Shadow cshadow = crownDecor.AddComponent<Shadow>();
            cshadow.effectColor = new Color(1f, 0.65f, 0.00f, 0.90f);
            cshadow.effectDistance = new Vector2(0f, 0f);
        }

        // 2. Thân thẻ tối màu bên trong
        GameObject innerBody = new GameObject("CardBody");
        innerBody.transform.SetParent(cardObj.transform, false);
        RectTransform ibRect = innerBody.AddComponent<RectTransform>();
        ibRect.anchorMin = Vector2.zero;
        ibRect.anchorMax = Vector2.one;
        ibRect.offsetMin = new Vector2(4f, 4f);
        ibRect.offsetMax = new Vector2(-4f, -4f);

        Image bodyImg = innerBody.AddComponent<Image>();
        bodyImg.sprite = sprCardGradient;
        bodyImg.color = Color.Lerp(new Color(0.04f, 0.06f, 0.12f, 0.98f), themeColor, 0.16f);

        // 3. Họa tiết bóng hàng rào & bia mộ dưới đáy thẻ
        GameObject botSil = new GameObject("GraveyardBottomSilhouette");
        botSil.transform.SetParent(innerBody.transform, false);
        RectTransform bsRect = botSil.AddComponent<RectTransform>();
        bsRect.anchorMin = new Vector2(0f, 0f);
        bsRect.anchorMax = new Vector2(1f, 0f);
        bsRect.pivot = new Vector2(0.5f, 0f);
        bsRect.anchoredPosition = Vector2.zero;
        bsRect.sizeDelta = new Vector2(0f, 68f);
        Image bsImg = botSil.AddComponent<Image>();
        bsImg.sprite = sprGraveyardBottom;
        bsImg.color = Color.white;

        // 4. Hào quang mờ sau Icon
        GameObject iconHalo = new GameObject("IconHalo");
        iconHalo.transform.SetParent(innerBody.transform, false);
        RectTransform ihRect = iconHalo.AddComponent<RectTransform>();
        ihRect.anchorMin = new Vector2(0.5f, 0.5f);
        ihRect.anchorMax = new Vector2(0.5f, 0.5f);
        ihRect.pivot = new Vector2(0.5f, 0.5f);
        ihRect.anchoredPosition = new Vector2(0f, 68f);
        ihRect.sizeDelta = new Vector2(140f, 140f);
        Image ihImg = iconHalo.AddComponent<Image>();
        ihImg.sprite = sprCircleBadge;
        ihImg.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.22f);

        // 5. Icon Vector Sắc Nét
        GameObject iconObj = new GameObject("CardIcon");
        iconObj.transform.SetParent(innerBody.transform, false);
        RectTransform icRect = iconObj.AddComponent<RectTransform>();
        icRect.anchorMin = new Vector2(0.5f, 0.5f);
        icRect.anchorMax = new Vector2(0.5f, 0.5f);
        icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.anchoredPosition = new Vector2(0f, 68f);
        icRect.sizeDelta = isRoyalCenterCard ? new Vector2(110f, 110f) : new Vector2(98f, 98f);

        Image icImg = iconObj.AddComponent<Image>();
        icImg.sprite = iconSprite;
        icImg.color = Color.Lerp(themeColor, Color.white, 0.40f);

        // 6. Tiêu đề Thẻ (Bold Crisp Title)
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(innerBody.transform, false);
        RectTransform ttRect = titleObj.AddComponent<RectTransform>();
        ttRect.anchorMin = new Vector2(0f, 0.5f);
        ttRect.anchorMax = new Vector2(1f, 0.5f);
        ttRect.pivot = new Vector2(0.5f, 0.5f);
        ttRect.anchoredPosition = new Vector2(0f, -12f);
        ttRect.sizeDelta = new Vector2(cardSize.x - 20f, 32f);

        Text ttTxt = titleObj.AddComponent<Text>();
        ttTxt.font = font;
        ttTxt.fontSize = isRoyalCenterCard ? 22 : 20;
        ttTxt.fontStyle = FontStyle.Bold;
        ttTxt.alignment = TextAnchor.MiddleCenter;
        ttTxt.color = isRoyalCenterCard ? new Color(1f, 0.92f, 0.65f) : Color.white;
        ttTxt.text = titleText;
        if (index >= 0 && index < cardTitleTexts.Length)
        {
            cardTitleTexts[index] = ttTxt;
        }

        Outline ttOut = titleObj.AddComponent<Outline>();
        ttOut.effectColor = new Color(0f, 0f, 0f, 0.90f);
        ttOut.effectDistance = new Vector2(1.5f, -1.5f);

        // 7. Mô tả phụ của Thẻ (Subtitle 2 dòng)
        GameObject subObj = new GameObject("CardSubtitle");
        subObj.transform.SetParent(innerBody.transform, false);
        RectTransform stRect = subObj.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0f, 0.5f);
        stRect.anchorMax = new Vector2(1f, 0.5f);
        stRect.pivot = new Vector2(0.5f, 0.5f);
        stRect.anchoredPosition = new Vector2(0f, -54f);
        stRect.sizeDelta = new Vector2(cardSize.x - 24f, 44f);

        Text stTxt = subObj.AddComponent<Text>();
        stTxt.font = font;
        stTxt.fontSize = 14;
        stTxt.fontStyle = FontStyle.Normal;
        stTxt.alignment = TextAnchor.MiddleCenter;
        stTxt.lineSpacing = 1.15f;
        stTxt.color = Color.Lerp(themeColor, new Color(0.85f, 0.92f, 1f), 0.70f);
        stTxt.text = subtitleText;
        if (index >= 0 && index < cardSubtitleTexts.Length)
        {
            cardSubtitleTexts[index] = stTxt;
        }

        // 8. Nút hành động dạng Pill ở đáy thẻ
        Button actBtn = CreateStyledActionButton(
            innerBody.transform,
            "CardActionButton",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 18f),
            new Vector2(cardSize.x - 36f, 44f),
            new Color(themeColor.r * 0.35f, themeColor.g * 0.35f, themeColor.b * 0.35f, 0.95f),
            new Color(themeColor.r, themeColor.g, themeColor.b, 1f),
            btnLabel,
            font,
            16,
            () => onClickAction?.Invoke()
        );
        if (index >= 0 && index < cardBtnTexts.Length)
        {
            cardBtnTexts[index] = actBtn != null ? actBtn.GetComponentInChildren<Text>() : null;
        }

        // 9. Hiệu ứng Hover & Click
        Button btn = cardObj.AddComponent<Button>();
        btn.targetGraphic = bodyImg;
        btn.onClick.AddListener(() => onClickAction?.Invoke());

        EventTrigger trigger = cardObj.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            cardRect.localScale = Vector3.one * 1.045f;
            outerFrame.color = Color.Lerp(themeColor, Color.white, 0.35f);
            glowOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.95f);
            ihImg.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.36f);
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) =>
        {
            cardRect.localScale = Vector3.one;
            outerFrame.color = new Color(themeColor.r * 0.90f, themeColor.g * 0.90f, themeColor.b * 0.90f, 0.95f);
            glowOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.70f);
            ihImg.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.22f);
        });
        trigger.triggers.Add(exit);
    }

    private void BuildBottomStatusBar(Transform parent, Font font)
    {
        GameObject bottomBar = new GameObject("BottomStatusBar");
        bottomBar.transform.SetParent(parent, false);
        RectTransform brt = bottomBar.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 12f);
        brt.sizeDelta = new Vector2(0f, 38f);

        // Trái: ● Trạng thái kết nối: Kết nối tốt
        GameObject leftObj = new GameObject("ConnectionStatusLeft");
        leftObj.transform.SetParent(bottomBar.transform, false);
        RectTransform lrt = leftObj.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(50f, 0f);
        lrt.sizeDelta = new Vector2(450f, 0f);

        bottomConnectionStatusText = leftObj.AddComponent<Text>();
        bottomConnectionStatusText.font = font;
        bottomConnectionStatusText.fontSize = 15;
        bottomConnectionStatusText.fontStyle = FontStyle.Bold;
        bottomConnectionStatusText.alignment = TextAnchor.MiddleLeft;
        bottomConnectionStatusText.supportRichText = true;
        bottomConnectionStatusText.color = new Color(0.00f, 0.95f, 0.55f);
        bottomConnectionStatusText.text = $"<color=#00FF9D>●</color> {GameLocalizationManager.T("LOBBY_STATUS_GOOD", "Trạng thái kết nối: Kết nối tốt")}";

        // Phải: PLAY TOGETHER • HAVE FUN
        GameObject rightObj = new GameObject("SloganRight");
        rightObj.transform.SetParent(bottomBar.transform, false);
        RectTransform rrt = rightObj.AddComponent<RectTransform>();
        rrt.anchorMin = new Vector2(1f, 0f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(1f, 0.5f);
        rrt.anchoredPosition = new Vector2(-50f, 0f);
        rrt.sizeDelta = new Vector2(400f, 0f);

        bottomSloganText = rightObj.AddComponent<Text>();
        bottomSloganText.font = font;
        bottomSloganText.fontSize = 14;
        bottomSloganText.fontStyle = FontStyle.Bold;
        bottomSloganText.alignment = TextAnchor.MiddleRight;
        bottomSloganText.supportRichText = true;
        bottomSloganText.color = new Color(0.60f, 0.72f, 0.90f, 0.85f);
        bottomSloganText.text = GameLocalizationManager.T("LOBBY_FOOTER_SLOGAN", "PLAY TOGETHER • HAVE FUN");
    }

    // =========================================================================
    // 6. MÀN HÌNH 2: BÊN TRONG PHÒNG CHỜ (WAITING ROOM - HIỂN THỊ ID PHÒNG & CÓ TÔI Ở ĐÂY + 4 NGƯỜI)
    // =========================================================================
    private void BuildWaitingRoomView(Transform parent, Font font)
    {
        waitingRoomViewObj = new GameObject("WaitingRoomView");
        waitingRoomViewObj.transform.SetParent(parent, false);
        RectTransform wrRect = waitingRoomViewObj.AddComponent<RectTransform>();
        wrRect.anchorMin = Vector2.zero;
        wrRect.anchorMax = Vector2.one;
        wrRect.offsetMin = Vector2.zero;
        wrRect.offsetMax = Vector2.zero;

        // Khung Panel chính giữa màn hình
        GameObject mainBox = new GameObject("WaitingRoomMainBox");
        mainBox.transform.SetParent(waitingRoomViewObj.transform, false);
        RectTransform mbRect = mainBox.AddComponent<RectTransform>();
        mbRect.anchorMin = new Vector2(0.5f, 0.5f);
        mbRect.anchorMax = new Vector2(0.5f, 0.5f);
        mbRect.pivot = new Vector2(0.5f, 0.5f);
        mbRect.anchoredPosition = new Vector2(0f, -25f);
        mbRect.sizeDelta = new Vector2(1380f, 820f);

        Image mbBg = mainBox.AddComponent<Image>();
        mbBg.color = new Color(0.05f, 0.08f, 0.15f, 0.96f);
        Outline mbOut = mainBox.AddComponent<Outline>();
        mbOut.effectColor = new Color(0.95f, 0.76f, 0.22f, 0.92f);
        mbOut.effectDistance = new Vector2(3f, 3f);

        // 1. Tiêu đề Sảnh Phòng Chờ
        GameObject headerObj = new GameObject("WaitingHeaderTitle");
        headerObj.transform.SetParent(mainBox.transform, false);
        RectTransform hdRect = headerObj.AddComponent<RectTransform>();
        hdRect.anchorMin = new Vector2(0f, 1f);
        hdRect.anchorMax = new Vector2(1f, 1f);
        hdRect.pivot = new Vector2(0.5f, 1f);
        hdRect.anchoredPosition = new Vector2(0f, -22f);
        hdRect.sizeDelta = new Vector2(-40f, 48f);

        Text hdTxt = headerObj.AddComponent<Text>();
        hdTxt.font = font;
        hdTxt.fontSize = 30;
        hdTxt.fontStyle = FontStyle.Bold;
        hdTxt.alignment = TextAnchor.MiddleCenter;
        hdTxt.color = new Color(1f, 0.88f, 0.32f, 1f);
        hdTxt.text = GameLocalizationManager.T("WAITING_HEADER_TITLE", "🏰 SẢNH PHÒNG CHỜ TRẬN ĐẤU (4 NGƯỜI CHƠI) 🏰");
        waitingHeaderTitleText = hdTxt;

        // 2. Hộp Hiển Thị ID Phòng Cực Lớn + Nút Sao Chép ID + Ô Đổi Tên Của Bạn
        GameObject infoRow = new GameObject("RoomIdAndNicknameRow");
        infoRow.transform.SetParent(mainBox.transform, false);
        RectTransform irRect = infoRow.AddComponent<RectTransform>();
        irRect.anchorMin = new Vector2(0.5f, 1f);
        irRect.anchorMax = new Vector2(0.5f, 1f);
        irRect.pivot = new Vector2(0.5f, 1f);
        irRect.anchoredPosition = new Vector2(0f, -82f);
        irRect.sizeDelta = new Vector2(1280f, 78f);

        Image irBg = infoRow.AddComponent<Image>();
        irBg.color = new Color(0.08f, 0.12f, 0.24f, 0.95f);
        Outline irOut = infoRow.AddComponent<Outline>();
        irOut.effectColor = new Color(0.38f, 0.52f, 0.92f, 0.85f);
        irOut.effectDistance = new Vector2(2f, 2f);

        // Hộp ID Phòng bên trái
        GameObject idBox = new GameObject("RoomIdDisplay");
        idBox.transform.SetParent(infoRow.transform, false);
        RectTransform idRect = idBox.AddComponent<RectTransform>();
        idRect.anchorMin = new Vector2(0f, 0f);
        idRect.anchorMax = new Vector2(0f, 1f);
        idRect.pivot = new Vector2(0f, 0.5f);
        idRect.anchoredPosition = new Vector2(24f, 0f);
        idRect.sizeDelta = new Vector2(360f, 0f);

        waitingRoomIdText = idBox.AddComponent<Text>();
        waitingRoomIdText.font = font;
        waitingRoomIdText.fontSize = 27;
        waitingRoomIdText.fontStyle = FontStyle.Bold;
        waitingRoomIdText.alignment = TextAnchor.MiddleLeft;
        waitingRoomIdText.color = new Color(1f, 0.90f, 0.28f, 1f);
        waitingRoomIdText.text = "🚪 ID PHÒNG:  21234";

        // Nút Sao chép ID Phòng
        Button copyBtn = CreateStyledActionButton(
            infoRow.transform,
            "CopyRoomIdBtn",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(400f, 0f),
            new Vector2(185f, 48f),
            new Color(0.22f, 0.45f, 0.85f, 0.95f),
            new Color(0.50f, 0.78f, 1.00f, 1f),
            "📋 Sao chép ID",
            font,
            17,
            CopyRoomIdToClipboard
        );
        waitingCopyBtnText = copyBtn.GetComponentInChildren<Text>();

        // Phần hiển thị & sửa "Tên của tôi" bên phải thanh Info
        GameObject myNameLbl = new GameObject("MyNameLabel");
        myNameLbl.transform.SetParent(infoRow.transform, false);
        RectTransform mnRect = myNameLbl.AddComponent<RectTransform>();
        mnRect.anchorMin = new Vector2(0f, 0.5f);
        mnRect.anchorMax = new Vector2(0f, 0.5f);
        mnRect.pivot = new Vector2(0f, 0.5f);
        mnRect.anchoredPosition = new Vector2(635f, 0f);
        mnRect.sizeDelta = new Vector2(175f, 48f);

        Text mnTxt = myNameLbl.AddComponent<Text>();
        mnTxt.font = font;
        mnTxt.fontSize = 18;
        mnTxt.fontStyle = FontStyle.Bold;
        mnTxt.alignment = TextAnchor.MiddleLeft;
        mnTxt.color = new Color(0.85f, 0.92f, 1f, 1f);
        mnTxt.text = GameLocalizationManager.T("WAITING_MY_NAME_LABEL", "👤 Tên của bạn:");
        waitingMyNameLabelText = mnTxt;

        GameObject nameInObj = new GameObject("MyNicknameInput");
        nameInObj.transform.SetParent(infoRow.transform, false);
        RectTransform niRect = nameInObj.AddComponent<RectTransform>();
        niRect.anchorMin = new Vector2(0f, 0.5f);
        niRect.anchorMax = new Vector2(0f, 0.5f);
        niRect.pivot = new Vector2(0f, 0.5f);
        niRect.anchoredPosition = new Vector2(805f, 0f);
        niRect.sizeDelta = new Vector2(280f, 48f);

        Image niBg = nameInObj.AddComponent<Image>();
        niBg.color = new Color(0.03f, 0.05f, 0.11f, 0.96f);
        Outline niOut = nameInObj.AddComponent<Outline>();
        niOut.effectColor = new Color(0.25f, 0.85f, 0.65f, 0.85f);
        niOut.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject niTxtObj = new GameObject("Text");
        niTxtObj.transform.SetParent(nameInObj.transform, false);
        RectTransform nitRect = niTxtObj.AddComponent<RectTransform>();
        nitRect.anchorMin = Vector2.zero;
        nitRect.anchorMax = Vector2.one;
        nitRect.offsetMin = new Vector2(14f, 0f);
        nitRect.offsetMax = new Vector2(-14f, 0f);

        Text niTxt = niTxtObj.AddComponent<Text>();
        niTxt.font = font;
        niTxt.fontSize = 19;
        niTxt.fontStyle = FontStyle.Bold;
        niTxt.alignment = TextAnchor.MiddleLeft;
        niTxt.color = new Color(0.30f, 1.0f, 0.70f, 1f);
        niTxt.supportRichText = false;

        waitingPlayerNameInput = nameInObj.AddComponent<InputField>();
        waitingPlayerNameInput.textComponent = niTxt;
        waitingPlayerNameInput.characterLimit = 20;
        waitingPlayerNameInput.onEndEdit.AddListener(OnWaitingNicknameChanged);

        Button applyNameBtn = CreateStyledActionButton(
            infoRow.transform,
            "ApplyNameBtn",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(1100f, 0f),
            new Vector2(155f, 48f),
            new Color(0.15f, 0.62f, 0.45f, 0.95f),
            new Color(0.30f, 0.95f, 0.68f, 1f),
            GameLocalizationManager.T("WAITING_SAVE_NAME_BTN", "✔ Lưu tên"),
            font,
            17,
            () =>
            {
                if (waitingPlayerNameInput != null)
                {
                    OnWaitingNicknameChanged(waitingPlayerNameInput.text);
                }
            }
        );
        waitingSaveNameBtnText = applyNameBtn != null ? applyNameBtn.GetComponentInChildren<Text>() : null;

        // 3. Tiêu đề Danh sách người chơi trong phòng (X / 4)
        GameObject countRow = new GameObject("PlayerCountHeader");
        countRow.transform.SetParent(mainBox.transform, false);
        RectTransform crRect = countRow.AddComponent<RectTransform>();
        crRect.anchorMin = new Vector2(0f, 1f);
        crRect.anchorMax = new Vector2(1f, 1f);
        crRect.pivot = new Vector2(0.5f, 1f);
        crRect.anchoredPosition = new Vector2(0f, -176f);
        crRect.sizeDelta = new Vector2(-80f, 36f);

        waitingPlayerCountText = countRow.AddComponent<Text>();
        waitingPlayerCountText.font = font;
        waitingPlayerCountText.fontSize = 22;
        waitingPlayerCountText.fontStyle = FontStyle.Bold;
        waitingPlayerCountText.alignment = TextAnchor.MiddleCenter;
        waitingPlayerCountText.color = new Color(0.45f, 0.95f, 0.75f, 1f);
        waitingPlayerCountText.text = "👥 DANH SÁCH NGƯỜI CHƠI TRONG PHÒNG:  1 / 4 NGƯỜI";

        // 4. Lưới 4 Thẻ Vị Trí Người Chơi (Hiển thị rõ "BẠN (TÔI Ở ĐÂY)" + Những người khác trong phòng)
        GameObject slotsContainer = new GameObject("FourPlayerSlotsContainer");
        slotsContainer.transform.SetParent(mainBox.transform, false);
        RectTransform scRect = slotsContainer.AddComponent<RectTransform>();
        scRect.anchorMin = new Vector2(0.5f, 1f);
        scRect.anchorMax = new Vector2(0.5f, 1f);
        scRect.pivot = new Vector2(0.5f, 1f);
        scRect.anchoredPosition = new Vector2(0f, -225f);
        scRect.sizeDelta = new Vector2(1280f, 390f);

        float[] slotX = new float[] { -486f, -162f, 162f, 486f };
        for (int i = 0; i < 4; i++)
        {
            playerSlots[i] = CreateWaitingPlayerSlotCard(slotsContainer.transform, i, slotX[i], font);
        }

        // 5. Thanh Tiến Trình & Thông Báo Chờ Đủ 4 Người Mới Chạy Game
        GameObject statusBox = new GameObject("WaitingStatusBanner");
        statusBox.transform.SetParent(mainBox.transform, false);
        RectTransform sbRect = statusBox.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 0f);
        sbRect.anchorMax = new Vector2(0.5f, 0f);
        sbRect.pivot = new Vector2(0.5f, 0f);
        sbRect.anchoredPosition = new Vector2(0f, 108f);
        sbRect.sizeDelta = new Vector2(1280f, 74f);

        Image sbBg = statusBox.AddComponent<Image>();
        sbBg.color = new Color(0.03f, 0.05f, 0.10f, 0.95f);
        Outline sbOut = statusBox.AddComponent<Outline>();
        sbOut.effectColor = new Color(0.35f, 0.48f, 0.85f, 0.8f);
        sbOut.effectDistance = new Vector2(1.5f, 1.5f);

        // Thanh tiến trình (1/4 -> 4/4)
        GameObject progBgObj = new GameObject("ProgressBarBg");
        progBgObj.transform.SetParent(statusBox.transform, false);
        RectTransform pbRect = progBgObj.AddComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0f, 0f);
        pbRect.anchorMax = new Vector2(1f, 0f);
        pbRect.pivot = new Vector2(0.5f, 0f);
        pbRect.anchoredPosition = new Vector2(0f, 0f);
        pbRect.sizeDelta = new Vector2(0f, 10f);
        Image pbImg = progBgObj.AddComponent<Image>();
        pbImg.color = new Color(0.10f, 0.14f, 0.25f, 1f);

        GameObject progFillObj = new GameObject("ProgressBarFill");
        progFillObj.transform.SetParent(progBgObj.transform, false);
        RectTransform pfRect = progFillObj.AddComponent<RectTransform>();
        pfRect.anchorMin = new Vector2(0f, 0f);
        pfRect.anchorMax = new Vector2(0.25f, 1f);
        pfRect.offsetMin = Vector2.zero;
        pfRect.offsetMax = Vector2.zero;
        waitingProgressFill = progFillObj.AddComponent<Image>();
        waitingProgressFill.color = new Color(0.20f, 0.92f, 0.55f, 1f);

        GameObject stTxtObj = new GameObject("StatusText");
        stTxtObj.transform.SetParent(statusBox.transform, false);
        RectTransform sttRect = stTxtObj.AddComponent<RectTransform>();
        sttRect.anchorMin = Vector2.zero;
        sttRect.anchorMax = Vector2.one;
        sttRect.offsetMin = new Vector2(20f, 10f);
        sttRect.offsetMax = new Vector2(-20f, 0f);

        waitingStatusBannerText = stTxtObj.AddComponent<Text>();
        waitingStatusBannerText.font = font;
        waitingStatusBannerText.fontSize = 20;
        waitingStatusBannerText.fontStyle = FontStyle.Bold;
        waitingStatusBannerText.alignment = TextAnchor.MiddleCenter;
        waitingStatusBannerText.color = new Color(1f, 0.84f, 0.28f, 1f);
        waitingStatusBannerText.text = "🔒 GAME ĐANG TẠM DỪNG CHỜ ĐỦ 4 NGƯỜI CHƠI VÀO PHÒNG...";

        // 6. Hàng Nút Điều Khiển Phía Dưới Phòng Chờ
        // Nút Rời Phòng / Quay Lại
        Button leaveBtn = CreateStyledActionButton(
            mainBox.transform,
            "LeaveRoomBtn",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(50f, 26f),
            new Vector2(340f, 60f),
            new Color(0.65f, 0.18f, 0.22f, 0.95f),
            new Color(1.00f, 0.40f, 0.45f, 1f),
            GameLocalizationManager.T("WAITING_LEAVE_BTN", "⬅  Rời Phòng / Quay Lại Sảnh"),
            font,
            20,
            OnLeaveWaitingRoomClicked
        );
        waitingLeaveBtnText = leaveBtn != null ? leaveBtn.GetComponentInChildren<Text>() : null;

        // Nút lớn trung tâm: Bấm để xác nhận sẵn sàng (Toggle Ready)
        Button readyBtn = CreateStyledActionButton(
            mainBox.transform,
            "ReadyConfirmBtn",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 26f),
            new Vector2(380f, 60f),
            new Color(0.18f, 0.45f, 0.85f, 0.95f),
            new Color(0.35f, 0.70f, 1.00f, 1f),
            "✔ BẤM ĐỂ XÁC NHẬN SẴN SÀNG",
            font,
            20,
            OnToggleReadyClicked
        );
        waitingReadyBtnBg = readyBtn.GetComponent<Image>();
        waitingReadyBtnOutline = readyBtn.GetComponent<Outline>();
        waitingReadyBtnText = readyBtn.GetComponentInChildren<Text>();

        // Nút Bắt Đầu Ngay (Dành cho Chủ phòng nếu muốn Test nhanh không cần chờ đủ 4 máy)
        Button forceBtn = CreateStyledActionButton(
            mainBox.transform,
            "ForceStartTestBtn",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-50f, 26f),
            new Vector2(460f, 60f),
            new Color(0.16f, 0.62f, 0.36f, 0.95f),
            new Color(0.35f, 0.98f, 0.62f, 1f),
            "⚡ Chủ Phòng: Bắt Đầu Ngay (Test Nhanh) [F1]",
            font,
            19,
            OnHostForceStartClicked
        );
        waitingForceStartBtnObj = forceBtn.gameObject;
        if (!Application.isEditor && waitingForceStartBtnObj != null)
        {
            waitingForceStartBtnObj.SetActive(false);
        }
    }

    private PlayerSlotUI CreateWaitingPlayerSlotCard(Transform parent, int slotIndex, float posX, Font font)
    {
        PlayerSlotUI slot = new PlayerSlotUI();

        GameObject cardObj = new GameObject($"PlayerSlot_{slotIndex + 1}");
        cardObj.transform.SetParent(parent, false);
        RectTransform rt = cardObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta = new Vector2(298f, 375f);
        slot.rootObj = cardObj;

        slot.bgImage = cardObj.AddComponent<Image>();
        slot.bgImage.color = new Color(0.07f, 0.11f, 0.22f, 0.96f);

        slot.cardOutline = cardObj.AddComponent<Outline>();
        slot.cardOutline.effectColor = new Color(0.28f, 0.38f, 0.62f, 0.85f);
        slot.cardOutline.effectDistance = new Vector2(2.5f, 2.5f);

        // Nhãn vị trí ở đỉnh thẻ (VD: "VỊ TRÍ 1 - BẠN ĐANG Ở ĐÂY")
        GameObject topBadge = new GameObject("SlotBadge");
        topBadge.transform.SetParent(cardObj.transform, false);
        RectTransform tbRect = topBadge.AddComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0f, 1f);
        tbRect.anchorMax = new Vector2(1f, 1f);
        tbRect.pivot = new Vector2(0.5f, 1f);
        tbRect.anchoredPosition = new Vector2(0f, -12f);
        tbRect.sizeDelta = new Vector2(-20f, 38f);

        slot.slotBadgeText = topBadge.AddComponent<Text>();
        slot.slotBadgeText.font = font;
        slot.slotBadgeText.fontSize = 16;
        slot.slotBadgeText.fontStyle = FontStyle.Bold;
        slot.slotBadgeText.alignment = TextAnchor.MiddleCenter;
        slot.slotBadgeText.color = new Color(1f, 0.88f, 0.35f, 1f);
        slot.slotBadgeText.text = $"VỊ TRÍ #{slotIndex + 1}";

        // Vòng tròn Avatar ở giữa
        GameObject avCircle = new GameObject("AvatarCircle");
        avCircle.transform.SetParent(cardObj.transform, false);
        RectTransform avRect = avCircle.AddComponent<RectTransform>();
        avRect.anchorMin = new Vector2(0.5f, 0.5f);
        avRect.anchorMax = new Vector2(0.5f, 0.5f);
        avRect.pivot = new Vector2(0.5f, 0.5f);
        avRect.anchoredPosition = new Vector2(0f, 34f);
        avRect.sizeDelta = new Vector2(118f, 118f);

        slot.avatarBg = avCircle.AddComponent<Image>();
        slot.avatarBg.sprite = sprCircleBadge;
        slot.avatarBg.color = new Color(0.14f, 0.24f, 0.45f, 0.9f);

        GameObject avIconObj = new GameObject("AvatarIcon");
        avIconObj.transform.SetParent(avCircle.transform, false);
        RectTransform aiRect = avIconObj.AddComponent<RectTransform>();
        aiRect.anchorMin = Vector2.zero;
        aiRect.anchorMax = Vector2.one;
        aiRect.offsetMin = new Vector2(20f, 20f);
        aiRect.offsetMax = new Vector2(-20f, -20f);

        slot.avatarIcon = avIconObj.AddComponent<Image>();
        slot.avatarIcon.sprite = sprCardIcons[0];
        slot.avatarIcon.color = Color.white;

        // Tên người chơi
        GameObject nameObj = new GameObject("PlayerName");
        nameObj.transform.SetParent(cardObj.transform, false);
        RectTransform nmRect = nameObj.AddComponent<RectTransform>();
        nmRect.anchorMin = new Vector2(0f, 0f);
        nmRect.anchorMax = new Vector2(1f, 0f);
        nmRect.pivot = new Vector2(0.5f, 0f);
        nmRect.anchoredPosition = new Vector2(0f, 78f);
        nmRect.sizeDelta = new Vector2(-20f, 54f);

        slot.playerNameText = nameObj.AddComponent<Text>();
        slot.playerNameText.font = font;
        slot.playerNameText.fontSize = 20;
        slot.playerNameText.fontStyle = FontStyle.Bold;
        slot.playerNameText.alignment = TextAnchor.MiddleCenter;
        slot.playerNameText.color = Color.white;
        slot.playerNameText.text = "Đang chờ...";

        // Trạng thái sẵn sàng dưới đáy thẻ
        GameObject stObj = new GameObject("RoleStatus");
        stObj.transform.SetParent(cardObj.transform, false);
        RectTransform stRect = stObj.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0f, 0f);
        stRect.anchorMax = new Vector2(1f, 0f);
        stRect.pivot = new Vector2(0.5f, 0f);
        stRect.anchoredPosition = new Vector2(0f, 22f);
        stRect.sizeDelta = new Vector2(-20f, 44f);

        slot.playerRoleStatusText = stObj.AddComponent<Text>();
        slot.playerRoleStatusText.font = font;
        slot.playerRoleStatusText.fontSize = 15;
        slot.playerRoleStatusText.fontStyle = FontStyle.Bold;
        slot.playerRoleStatusText.alignment = TextAnchor.MiddleCenter;
        slot.playerRoleStatusText.color = new Color(0.65f, 0.75f, 0.90f, 0.9f);
        slot.playerRoleStatusText.text = "⏳ Chưa có người";

        return slot;
    }

    // =========================================================================
    // 7. THANH HUD ID PHÒNG NHỎ GỌN TRONG GAME (CHỈ HIỆN KHI ĐỦ NGƯỜI & GAME BẮT ĐẦU)
    // =========================================================================
    private void BuildInGameRoomHUD(Transform parent, Font font)
    {
        inGameRoomBannerObj = new GameObject("InGameRoomHUD");
        inGameRoomBannerObj.transform.SetParent(parent, false);
        RectTransform hRect = inGameRoomBannerObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(1f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot = new Vector2(1f, 1f);
        hRect.anchoredPosition = new Vector2(-20f, -20f);
        hRect.sizeDelta = new Vector2(360f, 46f);

        Image hBg = inGameRoomBannerObj.AddComponent<Image>();
        hBg.color = new Color(0.04f, 0.07f, 0.14f, 0.86f);
        Outline hOut = inGameRoomBannerObj.AddComponent<Outline>();
        hOut.effectColor = new Color(0.95f, 0.78f, 0.22f, 0.85f);
        hOut.effectDistance = new Vector2(1.5f, 1.5f);

        Button hBtn = inGameRoomBannerObj.AddComponent<Button>();
        hBtn.onClick.AddListener(CopyRoomIdToClipboard);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(inGameRoomBannerObj.transform, false);
        RectTransform tRect = txtObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        inGameRoomBannerText = txtObj.AddComponent<Text>();
        inGameRoomBannerText.font = font;
        inGameRoomBannerText.fontSize = 14;
        inGameRoomBannerText.fontStyle = FontStyle.Bold;
        inGameRoomBannerText.alignment = TextAnchor.MiddleCenter;
        inGameRoomBannerText.color = new Color(1f, 0.90f, 0.35f, 1f);
        inGameRoomBannerText.text = "🚪 ID Phòng: ...";

        inGameRoomBannerObj.SetActive(false);
    }

    // =========================================================================
    // 7.1. BẢNG OVERLAY VÒNG XOAY TÌM TRẬN TRỰC TIẾP (KHÔNG CẦN VÀO PHÒNG CHỜ)
    // =========================================================================
    private void BuildMatchmakingSpinnerOverlay(Transform parent, Font font)
    {
        matchmakingSpinnerOverlayObj = new GameObject("MatchmakingSpinnerOverlay");
        matchmakingSpinnerOverlayObj.transform.SetParent(parent, false);

        RectTransform rt = matchmakingSpinnerOverlayObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -30f);
        rt.sizeDelta = new Vector2(580f, 260f);

        Image bg = matchmakingSpinnerOverlayObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.16f, 0.97f);

        Outline outline = matchmakingSpinnerOverlayObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.88f, 1f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Biểu tượng vòng xoay (Spinner Arc)
        GameObject spinnerGo = new GameObject("SpinnerArc");
        spinnerGo.transform.SetParent(matchmakingSpinnerOverlayObj.transform, false);
        RectTransform srt = spinnerGo.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, -44f);
        srt.sizeDelta = new Vector2(58f, 58f);

        matchmakingSpinnerArcImage = spinnerGo.AddComponent<Image>();
        matchmakingSpinnerArcImage.sprite = sprSpinnerArc;
        matchmakingSpinnerArcImage.color = new Color(0f, 0.95f, 1f, 1f);

        // Tiêu đề
        GameObject titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(matchmakingSpinnerOverlayObj.transform, false);
        RectTransform trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -82f);
        trt.sizeDelta = new Vector2(-20f, 32f);

        matchmakingSpinnerTitleText = titleGo.AddComponent<Text>();
        matchmakingSpinnerTitleText.font = font;
        matchmakingSpinnerTitleText.fontSize = 20;
        matchmakingSpinnerTitleText.fontStyle = FontStyle.Bold;
        matchmakingSpinnerTitleText.alignment = TextAnchor.MiddleCenter;
        matchmakingSpinnerTitleText.color = new Color(0.35f, 1f, 0.85f, 1f);
        matchmakingSpinnerTitleText.text = "🔍 ĐANG TÌM TRẬN ĐẤU...";

        // Đồng hồ đếm giây
        GameObject timerGo = new GameObject("TimerText");
        timerGo.transform.SetParent(matchmakingSpinnerOverlayObj.transform, false);
        RectTransform tmrt = timerGo.AddComponent<RectTransform>();
        tmrt.anchorMin = new Vector2(0f, 1f);
        tmrt.anchorMax = new Vector2(1f, 1f);
        tmrt.pivot = new Vector2(0.5f, 1f);
        tmrt.anchoredPosition = new Vector2(0f, -116f);
        tmrt.sizeDelta = new Vector2(-20f, 34f);

        matchmakingSpinnerTimerText = timerGo.AddComponent<Text>();
        matchmakingSpinnerTimerText.font = font;
        matchmakingSpinnerTimerText.fontSize = 25;
        matchmakingSpinnerTimerText.fontStyle = FontStyle.Bold;
        matchmakingSpinnerTimerText.alignment = TextAnchor.MiddleCenter;
        matchmakingSpinnerTimerText.color = new Color(1f, 0.90f, 0.25f, 1f);
        matchmakingSpinnerTimerText.text = "00:00";

        // Số người chơi đã tìm thấy
        GameObject countGo = new GameObject("CountText");
        countGo.transform.SetParent(matchmakingSpinnerOverlayObj.transform, false);
        RectTransform crt = countGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, -152f);
        crt.sizeDelta = new Vector2(-20f, 26f);

        matchmakingSpinnerCountText = countGo.AddComponent<Text>();
        matchmakingSpinnerCountText.font = font;
        matchmakingSpinnerCountText.fontSize = 16;
        matchmakingSpinnerCountText.fontStyle = FontStyle.Bold;
        matchmakingSpinnerCountText.alignment = TextAnchor.MiddleCenter;
        matchmakingSpinnerCountText.color = Color.white;
        matchmakingSpinnerCountText.supportRichText = true;
        matchmakingSpinnerCountText.text = "👥 Đã tìm thấy: <color=#2EE87A>1 / 4</color> người chơi (Đủ 4 tự vào)";

        // Nút HỦY TÌM TRẬN
        Button cancelBtn = CreateStyledActionButton(
            matchmakingSpinnerOverlayObj.transform,
            "CancelMatchmakingBtn",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 18f),
            new Vector2(230f, 44f),
            new Color(0.75f, 0.20f, 0.20f, 0.95f),
            new Color(1f, 0.45f, 0.45f, 1f),
            GameLocalizationManager.T("SPINNER_CANCEL_BTN", "🛑 HỦY TÌM TRẬN"),
            font,
            16,
            CancelDirectMatchmaking
        );
        matchmakingSpinnerCancelBtn = cancelBtn;
        matchmakingSpinnerCancelBtnText = cancelBtn != null ? cancelBtn.GetComponentInChildren<Text>() : null;

        matchmakingSpinnerOverlayObj.SetActive(false);
    }

    public void StartDirectMatchmaking()
    {
        isDirectMatchmakingActive = true;
        isInsideWaitingRoom = false;
        if (waitingRoomViewObj != null) waitingRoomViewObj.SetActive(false);
        if (mainCardsViewObj != null) mainCardsViewObj.SetActive(true);

        if (matchmakingSpinnerOverlayObj != null)
        {
            matchmakingSpinnerOverlayObj.SetActive(true);
            if (matchmakingSpinnerTimerText != null) matchmakingSpinnerTimerText.text = "00:00";
            if (matchmakingSpinnerCountText != null) matchmakingSpinnerCountText.text = "👥 Đang tìm: <color=#2EE87A>1 / 4</color> người chơi...";
            if (matchmakingSpinnerTitleText != null)
            {
                matchmakingSpinnerTitleText.text = "🔍 ĐANG TÌM TRẬN ĐẤU...";
                matchmakingSpinnerTitleText.color = new Color(0.35f, 1f, 0.85f, 1f);
            }
            if (matchmakingSpinnerCancelBtn != null) matchmakingSpinnerCancelBtn.gameObject.SetActive(true);
        }

        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        if (photonMgr != null)
        {
            photonMgr.StartSoloPartyMatchmaking();
        }
    }

    public void CancelDirectMatchmaking()
    {
        isDirectMatchmakingActive = false;
        if (matchmakingSpinnerOverlayObj != null)
        {
            matchmakingSpinnerOverlayObj.SetActive(false);
        }

        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        if (photonMgr != null)
        {
            photonMgr.CancelSoloPartyMatchmaking();
        }
    }

    // =========================================================================
    // 8. ĐIỀU HƯỚNG GIỮA SẢNH CHÍNH <-> PHÒNG CHỜ 4 NGƯỜI <-> VÀO GAME
    // =========================================================================
    private void SwitchToMainCardsView()
    {
        isInsideWaitingRoom = false;
        if (mainCardsViewObj != null) mainCardsViewObj.SetActive(true);
        if (waitingRoomViewObj != null) waitingRoomViewObj.SetActive(false);
        if (!isDirectMatchmakingActive && matchmakingSpinnerOverlayObj != null)
        {
            matchmakingSpinnerOverlayObj.SetActive(false);
        }
    }

    private void EnterWaitingRoomWithId(string roomId, bool isCreatingNewRoom)
    {
        if (isDirectMatchmakingActive)
        {
            CancelDirectMatchmaking();
        }

        bool autoMatchAnyOpenRoom = !isCreatingNewRoom && string.IsNullOrWhiteSpace(roomId);
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = isCreatingNewRoom ? UnityEngine.Random.Range(10000, 99999).ToString() : "";
        }
        currentRoomId = roomId.Trim();

        if (!string.IsNullOrEmpty(currentRoomId))
        {
            if (roomIdInputField != null) roomIdInputField.text = currentRoomId;
            if (bootstrap != null) bootstrap.DefaultRoomName = currentRoomId;
        }

        if (PhotonMultiplayerChatVoice.Instance != null)
        {
            if (waitingPlayerNameInput != null && string.IsNullOrWhiteSpace(waitingPlayerNameInput.text))
            {
                waitingPlayerNameInput.text = PhotonMultiplayerChatVoice.Instance.playerName;
            }
            if (autoMatchAnyOpenRoom)
            {
                PhotonMultiplayerChatVoice.Instance.JoinRandomOrCreateRoom();
            }
            else
            {
                PhotonMultiplayerChatVoice.Instance.JoinOrCreateCustomRoom(currentRoomId);
            }
        }

        isInsideWaitingRoom = true;
        if (mainCardsViewObj != null) mainCardsViewObj.SetActive(false);
        if (waitingRoomViewObj != null) waitingRoomViewObj.SetActive(true);

        if (waitingCopyBtnText != null) waitingCopyBtnText.text = "📋 Sao chép ID";

        UpdateWaitingRoomUI();
    }

    private void OnRoomIdInputChanged(string val)
    {
        currentRoomId = val != null ? val.Trim() : "";
        if (bootstrap != null) bootstrap.DefaultRoomName = currentRoomId;
    }

    private void OnWaitingNicknameChanged(string newNick)
    {
        if (string.IsNullOrWhiteSpace(newNick)) return;
        if (PhotonMultiplayerChatVoice.Instance != null)
        {
            PhotonMultiplayerChatVoice.Instance.SetLocalPlayerNickname(newNick.Trim());
        }
        UpdateWaitingRoomUI();
    }

    private string GetOrGenerateRoomId(bool forceNewIfEmpty = true)
    {
        string typed = roomIdInputField != null ? roomIdInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(typed) && forceNewIfEmpty)
        {
            typed = UnityEngine.Random.Range(10000, 99999).ToString();
            if (roomIdInputField != null) roomIdInputField.text = typed;
        }
        return typed;
    }

    // Các nút bấm ở Sảnh Chính
    public void OnCreateRoomClicked()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        // Khi nhấn Tạo phòng: Nếu chưa nhập ID thì tự sinh ID 5 chữ số mới và đưa ngay vào Phòng Chờ!
        string rid = roomIdInputField != null ? roomIdInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(rid))
        {
            rid = UnityEngine.Random.Range(10000, 99999).ToString();
        }
        Debug.Log($"<color=#FFD700>[Lobby] TẠO PHÒNG MỚI - ID Phòng: {rid}</color>");
        EnterWaitingRoomWithId(rid, true);
    }

    public void OnJoinRoomClicked()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        string rid = GetOrGenerateRoomId(false);
        Debug.Log($"<color=#A29BFE>[Lobby] THAM GIA PHÒNG - ID Phòng: {(string.IsNullOrEmpty(rid) ? "Tự động tìm phòng đang mở" : rid)}</color>");
        EnterWaitingRoomWithId(rid, false);
    }

    public void OnCard1_StartSinglePlayer()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        Debug.Log("<color=#3498DB>[Lobby] Chế độ Đấu Đơn / Ghép Trận: Bắt đầu tìm trận xoay xoay trực tiếp (không cần vào phòng chờ!)</color>");
        StartDirectMatchmaking();
    }

    public void OnCard2_StartSharedClient()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        string rid = GetOrGenerateRoomId(true);
        Debug.Log($"<color=#1ABC9C>[Lobby] Chế độ Đấu Tổ Đội - Phòng Chờ ID: {rid}</color>");
        EnterWaitingRoomWithId(rid, true);
    }

    public void OnCard3_StartServer()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        string rid = GetOrGenerateRoomId(true);
        Debug.Log($"<color=#9B59B6>[Lobby] Bắt đầu máy chủ - Phòng Chờ ID: {rid}</color>");
        EnterWaitingRoomWithId(rid, true);
    }

    public void OnCard4_StartHost()
    {
        // Nhấn thẻ "Bắt đầu làm chủ phòng" -> Sinh ID Phòng và đưa vào bên trong Phòng Chờ!
        OnCreateRoomClicked();
    }

    public void OnCard5_StartClient()
    {
        OnJoinRoomClicked();
    }

    public void OnCard6_StartAutoClient()
    {
        PhotonMultiplayerChatVoice.AllowSinglePlayerEarlyStart = false;
        Debug.Log("<color=#E056FD>[Lobby] Tự động tìm trận trực tiếp</color>");
        StartDirectMatchmaking();
    }

    public void HideImmediatelyForMatchStart()
    {
        isInsideWaitingRoom = false;
        isDirectMatchmakingActive = false;
        if (matchmakingSpinnerOverlayObj != null && matchmakingSpinnerOverlayObj.activeSelf)
        {
            matchmakingSpinnerOverlayObj.SetActive(false);
        }
        ThirdPersonCamera.isUIPanelOpen = false;
        if (lobbyRootObj != null && lobbyRootObj.activeSelf)
        {
            lobbyRootObj.SetActive(false);
        }
        if (inGameRoomBannerObj != null && inGameRoomBannerObj.activeSelf)
        {
            inGameRoomBannerObj.SetActive(false);
        }
        if (lobbyCanvas != null && lobbyCanvas.gameObject.activeSelf)
        {
            lobbyCanvas.gameObject.SetActive(false);
        }
    }

    public void OnLeaveWaitingRoomClicked()
    {
        Debug.Log("<color=yellow>[Lobby] Rời phòng chờ, quay lại sảnh chọn chế độ.</color>");
        currentRoomId = "";
        if (roomIdInputField != null)
        {
            roomIdInputField.text = "";
        }
        if (PhotonMultiplayerChatVoice.Instance != null)
        {
            PhotonMultiplayerChatVoice.Instance.LeaveRoomAndReturnToMenu();
        }
        if (bootstrap != null && bootstrap.CurrentStage != FusionBootstrap.Stage.Disconnected)
        {
            bootstrap.ShutdownAll();
        }
        SwitchToMainCardsView();
    }

    public void OnToggleReadyClicked()
    {
        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        if (photonMgr == null) return;

        int currentCount = photonMgr.CurrentRoomPlayersCount;

        // 1. Nếu đang trong trạng thái tìm trận -> Bấm nút là HỦY TÌM TRẬN
        if (PhotonMultiplayerChatVoice.IsMatchmakingSearching)
        {
            photonMgr.CancelSoloPartyMatchmaking();
            UpdateWaitingRoomUI();
            return;
        }

        // 2. Nếu phòng đã đủ 4 người -> Bật/tắt sẵn sàng để vào trận ngay
        if (currentCount >= 4)
        {
            photonMgr.ToggleLocalPlayerReady();
            UpdateWaitingRoomUI();
            return;
        }

        // 3. Nếu phòng chưa đủ 4 người (1, 2 hoặc 3 người):
        // Nếu là người đi đơn 1 mình (Solo) -> Bắt đầu tìm trận ngay!
        if (currentCount <= 1)
        {
            photonMgr.StartSoloPartyMatchmaking();
            UpdateWaitingRoomUI();
            return;
        }

        // Nếu là tổ đội (2 hoặc 3 người):
        if (!photonMgr.IsLocalMaster)
        {
            // Thành viên trong đội: bật/tắt sẵn sàng
            photonMgr.ToggleLocalPlayerReady();
        }
        else
        {
            // Chủ phòng tổ đội: kiểm tra xem các thành viên đã sẵn sàng hết chưa
            if (photonMgr.AreAllOtherPlayersReady())
            {
                photonMgr.StartSoloPartyMatchmaking();
            }
            else
            {
                photonMgr.AddSystemMessage("⚠️ Hãy nhắc đồng đội trong tổ đội bấm [Sẵn Sàng] trước khi Bắt Đầu Tìm Trận!");
            }
        }
        UpdateWaitingRoomUI();
    }

    public void OnHostForceStartClicked()
    {
        Debug.Log("<color=#00FF88>[Lobby] Chủ phòng bấm Bắt đầu ngay (Test nhanh)!</color>");
        if (PhotonMultiplayerChatVoice.Instance != null)
        {
            PhotonMultiplayerChatVoice.Instance.HostForceStartGame();
        }
    }

    public void CopyRoomIdToClipboard()
    {
        string rid = !string.IsNullOrEmpty(currentRoomId)
            ? currentRoomId
            : (roomIdInputField != null ? roomIdInputField.text.Trim() : "");

        if (!string.IsNullOrEmpty(rid))
        {
            GUIUtility.systemCopyBuffer = rid;
            if (waitingCopyBtnText != null)
            {
                waitingCopyBtnText.text = "✔ Đã sao chép!";
            }
            if (inGameRoomBannerText != null)
            {
                inGameRoomBannerText.text = $"📋 ĐÃ SAO CHÉP ID PHÒNG: {rid}";
            }
        }
    }

    // =========================================================================
    // 9. CẬP NHẬT TRẠNG THÁI MỖI FRAME (ĐẢM BẢO GAME KHÔNG CHẠY TRƯỚC KHI ĐỦ 4 NGƯỜI)
    // =========================================================================
    private void UpdateLobbyAndWaitingState()
    {
        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        bool isPhotonConnected = photonMgr != null && photonMgr.IsConnectedToMaster;
        bool isMatchRunning = PhotonMultiplayerChatVoice.IsGameStarted;

        // 1. Cập nhật Badge Trạng Thái Kết Nối góc trên bên trái
        if (connectionStatusBadgeText != null && connectionStatusDotImage != null)
        {
            if (isPhotonConnected)
            {
                string reg = photonMgr != null ? photonMgr.ConnectedRegionName : "";
                connectionStatusBadgeText.text = string.IsNullOrEmpty(reg) ? "Đã kết nối" : $"Đã kết nối ({reg})";
                connectionStatusBadgeText.color = colConnected;
                connectionStatusDotImage.color = colConnected;
            }
            else
            {
                string st = (photonMgr != null && !string.IsNullOrEmpty(photonMgr.LastLobbyStatus))
                    ? photonMgr.LastLobbyStatus
                    : "Đang kết nối...";
                connectionStatusBadgeText.text = st;
                connectionStatusBadgeText.color = colConnecting;
                connectionStatusDotImage.color = colConnecting;
            }
        }

        if (topRightPlayerNameText != null && photonMgr != null && !string.IsNullOrEmpty(photonMgr.playerName))
        {
            topRightPlayerNameText.text = $"👤 <color=#00E5FF>{photonMgr.playerName}</color>";
        }

        // 2. Nếu trận đấu ĐÃ BẮT ĐẦU (sau khi đủ 4 người vào phòng hoặc bấm Test):
        if (isMatchRunning)
        {
            if (isDirectMatchmakingActive)
            {
                isDirectMatchmakingActive = false;
                if (matchmakingSpinnerOverlayObj != null) matchmakingSpinnerOverlayObj.SetActive(false);
            }
            HideImmediatelyForMatchStart();
            return;
        }

        // 3. Khi trận đấu CHƯA BẮT ĐẦU (Đang ở Sảnh Chờ hoặc Phòng Chờ đợi đủ 4 người):
        // Khóa cứng game, giữ chuột hiển thị tự do 100%
        if (lobbyRootObj != null && !lobbyRootObj.activeSelf)
        {
            lobbyRootObj.SetActive(true);
        }
        if (inGameRoomBannerObj != null && inGameRoomBannerObj.activeSelf)
        {
            inGameRoomBannerObj.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ThirdPersonCamera.isUIPanelOpen = true;

        if (isDirectMatchmakingActive)
        {
            UpdateDirectMatchmakingOverlay();
        }

        if (isInsideWaitingRoom)
        {
            UpdateWaitingRoomUI();
        }
    }

    private void UpdateDirectMatchmakingOverlay()
    {
        if (matchmakingSpinnerOverlayObj == null || !matchmakingSpinnerOverlayObj.activeSelf) return;

        // Xoay vòng tròn spinner liên tục
        if (matchmakingSpinnerArcImage != null)
        {
            matchmakingSpinnerArcImage.transform.Rotate(0f, 0f, -320f * Time.unscaledDeltaTime);
        }

        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        int sec = Mathf.FloorToInt(PhotonMultiplayerChatVoice.MatchmakingSearchDuration);
        string timeStr = $"{sec / 60:D2}:{sec % 60:D2}";
        if (matchmakingSpinnerTimerText != null)
        {
            matchmakingSpinnerTimerText.text = timeStr;
        }

        int count = photonMgr != null ? photonMgr.CurrentRoomPlayersCount : 1;
        float countdown = photonMgr != null ? photonMgr.RemainingCountdown : -1f;

        if (countdown >= 0f)
        {
            int remSec = Mathf.CeilToInt(countdown);
            if (matchmakingSpinnerTitleText != null)
            {
                matchmakingSpinnerTitleText.text = "⚡ ĐÃ TÌM THẤY TRẬN ĐẤU!";
                matchmakingSpinnerTitleText.color = new Color(0.25f, 1.0f, 0.58f, 1f);
            }
            if (matchmakingSpinnerCountText != null)
            {
                matchmakingSpinnerCountText.text = $"🎉 ĐỦ 4/4 NGƯỜI CHƠI! VÀO TRẬN TRONG: {remSec} GIÂY...";
            }
            if (matchmakingSpinnerCancelBtn != null && matchmakingSpinnerCancelBtn.gameObject.activeSelf)
            {
                matchmakingSpinnerCancelBtn.gameObject.SetActive(false);
            }
        }
        else
        {
            if (matchmakingSpinnerTitleText != null)
            {
                matchmakingSpinnerTitleText.text = "🔍 ĐANG TÌM TRẬN ĐẤU...";
                matchmakingSpinnerTitleText.color = new Color(0.35f, 1f, 0.85f, 1f);
            }
            if (matchmakingSpinnerCountText != null)
            {
                matchmakingSpinnerCountText.text = $"👥 Đã tìm thấy: <color=#2EE87A>{count} / 4</color> người chơi (Đủ 4 tự vào)";
            }
            if (matchmakingSpinnerCancelBtn != null && !matchmakingSpinnerCancelBtn.gameObject.activeSelf)
            {
                matchmakingSpinnerCancelBtn.gameObject.SetActive(true);
            }
        }
    }

    private void UpdateWaitingRoomUI()
    {
        var photonMgr = PhotonMultiplayerChatVoice.Instance;
        if (photonMgr != null && photonMgr.IsInPhotonRoom && !string.IsNullOrEmpty(photonMgr.roomName))
        {
            currentRoomId = photonMgr.roomName;
        }

        string displayRoomId = !string.IsNullOrEmpty(currentRoomId)
            ? currentRoomId
            : (photonMgr != null && !string.IsNullOrEmpty(photonMgr.roomName) ? photonMgr.roomName : "Đang tìm...");

        bool inRoom = photonMgr != null && photonMgr.IsInPhotonRoom;
        if (waitingRoomIdText != null)
        {
            string statusSuffix = inRoom ? "" : " <color=#FFD43B>(Đang vào phòng...)</color>";
            waitingRoomIdText.text = $"🚪 ID PHÒNG:  <color=#FFF275>{displayRoomId}</color>{statusSuffix}";
        }

        if (waitingPlayerNameInput != null && !waitingPlayerNameInput.isFocused && photonMgr != null)
        {
            if (waitingPlayerNameInput.text != photonMgr.playerName)
            {
                waitingPlayerNameInput.text = photonMgr.playerName;
            }
        }

        List<PhotonMultiplayerChatVoice.RoomPlayerEntry> players = photonMgr != null
            ? photonMgr.GetRoomPlayersList()
            : new List<PhotonMultiplayerChatVoice.RoomPlayerEntry>
            {
                new PhotonMultiplayerChatVoice.RoomPlayerEntry
                {
                    actorNumber = 1,
                    nickName = "NgườiChơi_Tôi",
                    isLocal = true,
                    isMaster = true
                }
            };

        int required = photonMgr != null ? Mathf.Max(1, photonMgr.requiredPlayersToStart) : 4;
        int currentCount = Mathf.Max(1, players.Count);

        if (waitingPlayerCountText != null)
        {
            string countHex = currentCount >= required ? "#2EE87A" : "#FFD43B";
            waitingPlayerCountText.text = $"👥 DANH SÁCH NGƯỜI CHƠI TRONG PHÒNG:  <color={countHex}>{currentCount} / {required}</color> NGƯỜI";
        }

        if (waitingProgressFill != null)
        {
            float ratio = Mathf.Clamp01(currentCount / (float)required);
            RectTransform rt = waitingProgressFill.rectTransform;
            rt.anchorMax = new Vector2(ratio, 1f);
        }

        // Cập nhật 4 thẻ vị trí người chơi trong phòng
        for (int i = 0; i < 4; i++)
        {
            var slot = playerSlots[i];
            if (slot.rootObj == null) continue;

            if (i < players.Count)
            {
                var p = players[i];
                if (p.isLocal)
                {
                    // Thẻ của BẠN (TÔI Ở ĐÂY)
                    slot.bgImage.color = new Color(0.11f, 0.18f, 0.28f, 0.98f);
                    slot.cardOutline.effectColor = new Color(1.00f, 0.82f, 0.22f, 0.98f);
                    slot.avatarBg.color = new Color(0.85f, 0.62f, 0.12f, 0.95f);
                    slot.avatarIcon.sprite = p.isMaster ? sprCardIcons[3] : sprCardIcons[0];
                    slot.avatarIcon.color = new Color(1f, 0.96f, 0.78f, 1f);

                    slot.slotBadgeText.text = GameLocalizationManager.T("WAITING_YOU", "⭐ BẠN ĐANG Ở ĐÂY (TÔI) ⭐");
                    slot.slotBadgeText.color = new Color(1f, 0.90f, 0.28f, 1f);

                    slot.playerNameText.text = $"{p.nickName} ({GameLocalizationManager.T("LOBBY_ME_PROFILE", "Bạn")})";
                    slot.playerNameText.color = new Color(0.35f, 1.0f, 0.72f, 1f);

                    string rolePrefix = p.isMaster ? GameLocalizationManager.T("WAITING_ROLE_HOST", "👑 Chủ Phòng") : GameLocalizationManager.T("WAITING_ROLE_MEMBER", "🟢 Thành Viên");
                    string readyTxt = p.isReady ? GameLocalizationManager.T("WAITING_READY_YES", "✔ Đã sẵn sàng") : GameLocalizationManager.T("WAITING_READY_NO", "⏳ Chưa sẵn sàng");
                    if (!inRoom)
                    {
                        slot.playerRoleStatusText.text = "⏳ " + GameLocalizationManager.T("LOBBY_CONN_CONNECTING", "Đang kết nối...");
                        slot.playerRoleStatusText.color = new Color(1.0f, 0.85f, 0.30f, 1f);
                    }
                    else
                    {
                        slot.playerRoleStatusText.text = $"{rolePrefix}  ●  {readyTxt}";
                        slot.playerRoleStatusText.color = p.isReady
                            ? new Color(0.25f, 0.98f, 0.62f, 1f)
                            : new Color(1.0f, 0.85f, 0.30f, 1f);
                    }
                }
                else
                {
                    // Thẻ của người chơi khác đã vào phòng
                    slot.bgImage.color = new Color(0.08f, 0.16f, 0.24f, 0.96f);
                    slot.cardOutline.effectColor = p.isReady
                        ? new Color(0.20f, 0.95f, 0.64f, 0.95f)
                        : new Color(0.35f, 0.60f, 0.85f, 0.85f);
                    slot.avatarBg.color = new Color(0.12f, 0.55f, 0.45f, 0.95f);
                    slot.avatarIcon.sprite = p.isMaster ? sprCardIcons[3] : sprCardIcons[0];
                    slot.avatarIcon.color = Color.white;

                    slot.slotBadgeText.text = p.isMaster
                        ? string.Format(GameLocalizationManager.T("WAITING_SLOT_HOST", "VỊ TRÍ #{0} (CHỦ PHÒNG)"), i + 1)
                        : string.Format(GameLocalizationManager.T("WAITING_SLOT_TEAMMATE", "VỊ TRÍ #{0} (ĐỒNG ĐỘI)"), i + 1);
                    slot.slotBadgeText.color = new Color(0.45f, 0.95f, 0.82f, 1f);

                    slot.playerNameText.text = p.nickName;
                    slot.playerNameText.color = Color.white;

                    slot.playerRoleStatusText.text = p.isReady
                        ? GameLocalizationManager.T("WAITING_READY_YES", "✔ Đã xác nhận sẵn sàng")
                        : GameLocalizationManager.T("WAITING_READY_NO", "🟢 Đã vào phòng (Chờ sẵn sàng)");
                    slot.playerRoleStatusText.color = p.isReady
                        ? new Color(0.25f, 0.98f, 0.62f, 1f)
                        : new Color(0.75f, 0.88f, 1.0f, 0.95f);
                }
            }
            else
            {
                // Vị trí còn trống đang chờ người vào
                slot.bgImage.color = new Color(0.05f, 0.07f, 0.13f, 0.88f);
                slot.cardOutline.effectColor = new Color(0.22f, 0.28f, 0.44f, 0.65f);
                slot.avatarBg.color = new Color(0.10f, 0.14f, 0.24f, 0.70f);
                slot.avatarIcon.sprite = sprCardIcons[4];
                slot.avatarIcon.color = new Color(0.45f, 0.52f, 0.68f, 0.45f);

                slot.slotBadgeText.text = string.Format(GameLocalizationManager.T("WAITING_SLOT_EMPTY", "VỊ TRÍ #{0} (CÒN TRỐNG)"), i + 1);
                slot.slotBadgeText.color = new Color(0.52f, 0.60f, 0.76f, 0.85f);

                bool isSearching = PhotonMultiplayerChatVoice.IsMatchmakingSearching;
                slot.playerNameText.text = isSearching
                    ? GameLocalizationManager.T("WAITING_SLOT_SEARCHING", "🔍 Đang ghép người...")
                    : GameLocalizationManager.T("WAITING_SLOT_WAITING", "Đang chờ người chơi...");
                slot.playerNameText.color = isSearching ? new Color(0.20f, 0.95f, 0.65f, 0.85f) : new Color(0.48f, 0.56f, 0.72f, 0.75f);

                slot.playerRoleStatusText.text = isSearching
                    ? "⏳ Đang quét người chơi lẻ trên server"
                    : $"⏳ Nhập ID [{displayRoomId}] hoặc bấm Tìm trận";
                slot.playerRoleStatusText.color = new Color(0.55f, 0.65f, 0.82f, 0.75f);
            }
        }

        // Cập nhật thông báo đếm ngược hoặc tìm trận
        if (waitingStatusBannerText != null)
        {
            float remCountdown = photonMgr != null ? photonMgr.RemainingCountdown : -1f;
            bool isSearching = PhotonMultiplayerChatVoice.IsMatchmakingSearching;
            int secDuration = Mathf.FloorToInt(PhotonMultiplayerChatVoice.MatchmakingSearchDuration);
            string timeStr = $"{secDuration / 60:D2}:{secDuration % 60:D2}";

            if (remCountdown >= 0f)
            {
                int sec = Mathf.CeilToInt(remCountdown);
                waitingStatusBannerText.text = $"🔥 ĐÃ GHÉP ĐỦ {required}/{required} NGƯỜI CHƠI! TRẬN ĐẤU BẮT ĐẦU TRONG: {sec} GIÂY! 🔥";
                waitingStatusBannerText.color = new Color(0.25f, 1.0f, 0.58f, 1f);
            }
            else if (isSearching)
            {
                if (currentCount <= 1)
                {
                    waitingStatusBannerText.text = $"🔍 ĐANG TÌM TRẬN ĐẤU [{timeStr}]... Đang quét tìm các người chơi đơn khác trên máy chủ Châu Á...";
                    waitingStatusBannerText.color = new Color(1.0f, 0.72f, 0.20f, 1f);
                }
                else
                {
                    waitingStatusBannerText.text = $"🔍 ĐANG TÌM TRẬN ĐẤU [{timeStr}]... Đã ghép: <color=#00FF9D>{currentCount}/{required} người</color> (Đang tìm thêm {required - currentCount} bạn lẻ)...";
                    waitingStatusBannerText.color = new Color(0.25f, 0.95f, 1.0f, 1f);
                }
            }
            else if (currentCount < required)
            {
                if (currentCount <= 1)
                {
                    waitingStatusBannerText.text = $"🎮 PHÒNG CHUẨN BỊ (ĐẤU ĐƠN): Bấm [⚔️ BẮT ĐẦU TÌM TRẬN] để tự động ghép cặp cùng 3 người chơi khác!";
                    waitingStatusBannerText.color = new Color(0.40f, 0.75f, 1.0f, 1f);
                }
                else
                {
                    bool allOthers = photonMgr != null && photonMgr.AreAllOtherPlayersReady();
                    if (photonMgr != null && photonMgr.IsLocalMaster)
                    {
                        waitingStatusBannerText.text = allOthers
                            ? $"⚡ TẤT CẢ ĐỒNG ĐỘI ĐÃ SẴN SÀNG! Đội trưởng hãy bấm [BẮT ĐẦU TÌM TRẬN] để ghép thêm {required - currentCount} bạn lẻ!"
                            : $"⏳ PHÒNG TỔ ĐỘI ({currentCount}/{required} NGƯỜI): Đang chờ đồng đội bấm ✔ SẴN SÀNG...";
                        waitingStatusBannerText.color = allOthers ? new Color(0.20f, 1.0f, 0.65f, 1f) : new Color(1.0f, 0.84f, 0.28f, 1f);
                    }
                    else
                    {
                        waitingStatusBannerText.text = $"🎮 PHÒNG TỔ ĐỘI ({currentCount}/{required} NGƯỜI): Hãy bấm ✔ SẴN SÀNG để Đội trưởng bắt đầu tìm trận!";
                        waitingStatusBannerText.color = new Color(0.35f, 0.95f, 1.0f, 1f);
                    }
                }
            }
            else
            {
                waitingStatusBannerText.text = $"🎉 ĐÃ ĐỦ {required}/{required} NGƯỜI TRONG PHÒNG! Bấm ✔ SẴN SÀNG để bắt đầu trận đấu ngay!";
                waitingStatusBannerText.color = new Color(0.25f, 1.0f, 0.58f, 1f);
            }
        }

        if (waitingForceStartBtnObj != null)
        {
            bool canForce = photonMgr == null || photonMgr.IsLocalMaster;
            waitingForceStartBtnObj.SetActive(canForce);
        }

        if (waitingReadyBtnText != null)
        {
            float remCountdown = photonMgr != null ? photonMgr.RemainingCountdown : -1f;
            bool isSearching = PhotonMultiplayerChatVoice.IsMatchmakingSearching;
            bool isMeReady = PhotonMultiplayerChatVoice.IsLocalPlayerReady;
            int secDuration = Mathf.FloorToInt(PhotonMultiplayerChatVoice.MatchmakingSearchDuration);
            string timeStr = $"{secDuration / 60:D2}:{secDuration % 60:D2}";

            if (remCountdown >= 0f)
            {
                waitingReadyBtnText.text = $"⚡ ĐANG VÀO TRẬN ({Mathf.CeilToInt(remCountdown)}s)...";
                if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.16f, 0.62f, 0.36f, 0.95f);
                if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(0.35f, 0.98f, 0.62f, 1f);
            }
            else if (isSearching)
            {
                if (photonMgr == null || photonMgr.IsLocalMaster || currentCount <= 1)
                {
                    waitingReadyBtnText.text = $"❌ ĐANG TÌM TRẬN [{timeStr}] (BẤM ĐỂ HỦY)";
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.85f, 0.26f, 0.18f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.0f, 0.45f, 0.35f, 1f);
                }
                else
                {
                    waitingReadyBtnText.text = $"⏳ ĐANG TÌM TRẬN CÙNG ĐỘI [{timeStr}]...";
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.85f, 0.55f, 0.15f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.0f, 0.75f, 0.30f, 1f);
                }
            }
            else if (currentCount >= required)
            {
                if (isMeReady)
                {
                    waitingReadyBtnText.text = GameLocalizationManager.T("WAITING_BTN_READY_CANCEL", "✔ BẠN ĐÃ SẴN SÀNG (Click để hủy)");
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.16f, 0.62f, 0.36f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(0.35f, 0.98f, 0.62f, 1f);
                }
                else
                {
                    waitingReadyBtnText.text = GameLocalizationManager.T("WAITING_BTN_READY_CONFIRM", "✔ BẤM ĐỂ XÁC NHẬN SẴN SÀNG (VÀO TRẬN)");
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.18f, 0.45f, 0.85f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(0.35f, 0.70f, 1.00f, 1f);
                }
            }
            else
            {
                // Chưa đủ 4 người
                if (currentCount <= 1)
                {
                    // Người chơi đơn 1 mình
                    waitingReadyBtnText.text = GameLocalizationManager.T("WAITING_BTN_START_SEARCH", "⚔️ BẮT ĐẦU TÌM TRẬN (SẴN SÀNG)");
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.95f, 0.52f, 0.10f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.00f, 0.75f, 0.25f, 1f);
                }
                else
                {
                    // Tổ đội (2 hoặc 3 người)
                    if (photonMgr != null && !photonMgr.IsLocalMaster)
                    {
                        // Thành viên trong đội
                        waitingReadyBtnText.text = isMeReady
                            ? GameLocalizationManager.T("WAITING_BTN_READY_CANCEL", "✔ ĐÃ SẴN SÀNG (Click để hủy)")
                            : GameLocalizationManager.T("WAITING_BTN_READY_CONFIRM", "✔ BẤM ĐỂ SẴN SÀNG");
                        if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = isMeReady ? new Color(0.16f, 0.62f, 0.36f, 0.95f) : new Color(0.18f, 0.45f, 0.85f, 0.95f);
                        if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = isMeReady ? new Color(0.35f, 0.98f, 0.62f, 1f) : new Color(0.35f, 0.70f, 1.00f, 1f);
                    }
                    else
                    {
                        // Chủ phòng tổ đội
                        bool allOthers = photonMgr != null && photonMgr.AreAllOtherPlayersReady();
                        if (allOthers)
                        {
                            waitingReadyBtnText.text = GameLocalizationManager.T("WAITING_BTN_START_SEARCH", "⚔️ BẮT ĐẦU TÌM TRẬN");
                            if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.95f, 0.52f, 0.10f, 0.95f);
                            if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.00f, 0.75f, 0.25f, 1f);
                        }
                        else
                        {
                            waitingReadyBtnText.text = GameLocalizationManager.T("WAITING_READY_NO", "⏳ CHỜ TỔ ĐỘI SẴN SÀNG...");
                            if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.35f, 0.40f, 0.50f, 0.95f);
                            if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(0.50f, 0.55f, 0.65f, 1f);
                        }
                    }
                }
            }
        }
    }

    private void HandleInGameShortcuts()
    {
        bool f4Pressed = false;
        bool f1Pressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.f4Key.wasPressedThisFrame) f4Pressed = true;
            if (kb.f1Key.wasPressedThisFrame) f1Pressed = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F4)) f4Pressed = true;
        if (Input.GetKeyDown(KeyCode.F1)) f1Pressed = true;
#endif

        if (f4Pressed)
        {
            OnLeaveWaitingRoomClicked();
        }
        else if (f1Pressed && Application.isEditor)
        {
            var net = PhotonMultiplayerChatVoice.Instance;
            bool inPhotonWaitingRoom = isInsideWaitingRoom && net != null && net.IsInPhotonRoom && !PhotonMultiplayerChatVoice.IsGameStarted;
            if (inPhotonWaitingRoom)
            {
                OnHostForceStartClicked();
            }
            else
            {
                bool lobbyInputFocused = (roomIdInputField != null && roomIdInputField.isFocused)
                    || (waitingPlayerNameInput != null && waitingPlayerNameInput.isFocused);
                if (!lobbyInputFocused)
                {
                    if (net == null)
                    {
                        GameObject go = new GameObject("Photon_Multiplayer_VoiceChat");
                        net = go.AddComponent<PhotonMultiplayerChatVoice>();
                    }
                    net.StartOfflineQuickTestWithTwoGhosts();
                }
            }
        }
    }

    // =========================================================================
    // 7. HỆ THỐNG ĐA NGÔN NGỮ (VI/EN/ES/ZH) & TAG 18+ BẢO HỘ PHÁP LÝ SẢNH CHỜ
    // =========================================================================

    private void BuildBottomLegalDisclaimer(Transform parent, Font font)
    {
        GameObject disGo = new GameObject("BottomLegalDisclaimer");
        disGo.transform.SetParent(parent, false);
        RectTransform drt = disGo.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(1f, 0f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, 4f);
        drt.sizeDelta = new Vector2(0f, 28f);

        Image dbg = disGo.AddComponent<Image>();
        dbg.color = new Color(0.04f, 0.06f, 0.10f, 0.88f);

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(disGo.transform, false);
        RectTransform trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 0f);
        trt.offsetMax = new Vector2(-20f, 0f);

        bottomDisclaimerText = txtGo.AddComponent<Text>();
        bottomDisclaimerText.font = font;
        bottomDisclaimerText.fontSize = 12;
        bottomDisclaimerText.alignment = TextAnchor.MiddleCenter;
        bottomDisclaimerText.color = new Color(0.72f, 0.78f, 0.85f, 0.90f);
        bottomDisclaimerText.supportRichText = true;
        bottomDisclaimerText.text = GameLocalizationManager.T("AGE_DISCLAIMER_FULL");
    }

    private void UpdateLanguageButtonLabel()
    {
        if (headerLanguageBtnText == null) return;
        var lang = GameLocalizationManager.CurrentLanguage;
        string flag = GameLocalizationManager.GetLanguageFlag(lang);
        string code = GameLocalizationManager.GetLanguageCode(lang);
        string name = GameLocalizationManager.GetLanguageNativeName(lang);
        headerLanguageBtnText.text = $"🌐 [{code}] {flag} {name}";
    }

    private void UpdateAllLobbyLocalizedTexts()
    {
        UpdateLanguageButtonLabel();

        if (lobbySettingsBtnText != null)
        {
            lobbySettingsBtnText.text = GameLocalizationManager.T("BTN_LOBBY_SETTINGS", "⚙️ CÀI ĐẶT");
        }

        if (headerSubtitleText != null)
        {
            headerSubtitleText.text = GameLocalizationManager.T("HEADER_SUBTITLE", "⚡  MÊ CUNG HUYỀN BÍ  ●  SINH TỒN TỔ ĐỘI 4 NGƯỜI  ⚡");
        }

        if (headerAgeRatingText != null)
        {
            headerAgeRatingText.text = GameLocalizationManager.T("AGE_TAG_PILL", "🔞 18+ MATURE");
        }

        if (bottomDisclaimerText != null)
        {
            bottomDisclaimerText.text = GameLocalizationManager.T("AGE_DISCLAIMER_FULL");
        }

        if (connectionStatusLabelText != null)
        {
            connectionStatusLabelText.text = GameLocalizationManager.T("LOBBY_CONN_STATUS_LABEL", "Trạng thái kết nối:");
        }

        if (roomIdLabelText != null)
        {
            roomIdLabelText.text = GameLocalizationManager.T("LOBBY_ROOM_ID_LABEL", "ID Phòng:");
        }

        if (roomIdInputPlaceholderText != null)
        {
            roomIdInputPlaceholderText.text = GameLocalizationManager.T("LOBBY_ROOM_INPUT_PH", "Nhập ID phòng 5 số...");
        }

        if (joinRoomBtnText != null)
        {
            joinRoomBtnText.text = GameLocalizationManager.T("BTN_JOIN_ROOM", "➔  Tham gia");
        }

        if (createRoomBtnText != null)
        {
            createRoomBtnText.text = GameLocalizationManager.T("BTN_CREATE_ROOM", "👑  Tạo phòng");
        }

        if (topRightPlayerNameText != null)
        {
            topRightPlayerNameText.text = GameLocalizationManager.T("LOBBY_ME_PROFILE", "👤 Tôi");
        }

        if (waitingHeaderTitleText != null)
        {
            waitingHeaderTitleText.text = GameLocalizationManager.T("WAITING_HEADER_TITLE", "🏰 SẢNH PHÒNG CHỜ TRẬN ĐẤU (4 NGƯỜI CHƠI) 🏰");
        }

        if (waitingCopyBtnText != null)
        {
            waitingCopyBtnText.text = GameLocalizationManager.T("WAITING_COPY_BTN", "📋 Sao chép ID");
        }

        if (waitingMyNameLabelText != null)
        {
            waitingMyNameLabelText.text = GameLocalizationManager.T("WAITING_MY_NAME_LABEL", "👤 Tên của bạn:");
        }

        if (waitingSaveNameBtnText != null)
        {
            waitingSaveNameBtnText.text = GameLocalizationManager.T("WAITING_SAVE_NAME_BTN", "✔ Lưu tên");
        }

        if (waitingLeaveBtnText != null)
        {
            waitingLeaveBtnText.text = GameLocalizationManager.T("WAITING_LEAVE_BTN", "⬅  Rời Phòng / Quay Lại Sảnh");
        }

        if (matchmakingSpinnerCancelBtnText != null)
        {
            matchmakingSpinnerCancelBtnText.text = GameLocalizationManager.T("SPINNER_CANCEL_BTN", "🛑 HỦY TÌM TRẬN");
        }

        if (matchmakingSpinnerTitleText != null)
        {
            matchmakingSpinnerTitleText.text = GameLocalizationManager.T("SPINNER_TITLE", "🔍 ĐANG TÌM TRẬN ĐẤU...");
        }

        if (tabMultiplayerText != null) tabMultiplayerText.text = GameLocalizationManager.T("LOBBY_TAB_MULTIPLAYER", "👥 Đa Người Chơi");
        if (tabSoloText != null) tabSoloText.text = GameLocalizationManager.T("LOBBY_TAB_SOLO", "👤 Chơi Đơn");
        if (tabSettingsText != null) tabSettingsText.text = GameLocalizationManager.T("LOBBY_TAB_SETTINGS", "⚙️ Cài Đặt");
        if (bottomConnectionStatusText != null) bottomConnectionStatusText.text = $"<color=#00FF9D>●</color> {GameLocalizationManager.T("LOBBY_STATUS_GOOD", "Trạng thái kết nối: Kết nối tốt")}";
        if (bottomSloganText != null) bottomSloganText.text = GameLocalizationManager.T("LOBBY_FOOTER_SLOGAN", "PLAY TOGETHER • HAVE FUN");

        for (int i = 0; i < 6; i++)
        {
            if (i < cardTitleTexts.Length && cardTitleTexts[i] != null)
            {
                cardTitleTexts[i].text = GameLocalizationManager.T($"LOBBY_CARD{i + 1}_TITLE");
            }
            if (i < cardSubtitleTexts.Length && cardSubtitleTexts[i] != null)
            {
                cardSubtitleTexts[i].text = GameLocalizationManager.T($"LOBBY_CARD{i + 1}_SUB");
            }
            if (i < cardBadgeTexts.Length && cardBadgeTexts[i] != null)
            {
                cardBadgeTexts[i].text = GameLocalizationManager.T($"CARD_{i + 1}_BADGE");
            }
            if (i < cardBtnTexts.Length && cardBtnTexts[i] != null)
            {
                cardBtnTexts[i].text = GameLocalizationManager.T($"LOBBY_CARD{i + 1}_BTN");
            }
        }

        UpdateWaitingRoomUI();
    }

    private void HandleLobbyLanguageChanged(GameLocalizationManager.Language lang)
    {
        UpdateAllLobbyLocalizedTexts();
    }
}
