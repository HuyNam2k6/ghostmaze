using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Realtime;
using Photon.Client;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hệ thống Multiplayer + Nhắn tin (Text Chat) + Đàm thoại giọng nói (Voice Chat) thời gian thực qua Photon Cloud.
/// Tự động khởi tạo khi vào Play Mode và kết nối tới phòng chung bằng App ID: 438b0f59-e83e-495b-9660-9bc8f0510c60.
/// </summary>
public class PhotonMultiplayerChatVoice : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks, IOnEventCallback
{
    public static PhotonMultiplayerChatVoice Instance { get; private set; }

    [Header("1. Cấu Hình Kết Nối Photon Cloud")]
    [Tooltip("App ID loại FUSION (dùng khi chưa có App ID Realtime). Server Fusion bắt buộc phòng phải dùng plugin Fusion.")]
    public string photonAppId = "438b0f59-e83e-495b-9660-9bc8f0510c60";
    [Tooltip("App ID loại VOICE / REALTIME riêng cho Chat & Voice-Mic để tránh lỗi Unsupported Plugin.")]
    public string photonRealtimeAppId = "d99a4e79-137c-4371-9663-34152e3e4a08";
    [Tooltip("Tên plugin của Photon Fusion 2 trên server (chỉ dùng khi kết nối bằng App ID Fusion)")]
    public string fusionPluginName = "FusionPlugin21";
    public string roomName = "GhostMaze_Online";
    public string playerName = "";
    public byte maxPlayersPerRoom = 16;

    [Header("2. Phím Tắt Chat & Voice")]
    public KeyCode toggleMicKey = KeyCode.V;
    public KeyCode openChatKey = KeyCode.Return;

    [Header("3. Chế Độ Phòng Chờ Đủ Người Mới Vào (Matchmaking Lobby)")]
    [Tooltip("Bật chế độ chờ đủ 4 người chơi mới mở phong ấn bắt đầu game")]
    public bool requireFullRoomToStart = false;
    [Tooltip("Số lượng người chơi cần thiết để bắt đầu game (mặc định 4 người)")]
    [Range(1, 16)] public int requiredPlayersToStart = 4;
    [Tooltip("Thời gian đếm ngược sau khi đủ người chơi (giây)")]
    [Range(2f, 15f)] public float countdownDuration = 5f;
    [Tooltip("Cho phép Chủ phòng (Host) bấm nút bắt đầu sớm nếu muốn test ngay")]
    public bool allowHostForceStart = true;

    [Header("4. Tùy Chọn Thu Gọn / Ẩn Thanh Chat")]
    [Tooltip("Phím tắt để ẩn/hiện thanh Chat")]
    public KeyCode toggleChatKey = KeyCode.H;
    [Tooltip("Khởi đầu với thanh Chat ở chế độ thu gọn")]
    public bool startChatMinimized = false;

    // Trạng thái Bắt đầu Game
    public static bool IsGameStarted { get; private set; } = false;
    public enum MatchState { WaitingForPlayers, CountingDown, GameStarted }
    public MatchState currentMatchState = MatchState.WaitingForPlayers;
    private float countdownTimer;
    private bool isChatMinimized = false;
    private bool hasUnreadChat = false;

    // Mã sự kiện mạng Photon (Event Codes)
    private const byte EVENT_CHAT_MSG = 101;
    private const byte EVENT_VOICE_PACKET = 102;
    private const byte EVENT_PLAYER_SYNC = 103;
    private const byte EVENT_MATCH_STATE = 104;

    private RealtimeClient client;
    private bool isConnectingOrConnected;
    private bool useFusionPlugin = false;
    private bool roomJoinBlocked = false; // Server từ chối phòng (sai loại App ID) -> ngừng thử lại liên tục
    private float reconnectTimer;

    // Trạng thái Chat
    private readonly List<string> chatMessages = new List<string>();
    private const int MAX_CHAT_LINES = 10;
    private bool isChatInputFocused;

    // Trạng thái Voice Chat (Microphone + Audio Ring Buffer)
    private bool isMicMuted = false;
    private float localSpeakingTimer = 0f;
    private float localVoiceSendHangoverTimer = 0f;
    public bool IsLocalSpeaking => Time.unscaledTime < localSpeakingTimer && !isMicMuted;
    private float remoteSpeakingTimer = 0f;
    private string remoteSpeakerName = "";
    private string activeMicDevice = null;
    private AudioClip micClip;
    private int lastMicSamplePos = 0;
    private const int VOICE_SAMPLE_RATE = 8000;
    private const int VOICE_CHUNK_SAMPLES = 800; // 100ms mỗi gói âm thanh (800 byte)
    private readonly float[] micSampleBuffer = new float[VOICE_CHUNK_SAMPLES];
    private readonly float[] clearSilenceBuffer = new float[400];

    private AudioSource voicePlaybackSource;
    private AudioClip playbackRingClip;
    private int playbackWritePos = 0;
    private const int PLAYBACK_RING_SAMPLES = VOICE_SAMPLE_RATE * 4; // Đệm 4 giây

    // Đồng bộ vị trí người chơi trong phòng
    private float nextSyncTime = 0f;
    private readonly Dictionary<int, RemotePlayerAvatar> remotePlayers = new Dictionary<int, RemotePlayerAvatar>();

    // Giao diện UI
    private Text statusBadgeText;
    private Text micButtonText;
    private Image micButtonBg;
    private Text speakingIndicatorText;
    private Text chatHistoryText;
    private InputField chatInputField;
    private GameObject chatInputRow;

    // Giao diện UI Thu Gọn Chat & Phòng Chờ
    private RectTransform chatPanelRect;
    private Text minimizeButtonText;
    private GameObject lobbyPanelObj;
    private Text lobbyTitleText;
    private Text lobbyRoomText;
    private Text lobbyCountText;
    private Text lobbyPlayerListText;
    private Text lobbyStatusText;
    private GameObject lobbyForceStartBtnObj;
    private GameObject gameStartBannerObj;
    private Text gameStartBannerText;
    private float gameStartBannerTimer = 0f;
    private readonly System.Text.StringBuilder lobbySb = new System.Text.StringBuilder();

    private class RemotePlayerAvatar
    {
        public GameObject rootObj;
        public Vector3 targetPos;
        public Quaternion targetRot;
        public float lastUpdateTime;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance == null && FindAnyObjectByType<PhotonMultiplayerChatVoice>() == null)
        {
            GameObject go = new GameObject("Photon_Multiplayer_VoiceChat");
            go.AddComponent<PhotonMultiplayerChatVoice>();
        }
    }

