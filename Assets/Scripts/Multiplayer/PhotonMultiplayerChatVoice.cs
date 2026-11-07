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
    [Tooltip("(Khuyên dùng) App ID loại REALTIME riêng cho Chat/Voice/Đồng bộ vị trí. Tạo miễn phí tại dashboard.photonengine.com. Để trống = dùng App ID Fusion ở trên.")]
    public string photonRealtimeAppId = "";
    [Tooltip("Tên plugin của Photon Fusion 2 trên server (chỉ dùng khi kết nối bằng App ID Fusion)")]
    public string fusionPluginName = "FusionPlugin21";
    public string roomName = "GhostMaze_Online";
    public string playerName = "";
    public byte maxPlayersPerRoom = 16;

    [Header("2. Phím Tắt Chat & Voice")]
    public KeyCode toggleMicKey = KeyCode.V;
    public KeyCode openChatKey = KeyCode.Return;

    // Mã sự kiện mạng Photon (Event Codes)
    private const byte EVENT_CHAT_MSG = 101;
    private const byte EVENT_VOICE_PACKET = 102;
    private const byte EVENT_PLAYER_SYNC = 103;

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
    private bool isLocalSpeaking = false;
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

        HandleShortcuts();
        ProcessMicrophoneAndSendVoice();
        UpdateRemotePlayersInterpolation();
        UpdateStatusUI();
    }

    private void OnDestroy()
    {
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
        AddSystemMessage($"Đã vào phòng [{roomName}] ({count}/{maxPlayersPerRoom} người). Nhấn [Enter] để Chat, [V] để bật/tắt Mic!");
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
    }

    public void OnDisconnected(DisconnectCause cause)
    {
        AddSystemMessage($"Mất kết nối Photon ({cause}). Tự động kết nối lại sau 5s...");
        reconnectTimer = Time.unscaledTime + 5f;
        ClearRemotePlayers();
    }

    public void OnRegionListReceived(RegionHandler regionHandler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string debugMessage) { }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        string nick = !string.IsNullOrEmpty(newPlayer.NickName) ? newPlayer.NickName : $"NgườiChơi_{newPlayer.ActorNumber}";
        AddSystemMessage($"🟢 {nick} đã tham gia phòng!");
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
    }

    public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }
    public void OnMasterClientSwitched(Player newMasterClient) { }

    // =========================================================================
    // 2. NHẮN TIN TRỰC TUYẾN (TEXT CHAT) & ĐÀM THOẠI GIỌNG NÓI (VOICE CHAT)
    // =========================================================================
    private void HandleShortcuts()
    {
        bool inputFocused = chatInputField != null && chatInputField.isFocused;

        bool enterPressed = false;
        bool escPressed = false;
        bool micTogglePressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            enterPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            escPressed = kb.escapeKey.wasPressedThisFrame;
            micTogglePressed = kb.vKey.wasPressedThisFrame;
        }
