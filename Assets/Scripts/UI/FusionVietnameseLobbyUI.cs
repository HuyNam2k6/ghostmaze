using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Fusion;

/// <summary>
/// Giao diện sảnh chờ kết nối đa người chơi tiếng Việt ma mị (Ảnh 2)
/// Tự động đồng bộ với FusionBootstrap, hiển thị ID Phòng khi tạo, hỗ trợ tham gia phòng và 6 chế độ chơi.
/// </summary>
public class FusionVietnameseLobbyUI : MonoBehaviour
{
    public static FusionVietnameseLobbyUI Instance { get; private set; }

    [Header("1. Tham Chiếu Fusion Bootstrap")]
    public FusionBootstrap bootstrap;

    [Header("2. Cấu Hình Hình Nền")]
    public string backgroundRelativePath = "Assets/Textures/UI/FusionLobbyBackground.jpg";
    public Texture2D backgroundTexture;

    // Các thành phần UI chính
    private Canvas lobbyCanvas;
    private GameObject lobbyRootObj;
    private CanvasGroup lobbyCanvasGroup;
    private Text connectionStatusBadgeText;
    private Image connectionStatusDotImage;
    private InputField roomIdInputField;
    private GameObject inGameRoomBannerObj;
    private Text inGameRoomBannerText;

    private readonly Color colDisconnected = new Color(0.95f, 0.35f, 0.35f, 1f);
    private readonly Color colConnecting   = new Color(1.00f, 0.75f, 0.20f, 1f);
    private readonly Color colConnected    = new Color(0.15f, 0.95f, 0.55f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeIfBootstrapExists()
    {
        var bs = FindAnyObjectByType<FusionBootstrap>();
        if (bs != null && FindAnyObjectByType<FusionVietnameseLobbyUI>() == null)
        {
            bs.gameObject.AddComponent<FusionVietnameseLobbyUI>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Ẩn bảng IMGUI tiếng Anh mặc định của Fusion, dùng giao diện tiếng Việt này thay thế
        FusionBootstrapDebugGUI.HideDefaultGUI = true;

        if (bootstrap == null)
        {
            bootstrap = GetComponent<FusionBootstrap>();
            if (bootstrap == null) bootstrap = FindAnyObjectByType<FusionBootstrap>();
        }

        EnsureBackgroundTextureLoaded();
        BuildModernVietnameseUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            FusionBootstrapDebugGUI.HideDefaultGUI = false;
        }
    }

    private void Start()
    {
        // Đồng bộ ID phòng ban đầu nếu có sẵn
        if (bootstrap != null && !string.IsNullOrEmpty(bootstrap.DefaultRoomName) && roomIdInputField != null)
        {
            roomIdInputField.text = bootstrap.DefaultRoomName;
        }
    }

    private void Update()
    {
        if (bootstrap == null) return;

        UpdateConnectionStatusUI();
        HandleInGameShortcuts();
    }

    // =========================================================================
    // 1. NẠP HÌNH NỀN HUYỀN BÍ (ẢNH 2)
    // =========================================================================
    private void EnsureBackgroundTextureLoaded()
    {
        if (backgroundTexture != null) return;

#if UNITY_EDITOR
        backgroundTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundRelativePath);
        if (backgroundTexture != null) return;
#endif

        string fullPath = Path.Combine(Application.dataPath, "Textures/UI/FusionLobbyBackground.jpg");
        if (File.Exists(fullPath))
        {
            byte[] fileData = File.ReadAllBytes(fullPath);
            backgroundTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            backgroundTexture.LoadImage(fileData);
        }
    }

