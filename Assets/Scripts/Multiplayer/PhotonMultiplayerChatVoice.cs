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
    public KeyCode toggleVoiceSettingsKey = KeyCode.K;

    [Header("2.1. Cài Đặt Photon Voice Nâng Cao")]
    [Range(0.5f, 3.0f)] public float micGain = 1.0f;
    [Range(0.002f, 0.035f)] public float vadThreshold = 0.008f;
    [Range(0.0f, 1.5f)] public float incomingVoiceVolume = 1.0f;
    public float currentMicRMS { get; private set; } = 0f;

    [Header("3. Chế Độ Phòng Chờ Đủ Người Mới Vào (Matchmaking Lobby)")]
    [Tooltip("Bật chế độ chờ đủ 4 người chơi mới mở phong ấn bắt đầu game")]
    public bool requireFullRoomToStart = true;
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
    private const byte EVENT_ROLE_ASSIGN = 105;
    private const byte EVENT_PLAYER_READY = 106;
    private const byte EVENT_GHOST_ATTACK = 107;
    public static readonly System.Collections.Generic.Dictionary<int, bool> ReadyPlayerDict = new System.Collections.Generic.Dictionary<int, bool>();
    public static bool IsLocalPlayerReady = false;
    public static readonly System.Collections.Generic.List<int> GhostActorNumbers = new System.Collections.Generic.List<int>();
    public static bool soloTestAsGhost = false;

    private const string ROOM_PROP_MATCH_STATE = "gm_ms";
    private const string ROOM_PROP_GHOST_ACTORS = "gm_ga";
    private bool isTransitioningToMatchScene = false;
    private bool needReliableInitialPlayerSync = false;

    private void ResetMatchLobbyState(bool clearRoles = true)
    {
        IsLocalPlayerReady = false;
        ReadyPlayerDict.Clear();
        if (clearRoles) GhostActorNumbers.Clear();
        IsGameStarted = !requireFullRoomToStart;
        currentMatchState = requireFullRoomToStart ? MatchState.WaitingForPlayers : MatchState.GameStarted;
        countdownTimer = 0f;
        isTransitioningToMatchScene = false;
        needReliableInitialPlayerSync = false;
    }

    private RealtimeClient client;
    private bool isConnectingOrConnected;
    private bool useFusionPlugin = false;
    private bool roomJoinBlocked = false; // Server từ chối phòng (sai loại App ID) -> ngừng thử lại liên tục
    private float reconnectTimer;
    private bool isAutoMatchingRandomRoom = false;
    private float nextMicRetryTime = 0f;

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
    public RectTransform ChatPanelRect => chatPanelRect;
    private Text minimizeButtonText;

    // Giao diện UI Bảng Cài Đặt Photon Voice
    private GameObject voiceSettingsDialogObj;
    private Text voiceSettingsMicDeviceText;
    private RectTransform voiceSettingsVuFillRect;
    private Slider voiceSettingsGainSlider;
    private Text voiceSettingsGainValueText;
    private Slider voiceSettingsVadSlider;
    private Text voiceSettingsVadValueText;
    private Slider voiceSettingsIncomingSlider;
    private Text voiceSettingsIncomingValueText;

    private GameObject lobbyPanelObj;
    private GameObject lobbyMainMenuSubView;
    private GameObject lobbyRoomWaitingSubView;
    private InputField lobbyPlayerNameInput = null;
    private InputField lobbyRoomNameInput = null;
    private Text lobbyServerStatusText;
    private Text lobbyTitleText;
    private Text lobbyRoomText;
    private Text lobbyCountText;
    private Text lobbyPlayerListText;
    private Text lobbyStatusText;
    private GameObject lobbyForceStartBtnObj;
    private GameObject gameStartBannerObj;
    private Text gameStartBannerText;
    private float gameStartBannerTimer = 0f;
    private bool isLobbyCursorUnlocked = false;
    private readonly System.Text.StringBuilder lobbySb = new System.Text.StringBuilder();
    private readonly List<int> voiceTargetActorBuffer = new List<int>(8);

    public struct RoomPlayerEntry
    {
        public int actorNumber;
        public string nickName;
        public bool isLocal;
        public bool isMaster;
        public bool isReady;
    }

    public void ToggleLocalPlayerReady()
    {
        IsLocalPlayerReady = !IsLocalPlayerReady;
        int myActor = (client != null && client.LocalPlayer != null) ? client.LocalPlayer.ActorNumber : 1;
        ReadyPlayerDict[myActor] = IsLocalPlayerReady;
        if (client != null && client.InRoom)
        {
            object[] payload = new object[] { myActor, IsLocalPlayerReady };
            RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.All };
            client.OpRaiseEvent(EVENT_PLAYER_READY, payload, args, SendOptions.SendReliable);
        }
        CheckAllPlayersConfirmedAndStart();
    }

    public bool AreAllPlayersReady()
    {
        if (client == null || !client.InRoom || client.CurrentRoom == null || client.CurrentRoom.Players == null)
        {
            return IsLocalPlayerReady;
        }
        if (client.CurrentRoom.PlayerCount == 0) return false;

        int localActor = client.LocalPlayer != null ? client.LocalPlayer.ActorNumber : -1;
        foreach (var kvp in client.CurrentRoom.Players)
        {
            int actorNr = kvp.Key;
            if (actorNr == localActor)
            {
                if (!IsLocalPlayerReady) return false;
            }
            else
            {
                if (!ReadyPlayerDict.TryGetValue(actorNr, out bool r) || !r) return false;
            }
        }
        return true;
    }

    public int GetReadyPlayersCount()
    {
        int count = 0;
        if (client != null && client.InRoom && client.CurrentRoom != null && client.CurrentRoom.Players != null)
        {
            int localActor = client.LocalPlayer != null ? client.LocalPlayer.ActorNumber : -1;
            foreach (var kvp in client.CurrentRoom.Players)
            {
                int actorNr = kvp.Key;
                if (actorNr == localActor)
                {
                    if (IsLocalPlayerReady) count++;
                }
                else if (ReadyPlayerDict.TryGetValue(actorNr, out bool r) && r)
                {
                    count++;
                }
            }
        }
        else
        {
            if (IsLocalPlayerReady) count = 1;
        }
        return count;
    }

    public void CheckAllPlayersConfirmedAndStart()
    {
        if (IsGameStarted) return;
        if (!hasUserRequestedRoom) return;

        int totalCount = CurrentRoomPlayersCount;
        if (totalCount < 1) return;

        bool allReady = AreAllPlayersReady();
        if (allReady && (totalCount >= requiredPlayersToStart || totalCount >= 4))
        {
            if (client != null && client.InRoom)
            {
                if (client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
                {
                    HostAssignRandomRoles();
                    BroadcastMatchState(2, 0f);
                    PublishMatchStartRoomProperties();
                    client.SendOutgoingCommands();
                    StartGameLocally();
                }
            }
            else
            {
                StartGameLocally();
            }
        }
    }

    private bool hasUserRequestedRoom = false;
    public bool HasUserRequestedRoom => hasUserRequestedRoom;
    public bool IsConnectedToMaster => client != null && (client.IsConnectedAndReady || client.InRoom || client.State == ClientState.ConnectedToMasterServer);
    public bool IsInPhotonRoom => client != null && client.InRoom && client.CurrentRoom != null;
    public bool IsLocalMaster => client == null || !client.InRoom || (client.LocalPlayer != null && client.LocalPlayer.IsMasterClient);
    public int CurrentRoomPlayersCount => (client != null && client.InRoom && client.CurrentRoom != null) ? (int)client.CurrentRoom.PlayerCount : (hasUserRequestedRoom ? 1 : 0);
    public float RemainingCountdown => currentMatchState == MatchState.CountingDown ? Mathf.Max(0f, countdownTimer) : -1f;

    public void SetLocalPlayerNickname(string newNick)
    {
        if (string.IsNullOrWhiteSpace(newNick)) return;
        playerName = newNick.Trim();
        if (client != null && client.LocalPlayer != null)
        {
            client.LocalPlayer.NickName = playerName;
        }
    }

    public void JoinRandomOrCreateRoom(string customNickName = null)
    {
        if (!string.IsNullOrWhiteSpace(customNickName))
        {
            SetLocalPlayerNickname(customNickName);
        }
        hasUserRequestedRoom = true;
        isAutoMatchingRandomRoom = true;
        ResetMatchLobbyState();

        if (client == null)
        {
            ConnectToPhoton();
            return;
        }

        if (client.InRoom)
        {
            client.OpLeaveRoom(false);
            return;
        }

        if (client.State == ClientState.ConnectedToMasterServer)
        {
            client.OpJoinRandomRoom();
        }
        else if (!client.IsConnected)
        {
            ConnectToPhoton();
        }
    }

    public void JoinOrCreateCustomRoom(string targetRoomId, string customNickName = null)
    {
        if (!string.IsNullOrWhiteSpace(customNickName))
        {
            SetLocalPlayerNickname(customNickName);
        }
        if (!string.IsNullOrWhiteSpace(targetRoomId))
        {
            roomName = targetRoomId.Trim();
        }
        hasUserRequestedRoom = true;
        isAutoMatchingRandomRoom = false;

        if (client != null && client.InRoom && client.CurrentRoom != null
            && string.Equals(client.CurrentRoom.Name, roomName, StringComparison.OrdinalIgnoreCase))
        {
            CheckRoomPropertiesForMatchStart();
            CheckLobbyPlayerCount();
            return;
        }

        ResetMatchLobbyState();

        if (client == null)
        {
            ConnectToPhoton();
            return;
        }

        if (client.InRoom)
        {
            client.OpLeaveRoom(false);
            return;
        }

        if (client.State == ClientState.ConnectedToMasterServer)
        {
            JoinOrCreateTargetRoom();
        }
        else if (!client.IsConnected)
        {
            ConnectToPhoton();
        }
    }

    public void LeaveRoomAndReturnToMenu()
    {
        hasUserRequestedRoom = false;
        ResetMatchLobbyState();
        ClearRemotePlayers();
        if (client != null && client.InRoom)
        {
            client.OpLeaveRoom(false);
            client.SendOutgoingCommands();
        }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene")
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
        }
    }

    public List<RoomPlayerEntry> GetRoomPlayersList()
    {
        List<RoomPlayerEntry> list = new List<RoomPlayerEntry>();
        if (client != null && client.InRoom && client.CurrentRoom != null && client.CurrentRoom.Players != null && client.CurrentRoom.Players.Count > 0)
        {
            int localActor = client.LocalPlayer != null ? client.LocalPlayer.ActorNumber : -1;
            foreach (var kvp in client.CurrentRoom.Players)
            {
                var p = kvp.Value;
                if (p == null) continue;
                bool isMe = (p.ActorNumber == localActor);
                string pNick = !string.IsNullOrWhiteSpace(p.NickName) ? p.NickName : (isMe ? playerName : $"NgÆ°á»iChÆ¡i_{p.ActorNumber}");
                bool pReady = isMe ? IsLocalPlayerReady : (ReadyPlayerDict.TryGetValue(p.ActorNumber, out bool pr) && pr);
                list.Add(new RoomPlayerEntry
                {
                    actorNumber = p.ActorNumber,
                    nickName = pNick,
                    isLocal = isMe,
                    isMaster = p.IsMasterClient,
                    isReady = pReady
                });
            }
            list.Sort((a, b) =>
            {
                if (a.isLocal != b.isLocal) return a.isLocal ? -1 : 1;
                if (a.isMaster != b.isMaster) return a.isMaster ? -1 : 1;
                return a.actorNumber.CompareTo(b.actorNumber);
            });
        }
        else
        {
            // Hiá»ƒn thá»‹ ngay báº£n thÃ¢n ngÆ°á»i chÆ¡i ("TÃ´i á»Ÿ Ä‘Ã¢y") ngay khi vá»«a nháº¥n Táº¡o phÃ²ng / VÃ o phÃ²ng
            list.Add(new RoomPlayerEntry
            {
                actorNumber = 1,
                nickName = !string.IsNullOrWhiteSpace(playerName) ? playerName : "NgÆ°á»iChÆ¡i_TÃ´i",
                isLocal = true,
                isMaster = true,
                isReady = IsLocalPlayerReady
            });
        }
        return list;
    }

    private class RemotePlayerAvatar
    {
        public GameObject rootObj;
        public Vector3 targetPos;
        public Quaternion targetRot;
        public float lastUpdateTime;
        public bool isGhostForm;
        public Animator animator;
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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        // Giữ kết nối Photon khi cửa sổ Game mất focus (tránh cảnh báo "SendOutgoingCommands() was not called for > 5 seconds")
        Application.runInBackground = true;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "NgườiChơi_" + UnityEngine.Random.Range(100, 999);
        }

        ResetMatchLobbyState();
        isChatMinimized = startChatMinimized;

        SetupVoicePlayback();
        BuildChatAndVoiceUI();
        ConnectToPhoton();
    }

    private void Start()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif
        StartMicrophoneCapture();
    }

    private void Update()
    {
        // [GhostMaze] O LobbyScene: Luon mo chuot tu do (khong khoa chuot) va an toan bo Chat UI
        bool isLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (isLobbyScene)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (chatPanelRect != null && chatPanelRect.gameObject.activeSelf)
            {
                chatPanelRect.gameObject.SetActive(false);
            }
            if (lobbyPanelObj != null && lobbyPanelObj.activeSelf)
            {
                lobbyPanelObj.SetActive(false);
            }
        }
        else
        {
            if (chatPanelRect != null && !chatPanelRect.gameObject.activeSelf)
            {
                chatPanelRect.gameObject.SetActive(true);
            }
        }

        if (client != null)
        {
            client.Service();

            if (!IsGameStarted && !isTransitioningToMatchScene && client.InRoom)
            {
                CheckRoomPropertiesForMatchStart();
            }

            if (client.State == ClientState.Disconnected && !roomJoinBlocked && Time.unscaledTime >= reconnectTimer)
            {
                reconnectTimer = Time.unscaledTime + 5f;
                ConnectToPhoton();
            }
        }

        if (!IsGameStarted)
        {
            if (isLobbyScene)
            {
                if (!Application.isMobilePlatform)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                ThirdPersonCamera.isUIPanelOpen = true;
            }
            else if (client != null && client.InRoom)
            {
                if (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.currentPhase == SurvivalTimerUI.MatchPhase.Lobby)
                {
                    SurvivalTimerUI.Instance.ConstrainLocalPlayerToPlaza();
                }

                if (ReadAltToggleDown())
                {
                    isLobbyCursorUnlocked = !isLobbyCursorUnlocked;
                }

                if (ReadF1KeyDown() && client.LocalPlayer != null && client.LocalPlayer.IsMasterClient && currentMatchState == MatchState.WaitingForPlayers)
                {
                    HostForceStartGame();
                }

                bool isTyping = IsTypingChat();
                bool isDashboard = GameSettingsManager.Instance != null
                    && GameSettingsManager.Instance.dashboardPanel != null
                    && GameSettingsManager.Instance.dashboardPanel.activeSelf;
                bool isVoiceDialog = voiceSettingsDialogObj != null && voiceSettingsDialogObj.activeSelf;
                bool isEditingHUD = VirtualJoystickTouchUI.Instance != null && VirtualJoystickTouchUI.Instance.IsEditingHUDLayout;

                ThirdPersonCamera.isUIPanelOpen = isTyping || isDashboard || isVoiceDialog || isEditingHUD;

                if (!Application.isMobilePlatform)
                {
                    bool shouldShowCursor = isLobbyCursorUnlocked || isTyping || isDashboard || isVoiceDialog || isEditingHUD;
                    if (shouldShowCursor)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
            }
            else
            {
                bool isTyping = IsTypingChat();
                bool isDashboard = GameSettingsManager.Instance != null
                    && GameSettingsManager.Instance.dashboardPanel != null
                    && GameSettingsManager.Instance.dashboardPanel.activeSelf;
                bool isVoiceDialog = voiceSettingsDialogObj != null && voiceSettingsDialogObj.activeSelf;
                bool isLobbyBox = lobbyPanelObj != null && lobbyPanelObj.activeSelf;
                bool isEditingHUD = VirtualJoystickTouchUI.Instance != null && VirtualJoystickTouchUI.Instance.IsEditingHUDLayout;

                if (!Application.isMobilePlatform)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                ThirdPersonCamera.isUIPanelOpen = isTyping || isDashboard || isVoiceDialog || isLobbyBox || isEditingHUD;
            }
        }

        if (currentMatchState == MatchState.CountingDown)
        {
            countdownTimer -= Time.unscaledDeltaTime;
            UpdateLobbyUI();
            if (countdownTimer <= 0f)
            {
                if (client != null && client.InRoom && client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
                {
                    if (GhostActorNumbers.Count == 0) HostAssignRandomRoles();
                    BroadcastMatchState(2, 0f);
                    PublishMatchStartRoomProperties();
                    client.SendOutgoingCommands();
                }
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
            isChatInputFocused = fieldFocused;
        }

        HandleShortcuts();
#if UNITY_ANDROID
        if (string.IsNullOrEmpty(activeMicDevice) && UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            StartMicrophoneCapture();
        }
#endif
        ProcessMicrophoneAndSendVoice();
        UpdateRemotePlayersInterpolation();
        UpdateStatusUI();
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
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
        try
        {
            if (!string.IsNullOrEmpty(activeMicDevice) && Microphone.IsRecording(activeMicDevice))
            {
                Microphone.End(activeMicDevice);
            }
        }
        catch { }
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
        bool isChatTyping = isChatInputFocused || (chatInputField != null && chatInputField.isFocused);
        bool isLobbyTyping = (lobbyPlayerNameInput != null && lobbyPlayerNameInput.isFocused) || (lobbyRoomNameInput != null && lobbyRoomNameInput.isFocused);
        return isChatTyping || isLobbyTyping;
    }

    [Obsolete]
    public void OnConnected() { }

    public void OnConnectedToMaster()
    {
        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (inLobbyScene && !hasUserRequestedRoom)
        {
            AddSystemMessage($"Đã kết nối máy chủ Photon ({client.CurrentRegion})! Hãy Tạo phòng hoặc Tham gia phòng.");
            return;
        }

        if (isAutoMatchingRandomRoom)
        {
            AddSystemMessage($"Đã kết nối Photon ({client.CurrentRegion})! Đang tìm phòng đang mở...");
            client.OpJoinRandomRoom();
            return;
        }

        AddSystemMessage($"Đã kết nối Photon ({client.CurrentRegion})! Đang vào phòng '{roomName}'...");
        JoinOrCreateTargetRoom();
    }

    private void JoinOrCreateTargetRoom()
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = "88888";
        }

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

    private void BroadcastLocalReadyState()
    {
        if (client != null && client.InRoom && client.LocalPlayer != null)
        {
            int myActor = client.LocalPlayer.ActorNumber;
            ReadyPlayerDict[myActor] = IsLocalPlayerReady;
            object[] payload = new object[] { myActor, IsLocalPlayerReady };
            RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.All };
            client.OpRaiseEvent(EVENT_PLAYER_READY, payload, args, SendOptions.SendReliable);
        }
    }

    public void OnJoinedRoom()
    {
        isAutoMatchingRandomRoom = false;
        if (client.CurrentRoom != null && !string.IsNullOrEmpty(client.CurrentRoom.Name))
        {
            roomName = client.CurrentRoom.Name;
        }
        int count = client.CurrentRoom != null ? client.CurrentRoom.PlayerCount : 1;
        AddSystemMessage($"Đã vào phòng [{roomName}] ({count}/{requiredPlayersToStart} người). Nhấn [Enter] để Chat, [V] để Mic, [H] để ẩn Chat!");
        BroadcastLocalReadyState();
        CheckRoomPropertiesForMatchStart();
        if (!IsGameStarted)
        {
            CheckLobbyPlayerCount();
        }

        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (!inLobbyScene)
        {
            isLobbyCursorUnlocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ThirdPersonCamera.isUIPanelOpen = false;
        }
        if (SurvivalTimerUI.Instance != null)
        {
            SurvivalTimerUI.Instance.KeepLocalPlayerAsHuman();
        }
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

    public void OnJoinRandomFailed(short returnCode, string message)
    {
        isAutoMatchingRandomRoom = false;
        if (string.IsNullOrWhiteSpace(roomName) || roomName == "GhostMaze_Online")
        {
            roomName = "88888";
        }
        AddSystemMessage($"Chưa có phòng nào đang mở, tự động tạo phòng [{roomName}]...");
        JoinOrCreateTargetRoom();
    }

    public void OnLeftRoom()
    {
        ClearRemotePlayers();
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        ResetMatchLobbyState();
    }

    public void OnDisconnected(DisconnectCause cause)
    {
        AddSystemMessage($"Mất kết nối Photon ({cause}). Tự động kết nối lại sau 5s...");
        reconnectTimer = Time.unscaledTime + 5f;
        ClearRemotePlayers();
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene")
        {
            ResetMatchLobbyState();
        }
    }

    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        string nick = !string.IsNullOrEmpty(newPlayer.NickName) ? newPlayer.NickName : $"NgườiChơi_{newPlayer.ActorNumber}";
        AddSystemMessage($"🟢 {nick} đã tham gia phòng!");
        BroadcastLocalReadyState();
        CheckLobbyPlayerCount();

        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (!inLobbyScene)
        {
            isLobbyCursorUnlocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ThirdPersonCamera.isUIPanelOpen = false;
        }
        if (SurvivalTimerUI.Instance != null && !IsGameStarted)
        {
            SurvivalTimerUI.Instance.KeepLocalPlayerAsHuman();
        }
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        string nick = !string.IsNullOrEmpty(otherPlayer.NickName) ? otherPlayer.NickName : $"NgườiChơi_{otherPlayer.ActorNumber}";
        AddSystemMessage($"🔴 {nick} đã rời phòng.");
        ReadyPlayerDict.Remove(otherPlayer.ActorNumber);

        if (remotePlayers.TryGetValue(otherPlayer.ActorNumber, out var av))
        {
            if (av.rootObj != null) Destroy(av.rootObj);
            remotePlayers.Remove(otherPlayer.ActorNumber);
        }
        CheckLobbyPlayerCount();

        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (!inLobbyScene)
        {
            isLobbyCursorUnlocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ThirdPersonCamera.isUIPanelOpen = false;
        }
    }

    public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        CheckRoomPropertiesForMatchStart();
    }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }
    public void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!IsGameStarted && !isTransitioningToMatchScene)
        {
            CheckAllPlayersConfirmedAndStart();
            CheckLobbyPlayerCount();
        }
    }

    // =========================================================================
    // 2. NHẮN TIN TRỰC TUYẾN (TEXT CHAT) & ĐÀM THOẠI GIỌNG NÓI (VOICE CHAT)
    // =========================================================================
    private bool ReadAltToggleDown()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.leftAltKey.wasPressedThisFrame || kb.rightAltKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)) return true;