#if UNITY_EDITOR
    [MenuItem("Tools/GhostMaze/Tạo Photon Multiplayer & Voice-Chat Manager")]
    public static void CreateManagerInScene()
    {
        var existing = FindAnyObjectByType<PhotonMultiplayerChatVoice>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("<color=#00FF88>[Photon] Đã có sẵn Photon_Multiplayer_VoiceChat trong Scene!</color>");
            return;
        }
        GameObject go = new GameObject("Photon_Multiplayer_VoiceChat");
        go.AddComponent<PhotonMultiplayerChatVoice>();
        Undo.RegisterCreatedObjectUndo(go, "Create Photon Manager");
        Selection.activeGameObject = go;
        Debug.Log("<color=#00FF88>[Photon] Đã tạo Photon_Multiplayer_VoiceChat trong Scene thành công!</color>");
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Giữ kết nối Photon khi cửa sổ Game mất focus (tránh cảnh báo "SendOutgoingCommands() was not called for > 5 seconds")
        Application.runInBackground = true;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "NgườiChơi_" + UnityEngine.Random.Range(100, 999);
        }

        IsGameStarted = !requireFullRoomToStart;
        currentMatchState = requireFullRoomToStart ? MatchState.WaitingForPlayers : MatchState.GameStarted;
        isChatMinimized = startChatMinimized;

        SetupVoicePlayback();
        BuildChatAndVoiceUI();
        ConnectToPhoton();
    }

    private void Start()
    {
        StartMicrophoneCapture();
    }

    private void Update()
    {
        if (client != null)
        {
            client.Service();

            if (client.State == ClientState.Disconnected && !roomJoinBlocked && Time.unscaledTime >= reconnectTimer)
            {
                reconnectTimer = Time.unscaledTime + 5f;
                ConnectToPhoton();
            }
        }

        if (currentMatchState == MatchState.CountingDown)
        {
            countdownTimer -= Time.unscaledDeltaTime;
            UpdateLobbyUI();
            if (countdownTimer <= 0f)
            {
                StartGameLocally();
            }
        }

        if (gameStartBannerTimer > 0f)
        {
            gameStartBannerTimer -= Time.unscaledDeltaTime;
            if (gameStartBannerTimer <= 0f && gameStartBannerObj != null)
            {
                gameStartBannerObj.SetActive(false);
            }
        }

        // Tự động đồng bộ trạng thái focus chat khi người dùng click chuột ra ngoài hoặc vào ô chat
        if (chatInputField != null)
        {
            bool fieldFocused = chatInputField.isFocused;
            if (!fieldFocused && isChatInputFocused)
            {
                SetChatFocus(false);
            }
            else if (fieldFocused && !isChatInputFocused)
            {
                SetChatFocus(true);
            }
        }

        HandleShortcuts();
        ProcessMicrophoneAndSendVoice();
        UpdateRemotePlayersInterpolation();
        UpdateStatusUI();
    }

    private void OnDestroy()
    {
        IsGameStarted = false;
        if (client != null)
        {
            client.RemoveCallbackTarget(this);
            if (client.IsConnected)
            {
                client.Disconnect();
            }
            client = null;
        }
        if (!string.IsNullOrEmpty(activeMicDevice) && Microphone.IsRecording(activeMicDevice))
        {
            Microphone.End(activeMicDevice);
        }
        if (Instance == this) Instance = null;
    }

    // =========================================================================
    // 1. KẾT NỐI PHOTON CLOUD & VÀO PHÒNG TỰ ĐỘNG
    // =========================================================================
    public void ConnectToPhoton()
    {
        if (client == null)
        {
            client = new RealtimeClient(ConnectionProtocol.Udp);
            client.AddCallbackTarget(this);
        }

        if (client.IsConnected) return;

        client.LocalPlayer.NickName = playerName;

        bool hasRealtimeId = !string.IsNullOrWhiteSpace(photonRealtimeAppId);
        // App ID Fusion: server bắt buộc plugin Fusion -> gửi plugin ngay từ lần đầu (không còn lỗi "Unsupported Plugin")
        useFusionPlugin = !hasRealtimeId;

        AppSettings settings = new AppSettings
        {
            AppIdRealtime = hasRealtimeId ? photonRealtimeAppId.Trim() : null,
            AppIdFusion = hasRealtimeId ? null : photonAppId,
            AppIdVoice = hasRealtimeId ? photonRealtimeAppId.Trim() : photonAppId,
            AppIdChat = hasRealtimeId ? photonRealtimeAppId.Trim() : photonAppId,
            AppVersion = "1.0",
            UseNameServer = true,
            Protocol = ConnectionProtocol.Udp,
            AuthMode = AuthModeOption.Auth,
            EnableProtocolFallback = true,
            NetworkLogging = LogLevel.Error,
            ClientLogging = LogLevel.Warning
        };

        isConnectingOrConnected = client.ConnectUsingSettings(settings);
        AddSystemMessage("Đang kết nối tới máy chủ Photon Cloud...");
    }

    public bool IsTypingChat()
    {
        return isChatInputFocused || (chatInputField != null && chatInputField.isFocused);
    }

    [Obsolete]
    public void OnConnected() { }

    public void OnConnectedToMaster()
    {
        AddSystemMessage($"Đã kết nối Photon ({client.CurrentRegion})! Đang vào phòng '{roomName}'...");
        JoinOrCreateTargetRoom();
    }

    private void JoinOrCreateTargetRoom()
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        if (useFusionPlugin && !string.IsNullOrEmpty(fusionPluginName))
        {
            roomOptions.Plugins = new string[] { fusionPluginName };
        }

        EnterRoomArgs roomArgs = new EnterRoomArgs
        {
            RoomName = roomName,
            RoomOptions = roomOptions
        };
        client.OpJoinOrCreateRoom(roomArgs);
    }

    public void OnJoinedRoom()
    {
        int count = client.CurrentRoom != null ? client.CurrentRoom.PlayerCount : 1;
        AddSystemMessage($"Đã vào phòng [{roomName}] ({count}/{requiredPlayersToStart} người). Nhấn [Enter] để Chat, [V] để Mic, [H] để ẩn Chat!");
        CheckLobbyPlayerCount();
    }

    public void OnCreatedRoom() { }

    public void OnCreateRoomFailed(short returnCode, string message)
    {
        HandleRoomFailure("tạo", returnCode, message);
    }

    public void OnJoinRoomFailed(short returnCode, string message)
    {
        HandleRoomFailure("vào", returnCode, message);
    }

    private void HandleRoomFailure(string action, short returnCode, string message)
    {
        bool isPluginError = returnCode == ErrorCode.PluginMismatch || returnCode == ErrorCode.PluginReportedError
            || (!string.IsNullOrEmpty(message) && message.IndexOf("Plugin", StringComparison.OrdinalIgnoreCase) >= 0);

        if (isPluginError)
        {
            roomJoinBlocked = true;
            AddSystemMessage("Server Photon từ chối phòng Chat/Voice (App ID hiện tại là loại Fusion). " +
                             "Hãy tạo App ID loại REALTIME tại dashboard.photonengine.com và dán vào ô 'Photon Realtime App Id'.");
            if (client != null && client.IsConnected) client.Disconnect();
            return;
        }
        AddSystemMessage($"Lỗi {action} phòng ({returnCode}): {message}");
    }

    public void OnJoinRandomFailed(short returnCode, string message) { }

    public void OnLeftRoom()
    {
        ClearRemotePlayers();
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        IsGameStarted = !requireFullRoomToStart;
        currentMatchState = requireFullRoomToStart ? MatchState.WaitingForPlayers : MatchState.GameStarted;
    }

    public void OnDisconnected(DisconnectCause cause)
    {
        AddSystemMessage($"Mất kết nối Photon ({cause}). Tự động kết nối lại sau 5s...");
        reconnectTimer = Time.unscaledTime + 5f;
        ClearRemotePlayers();
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        IsGameStarted = !requireFullRoomToStart;
        currentMatchState = requireFullRoomToStart ? MatchState.WaitingForPlayers : MatchState.GameStarted;
    }

    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        string nick = !string.IsNullOrEmpty(newPlayer.NickName) ? newPlayer.NickName : $"NgườiChơi_{newPlayer.ActorNumber}";
        AddSystemMessage($"🟢 {nick} đã tham gia phòng!");
        CheckLobbyPlayerCount();
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        string nick = !string.IsNullOrEmpty(otherPlayer.NickName) ? otherPlayer.NickName : $"NgườiChơi_{otherPlayer.ActorNumber}";
        AddSystemMessage($"🔴 {nick} đã rời phòng.");

        if (remotePlayers.TryGetValue(otherPlayer.ActorNumber, out var av))
        {
            if (av.rootObj != null) Destroy(av.rootObj);
            remotePlayers.Remove(otherPlayer.ActorNumber);
        }
        CheckLobbyPlayerCount();
    }

    public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }
    public void OnMasterClientSwitched(Player newMasterClient) { }

    // =========================================================================
    // 2. NHẮN TIN TRỰC TUYẾN (TEXT CHAT) & ĐÀM THOẠI GIỌNG NÓI (VOICE CHAT)
    // =========================================================================
    private void HandleShortcuts()
    {
        bool isCurrentlyTyping = isChatInputFocused || (chatInputField != null && chatInputField.isFocused);

        bool enterPressed = false;
        bool escPressed = false;
        bool micTogglePressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        bool toggleChatPressed = false;
        if (kb != null)
        {
            enterPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            escPressed = kb.escapeKey.wasPressedThisFrame;
            micTogglePressed = kb.vKey != null && kb.vKey.wasPressedThisFrame;
            toggleChatPressed = kb.hKey != null && kb.hKey.wasPressedThisFrame;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!micTogglePressed) micTogglePressed = Input.GetKeyDown(toggleMicKey);
        if (!toggleChatPressed) toggleChatPressed = Input.GetKeyDown(toggleChatKey);
        if (!enterPressed) enterPressed = Input.GetKeyDown(openChatKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!escPressed) escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
#else
        enterPressed = Input.GetKeyDown(openChatKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
        escPressed = Input.GetKeyDown(KeyCode.Escape);
        micTogglePressed = Input.GetKeyDown(toggleMicKey);
        bool toggleChatPressed = Input.GetKeyDown(toggleChatKey);
#endif

        if (enterPressed)
        {
            if (isCurrentlyTyping)
            {
                SendChatMessageFromUI();
                SetChatFocus(false);
            }
            else
            {
                if (isChatMinimized)
                {
                    SetChatMinimized(false);
                }
                SetChatFocus(true);
            }
        }
        else if (isCurrentlyTyping && escPressed)
        {
            SetChatFocus(false);
        }
        else if (!isCurrentlyTyping && toggleChatPressed)
        {
            ToggleChatMinimized();
        }
        else if (!isCurrentlyTyping && micTogglePressed)
        {
            ToggleMicrophone();
        }
    }

    public void ToggleChatMinimized()
    {
        SetChatMinimized(!isChatMinimized);
    }

    public void SetChatMinimized(bool min)
    {
        isChatMinimized = min;
        if (isChatMinimized)
        {
            if (chatPanelRect != null) chatPanelRect.sizeDelta = new Vector2(340f, 38f);
            if (chatHistoryText != null) chatHistoryText.gameObject.SetActive(false);
            if (chatInputRow != null) chatInputRow.SetActive(false);
            if (speakingIndicatorText != null) speakingIndicatorText.gameObject.SetActive(false);
            if (minimizeButtonText != null) minimizeButtonText.text = "▲";
        }
        else
        {
            if (chatPanelRect != null) chatPanelRect.sizeDelta = new Vector2(410f, 225f);
            if (chatHistoryText != null) chatHistoryText.gameObject.SetActive(true);
            if (chatInputRow != null) chatInputRow.SetActive(true);
            if (speakingIndicatorText != null) speakingIndicatorText.gameObject.SetActive(true);
            if (minimizeButtonText != null) minimizeButtonText.text = "▼";
            hasUnreadChat = false;
        }
    }

    private void SetChatFocus(bool focused)
    {
        isChatInputFocused = focused;
        bool isDashboardOpen = GameSettingsManager.Instance != null
            && GameSettingsManager.Instance.dashboardPanel != null
            && GameSettingsManager.Instance.dashboardPanel.activeSelf;

        ThirdPersonCamera.isUIPanelOpen = focused || isDashboardOpen;
        if (chatInputField == null) return;

        if (focused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            chatInputField.ActivateInputField();
            chatInputField.Select();
        }
        else
        {
            chatInputField.DeactivateInputField();
            if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject == chatInputField.gameObject))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (!isDashboardOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void SendChatMessageFromUI()
    {
        if (chatInputField == null) return;
        string rawText = chatInputField.text;
        chatInputField.text = ""; // Xóa ngay tức thì tránh gửi đúp
        if (string.IsNullOrWhiteSpace(rawText)) return;

        string msg = rawText.Trim();
        // Giới hạn 40 ký tự cho 1 lần chat theo yêu cầu
        if (msg.Length > 40)
        {
            msg = msg.Substring(0, 40);
        }

        if (client != null && client.InRoom)
        {
            object[] payload = new object[] { playerName, msg };
            RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.All };
            client.OpRaiseEvent(EVENT_CHAT_MSG, payload, args, SendOptions.SendReliable);
        }
        else
        {
            // Hiển thị nội bộ ngay cả khi đang kết nối
            AppendChatLine($"<color=#55E6C1><b>{playerName}:</b></color> {msg}");
        }
    }

    private void OnChatInputEndEdit(string text)
    {
        // Khi người dùng bấm Enter kết thúc nhập liệu trên InputField
        bool enterPressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            enterPressed = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!enterPressed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            enterPressed = true;
        }
#endif
        if (enterPressed)
        {
            SendChatMessageFromUI();
        }
        SetChatFocus(false);
    }

    public void ToggleMicrophone()
    {
        // 1. Kiểm tra sự tồn tại của thiết bị Microphone trên máy tính
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[PhotonVoice] Không tìm thấy thiết bị Microphone khả dụng trên hệ thống.");
            isMicMuted = true;
            activeMicDevice = null;
            localSpeakingTimer = 0f;
            localVoiceSendHangoverTimer = 0f;
            UpdateStatusUI();
            return;
        }

        isMicMuted = !isMicMuted;
        if (isMicMuted)
        {
            localSpeakingTimer = 0f;
            localVoiceSendHangoverTimer = 0f;
        }
        else
        {
            if (string.IsNullOrEmpty(activeMicDevice) || !Microphone.IsRecording(activeMicDevice))
            {
                StartMicrophoneCapture();
            }

            // Đồng bộ con trỏ thu âm ngay thời điểm vừa bật để tránh nhại lại chuỗi dữ liệu cũ tích tụ
            if (!string.IsNullOrEmpty(activeMicDevice))
            {
                int currentPos = Microphone.GetPosition(activeMicDevice);
                if (currentPos >= 0)
                {
                    lastMicSamplePos = currentPos;
                }
            }
        }
        UpdateStatusUI();
    }

    private void SetupVoicePlayback()
    {
        voicePlaybackSource = gameObject.AddComponent<AudioSource>();
        voicePlaybackSource.spatialBlend = 0f; // Âm thanh Voice Chat rõ ràng toàn phòng
        voicePlaybackSource.loop = true;
        voicePlaybackSource.volume = 1.0f;

        playbackRingClip = AudioClip.Create("PhotonVoiceRing", PLAYBACK_RING_SAMPLES, 1, VOICE_SAMPLE_RATE, false);
        float[] zeroData = new float[PLAYBACK_RING_SAMPLES];
        playbackRingClip.SetData(zeroData, 0);

        voicePlaybackSource.clip = playbackRingClip;
        voicePlaybackSource.Play();
    }

    private void StartMicrophoneCapture()
    {
        try
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                activeMicDevice = null;
                micClip = null;
                return;
            }

            activeMicDevice = Microphone.devices[0];
            if (Microphone.IsRecording(activeMicDevice))
            {
                Microphone.End(activeMicDevice);
            }

            micClip = Microphone.Start(activeMicDevice, true, 2, VOICE_SAMPLE_RATE);
            lastMicSamplePos = 0;
            Debug.Log($"<color=#2ECC71>[PhotonVoice] Micro sẵn sàng: {activeMicDevice} ({VOICE_SAMPLE_RATE}Hz)</color>");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhotonVoice] Lỗi khởi động Microphone: {ex.Message}");
            activeMicDevice = null;
            micClip = null;
        }
    }

    /// <summary>
    /// Đọc an toàn các mẫu âm thanh từ vòng lặp đệm của Microphone, tự động chia hai nửa nếu vòng qua cuối AudioClip
    /// </summary>
    private void SafeGetMicData(AudioClip clip, float[] destBuffer, int offsetSamples, int totalClipSamples)
    {
        if (clip == null || destBuffer == null || destBuffer.Length == 0) return;
        int reqLen = destBuffer.Length;
        if (offsetSamples + reqLen <= totalClipSamples)
        {
            clip.GetData(destBuffer, offsetSamples);
        }
        else
        {
            int part1Len = totalClipSamples - offsetSamples;
            if (part1Len > 0)
            {
                float[] temp1 = new float[part1Len];
                clip.GetData(temp1, offsetSamples);
                Array.Copy(temp1, 0, destBuffer, 0, part1Len);
            }
            int part2Len = reqLen - part1Len;
            if (part2Len > 0)
            {
                float[] temp2 = new float[part2Len];
                clip.GetData(temp2, 0);
                Array.Copy(temp2, 0, destBuffer, part1Len, part2Len);
            }
        }
    }

    private void ProcessMicrophoneAndSendVoice()
    {
        if (isMicMuted || micClip == null || string.IsNullOrEmpty(activeMicDevice)) return;

        if (!Microphone.IsRecording(activeMicDevice))
        {
            StartMicrophoneCapture();
            return;
        }

        int currentPos = Microphone.GetPosition(activeMicDevice);
        if (currentPos < 0) return;

        int clipSamples = micClip.samples;
        int available = currentPos - lastMicSamplePos;
        if (available < 0) available += clipSamples;

        // Nếu bị lag khung hình hoặc giật quá 3 gói (300ms), nhảy cóc về gói mới nhất để chống trễ và chống flood mạng
        if (available > VOICE_CHUNK_SAMPLES * 3)
        {
            lastMicSamplePos = (currentPos - VOICE_CHUNK_SAMPLES + clipSamples) % clipSamples;
            available = VOICE_CHUNK_SAMPLES;
        }

        while (available >= VOICE_CHUNK_SAMPLES)
        {
            SafeGetMicData(micClip, micSampleBuffer, lastMicSamplePos, clipSamples);
            lastMicSamplePos = (lastMicSamplePos + VOICE_CHUNK_SAMPLES) % clipSamples;
            available -= VOICE_CHUNK_SAMPLES;

            // Kiểm tra cường độ âm thanh (Voice Activity Detection - VAD)
            float sumSq = 0f;
            for (int i = 0; i < micSampleBuffer.Length; i++)
            {
                sumSq += micSampleBuffer[i] * micSampleBuffer[i];
            }
            float rms = Mathf.Sqrt(sumSq / micSampleBuffer.Length);

            bool isVoiceDetected = rms > 0.008f;
            if (isVoiceDetected)
            {
                // Giữ trạng thái nói ổn định 0.45s chống nhấp nháy đèn mic và UI
                localSpeakingTimer = Time.unscaledTime + 0.45f;
                localVoiceSendHangoverTimer = Time.unscaledTime + 0.20f;
            }

            // Gửi gói tin nếu có tiếng nói hoặc trong khoảng thời gian đệm (hangover) để tránh nuốt đuôi chữ
            if ((isVoiceDetected || Time.unscaledTime < localVoiceSendHangoverTimer) && !isMicMuted)
            {
                if (client != null && client.InRoom)
                {
                    byte[] encoded = EncodeMuLaw(micSampleBuffer);
                    object[] voicePayload = new object[] { playerName, encoded };
                    RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
                    client.OpRaiseEvent(EVENT_VOICE_PACKET, voicePayload, args, SendOptions.SendUnreliable);
                }
            }
        }
    }

    private void PlayReceivedVoicePacket(string speaker, byte[] encodedBytes)
    {
        if (encodedBytes == null || encodedBytes.Length == 0 || playbackRingClip == null) return;

        remoteSpeakerName = speaker;
        remoteSpeakingTimer = Time.unscaledTime + 0.35f;

        float[] decoded = DecodeMuLaw(encodedBytes);
        int playhead = voicePlaybackSource.timeSamples;

        // Nếu con trỏ ghi bị tụt lại hoặc quá xa con trỏ phát thì căn lại cách 80ms phía trước
        int dist = playbackWritePos - playhead;
        if (dist < 0) dist += PLAYBACK_RING_SAMPLES;
        if (dist < 200 || dist > VOICE_SAMPLE_RATE * 2)
        {
            playbackWritePos = (playhead + 640) % PLAYBACK_RING_SAMPLES;
        }

        if (playbackWritePos + decoded.Length <= PLAYBACK_RING_SAMPLES)
        {
            playbackRingClip.SetData(decoded, playbackWritePos);
        }
        else
        {
            int firstPart = PLAYBACK_RING_SAMPLES - playbackWritePos;
            float[] part1 = new float[firstPart];
            Array.Copy(decoded, 0, part1, 0, firstPart);
            playbackRingClip.SetData(part1, playbackWritePos);

            int secondPart = decoded.Length - firstPart;
            float[] part2 = new float[secondPart];
            Array.Copy(decoded, firstPart, part2, 0, secondPart);
            playbackRingClip.SetData(part2, 0);
        }

        playbackWritePos = (playbackWritePos + decoded.Length) % PLAYBACK_RING_SAMPLES;

        // Xóa sạch vùng đệm nhỏ phía trước để tránh lặp lại tiếng cũ trong vòng tròn (an toàn khi chạm biên)
        int clearPos = playbackWritePos;
        int clearLen = clearSilenceBuffer.Length;
        if (clearPos + clearLen <= PLAYBACK_RING_SAMPLES)
        {
            playbackRingClip.SetData(clearSilenceBuffer, clearPos);
        }
        else
        {
            int p1 = PLAYBACK_RING_SAMPLES - clearPos;
            if (p1 > 0)
            {
                float[] s1 = new float[p1];
                playbackRingClip.SetData(s1, clearPos);
            }
            int p2 = clearLen - p1;
            if (p2 > 0)
            {
                float[] s2 = new float[p2];
                playbackRingClip.SetData(s2, 0);
            }
        }
    }

    // Chuẩn nén âm thanh G.711 μ-law (16-bit float -> 8-bit byte) giúp gói tin Voice siêu nhẹ & rõ nét
    private static byte[] EncodeMuLaw(float[] samples)
    {
        byte[] outBytes = new byte[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float s = Mathf.Clamp(samples[i] * 1.4f, -1f, 1f);
            short pcm = (short)(s * 32767f);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = (short)-pcm;
            if (pcm > 32635) pcm = 32635;
            pcm += 0x84;
            int exponent = 7;
            for (int expMask = 0x4000; (pcm & expMask) == 0 && exponent > 0; exponent--, expMask >>= 1) { }
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            byte mulaw = (byte)(sign | (exponent << 4) | mantissa);
            outBytes[i] = (byte)~mulaw;
        }
        return outBytes;
    }

    private static float[] DecodeMuLaw(byte[] data)
    {
        float[] samples = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte mulaw = (byte)~data[i];
            int sign = mulaw & 0x80;
            int exponent = (mulaw >> 4) & 0x07;
            int mantissa = mulaw & 0x0F;
            int pcm = ((mantissa << 3) + 0x84) << exponent;
            pcm -= 0x84;
            if (sign != 0) pcm = -pcm;
            samples[i] = Mathf.Clamp(pcm / 32767f, -1f, 1f);
        }
        return samples;
    }

    // =========================================================================
    // 3. NHẬN SỰ KIỆN MẠNG & ĐỒNG BỘ NGƯỜI CHƠI TRONG PHÒNG
    // =========================================================================
    public void OnEvent(EventData photonEvent)
    {
        byte code = photonEvent.Code;
        if (code == EVENT_CHAT_MSG)
        {
            if (photonEvent.CustomData is object[] arr && arr.Length >= 2)
            {
                string sender = arr[0]?.ToString() ?? "NgườiChơi";
                string text = arr[1]?.ToString() ?? "";
                AppendChatLine($"<color=#55E6C1><b>{sender}:</b></color> {text}");
                if (isChatMinimized)
                {
                    hasUnreadChat = true;
                }
            }
        }
        else if (code == EVENT_MATCH_STATE)
        {
            if (photonEvent.CustomData is object[] arr && arr.Length >= 1)
            {
                byte stateCode = Convert.ToByte(arr[0]);
                if (stateCode == 1)
                {
                    float dur = arr.Length > 1 ? Convert.ToSingle(arr[1]) : countdownDuration;
                    StartCountdownLocally(dur);
                }
                else if (stateCode == 2)
                {
                    StartGameLocally();
                }
                else if (stateCode == 3)
                {
                    currentMatchState = MatchState.WaitingForPlayers;
                    countdownTimer = 0f;
                    AddSystemMessage("⚠️ Người chơi rời phòng khiến không còn đủ người. Đã hủy đếm ngược!");
                    UpdateLobbyUI();
                }
            }
        }
        else if (code == EVENT_VOICE_PACKET)
        {
            if (photonEvent.CustomData is object[] arr && arr.Length >= 2)
            {
                string speaker = arr[0]?.ToString() ?? "NgườiChơi";
                byte[] audioBytes = arr[1] as byte[];
                PlayReceivedVoicePacket(speaker, audioBytes);
            }
        }
        else if (code == EVENT_PLAYER_SYNC)
        {
            int senderActor = photonEvent.Sender;
            if (client != null && client.LocalPlayer != null && senderActor == client.LocalPlayer.ActorNumber) return;

            if (photonEvent.CustomData is object[] syncArr && syncArr.Length >= 5)
            {
                string rName = syncArr[0]?.ToString() ?? $"NgườiChơi_{senderActor}";
                float px = Convert.ToSingle(syncArr[1]);
                float py = Convert.ToSingle(syncArr[2]);
                float pz = Convert.ToSingle(syncArr[3]);
                float ry = Convert.ToSingle(syncArr[4]);

                UpdateOrCreateRemoteAvatar(senderActor, rName, new Vector3(px, py, pz), Quaternion.Euler(0f, ry, 0f));
            }
        }
    }

    private void UpdateRemotePlayersInterpolation()
    {
        if (client != null && client.InRoom && Time.unscaledTime >= nextSyncTime)
        {
            nextSyncTime = Time.unscaledTime + 0.10f; // Đồng bộ 10 lần/giây
            if (ThirdPersonPlayerController.Instance != null)
            {
                Transform pt = ThirdPersonPlayerController.Instance.transform;
                Vector3 p = pt.position;
                float ry = pt.eulerAngles.y;
                object[] syncData = new object[] { playerName, p.x, p.y, p.z, ry };
                RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
                client.OpRaiseEvent(EVENT_PLAYER_SYNC, syncData, args, SendOptions.SendUnreliable);
            }
        }

        foreach (var kvp in remotePlayers)
        {
            var av = kvp.Value;
            if (av.rootObj == null) continue;
            av.rootObj.transform.position = Vector3.Lerp(av.rootObj.transform.position, av.targetPos, Time.deltaTime * 12f);
            av.rootObj.transform.rotation = Quaternion.Slerp(av.rootObj.transform.rotation, av.targetRot, Time.deltaTime * 12f);
        }
    }

    private void UpdateOrCreateRemoteAvatar(int actorNr, string nickName, Vector3 pos, Quaternion rot)
    {
        if (!remotePlayers.TryGetValue(actorNr, out var av) || av.rootObj == null)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = $"RemotePlayer_{actorNr}_{nickName}";
            Collider col = root.GetComponent<Collider>();
            if (col != null) Destroy(col);

            root.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
            root.transform.position = pos;
            root.transform.rotation = rot;

            var mr = root.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    Material mat = new Material(sh);
                    mat.color = new Color(0.2f, 0.85f, 0.65f, 1f);
                    mr.sharedMaterial = mat;
                }
            }

            av = new RemotePlayerAvatar
            {
                rootObj = root,
                targetPos = pos,
                targetRot = rot,
                lastUpdateTime = Time.unscaledTime
            };
            remotePlayers[actorNr] = av;
        }
        else
        {
            av.targetPos = pos;
            av.targetRot = rot;
            av.lastUpdateTime = Time.unscaledTime;
        }
    }

    private void ClearRemotePlayers()
    {
        foreach (var kvp in remotePlayers)
        {
            if (kvp.Value.rootObj != null) Destroy(kvp.Value.rootObj);
        }
        remotePlayers.Clear();
    }

    // =========================================================================
    // 3B. QUẢN LÝ PHÒNG CHỜ 4 NGƯỜI (MATCHMAKING LOBBY)
    // =========================================================================
    private void StartCountdownLocally(float duration)
    {
        currentMatchState = MatchState.CountingDown;
        countdownTimer = duration;
        AddSystemMessage($"⚡ Đã đủ {requiredPlayersToStart} người chơi! Cổng mê cung sẽ mở sau {Mathf.CeilToInt(duration)} giây!");
        UpdateLobbyUI();
    }

    public void StartGameLocally()
    {
        currentMatchState = MatchState.GameStarted;
        IsGameStarted = true;
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        if (gameStartBannerObj != null)
        {
            gameStartBannerObj.SetActive(true);
            gameStartBannerTimer = 4.5f;
        }
        if (SurvivalTimerUI.Instance != null)
        {
            SurvivalTimerUI.Instance.ResetTimer(true);
        }
        AddSystemMessage("🔥 PHONG ẤN ĐÃ MỞ! HÃY TÌM ĐƯỜNG THOÁT VÀ SỐNG SÓT!");
    }

    public void HostForceStartGame()
    {
        if (client != null && client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
        {
            BroadcastMatchState(2, 0f);
            StartGameLocally();
        }
    }

    private void BroadcastMatchState(byte stateCode, float duration)
    {
        if (client == null || !client.InRoom) return;
        object[] content = new object[] { stateCode, duration };
        RaiseEventArgs raiseEventOptions = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
        client.OpRaiseEvent(EVENT_MATCH_STATE, content, raiseEventOptions, SendOptions.SendReliable);
    }

    private void CheckLobbyPlayerCount()
    {
        if (!requireFullRoomToStart)
        {
            if (!IsGameStarted) StartGameLocally();
            return;
        }

        if (client == null || !client.InRoom || client.CurrentRoom == null) return;
        int count = client.CurrentRoom.PlayerCount;

        if (currentMatchState == MatchState.WaitingForPlayers)
        {
            if (count >= requiredPlayersToStart)
            {
                if (client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
                {
                    BroadcastMatchState(1, countdownDuration);
                }
                StartCountdownLocally(countdownDuration);
            }
        }
        else if (currentMatchState == MatchState.CountingDown)
        {
            if (count < requiredPlayersToStart)
            {
                currentMatchState = MatchState.WaitingForPlayers;
                countdownTimer = 0f;
                if (client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
                {
                    BroadcastMatchState(3, 0f);
                }
                AddSystemMessage("⚠️ Người chơi rời phòng khiến không còn đủ người. Đã hủy đếm ngược!");
            }
        }

        UpdateLobbyUI();
    }

    private void UpdateLobbyUI()
    {
        if (lobbyPanelObj == null) return;

        if (!requireFullRoomToStart || IsGameStarted || client == null || !client.InRoom || client.CurrentRoom == null)
        {
            if (lobbyPanelObj.activeSelf) lobbyPanelObj.SetActive(false);
            return;
        }

        if (!lobbyPanelObj.activeSelf) lobbyPanelObj.SetActive(true);

        int count = client.CurrentRoom.PlayerCount;
        bool isMaster = client.LocalPlayer != null && client.LocalPlayer.IsMasterClient;

        if (lobbyRoomText != null)
        {
            lobbyRoomText.text = $"Phòng: <b>{roomName}</b>  ●  Khu vực: <b>{client.CurrentRegion}</b>";
        }

        if (lobbyCountText != null)
        {
            string col = count >= requiredPlayersToStart ? "#00FF88" : "#FFD700";
            lobbyCountText.text = $"👥 Người chơi: <color={col}>{count} / {requiredPlayersToStart}</color> người";
        }

        if (lobbyPlayerListText != null && client.CurrentRoom.Players != null)
        {
            lobbySb.Clear();
            foreach (var kvp in client.CurrentRoom.Players)
            {
                var p = kvp.Value;
                if (p == null) continue;
                string pNick = !string.IsNullOrEmpty(p.NickName) ? p.NickName : $"NgườiChơi_{p.ActorNumber}";
                if (lobbySb.Length > 0) lobbySb.Append("   |   ");
                if (p.IsMasterClient)
                {
                    lobbySb.Append($"👑 <b>{pNick}</b> (Chủ phòng)");
                }
                else
                {
                    lobbySb.Append($"● {pNick}");
                }
            }
            lobbyPlayerListText.text = lobbySb.ToString();
        }

        if (lobbyStatusText != null)
        {
            if (currentMatchState == MatchState.CountingDown)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, countdownTimer));
                lobbyStatusText.text = $"⚡ ĐỦ {requiredPlayersToStart} NGƯỜI! CỔNG MỞ TRONG: <color=#FFDD44><size=15><b>{sec}s</b></size></color>";
            }
            else
            {
                int missing = Mathf.Max(0, requiredPlayersToStart - count);
                lobbyStatusText.text = missing > 0
                    ? $"⏳ Cần thêm <b>{missing}</b> người để mở phong ấn..."
                    : "⚡ Đã đủ người chơi! Chuẩn bị...";
            }
        }

        if (lobbyForceStartBtnObj != null)
        {
            lobbyForceStartBtnObj.SetActive(isMaster && count < requiredPlayersToStart && allowHostForceStart && currentMatchState == MatchState.WaitingForPlayers);
        }
    }

    // =========================================================================
    // 4. DỰNG GIAO DIỆN UI CHAT & VOICE CHAT TỰ ĐỘNG
    // =========================================================================
    private void BuildChatAndVoiceUI()
    {
        GameObject cGo = new GameObject("Canvas_PhotonChatVoice");
        cGo.transform.SetParent(transform, false);
        Canvas canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;
        scaler.referencePixelsPerUnit = 100f;
        cGo.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(es);
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Khung chứa chính ở góc dưới bên trái
        GameObject panel = new GameObject("PhotonChatVoicePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(18f, 18f);
        panelRect.sizeDelta = new Vector2(410f, 225f);
        chatPanelRect = panelRect;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.12f, 0.72f);

        // Thanh trạng thái kết nối + Nút bật/tắt Mic + Nút Thu gọn ở phía trên khung Chat
        GameObject topBar = new GameObject("TopVoiceBar");
        topBar.transform.SetParent(panel.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = new Vector2(0f, -6f);
        topRect.sizeDelta = new Vector2(-16f, 28f);

        // Trạng thái phòng (Bên trái)
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(topBar.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(0.50f, 1f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        statusBadgeText = statusObj.AddComponent<Text>();
        statusBadgeText.font = defaultFont;
        statusBadgeText.fontSize = 12;
        statusBadgeText.fontStyle = FontStyle.Bold;
        statusBadgeText.alignment = TextAnchor.MiddleLeft;
        statusBadgeText.color = new Color(0.4f, 0.9f, 1f, 1f);
        statusBadgeText.text = "● Đang kết nối Photon...";

        // Nút Mic [V] (Ở giữa)
        GameObject micBtnObj = new GameObject("MicToggleButton");
        micBtnObj.transform.SetParent(topBar.transform, false);
        RectTransform micRect = micBtnObj.AddComponent<RectTransform>();
        micRect.anchorMin = new Vector2(0.52f, 0f);
        micRect.anchorMax = new Vector2(0.83f, 1f);
        micRect.offsetMin = Vector2.zero;
        micRect.offsetMax = Vector2.zero;

        micButtonBg = micBtnObj.AddComponent<Image>();
        micButtonBg.color = new Color(0.15f, 0.65f, 0.38f, 0.92f);

        Button micBtn = micBtnObj.AddComponent<Button>();
        micBtn.onClick.AddListener(ToggleMicrophone);

        GameObject micTxtObj = new GameObject("Text");
        micTxtObj.transform.SetParent(micBtnObj.transform, false);
        RectTransform micTxtRect = micTxtObj.AddComponent<RectTransform>();
        micTxtRect.anchorMin = Vector2.zero;
        micTxtRect.anchorMax = Vector2.one;
        micTxtRect.offsetMin = Vector2.zero;
        micTxtRect.offsetMax = Vector2.zero;

        micButtonText = micTxtObj.AddComponent<Text>();
        micButtonText.font = defaultFont;
        micButtonText.fontSize = 12;
        micButtonText.fontStyle = FontStyle.Bold;
        micButtonText.alignment = TextAnchor.MiddleCenter;
        micButtonText.color = Color.white;
        micButtonText.text = "🎙 MIC: BẬT [V]";

        // Nút Thu Gọn / Ẩn Chat (Bên phải)
        GameObject minBtnObj = new GameObject("MinimizeChatButton");
        minBtnObj.transform.SetParent(topBar.transform, false);
        RectTransform minRect = minBtnObj.AddComponent<RectTransform>();
        minRect.anchorMin = new Vector2(0.85f, 0f);
        minRect.anchorMax = new Vector2(1f, 1f);
        minRect.offsetMin = Vector2.zero;
        minRect.offsetMax = Vector2.zero;

        Image minBg = minBtnObj.AddComponent<Image>();
        minBg.color = new Color(0.18f, 0.25f, 0.35f, 0.92f);

        Button minBtn = minBtnObj.AddComponent<Button>();
        minBtn.onClick.AddListener(ToggleChatMinimized);

        GameObject minTxtObj = new GameObject("Text");
        minTxtObj.transform.SetParent(minBtnObj.transform, false);
        RectTransform minTxtRect = minTxtObj.AddComponent<RectTransform>();
        minTxtRect.anchorMin = Vector2.zero;
        minTxtRect.anchorMax = Vector2.one;
        minTxtRect.offsetMin = Vector2.zero;
        minTxtRect.offsetMax = Vector2.zero;

        minimizeButtonText = minTxtObj.AddComponent<Text>();
        minimizeButtonText.font = defaultFont;
        minimizeButtonText.fontSize = 12;
        minimizeButtonText.fontStyle = FontStyle.Bold;
        minimizeButtonText.alignment = TextAnchor.MiddleCenter;
        minimizeButtonText.color = Color.white;
        minimizeButtonText.text = isChatMinimized ? "▲" : "▼";

        // Dòng báo hiệu ai đang nói Voice Chat
        GameObject speakObj = new GameObject("SpeakingIndicator");
        speakObj.transform.SetParent(panel.transform, false);
        RectTransform speakRect = speakObj.AddComponent<RectTransform>();
        speakRect.anchorMin = new Vector2(0f, 1f);
        speakRect.anchorMax = new Vector2(1f, 1f);
        speakRect.pivot = new Vector2(0.5f, 1f);
        speakRect.anchoredPosition = new Vector2(0f, -35f);
        speakRect.sizeDelta = new Vector2(-16f, 18f);

        speakingIndicatorText = speakObj.AddComponent<Text>();
        speakingIndicatorText.font = defaultFont;
        speakingIndicatorText.fontSize = 12;
        speakingIndicatorText.fontStyle = FontStyle.Italic;
        speakingIndicatorText.alignment = TextAnchor.MiddleLeft;
        speakingIndicatorText.color = new Color(0.3f, 1f, 0.55f, 1f);
        speakingIndicatorText.text = "";

        // Vùng hiển thị lịch sử tin nhắn
        GameObject historyObj = new GameObject("ChatHistoryText");
        historyObj.transform.SetParent(panel.transform, false);
        RectTransform histRect = historyObj.AddComponent<RectTransform>();
        histRect.anchorMin = new Vector2(0f, 0f);
        histRect.anchorMax = new Vector2(1f, 1f);
        histRect.offsetMin = new Vector2(10f, 40f);
        histRect.offsetMax = new Vector2(-10f, -54f);

        chatHistoryText = historyObj.AddComponent<Text>();
        chatHistoryText.font = defaultFont;
        chatHistoryText.fontSize = 13;
        chatHistoryText.alignment = TextAnchor.LowerLeft;
        chatHistoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        chatHistoryText.verticalOverflow = VerticalWrapMode.Truncate;
        chatHistoryText.supportRichText = true;
        chatHistoryText.color = new Color(0.92f, 0.95f, 1f, 0.95f);

        // Dòng nhập tin nhắn ở dưới cùng
        chatInputRow = new GameObject("ChatInputRow");
        chatInputRow.transform.SetParent(panel.transform, false);
        RectTransform inputRowRect = chatInputRow.AddComponent<RectTransform>();
        inputRowRect.anchorMin = new Vector2(0f, 0f);
        inputRowRect.anchorMax = new Vector2(1f, 0f);
        inputRowRect.pivot = new Vector2(0.5f, 0f);
        inputRowRect.anchoredPosition = new Vector2(0f, 6f);
        inputRowRect.sizeDelta = new Vector2(-16f, 28f);

        GameObject inputBoxObj = new GameObject("ChatInputField");
        inputBoxObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform inputBoxRect = inputBoxObj.AddComponent<RectTransform>();
        inputBoxRect.anchorMin = new Vector2(0f, 0f);
        inputBoxRect.anchorMax = new Vector2(0.80f, 1f);
        inputBoxRect.offsetMin = Vector2.zero;
        inputBoxRect.offsetMax = new Vector2(-4f, 0f);

        Image inputBg = inputBoxObj.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);

        chatInputField = inputBoxObj.AddComponent<InputField>();
        chatInputField.characterLimit = 40; // Giới hạn tối đa 40 ký tự mỗi lần chat
        chatInputField.lineType = InputField.LineType.SingleLine;
        chatInputField.onEndEdit.AddListener(OnChatInputEndEdit);

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputBoxObj.transform, false);
        RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(8f, 2f);
        phRect.offsetMax = new Vector2(-8f, -2f);

        Text phText = placeholderObj.AddComponent<Text>();
        phText.font = defaultFont;
        phText.fontSize = 12;
        phText.fontStyle = FontStyle.Italic;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.6f, 0.68f, 0.78f, 0.75f);
        phText.text = "Nhập tin nhắn (tối đa 40 ký tự)...";

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputBoxObj.transform, false);
        RectTransform itRect = inputTextObj.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = new Vector2(8f, 2f);
        itRect.offsetMax = new Vector2(-8f, -2f);

        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = defaultFont;
        inputText.fontSize = 13;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.color = Color.white;
        inputText.supportRichText = false;

        chatInputField.textComponent = inputText;
        chatInputField.placeholder = phText;

        // Nút GỬI
        GameObject sendBtnObj = new GameObject("SendButton");
        sendBtnObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform sendRect = sendBtnObj.AddComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(0.80f, 0f);
        sendRect.anchorMax = new Vector2(1f, 1f);
        sendRect.offsetMin = Vector2.zero;
        sendRect.offsetMax = Vector2.zero;

        Image sendBg = sendBtnObj.AddComponent<Image>();
        sendBg.color = new Color(0.2f, 0.55f, 0.95f, 0.95f);

        Button sendBtn = sendBtnObj.AddComponent<Button>();
        sendBtn.onClick.AddListener(() =>
        {
            SendChatMessageFromUI();
            SetChatFocus(false);
        });

        GameObject sendTxtObj = new GameObject("Text");
        sendTxtObj.transform.SetParent(sendBtnObj.transform, false);
        RectTransform stRect = sendTxtObj.AddComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        Text sendTxt = sendTxtObj.AddComponent<Text>();
        sendTxt.font = defaultFont;
        sendTxt.fontSize = 12;
        sendTxt.fontStyle = FontStyle.Bold;
        sendTxt.alignment = TextAnchor.MiddleCenter;
        sendTxt.color = Color.white;
        sendTxt.text = "GỬI";

        // Dựng giao diện Phòng Chờ 4 người (Lobby HUD)
        BuildLobbyWaitingUI(canvas, defaultFont);

        if (startChatMinimized)
        {
            SetChatMinimized(true);
        }
    }

    private void BuildLobbyWaitingUI(Canvas canvas, Font defaultFont)
    {
        // Khung chứa phòng chờ
        lobbyPanelObj = new GameObject("PhotonLobbyWaitingPanel");
        lobbyPanelObj.transform.SetParent(canvas.transform, false);
        RectTransform lobbyRect = lobbyPanelObj.AddComponent<RectTransform>();
        lobbyRect.anchorMin = new Vector2(0.5f, 1f);
        lobbyRect.anchorMax = new Vector2(0.5f, 1f);
        lobbyRect.pivot = new Vector2(0.5f, 1f);
        lobbyRect.anchoredPosition = new Vector2(0f, -65f);
        lobbyRect.sizeDelta = new Vector2(520f, 195f);

        Image lobbyBg = lobbyPanelObj.AddComponent<Image>();
        lobbyBg.color = new Color(0.04f, 0.07f, 0.11f, 0.90f);
        Outline lobbyOutline = lobbyPanelObj.AddComponent<Outline>();
        lobbyOutline.effectColor = new Color(0.85f, 0.7f, 0.2f, 0.8f);
        lobbyOutline.effectDistance = new Vector2(2f, 2f);

        // Tiêu đề phòng chờ
        GameObject lTitleObj = new GameObject("LobbyTitle");
        lTitleObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform ltRect = lTitleObj.AddComponent<RectTransform>();
        ltRect.anchorMin = new Vector2(0f, 1f);
        ltRect.anchorMax = new Vector2(1f, 1f);
        ltRect.pivot = new Vector2(0.5f, 1f);
        ltRect.anchoredPosition = new Vector2(0f, -8f);
        ltRect.sizeDelta = new Vector2(-20f, 26f);

        lobbyTitleText = lTitleObj.AddComponent<Text>();
        lobbyTitleText.font = defaultFont;
        lobbyTitleText.fontSize = 15;
        lobbyTitleText.fontStyle = FontStyle.Bold;
        lobbyTitleText.alignment = TextAnchor.MiddleCenter;
        lobbyTitleText.color = new Color(1f, 0.85f, 0.3f, 1f);
        lobbyTitleText.text = "⚔️ CỔNG MÊ CUNG NGHĨA ĐỊA - PHÒNG CHỜ ⚔️";

        // Tên phòng & Server
        GameObject lRoomObj = new GameObject("LobbyRoomText");
        lRoomObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform lrRect = lRoomObj.AddComponent<RectTransform>();
        lrRect.anchorMin = new Vector2(0f, 1f);
        lrRect.anchorMax = new Vector2(1f, 1f);
        lrRect.pivot = new Vector2(0.5f, 1f);
        lrRect.anchoredPosition = new Vector2(0f, -34f);
        lrRect.sizeDelta = new Vector2(-20f, 20f);

        lobbyRoomText = lRoomObj.AddComponent<Text>();
        lobbyRoomText.font = defaultFont;
        lobbyRoomText.fontSize = 12;
        lobbyRoomText.alignment = TextAnchor.MiddleCenter;
        lobbyRoomText.color = new Color(0.7f, 0.85f, 1f, 0.95f);
        lobbyRoomText.text = $"Phòng: <b>{roomName}</b>";

        // Số lượng người chơi (Counter)
        GameObject lCountObj = new GameObject("LobbyCountText");
        lCountObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform lcRect = lCountObj.AddComponent<RectTransform>();
        lcRect.anchorMin = new Vector2(0f, 1f);
        lcRect.anchorMax = new Vector2(1f, 1f);
        lcRect.pivot = new Vector2(0.5f, 1f);
        lcRect.anchoredPosition = new Vector2(0f, -56f);
        lcRect.sizeDelta = new Vector2(-20f, 26f);

        lobbyCountText = lCountObj.AddComponent<Text>();
        lobbyCountText.font = defaultFont;
        lobbyCountText.fontSize = 15;
        lobbyCountText.fontStyle = FontStyle.Bold;
        lobbyCountText.alignment = TextAnchor.MiddleCenter;
        lobbyCountText.color = new Color(0.2f, 0.95f, 0.65f, 1f);
        lobbyCountText.text = $"👥 Người chơi: 1 / {requiredPlayersToStart}";

        // Danh sách người chơi trong phòng
        GameObject lListObj = new GameObject("LobbyPlayerList");
        lListObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform llRect = lListObj.AddComponent<RectTransform>();
        llRect.anchorMin = new Vector2(0f, 0f);
        llRect.anchorMax = new Vector2(1f, 1f);
        llRect.offsetMin = new Vector2(20f, 38f);
        llRect.offsetMax = new Vector2(-20f, -84f);

        lobbyPlayerListText = lListObj.AddComponent<Text>();
        lobbyPlayerListText.font = defaultFont;
        lobbyPlayerListText.fontSize = 12;
        lobbyPlayerListText.alignment = TextAnchor.MiddleCenter;
        lobbyPlayerListText.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        lobbyPlayerListText.text = "● Đang tải danh sách người chơi...";

        // Trạng thái đếm ngược / Chờ người
        GameObject lStatusObj = new GameObject("LobbyStatusText");
        lStatusObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform lsRect = lStatusObj.AddComponent<RectTransform>();
        lsRect.anchorMin = new Vector2(0f, 0f);
        lsRect.anchorMax = new Vector2(0.72f, 0f);
        lsRect.pivot = new Vector2(0f, 0f);
        lsRect.anchoredPosition = new Vector2(16f, 6f);
        lsRect.sizeDelta = new Vector2(0f, 28f);

        lobbyStatusText = lStatusObj.AddComponent<Text>();
        lobbyStatusText.font = defaultFont;
        lobbyStatusText.fontSize = 12;
        lobbyStatusText.fontStyle = FontStyle.Bold;
        lobbyStatusText.alignment = TextAnchor.MiddleLeft;
        lobbyStatusText.color = new Color(1f, 0.8f, 0.25f, 1f);
        lobbyStatusText.text = $"⏳ Đang chờ đủ {requiredPlayersToStart} người để mở cổng...";

        // Nút Bắt Đầu Ngay (Dành cho Chủ phòng Host)
        lobbyForceStartBtnObj = new GameObject("LobbyForceStartBtn");
        lobbyForceStartBtnObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform lfBtnRect = lobbyForceStartBtnObj.AddComponent<RectTransform>();
        lfBtnRect.anchorMin = new Vector2(0.74f, 0f);
        lfBtnRect.anchorMax = new Vector2(1f, 0f);
        lfBtnRect.pivot = new Vector2(1f, 0f);
        lfBtnRect.anchoredPosition = new Vector2(-16f, 6f);
        lfBtnRect.sizeDelta = new Vector2(0f, 28f);

        Image lfBtnBg = lobbyForceStartBtnObj.AddComponent<Image>();
        lfBtnBg.color = new Color(0.2f, 0.65f, 0.35f, 0.95f);
        Button lfBtn = lobbyForceStartBtnObj.AddComponent<Button>();
        lfBtn.onClick.AddListener(HostForceStartGame);

        GameObject lfTxtObj = new GameObject("Text");
        lfTxtObj.transform.SetParent(lobbyForceStartBtnObj.transform, false);
        RectTransform lfTxtRect = lfTxtObj.AddComponent<RectTransform>();
        lfTxtRect.anchorMin = Vector2.zero;
        lfTxtRect.anchorMax = Vector2.one;
        lfTxtRect.offsetMin = Vector2.zero;
        lfTxtRect.offsetMax = Vector2.zero;

        Text lfTxt = lfTxtObj.AddComponent<Text>();
        lfTxt.font = defaultFont;
        lfTxt.fontSize = 11;
        lfTxt.fontStyle = FontStyle.Bold;
        lfTxt.alignment = TextAnchor.MiddleCenter;
        lfTxt.color = Color.white;
        lfTxt.text = "Vào Sớm ▶";

        lobbyPanelObj.SetActive(false); // Sẽ bật khi đã vào phòng

        // Banner Game Bắt Đầu (To giữa màn hình)
        gameStartBannerObj = new GameObject("GameStartBanner");
        gameStartBannerObj.transform.SetParent(canvas.transform, false);
        RectTransform gbRect = gameStartBannerObj.AddComponent<RectTransform>();
        gbRect.anchorMin = new Vector2(0.5f, 0.5f);
        gbRect.anchorMax = new Vector2(0.5f, 0.5f);
        gbRect.pivot = new Vector2(0.5f, 0.5f);
        gbRect.anchoredPosition = new Vector2(0f, 120f);
        gbRect.sizeDelta = new Vector2(800f, 80f);

        Image gbBg = gameStartBannerObj.AddComponent<Image>();
        gbBg.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);

        GameObject gbTxtObj = new GameObject("Text");
        gbTxtObj.transform.SetParent(gameStartBannerObj.transform, false);
        RectTransform gbtRect = gbTxtObj.AddComponent<RectTransform>();
        gbtRect.anchorMin = Vector2.zero;
        gbtRect.anchorMax = Vector2.one;
        gbtRect.offsetMin = Vector2.zero;
        gbtRect.offsetMax = Vector2.zero;

        gameStartBannerText = gbTxtObj.AddComponent<Text>();
        gameStartBannerText.font = defaultFont;
        gameStartBannerText.fontSize = 24;
        gameStartBannerText.fontStyle = FontStyle.Bold;
        gameStartBannerText.alignment = TextAnchor.MiddleCenter;
        gameStartBannerText.color = new Color(1f, 0.85f, 0.2f, 1f);
        gameStartBannerText.text = "🔥 PHONG ẤN ĐÃ MỞ! HÃY TÌM ĐƯỜNG THOÁT VÀ SỐNG SÓT! 🔥";

        Outline gbOutline = gbTxtObj.AddComponent<Outline>();
        gbOutline.effectColor = new Color(0.9f, 0.2f, 0.1f, 0.9f);
        gbOutline.effectDistance = new Vector2(2f, 2f);

        gameStartBannerObj.SetActive(false);
    }

    private void AddSystemMessage(string msg)
    {
        AppendChatLine($"<color=#FDCB6E>[Hệ Thống] {msg}</color>");
    }

    private void AppendChatLine(string formattedLine)
    {
        chatMessages.Add(formattedLine);
        while (chatMessages.Count > MAX_CHAT_LINES)
        {
            chatMessages.RemoveAt(0);
        }
        if (chatHistoryText != null)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < chatMessages.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(chatMessages[i]);
            }
            chatHistoryText.text = sb.ToString();
        }
    }

    private void UpdateStatusUI()
    {
        if (statusBadgeText != null && client != null)
        {
            if (isChatMinimized)
            {
                if (hasUnreadChat)
                {
                    statusBadgeText.color = new Color(1f, 0.75f, 0.15f, 1f);
                    statusBadgeText.text = $"💬 <color=#FFDD44>Tin mới!</color> [{toggleChatKey}]";
                }
                else if (client.InRoom && client.CurrentRoom != null)
                {
                    statusBadgeText.color = new Color(0.4f, 0.95f, 0.65f, 1f);
                    statusBadgeText.text = $"💬 Chat [{toggleChatKey}] ● {client.CurrentRoom.PlayerCount}/{requiredPlayersToStart}";
                }
                else
                {
                    statusBadgeText.color = new Color(0.7f, 0.85f, 1f, 0.9f);
                    statusBadgeText.text = $"💬 Chat [{toggleChatKey}]";
                }
            }
            else
            {
                if (client.InRoom && client.CurrentRoom != null)
                {
                    statusBadgeText.color = new Color(0.3f, 1f, 0.55f, 1f);
                    statusBadgeText.text = $"● Online: {client.CurrentRoom.Name} ({client.CurrentRoom.PlayerCount}/{requiredPlayersToStart})";
                }
                else if (client.IsConnected)
                {
                    statusBadgeText.color = new Color(1f, 0.85f, 0.3f, 1f);
                    statusBadgeText.text = $"● Photon: {client.State}...";
                }
                else
                {
                    statusBadgeText.color = new Color(1f, 0.45f, 0.45f, 1f);
                    statusBadgeText.text = "● Đang kết nối Photon Cloud...";
                }
            }
        }

        if (micButtonText != null && micButtonBg != null)
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                micButtonText.text = "🚫 MIC: KHÔNG TÌM THẤY";
                micButtonBg.color = new Color(0.40f, 0.40f, 0.45f, 0.88f);
            }
            else if (isMicMuted)
            {
                micButtonText.text = $"🔇 MIC: TẮT [{toggleMicKey}]";
                micButtonBg.color = new Color(0.65f, 0.22f, 0.22f, 0.92f);
            }
            else
            {
                micButtonText.text = $"🎙 MIC: BẬT [{toggleMicKey}]";
                micButtonBg.color = IsLocalSpeaking
                    ? new Color(0.18f, 0.88f, 0.45f, 1f)
                    : new Color(0.15f, 0.58f, 0.35f, 0.92f);
            }
        }

        if (speakingIndicatorText != null)
        {
            if (Time.unscaledTime < remoteSpeakingTimer && !string.IsNullOrEmpty(remoteSpeakerName))
            {
                speakingIndicatorText.text = $"🔊 {remoteSpeakerName} đang nói...";
            }
            else if (IsLocalSpeaking)
            {
                speakingIndicatorText.text = "🎙 Bạn đang nói qua Mic...";
            }
            else
            {
                speakingIndicatorText.text = "";
            }
        }
    }
}
