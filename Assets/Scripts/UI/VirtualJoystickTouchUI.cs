using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Hệ thống điều khiển cảm ứng ảo chuẩn giao diện thiết kế chuyên nghiệp (Mobile Gaming HUD):
/// 1. Virtual Joystick (Góc dưới trái):
///    - Vòng tròn viền phát sáng Cyan neon với 4 mũi tên định hướng (▲, ▼, ◄, ►).
///    - Núm gạt tròn 3D Cyan phát sáng kéo 360 độ cực mượt.
/// 2. Nút FPP (Bên phải joystick ở đáy màn hình):
///    - Nút con nhộng bo tròn phong cách kính trong suốt viền neon Cyan: [ 📷 FPP ].
///    - Bấm để chuyển đổi mượt mà giữa góc nhìn thứ nhất (FPP) và thứ ba (TPP).
/// 3. Nút Nhảy (Góc dưới phải, phía trên nút Đấm):
///    - Vòng tròn phát sáng xanh neon (Cyan Blue).
///    - Biểu tượng người nhảy năng động với mũi tên đôi chevron (⏫) màu trắng sắc nét.
/// 4. Nút Đấm / Tấn Công (Góc dưới cùng bên phải - Nút chính to nhất):
///    - Vòng năng lượng lửa vàng cam (Fiery Golden Aura) với các khía năng lượng.
///    - Biểu tượng Nắm Đấm (Golden Fist 👊) màu vàng ánh kim cực kỳ dũng mãnh ở tâm.
///    - Khi là Người: Kéo/nhấn để tung đòn Ma Thuật / Sóng Thần.
///    - Khi là Ma: Kéo/nhấn để lao tới cào xé liên hoàn!
/// 5. Tâm ngắm Crosshair tinh tế ở giữa màn hình (4 vạch trắng thanh lịch).
/// 6. Khu vực vuốt nửa phải màn hình để xoay Camera (Touch Look Swipe).
/// </summary>
public class VirtualJoystickTouchUI : MonoBehaviour
{
    public static VirtualJoystickTouchUI Instance { get; private set; }

    [Header("Cấu Hình Kích Thước")]
    public float joystickBaseRadius = 95f;
    public float joystickHandleRadius = 42f;

    private Canvas touchCanvas;

    // Joystick Di Chuyển
    private RectTransform joystickBaseRect;
    private RectTransform joystickHandleRect;
    private Image joystickBaseImg;
    private Image joystickHandleImg;
    private Vector2 currentDragVector = Vector2.zero;
    private bool isDragging = false;
    private int joystickPointerId = -1;

    // Nút FPP / TPP
    private RectTransform fppBtnRect;
    private Button fppToggleBtn;
    private Text fppToggleText;
    private Image fppToggleBg;

    // Nút Tự Chạy (Auto-Run) gắn cạnh Joystick
    private RectTransform autoRunBtnRect;
    private Button autoRunBtn;
    private Image autoRunBtnBg;
    private Image autoRunIconImg;
    private Text autoRunBtnText;
    private bool isAutoRunEnabled = false;
    public bool IsAutoRunEnabled => isAutoRunEnabled;

    // Nút Nhảy
    private RectTransform jumpBtnRect;
    private Image jumpBtnImg;
    private float jumpConfiguredScale = 1f;

    // Nút Đấm (Fist Button)
    private RectTransform punchBtnRect;
    private Image punchBtnImg;
    private float punchConfiguredScale = 1f;
    private bool isPunchHeld = false;

    // Tâm ngắm Crosshair
    private GameObject crosshairObj;

    // Nút chuyển chuột test trên PC
    private Button toggleCursorBtn;
    private Text toggleCursorText;

    // =========================================================================
    // TRÌNH CHỈNH SỬA VỊ TRÍ & KÍCH THƯỚC NÚT BẤM (CUSTOM HUD KIỂU FREE FIRE)
    // =========================================================================
    private const string HUD_PREFS_PREFIX = "GhostMaze_CustomHUD_";
    private bool isEditingHUDLayout = false;
    public bool IsEditingHUDLayout => isEditingHUDLayout;

    private GameObject editModeBackdropObj;
    private GameObject hudEditToolbarObj;
    private Text hudEditSelectedText;
    private Text hudEditScaleLabel;
    private Slider hudEditScaleSlider;
    private Text hudEditNoticeText;
    private float hudEditNoticeTimer = 0f;
    private RectTransform chatProxyRect;
    private bool suppressScaleSliderCallback = false;

    private readonly List<DraggableHUDTarget> customHUDTargets = new List<DraggableHUDTarget>();
    private DraggableHUDTarget selectedHUDTarget;
    private readonly Dictionary<string, Vector3> editModeSnapshots = new Dictionary<string, Vector3>();

    // Procedural Sprites Cache
    private static Sprite joystickBaseSprite;
    private static Sprite joystickHandleSprite;
    private static Sprite fppCapsuleSprite;
    private static Sprite cameraIconSprite;
    private static Sprite autoRunIconSprite;
    private static Sprite jumpButtonSprite;
    private static Sprite punchButtonSprite;
    private static Sprite crosshairSprite;
    private static Font cachedFont;

    private struct ActiveTouchEntry
    {
        public int id;
        public Vector2 pos;
        public bool isBegan;
    }

    private int lookTouchFingerId = -1;
    private Vector2 lastLookTouchPos = Vector2.zero;
    private int joystickRawTouchId = -1;
    private bool isPinching = false;
    private float lastPinchDistance = 0f;
    private bool gameplayControlsHiddenForSpectate = false;
    private readonly HashSet<int> ignoredUiTouchIds = new HashSet<int>();
    private readonly List<ActiveTouchEntry> rawFreeTouches = new List<ActiveTouchEntry>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<VirtualJoystickTouchUI>() == null)
        {
            GameObject go = new GameObject("VirtualJoystickTouchUI");
            go.AddComponent<VirtualJoystickTouchUI>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Giữ màn hình điện thoại luôn sáng khi đang chơi game trên Android
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        EnsureAllProceduralSprites();
        BuildDesignerTouchHUD();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isAutoRunEnabled = false;
        isPinching = false;
        joystickRawTouchId = -1;
        ignoredUiTouchIds.Clear();
        SetGameplayHUDControlsVisible(true);
        UpdateAutoRunBtnLabel();
        EnsureEventSystem();
    }

    private void Start()
    {
        EnsureEventSystem();
        UpdateFPPToggleBtnLabel();
        UpdateAutoRunBtnLabel();
    }

    private void Update()
    {
        bool isLobbyScene = SceneManager.GetActiveScene().name == "LobbyScene";
        if (touchCanvas != null && touchCanvas.enabled == isLobbyScene)
        {
            touchCanvas.enabled = !isLobbyScene;
        }
        if (isLobbyScene)
        {
            if (isEditingHUDLayout) ExitHUDCustomizationModeInternal();
            isDragging = false;
            isAutoRunEnabled = false;
            joystickPointerId = -1;
            joystickRawTouchId = -1;
            isPunchHeld = false;
            lookTouchFingerId = -1;
            isPinching = false;
            ignoredUiTouchIds.Clear();
            currentDragVector = Vector2.zero;
            if (joystickHandleRect != null) joystickHandleRect.anchoredPosition = Vector2.zero;
            UpdateAutoRunBtnLabel();
            return;
        }

        if (hudEditNoticeTimer > 0f)
        {
            hudEditNoticeTimer -= Time.unscaledDeltaTime;
            if (hudEditNoticeTimer <= 0f && hudEditNoticeText != null)
            {
                hudEditNoticeText.gameObject.SetActive(false);
            }
        }

        // Trong chế độ Tùy chỉnh vị trí nút (Custom HUD Editor): tạm dừng gameplay input & khóa xoay camera
        if (isEditingHUDLayout)
        {
            SetGameplayHUDControlsVisible(true);
            ThirdPersonCamera.isUIPanelOpen = true;
            isDragging = false;
            isAutoRunEnabled = false;
            joystickPointerId = -1;
            joystickRawTouchId = -1;
            isPunchHeld = false;
            lookTouchFingerId = -1;
            isPinching = false;
            ignoredUiTouchIds.Clear();
            currentDragVector = Vector2.zero;
            if (joystickHandleRect != null) joystickHandleRect.anchoredPosition = Vector2.zero;
            if (ThirdPersonPlayerController.Instance != null) ThirdPersonPlayerController.Instance.SetVirtualSprint(false);
            PumpkinGhostAI activeGhostEdit = GetActivePlayerGhost();
            if (activeGhostEdit != null) activeGhostEdit.SetVirtualSprint(false);
            UpdateAutoRunBtnLabel();
            return;
        }

        // 1. Phím [T] để mở/khóa chuột test trên PC (bỏ qua khi đang gõ Chat hoặc mở UI)
        if (!Application.isMobilePlatform && !ThirdPersonCamera.isUIPanelOpen)
        {
            bool toggleKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) toggleKeyPressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T)) toggleKeyPressed = true;