#endif
        return false;
    }

    private bool ReadF1KeyDown()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F1)) return true;
#endif
        return false;
    }

    private void HandleShortcuts()
    {
        bool isCurrentlyTyping = isChatInputFocused || (chatInputField != null && chatInputField.isFocused);

        bool enterPressed = false;
        bool escPressed = false;
        bool micTogglePressed = false;
        bool f1Pressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        bool toggleChatPressed = false;
        bool toggleVoiceSettingsPressed = false;
        if (kb != null)
        {
            enterPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            escPressed = kb.escapeKey.wasPressedThisFrame;
            micTogglePressed = kb.vKey != null && kb.vKey.wasPressedThisFrame;
            toggleChatPressed = kb.hKey != null && kb.hKey.wasPressedThisFrame;
            toggleVoiceSettingsPressed = kb.kKey != null && kb.kKey.wasPressedThisFrame;
            if (kb.f1Key != null && kb.f1Key.wasPressedThisFrame) f1Pressed = true;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!micTogglePressed) micTogglePressed = Input.GetKeyDown(toggleMicKey);
        if (!toggleChatPressed) toggleChatPressed = Input.GetKeyDown(toggleChatKey);
        if (!toggleVoiceSettingsPressed) toggleVoiceSettingsPressed = Input.GetKeyDown(toggleVoiceSettingsKey);
        if (!enterPressed) enterPressed = Input.GetKeyDown(openChatKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!escPressed) escPressed = Input.GetKeyDown(KeyCode.Escape);
        if (!f1Pressed && Input.GetKeyDown(KeyCode.F1)) f1Pressed = true;
#endif
#else
        enterPressed = Input.GetKeyDown(openChatKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
        escPressed = Input.GetKeyDown(KeyCode.Escape);
        micTogglePressed = Input.GetKeyDown(toggleMicKey);
        bool toggleChatPressed = Input.GetKeyDown(toggleChatKey);
        bool toggleVoiceSettingsPressed = Input.GetKeyDown(toggleVoiceSettingsKey);
        if (Input.GetKeyDown(KeyCode.F1)) f1Pressed = true;
#endif

        if (f1Pressed)
        {
            if (isChatMinimized) SetChatMinimized(false);
            SetChatFocus(!isChatInputFocused);
        }

        if (toggleVoiceSettingsPressed && !isCurrentlyTyping)
        {
            ToggleVoiceSettingsDialog();
        }

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
                SetChatFocus(false);
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
            if (chatPanelRect != null) chatPanelRect.sizeDelta = new Vector2(390f, 40f);
            if (chatHistoryText != null) chatHistoryText.gameObject.SetActive(false);
            if (chatInputRow != null) chatInputRow.SetActive(false);
            if (speakingIndicatorText != null) speakingIndicatorText.gameObject.SetActive(false);
            if (minimizeButtonText != null) minimizeButtonText.text = "▲";
        }
        else
        {
            if (chatPanelRect != null) chatPanelRect.sizeDelta = new Vector2(390f, 80f);
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
            if (EventSystem.current != null && !EventSystem.current.alreadySelecting) chatInputField.Select();
        }
        else
        {
            chatInputField.DeactivateInputField();
            if (EventSystem.current != null && !EventSystem.current.alreadySelecting && (EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject == chatInputField.gameObject))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (!isDashboardOpen && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene")
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
        try
        {
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                AddSystemMessage("Đang yêu cầu cấp quyền Micro trên thiết bị Android...");
                return;
            }
#endif
            // 1. Kiểm tra sự tồn tại của thiết bị Microphone trên máy tính / điện thoại
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
                if (!string.IsNullOrEmpty(activeMicDevice) && Microphone.IsRecording(activeMicDevice))
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
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhotonVoice] Không thể chuyển trạng thái Microphone: {ex.Message}");
            isMicMuted = true;
            UpdateStatusUI();
        }
    }

    private void SetupVoicePlayback()
    {
        voicePlaybackSource = gameObject.GetComponent<AudioSource>();
        if (voicePlaybackSource == null)
        {
            voicePlaybackSource = gameObject.AddComponent<AudioSource>();
        }
        voicePlaybackSource.spatialBlend = 0f; // Âm thanh Voice Chat rõ ràng toàn phòng
        voicePlaybackSource.loop = true;
        voicePlaybackSource.volume = 1.0f;
        voicePlaybackSource.playOnAwake = true;

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
            if (micClip != null)
            {
                Debug.Log($"<color=#2ECC71>[PhotonVoice] Micro sẵn sàng: {activeMicDevice} ({VOICE_SAMPLE_RATE}Hz)</color>");
            }
            else
            {
                nextMicRetryTime = Time.unscaledTime + 5f;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhotonVoice] Lỗi khởi động Microphone: {ex.Message}");
            activeMicDevice = null;
            micClip = null;
            nextMicRetryTime = Time.unscaledTime + 5f;
        }
    }

    /// <summary>
    /// Đọc an toàn các mẫu âm thanh từ vòng lặp đệm của Microphone, tự động chia hai nửa nếu vòng qua cuối AudioClip
    /// </summary>
    private void SafeGetMicData(AudioClip clip, float[] destBuffer, int offsetSamples, int totalClipSamples)
    {
        if (clip == null || destBuffer == null || destBuffer.Length == 0 || totalClipSamples <= 0) return;
        try
        {
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
        catch { }
    }

    private void ProcessMicrophoneAndSendVoice()
    {
        if (isMicMuted) return;

        try
        {
            if (micClip == null || string.IsNullOrEmpty(activeMicDevice) || !Microphone.IsRecording(activeMicDevice))
            {
                if (Time.unscaledTime >= nextMicRetryTime)
                {
                    nextMicRetryTime = Time.unscaledTime + 5f;
                    StartMicrophoneCapture();
                }
                return;
            }

            int currentPos = Microphone.GetPosition(activeMicDevice);
            if (currentPos < 0) return;

            int clipSamples = micClip.samples;
            if (clipSamples <= 0) return;

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
                currentMicRMS = rms;

                bool isVoiceDetected = rms > vadThreshold;
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
                        // Khuếch đại âm lượng mic theo micGain
                        if (Mathf.Abs(micGain - 1.0f) > 0.01f)
                        {
                            for (int i = 0; i < micSampleBuffer.Length; i++)
                            {
                                micSampleBuffer[i] = Mathf.Clamp(micSampleBuffer[i] * micGain, -1.0f, 1.0f);
                            }
                        }

                        byte[] encoded = EncodeMuLaw(micSampleBuffer);
                        bool localIsGhost = IsLocalPlayerGhostRole();
                        bool teamIsolated = IsVoiceTeamIsolatedNow();
                        object[] voicePayload = new object[] { playerName, encoded, localIsGhost, teamIsolated };

                        if (teamIsolated && client.CurrentRoom != null && client.CurrentRoom.Players != null && GhostActorNumbers.Count > 0)
                        {
                            int myActor = client.LocalPlayer != null ? client.LocalPlayer.ActorNumber : -1;
                            voiceTargetActorBuffer.Clear();
                            foreach (var kvp in client.CurrentRoom.Players)
                            {
                                int actorNr = kvp.Key;
                                if (actorNr == myActor) continue;
                                bool peerIsGhost = GhostActorNumbers.Contains(actorNr);
                                if (peerIsGhost == localIsGhost)
                                {
                                    voiceTargetActorBuffer.Add(actorNr);
                                }
                            }

                            // Sau 20s trốn: Chỉ gửi gói tin Voice cho đồng đội cùng phe (Ma -> Ma, Người -> Người)
                            if (voiceTargetActorBuffer.Count > 0)
                            {
                                RaiseEventArgs teamArgs = new RaiseEventArgs { TargetActors = voiceTargetActorBuffer.ToArray() };
                                client.OpRaiseEvent(EVENT_VOICE_PACKET, voicePayload, teamArgs, SendOptions.SendUnreliable);
                            }
                        }
                        else
                        {
                            RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
                            client.OpRaiseEvent(EVENT_VOICE_PACKET, voicePayload, args, SendOptions.SendUnreliable);
                        }
                    }
                }
            }
        }
        catch { }
    }

    public bool IsVoiceTeamIsolatedNow()
    {
        return SurvivalTimerUI.Instance != null
            && SurvivalTimerUI.Instance.currentPhase == SurvivalTimerUI.MatchPhase.Hunting_3Min;
    }

    public bool IsLocalPlayerGhostRole()
    {
        if (client != null && client.InRoom && client.LocalPlayer != null && GhostActorNumbers.Count > 0)
        {
            return GhostActorNumbers.Contains(client.LocalPlayer.ActorNumber);
        }
        if (SurvivalTimerUI.Instance != null)
        {
            return SurvivalTimerUI.Instance.localRole == SurvivalTimerUI.PlayerRole.Ghost;
        }
        return false;
    }

    private bool IsActorGhostRole(int actorNr, object[] payloadArr)
    {
        if (GhostActorNumbers.Count > 0)
        {
            return GhostActorNumbers.Contains(actorNr);
        }
        if (payloadArr != null && payloadArr.Length >= 3 && payloadArr[2] is bool senderGhost)
        {
            return senderGhost;
        }
        if (remotePlayers.TryGetValue(actorNr, out var av) && av != null)
        {
            return av.isGhostForm;
        }
        return false;
    }

    public void OnEnterHuntingVoiceIsolation()
    {
        remoteSpeakerName = "";
        remoteSpeakingTimer = 0f;

        if (playbackRingClip != null)
        {
            float[] silence = new float[PLAYBACK_RING_SAMPLES];
            playbackRingClip.SetData(silence, 0);
        }

        bool amIGhost = IsLocalPlayerGhostRole();
        if (amIGhost)
        {
            AddSystemMessage("🔒 [VOICE PHE MA] Đã hết 20s trốn! Từ lúc này chỉ Phe MA mới nghe được mic của nhau.");
        }
        else
        {
            AddSystemMessage("🔒 [VOICE PHE NGƯỜI] Đã hết 20s trốn! Từ lúc này chỉ Phe NGƯỜI mới nghe được mic của nhau.");
        }
    }

    public void OnExitHuntingVoiceIsolation(bool notifyEnd)
    {
        remoteSpeakerName = "";
        remoteSpeakingTimer = 0f;
        if (notifyEnd)
        {
            AddSystemMessage("🔊 [VOICE KÊNH CHUNG] Trận đấu kết thúc — Tất cả người chơi có thể nghe lại mic của nhau!");
        }
    }

    private void PlayReceivedVoicePacket(string speaker, byte[] encodedBytes)
    {
        if (encodedBytes == null || encodedBytes.Length == 0) return;

        if (voicePlaybackSource == null || playbackRingClip == null)
        {
            SetupVoicePlayback();
        }
        if (voicePlaybackSource != null)
        {
            voicePlaybackSource.volume = incomingVoiceVolume;
            if (!voicePlaybackSource.isPlaying)
            {
                voicePlaybackSource.Play();
            }
        }
        if (playbackRingClip == null || voicePlaybackSource == null) return;

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
                // Nhận danh sách Ma Bí Ngô được đính kèm trực tiếp trong gói MatchState
                if (arr.Length >= 3 && arr[2] is int[] ghostInts)
                {
                    GhostActorNumbers.Clear();
                    GhostActorNumbers.AddRange(ghostInts);
                    SyncRoleWithSurvivalTimer();
                }

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
        else if (code == EVENT_ROLE_ASSIGN)
        {
            GhostActorNumbers.Clear();
            if (photonEvent.CustomData is object[] gArr)
            {
                for (int i = 0; i < gArr.Length; i++)
                {
                    GhostActorNumbers.Add(Convert.ToInt32(gArr[i]));
                }
            }
            else if (photonEvent.CustomData is int[] gInts)
            {
                GhostActorNumbers.AddRange(gInts);
            }
            SyncRoleWithSurvivalTimer();
        }
        else if (code == EVENT_PLAYER_READY)
        {
            if (photonEvent.CustomData is object[] rArr && rArr.Length >= 2)
            {
                int actorNr = Convert.ToInt32(rArr[0]);
                bool rState = Convert.ToBoolean(rArr[1]);
                ReadyPlayerDict[actorNr] = rState;
                CheckAllPlayersConfirmedAndStart();
                UpdateLobbyUI();
            }
        }
        else if (code == EVENT_GHOST_ATTACK)
        {
            if (photonEvent.CustomData is object[] atkArr && atkArr.Length >= 5)
            {
                int targetActor = Convert.ToInt32(atkArr[0]);
                float dmg = Convert.ToSingle(atkArr[1]);
                float ox = Convert.ToSingle(atkArr[2]);
                float oy = Convert.ToSingle(atkArr[3]);
                float oz = Convert.ToSingle(atkArr[4]);

                int myActor = (client != null && client.LocalPlayer != null) ? client.LocalPlayer.ActorNumber : -1;
                if (targetActor == -1 || targetActor == myActor)
                {
                    bool amIGhost = (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.localRole == SurvivalTimerUI.PlayerRole.Ghost);
                    if (!amIGhost && PlayerHealth.Instance != null && !PlayerHealth.Instance.isDead)
                    {
                        Vector3 attackOrigin = new Vector3(ox, oy, oz);
                        if (Vector3.Distance(PlayerHealth.Instance.transform.position, attackOrigin) <= 3.6f)
                        {
                            PlayerHealth.Instance.TakeDamage(dmg, attackOrigin);
                        }
                    }
                }
            }
        }
        else if (code == EVENT_VOICE_PACKET)
        {
            if (photonEvent.CustomData is object[] arr && arr.Length >= 2)
            {
                int senderActor = photonEvent.Sender;
                bool isolateVoice = IsVoiceTeamIsolatedNow() || (arr.Length >= 4 && arr[3] is bool bIso && bIso);
                if (isolateVoice)
                {
                    bool amIGhost = IsLocalPlayerGhostRole();
                    bool senderIsGhost = IsActorGhostRole(senderActor, arr);
                    // Sau 20s trốn: Ma chỉ nghe được mic Ma, Người chỉ nghe được mic Người
                    if (amIGhost != senderIsGhost)
                    {
                        return;
                    }
                }

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
                bool isGhostForm = syncArr.Length >= 6 && Convert.ToBoolean(syncArr[5]);

                UpdateOrCreateRemoteAvatar(senderActor, rName, new Vector3(px, py, pz), Quaternion.Euler(0f, ry, 0f), isGhostForm);
            }
        }
    }

    public void TrySendGhostAttackHit(Vector3 originPos, float maxReach, float damage)
    {
        if (client == null || !client.InRoom) return;

        foreach (var kvp in remotePlayers)
        {
            int actorNr = kvp.Key;
            var av = kvp.Value;
            if (av == null || av.rootObj == null || av.isGhostForm) continue;

            float dist = Vector3.Distance(originPos, av.rootObj.transform.position);
            if (dist <= maxReach + 0.6f)
            {
                object[] payload = new object[] { actorNr, damage, originPos.x, originPos.y, originPos.z };
                RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
                client.OpRaiseEvent(EVENT_GHOST_ATTACK, payload, args, SendOptions.SendReliable);
            }
        }
    }

    private bool IsLocalPlayerInGhostForm(out Transform activeTransform)
    {
        if (SurvivalTimerUI.Instance != null
            && SurvivalTimerUI.Instance.CachedGhostVessel != null
            && SurvivalTimerUI.Instance.CachedGhostVessel.gameObject.activeInHierarchy
            && SurvivalTimerUI.Instance.CachedGhostVessel.isPlayerControlled)
        {
            activeTransform = SurvivalTimerUI.Instance.CachedGhostVessel.transform;
            return true;
        }
        if (ThirdPersonPlayerController.Instance != null)
        {
            activeTransform = ThirdPersonPlayerController.Instance.transform;
            return false;
        }
        activeTransform = null;
        return false;
    }

    private void UpdateRemotePlayersInterpolation()
    {
        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene";
        if (!IsGameStarted || inLobbyScene || isTransitioningToMatchScene)
        {
            return;
        }

        if (client != null && client.InRoom && Time.unscaledTime >= nextSyncTime)
        {
            nextSyncTime = Time.unscaledTime + 0.08f; // Đồng bộ 12.5 lần/giây mượt mà
            bool isGhostForm = IsLocalPlayerInGhostForm(out Transform pt);
            if (pt != null)
            {
                Vector3 p = pt.position;
                float ry = pt.eulerAngles.y;
                object[] syncData = new object[] { playerName, p.x, p.y, p.z, ry, isGhostForm };
                RaiseEventArgs args = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
                SendOptions opts = needReliableInitialPlayerSync ? SendOptions.SendReliable : SendOptions.SendUnreliable;
                client.OpRaiseEvent(EVENT_PLAYER_SYNC, syncData, args, opts);
                if (needReliableInitialPlayerSync)
                {
                    needReliableInitialPlayerSync = false;
                    client.SendOutgoingCommands();
                }
            }
        }

        foreach (var kvp in remotePlayers)
        {
            var av = kvp.Value;
            if (av.rootObj == null) continue;

            Vector3 prevPos = av.rootObj.transform.position;
            av.rootObj.transform.position = Vector3.Lerp(prevPos, av.targetPos, Time.deltaTime * 12f);
            av.rootObj.transform.rotation = Quaternion.Slerp(av.rootObj.transform.rotation, av.targetRot, Time.deltaTime * 12f);

            // Đồng bộ kích thước avatar từ xa khớp 100% với kích thước người chơi thực tế trong Scene
            Vector3 expectedScale = av.isGhostForm
                ? ((SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.CachedGhostVessel != null) ? SurvivalTimerUI.Instance.CachedGhostVessel.transform.lossyScale : Vector3.one)
                : ((ThirdPersonPlayerController.Instance != null && ThirdPersonPlayerController.Instance.transform.lossyScale.sqrMagnitude > 0.0001f) ? ThirdPersonPlayerController.Instance.transform.lossyScale : new Vector3(0.1f, 0.1f, 0.1f));
            av.rootObj.transform.localScale = expectedScale;

            if (av.animator != null)
            {
                float moveSpeed = Vector3.Distance(prevPos, av.targetPos) * 5.0f;
                try
                {
                    av.animator.SetFloat("Speed", moveSpeed);
                }
                catch { }
            }
        }
    }

    private void UpdateOrCreateRemoteAvatar(int actorNr, string nickName, Vector3 pos, Quaternion rot, bool isGhostForm = false)
    {
        if (!remotePlayers.TryGetValue(actorNr, out var av) || av.rootObj == null || av.isGhostForm != isGhostForm)
        {
            bool isFirstSpawn = (av == null || av.rootObj == null);
            if (av != null && av.rootObj != null)
            {
                Destroy(av.rootObj);
            }

            GameObject root = new GameObject($"RemotePlayer_{actorNr}_{nickName}_{(isGhostForm ? "Ghost" : "Human")}");
            root.transform.position = pos;
            root.transform.rotation = rot;

            Vector3 targetScale = isGhostForm
                ? ((SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.CachedGhostVessel != null) ? SurvivalTimerUI.Instance.CachedGhostVessel.transform.lossyScale : Vector3.one)
                : ((ThirdPersonPlayerController.Instance != null && ThirdPersonPlayerController.Instance.transform.lossyScale.sqrMagnitude > 0.0001f) ? ThirdPersonPlayerController.Instance.transform.lossyScale : new Vector3(0.1f, 0.1f, 0.1f));
            root.transform.localScale = targetScale;

            Animator clonedAnim = null;
            bool modelCloned = false;

            if (isGhostForm && SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.CachedGhostVessel != null)
            {
                Animator srcGhostAnim = SurvivalTimerUI.Instance.CachedGhostVessel.GetComponentInChildren<Animator>(true);
                if (srcGhostAnim != null)
                {
                    GameObject visualClone = Instantiate(srcGhostAnim.gameObject, root.transform);
                    visualClone.SetActive(true);
                    visualClone.transform.localPosition = srcGhostAnim.transform.localPosition;
                    visualClone.transform.localRotation = srcGhostAnim.transform.localRotation;
                    visualClone.transform.localScale = srcGhostAnim.transform.localScale;
                    var forwarder = visualClone.GetComponent<GhostAnimationEventForwarder>();
                    if (forwarder != null) Destroy(forwarder);
                    clonedAnim = visualClone.GetComponent<Animator>();
                    modelCloned = true;
                }
            }
            else if (!isGhostForm && ThirdPersonPlayerController.Instance != null && ThirdPersonPlayerController.Instance.playerAnimator != null)
            {
                GameObject srcHumanVisual = ThirdPersonPlayerController.Instance.playerAnimator.gameObject;
                if (srcHumanVisual != ThirdPersonPlayerController.Instance.gameObject)
                {
                    GameObject visualClone = Instantiate(srcHumanVisual, root.transform);
                    visualClone.SetActive(true);
                    visualClone.transform.localPosition = srcHumanVisual.transform.localPosition;
                    visualClone.transform.localRotation = srcHumanVisual.transform.localRotation;
                    visualClone.transform.localScale = srcHumanVisual.transform.localScale;
                    var forwarder = visualClone.GetComponent<PlayerAnimationEventForwarder>();
                    if (forwarder != null) Destroy(forwarder);
                    clonedAnim = visualClone.GetComponent<Animator>();
                    modelCloned = true;
                }
            }

            if (!modelCloned)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.transform.SetParent(root.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                cap.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
                Collider col = cap.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var mr = cap.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material mat = new Material(sh);
                        mat.color = isGhostForm ? new Color(1.0f, 0.38f, 0.08f, 1f) : new Color(0.2f, 0.85f, 0.65f, 1f);
                        mr.sharedMaterial = mat;
                    }
                }
            }

            av = new RemotePlayerAvatar
            {
                rootObj = root,
                targetPos = pos,
                targetRot = rot,
                lastUpdateTime = Time.unscaledTime,
                isGhostForm = isGhostForm,
                animator = clonedAnim
            };
            remotePlayers[actorNr] = av;

            if (isFirstSpawn)
            {
                AppendChatLine($"<color=#00FF9D><b>⚡ [TRẬN ĐẤU] Người chơi [{nickName}] đã vào trận!</b></color>");
            }
            else if (isGhostForm)
            {
                AppendChatLine($"<color=#FFA502><b>🎃 [CẢNH BÁO] Người chơi [{nickName}] đã hóa thân thành MA BÍ NGÔ!</b></color>");
            }
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
        if (client != null && client.LocalPlayer != null && client.LocalPlayer.IsMasterClient)
        {
            HostAssignRandomRoles();
        }
        AddSystemMessage($"⚡ Đã đủ {requiredPlayersToStart} người chơi! Cổng mê cung sẽ mở sau {Mathf.CeilToInt(duration)} giây!");
        UpdateLobbyUI();
    }

    public void StartGameLocally()
    {
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (isTransitioningToMatchScene && activeSceneName == "LobbyScene")
        {
            return;
        }
        if (currentMatchState == MatchState.GameStarted && IsGameStarted && activeSceneName != "LobbyScene")
        {
            return;
        }

        currentMatchState = MatchState.GameStarted;
        IsGameStarted = true;
        isLobbyCursorUnlocked = false;
        needReliableInitialPlayerSync = true;
        nextSyncTime = 0f;

        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        ThirdPersonCamera.isUIPanelOpen = false;
        if (lobbyPanelObj != null) lobbyPanelObj.SetActive(false);
        if (FusionVietnameseLobbyUI.Instance != null)
        {
            FusionVietnameseLobbyUI.Instance.HideImmediatelyForMatchStart();
        }
        if (gameStartBannerObj != null)
        {
            gameStartBannerObj.SetActive(true);
            gameStartBannerTimer = 4.5f;
        }
        SyncRoleWithSurvivalTimer();
        if (SurvivalTimerUI.Instance != null)
        {
            SurvivalTimerUI.Instance.ResetTimer(true);
        }
        string curPName = !string.IsNullOrWhiteSpace(playerName) ? playerName : "Người chơi";
        AppendChatLine($"<color=#00FF9D><b>⚡ [TRẬN ĐẤU] Người chơi [{curPName}] đã vào trận!</b></color>");
        if (gameStartBannerText != null)
        {
            gameStartBannerText.text = $"🎮 Người chơi [{curPName}] đã vào trận! 🔥";
        }

        // [GhostMaze] Khi bắt đầu trận đấu ở LobbyScene -> Bơm gói mạng Photon ra ngoài trước rồi tải SampleScene bất đồng bộ để không treo kết nối
        if (activeSceneName == "LobbyScene")
        {
            ClearRemotePlayers();
            if (client != null && client.IsConnected)
            {
                client.SendOutgoingCommands();
            }
            isTransitioningToMatchScene = true;
            StartCoroutine(LoadSampleSceneWithNetworkFlushCoroutine());
        }
    }

    private System.Collections.IEnumerator LoadSampleSceneWithNetworkFlushCoroutine()
    {
        for (int i = 0; i < 3; i++)
        {
            if (client != null && client.IsConnected)
            {
                client.Service();
                client.SendOutgoingCommands();
            }
            yield return null;
        }

        AsyncOperation loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        while (loadOp != null && !loadOp.isDone)
        {
            if (client != null && client.IsConnected)
            {
                client.Service();
            }
            yield return null;
        }
        isTransitioningToMatchScene = false;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "SampleScene")
        {
            isTransitioningToMatchScene = false;
            isLobbyCursorUnlocked = false;
            ThirdPersonCamera.isUIPanelOpen = false;
            needReliableInitialPlayerSync = true;
            nextSyncTime = 0f;

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            SyncRoleWithSurvivalTimer();
        }
        else if (scene.name == "LobbyScene")
        {
            isTransitioningToMatchScene = false;
            ClearRemotePlayers();
        }
    }

    public void SyncRoleWithSurvivalTimer()
    {
        if (SurvivalTimerUI.Instance == null) return;

        if (client != null && client.InRoom && client.LocalPlayer != null && GhostActorNumbers.Count > 0)
        {
            int myActor = client.LocalPlayer.ActorNumber;
            bool amIGhost = GhostActorNumbers.Contains(myActor);
            int ghostSlot = Mathf.Max(0, GhostActorNumbers.IndexOf(myActor));
            SurvivalTimerUI.Instance.SetLocalPlayerRole(
                amIGhost ? SurvivalTimerUI.PlayerRole.Ghost : SurvivalTimerUI.PlayerRole.Human,
                ghostSlot
            );
        }
    }

    public void HostAssignRandomRoles()
    {
        GhostActorNumbers.Clear();
        if (client != null && client.InRoom && client.CurrentRoom != null && client.CurrentRoom.Players != null)
        {
            var allActors = new System.Collections.Generic.List<int>(client.CurrentRoom.Players.Keys);
            for (int i = 0; i < allActors.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, allActors.Count);
                int tmp = allActors[i];
                allActors[i] = allActors[r];
                allActors[r] = tmp;
            }

            // Trong 4 người chơi: Random đúng 2 người làm Ma Bí Ngô, 2 người làm Con Người để chạy và hú!
            int ghostCount = allActors.Count >= 4 ? 2 : (allActors.Count >= 2 ? 1 : (soloTestAsGhost ? 1 : 0));
            for (int i = 0; i < ghostCount; i++)
            {
                GhostActorNumbers.Add(allActors[i]);
            }

            object[] payload = new object[GhostActorNumbers.Count];
            for (int i = 0; i < GhostActorNumbers.Count; i++)
            {
                payload[i] = GhostActorNumbers[i];
            }

            RaiseEventArgs raiseArgs = new RaiseEventArgs { Receivers = ReceiverGroup.All };
            client.OpRaiseEvent(EVENT_ROLE_ASSIGN, payload, raiseArgs, SendOptions.SendReliable);
            SyncRoleWithSurvivalTimer();
        }
        else
        {
            if (SurvivalTimerUI.Instance != null)
            {
                SurvivalTimerUI.Instance.SetLocalPlayerRole(soloTestAsGhost ? SurvivalTimerUI.PlayerRole.Ghost : SurvivalTimerUI.PlayerRole.Human, 0);
            }
        }
    }

    public void HostForceStartGame()
    {
        HostAssignRandomRoles();
        if (client != null && client.InRoom)
        {
            BroadcastMatchState(2, 0f);
            PublishMatchStartRoomProperties();
            client.SendOutgoingCommands();
            StartGameLocally();
        }
        else
        {
            StartGameLocally();
        }
    }

    private void PublishMatchStartRoomProperties()
    {
        if (client == null || !client.InRoom || client.CurrentRoom == null) return;
        if (client.LocalPlayer == null || !client.LocalPlayer.IsMasterClient) return;

        PhotonHashtable props = new PhotonHashtable
        {
            { ROOM_PROP_MATCH_STATE, (byte)2 },
            { ROOM_PROP_GHOST_ACTORS, GhostActorNumbers.ToArray() }
        };
        client.CurrentRoom.SetCustomProperties(props);
    }

    private void CheckRoomPropertiesForMatchStart()
    {
        if (IsGameStarted || isTransitioningToMatchScene) return;
        if (client == null || !client.InRoom || client.CurrentRoom == null || client.CurrentRoom.CustomProperties == null) return;

        var props = client.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(ROOM_PROP_MATCH_STATE, out object msObj) || msObj == null) return;

        byte stateCode = Convert.ToByte(msObj);
        if (stateCode != 2) return;

        if (props.TryGetValue(ROOM_PROP_GHOST_ACTORS, out object gaObj) && gaObj is int[] ghostInts)
        {
            GhostActorNumbers.Clear();
            GhostActorNumbers.AddRange(ghostInts);
            SyncRoleWithSurvivalTimer();
        }

        StartGameLocally();
    }

    private void BroadcastMatchState(byte stateCode, float duration)
    {
        if (client == null || !client.InRoom) return;
        object[] content = new object[] { stateCode, duration, GhostActorNumbers.ToArray() };
        RaiseEventArgs raiseEventOptions = new RaiseEventArgs { Receivers = ReceiverGroup.Others };
        client.OpRaiseEvent(EVENT_MATCH_STATE, content, raiseEventOptions, SendOptions.SendReliable);
        client.SendOutgoingCommands();
    }

    private void CheckLobbyPlayerCount()
    {
        if (IsGameStarted || isTransitioningToMatchScene) return;

        if (!requireFullRoomToStart)
        {
            StartGameLocally();
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
                    if (GhostActorNumbers.Count == 0) HostAssignRandomRoles();
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

        // Khung chứa chính ở góc dưới bên trái (đặt ở Y: 335 để không bị Virtual Joystick che khuất)
        // Khung chứa chính ở góc dưới bên trái (đặt ở Y: 335 để không bị Virtual Joystick che khuất)
        GameObject panel = new GameObject("PhotonChatVoicePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(18f, 335f);
        panelRect.sizeDelta = new Vector2(390f, 80f);
        chatPanelRect = panelRect;
        VirtualJoystickTouchUI.ApplySavedRectTransform("ChatVoice", panelRect, new Vector2(18f, 335f));

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.12f, 0.78f);

        // ==========================================
        // HÀNG TRÊN: Ô Nhập Tin Nhắn + Nút Máy Bay Gửi
        // ==========================================
        chatInputRow = new GameObject("ChatInputRow");
        chatInputRow.transform.SetParent(panel.transform, false);
        RectTransform inputRowRect = chatInputRow.AddComponent<RectTransform>();
        inputRowRect.anchorMin = new Vector2(0f, 1f);
        inputRowRect.anchorMax = new Vector2(1f, 1f);
        inputRowRect.pivot = new Vector2(0.5f, 1f);
        inputRowRect.anchoredPosition = new Vector2(0f, -6f);
        inputRowRect.sizeDelta = new Vector2(-12f, 32f);

        GameObject inputBoxObj = new GameObject("ChatInputField");
        inputBoxObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform inputBoxRect = inputBoxObj.AddComponent<RectTransform>();
        inputBoxRect.anchorMin = new Vector2(0f, 0f);
        inputBoxRect.anchorMax = new Vector2(0.88f, 1f);
        inputBoxRect.offsetMin = Vector2.zero;
        inputBoxRect.offsetMax = new Vector2(-4f, 0f);

        Image inputBg = inputBoxObj.AddComponent<Image>();
        inputBg.color = new Color(0.10f, 0.14f, 0.20f, 0.90f);

        chatInputField = inputBoxObj.AddComponent<InputField>();
        chatInputField.characterLimit = 40; // Giới hạn tối đa 40 ký tự mỗi lần chat
        chatInputField.lineType = InputField.LineType.SingleLine;
        chatInputField.shouldHideMobileInput = false; // Hiện bàn phím ảo và thanh nhập liệu chuẩn trên Android
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
        phText.fontStyle = FontStyle.Normal;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.68f, 0.75f, 0.85f, 0.80f);
        phText.text = "💬 Chat [F1] - Nhập để trò chuyện...";

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

        // Nút GỬI (Biểu tượng phi cơ giấy ✈)
        GameObject sendBtnObj = new GameObject("SendButton");
        sendBtnObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform sendRect = sendBtnObj.AddComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(0.88f, 0f);
        sendRect.anchorMax = new Vector2(1f, 1f);
        sendRect.offsetMin = Vector2.zero;
        sendRect.offsetMax = Vector2.zero;

        Image sendBg = sendBtnObj.AddComponent<Image>();
        sendBg.color = new Color(0.18f, 0.45f, 0.85f, 0.95f);

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
        sendTxt.fontSize = 14;
        sendTxt.fontStyle = FontStyle.Bold;
        sendTxt.alignment = TextAnchor.MiddleCenter;
        sendTxt.color = Color.white;
        sendTxt.text = "✈";

        // ==========================================
        // HÀNG DƯỚI: 4 Nút Điều Khiển
        // 1. [Chat [F1] ●] | 2. [🎙 MIC: TẮT [V]] | 3. [🔊 VOICE] | 4. [▲]
        // ==========================================
        GameObject bottomBar = new GameObject("BottomControlBar");
        bottomBar.transform.SetParent(panel.transform, false);
        RectTransform bottomRect = bottomBar.AddComponent<RectTransform>();
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(1f, 0f);
        bottomRect.pivot = new Vector2(0.5f, 0f);
        bottomRect.anchoredPosition = new Vector2(0f, 6f);
        bottomRect.sizeDelta = new Vector2(-12f, 30f);

        // 1. Nút Chat [F1] ●
        GameObject statusBtnObj = new GameObject("ChatBadgeButton");
        statusBtnObj.transform.SetParent(bottomBar.transform, false);
        RectTransform statusRect = statusBtnObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(0.28f, 1f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = new Vector2(-2f, 0f);

        Image statusBg = statusBtnObj.AddComponent<Image>();
        statusBg.color = new Color(0.12f, 0.18f, 0.25f, 0.92f);

        Button statusBtn = statusBtnObj.AddComponent<Button>();
        statusBtn.onClick.AddListener(() =>
        {
            if (isChatMinimized) SetChatMinimized(false);
            SetChatFocus(!isChatInputFocused);
        });

        GameObject statusTxtObj = new GameObject("Text");
        statusTxtObj.transform.SetParent(statusBtnObj.transform, false);
        RectTransform strt = statusTxtObj.AddComponent<RectTransform>();
        strt.anchorMin = Vector2.zero;
        strt.anchorMax = Vector2.one;
        strt.offsetMin = Vector2.zero;
        strt.offsetMax = Vector2.zero;

        statusBadgeText = statusTxtObj.AddComponent<Text>();
        statusBadgeText.font = defaultFont;
        statusBadgeText.fontSize = 11;
        statusBadgeText.fontStyle = FontStyle.Bold;
        statusBadgeText.alignment = TextAnchor.MiddleCenter;
        statusBadgeText.color = new Color(0.35f, 0.95f, 0.60f, 1f);
        statusBadgeText.text = "Chat [F1] ●";

        // 2. Nút MIC: BẬT / TẮT [V]
        GameObject micBtnObj = new GameObject("MicToggleButton");
        micBtnObj.transform.SetParent(bottomBar.transform, false);
        RectTransform micRect = micBtnObj.AddComponent<RectTransform>();
        micRect.anchorMin = new Vector2(0.28f, 0f);
        micRect.anchorMax = new Vector2(0.68f, 1f);
        micRect.offsetMin = new Vector2(2f, 0f);
        micRect.offsetMax = new Vector2(-2f, 0f);

        micButtonBg = micBtnObj.AddComponent<Image>();
        micButtonBg.color = new Color(0.65f, 0.22f, 0.22f, 0.92f);

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
        micButtonText.fontSize = 11;
        micButtonText.fontStyle = FontStyle.Bold;
        micButtonText.alignment = TextAnchor.MiddleCenter;
        micButtonText.color = Color.white;
        micButtonText.text = "🎙 MIC: TẮT [V]";

        // 3. Nút Cài Đặt VOICE
        GameObject voiceSetBtnObj = new GameObject("VoiceSettingsButton");
        voiceSetBtnObj.transform.SetParent(bottomBar.transform, false);
        RectTransform voiceSetRect = voiceSetBtnObj.AddComponent<RectTransform>();
        voiceSetRect.anchorMin = new Vector2(0.68f, 0f);
        voiceSetRect.anchorMax = new Vector2(0.88f, 1f);
        voiceSetRect.offsetMin = new Vector2(2f, 0f);
        voiceSetRect.offsetMax = new Vector2(-2f, 0f);

        Image voiceSetBg = voiceSetBtnObj.AddComponent<Image>();
        voiceSetBg.color = new Color(0.18f, 0.48f, 0.88f, 0.92f);

        Button voiceSetBtn = voiceSetBtnObj.AddComponent<Button>();
        voiceSetBtn.onClick.AddListener(ToggleVoiceSettingsDialog);

        GameObject voiceSetTxtObj = new GameObject("Text");
        voiceSetTxtObj.transform.SetParent(voiceSetBtnObj.transform, false);
        RectTransform vstrt = voiceSetTxtObj.AddComponent<RectTransform>();
        vstrt.anchorMin = Vector2.zero;
        vstrt.anchorMax = Vector2.one;
        vstrt.offsetMin = Vector2.zero;
        vstrt.offsetMax = Vector2.zero;

        Text voiceSetTxt = voiceSetTxtObj.AddComponent<Text>();
        voiceSetTxt.font = defaultFont;
        voiceSetTxt.fontSize = 11;
        voiceSetTxt.fontStyle = FontStyle.Bold;
        voiceSetTxt.alignment = TextAnchor.MiddleCenter;
        voiceSetTxt.color = Color.white;
        voiceSetTxt.text = "🔊 VOICE";

        // 4. Nút Thu Gọn ▲ / ▼
        GameObject minBtnObj = new GameObject("MinimizeChatButton");
        minBtnObj.transform.SetParent(bottomBar.transform, false);
        RectTransform minRect = minBtnObj.AddComponent<RectTransform>();
        minRect.anchorMin = new Vector2(0.88f, 0f);
        minRect.anchorMax = new Vector2(1f, 1f);
        minRect.offsetMin = new Vector2(2f, 0f);
        minRect.offsetMax = Vector2.zero;

        Image minBg = minBtnObj.AddComponent<Image>();
        minBg.color = new Color(0.16f, 0.22f, 0.30f, 0.92f);

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

        // Dòng báo hiệu ai đang nói Voice Chat (nằm ngay trên khung)
        GameObject speakObj = new GameObject("SpeakingIndicator");
        speakObj.transform.SetParent(panel.transform, false);
        RectTransform speakRect = speakObj.AddComponent<RectTransform>();
        speakRect.anchorMin = new Vector2(0f, 1f);
        speakRect.anchorMax = new Vector2(1f, 1f);
        speakRect.pivot = new Vector2(0.5f, 0f);
        speakRect.anchoredPosition = new Vector2(0f, 2f);
        speakRect.sizeDelta = new Vector2(0f, 18f);

        speakingIndicatorText = speakObj.AddComponent<Text>();
        speakingIndicatorText.font = defaultFont;
        speakingIndicatorText.fontSize = 12;
        speakingIndicatorText.fontStyle = FontStyle.Italic;
        speakingIndicatorText.alignment = TextAnchor.MiddleLeft;
        speakingIndicatorText.color = new Color(0.3f, 1f, 0.55f, 1f);
        speakingIndicatorText.text = "";
        speakingIndicatorText.raycastTarget = false;

        // Vùng hiển thị lịch sử tin nhắn (nổi phía trên khung nhập khi có tin nhắn)
        GameObject historyObj = new GameObject("ChatHistoryText");
        historyObj.transform.SetParent(panel.transform, false);
        RectTransform histRect = historyObj.AddComponent<RectTransform>();
        histRect.anchorMin = new Vector2(0f, 1f);
        histRect.anchorMax = new Vector2(1f, 1f);
        histRect.pivot = new Vector2(0.5f, 0f);
        histRect.anchoredPosition = new Vector2(0f, 22f);
        histRect.sizeDelta = new Vector2(0f, 140f);

        chatHistoryText = historyObj.AddComponent<Text>();
        chatHistoryText.font = defaultFont;
        chatHistoryText.fontSize = 13;
        chatHistoryText.alignment = TextAnchor.LowerLeft;
        chatHistoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        chatHistoryText.verticalOverflow = VerticalWrapMode.Truncate;
        chatHistoryText.supportRichText = true;
        chatHistoryText.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        chatHistoryText.raycastTarget = false;

        // Dựng giao diện Phòng Chờ 4 người (Lobby HUD)
        BuildLobbyWaitingUI(canvas, defaultFont);

        // Dựng bảng Cài Đặt Photon Voice Nâng Cao
        BuildVoiceSettingsDialog(canvas, defaultFont);

        if (startChatMinimized)
        {
            SetChatMinimized(true);
        }
    }

    private void BuildVoiceSettingsDialog(Canvas canvas, Font defaultFont)
    {
        voiceSettingsDialogObj = new GameObject("PhotonVoiceSettingsDialog");
        voiceSettingsDialogObj.transform.SetParent(canvas.transform, false);

        RectTransform dialogRect = voiceSettingsDialogObj.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(490f, 390f);

        Image dialogBg = voiceSettingsDialogObj.AddComponent<Image>();
        dialogBg.color = new Color(0.06f, 0.10f, 0.16f, 0.96f);

        // Viền sáng xung quanh
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        Outline outline = borderObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.75f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Header Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta = new Vector2(-20f, 32f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.35f, 0.9f, 1f, 1f);
        titleText.text = "🎙️ CÀI ĐẶT PHOTON VOICE CHAT";

        // Nút Đóng [X] góc trên phải
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-8f, -8f);
        closeRect.sizeDelta = new Vector2(32f, 32f);

        Image closeBg = closeBtnObj.AddComponent<Image>();
        closeBg.color = new Color(0.85f, 0.25f, 0.25f, 0.9f);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(ToggleVoiceSettingsDialog);

        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform ctrt = closeTxtObj.AddComponent<RectTransform>();
        ctrt.anchorMin = Vector2.zero;
        ctrt.anchorMax = Vector2.one;
        ctrt.sizeDelta = Vector2.zero;
        Text closeTxt = closeTxtObj.AddComponent<Text>();
        closeTxt.font = defaultFont;
        closeTxt.fontSize = 15;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.color = Color.white;
        closeTxt.text = "✕";

        // Dòng 1: Tên Micro hiện tại + Nút Đổi Micro
        GameObject micRow = new GameObject("MicDeviceRow");
        micRow.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform micRowRect = micRow.AddComponent<RectTransform>();
        micRowRect.anchorMin = new Vector2(0.5f, 1f);
        micRowRect.anchorMax = new Vector2(0.5f, 1f);
        micRowRect.pivot = new Vector2(0.5f, 1f);
        micRowRect.anchoredPosition = new Vector2(0f, -50f);
        micRowRect.sizeDelta = new Vector2(450f, 36f);

        Image micRowBg = micRow.AddComponent<Image>();
        micRowBg.color = new Color(0.12f, 0.16f, 0.24f, 0.85f);

        GameObject devTxtObj = new GameObject("DeviceText");
        devTxtObj.transform.SetParent(micRow.transform, false);
        RectTransform devRt = devTxtObj.AddComponent<RectTransform>();
        devRt.anchorMin = new Vector2(0f, 0f);
        devRt.anchorMax = new Vector2(0.72f, 1f);
        devRt.offsetMin = new Vector2(10f, 0f);
        devRt.offsetMax = Vector2.zero;

        voiceSettingsMicDeviceText = devTxtObj.AddComponent<Text>();
        voiceSettingsMicDeviceText.font = defaultFont;
        voiceSettingsMicDeviceText.fontSize = 12;
        voiceSettingsMicDeviceText.alignment = TextAnchor.MiddleLeft;
        voiceSettingsMicDeviceText.color = new Color(0.9f, 0.95f, 1f);
        voiceSettingsMicDeviceText.text = "Micro: " + (string.IsNullOrEmpty(activeMicDevice) ? "Mặc định" : activeMicDevice);

        GameObject switchMicBtnObj = new GameObject("SwitchMicBtn");
        switchMicBtnObj.transform.SetParent(micRow.transform, false);
        RectTransform swRt = switchMicBtnObj.AddComponent<RectTransform>();
        swRt.anchorMin = new Vector2(0.74f, 0.1f);
        swRt.anchorMax = new Vector2(0.98f, 0.9f);
        swRt.offsetMin = Vector2.zero;
        swRt.offsetMax = Vector2.zero;

        Image swBg = switchMicBtnObj.AddComponent<Image>();
        swBg.color = new Color(0.20f, 0.55f, 0.85f, 0.9f);
        Button swBtn = switchMicBtnObj.AddComponent<Button>();
        swBtn.onClick.AddListener(SwitchToNextMicrophone);

        GameObject swTxtObj = new GameObject("Text");
        swTxtObj.transform.SetParent(switchMicBtnObj.transform, false);
        RectTransform swtrt = swTxtObj.AddComponent<RectTransform>();
        swtrt.anchorMin = Vector2.zero;
        swtrt.anchorMax = Vector2.one;
        swtrt.sizeDelta = Vector2.zero;
        Text swTxt = swTxtObj.AddComponent<Text>();
        swTxt.font = defaultFont;
        swTxt.fontSize = 11;
        swTxt.fontStyle = FontStyle.Bold;
        swTxt.alignment = TextAnchor.MiddleCenter;
        swTxt.color = Color.white;
        swTxt.text = "🔄 Đổi Mic";

        // Dòng 2: Thanh đo âm lượng Mic thời gian thực (VU Meter)
        GameObject vuRow = new GameObject("VuMeterRow");
        vuRow.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform vuRowRect = vuRow.AddComponent<RectTransform>();
        vuRowRect.anchorMin = new Vector2(0.5f, 1f);
        vuRowRect.anchorMax = new Vector2(0.5f, 1f);
        vuRowRect.pivot = new Vector2(0.5f, 1f);
        vuRowRect.anchoredPosition = new Vector2(0f, -94f);
        vuRowRect.sizeDelta = new Vector2(450f, 30f);

        GameObject vuLabelObj = new GameObject("Label");
        vuLabelObj.transform.SetParent(vuRow.transform, false);
        RectTransform vult = vuLabelObj.AddComponent<RectTransform>();
        vult.anchorMin = new Vector2(0f, 0f);
        vult.anchorMax = new Vector2(0.36f, 1f);
        vult.offsetMin = new Vector2(6f, 0f);
        vult.offsetMax = Vector2.zero;
        Text vuLabel = vuLabelObj.AddComponent<Text>();
        vuLabel.font = defaultFont;
        vuLabel.fontSize = 11;
        vuLabel.alignment = TextAnchor.MiddleLeft;
        vuLabel.color = new Color(0.7f, 0.85f, 1f);
        vuLabel.text = "Âm Thanh Mic (Live):";

        GameObject vuBgObj = new GameObject("VuBg");
        vuBgObj.transform.SetParent(vuRow.transform, false);
        RectTransform vuBgRt = vuBgObj.AddComponent<RectTransform>();
        vuBgRt.anchorMin = new Vector2(0.38f, 0.18f);
        vuBgRt.anchorMax = new Vector2(0.98f, 0.82f);
        vuBgRt.offsetMin = Vector2.zero;
        vuBgRt.offsetMax = Vector2.zero;
        Image vuBgImg = vuBgObj.AddComponent<Image>();
        vuBgImg.color = new Color(0.1f, 0.14f, 0.2f, 0.95f);

        GameObject vuFillObj = new GameObject("VuFill");
        vuFillObj.transform.SetParent(vuBgObj.transform, false);
        voiceSettingsVuFillRect = vuFillObj.AddComponent<RectTransform>();
        voiceSettingsVuFillRect.anchorMin = Vector2.zero;
        voiceSettingsVuFillRect.anchorMax = new Vector2(0f, 1f);
        voiceSettingsVuFillRect.offsetMin = Vector2.zero;
        voiceSettingsVuFillRect.offsetMax = Vector2.zero;
        Image vuFillImg = vuFillObj.AddComponent<Image>();
        vuFillImg.color = new Color(0.2f, 0.88f, 0.45f, 0.95f);

        // Dòng 3: Slider Âm Lượng Mic (Mic Gain)
        CreateVoiceSliderRow(
            voiceSettingsDialogObj.transform,
            defaultFont,
            name: "Row_MicGain",
            yPos: -136f,
            title: "Âm Lượng Mic (Gain):",
            min: 0.5f,
            max: 3.0f,
            initVal: micGain,
            onChanged: (val) =>
            {
                micGain = val;
                if (voiceSettingsGainValueText != null) voiceSettingsGainValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            },
            out voiceSettingsGainSlider,
            out voiceSettingsGainValueText
        );
        if (voiceSettingsGainValueText != null) voiceSettingsGainValueText.text = $"{Mathf.RoundToInt(micGain * 100)}%";

        // Dòng 4: Slider Độ Nhạy / Lọc Ồn (VAD Threshold)
        CreateVoiceSliderRow(
            voiceSettingsDialogObj.transform,
            defaultFont,
            name: "Row_VadSensitivity",
            yPos: -192f,
            title: "Ngưỡng Nhận Diện (VAD):",
            min: 0.002f,
            max: 0.030f,
            initVal: vadThreshold,
            onChanged: (val) =>
            {
                vadThreshold = val;
                if (voiceSettingsVadValueText != null)
                {
                    string desc = val < 0.006f ? "Rất nhạy" : (val > 0.015f ? "Lọc ồn cao" : "Tiêu chuẩn");
                    voiceSettingsVadValueText.text = $"{val:F3} ({desc})";
                }
            },
            out voiceSettingsVadSlider,
            out voiceSettingsVadValueText
        );
        if (voiceSettingsVadValueText != null) voiceSettingsVadValueText.text = $"{vadThreshold:F3} (Tiêu chuẩn)";

        // Dòng 5: Slider Âm Lượng Nghe Người Khác (Incoming Volume)
        CreateVoiceSliderRow(
            voiceSettingsDialogObj.transform,
            defaultFont,
            name: "Row_IncomingVoice",
            yPos: -248f,
            title: "Âm Lượng Voice Đến:",
            min: 0.0f,
            max: 1.5f,
            initVal: incomingVoiceVolume,
            onChanged: (val) =>
            {
                incomingVoiceVolume = val;
                if (voicePlaybackSource != null) voicePlaybackSource.volume = incomingVoiceVolume;
                if (voiceSettingsIncomingValueText != null) voiceSettingsIncomingValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            },
            out voiceSettingsIncomingSlider,
            out voiceSettingsIncomingValueText
        );
        if (voiceSettingsIncomingValueText != null) voiceSettingsIncomingValueText.text = $"{Mathf.RoundToInt(incomingVoiceVolume * 100)}%";

        // Dòng 6: Hàng nút phía dưới: Bật/Tắt Mic + Nút Lưu & Đóng
        GameObject botRow = new GameObject("BottomRow");
        botRow.transform.SetParent(voiceSettingsDialogObj.transform, false);
        RectTransform botRt = botRow.AddComponent<RectTransform>();
        botRt.anchorMin = new Vector2(0.5f, 0f);
        botRt.anchorMax = new Vector2(0.5f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.anchoredPosition = new Vector2(0f, 16f);
        botRt.sizeDelta = new Vector2(450f, 38f);

        // Nút toggle Mic trong Dialog
        GameObject toggleMicBtnObj = new GameObject("ToggleMicInDialogBtn");
        toggleMicBtnObj.transform.SetParent(botRow.transform, false);
        RectTransform tmbRt = toggleMicBtnObj.AddComponent<RectTransform>();
        tmbRt.anchorMin = new Vector2(0f, 0f);
        tmbRt.anchorMax = new Vector2(0.48f, 1f);
        tmbRt.offsetMin = Vector2.zero;
        tmbRt.offsetMax = Vector2.zero;
        Image tmbBg = toggleMicBtnObj.AddComponent<Image>();
        tmbBg.color = new Color(0.15f, 0.65f, 0.38f, 0.92f);
        Button tmbBtn = toggleMicBtnObj.AddComponent<Button>();
        tmbBtn.onClick.AddListener(() =>
        {
            ToggleMicrophone();
            UpdateVoiceSettingsLabels();
        });

        GameObject tmbTxtObj = new GameObject("Text");
        tmbTxtObj.transform.SetParent(toggleMicBtnObj.transform, false);
        RectTransform tmbtRt = tmbTxtObj.AddComponent<RectTransform>();
        tmbtRt.anchorMin = Vector2.zero;
        tmbtRt.anchorMax = Vector2.one;
        tmbtRt.sizeDelta = Vector2.zero;
        Text tmbTxt = tmbTxtObj.AddComponent<Text>();
        tmbTxt.font = defaultFont;
        tmbTxt.fontSize = 12;
        tmbTxt.fontStyle = FontStyle.Bold;
        tmbTxt.alignment = TextAnchor.MiddleCenter;
        tmbTxt.color = Color.white;
        tmbTxt.text = "🎙 Bật / Tắt Mic [V]";

        // Nút Đóng
        GameObject saveCloseBtnObj = new GameObject("SaveCloseBtn");
        saveCloseBtnObj.transform.SetParent(botRow.transform, false);
        RectTransform scRt = saveCloseBtnObj.AddComponent<RectTransform>();
        scRt.anchorMin = new Vector2(0.52f, 0f);
        scRt.anchorMax = new Vector2(1f, 1f);
        scRt.offsetMin = Vector2.zero;
        scRt.offsetMax = Vector2.zero;
        Image scBg = saveCloseBtnObj.AddComponent<Image>();
        scBg.color = new Color(0.2f, 0.5f, 0.85f, 0.92f);
        Button scBtn = saveCloseBtnObj.AddComponent<Button>();
        scBtn.onClick.AddListener(ToggleVoiceSettingsDialog);

        GameObject scTxtObj = new GameObject("Text");
        scTxtObj.transform.SetParent(saveCloseBtnObj.transform, false);
        RectTransform sctRt = scTxtObj.AddComponent<RectTransform>();
        sctRt.anchorMin = Vector2.zero;
        sctRt.anchorMax = Vector2.one;
        sctRt.sizeDelta = Vector2.zero;
        Text scTxt = scTxtObj.AddComponent<Text>();
        scTxt.font = defaultFont;
        scTxt.fontSize = 12;
        scTxt.fontStyle = FontStyle.Bold;
        scTxt.alignment = TextAnchor.MiddleCenter;
        scTxt.color = Color.white;
        scTxt.text = "✓ LƯU & ĐÓNG";

        // Mặc định ẩn bảng cài đặt
        voiceSettingsDialogObj.SetActive(false);
    }

    private void CreateVoiceSliderRow(
        Transform parent,
        Font font,
        string name,
        float yPos,
        string title,
        float min,
        float max,
        float initVal,
        UnityEngine.Events.UnityAction<float> onChanged,
        out Slider outSlider,
        out Text outValueText)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1f);
        rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, yPos);
        rowRt.sizeDelta = new Vector2(450f, 48f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(row.transform, false);
        RectTransform trt = titleObj.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(0.65f, 1f);
        trt.offsetMin = new Vector2(6f, 0f);
        trt.offsetMax = Vector2.zero;
        Text t = titleObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 12;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = new Color(0.85f, 0.92f, 1f);
        t.text = title;

        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(row.transform, false);
        RectTransform vrt = valObj.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.65f, 0.5f);
        vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = new Vector2(-6f, 0f);
        outValueText = valObj.AddComponent<Text>();
        outValueText.font = font;
        outValueText.fontSize = 12;
        outValueText.fontStyle = FontStyle.Bold;
        outValueText.alignment = TextAnchor.MiddleRight;
        outValueText.color = new Color(0.35f, 0.9f, 1f);
        outValueText.text = $"{initVal}";

        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        RectTransform srt = sliderObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0.45f);
        srt.offsetMin = new Vector2(6f, 0f);
        srt.offsetMax = new Vector2(-6f, 0f);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgrt = bgObj.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero;
        fart.anchorMax = Vector2.one;
        fart.offsetMin = new Vector2(4f, 0f);
        fart.offsetMax = new Vector2(-4f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.zero;
        frt.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.20f, 0.75f, 1.0f, 0.95f);

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform hart = handleArea.AddComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.offsetMin = new Vector2(6f, 0f);
        hart.offsetMax = new Vector2(-6f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform hrt = handle.AddComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(16f, 24f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        outSlider = sliderObj.AddComponent<Slider>();
        outSlider.fillRect = frt;
        outSlider.handleRect = hrt;
        outSlider.targetGraphic = handleImg;
        outSlider.direction = Slider.Direction.LeftToRight;
        outSlider.minValue = min;
        outSlider.maxValue = max;
        outSlider.value = initVal;
        outSlider.onValueChanged.AddListener(onChanged);
    }

    public void SwitchToNextMicrophone()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0) return;
        int currentIndex = -1;
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            if (Microphone.devices[i] == activeMicDevice)
            {
                currentIndex = i;
                break;
            }
        }
        int nextIndex = (currentIndex + 1) % Microphone.devices.Length;
        activeMicDevice = Microphone.devices[nextIndex];
        if (Microphone.IsRecording(activeMicDevice))
        {
            Microphone.End(activeMicDevice);
        }
        micClip = Microphone.Start(activeMicDevice, true, 2, VOICE_SAMPLE_RATE);
        lastMicSamplePos = 0;
        Debug.Log($"<color=#2ECC71>[PhotonVoice] Đã đổi Microphone sang: '{activeMicDevice}'</color>");
        UpdateVoiceSettingsLabels();
    }

    public void UpdateVoiceSettingsLabels()
    {
        if (voiceSettingsMicDeviceText != null)
        {
            string devName = string.IsNullOrEmpty(activeMicDevice) ? "Không có Micro" : activeMicDevice;
            voiceSettingsMicDeviceText.text = $"Micro: {devName}";
        }
        if (voiceSettingsGainValueText != null)
        {
            voiceSettingsGainValueText.text = $"{Mathf.RoundToInt(micGain * 100)}%";
        }
        if (voiceSettingsVadValueText != null)
        {
            string desc = vadThreshold < 0.006f ? "Rất nhạy" : (vadThreshold > 0.015f ? "Lọc ồn cao" : "Chuẩn");
            voiceSettingsVadValueText.text = $"{vadThreshold:F3} ({desc})";
        }
        if (voiceSettingsIncomingValueText != null)
        {
            voiceSettingsIncomingValueText.text = $"{Mathf.RoundToInt(incomingVoiceVolume * 100)}%";
        }
    }

    public void ToggleVoiceSettingsDialog()
    {
        if (voiceSettingsDialogObj != null)
        {
            bool nextState = !voiceSettingsDialogObj.activeSelf;
            voiceSettingsDialogObj.SetActive(nextState);
            if (nextState)
            {
                UpdateVoiceSettingsLabels();
                if (!Application.isMobilePlatform)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                ThirdPersonCamera.isUIPanelOpen = true;
            }
            else
            {
                bool isDashboard = GameSettingsManager.Instance != null
                    && GameSettingsManager.Instance.dashboardPanel != null
                    && GameSettingsManager.Instance.dashboardPanel.activeSelf;
                ThirdPersonCamera.isUIPanelOpen = IsTypingChat() || isDashboard;
            }
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
        lobbyRect.anchoredPosition = new Vector2(0f, -12f);
        lobbyRect.sizeDelta = new Vector2(680f, 130f);

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
        lfTxt.text = "Bắt Đầu ▶ [F1]";

        GameObject hintObj = new GameObject("LobbyKeyHintText");
        hintObj.transform.SetParent(lobbyPanelObj.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 4f);
        hintRect.sizeDelta = new Vector2(-20f, 18f);

        Text hintTxt = hintObj.AddComponent<Text>();
        hintTxt.font = defaultFont;
        hintTxt.fontSize = 11;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(0.65f, 0.85f, 0.95f, 0.9f);
        hintTxt.text = "🎮 [WASD]: Chạy nhảy tự do khắp đảo  |  [F1]: Bắt đầu ngay (Host)  |  [Alt]: Mở chuột  |  [Enter]: Chat  |  [V]: Mic";

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
        if (statusBadgeText != null)
        {
            if (hasUnreadChat)
            {
                statusBadgeText.color = new Color(1f, 0.75f, 0.15f, 1f);
                statusBadgeText.text = "Chat [F1] ● !";
            }
            else
            {
                statusBadgeText.color = new Color(0.35f, 0.95f, 0.60f, 1f);
                statusBadgeText.text = "Chat [F1] ●";
            }
        }

        if (micButtonText != null && micButtonBg != null)
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                micButtonText.text = "🚫 MIC: TRỐNG";
                micButtonBg.color = new Color(0.40f, 0.40f, 0.45f, 0.88f);
            }
            else if (isMicMuted)
            {
                micButtonText.text = "🎙 MIC: TẮT [V]";
                micButtonBg.color = new Color(0.65f, 0.22f, 0.22f, 0.92f);
            }
            else
            {
                micButtonText.text = "🎙 MIC: BẬT [V]";
                micButtonBg.color = IsLocalSpeaking
                    ? new Color(0.18f, 0.88f, 0.45f, 1f)
                    : new Color(0.15f, 0.58f, 0.35f, 0.92f);
            }
        }

        if (speakingIndicatorText != null)
        {
            bool teamIsolated = IsVoiceTeamIsolatedNow();
            bool amIGhost = IsLocalPlayerGhostRole();
            string teamTag = teamIsolated ? (amIGhost ? " [Phe MA]" : " [Phe NGƯỜI]") : "";

            if (Time.unscaledTime < remoteSpeakingTimer && !string.IsNullOrEmpty(remoteSpeakerName))
            {
                speakingIndicatorText.color = teamIsolated && amIGhost ? new Color(1f, 0.55f, 0.25f, 1f) : new Color(0.3f, 1f, 0.55f, 1f);
                speakingIndicatorText.text = $"🔊 {remoteSpeakerName}{teamTag} đang nói...";
            }
            else if (IsLocalSpeaking)
            {
                speakingIndicatorText.color = teamIsolated && amIGhost ? new Color(1f, 0.65f, 0.28f, 1f) : new Color(0.3f, 1f, 0.55f, 1f);
                speakingIndicatorText.text = $"🎙 Bạn đang nói{teamTag}...";
            }
            else if (teamIsolated)
            {
                speakingIndicatorText.color = amIGhost ? new Color(1f, 0.58f, 0.25f, 0.9f) : new Color(0.45f, 0.92f, 1f, 0.9f);
                speakingIndicatorText.text = amIGhost ? "🔒 Kênh Mic: Chỉ Phe MA" : "🔒 Kênh Mic: Chỉ Phe NGƯỜI";
            }
            else
            {
                speakingIndicatorText.text = "";
            }
        }

        if (voiceSettingsDialogObj != null && voiceSettingsDialogObj.activeSelf)
        {
            float vu = Mathf.Clamp01(currentMicRMS * 35f);
            if (voiceSettingsVuFillRect != null)
            {
                voiceSettingsVuFillRect.anchorMax = new Vector2(vu, 1f);
            }
        }
    }
    private InputField CreateLobbyInputField(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string placeholderText, string defaultText, Font font)
    {
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);
        RectTransform rt = box.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image bg = box.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.15f, 0.22f, 0.95f);

        InputField ifield = box.AddComponent<InputField>();
        ifield.characterLimit = 30;
        ifield.lineType = InputField.LineType.SingleLine;

        GameObject phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(box.transform, false);
        RectTransform phRt = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(10f, 2f);
        phRt.offsetMax = new Vector2(-10f, -2f);

        Text phTxt = phGo.AddComponent<Text>();
        phTxt.font = font;
        phTxt.fontSize = 13;
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft;
        phTxt.color = new Color(0.6f, 0.68f, 0.78f, 0.75f);
        phTxt.text = placeholderText;

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(box.transform, false);
        RectTransform tRt = txtGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(10f, 2f);
        tRt.offsetMax = new Vector2(-10f, -2f);

        Text txt = txtGo.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 13;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.supportRichText = false;

        ifield.textComponent = txt;
        ifield.placeholder = phTxt;
        ifield.text = defaultText;

        return ifield;
    }

    private Button CreateLobbyButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color bgColor, string label, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image bg = btnGo.AddComponent<Image>();
        bg.color = bgColor;

        Button btn = btnGo.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform tRt = txtGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        Text txt = txtGo.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 13;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;

        return btn;
    }
}