    // =========================================================================
    // 2. KHỞI TẠO CANVAS & GIAO DIỆN UGUI CHUẨN 1920x1080 (THEO ẢNH 2)
    // =========================================================================
    private void BuildModernVietnameseUI()
    {
        // 1. Tạo Canvas
        GameObject cGo = new GameObject("Canvas_FusionVietnameseLobby");
        cGo.transform.SetParent(transform, false);
        lobbyCanvas = cGo.AddComponent<Canvas>();
        lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lobbyCanvas.sortingOrder = 40; // Nằm dưới chat/voice (order 50)

        CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystemExists();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        // Khung gốc chứa toàn bộ sảnh chờ
        lobbyRootObj = new GameObject("LobbyRoot");
        lobbyRootObj.transform.SetParent(lobbyCanvas.transform, false);
        RectTransform rootRect = lobbyRootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        lobbyCanvasGroup = lobbyRootObj.AddComponent<CanvasGroup>();

        // 2. Hình nền lớn phủ toàn màn hình (Ảnh 2)
        GameObject bgObj = new GameObject("LobbyBackground");
        bgObj.transform.SetParent(lobbyRootObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        RawImage bgImg = bgObj.AddComponent<RawImage>();
        if (backgroundTexture != null)
        {
            bgImg.texture = backgroundTexture;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.06f, 0.08f, 0.14f, 1f); // Màu nền tối dự phòng
        }

        // 3. Badge Trạng Thái Kết Nối (Góc trên bên trái, vị trí theo ảnh 2)
        BuildConnectionBadge(lobbyRootObj.transform, defaultFont);

        // 4. Thanh Nhập ID Phòng & Nút Tham Gia (Ở giữa phía trên, theo ảnh 2)
        BuildRoomBar(lobbyRootObj.transform, defaultFont);

        // 5. 6 Thẻ Bài Ma Thuật (Xếp hàng ngang ở nửa dưới, theo ảnh 2)
        BuildCardButtons(lobbyRootObj.transform, defaultFont);

        // 6. Thanh HUD ID Phòng Nhỏ Gọn Trong Game (Hiện khi đã vào trận)
        BuildInGameRoomHUD(lobbyCanvas.transform, defaultFont);
    }