#endif
            if (toggleKeyPressed)
            {
                ToggleCursorState();
            }
        }

        // 2. Xoay Camera cảm ứng trực tiếp trên Android (PUBG Touch Look System)
        HandleMobileCameraTouchLook();

        // 3. Cập nhật nhãn nút FPP / TPP & ẩn nút điều khiển hành động khi đã tử trận / hết trận (giữ Camera xoay & zoom)
        UpdateFPPToggleBtnLabel();

        bool isMatchFinished = SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.IsFinished;
        bool isLocalHumanDead = !IsPlayerControllingGhost() && PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        if (isMatchFinished || isLocalHumanDead)
        {
            isDragging = false;
            joystickPointerId = -1;
            joystickRawTouchId = -1;
            isPunchHeld = false;
            isAutoRunEnabled = false;
            currentDragVector = Vector2.zero;
            UpdateAutoRunBtnLabel();
            SetGameplayHUDControlsVisible(false);

            if (ThirdPersonPlayerController.Instance != null)
            {
                ThirdPersonPlayerController.Instance.SetVirtualJoystick(Vector2.zero);
                ThirdPersonPlayerController.Instance.SetVirtualSprint(false);
            }
            if (isMatchFinished)
            {
                PumpkinGhostAI gEnd = GetActivePlayerGhost();
                if (gEnd != null)
                {
                    gEnd.SetVirtualMove(Vector2.zero);
                    gEnd.SetVirtualSprint(false);
                }
            }
            return;
        }
        else
        {
            SetGameplayHUDControlsVisible(true);
        }

        Vector2 moveToSend = currentDragVector;
        bool sprintToSend = false;
        if (isAutoRunEnabled)
        {
            if (!isDragging)
            {
                moveToSend = Vector2.up;
            }
            sprintToSend = true;
        }

        if (!isDragging && joystickHandleRect != null)
        {
            joystickHandleRect.anchoredPosition = isAutoRunEnabled
                ? new Vector2(0f, joystickBaseRadius * 0.78f)
                : Vector2.zero;
        }

        // 4. Đẩy dữ liệu điều khiển di chuyển & chạy nhanh (Tự Chạy)
        bool isGhost = IsPlayerControllingGhost();
        if (isGhost)
        {
            PumpkinGhostAI ghost = GetActivePlayerGhost();
            if (ghost != null)
            {
                ghost.SetVirtualMove(moveToSend);
                ghost.SetVirtualSprint(sprintToSend);
            }
            if (ThirdPersonPlayerController.Instance != null)
            {
                ThirdPersonPlayerController.Instance.SetVirtualSprint(false);
            }
        }
        else
        {
            if (ThirdPersonPlayerController.Instance != null)
            {
                ThirdPersonPlayerController.Instance.SetVirtualJoystick(moveToSend);
                ThirdPersonPlayerController.Instance.SetVirtualSprint(sprintToSend);
            }
        }

        // 5. Nếu đang giữ nút Đấm khi là Ma: cào liên tục
        if (isPunchHeld && isGhost)
        {
            PumpkinGhostAI ghost = GetActivePlayerGhost();
            if (ghost != null)
            {
                ghost.SetVirtualAttackHeld(true);
                ghost.TriggerVirtualAttack();
            }
        }
    }

    /// <summary>
    /// Xử lý xoay góc nhìn cảm ứng 1 ngón và chụm/tách 2 ngón thu phóng góc rộng camera (tối đa 1.5x).
    /// Tương thích hoàn toàn New Input System &amp; Legacy Input trên thiết bị di động.
    /// </summary>
    private void HandleMobileCameraTouchLook()
    {
        if (ThirdPersonCamera.Instance == null || ThirdPersonCamera.isUIPanelOpen)
        {
            lookTouchFingerId = -1;
            isPinching = false;
            return;
        }

        rawFreeTouches.Clear();

#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var tc = touches[i];
                var phase = tc.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;

                int fid = tc.touchId.ReadValue();
                Vector2 pos = tc.position.ReadValue();
                CollectTouchSample(fid, pos,
                    phase == UnityEngine.InputSystem.TouchPhase.Began,
                    phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled);
            }

            ProcessFreeTouchesForPinchAndLook();
            return;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                UnityEngine.Touch t = Input.GetTouch(i);
                CollectTouchSample(t.fingerId, t.position,
                    t.phase == UnityEngine.TouchPhase.Began,
                    t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled);
            }

            ProcessFreeTouchesForPinchAndLook();
        }
        else
        {
            lookTouchFingerId = -1;
            isPinching = false;
            ignoredUiTouchIds.Clear();
        }
#endif
    }

    private void CollectTouchSample(int touchId, Vector2 pos, bool isBegan, bool isEnded)
    {
        if (isEnded)
        {
            ignoredUiTouchIds.Remove(touchId);
            if (touchId == joystickRawTouchId) joystickRawTouchId = -1;
            if (touchId == lookTouchFingerId) lookTouchFingerId = -1;
            return;
        }

        if (isBegan)
        {
            bool overJoystick = joystickBaseRect != null
                && joystickBaseRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(joystickBaseRect, pos);
            if (overJoystick && joystickRawTouchId == -1)
            {
                joystickRawTouchId = touchId;
            }
            else if (IsPointerOverInteractiveUI(pos))
            {
                ignoredUiTouchIds.Add(touchId);
                return;
            }
        }

        if (touchId == joystickRawTouchId || ignoredUiTouchIds.Contains(touchId))
        {
            return;
        }

        rawFreeTouches.Add(new ActiveTouchEntry
        {
            id = touchId,
            pos = pos,
            isBegan = isBegan
        });
    }

    private void ProcessFreeTouchesForPinchAndLook()
    {
        if (rawFreeTouches.Count >= 2)
        {
            // Hai ngón tay tự do -> kích hoạt thu phóng (chụm lại = thu màn hình nhỏ lại -> góc camera rộng tới 1.5x)
            lookTouchFingerId = -1;
            float dist = Vector2.Distance(rawFreeTouches[0].pos, rawFreeTouches[1].pos);
            if (isPinching)
            {
                float deltaDist = dist - lastPinchDistance;
                if (Mathf.Abs(deltaDist) > 0.25f)
                {
                    ThirdPersonCamera.Instance.AddPinchZoomDelta(deltaDist);
                }
            }
            isPinching = true;
            lastPinchDistance = dist;
            return;
        }

        isPinching = false;
        bool lookFingerStillActive = false;

        for (int i = 0; i < rawFreeTouches.Count; i++)
        {
            ActiveTouchEntry t = rawFreeTouches[i];
            if (t.isBegan && lookTouchFingerId == -1 && t.pos.x > Screen.width * 0.25f)
            {
                lookTouchFingerId = t.id;
                lastLookTouchPos = t.pos;
                lookFingerStillActive = true;
            }
            else if (t.id == lookTouchFingerId)
            {
                lookFingerStillActive = true;
                Vector2 delta = t.pos - lastLookTouchPos;
                lastLookTouchPos = t.pos;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    ThirdPersonCamera.Instance.AddTouchLookDelta(delta);
                }
            }
        }

        if (lookTouchFingerId != -1 && !lookFingerStillActive)
        {
            lookTouchFingerId = -1;
        }
    }

    private void RecordJoystickRawTouchFromPointerPos(Vector2 screenPos)
    {
        float bestDistSq = 180f * 180f;
        int bestId = -1;

#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var tc = touches[i];
                var phase = tc.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None ||
                    phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    phase == UnityEngine.InputSystem.TouchPhase.Canceled) continue;
                float dSq = (tc.position.ReadValue() - screenPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestId = tc.touchId.ReadValue();
                }
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            UnityEngine.Touch t = Input.GetTouch(i);
            float dSq = (t.position - screenPos).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestId = t.fingerId;
            }
        }