#else
        enterPressed = Input.GetKeyDown(openChatKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
        escPressed = Input.GetKeyDown(KeyCode.Escape);
        micTogglePressed = Input.GetKeyDown(toggleMicKey);
#endif

        if (enterPressed)
        {
            if (isChatInputFocused)
            {
                SendChatMessageFromUI();
                SetChatFocus(false);
            }
            else
            {
                SetChatFocus(true);
            }
        }
        else if (isChatInputFocused && escPressed)
        {
            SetChatFocus(false);
        }
        else if (!inputFocused && micTogglePressed)
        {
            ToggleMicrophone();
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
            if (EventSystem.current != null)
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
        if (string.IsNullOrWhiteSpace(rawText)) return;

        string msg = rawText.Trim();
        chatInputField.text = "";

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

    public void ToggleMicrophone()
    {
        isMicMuted = !isMicMuted;
        if (!isMicMuted && string.IsNullOrEmpty(activeMicDevice))
        {
            StartMicrophoneCapture();
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
                return;
            }
            activeMicDevice = Microphone.devices[0];
            micClip = Microphone.Start(activeMicDevice, true, 2, VOICE_SAMPLE_RATE);
            lastMicSamplePos = 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhotonVoice] Không thể mở Microphone: {ex.Message}");
            activeMicDevice = null;
        }
    }

    private void ProcessMicrophoneAndSendVoice()
    {
        isLocalSpeaking = false;
        if (isMicMuted || micClip == null || string.IsNullOrEmpty(activeMicDevice)) return;
        if (!Microphone.IsRecording(activeMicDevice)) return;

        int currentPos = Microphone.GetPosition(activeMicDevice);
        if (currentPos < 0) return;

        int clipSamples = micClip.samples;
        int available = currentPos - lastMicSamplePos;
        if (available < 0) available += clipSamples;

        while (available >= VOICE_CHUNK_SAMPLES)
        {
            micClip.GetData(micSampleBuffer, lastMicSamplePos);
            lastMicSamplePos = (lastMicSamplePos + VOICE_CHUNK_SAMPLES) % clipSamples;
            available -= VOICE_CHUNK_SAMPLES;

            // Kiểm tra cường độ âm thanh (Voice Activity Detection)
            float sumSq = 0f;
            for (int i = 0; i < micSampleBuffer.Length; i++)
            {
                sumSq += micSampleBuffer[i] * micSampleBuffer[i];
            }
            float rms = Mathf.Sqrt(sumSq / micSampleBuffer.Length);

            if (rms > 0.008f)
            {
                isLocalSpeaking = true;
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

        // Xóa sạch vùng đệm nhỏ phía trước để tránh lặp lại tiếng cũ trong vòng tròn
        int clearPos = playbackWritePos;
        if (clearPos + clearSilenceBuffer.Length <= PLAYBACK_RING_SAMPLES)
        {
            playbackRingClip.SetData(clearSilenceBuffer, clearPos);
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
    // 4. DỰNG GIAO DIỆN UI CHAT & VOICE CHAT TỰ ĐỘNG
    // =========================================================================
    private void BuildChatAndVoiceUI()
    {
        GameObject cGo = new GameObject("Canvas_PhotonChatVoice");
        cGo.transform.SetParent(transform, false);
        Canvas canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
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

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.12f, 0.72f);

        // Thanh trạng thái kết nối + Nút bật/tắt Mic ở phía trên khung Chat
        GameObject topBar = new GameObject("TopVoiceBar");
        topBar.transform.SetParent(panel.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = new Vector2(0f, -6f);
        topRect.sizeDelta = new Vector2(-16f, 28f);

        // Trạng thái phòng
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(topBar.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(0.65f, 1f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        statusBadgeText = statusObj.AddComponent<Text>();
        statusBadgeText.font = defaultFont;
        statusBadgeText.fontSize = 12;
        statusBadgeText.fontStyle = FontStyle.Bold;
        statusBadgeText.alignment = TextAnchor.MiddleLeft;
        statusBadgeText.color = new Color(0.4f, 0.9f, 1f, 1f);
        statusBadgeText.text = "● Đang kết nối Photon...";

        // Nút Mic [V]
        GameObject micBtnObj = new GameObject("MicToggleButton");
        micBtnObj.transform.SetParent(topBar.transform, false);
        RectTransform micRect = micBtnObj.AddComponent<RectTransform>();
        micRect.anchorMin = new Vector2(0.66f, 0f);
        micRect.anchorMax = new Vector2(1f, 1f);
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
        phText.text = "Nhấn [Enter] để nhập tin nhắn...";

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
            if (client.InRoom && client.CurrentRoom != null)
            {
                statusBadgeText.color = new Color(0.3f, 1f, 0.55f, 1f);
                statusBadgeText.text = $"● Online: {client.CurrentRoom.Name} ({client.CurrentRoom.PlayerCount} người)";
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

        if (micButtonText != null && micButtonBg != null)
        {
            if (isMicMuted)
            {
                micButtonText.text = "🔇 MIC: TẮT [V]";
                micButtonBg.color = new Color(0.65f, 0.22f, 0.22f, 0.92f);
            }
            else
            {
                micButtonText.text = "🎙 MIC: BẬT [V]";
                micButtonBg.color = isLocalSpeaking
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
            else if (isLocalSpeaking && !isMicMuted)
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