    private void EnsureEventSystemExists()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(es);
        }
    }

    // =========================================================================
    // 3. BADGE TRẠNG THÁI KẾT NỐI (GÓC TRÊN BÊN TRÁI)
    // =========================================================================
    private void BuildConnectionBadge(Transform parent, Font font)
    {
        GameObject badgeObj = new GameObject("ConnectionBadge");
        badgeObj.transform.SetParent(parent, false);
        RectTransform bRect = badgeObj.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0f, 1f);
        bRect.anchorMax = new Vector2(0f, 1f);
        bRect.pivot = new Vector2(0f, 1f);
        bRect.anchoredPosition = new Vector2(70f, -50f);
        bRect.sizeDelta = new Vector2(360f, 65f);

        // Nút trong suốt bao quanh badge
        Image bBg = badgeObj.AddComponent<Image>();
        bBg.color = new Color(0.05f, 0.08f, 0.14f, 0.35f);

        // Chấm trạng thái phát sáng
        GameObject dotObj = new GameObject("StatusDot");
        dotObj.transform.SetParent(badgeObj.transform, false);
        RectTransform dRect = dotObj.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0f, 0.5f);
        dRect.anchorMax = new Vector2(0f, 0.5f);
        dRect.pivot = new Vector2(0.5f, 0.5f);
        dRect.anchoredPosition = new Vector2(250f, 0f);
        dRect.sizeDelta = new Vector2(16f, 16f);

        connectionStatusDotImage = dotObj.AddComponent<Image>();
        connectionStatusDotImage.color = colDisconnected;

        // Chữ hiển thị
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(badgeObj.transform, false);
        RectTransform tRect = txtObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(45f, 0f);
        tRect.offsetMax = new Vector2(-15f, 0f);

        connectionStatusBadgeText = txtObj.AddComponent<Text>();
        connectionStatusBadgeText.font = font;
        connectionStatusBadgeText.fontSize = 17;
        connectionStatusBadgeText.fontStyle = FontStyle.Bold;
        connectionStatusBadgeText.alignment = TextAnchor.MiddleLeft;
        connectionStatusBadgeText.color = Color.white;
        connectionStatusBadgeText.text = "Trạng thái kết nối:  Chưa kết nối";
    }

    // =========================================================================
    // 4. THANH NHẬP ID PHÒNG & NÚT THAM GIA (Ở GIỮA TRÊN)
    // =========================================================================
    private void BuildRoomBar(Transform parent, Font font)
    {
        GameObject barObj = new GameObject("RoomBarContainer");
        barObj.transform.SetParent(parent, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -235f);
        barRect.sizeDelta = new Vector2(980f, 80f);

        // 1. Tiêu đề ID Phòng (Bên trái)
        GameObject labelObj = new GameObject("RoomLabel");
        labelObj.transform.SetParent(barObj.transform, false);
        RectTransform lRect = labelObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = new Vector2(50f, 0f);
        lRect.sizeDelta = new Vector2(170f, 50f);

        Text lText = labelObj.AddComponent<Text>();
        lText.font = font;
        lText.fontSize = 20;
        lText.fontStyle = FontStyle.Bold;
        lText.alignment = TextAnchor.MiddleLeft;
        lText.color = new Color(1f, 0.95f, 0.85f, 0.95f);
        lText.text = "🚪  ID Phòng:";

        // 2. Ô InputField (Ở giữa)
        GameObject inputObj = new GameObject("RoomInputField");
        inputObj.transform.SetParent(barObj.transform, false);
        RectTransform inRect = inputObj.AddComponent<RectTransform>();
        inRect.anchorMin = new Vector2(0f, 0.5f);
        inRect.anchorMax = new Vector2(0f, 0.5f);
        inRect.pivot = new Vector2(0f, 0.5f);
        inRect.anchoredPosition = new Vector2(230f, 0f);
        inRect.sizeDelta = new Vector2(430f, 52f);

        Image inBg = inputObj.AddComponent<Image>();
        inBg.color = new Color(0.05f, 0.08f, 0.16f, 0.85f);
        Outline inOutline = inputObj.AddComponent<Outline>();
        inOutline.effectColor = new Color(0.35f, 0.45f, 0.85f, 0.65f);
        inOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Text nhập bên trong
        GameObject inTextObj = new GameObject("Text");
        inTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform itRect = inTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = new Vector2(15f, 0f);
        itRect.offsetMax = new Vector2(-15f, 0f);

        Text inText = inTextObj.AddComponent<Text>();
        inText.font = font;
        inText.fontSize = 20;
        inText.fontStyle = FontStyle.Bold;
        inText.alignment = TextAnchor.MiddleLeft;
        inText.color = new Color(1f, 0.90f, 0.35f, 1f); // Màu vàng kim rực rỡ
        inText.supportRichText = false;

        // Placeholder
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(15f, 0f);
        phRect.offsetMax = new Vector2(-15f, 0f);

        Text phText = phObj.AddComponent<Text>();
        phText.font = font;
        phText.fontSize = 18;
        phText.fontStyle = FontStyle.Italic;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.55f, 0.65f, 0.85f, 0.55f);
        phText.text = "Nhập ID phòng...";

        roomIdInputField = inputObj.AddComponent<InputField>();
        roomIdInputField.textComponent = inText;
        roomIdInputField.placeholder = phText;
        roomIdInputField.characterLimit = 25;
        roomIdInputField.onValueChanged.AddListener(OnRoomIdInputChanged);

        // 3. Nút Tham Gia (Bên phải, màu tím phát sáng)
        GameObject joinBtnObj = new GameObject("JoinRoomButton");
        joinBtnObj.transform.SetParent(barObj.transform, false);
        RectTransform jbRect = joinBtnObj.AddComponent<RectTransform>();
        jbRect.anchorMin = new Vector2(1f, 0.5f);
        jbRect.anchorMax = new Vector2(1f, 0.5f);
        jbRect.pivot = new Vector2(1f, 0.5f);
        jbRect.anchoredPosition = new Vector2(-40f, 0f);
        jbRect.sizeDelta = new Vector2(195f, 54f);

        Image jbBg = joinBtnObj.AddComponent<Image>();
        jbBg.color = new Color(0.35f, 0.20f, 0.75f, 0.88f);
        Outline jbOutline = joinBtnObj.AddComponent<Outline>();
        jbOutline.effectColor = new Color(0.70f, 0.45f, 1.0f, 0.95f);
        jbOutline.effectDistance = new Vector2(2f, 2f);

        Button jbBtn = joinBtnObj.AddComponent<Button>();
        jbBtn.onClick.AddListener(OnJoinRoomClicked);

        GameObject jbTextObj = new GameObject("Text");
        jbTextObj.transform.SetParent(joinBtnObj.transform, false);
        RectTransform jbtRect = jbTextObj.AddComponent<RectTransform>();
        jbtRect.anchorMin = Vector2.zero;
        jbtRect.anchorMax = Vector2.one;
        jbtRect.offsetMin = Vector2.zero;
        jbtRect.offsetMax = Vector2.zero;

        Text jbText = jbTextObj.AddComponent<Text>();
        jbText.font = font;
        jbText.fontSize = 19;
        jbText.fontStyle = FontStyle.Bold;
        jbText.alignment = TextAnchor.MiddleCenter;
        jbText.color = Color.white;
        jbText.text = "➔ Tham gia";
    }

    // =========================================================================
    // 5. 6 THẺ BÀI MA THUẬT (XẾP HÀNG NGANG Ở NỬA DƯỚI)
    // =========================================================================
    private void BuildCardButtons(Transform parent, Font font)
    {
        // Khung chứa 6 thẻ bài
        GameObject cardsContainer = new GameObject("CardsContainer");
        cardsContainer.transform.SetParent(parent, false);
        RectTransform cRect = cardsContainer.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(0f, -145f);
        cRect.sizeDelta = new Vector2(1740f, 450f);

        // Vị trí tâm X của 6 thẻ bài (đối xứng qua tâm, khớp với tranh vẽ trong ảnh 2)
        float[] cardXPositions = new float[] { -670f, -400f, -135f, 135f, 400f, 670f };
        Color[] cardNeonColors = new Color[]
        {
            new Color(0.20f, 0.55f, 0.95f, 0.65f), // 1. Xanh dương
            new Color(0.15f, 0.85f, 0.70f, 0.65f), // 2. Xanh ngọc
            new Color(0.65f, 0.35f, 0.95f, 0.65f), // 3. Tím
            new Color(1.00f, 0.80f, 0.20f, 0.85f), // 4. Vàng hoàng gia (vương miện)
            new Color(0.25f, 0.75f, 0.95f, 0.65f), // 5. Xanh biển
            new Color(0.85f, 0.35f, 0.95f, 0.65f)  // 6. Tím neon
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
            CreateInteractiveCardOverlay(cardsContainer.transform, cardXPositions[i], cardNeonColors[i], cardActions[i], i + 1);
        }
    }

    private void CreateInteractiveCardOverlay(Transform parent, float posX, Color neonColor, Action onClickAction, int cardIndex)
    {
        GameObject cardObj = new GameObject($"CardButton_{cardIndex}");
        cardObj.transform.SetParent(parent, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(posX, 0f);
        cardRect.sizeDelta = new Vector2(250f, 430f);

        // Nền tương tác trong suốt
        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = new Color(0f, 0f, 0f, 0.01f); // Gần như trong suốt để lộ ảnh gốc tuyệt đẹp

        // Viền neon phát sáng khi hover
        Outline cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0f); // Mặc định ẩn
        cardOutline.effectDistance = new Vector2(3f, 3f);

        Button btn = cardObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClickAction?.Invoke());

        // Thêm hiệu ứng Hover phóng to nhẹ và sáng viền
        EventTrigger trigger = cardObj.AddComponent<EventTrigger>();

        // Pointer Enter
        EventTrigger.Entry entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) =>
        {
            cardRect.localScale = Vector3.one * 1.035f;
            cardOutline.effectColor = neonColor;
            cardImg.color = new Color(neonColor.r, neonColor.g, neonColor.b, 0.12f);
        });
        trigger.triggers.Add(entryEnter);

        // Pointer Exit
        EventTrigger.Entry entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            cardRect.localScale = Vector3.one;
            cardOutline.effectColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0f);
            cardImg.color = new Color(0f, 0f, 0f, 0.01f);
        });
        trigger.triggers.Add(entryExit);
    }

    // =========================================================================
    // 6. THANH HUD ID PHÒNG NHỎ GỌN TRONG GAME (HIỂN THỊ KHI ĐÃ VÀO TRẬN)
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
        hRect.sizeDelta = new Vector2(340f, 48f);

        Image hBg = inGameRoomBannerObj.AddComponent<Image>();
        hBg.color = new Color(0.04f, 0.07f, 0.12f, 0.85f);
        Outline hOutline = inGameRoomBannerObj.AddComponent<Outline>();
        hOutline.effectColor = new Color(0.85f, 0.70f, 0.20f, 0.85f);
        hOutline.effectDistance = new Vector2(1.5f, 1.5f);

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
        inGameRoomBannerText.fontSize = 13;
        inGameRoomBannerText.fontStyle = FontStyle.Bold;
        inGameRoomBannerText.alignment = TextAnchor.MiddleCenter;
        inGameRoomBannerText.color = new Color(1f, 0.90f, 0.35f, 1f);
        inGameRoomBannerText.text = "🚪 ID Phòng: Đang kết nối... (Click sao chép)";

        inGameRoomBannerObj.SetActive(false);
    }

    // =========================================================================
    // 7. XỬ LÝ SỰ KIỆN KHI BẤM NÚT & SINH ID PHÒNG NGẪU NHIÊN KHI TẠO PHÒNG
    // =========================================================================
    private void OnRoomIdInputChanged(string val)
    {
        if (bootstrap != null)
        {
            bootstrap.DefaultRoomName = val.Trim();
        }
    }

    /// <summary>
    /// Đảm bảo luôn có ID phòng rõ ràng. Nếu người chơi chưa nhập thì tự sinh mã 5 số ngẫu nhiên (VD 21234)
    /// và hiển thị ngay lập tức lên ô ID phòng để người chơi nhìn thấy!
    /// </summary>
    private string EnsureRoomIdGenerated()
    {
        string currentId = roomIdInputField != null ? roomIdInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(currentId))
        {
            if (bootstrap != null && !string.IsNullOrEmpty(bootstrap.DefaultRoomName))
            {
                currentId = bootstrap.DefaultRoomName.Trim();
            }
        }

        if (string.IsNullOrEmpty(currentId))
        {
            // Sinh mã số ID ngẫu nhiên 5 chữ số
            currentId = UnityEngine.Random.Range(10000, 99999).ToString();
            Debug.Log($"<color=#00FF88>[Lobby] Đã tự động tạo ID Phòng ngẫu nhiên: {currentId}</color>");
        }

        // Cập nhật ngay lên giao diện và bootstrap
        if (roomIdInputField != null) roomIdInputField.text = currentId;
        if (bootstrap != null) bootstrap.DefaultRoomName = currentId;

        return currentId;
    }

    public void OnJoinRoomClicked()
    {
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#00FF88>[Lobby] Tham gia phòng ID: {rid}</color>");
        if (bootstrap != null) bootstrap.StartClient();
    }

    public void OnCard1_StartSinglePlayer()
    {
        Debug.Log("<color=#3498DB>[Lobby] Bắt đầu chế độ một người chơi (Single Player)</color>");
        if (bootstrap != null) bootstrap.StartSinglePlayer();
    }

    public void OnCard2_StartSharedClient()
    {
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#1ABC9C>[Lobby] Bắt đầu chế độ nhiều người chơi (Shared Client) - ID Phòng: {rid}</color>");
        if (bootstrap != null) bootstrap.StartSharedClient();
    }

    public void OnCard3_StartServer()
    {
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#9B59B6>[Lobby] Bắt đầu máy chủ (Server) - ID Phòng: {rid}</color>");
        if (bootstrap != null) bootstrap.StartServer();
    }

    public void OnCard4_StartHost()
    {
        // Khi nhấn tạo phòng: Tự sinh mã ID phòng (nếu trống) và hiển thị to rõ lên ô ID phòng!
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#F1C40F>[Lobby] BẮT ĐẦU LÀM CHỦ PHÒNG (Host) - Đã tạo ID Phòng: {rid}</color>");
        if (bootstrap != null) bootstrap.StartHost();
    }

    public void OnCard5_StartClient()
    {
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#3498DB>[Lobby] Bắt đầu làm khách (Client) - Tham gia ID: {rid}</color>");
        if (bootstrap != null) bootstrap.StartClient();
    }

    public void OnCard6_StartAutoClient()
    {
        string rid = EnsureRoomIdGenerated();
        Debug.Log($"<color=#E056FD>[Lobby] Tự động làm chủ hoặc làm khách - ID Phòng: {rid}</color>");
        if (bootstrap != null) bootstrap.StartAutoClient();
    }

    public void CopyRoomIdToClipboard()
    {
        string rid = bootstrap != null ? bootstrap.DefaultRoomName : "";
        if (!string.IsNullOrEmpty(rid))
        {
            GUIUtility.systemCopyBuffer = rid;
            if (inGameRoomBannerText != null)
            {
                inGameRoomBannerText.text = $"📋 ĐÃ SAO CHÉP ID: {rid}!";
            }
        }
    }

    // =========================================================================
    // 8. CẬP NHẬT TRẠNG THÁI KẾT NỐI & ẨN/HIỆN KHI VÀO GAME
    // =========================================================================
    private void UpdateConnectionStatusUI()
    {
        bool isConnected = bootstrap.CurrentStage == FusionBootstrap.Stage.AllConnected;
        bool isStarting = !isConnected && bootstrap.CurrentStage != FusionBootstrap.Stage.Disconnected;

        // Cập nhật text trạng thái góc trên bên trái
        if (connectionStatusBadgeText != null)
        {
            if (isConnected)
            {
                connectionStatusBadgeText.text = "Trạng thái kết nối:  ● Đã kết nối";
                connectionStatusBadgeText.color = colConnected;
            }
            else if (isStarting)
            {
                connectionStatusBadgeText.text = "Trạng thái kết nối:  ● Đang kết nối...";
                connectionStatusBadgeText.color = colConnecting;
            }
            else
            {
                connectionStatusBadgeText.text = "Trạng thái kết nối:  ● Chưa kết nối";
                connectionStatusBadgeText.color = colDisconnected;
            }
        }

        if (connectionStatusDotImage != null)
        {
            connectionStatusDotImage.color = isConnected ? colConnected : (isStarting ? colConnecting : colDisconnected);
        }

        // Tự động ẩn sảnh chờ khi đã vào game và hiện HUD ID phòng góc màn hình
        if (isConnected)
        {
            if (lobbyRootObj != null && lobbyRootObj.activeSelf)
            {
                lobbyRootObj.SetActive(false);
            }
            if (inGameRoomBannerObj != null && !inGameRoomBannerObj.activeSelf)
            {
                inGameRoomBannerObj.SetActive(true);
            }
            if (inGameRoomBannerText != null)
            {
                string rName = !string.IsNullOrEmpty(bootstrap.DefaultRoomName) ? bootstrap.DefaultRoomName : "Mặc định";
                inGameRoomBannerText.text = $"🚪 ID Phòng: <b>{rName}</b>  ●  [F4: Thoát]";
            }
        }
        else
        {
            if (lobbyRootObj != null && !lobbyRootObj.activeSelf)
            {
                lobbyRootObj.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (inGameRoomBannerObj != null && inGameRoomBannerObj.activeSelf)
            {
                inGameRoomBannerObj.SetActive(false);
            }
        }
    }

    private void HandleInGameShortcuts()
    {
        // Phím F4 để rời phòng / ngắt kết nối khi đang trong trận
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f4Key.wasPressedThisFrame)
        {
            DisconnectAndReturnToLobby();
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F4))
        {
            DisconnectAndReturnToLobby();
        }
#endif
    }

    public void DisconnectAndReturnToLobby()
    {
        if (bootstrap != null)
        {
            Debug.Log("<color=yellow>[Lobby] Đã ngắt kết nối và quay trở lại sảnh chờ.</color>");
            bootstrap.ShutdownAll();
        }
    }
}