#endif
        if (bestId != -1)
        {
            joystickRawTouchId = bestId;
        }
    }

    private void SetGameplayHUDControlsVisible(bool visible)
    {
        if (gameplayControlsHiddenForSpectate == !visible) return;
        gameplayControlsHiddenForSpectate = !visible;

        if (joystickBaseRect != null) joystickBaseRect.gameObject.SetActive(visible);
        if (autoRunBtnRect != null) autoRunBtnRect.gameObject.SetActive(visible);
        if (jumpBtnRect != null) jumpBtnRect.gameObject.SetActive(visible);
        if (punchBtnRect != null) punchBtnRect.gameObject.SetActive(visible);
        if (fppBtnRect != null) fppBtnRect.gameObject.SetActive(visible);
        if (crosshairObj != null) crosshairObj.SetActive(visible);
    }

    /// <summary>
    /// Kiểm tra vị trí chạm có đè lên nút tương tác hay không (Jump, Punch, FPP, Voice, Joystick...)
    /// để không xoay camera ngoài ý muốn khi nhấn nút chiến đấu hoặc kéo joystick.
    /// </summary>
    private bool IsPointerOverInteractiveUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.position = screenPos;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (go == null) continue;
            string n = go.name;
            if (n == "TouchLookSwipeArea" || n == "CenterCrosshair" || n == "Canvas_VirtualTouchControls") continue;

            if (n == "Joystick_Base" || n == "Joystick_Handle" ||
                n == "PhotonChatVoicePanel" || n == "EndGameResultBanner" || n == "AlliedSpectateHUD" ||
                go.GetComponent<Button>() != null ||
                go.GetComponent<InputField>() != null ||
                go.GetComponentInParent<Button>() != null ||
                go.GetComponentInParent<InputField>() != null ||
                go.GetComponent<Slider>() != null ||
                go.GetComponent<Toggle>() != null ||
                go.GetComponent<Scrollbar>() != null ||
                go.GetComponent<EventTrigger>() != null ||
                n.StartsWith("Btn_") ||
                n.Contains("Button"))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPlayerControllingGhost()
    {
        if (PumpkinGhostAI.PlayerControlledGhost != null && PumpkinGhostAI.PlayerControlledGhost.isPlayerControlled)
        {
            return true;
        }

        for (int i = 0; i < PumpkinGhostAI.ActiveGhosts.Count; i++)
        {
            PumpkinGhostAI g = PumpkinGhostAI.ActiveGhosts[i];
            if (g != null && g.isPlayerControlled && g.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        if (SurvivalTimerUI.Instance != null)
        {
            if (SurvivalTimerUI.Instance.localRole == SurvivalTimerUI.PlayerRole.Ghost &&
                SurvivalTimerUI.Instance.currentPhase == SurvivalTimerUI.MatchPhase.Hunting_3Min)
            {
                return true;
            }
        }

        return false;
    }

    public PumpkinGhostAI GetActivePlayerGhost()
    {
        if (PumpkinGhostAI.PlayerControlledGhost != null && PumpkinGhostAI.PlayerControlledGhost.isPlayerControlled)
        {
            return PumpkinGhostAI.PlayerControlledGhost;
        }

        for (int i = 0; i < PumpkinGhostAI.ActiveGhosts.Count; i++)
        {
            PumpkinGhostAI g = PumpkinGhostAI.ActiveGhosts[i];
            if (g != null && g.isPlayerControlled && g.gameObject.activeInHierarchy)
            {
                return g;
            }
        }

        return null;
    }

    public void OnClickFPPToggle()
    {
        if (isEditingHUDLayout) return;
        if (ThirdPersonCamera.Instance != null)
        {
            ThirdPersonCamera.Instance.TogglePerspective();
            UpdateFPPToggleBtnLabel();
        }
    }

    private void UpdateFPPToggleBtnLabel()
    {
        if (fppToggleText == null) return;
        bool isFPP = ThirdPersonCamera.IsFirstPerson;
        if (isFPP)
        {
            fppToggleText.text = "TPP";
            fppToggleText.color = new Color(0.35f, 0.95f, 1.0f, 0.98f);
        }
        else
        {
            fppToggleText.text = "FPP";
            fppToggleText.color = new Color(1.0f, 0.92f, 0.45f, 0.98f);
        }
    }

    public void OnClickAutoRunToggle()
    {
        if (isEditingHUDLayout) return;
        isAutoRunEnabled = !isAutoRunEnabled;
        UpdateAutoRunBtnLabel();
    }

    public void CancelAutoRun()
    {
        if (!isAutoRunEnabled) return;
        isAutoRunEnabled = false;
        if (!isDragging && joystickHandleRect != null)
        {
            joystickHandleRect.anchoredPosition = Vector2.zero;
        }
        UpdateAutoRunBtnLabel();
    }

    private void UpdateAutoRunBtnLabel()
    {
        if (autoRunBtnText != null)
        {
            if (isAutoRunEnabled)
            {
                autoRunBtnText.text = "ĐANG CHẠY";
                autoRunBtnText.color = Color.white;
            }
            else
            {
                autoRunBtnText.text = "TỰ CHẠY";
                autoRunBtnText.color = new Color(0.55f, 0.96f, 1.0f, 0.98f);
            }
        }
        if (autoRunBtnBg != null)
        {
            autoRunBtnBg.color = isAutoRunEnabled
                ? new Color(0.18f, 0.85f, 0.52f, 0.98f)
                : Color.white;
        }
        if (autoRunIconImg != null)
        {
            autoRunIconImg.color = isAutoRunEnabled
                ? new Color(1.0f, 0.95f, 0.35f, 1.0f)
                : new Color(0.88f, 0.98f, 1.0f, 0.96f);
        }
    }

    public void ToggleCursorState()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        UpdateCursorBtnLabel();
    }

    private void UpdateCursorBtnLabel()
    {
        if (toggleCursorText != null)
        {
            toggleCursorText.text = Cursor.lockState == CursorLockMode.Locked
                ? "🖱️ Chuột: ĐÃ KHÓA [T]"
                : "🖱️ Chuột: TỰ DO [T]";
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    private void EnsureAllProceduralSprites()
    {
        if (joystickBaseSprite == null) joystickBaseSprite = CreateJoystickBaseSprite(256);
        if (joystickHandleSprite == null) joystickHandleSprite = CreateJoystickHandleSprite(128);
        if (fppCapsuleSprite == null) fppCapsuleSprite = CreateCapsuleSprite(160, 64, 30);
        if (cameraIconSprite == null) cameraIconSprite = CreateCameraIconSprite(48);
        if (autoRunIconSprite == null) autoRunIconSprite = CreateRunIconSprite(48);
        if (jumpButtonSprite == null) jumpButtonSprite = CreateJumpButtonSprite(256);
        if (punchButtonSprite == null) punchButtonSprite = CreatePunchButtonSprite(256);
        if (crosshairSprite == null) crosshairSprite = CreateCrosshairSprite(64);

        if (cachedFont == null)
        {
            cachedFont = UIFontHelper.GetCrispFont(14);
        }
    }

    private void BuildDesignerTouchHUD()
    {
        GameObject canvasGo = new GameObject("Canvas_VirtualTouchControls");
        canvasGo.transform.SetParent(transform, false);

        touchCanvas = canvasGo.AddComponent<Canvas>();
        touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        touchCanvas.sortingOrder = 35;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        customHUDTargets.Clear();

        // 0. Lớp nền tối dùng riêng khi đang chỉnh sửa bố cục nút (Custom HUD Editor)
        CreateEditModeBackdrop(canvasGo.transform);

        // 1. Vùng vuốt xoay Camera trên nửa phải màn hình
        CreateTouchLookSwipeArea(canvasGo.transform);

        // 2. Tâm ngắm Crosshair chính giữa màn hình
        CreateCrosshairUI(canvasGo.transform);

        // 3. Virtual Joystick (Góc dưới bên trái, 4 mũi tên Cyan neon)
        CreateVirtualJoystickUI(canvasGo.transform);

        // 4. Nút [ 📷 FPP ] (Nằm bên phải joystick ở đáy màn hình)
        CreateFPPButtonUI(canvasGo.transform);

        // 4.1. Nút Tự Chạy (Auto-Run / Sprint Lock) nằm ngay cạnh Joystick
        CreateAutoRunButtonUI(canvasGo.transform);

        // 5. Nút Nhảy [ ⏫ ] (Góc dưới bên phải, phía trên nút Đấm)
        CreateJumpButtonUI(canvasGo.transform);

        // 6. Nút Đấm / Tấn Công [ 👊 ] (Góc dưới cùng bên phải, vòng lửa vàng cam)
        CreatePunchButtonUI(canvasGo.transform);

        // 7. Proxy kéo thả cho Khung Chat & Voice (hiện khi đang tùy chỉnh nút)
        CreateChatVoiceDragProxy(canvasGo.transform);

        // 8. Thanh công cụ nổi ở trên cùng màn hình khi vào Chế Độ Tùy Chỉnh Nút (kiểu Free Fire)
        CreateHUDCustomizationToolbar(canvasGo.transform);

        // 9. Nút bật/tắt chuột để test trên PC
        CreateCursorToggleButton(canvasGo.transform);

        // 10. Tự động tải bố cục vị trí & kích thước nút đã lưu từ PlayerPrefs
        LoadSavedHUDLayout();
    }

    private void CreateTouchLookSwipeArea(Transform parent)
    {
        GameObject lookArea = new GameObject("TouchLookSwipeArea");
        lookArea.transform.SetParent(parent, false);

        RectTransform rt = lookArea.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.15f);
        rt.anchorMax = new Vector2(1.0f, 0.95f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = lookArea.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        // Trên Android: tắt raycastTarget để chạm đa điểm đi thẳng vào HandleMobileCameraTouchLook không bị cản bởi EventTrigger
        // Trên PC: bật raycastTarget để có thể giữ chuột trái kéo xoay camera thử nghiệm
        img.raycastTarget = !Application.isMobilePlatform;

        EventTrigger trigger = lookArea.AddComponent<EventTrigger>();
        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((data) =>
        {
            if (lookTouchFingerId != -1) return; // Tránh xoay kép khi HandleMobileCameraTouchLook đang xử lý cảm ứng
            PointerEventData ped = (PointerEventData)data;
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.AddTouchLookDelta(ped.delta);
            }
        });
        trigger.triggers.Add(dragEntry);
    }

    private void CreateCrosshairUI(Transform parent)
    {
        crosshairObj = new GameObject("CenterCrosshair");
        crosshairObj.transform.SetParent(parent, false);

        RectTransform rt = crosshairObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(40f, 40f);

        Image img = crosshairObj.AddComponent<Image>();
        img.sprite = crosshairSprite;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void CreateVirtualJoystickUI(Transform parent)
    {
        GameObject baseGo = new GameObject("Joystick_Base");
        baseGo.transform.SetParent(parent, false);

        joystickBaseRect = baseGo.AddComponent<RectTransform>();
        joystickBaseRect.anchorMin = new Vector2(0f, 0f);
        joystickBaseRect.anchorMax = new Vector2(0f, 0f);
        joystickBaseRect.pivot = new Vector2(0.5f, 0.5f);
        joystickBaseRect.anchoredPosition = new Vector2(165f, 165f);
        joystickBaseRect.sizeDelta = new Vector2(joystickBaseRadius * 2f, joystickBaseRadius * 2f);

        joystickBaseImg = baseGo.AddComponent<Image>();
        joystickBaseImg.sprite = joystickBaseSprite;
        joystickBaseImg.color = Color.white;

        // Núm gạt điều khiển bên trong
        GameObject handleGo = new GameObject("Joystick_Handle");
        handleGo.transform.SetParent(baseGo.transform, false);

        joystickHandleRect = handleGo.AddComponent<RectTransform>();
        joystickHandleRect.anchorMin = new Vector2(0.5f, 0.5f);
        joystickHandleRect.anchorMax = new Vector2(0.5f, 0.5f);
        joystickHandleRect.pivot = new Vector2(0.5f, 0.5f);
        joystickHandleRect.anchoredPosition = Vector2.zero;
        joystickHandleRect.sizeDelta = new Vector2(joystickHandleRadius * 2f, joystickHandleRadius * 2f);

        joystickHandleImg = handleGo.AddComponent<Image>();
        joystickHandleImg.sprite = joystickHandleSprite;
        joystickHandleImg.color = Color.white;
        joystickHandleImg.raycastTarget = false;

        EventTrigger trigger = baseGo.AddComponent<EventTrigger>();

        EventTrigger.Entry downEntry = new EventTrigger.Entry();
        downEntry.eventID = EventTriggerType.PointerDown;
        downEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            CancelAutoRun();
            PointerEventData ped = (PointerEventData)data;
            if (!isDragging)
            {
                isDragging = true;
                joystickPointerId = ped.pointerId;
                RecordJoystickRawTouchFromPointerPos(ped.position);
                UpdateJoystickMovement(ped);
            }
        });
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            CancelAutoRun();
            PointerEventData ped = (PointerEventData)data;
            if (isDragging && ped.pointerId == joystickPointerId)
            {
                UpdateJoystickMovement(ped);
            }
        });
        trigger.triggers.Add(dragEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry();
        upEntry.eventID = EventTriggerType.PointerUp;
        upEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            PointerEventData ped = (PointerEventData)data;
            if (ped.pointerId != joystickPointerId && joystickPointerId != -1) return;

            isDragging = false;
            joystickPointerId = -1;
            joystickRawTouchId = -1;
            joystickHandleRect.anchoredPosition = isAutoRunEnabled
                ? new Vector2(0f, joystickBaseRadius * 0.78f)
                : Vector2.zero;
            currentDragVector = Vector2.zero;

            if (!isAutoRunEnabled)
            {
                if (ThirdPersonPlayerController.Instance != null)
                {
                    ThirdPersonPlayerController.Instance.SetVirtualJoystick(Vector2.zero);
                }
                PumpkinGhostAI ghost = GetActivePlayerGhost();
                if (ghost != null)
                {
                    ghost.SetVirtualMove(Vector2.zero);
                }
            }
        });
        trigger.triggers.Add(upEntry);

        RegisterCustomHUDTarget(baseGo, "Joystick", "Cần Gạt Di Chuyển", joystickBaseRect, null, new Vector2(165f, 165f), 1f);
    }

    private void UpdateJoystickMovement(PointerEventData ped)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBaseRect, ped.position, ped.pressEventCamera, out localPoint))
        {
            Vector2 clamped = Vector2.ClampMagnitude(localPoint, joystickBaseRadius);
            joystickHandleRect.anchoredPosition = clamped;
            currentDragVector = clamped / joystickBaseRadius;
            CancelAutoRun();
        }
    }

    private void CreateAutoRunButtonUI(Transform parent)
    {
        GameObject btnGo = new GameObject("Btn_AutoRun_Sprint");
        btnGo.transform.SetParent(parent, false);

        autoRunBtnRect = btnGo.AddComponent<RectTransform>();
        autoRunBtnRect.anchorMin = new Vector2(0f, 0f);
        autoRunBtnRect.anchorMax = new Vector2(0f, 0f);
        autoRunBtnRect.pivot = new Vector2(0.5f, 0.5f);
        autoRunBtnRect.anchoredPosition = new Vector2(338f, 132f); // Nằm cạnh phải Joystick, phía trên nút FPP
        autoRunBtnRect.sizeDelta = new Vector2(138f, 46f);

        autoRunBtnBg = btnGo.AddComponent<Image>();
        autoRunBtnBg.sprite = fppCapsuleSprite;
        autoRunBtnBg.color = Color.white;

        autoRunBtn = btnGo.AddComponent<Button>();
        autoRunBtn.onClick.AddListener(OnClickAutoRunToggle);

        GameObject iconGo = new GameObject("RunIcon");
        iconGo.transform.SetParent(btnGo.transform, false);
        RectTransform irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(24f, 0f);
        irt.sizeDelta = new Vector2(24f, 24f);

        autoRunIconImg = iconGo.AddComponent<Image>();
        autoRunIconImg.sprite = autoRunIconSprite;
        autoRunIconImg.color = new Color(0.88f, 0.98f, 1.0f, 0.96f);
        autoRunIconImg.raycastTarget = false;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(38f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);

        autoRunBtnText = textGo.AddComponent<Text>();
        autoRunBtnText.font = cachedFont;
        autoRunBtnText.text = "TỰ CHẠY";
        autoRunBtnText.fontSize = 14;
        autoRunBtnText.fontStyle = FontStyle.Bold;
        autoRunBtnText.alignment = TextAnchor.MiddleCenter;
        autoRunBtnText.color = new Color(0.55f, 0.96f, 1.0f, 0.98f);
        autoRunBtnText.raycastTarget = false;

        Shadow shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        UpdateAutoRunBtnLabel();
        RegisterCustomHUDTarget(btnGo, "AutoRun", "Nút Tự Chạy (Auto-Run)", autoRunBtnRect, null, new Vector2(338f, 132f), 1f);
    }

    private void CreateFPPButtonUI(Transform parent)
    {
        GameObject btnGo = new GameObject("Btn_FPP_Perspective");
        btnGo.transform.SetParent(parent, false);

        fppBtnRect = btnGo.AddComponent<RectTransform>();
        fppBtnRect.anchorMin = new Vector2(0f, 0f);
        fppBtnRect.anchorMax = new Vector2(0f, 0f);
        fppBtnRect.pivot = new Vector2(0.5f, 0.5f);
        fppBtnRect.anchoredPosition = new Vector2(295f, 65f); // Nằm ngay bên phải joystick ở đáy màn hình
        fppBtnRect.sizeDelta = new Vector2(115f, 46f);

        fppToggleBg = btnGo.AddComponent<Image>();
        fppToggleBg.sprite = fppCapsuleSprite;
        fppToggleBg.color = Color.white;

        fppToggleBtn = btnGo.AddComponent<Button>();
        fppToggleBtn.onClick.AddListener(OnClickFPPToggle);

        // Biểu tượng Camera vector sắc nét bên trái (tránh lỗi hộp ký tự Emoji trên Android)
        GameObject camIconGo = new GameObject("CameraIcon");
        camIconGo.transform.SetParent(btnGo.transform, false);
        RectTransform cirt = camIconGo.AddComponent<RectTransform>();
        cirt.anchorMin = new Vector2(0f, 0.5f);
        cirt.anchorMax = new Vector2(0f, 0.5f);
        cirt.pivot = new Vector2(0.5f, 0.5f);
        cirt.anchoredPosition = new Vector2(30f, 0f);
        cirt.sizeDelta = new Vector2(24f, 24f);

        Image camIconImg = camIconGo.AddComponent<Image>();
        camIconImg.sprite = cameraIconSprite;
        camIconImg.color = new Color(0.95f, 0.98f, 1f, 0.98f);
        camIconImg.raycastTarget = false;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(38f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);

        fppToggleText = textGo.AddComponent<Text>();
        fppToggleText.font = cachedFont;
        fppToggleText.text = "FPP";
        fppToggleText.fontSize = 18;
        fppToggleText.fontStyle = FontStyle.Bold;
        fppToggleText.alignment = TextAnchor.MiddleCenter;
        fppToggleText.color = new Color(1.0f, 0.92f, 0.45f, 0.98f);
        fppToggleText.raycastTarget = false;

        Shadow shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        UpdateFPPToggleBtnLabel();
        RegisterCustomHUDTarget(btnGo, "FPP", "Nút Đổi Góc Nhìn FPP", fppBtnRect, null, new Vector2(295f, 65f), 1f);
    }

    private void CreateJumpButtonUI(Transform parent)
    {
        GameObject btnGo = new GameObject("Btn_Jump_Action");
        btnGo.transform.SetParent(parent, false);

        jumpBtnRect = btnGo.AddComponent<RectTransform>();
        jumpBtnRect.anchorMin = new Vector2(1f, 0f);
        jumpBtnRect.anchorMax = new Vector2(1f, 0f);
        jumpBtnRect.pivot = new Vector2(0.5f, 0.5f);
        jumpBtnRect.anchoredPosition = new Vector2(-195f, 215f); // Nằm phía trên bên trái nút Đấm
        jumpBtnRect.sizeDelta = new Vector2(98f, 98f);

        jumpBtnImg = btnGo.AddComponent<Image>();
        jumpBtnImg.sprite = jumpButtonSprite;
        jumpBtnImg.color = Color.white;

        EventTrigger trigger = btnGo.AddComponent<EventTrigger>();

        EventTrigger.Entry downEntry = new EventTrigger.Entry();
        downEntry.eventID = EventTriggerType.PointerDown;
        downEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            jumpBtnRect.localScale = Vector3.one * (jumpConfiguredScale * 0.92f);

            bool isGhost = IsPlayerControllingGhost();
            if (isGhost)
            {
                PumpkinGhostAI ghost = GetActivePlayerGhost();
                if (ghost != null)
                {
                    ghost.TriggerVirtualLeap();
                    ghost.SetVirtualLeapHeld(true);
                }
            }
            else
            {
                if (ThirdPersonPlayerController.Instance != null)
                {
                    ThirdPersonPlayerController.Instance.TriggerVirtualJump();
                }
            }
        });
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry();
        upEntry.eventID = EventTriggerType.PointerUp;
        upEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            jumpBtnRect.localScale = Vector3.one * jumpConfiguredScale;

            bool isGhost = IsPlayerControllingGhost();
            if (isGhost)
            {
                PumpkinGhostAI ghost = GetActivePlayerGhost();
                if (ghost != null)
                {
                    ghost.SetVirtualLeapHeld(false);
                }
            }
        });
        trigger.triggers.Add(upEntry);

        RegisterCustomHUDTarget(btnGo, "Jump", "Nút Nhảy / Vồ Tới", jumpBtnRect, null, new Vector2(-195f, 215f), 1f);
    }

    private void CreatePunchButtonUI(Transform parent)
    {
        GameObject btnGo = new GameObject("Btn_Punch_Action");
        btnGo.transform.SetParent(parent, false);

        punchBtnRect = btnGo.AddComponent<RectTransform>();
        punchBtnRect.anchorMin = new Vector2(1f, 0f);
        punchBtnRect.anchorMax = new Vector2(1f, 0f);
        punchBtnRect.pivot = new Vector2(0.5f, 0.5f);
        punchBtnRect.anchoredPosition = new Vector2(-115f, 115f); // Góc dưới cùng bên phải
        punchBtnRect.sizeDelta = new Vector2(138f, 138f);

        punchBtnImg = btnGo.AddComponent<Image>();
        punchBtnImg.sprite = punchButtonSprite;
        punchBtnImg.color = Color.white;

        EventTrigger trigger = btnGo.AddComponent<EventTrigger>();

        EventTrigger.Entry downEntry = new EventTrigger.Entry();
        downEntry.eventID = EventTriggerType.PointerDown;
        downEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            CancelAutoRun();
            punchBtnRect.localScale = Vector3.one * (punchConfiguredScale * 0.93f);
            isPunchHeld = true;

            bool isGhost = IsPlayerControllingGhost();
            if (isGhost)
            {
                PumpkinGhostAI ghost = GetActivePlayerGhost();
                if (ghost != null)
                {
                    ghost.TriggerVirtualAttack();
                    ghost.SetVirtualAttackHeld(true);
                }
            }
            else
            {
                if (ThirdPersonPlayerController.Instance != null)
                {
                    ThirdPersonPlayerController.Instance.SetVirtualAttack(true);
                }
            }
        });
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry dragAimEntry = new EventTrigger.Entry();
        dragAimEntry.eventID = EventTriggerType.Drag;
        dragAimEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout || !isPunchHeld) return;
            PointerEventData ped = (PointerEventData)data;
            if (ThirdPersonCamera.Instance != null && ped.delta.sqrMagnitude > 0.25f)
            {
                ThirdPersonCamera.Instance.AddTouchLookDelta(ped.delta * 0.75f);
            }
        });
        trigger.triggers.Add(dragAimEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry();
        upEntry.eventID = EventTriggerType.PointerUp;
        upEntry.callback.AddListener((data) =>
        {
            if (isEditingHUDLayout) return;
            punchBtnRect.localScale = Vector3.one * punchConfiguredScale;
            isPunchHeld = false;

            bool isGhost = IsPlayerControllingGhost();
            if (isGhost)
            {
                PumpkinGhostAI ghost = GetActivePlayerGhost();
                if (ghost != null)
                {
                    ghost.SetVirtualAttackHeld(false);
                }
            }
            else
            {
                if (ThirdPersonPlayerController.Instance != null)
                {
                    ThirdPersonPlayerController.Instance.SetVirtualAttack(false);
                }
            }
        });
        trigger.triggers.Add(upEntry);

        RegisterCustomHUDTarget(btnGo, "Punch", "Nút Đấm / Tấn Công", punchBtnRect, null, new Vector2(-115f, 115f), 1f);
    }

    // =========================================================================
    // TRÌNH TÙY CHỈNH VỊ TRÍ & KÍCH THƯỚC NÚT BẤM (CUSTOM HUD KIỂU FREE FIRE)
    // =========================================================================

    private void RegisterCustomHUDTarget(
        GameObject hostObj,
        string hudId,
        string displayName,
        RectTransform targetRect,
        RectTransform linkedExternalRect,
        Vector2 defaultPos,
        float defaultScale)
    {
        GameObject borderGo = new GameObject("EditSelectionFrame");
        borderGo.transform.SetParent(hostObj.transform, false);
        RectTransform brt = borderGo.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-6f, -6f);
        brt.offsetMax = new Vector2(6f, 6f);

        Image bgImg = borderGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.85f, 1f, 0.14f);
        bgImg.raycastTarget = false;

        Outline ol = borderGo.AddComponent<Outline>();
        ol.effectColor = new Color(0.25f, 0.9f, 1f, 0.9f);
        ol.effectDistance = new Vector2(2.5f, -2.5f);

        borderGo.SetActive(false);

        DraggableHUDTarget draggable = hostObj.AddComponent<DraggableHUDTarget>();
        draggable.hudId = hudId;
        draggable.displayName = displayName;
        draggable.targetRect = targetRect;
        draggable.linkedExternalRect = linkedExternalRect;
        draggable.defaultAnchoredPos = defaultPos;
        draggable.defaultScale = defaultScale;
        draggable.selectionFrameObj = borderGo;
        draggable.selectionFillImg = bgImg;
        draggable.selectionOutline = ol;

        customHUDTargets.Add(draggable);
    }

    private void CreateEditModeBackdrop(Transform parent)
    {
        editModeBackdropObj = new GameObject("HUD_EditBackdrop");
        editModeBackdropObj.transform.SetParent(parent, false);
        RectTransform rt = editModeBackdropObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = editModeBackdropObj.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.10f, 0.48f);
        img.raycastTarget = true; // Hấp thụ mọi chạm nền để không xoay camera khi kéo trượt tay
        editModeBackdropObj.SetActive(false);
    }

    private void CreateChatVoiceDragProxy(Transform parent)
    {
        GameObject proxyGo = new GameObject("ChatVoice_DragProxy");
        proxyGo.transform.SetParent(parent, false);

        chatProxyRect = proxyGo.AddComponent<RectTransform>();
        chatProxyRect.anchorMin = new Vector2(0f, 0f);
        chatProxyRect.anchorMax = new Vector2(0f, 0f);
        chatProxyRect.pivot = new Vector2(0f, 0f);
        chatProxyRect.anchoredPosition = new Vector2(18f, 335f);
        chatProxyRect.sizeDelta = new Vector2(420f, 94f);

        Image bg = proxyGo.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.16f, 0.28f, 0.72f);

        GameObject lblGo = new GameObject("Label");
        lblGo.transform.SetParent(proxyGo.transform, false);
        RectTransform lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        Text lbl = lblGo.AddComponent<Text>();
        lbl.font = cachedFont;
        lbl.fontSize = 14;
        lbl.fontStyle = FontStyle.Bold;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.color = Color.white;
        lbl.text = "KHUNG CHAT & VOICE (Kéo để dời)";
        lbl.raycastTarget = false;

        RegisterCustomHUDTarget(proxyGo, "ChatVoice", "Khung Chat & Voice", chatProxyRect, null, new Vector2(18f, 335f), 1f);
        proxyGo.SetActive(false);
    }

    private void CreateHUDCustomizationToolbar(Transform parent)
    {
        hudEditToolbarObj = new GameObject("HUD_CustomizationToolbar");
        hudEditToolbarObj.transform.SetParent(parent, false);

        RectTransform panelRect = hudEditToolbarObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -16f);
        panelRect.sizeDelta = new Vector2(520f, 168f);

        Image panelBg = hudEditToolbarObj.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.09f, 0.16f, 0.94f);

        Outline panelOl = hudEditToolbarObj.AddComponent<Outline>();
        panelOl.effectColor = new Color(0.22f, 0.78f, 1f, 0.9f);
        panelOl.effectDistance = new Vector2(2f, -2f);

        // Tiêu đề
        CreateToolbarText(hudEditToolbarObj.transform, "Title",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(-24f, 26f),
            "TÙY CHỈNH NÚT BẤM (KÉO THẢ)", 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.28f));

        // Dòng hướng dẫn / nút đang chọn
        hudEditSelectedText = CreateToolbarText(hudEditToolbarObj.transform, "SelectedInfo",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(-24f, 22f),
            "Chạm & kéo trực tiếp bất kỳ nút nào trên màn hình", 12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.90f, 1f));

        // Thanh kéo kích cỡ nút (Scale Slider 55% - 160%)
        hudEditScaleLabel = CreateToolbarText(hudEditToolbarObj.transform, "ScaleLabel",
            new Vector2(0f, 1f), new Vector2(0.36f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -66f), new Vector2(0f, 28f),
            "Kích cỡ: 100%", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);

        hudEditScaleSlider = CreateToolbarSlider(hudEditToolbarObj.transform,
            new Vector2(0.36f, 1f), new Vector2(1f, 1f),
            new Vector2(-4f, -68f), new Vector2(-22f, 24f),
            0.55f, 1.60f, 1.0f, OnHUDScaleSliderChanged);

        // Hàng 3 nút thao tác: [↺ MẶC ĐỊNH] | [✕ HỦY] | [✔ LƯU]
        CreateToolbarButton(hudEditToolbarObj.transform, "Btn_ResetHUD",
            new Vector2(0.03f, 0f), new Vector2(0.34f, 0f), new Vector2(0f, 14f), 42f,
            "MẶC ĐỊNH", new Color(0.75f, 0.45f, 0.12f, 0.96f), ResetAllCustomHUDToDefault);

        CreateToolbarButton(hudEditToolbarObj.transform, "Btn_CancelHUD",
            new Vector2(0.36f, 0f), new Vector2(0.65f, 0f), new Vector2(0f, 14f), 42f,
            "HỦY", new Color(0.58f, 0.18f, 0.20f, 0.96f), CancelHUDCustomization);

        CreateToolbarButton(hudEditToolbarObj.transform, "Btn_SaveHUD",
            new Vector2(0.67f, 0f), new Vector2(0.97f, 0f), new Vector2(0f, 14f), 42f,
            "LƯU & XONG", new Color(0.14f, 0.65f, 0.36f, 0.98f), SaveAndExitHUDCustomization);

        // Thông báo xác nhận sau khi lưu (hiện ngắn ngoài Edit Mode)
        hudEditNoticeText = CreateToolbarText(parent, "HUD_SavedNotice",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -92f), new Vector2(420f, 34f),
            "Đã lưu bố cục nút bấm mới!", 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.35f, 1f, 0.6f));
        hudEditNoticeText.gameObject.SetActive(false);

        hudEditToolbarObj.SetActive(false);
    }

    public void EnterHUDCustomizationMode()
    {
        if (touchCanvas == null) return;
        if (SceneManager.GetActiveScene().name == "LobbyScene") return;

        isEditingHUDLayout = true;
        ThirdPersonCamera.isUIPanelOpen = true;
        touchCanvas.enabled = true;
        touchCanvas.sortingOrder = 115; // Nổi trên tất cả HUD khác trong lúc kéo chỉnh nút

        // Liên kết & đồng bộ proxy ChatVoice với PhotonMultiplayerChatVoice thực tế (nếu có)
        var chatProxy = customHUDTargets.Find(t => t != null && t.hudId == "ChatVoice");
        if (chatProxy != null)
        {
            RectTransform liveChat = PhotonMultiplayerChatVoice.Instance != null ? PhotonMultiplayerChatVoice.Instance.ChatPanelRect : null;
            chatProxy.linkedExternalRect = liveChat;
            if (liveChat != null)
            {
                chatProxy.targetRect.anchoredPosition = liveChat.anchoredPosition;
                chatProxy.targetRect.sizeDelta = liveChat.sizeDelta;
                chatProxy.targetRect.localScale = liveChat.localScale;
            }
            chatProxy.gameObject.SetActive(liveChat != null);
        }

        // Lưu snapshot hiện tại để nút HỦY khôi phục được nguyên trạng nếu người dùng đổi ý
        editModeSnapshots.Clear();
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null || t.targetRect == null) continue;
            editModeSnapshots[t.hudId] = new Vector3(t.targetRect.anchoredPosition.x, t.targetRect.anchoredPosition.y, t.targetRect.localScale.x);
            t.SetEditModeVisible(true, false);
        }

        if (editModeBackdropObj != null) editModeBackdropObj.SetActive(true);
        if (hudEditToolbarObj != null) hudEditToolbarObj.SetActive(true);
        if (toggleCursorBtn != null) toggleCursorBtn.gameObject.SetActive(false);

        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Chọn mặc định nút Đấm hoặc target đầu tiên để slider hoạt động ngay
        DraggableHUDTarget initialTarget = customHUDTargets.Find(t => t != null && t.hudId == "Punch") ?? (customHUDTargets.Count > 0 ? customHUDTargets[0] : null);
        SelectCustomHUDTarget(initialTarget);
    }

    public void SelectCustomHUDTarget(DraggableHUDTarget target)
    {
        selectedHUDTarget = target;
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null) continue;
            t.SetEditModeVisible(isEditingHUDLayout, t == selectedHUDTarget);
        }

        if (selectedHUDTarget != null && selectedHUDTarget.targetRect != null)
        {
            float scale = Mathf.Clamp(selectedHUDTarget.targetRect.localScale.x, 0.55f, 1.60f);
            if (hudEditSelectedText != null)
            {
                hudEditSelectedText.text = $"Đang chọn: <color=#FFD54F>{selectedHUDTarget.displayName}</color>  (Kéo để di chuyển)";
            }
            if (hudEditScaleLabel != null)
            {
                hudEditScaleLabel.text = $"Kích cỡ: {Mathf.RoundToInt(scale * 100f)}%";
            }
            if (hudEditScaleSlider != null)
            {
                suppressScaleSliderCallback = true;
                hudEditScaleSlider.value = scale;
                suppressScaleSliderCallback = false;
            }
        }
    }

    private void OnHUDScaleSliderChanged(float newScale)
    {
        if (suppressScaleSliderCallback || !isEditingHUDLayout || selectedHUDTarget == null || selectedHUDTarget.targetRect == null) return;

        float clampedScale = Mathf.Clamp(newScale, 0.55f, 1.60f);
        selectedHUDTarget.targetRect.localScale = new Vector3(clampedScale, clampedScale, 1f);
        selectedHUDTarget.targetRect.anchoredPosition = ClampAnchoredPositionInsideParent(selectedHUDTarget.targetRect, selectedHUDTarget.targetRect.anchoredPosition);

        if (selectedHUDTarget.linkedExternalRect != null)
        {
            selectedHUDTarget.linkedExternalRect.localScale = new Vector3(clampedScale, clampedScale, 1f);
            selectedHUDTarget.linkedExternalRect.anchoredPosition = selectedHUDTarget.targetRect.anchoredPosition;
        }

        if (hudEditScaleLabel != null)
        {
            hudEditScaleLabel.text = $"Kích cỡ: {Mathf.RoundToInt(clampedScale * 100f)}%";
        }
    }

    public void ResetAllCustomHUDToDefault()
    {
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null || t.targetRect == null) continue;
            t.targetRect.localScale = Vector3.one * t.defaultScale;
            t.targetRect.anchoredPosition = ClampAnchoredPositionInsideParent(t.targetRect, t.defaultAnchoredPos);
            if (t.linkedExternalRect != null)
            {
                t.linkedExternalRect.localScale = t.targetRect.localScale;
                t.linkedExternalRect.anchoredPosition = t.targetRect.anchoredPosition;
            }
        }
        SelectCustomHUDTarget(selectedHUDTarget);
    }

    public void ResetSavedHUDLayoutToDefault()
    {
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null) continue;
            PlayerPrefs.DeleteKey(HUD_PREFS_PREFIX + t.hudId + "_X");
            PlayerPrefs.DeleteKey(HUD_PREFS_PREFIX + t.hudId + "_Y");
            PlayerPrefs.DeleteKey(HUD_PREFS_PREFIX + t.hudId + "_S");
        }
        PlayerPrefs.Save();

        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.ChatPanelRect != null)
        {
            var chatRect = PhotonMultiplayerChatVoice.Instance.ChatPanelRect;
            chatRect.localScale = Vector3.one;
            chatRect.anchoredPosition = ClampAnchoredPositionInsideParent(chatRect, new Vector2(18f, 335f));
        }

        jumpConfiguredScale = 1f;
        punchConfiguredScale = 1f;
        ResetAllCustomHUDToDefault();

        if (hudEditNoticeText != null)
        {
            hudEditNoticeText.text = "Đã khôi phục vị trí nút bấm mặc định!";
            hudEditNoticeText.gameObject.SetActive(true);
            hudEditNoticeTimer = 2.5f;
        }
    }

    public void CancelHUDCustomization()
    {
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null || t.targetRect == null) continue;
            if (editModeSnapshots.TryGetValue(t.hudId, out Vector3 snap))
            {
                t.targetRect.anchoredPosition = new Vector2(snap.x, snap.y);
                t.targetRect.localScale = new Vector3(snap.z, snap.z, 1f);
                if (t.linkedExternalRect != null)
                {
                    t.linkedExternalRect.anchoredPosition = t.targetRect.anchoredPosition;
                    t.linkedExternalRect.localScale = t.targetRect.localScale;
                }
            }
        }
        ExitHUDCustomizationModeInternal();
    }

    public void SaveAndExitHUDCustomization()
    {
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null || t.targetRect == null) continue;

            Vector2 pos = ClampAnchoredPositionInsideParent(t.targetRect, t.targetRect.anchoredPosition);
            float sc = Mathf.Clamp(t.targetRect.localScale.x, 0.55f, 1.60f);
            t.targetRect.anchoredPosition = pos;
            t.targetRect.localScale = new Vector3(sc, sc, 1f);

            PlayerPrefs.SetFloat(HUD_PREFS_PREFIX + t.hudId + "_X", pos.x);
            PlayerPrefs.SetFloat(HUD_PREFS_PREFIX + t.hudId + "_Y", pos.y);
            PlayerPrefs.SetFloat(HUD_PREFS_PREFIX + t.hudId + "_S", sc);

            if (t.hudId == "Jump") jumpConfiguredScale = sc;
            else if (t.hudId == "Punch") punchConfiguredScale = sc;

            if (t.linkedExternalRect != null)
            {
                t.linkedExternalRect.anchoredPosition = pos;
                t.linkedExternalRect.localScale = new Vector3(sc, sc, 1f);
            }
        }

        PlayerPrefs.Save();
        ExitHUDCustomizationModeInternal();

        if (hudEditNoticeText != null)
        {
            hudEditNoticeText.text = "Đã lưu bố cục nút bấm mới!";
            hudEditNoticeText.gameObject.SetActive(true);
            hudEditNoticeTimer = 2.5f;
        }
        Debug.Log("<color=#2ECC71>[CustomHUD] Đã lưu bố cục vị trí & kích thước nút bấm thành công!</color>");
    }

    private void ExitHUDCustomizationModeInternal()
    {
        isEditingHUDLayout = false;
        if (touchCanvas != null) touchCanvas.sortingOrder = 35;

        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null) continue;
            t.SetEditModeVisible(false, false);
            if (t.hudId == "ChatVoice") t.gameObject.SetActive(false);
        }

        if (editModeBackdropObj != null) editModeBackdropObj.SetActive(false);
        if (hudEditToolbarObj != null) hudEditToolbarObj.SetActive(false);
        if (toggleCursorBtn != null && !Application.isMobilePlatform) toggleCursorBtn.gameObject.SetActive(true);

        bool typing = PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsTypingChat();
        ThirdPersonCamera.isUIPanelOpen = typing;
    }

    private void LoadSavedHUDLayout()
    {
        for (int i = 0; i < customHUDTargets.Count; i++)
        {
            var t = customHUDTargets[i];
            if (t == null || t.targetRect == null) continue;
            ApplySavedRectTransform(t.hudId, t.targetRect, t.defaultAnchoredPos, t.defaultScale);

            if (t.hudId == "Jump") jumpConfiguredScale = t.targetRect.localScale.x;
            else if (t.hudId == "Punch") punchConfiguredScale = t.targetRect.localScale.x;
        }
    }

    public static void ApplySavedRectTransform(string hudId, RectTransform rt, Vector2 defaultPos, float defaultScale = 1f)
    {
        if (rt == null) return;
        float x = PlayerPrefs.GetFloat(HUD_PREFS_PREFIX + hudId + "_X", defaultPos.x);
        float y = PlayerPrefs.GetFloat(HUD_PREFS_PREFIX + hudId + "_Y", defaultPos.y);
        float s = Mathf.Clamp(PlayerPrefs.GetFloat(HUD_PREFS_PREFIX + hudId + "_S", defaultScale), 0.55f, 1.60f);

        rt.localScale = new Vector3(s, s, 1f);
        rt.anchoredPosition = ClampAnchoredPositionInsideParent(rt, new Vector2(x, y));
    }

    public static Vector2 ClampAnchoredPositionInsideParent(RectTransform rt, Vector2 desiredAnchoredPos, float margin = 8f)
    {
        if (rt == null) return desiredAnchoredPos;
        RectTransform parentRect = rt.parent as RectTransform;
        if (parentRect == null) return desiredAnchoredPos;

        Rect pr = parentRect.rect;
        if (pr.width <= 10f || pr.height <= 10f) return desiredAnchoredPos;

        float scaleX = Mathf.Abs(rt.localScale.x);
        float scaleY = Mathf.Abs(rt.localScale.y);
        float w = rt.rect.width > 0f ? rt.rect.width : rt.sizeDelta.x;
        float h = rt.rect.height > 0f ? rt.rect.height : rt.sizeDelta.y;

        float leftExtent = w * rt.pivot.x * scaleX;
        float rightExtent = w * (1f - rt.pivot.x) * scaleX;
        float bottomExtent = h * rt.pivot.y * scaleY;
        float topExtent = h * (1f - rt.pivot.y) * scaleY;

        Vector2 anchorOrigin = new Vector2(
            Mathf.Lerp(pr.xMin, pr.xMax, rt.anchorMin.x),
            Mathf.Lerp(pr.yMin, pr.yMax, rt.anchorMin.y));

        Vector2 posInParent = anchorOrigin + desiredAnchoredPos;
        float minX = pr.xMin + leftExtent + margin;
        float maxX = pr.xMax - rightExtent - margin;
        float minY = pr.yMin + bottomExtent + margin;
        float maxY = pr.yMax - topExtent - margin;

        if (minX <= maxX) posInParent.x = Mathf.Clamp(posInParent.x, minX, maxX);
        if (minY <= maxY) posInParent.y = Mathf.Clamp(posInParent.y, minY, maxY);

        return posInParent - anchorOrigin;
    }

    private Text CreateToolbarText(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, string txt, int fontSize, FontStyle style, TextAnchor align, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text t = go.AddComponent<Text>();
        t.font = cachedFont;
        t.text = txt;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private void CreateToolbarButton(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, float height, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(0f, height);

        Image bg = go.AddComponent<Image>();
        bg.color = bgColor;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        CreateToolbarText(go.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, label, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
    }

    private Slider CreateToolbarSlider(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, float minVal, float maxVal, float defaultVal, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject go = new GameObject("ScaleSlider");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image trackBg = go.AddComponent<Image>();
        trackBg.color = new Color(0.14f, 0.20f, 0.30f, 0.95f);

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero;
        fart.anchorMax = Vector2.one;
        fart.offsetMin = new Vector2(6f, 6f);
        fart.offsetMax = new Vector2(-6f, -6f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.22f, 0.78f, 1f, 0.95f);

        GameObject handleArea = new GameObject("HandleSlideArea");
        handleArea.transform.SetParent(go.transform, false);
        RectTransform hart = handleArea.AddComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.offsetMin = new Vector2(10f, 0f);
        hart.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform hrt = handle.AddComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(22f, 28f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        Slider sld = go.AddComponent<Slider>();
        sld.fillRect = frt;
        sld.handleRect = hrt;
        sld.targetGraphic = handleImg;
        sld.minValue = minVal;
        sld.maxValue = maxVal;
        sld.value = defaultVal;
        sld.onValueChanged.AddListener(onChanged);
        return sld;
    }

    public class DraggableHUDTarget : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        public string hudId;
        public string displayName;
        public RectTransform targetRect;
        public RectTransform linkedExternalRect;
        public Vector2 defaultAnchoredPos;
        public float defaultScale = 1f;
        public GameObject selectionFrameObj;
        public Image selectionFillImg;
        public Outline selectionOutline;

        private Vector2 dragStartPointerLocal;
        private Vector2 dragStartAnchoredPos;

        public void SetEditModeVisible(bool visible, bool selected)
        {
            if (selectionFrameObj == null) return;
            selectionFrameObj.SetActive(visible);
            if (!visible) return;

            if (selectionFillImg != null)
            {
                selectionFillImg.color = selected ? new Color(1f, 0.85f, 0.2f, 0.22f) : new Color(0.2f, 0.85f, 1f, 0.12f);
            }
            if (selectionOutline != null)
            {
                selectionOutline.effectColor = selected ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.25f, 0.88f, 1f, 0.82f);
                selectionOutline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Instance == null || !Instance.IsEditingHUDLayout || targetRect == null) return;
            Instance.SelectCustomHUDTarget(this);
            CaptureDragStart(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Instance == null || !Instance.IsEditingHUDLayout || targetRect == null) return;
            Instance.SelectCustomHUDTarget(this);
            CaptureDragStart(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Instance == null || !Instance.IsEditingHUDLayout || targetRect == null) return;
            RectTransform parentRect = targetRect.parent as RectTransform;
            if (parentRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 currentLocal))
            {
                Vector2 desired = dragStartAnchoredPos + (currentLocal - dragStartPointerLocal);
                Vector2 clamped = ClampAnchoredPositionInsideParent(targetRect, desired);
                targetRect.anchoredPosition = clamped;
                if (linkedExternalRect != null)
                {
                    linkedExternalRect.anchoredPosition = clamped;
                }
            }
        }

        private void CaptureDragStart(PointerEventData eventData)
        {
            RectTransform parentRect = targetRect.parent as RectTransform;
            if (parentRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out dragStartPointerLocal);
            dragStartAnchoredPos = targetRect.anchoredPosition;
        }
    }

    private void CreateCursorToggleButton(Transform parent)
    {
        // Trên Android / Thiết bị di động: Không có con trỏ chuột, ẩn nút này để màn hình sạch đẹp
        if (Application.isMobilePlatform) return;

        GameObject btnGo = new GameObject("Btn_ToggleCursor");
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta = new Vector2(210f, 36f);

        Image bg = btnGo.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.14f, 0.20f, 0.75f);

        toggleCursorBtn = btnGo.AddComponent<Button>();
        toggleCursorBtn.onClick.AddListener(ToggleCursorState);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        toggleCursorText = textGo.AddComponent<Text>();
        toggleCursorText.font = cachedFont;
        toggleCursorText.text = "🖱️ Hiện Chuột [T]";
        toggleCursorText.fontSize = 14;
        toggleCursorText.fontStyle = FontStyle.Bold;
        toggleCursorText.alignment = TextAnchor.MiddleCenter;
        toggleCursorText.color = new Color(0.9f, 0.95f, 1f, 0.95f);

        UpdateCursorBtnLabel();
    }

    // =========================================================================
    // HỆ THỐNG VẼ SPRITE THỦ TỤC ĐẠT CHUẨN ĐỒ HỌA THIẾT KẾ (PROCEDURAL VECTOR SPRITES)
    // =========================================================================

    private static Sprite CreatePunchButtonSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float R = center * 0.88f;

        Color bgDark = new Color(0.12f, 0.05f, 0.01f, 0.82f);
        Color goldCore = new Color(1.0f, 0.94f, 0.55f, 1f);
        Color goldMain = new Color(1.0f, 0.72f, 0.12f, 1f);
        Color orangeGlow = new Color(1.0f, 0.42f, 0.02f, 0.95f);

        // Các khớp xương của Nắm Đấm (Fist Geometry)
        Vector2 k1 = new Vector2(0.10f, 0.28f);
        Vector2 k2 = new Vector2(0.24f, 0.18f);
        Vector2 k3 = new Vector2(0.32f, 0.04f);
        Vector2 k4 = new Vector2(0.34f, -0.12f);

        Vector2 p1 = new Vector2(-0.06f, 0.14f);
        Vector2 p2 = new Vector2(0.06f, 0.04f);
        Vector2 p3 = new Vector2(0.14f, -0.08f);
        Vector2 p4 = new Vector2(0.16f, -0.22f);

        Vector2 thumbTip = new Vector2(-0.02f, -0.02f);
        Vector2 thumbBase = new Vector2(-0.16f, -0.18f);

        Vector2 wrist1 = new Vector2(-0.18f, -0.04f);
        Vector2 wrist2 = new Vector2(-0.40f, -0.32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - center, y - center);
                float dist = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                Color col = Color.clear;

                // 1. Nền tròn
                if (dist <= R)
                {
                    float bgAlpha = Mathf.Clamp01((R - dist) * 0.6f) * 0.82f;
                    float t = dist / R;
                    col = Color.Lerp(bgDark, new Color(0.32f, 0.10f, 0.02f, 0.88f), t * t);
                    col.a = bgAlpha;
                }

                // 2. Vòng lửa năng lượng với 4 khía hở
                float rOutDist = Mathf.Abs(dist - R * 0.94f);
                if (rOutDist < 12f)
                {
                    float ringAlpha = Mathf.Clamp01(1f - rOutDist / 8f);
                    float modA = (angle + 45f) % 90f;
                    float notch = Mathf.Clamp01(Mathf.Abs(modA - 45f) / 12f);
                    Color ringCol = Color.Lerp(orangeGlow, goldMain, Mathf.Clamp01(1f - rOutDist / 3f));
                    col = ColorBlendOver(col, ringCol, ringAlpha * notch * 0.95f);
                }

                float rInDist = Mathf.Abs(dist - R * 0.82f);
                if (rInDist < 5f)
                {
                    float ringAlpha = Mathf.Clamp01(1f - rInDist / 3.5f);
                    col = ColorBlendOver(col, goldCore, ringAlpha * 0.95f);
                }

                // 3. Biểu tượng Nắm Đấm (Fist Silhouette)
                Vector2 pn = p / (R * 0.85f);
                float dFinger1 = DistToSegment(pn, p1, k1) - 0.12f;
                float dFinger2 = DistToSegment(pn, p2, k2) - 0.11f;
                float dFinger3 = DistToSegment(pn, p3, k3) - 0.10f;
                float dFinger4 = DistToSegment(pn, p4, k4) - 0.09f;
                float dThumb = DistToSegment(pn, thumbBase, thumbTip) - 0.12f;
                float dPalm = DistToSegment(pn, new Vector2(-0.05f, 0.05f), new Vector2(0.05f, -0.15f)) - 0.18f;
                float dWrist = DistToSegment(pn, wrist1, wrist2) - 0.16f;

                float dFist = Mathf.Min(dFinger1, Mathf.Min(dFinger2, Mathf.Min(dFinger3, Mathf.Min(dFinger4, Mathf.Min(dThumb, Mathf.Min(dPalm, dWrist))))));

                float slit1 = DistToSegment(pn, (p1 + p2) * 0.5f, (k1 + k2) * 0.5f);
                float slit2 = DistToSegment(pn, (p2 + p3) * 0.5f, (k2 + k3) * 0.5f);
                float slit3 = DistToSegment(pn, (p3 + p4) * 0.5f, (k3 + k4) * 0.5f);
                float minSlit = Mathf.Min(slit1, Mathf.Min(slit2, slit3));

                if (dFist < 0.04f)
                {
                    float fistPixelDist = dFist * R * 0.85f;
                    float fistAlpha = Mathf.Clamp01(0.5f - fistPixelDist);
                    if (minSlit < 0.028f) fistAlpha *= Mathf.Clamp01(minSlit / 0.028f);

                    Color fistColor = Color.Lerp(goldMain, goldCore, Mathf.Clamp01(-dFist * 4f));
                    col = ColorBlendOver(col, fistColor, fistAlpha);
                }

                if (dFist >= 0f && dFist < 0.20f)
                {
                    float glowAlpha = Mathf.Clamp01(1f - dFist / 0.20f) * 0.40f;
                    col = ColorBlendOver(col, orangeGlow, glowAlpha);
                }

                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateJumpButtonSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float R = center * 0.88f;

        Color cyanRing = new Color(0.12f, 0.80f, 1.0f, 0.95f);
        Color whiteFigure = Color.white;
        Color bgDark = new Color(0.04f, 0.10f, 0.20f, 0.70f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - center, y - center);
                float dist = p.magnitude;
                Color col = Color.clear;

                if (dist <= R)
                {
                    float bgAlpha = Mathf.Clamp01((R - dist) * 0.5f) * 0.70f;
                    col = bgDark;
                    col.a = bgAlpha;
                }

                float rOutDist = Mathf.Abs(dist - R * 0.92f);
                if (rOutDist < 10f)
                {
                    float alpha = Mathf.Clamp01(1f - rOutDist / 6f);
                    col = ColorBlendOver(col, cyanRing, alpha * 0.90f);
                }
                float rInDist = Mathf.Abs(dist - R * 0.80f);
                if (rInDist < 4f)
                {
                    float alpha = Mathf.Clamp01(1f - rInDist / 2.5f);
                    col = ColorBlendOver(col, cyanRing, alpha * 0.95f);
                }

                Vector2 pn = p / (R * 0.72f);
                Vector2 pf = pn - new Vector2(0f, 0.12f);

                float dHead = (pf - new Vector2(0.04f, 0.38f)).magnitude - 0.13f;
                float dTorso = DistToSegment(pf, new Vector2(0.04f, 0.24f), new Vector2(-0.06f, -0.06f)) - 0.10f;
                float dArm1 = DistToSegment(pf, new Vector2(0.02f, 0.20f), new Vector2(-0.32f, 0.35f)) - 0.065f;
                float dArm2 = DistToSegment(pf, new Vector2(0.04f, 0.16f), new Vector2(0.28f, 0.08f)) - 0.065f;
                float dLeg1A = DistToSegment(pf, new Vector2(-0.06f, -0.06f), new Vector2(-0.25f, -0.22f)) - 0.075f;
                float dLeg1B = DistToSegment(pf, new Vector2(-0.25f, -0.22f), new Vector2(-0.34f, -0.08f)) - 0.065f;
                float dLeg2A = DistToSegment(pf, new Vector2(-0.06f, -0.06f), new Vector2(0.18f, -0.24f)) - 0.075f;
                float dLeg2B = DistToSegment(pf, new Vector2(0.18f, -0.24f), new Vector2(0.26f, -0.42f)) - 0.065f;

                float dFigure = Mathf.Min(dHead, Mathf.Min(dTorso, Mathf.Min(dArm1, Mathf.Min(dArm2, Mathf.Min(dLeg1A, Mathf.Min(dLeg1B, Mathf.Min(dLeg2A, dLeg2B)))))));

                Vector2 pc1 = pn - new Vector2(0f, -0.40f);
                float dChev1 = Mathf.Min(DistToSegment(pc1, Vector2.zero, new Vector2(-0.26f, -0.14f)), DistToSegment(pc1, Vector2.zero, new Vector2(0.26f, -0.14f))) - 0.055f;
                Vector2 pc2 = pn - new Vector2(0f, -0.58f);
                float dChev2 = Mathf.Min(DistToSegment(pc2, Vector2.zero, new Vector2(-0.26f, -0.14f)), DistToSegment(pc2, Vector2.zero, new Vector2(0.26f, -0.14f))) - 0.055f;

                float dAll = Mathf.Min(dFigure, Mathf.Min(dChev1, dChev2));
                if (dAll < 0.04f)
                {
                    float pixelD = dAll * R * 0.72f;
                    float figAlpha = Mathf.Clamp01(0.5f - pixelD);
                    col = ColorBlendOver(col, whiteFigure, figAlpha);
                }

                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateJoystickBaseSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float R = center * 0.90f;

        Color cyanGlow = new Color(0.10f, 0.85f, 1.0f, 0.95f);
        Color cyanSoft = new Color(0.20f, 0.75f, 1.0f, 0.45f);
        Color bgDark = new Color(0.04f, 0.10f, 0.20f, 0.65f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - center, y - center);
                float dist = p.magnitude;
                Color col = Color.clear;

                if (dist <= R)
                {
                    float bgAlpha = Mathf.Clamp01((R - dist) * 0.6f) * 0.65f;
                    col = bgDark;
                    col.a = bgAlpha;
                }

                float rOutDist = Mathf.Abs(dist - R * 0.92f);
                if (rOutDist < 8f)
                {
                    float alpha = Mathf.Clamp01(1f - rOutDist / 5f);
                    col = ColorBlendOver(col, cyanGlow, alpha * 0.95f);
                }

                float rInDist = Mathf.Abs(dist - R * 0.45f);
                if (rInDist < 3f)
                {
                    float alpha = Mathf.Clamp01(1f - rInDist / 2f);
                    col = ColorBlendOver(col, cyanSoft, alpha * 0.6f);
                }

                // 4 Mũi tên định hướng (▲, ▼, ◄, ►)
                float dArrowTop = DistToTriangle(p, new Vector2(0f, R * 0.78f), new Vector2(-15f, R * 0.62f), new Vector2(15f, R * 0.62f));
                float dArrowBot = DistToTriangle(p, new Vector2(0f, -R * 0.78f), new Vector2(-15f, -R * 0.62f), new Vector2(15f, -R * 0.62f));
                float dArrowLeft = DistToTriangle(p, new Vector2(-R * 0.78f, 0f), new Vector2(-R * 0.62f, -15f), new Vector2(-R * 0.62f, 15f));
                float dArrowRight = DistToTriangle(p, new Vector2(R * 0.78f, 0f), new Vector2(R * 0.62f, -15f), new Vector2(R * 0.62f, 15f));

                float dArrow = Mathf.Min(dArrowTop, Mathf.Min(dArrowBot, Mathf.Min(dArrowLeft, dArrowRight)));
                if (dArrow < 0f)
                {
                    float arrowAlpha = Mathf.Clamp01(0.5f - dArrow);
                    col = ColorBlendOver(col, cyanGlow, arrowAlpha);
                }
                else if (dArrow < 4f)
                {
                    float glowAlpha = Mathf.Clamp01(1f - dArrow / 4f) * 0.5f;
                    col = ColorBlendOver(col, cyanGlow, glowAlpha);
                }

                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateJoystickHandleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float R = center * 0.88f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - center, y - center);
                float dist = p.magnitude;
                if (dist > R)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float t = dist / R;
                float alpha = Mathf.Clamp01((R - dist) * 0.8f);

                Vector2 hlPos = new Vector2(-0.25f * R, 0.25f * R);
                float hlDist = (p - hlPos).magnitude / R;
                float hl = Mathf.Clamp01(1f - hlDist * 1.6f);
                hl = hl * hl;

                Color sphereBase = Color.Lerp(new Color(0.25f, 0.85f, 1.0f, 0.95f), new Color(0.08f, 0.45f, 0.85f, 0.95f), t);
                Color finalCol = Color.Lerp(sphereBase, Color.white, hl * 0.75f);
                finalCol.a = alpha * 0.95f;

                tex.SetPixel(x, y, finalCol);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCapsuleSprite(int width, int height, int cornerRadius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color cyanBorder = new Color(0.18f, 0.82f, 1.0f, 0.95f);
        Color darkBlueFill = new Color(0.06f, 0.14f, 0.26f, 0.88f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = Mathf.Clamp(x, cornerRadius, width - 1 - cornerRadius);
                float cy = Mathf.Clamp(y, cornerRadius, height - 1 - cornerRadius);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                Color col = Color.clear;
                if (dist <= cornerRadius)
                {
                    float fillAlpha = Mathf.Clamp01(cornerRadius - dist + 0.6f);
                    col = darkBlueFill;
                    col.a = fillAlpha * 0.88f;

                    // Viền sáng cyan
                    float rDist = Mathf.Abs(dist - cornerRadius + 2f);
                    if (rDist < 3.5f)
                    {
                        float borderAlpha = Mathf.Clamp01(1f - rDist / 2.5f);
                        col = ColorBlendOver(col, cyanBorder, borderAlpha * 0.95f);
                    }
                }
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCrosshairSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        int c = size / 2;
        Color white = new Color(1f, 1f, 1f, 0.88f);
        Color trans = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, trans);
            }
        }

        int gap = 4;
        int len = 8;
        for (int i = 0; i < len; i++)
        {
            tex.SetPixel(c - 1, c + gap + i, white);
            tex.SetPixel(c, c + gap + i, white);

            tex.SetPixel(c - 1, c - gap - 1 - i, white);
            tex.SetPixel(c, c - gap - 1 - i, white);

            tex.SetPixel(c - gap - 1 - i, c - 1, white);
            tex.SetPixel(c - gap - 1 - i, c, white);

            tex.SetPixel(c + gap + i, c - 1, white);
            tex.SetPixel(c + gap + i, c, white);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    private static float DistToTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float dX = p.x - p2.x;
        float dY = p.y - p2.y;
        float dX21 = p2.x - p1.x;
        float dY12 = p1.y - p2.y;
        float D = dY12 * (p0.x - p2.x) + dX21 * (p0.y - p2.y);
        float s = dY12 * dX + dX21 * dY;
        float t = (p2.y - p0.y) * dX + (p0.x - p2.x) * dY;
        if (D < 0) return 10f;
        bool inside = (s >= 0) && (t >= 0) && (s + t <= D);
        if (inside) return -1f;

        float e1 = DistToSegment(p, p0, p1);
        float e2 = DistToSegment(p, p1, p2);
        float e3 = DistToSegment(p, p2, p0);
        return Mathf.Min(e1, Mathf.Min(e2, e3));
    }

    private static Sprite CreateCameraIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.45f;
        float bodyW = size * 0.36f;
        float bodyH = size * 0.25f;
        float topW = size * 0.16f;
        float topY = cy + bodyH + size * 0.06f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inBody = Mathf.Abs(x - cx) <= bodyW && Mathf.Abs(y - cy) <= bodyH;
                bool inTop = Mathf.Abs(x - cx) <= topW && y >= cy + bodyH && y <= topY;
                float lensDist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                bool inLensRing = lensDist >= size * 0.11f && lensDist <= size * 0.16f;
                bool inLensCore = lensDist <= size * 0.075f;

                float a = 0f;
                if (inBody || inTop)
                {
                    if (lensDist <= size * 0.165f && !inLensCore && !inLensRing)
                        a = 0.25f; // Viền khoét ống kính
                    else
                        a = 1f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRunIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        ClearSpriteTexture(tex);

        float s = size;
        // Đầu người chạy (nghiêng tiến tới bên phải)
        Vector2 head = new Vector2(s * 0.62f, s * 0.78f);
        float headR = s * 0.11f;

        // Thân, tay và chân tạo dáng chạy nước rút năng động
        Vector2 shoulder = new Vector2(s * 0.55f, s * 0.60f);
        Vector2 hip = new Vector2(s * 0.44f, s * 0.40f);
        Vector2 armFront = new Vector2(s * 0.74f, s * 0.55f);
        Vector2 armBack = new Vector2(s * 0.34f, s * 0.58f);
        Vector2 legFrontKnee = new Vector2(s * 0.65f, s * 0.28f);
        Vector2 legFrontFoot = new Vector2(s * 0.78f, s * 0.16f);
        Vector2 legBackFoot = new Vector2(s * 0.24f, s * 0.18f);

        // Hai vạch gió tốc độ phía sau bên trái
        Vector2 wind1A = new Vector2(s * 0.12f, s * 0.66f);
        Vector2 wind1B = new Vector2(s * 0.30f, s * 0.66f);
        Vector2 wind2A = new Vector2(s * 0.08f, s * 0.48f);
        Vector2 wind2B = new Vector2(s * 0.26f, s * 0.48f);

        float limbWidth = Mathf.Max(1.8f, s * 0.065f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float dHead = Vector2.Distance(p, head) - headR;
                float dBody = DistToSegment(p, shoulder, hip) - limbWidth * 1.1f;
                float dArm1 = DistToSegment(p, shoulder, armFront) - limbWidth * 0.85f;
                float dArm2 = DistToSegment(p, shoulder, armBack) - limbWidth * 0.85f;
                float dLeg1 = Mathf.Min(DistToSegment(p, hip, legFrontKnee), DistToSegment(p, legFrontKnee, legFrontFoot)) - limbWidth * 0.9f;
                float dLeg2 = DistToSegment(p, hip, legBackFoot) - limbWidth * 0.9f;
                float dWind = Mathf.Min(DistToSegment(p, wind1A, wind1B), DistToSegment(p, wind2A, wind2B)) - limbWidth * 0.55f;

                float d = Mathf.Min(Mathf.Min(dHead, dBody), Mathf.Min(Mathf.Min(dArm1, dArm2), Mathf.Min(dLeg1, dLeg2)));
                float aBody = Mathf.Clamp01(1.0f - d);
                float aWind = Mathf.Clamp01(0.75f - dWind) * 0.82f;
                float a = Mathf.Clamp01(Mathf.Max(aBody, aWind));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void ClearSpriteTexture(Texture2D tex)
    {
        Color trans = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, y, trans);
            }
        }
    }

    private static Color ColorBlendOver(Color under, Color over, float overAlpha)
    {
        float a = Mathf.Clamp01(overAlpha * over.a);
        float outA = a + under.a * (1f - a);
        if (outA <= 0.0001f) return Color.clear;
        Color res = (over * a + under * under.a * (1f - a)) / outA;
        res.a = outA;
        return res;
    }
}
