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
    }

    private void OnDestroy()
    {
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
        sprBackdropSky = CreateGothicNightSkySprite(512, 256);
        sprCardGradient = CreateVerticalCardGradientSprite(64, 256);
        sprCircleBadge = CreateGlowingCircleSprite(64);
        sprLinkIcon = CreateChainLinkIconSprite(64);
        sprDoorIcon = CreatePortalDoorIconSprite(64);
        sprGraveyardBottom = CreateGraveyardSilhouetteSprite(160, 48);

        for (int i = 0; i < 6; i++)
        {
            sprCardIcons[i] = CreateModeIconSprite(i, 128);
        }
        sprSpinnerArc = CreateSpinnerArcSprite(128);
    }

    private Sprite CreateSpinnerArcSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.46f;
        float innerR = size * 0.30f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float dist = Vector2.Distance(p, center);
                if (dist >= innerR && dist <= outerR)
                {
                    float angle = (Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg + 360f) % 360f;
                    if (angle < 280f)
                    {
                        float alpha = Mathf.Clamp01(angle / 280f);
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

        // Mặt trăng ở góc trên bên phải (giống bố cục thiết kế)
        Vector2 moonCenter = new Vector2(w * 0.72f, h * 0.82f);
        float moonRadius = h * 0.16f;

        Color topSky = new Color(0.04f, 0.06f, 0.14f, 0.95f);
        Color midSky = new Color(0.09f, 0.08f, 0.22f, 0.94f);
        Color botMist = new Color(0.05f, 0.08f, 0.16f, 0.97f);

        for (int y = 0; y < h; y++)
        {
            float ty = y / (float)(h - 1);
            Color baseRow = ty > 0.45f
                ? Color.Lerp(midSky, topSky, (ty - 0.45f) / 0.55f)
                : Color.Lerp(botMist, midSky, ty / 0.45f);

            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);
                Color c = baseRow;

                // Hào quang mặt trăng
                float distMoon = Vector2.Distance(new Vector2(x, y), moonCenter);
                if (distMoon < moonRadius)
                {
                    float edge = Mathf.Clamp01((moonRadius - distMoon) / 2.5f);
                    float crater = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.12f;
                    Color moonCol = new Color(0.88f - crater, 0.91f - crater, 0.98f, 0.96f);
                    c = Color.Lerp(c, moonCol, edge * 0.85f);
                }
                else if (distMoon < moonRadius * 3.2f)
                {
                    float glow = Mathf.Pow(1f - (distMoon - moonRadius) / (moonRadius * 2.2f), 2.2f);
                    c += new Color(0.22f, 0.28f, 0.55f, 0f) * (glow * 0.45f);
                }

                // Sương mù ma mị ở nửa dưới
                if (ty < 0.38f)
                {
                    float mistNoise = Mathf.PerlinNoise(tx * 6f + 1.3f, ty * 10f + 2.7f);
                    float mistStrength = (1f - ty / 0.38f) * mistNoise * 0.18f;
                    c += new Color(0.18f, 0.14f, 0.35f, 0f) * mistStrength;
                }

                // Vignette 4 góc tối sâu
                float vx = (tx - 0.5f) * 2f;
                float vy = (ty - 0.5f) * 2f;
                float vig = Mathf.Clamp01((vx * vx + vy * vy) * 0.35f);
                c.r *= (1f - vig * 0.45f);
                c.g *= (1f - vig * 0.45f);
                c.b *= (1f - vig * 0.35f);

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

        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            // Đậm ở giữa, sáng nhẹ ở đỉnh và đáy để nhân màu với màu chủ đạo của từng thẻ
            float brightness = Mathf.Lerp(0.55f, 0.92f, Mathf.Abs(t - 0.45f) * 1.4f);
            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);
                float edgeGlow = Mathf.Pow(Mathf.Abs(tx - 0.5f) * 2f, 3f) * 0.25f;
                float val = Mathf.Clamp01(brightness + edgeGlow);
                tex.SetPixel(x, y, new Color(val, val, val, 0.94f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateGlowingCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01((maxR - d) / 2.0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateChainLinkIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ClearTex(tex, size, size);
        // Vẽ 2 mắt xích lồng nhau nghiêng 45 độ
        DrawRotatedCapsuleRing(tex, size, new Vector2(size * 0.40f, size * 0.40f), size * 0.14f, size * 0.08f, 45f, size * 0.055f);
        DrawRotatedCapsuleRing(tex, size, new Vector2(size * 0.60f, size * 0.60f), size * 0.14f, size * 0.08f, 45f, size * 0.055f);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreatePortalDoorIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ClearTex(tex, size, size);
        // Vẽ khung cửa và cánh cửa mở hé
        DrawRectRing(tex, size, new Rect(size * 0.22f, size * 0.12f, size * 0.52f, size * 0.76f), size * 0.06f);
        DrawFilledRect(tex, size, new Rect(size * 0.26f, size * 0.14f, size * 0.34f, size * 0.72f), new Color(1f, 1f, 1f, 0.92f));
        // Tay nắm cửa tròn nhỏ
        DrawFilledCircle(tex, size, new Vector2(size * 0.52f, size * 0.50f), size * 0.04f, Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateGraveyardSilhouetteSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        ClearTex(tex, w, h);

        for (int x = 0; x < w; x++)
        {
            // Nền đất gồ ghề dưới đáy thẻ
            float groundH = 8f + Mathf.Sin(x * 0.08f) * 3f;
            // Hàng rào sắt nhọn và bia mộ
            bool isFenceSpike = (x % 12 >= 4 && x % 12 <= 6);
            float spikeH = isFenceSpike ? 20f : 11f;
            bool isRail = (x > 4 && x < w - 4);

            // Bia mộ thánh giá ở hai bên
            bool isTomb1 = (x >= 22 && x <= 36);
            bool isTomb2 = (x >= w - 38 && x <= w - 24);

            for (int y = 0; y < h; y++)
            {
                bool fill = false;
                if (y <= groundH) fill = true;
                else if (isFenceSpike && y <= spikeH) fill = true;
                else if (isRail && (Mathf.Abs(y - 14f) <= 1.2f)) fill = true;
                else if (isTomb1 && y <= 28f) fill = true;
                else if (isTomb2 && y <= 25f) fill = true;

                // Chữ thập trên đỉnh bia mộ
                if ((Mathf.Abs(x - 29) <= 2 && y >= 28 && y <= 38) || (Mathf.Abs(x - 29) <= 6 && Mathf.Abs(y - 34) <= 2))
                    fill = true;

                if (fill)
                {
                    tex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.07f, 0.88f));
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateModeIconSprite(int modeIndex, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ClearTex(tex, size, size);
        Color white = new Color(1f, 1f, 1f, 0.96f);

        switch (modeIndex)
        {
            case 0: // 1. Bắt đầu một người chơi: Hình người + Nút Play tròn góc phải dưới
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.45f, size * 0.52f), size * 0.36f, white);
                DrawFilledCircle(tex, size, new Vector2(size * 0.72f, size * 0.28f), size * 0.16f, white);
                DrawPlayTriangle(tex, size, new Vector2(size * 0.73f, size * 0.28f), size * 0.08f, Color.clear);
                break;

            case 1: // 2. Bắt đầu chế độ nhiều người chơi: Nhóm 3 người + Dấu cộng
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.26f, size * 0.54f), size * 0.25f, new Color(1f, 1f, 1f, 0.75f));
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.72f, size * 0.54f), size * 0.25f, new Color(1f, 1f, 1f, 0.75f));
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.48f, size * 0.50f), size * 0.34f, white);
                DrawFilledCircle(tex, size, new Vector2(size * 0.76f, size * 0.24f), size * 0.14f, white);
                DrawPlusSign(tex, size, new Vector2(size * 0.76f, size * 0.24f), size * 0.08f, size * 0.03f, Color.clear);
                break;

            case 2: // 3. Bắt đầu máy chủ: 3 thanh Server Rack + Bánh răng
                for (int r = 0; r < 3; r++)
                {
                    float ry = size * (0.68f - r * 0.20f);
                    DrawFilledRect(tex, size, new Rect(size * 0.18f, ry - size * 0.065f, size * 0.56f, size * 0.13f), white);
                    // Đèn LED nhỏ trên thanh server
                    DrawFilledCircle(tex, size, new Vector2(size * 0.26f, ry), size * 0.025f, Color.clear);
                    DrawFilledCircle(tex, size, new Vector2(size * 0.34f, ry), size * 0.025f, Color.clear);
                }
                DrawGearBadge(tex, size, new Vector2(size * 0.74f, size * 0.26f), size * 0.16f, white);
                break;

            case 3: // 4. Bắt đầu làm chủ phòng: Vương miện 5 đỉnh Hoàng Gia (Crown)
                DrawRoyalCrown(tex, size, white);
                break;

            case 4: // 5. Bắt đầu làm khách: Hình người + Dấu cộng bên phải
                DrawPersonSilhouette(tex, size, new Vector2(size * 0.42f, size * 0.50f), size * 0.36f, white);
                DrawPlusSign(tex, size, new Vector2(size * 0.76f, size * 0.52f), size * 0.13f, size * 0.045f, white);
                break;

            case 5: // 6. Tự động làm chủ hoặc làm khách: Vòng xoay 2 mũi tên đồng bộ
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
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystemExists();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

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

        // 2. Badge Trạng Thái Kết Nối (Góc trên bên trái, luôn hiển thị ở cả Sảnh Chính và Phòng Chờ)
        BuildTopLeftConnectionBadge(lobbyRootObj.transform, font);

        // 3. MÀN HÌNH 1: SẢNH CHÍNH (Thanh ID Phòng + Nút Tham Gia/Tạo Phòng + 6 Thẻ Bài)
        mainCardsViewObj = new GameObject("MainCardsView");
        mainCardsViewObj.transform.SetParent(lobbyRootObj.transform, false);
        RectTransform mvRect = mainCardsViewObj.AddComponent<RectTransform>();
        mvRect.anchorMin = Vector2.zero;
        mvRect.anchorMax = Vector2.one;
        mvRect.offsetMin = Vector2.zero;
        mvRect.offsetMax = Vector2.zero;

        BuildTopCenterRoomBar(mainCardsViewObj.transform, font);
        BuildSixGothicModeCards(mainCardsViewObj.transform, font);

        // 4. MÀN HÌNH 2: BÊN TRONG PHÒNG CHỜ (Waiting Room View - Hiển thị ID Phòng + 4 Người Chơi & Tôi Ở Đây)
        BuildWaitingRoomView(lobbyRootObj.transform, font);

        // 4.1. Overlay Vòng Xoay Tìm Trận Trực Tiếp (Không cần vào phòng chờ)
        BuildMatchmakingSpinnerOverlay(lobbyRootObj.transform, font);

        // 5. Thanh HUD ID Phòng Nhỏ Gọn Góc Trên Phải Khi Đã Bắt Đầu Trận Đấu
        BuildInGameRoomHUD(lobbyCanvas.transform, font);

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
    // 3. BADGE TRẠNG THÁI KẾT NỐI GÓC TRÊN BÊN TRÁI (THIẾT KẾ GOTHIC)
    // =========================================================================
    private void BuildTopLeftConnectionBadge(Transform parent, Font font)
    {
        GameObject badgeObj = new GameObject("ConnectionStatusBadge");
        badgeObj.transform.SetParent(parent, false);
        RectTransform bRect = badgeObj.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0f, 1f);
        bRect.anchorMax = new Vector2(0f, 1f);
        bRect.pivot = new Vector2(0f, 1f);
        bRect.anchoredPosition = new Vector2(65f, -45f);
        bRect.sizeDelta = new Vector2(450f, 68f);

        Image bBg = badgeObj.AddComponent<Image>();
        bBg.color = new Color(0.06f, 0.09f, 0.18f, 0.94f);
        Outline bOut1 = badgeObj.AddComponent<Outline>();
        bOut1.effectColor = new Color(0.32f, 0.44f, 0.78f, 0.85f);
        bOut1.effectDistance = new Vector2(2f, 2f);

        // Vòng tròn biểu tượng mắt xích bên trái
        GameObject iconCircle = new GameObject("LinkIconCircle");
        iconCircle.transform.SetParent(badgeObj.transform, false);
        RectTransform icRect = iconCircle.AddComponent<RectTransform>();
        icRect.anchorMin = new Vector2(0f, 0.5f);
        icRect.anchorMax = new Vector2(0f, 0.5f);
        icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.anchoredPosition = new Vector2(38f, 0f);
        icRect.sizeDelta = new Vector2(48f, 48f);

        Image icBg = iconCircle.AddComponent<Image>();
        icBg.sprite = sprCircleBadge;
        icBg.color = new Color(0.14f, 0.26f, 0.58f, 0.95f);

        GameObject linkIcon = new GameObject("LinkIcon");
        linkIcon.transform.SetParent(iconCircle.transform, false);
        RectTransform liRect = linkIcon.AddComponent<RectTransform>();
        liRect.anchorMin = Vector2.zero;
        liRect.anchorMax = Vector2.one;
        liRect.offsetMin = new Vector2(8f, 8f);
        liRect.offsetMax = new Vector2(-8f, -8f);
        Image liImg = linkIcon.AddComponent<Image>();
        liImg.sprite = sprLinkIcon;
        liImg.color = new Color(0.55f, 0.82f, 1.0f, 1f);

        // Chấm tròn đèn trạng thái
        GameObject dotObj = new GameObject("StatusDot");
        dotObj.transform.SetParent(badgeObj.transform, false);
        RectTransform dRect = dotObj.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0f, 0.5f);
        dRect.anchorMax = new Vector2(0f, 0.5f);
        dRect.pivot = new Vector2(0.5f, 0.5f);
        dRect.anchoredPosition = new Vector2(266f, 0f);
        dRect.sizeDelta = new Vector2(16f, 16f);

        connectionStatusDotImage = dotObj.AddComponent<Image>();
        connectionStatusDotImage.sprite = sprCircleBadge;
        connectionStatusDotImage.color = colConnected;

        // Nhãn "Trạng thái kết nối:"
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(badgeObj.transform, false);
        RectTransform lRect = lblObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0f);
        lRect.anchorMax = new Vector2(0f, 1f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = new Vector2(78f, 0f);
        lRect.sizeDelta = new Vector2(180f, 0f);

        Text lblTxt = lblObj.AddComponent<Text>();
        lblTxt.font = font;
        lblTxt.fontSize = 18;
        lblTxt.fontStyle = FontStyle.Bold;
        lblTxt.alignment = TextAnchor.MiddleLeft;
        lblTxt.color = new Color(0.78f, 0.85f, 0.96f, 1f);
        lblTxt.text = "Trạng thái kết nối:";

        // Text trạng thái ("Đã kết nối" / "Đang kết nối...")
        GameObject valObj = new GameObject("StatusValueText");
        valObj.transform.SetParent(badgeObj.transform, false);
        RectTransform vRect = valObj.AddComponent<RectTransform>();
        vRect.anchorMin = new Vector2(0f, 0f);
        vRect.anchorMax = new Vector2(1f, 1f);
        vRect.offsetMin = new Vector2(282f, 0f);
        vRect.offsetMax = new Vector2(-12f, 0f);

        connectionStatusBadgeText = valObj.AddComponent<Text>();
        connectionStatusBadgeText.font = font;
        connectionStatusBadgeText.fontSize = 18;
        connectionStatusBadgeText.fontStyle = FontStyle.Bold;
        connectionStatusBadgeText.alignment = TextAnchor.MiddleLeft;
        connectionStatusBadgeText.color = colConnected;
        connectionStatusBadgeText.text = "Đã kết nối";
    }

    // =========================================================================
    // 4. THANH ID PHÒNG & NÚT THAM GIA / TẠO PHÒNG (Ở GIỮA PHÍA TRÊN)
    // =========================================================================
    private void BuildTopCenterRoomBar(Transform parent, Font font)
    {
        GameObject barObj = new GameObject("RoomIdBar");
        barObj.transform.SetParent(parent, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -210f);
        barRect.sizeDelta = new Vector2(1150f, 92f);

        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.06f, 0.08f, 0.16f, 0.95f);
        Outline barOut = barObj.AddComponent<Outline>();
        barOut.effectColor = new Color(0.45f, 0.36f, 0.82f, 0.90f);
        barOut.effectDistance = new Vector2(2.5f, 2.5f);

        // Viền phát sáng bên trong
        GameObject innerBorder = new GameObject("InnerTrim");
        innerBorder.transform.SetParent(barObj.transform, false);
        RectTransform ibRect = innerBorder.AddComponent<RectTransform>();
        ibRect.anchorMin = Vector2.zero;
        ibRect.anchorMax = Vector2.one;
        ibRect.offsetMin = new Vector2(6f, 6f);
        ibRect.offsetMax = new Vector2(-6f, -6f);
        Image ibImg = innerBorder.AddComponent<Image>();
        ibImg.color = new Color(0.09f, 0.12f, 0.24f, 0.65f);

        // 1. Icon Cánh Cửa + Chữ "ID Phòng:"
        GameObject doorIconObj = new GameObject("DoorIcon");
        doorIconObj.transform.SetParent(barObj.transform, false);
        RectTransform diRect = doorIconObj.AddComponent<RectTransform>();
        diRect.anchorMin = new Vector2(0f, 0.5f);
        diRect.anchorMax = new Vector2(0f, 0.5f);
        diRect.pivot = new Vector2(0.5f, 0.5f);
        diRect.anchoredPosition = new Vector2(54f, 0f);
        diRect.sizeDelta = new Vector2(46f, 46f);
        Image diImg = doorIconObj.AddComponent<Image>();
        diImg.sprite = sprDoorIcon;
        diImg.color = new Color(0.75f, 0.85f, 1f, 0.95f);

        GameObject labelObj = new GameObject("RoomIdLabel");
        labelObj.transform.SetParent(barObj.transform, false);
        RectTransform lRect = labelObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = new Vector2(92f, 0f);
        lRect.sizeDelta = new Vector2(140f, 54f);

        Text lTxt = labelObj.AddComponent<Text>();
        lTxt.font = font;
        lTxt.fontSize = 23;
        lTxt.fontStyle = FontStyle.Bold;
        lTxt.alignment = TextAnchor.MiddleLeft;
        lTxt.color = new Color(0.95f, 0.96f, 1f, 1f);
        lTxt.text = "ID Phòng:";

        // 2. Ô Nhập ID Phòng (InputField)
        GameObject inputObj = new GameObject("RoomInputField");
        inputObj.transform.SetParent(barObj.transform, false);
        RectTransform inRect = inputObj.AddComponent<RectTransform>();
        inRect.anchorMin = new Vector2(0f, 0.5f);
        inRect.anchorMax = new Vector2(0f, 0.5f);
        inRect.pivot = new Vector2(0f, 0.5f);
        inRect.anchoredPosition = new Vector2(235f, 0f);
        inRect.sizeDelta = new Vector2(455f, 56f);

        Image inBg = inputObj.AddComponent<Image>();
        inBg.color = new Color(0.03f, 0.04f, 0.09f, 0.96f);
        Outline inOut = inputObj.AddComponent<Outline>();
        inOut.effectColor = new Color(0.28f, 0.36f, 0.65f, 0.85f);
        inOut.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject inTextObj = new GameObject("Text");
        inTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform itRect = inTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = new Vector2(18f, 0f);
        itRect.offsetMax = new Vector2(-18f, 0f);

        Text inText = inTextObj.AddComponent<Text>();
        inText.font = font;
        inText.fontSize = 22;
        inText.fontStyle = FontStyle.Bold;
        inText.alignment = TextAnchor.MiddleLeft;
        inText.color = new Color(1f, 0.90f, 0.35f, 1f);
        inText.supportRichText = false;

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(18f, 0f);
        phRect.offsetMax = new Vector2(-18f, 0f);

        Text phText = phObj.AddComponent<Text>();
        phText.font = font;
        phText.fontSize = 20;
        phText.fontStyle = FontStyle.Italic;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.45f, 0.52f, 0.72f, 0.75f);
        phText.text = "Nhập ID phòng...";

        roomIdInputField = inputObj.AddComponent<InputField>();
        roomIdInputField.textComponent = inText;
        roomIdInputField.placeholder = phText;
        roomIdInputField.characterLimit = 25;
        roomIdInputField.onValueChanged.AddListener(OnRoomIdInputChanged);

        // 3. Nút "[ ➔ Tham gia ]" (Màu tím neon đúng thiết kế)
        CreateStyledActionButton(
            barObj.transform,
            "JoinRoomButton",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(710f, 0f),
            new Vector2(200f, 56f),
            new Color(0.38f, 0.20f, 0.85f, 0.96f),
            new Color(0.68f, 0.45f, 1.00f, 1f),
            "➔  Tham gia",
            font,
            21,
            OnJoinRoomClicked
        );

        // 4. Nút "[ 👑 Tạo phòng ]" (Màu vàng hoàng gia nổi bật ngay bên cạnh để tạo phòng & mở Phòng Chờ ngay lập tức)
        CreateStyledActionButton(
            barObj.transform,
            "CreateRoomButton",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(925f, 0f),
            new Vector2(200f, 56f),
            new Color(0.78f, 0.52f, 0.08f, 0.96f),
            new Color(1.00f, 0.85f, 0.30f, 1f),
            "👑  Tạo phòng",
            font,
            21,
            OnCreateRoomClicked
        );
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
    // 5. DỰNG 6 THẺ BÀI GOTHIC PHÁT SÁNG BẰNG UGUI THẬT (KHÔNG DÙNG ẢNH CHỤP)
    // =========================================================================
    private void BuildSixGothicModeCards(Transform parent, Font font)
    {
        GameObject cardsContainer = new GameObject("SixGothicCardsContainer");
        cardsContainer.transform.SetParent(parent, false);
        RectTransform cRect = cardsContainer.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(0f, -135f);
        cRect.sizeDelta = new Vector2(1760f, 480f);

        float[] cardX = new float[] { -685f, -412f, -140f, 140f, 412f, 685f };

        Color[] cardThemeColors = new Color[]
        {
            new Color(0.22f, 0.56f, 0.98f, 1f), // 1. Xanh dương (Một người chơi)
            new Color(0.12f, 0.86f, 0.72f, 1f), // 2. Xanh ngọc (Nhiều người chơi)
            new Color(0.62f, 0.32f, 0.98f, 1f), // 3. Tím đậm (Máy chủ)
            new Color(1.00f, 0.78f, 0.18f, 1f), // 4. Vàng hoàng gia (Làm chủ phòng / Tạo phòng)
            new Color(0.18f, 0.76f, 0.98f, 1f), // 5. Xanh băng (Làm khách)
            new Color(0.84f, 0.32f, 0.98f, 1f)  // 6. Tím hồng neon (Tự động làm chủ hoặc khách)
        };

        string[] cardTitles = new string[]
        {
            "Đấu Đơn / Ghép Trận\n(Tìm trận 4 người)",
            "Đấu Tổ Đội\n(Ghép bạn bè & người lẻ)",
            "Bắt đầu\nmáy chủ",
            "Bắt đầu\nlàm chủ phòng",
            "Bắt đầu\nlàm khách",
            "Tự động làm chủ\nhoặc làm khách"
        };

        Action[] cardActions = new Action[]
        {
            OnCard1_StartSinglePlayer,
            OnCard2_StartSharedClient,
            OnCard3_StartServer,
            OnCard4_StartHost,
            OnCard5_StartClient,
            OnCard6_StartAutoClient
        };

        for (int i = 0; i < 6; i++)
        {
            bool isRoyalHostCard = (i == 3);
            BuildSingleGothicCard(
                cardsContainer.transform,
                i,
                cardX[i],
                isRoyalHostCard ? new Vector2(266f, 460f) : new Vector2(248f, 425f),
                cardThemeColors[i],
                sprCardIcons[i],
                cardTitles[i],
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
        cardRect.anchoredPosition = new Vector2(posX, isRoyalCenterCard ? 12f : 0f);
        cardRect.sizeDelta = cardSize;

        // 1. Khung viền ngoài phát sáng Neon
        Image outerFrame = cardObj.AddComponent<Image>();
        outerFrame.color = new Color(themeColor.r * 0.85f, themeColor.g * 0.85f, themeColor.b * 0.85f, 0.92f);

        Outline glowOutline = cardObj.AddComponent<Outline>();
        glowOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.45f);
        glowOutline.effectDistance = new Vector2(3.5f, 3.5f);

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
        bodyImg.color = Color.Lerp(new Color(0.05f, 0.08f, 0.16f, 0.98f), themeColor, 0.18f);

        // 3. Đỉnh chóp Gothic phía trên thẻ (Crest ornament)
        GameObject topCrest = new GameObject("TopGothicCrest");
        topCrest.transform.SetParent(cardObj.transform, false);
        RectTransform tcRect = topCrest.AddComponent<RectTransform>();
        tcRect.anchorMin = new Vector2(0.5f, 1f);
        tcRect.anchorMax = new Vector2(0.5f, 1f);
        tcRect.pivot = new Vector2(0.5f, 0.5f);
        tcRect.anchoredPosition = new Vector2(0f, 4f);
        tcRect.sizeDelta = isRoyalCenterCard ? new Vector2(110f, 18f) : new Vector2(84f, 12f);
        Image tcImg = topCrest.AddComponent<Image>();
        tcImg.color = themeColor;

        // 4. Họa tiết bóng hàng rào & bia mộ dưới đáy thẻ
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

        // 5. Hào quang tròn mờ phía sau Icon
        GameObject iconHalo = new GameObject("IconHalo");
        iconHalo.transform.SetParent(innerBody.transform, false);
        RectTransform ihRect = iconHalo.AddComponent<RectTransform>();
        ihRect.anchorMin = new Vector2(0.5f, 0.5f);
        ihRect.anchorMax = new Vector2(0.5f, 0.5f);
        ihRect.pivot = new Vector2(0.5f, 0.5f);
        ihRect.anchoredPosition = new Vector2(0f, 62f);
        ihRect.sizeDelta = new Vector2(145f, 145f);
        Image ihImg = iconHalo.AddComponent<Image>();
        ihImg.sprite = sprCircleBadge;
        ihImg.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.18f);

        // 6. Icon Vector Sắc Nét ở giữa trên của thẻ
        GameObject iconObj = new GameObject("CardIcon");
        iconObj.transform.SetParent(innerBody.transform, false);
        RectTransform icRect = iconObj.AddComponent<RectTransform>();
        icRect.anchorMin = new Vector2(0.5f, 0.5f);
        icRect.anchorMax = new Vector2(0.5f, 0.5f);
        icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.anchoredPosition = new Vector2(0f, 62f);
        icRect.sizeDelta = isRoyalCenterCard ? new Vector2(118f, 118f) : new Vector2(106f, 106f);

        Image icImg = iconObj.AddComponent<Image>();
        icImg.sprite = iconSprite;
        icImg.color = Color.Lerp(themeColor, Color.white, 0.38f);

        // 7. Tiêu đề tiếng Việt trên thẻ
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(innerBody.transform, false);
        RectTransform ttRect = titleObj.AddComponent<RectTransform>();
        ttRect.anchorMin = new Vector2(0f, 0f);
        ttRect.anchorMax = new Vector2(1f, 0.5f);
        ttRect.offsetMin = new Vector2(14f, 52f);
        ttRect.offsetMax = new Vector2(-14f, -15f);

        Text ttTxt = titleObj.AddComponent<Text>();
        ttTxt.font = font;
        ttTxt.fontSize = isRoyalCenterCard ? 22 : 20;
        ttTxt.fontStyle = FontStyle.Bold;
        ttTxt.alignment = TextAnchor.MiddleCenter;
        ttTxt.lineSpacing = 1.12f;
        ttTxt.color = isRoyalCenterCard ? new Color(1f, 0.94f, 0.72f, 1f) : new Color(0.92f, 0.96f, 1f, 1f);
        ttTxt.text = titleText;

        Outline ttOut = titleObj.AddComponent<Outline>();
        ttOut.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ttOut.effectDistance = new Vector2(1.5f, -1.5f);

        // 8. Sự kiện Click & Hiệu ứng Hover sáng bừng
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
            outerFrame.color = new Color(themeColor.r * 0.85f, themeColor.g * 0.85f, themeColor.b * 0.85f, 0.92f);
            glowOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.45f);
            ihImg.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.18f);
        });
        trigger.triggers.Add(exit);
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
        hdTxt.text = "🏰 SẢNH PHÒNG CHỜ TRẬN ĐẤU (4 NGƯỜI CHƠI) 🏰";

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
        mnTxt.text = "👤 Tên của bạn:";

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

        CreateStyledActionButton(
            infoRow.transform,
            "ApplyNameBtn",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(1100f, 0f),
            new Vector2(155f, 48f),
            new Color(0.15f, 0.62f, 0.45f, 0.95f),
            new Color(0.30f, 0.95f, 0.68f, 1f),
            "✔ Lưu tên",
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
        CreateStyledActionButton(
            mainBox.transform,
            "LeaveRoomBtn",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(50f, 26f),
            new Vector2(340f, 60f),
            new Color(0.65f, 0.18f, 0.22f, 0.95f),
            new Color(1.00f, 0.40f, 0.45f, 1f),
            "⬅  Rời Phòng / Quay Lại Sảnh",
            font,
            20,
            OnLeaveWaitingRoomClicked
        );

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
            "🛑 HỦY TÌM TRẬN",
            font,
            16,
            CancelDirectMatchmaking
        );
        matchmakingSpinnerCancelBtn = cancelBtn;

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

                    slot.slotBadgeText.text = "⭐ BẠN ĐANG Ở ĐÂY (TÔI) ⭐";
                    slot.slotBadgeText.color = new Color(1f, 0.90f, 0.28f, 1f);

                    slot.playerNameText.text = $"{p.nickName} (Bạn)";
                    slot.playerNameText.color = new Color(0.35f, 1.0f, 0.72f, 1f);

                    string rolePrefix = p.isMaster ? "👑 Chủ Phòng" : "🟢 Thành Viên";
                    string readyTxt = p.isReady ? "✔ Đã sẵn sàng" : "⏳ Chưa sẵn sàng";
                    if (!inRoom)
                    {
                        slot.playerRoleStatusText.text = "⏳ Đang kết nối máy chủ phòng...";
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

                    slot.slotBadgeText.text = p.isMaster ? $"VỊ TRÍ #{i + 1} (CHỦ PHÒNG)" : $"VỊ TRÍ #{i + 1} (ĐỒNG ĐỘI)";
                    slot.slotBadgeText.color = new Color(0.45f, 0.95f, 0.82f, 1f);

                    slot.playerNameText.text = p.nickName;
                    slot.playerNameText.color = Color.white;

                    slot.playerRoleStatusText.text = p.isReady
                        ? "✔ Đã xác nhận sẵn sàng"
                        : "🟢 Đã vào phòng (Chờ sẵn sàng)";
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

                slot.slotBadgeText.text = $"VỊ TRÍ #{i + 1} (CÒN TRỐNG)";
                slot.slotBadgeText.color = new Color(0.52f, 0.60f, 0.76f, 0.85f);

                bool isSearching = PhotonMultiplayerChatVoice.IsMatchmakingSearching;
                slot.playerNameText.text = isSearching ? "🔍 Đang ghép người..." : "Đang chờ người chơi...";
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
                    waitingReadyBtnText.text = "✔ BẠN ĐÃ SẴN SÀNG (Click để hủy)";
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.16f, 0.62f, 0.36f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(0.35f, 0.98f, 0.62f, 1f);
                }
                else
                {
                    waitingReadyBtnText.text = "✔ BẤM ĐỂ XÁC NHẬN SẴN SÀNG (VÀO TRẬN)";
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
                    waitingReadyBtnText.text = "⚔️ BẮT ĐẦU TÌM TRẬN (SẴN SÀNG)";
                    if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.95f, 0.52f, 0.10f, 0.95f);
                    if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.00f, 0.75f, 0.25f, 1f);
                }
                else
                {
                    // Tổ đội (2 hoặc 3 người)
                    if (photonMgr != null && !photonMgr.IsLocalMaster)
                    {
                        // Thành viên trong đội
                        waitingReadyBtnText.text = isMeReady ? "✔ ĐÃ SẴN SÀNG (Click để hủy)" : "✔ BẤM ĐỂ SẴN SÀNG";
                        if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = isMeReady ? new Color(0.16f, 0.62f, 0.36f, 0.95f) : new Color(0.18f, 0.45f, 0.85f, 0.95f);
                        if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = isMeReady ? new Color(0.35f, 0.98f, 0.62f, 1f) : new Color(0.35f, 0.70f, 1.00f, 1f);
                    }
                    else
                    {
                        // Chủ phòng tổ đội
                        bool allOthers = photonMgr != null && photonMgr.AreAllOtherPlayersReady();
                        if (allOthers)
                        {
                            waitingReadyBtnText.text = $"⚔️ BẮT ĐẦU TÌM TRẬN (GHÉP THÊM {required - currentCount} BẠN)";
                            if (waitingReadyBtnBg != null) waitingReadyBtnBg.color = new Color(0.95f, 0.52f, 0.10f, 0.95f);
                            if (waitingReadyBtnOutline != null) waitingReadyBtnOutline.effectColor = new Color(1.00f, 0.75f, 0.25f, 1f);
                        }
                        else
                        {
                            waitingReadyBtnText.text = "⏳ CHỜ TỔ ĐỘI SẴN SÀNG...";
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
}
