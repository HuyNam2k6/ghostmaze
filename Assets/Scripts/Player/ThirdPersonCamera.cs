using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ThirdPersonCamera : MonoBehaviour
{
    private static ThirdPersonCamera _instance;
    public static ThirdPersonCamera Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<ThirdPersonCamera>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Mục Tiêu Theo Dõi (Target)")]
    [Tooltip("Đối tượng Player mà Camera sẽ bám theo")]
    public Transform target;

    [Tooltip("Độ lệch trọng tâm ngắm vào thân/đầu nhân vật (m)")]
    public Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);

    [Header("Khoảng Cách & Zoom")]
    [Tooltip("Khoảng cách mặc định từ Camera tới Player (m)")]
    [Range(2f, 10f)]
    public float distance = 4.6f;

    [Tooltip("Khoảng cách gần nhất khi zoom vào")]
    public float minDistance = 1.8f;

    [Tooltip("Khoảng cách xa nhất khi zoom ra")]
    public float maxDistance = 7.5f;

    [Tooltip("Tốc độ zoom bằng con lăn chuột")]
    public float zoomSpeed = 2.5f;

    [Header("Độ Nhạy & Góc Quay Chuột")]
    [Tooltip("Độ nhạy xoay ngang (Yaw)")]
    public float sensitivityX = 2.0f;

    [Tooltip("Độ nhạy xoay dọc (Pitch)")]
    public float sensitivityY = 1.6f;

    [Tooltip("Hệ số nhân độ nhạy chuột (tùy chỉnh thời gian thực từ bảng cài đặt)")]
    [Range(0.2f, 4.0f)]
    public float mouseSensitivityMultiplier = 1.0f;

    [Tooltip("Đảo ngược trục Y khi xoay chuột")]
    public bool invertY = false;

    [Tooltip("Góc ngước tối thiểu (nhìn từ dưới lên)")]
    public float minPitch = -15f;

    [Tooltip("Góc cúi tối đa (nhìn từ trên xuống)")]
    public float maxPitch = 65f;

    [Header("Độ Mượt Mà (Smoothing)")]
    [Tooltip("Tốc độ bám theo Player mượt mà")]
    public float smoothSpeed = 16f;

    [Header("Chống Xuyên Tường & Đá (Collision Clip)")]
    [Tooltip("Bật/tắt tính năng đẩy camera lại gần khi gặp tường đá")]
    public bool enableCollisionClip = true;

    [Tooltip("Bán kính hình cầu kiểm tra va chạm")]
    public float collisionRadius = 0.25f;

    [Tooltip("Layer tính là chướng ngại vật cản camera")]
    public LayerMask obstacleLayer = ~0; // Mặc định kiểm tra mọi layer ngoại trừ chính Player

    [Header("Góc Nhìn Thứ Nhất (First Person) - Nhấn R để chuyển")]
    [Tooltip("Phím chuyển đổi giữa góc nhìn thứ ba và thứ nhất")]
    public KeyCode toggleViewKey = KeyCode.R;

    [Tooltip("Bắt đầu game ở góc nhìn thứ nhất")]
    public bool startInFirstPerson = false;

    [Tooltip("Vị trí mắt so với chân nhân vật (m, theo hướng nhân vật: x = phải, y = cao, z = phía trước)")]
    public Vector3 firstPersonEyeOffset = new Vector3(0f, 1.6f, 0.12f);

    [Tooltip("Góc ngước tối thiểu ở góc nhìn thứ nhất")]
    public float firstPersonMinPitch = -80f;

    [Tooltip("Góc cúi tối đa ở góc nhìn thứ nhất")]
    public float firstPersonMaxPitch = 80f;

    [Tooltip("Tốc độ chuyển cảnh mượt giữa 2 góc nhìn (càng lớn càng nhanh)")]
    public float viewTransitionSpeed = 10f;

    [Tooltip("Ẩn mô hình nhân vật ở góc nhìn thứ nhất (vẫn giữ bóng đổ) để không bị che camera")]
    public bool hideBodyInFirstPerson = true;

    [Tooltip("Near Clip Plane khi ở góc nhìn thứ nhất (nhỏ để không bị cắt tay/vật gần)")]
    public float firstPersonNearClip = 0.03f;

    /// <summary>
    /// Đang ở góc nhìn thứ nhất hay không (các script khác có thể đọc, ví dụ PlayerController xoay theo camera)
    /// </summary>
    public static bool IsFirstPerson { get; private set; }

    private float viewBlend;                  // 0 = thứ ba, 1 = thứ nhất
    private Renderer[] cachedBodyRenderers;   // Cache 1 lần, không cấp phát mỗi frame
    private UnityEngine.Rendering.ShadowCastingMode[] cachedShadowModes;
    private bool bodyHidden;
    private Camera cachedCamera;
    private float defaultNearClip;
    private Transform rendererCacheOwner;

    private static bool _manualUIPanelOpen = false;

    /// <summary>
    /// Biến trạng thái toàn cục báo hiệu bảng UI / Cài đặt đang mở để giải phóng chuột và dừng xoay camera
    /// Tự động đồng bộ với Dashboard cài đặt và ô nhập Chat để chống kẹt trạng thái
    /// </summary>
    public static bool isUIPanelOpen
    {
        get
        {
            if (GameSettingsManager.Instance != null && GameSettingsManager.Instance.dashboardPanel != null && GameSettingsManager.Instance.dashboardPanel.activeSelf)
                return true;
            if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsTypingChat())
                return true;
            return _manualUIPanelOpen;
        }
        set
        {
            _manualUIPanelOpen = value;
        }
    }

    // Góc quay hiện tại
    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private float currentDistance;
    private static readonly RaycastHit[] s_CameraHitBuffer = new RaycastHit[8];

    private void Awake()
    {
        Instance = this;
        _manualUIPanelOpen = false;
    }

    /// <summary>
    /// Chuyển đổi mục tiêu theo dõi của Camera (ví dụ: chuyển sang PumpkinGhost khi Player tử trận)
    /// </summary>
    public void SetTarget(Transform newTarget, Vector3? newTargetOffset = null)
    {
        target = newTarget;
        if (newTargetOffset.HasValue)
        {
            targetOffset = newTargetOffset.Value;
        }

        // Tự động tắt góc nhìn thứ nhất để người chơi chiêm ngưỡng nhân vật mới
        IsFirstPerson = false;
        viewBlend = 0f;
        SetBodyHidden(false);
        cachedBodyRenderers = null;
        rendererCacheOwner = null;

        if (target != null)
        {
            // Reset góc ngắm và khoảng cách tối ưu
            currentDistance = distance;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }
    }

    private void Start()
    {
        _manualUIPanelOpen = false;
        currentDistance = distance;

        if (target == null && ThirdPersonPlayerController.Instance != null)
        {
            target = ThirdPersonPlayerController.Instance.transform;
        }

        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentYaw = angles.y;
            currentPitch = angles.x;
        }

        // Tự động ẩn và khóa con trỏ chuột vào giữa màn hình
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        cachedCamera = GetComponent<Camera>();
        if (cachedCamera != null) defaultNearClip = cachedCamera.nearClipPlane;

        IsFirstPerson = startInFirstPerson;
        viewBlend = IsFirstPerson ? 1f : 0f;
    }

    private void OnDisable()
    {
        // Trả lại hiển thị nhân vật nếu camera bị tắt khi đang ở góc nhìn thứ nhất
        SetBodyHidden(false);
        IsFirstPerson = false;
    }

    private void Update()
    {
        // Nhấn phím Escape để mở/khóa con trỏ chuột
        HandleCursorLockToggle();
        HandleViewToggle();
    }

    /// <summary>
    /// Nhấn R (mặc định) để chuyển qua lại giữa góc nhìn thứ ba và thứ nhất
    /// </summary>
    private void HandleViewToggle()
    {
        if (isUIPanelOpen) return; // Đang gõ chat / mở cài đặt thì không đổi góc nhìn

        if (ReadToggleViewPressed())
        {
            IsFirstPerson = !IsFirstPerson;
            if (!IsFirstPerson)
            {
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }
        }
    }

    private bool ReadToggleViewPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            Key key = KeyCodeToKey(toggleViewKey);
            if (key != Key.None && kb[key].wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(toggleViewKey)) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static Key KeyCodeToKey(KeyCode code)
    {
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (code - KeyCode.A);
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
        {
            // Key.Digit1..Digit0 trong Input System
            return code == KeyCode.Alpha0 ? Key.Digit0 : Key.Digit1 + (code - KeyCode.Alpha1);
        }
        if (code >= KeyCode.F1 && code <= KeyCode.F12) return Key.F1 + (code - KeyCode.F1);
        switch (code)
        {
            case KeyCode.Tab: return Key.Tab;
            case KeyCode.BackQuote: return Key.Backquote;
            case KeyCode.CapsLock: return Key.CapsLock;
            case KeyCode.LeftAlt: return Key.LeftAlt;
            case KeyCode.LeftControl: return Key.LeftCtrl;
            default: return Key.None;
        }
    }
#endif

    private void LateUpdate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene") return;
        if (target == null)
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            {
                if (PumpkinGhostAI.ActiveGhosts != null && PumpkinGhostAI.ActiveGhosts.Count > 0)
                {
                    target = PumpkinGhostAI.ActiveGhosts[0].transform;
                }
            }
            else if (ThirdPersonPlayerController.Instance != null)
            {
                target = ThirdPersonPlayerController.Instance.transform;
            }

            if (target == null) return;
        }

        HandleCameraRotation();
        HandleCameraZoom();
        UpdateCameraPosition();
    }

    private void HandleCursorLockToggle()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        // Khi bảng UI/Cài đặt đang mở, luôn giữ chuột mở tự do để thao tác button
        if (isUIPanelOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            // Nếu chuột đang trỏ trên UI/Button, không tự ý khóa lại
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
#endif
    }

    private void HandleCameraRotation()
    {
        // Không xoay camera khi đang mở bảng UI hoặc khi con trỏ chuột đang tự do
        if (isUIPanelOpen || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 mouseDelta = ReadMouseDelta();

        float effectiveSensX = sensitivityX * mouseSensitivityMultiplier;
        float effectiveSensY = sensitivityY * mouseSensitivityMultiplier;

        currentYaw += mouseDelta.x * effectiveSensX * 0.1f;
        float pitchDelta = mouseDelta.y * effectiveSensY * 0.1f;
        currentPitch += invertY ? pitchDelta : -pitchDelta;
        if (IsFirstPerson)
            currentPitch = Mathf.Clamp(currentPitch, firstPersonMinPitch, firstPersonMaxPitch);
        else
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    private void HandleCameraZoom()
    {
        if (isUIPanelOpen || IsFirstPerson) return;

        float scroll = ReadMouseScroll();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.1f, minDistance, maxDistance);
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredCameraPos = focusPoint - (rotation * Vector3.forward * distance);

        // Kiểm tra chống xuyên tường/bia mộ (SphereCastNonAlloc 0 GC)
        float targetDist = distance;
        if (enableCollisionClip)
        {
            Vector3 direction = (desiredCameraPos - focusPoint).normalized;
            float maxRayDist = distance;
            float closestHit = maxRayDist;
            bool foundHit = false;

            int hitCount = Physics.SphereCastNonAlloc(focusPoint, collisionRadius, direction, s_CameraHitBuffer, maxRayDist, obstacleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = s_CameraHitBuffer[i];
                if (hit.collider == null) continue;
                if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
                if (hit.collider.GetComponentInParent<MagicWaveOrb>() != null) continue;

                if (hit.distance < closestHit)
                {
                    closestHit = hit.distance;
                    foundHit = true;
                }
            }

            if (foundHit)
            {
                targetDist = Mathf.Clamp(closestHit - 0.15f, minDistance * 0.6f, distance);
            }
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDist, Time.deltaTime * smoothSpeed);
        Vector3 finalPos = focusPoint - (rotation * Vector3.forward * currentDistance);

        // ===== Pha trộn mượt sang góc nhìn thứ nhất =====
        float targetBlend = IsFirstPerson ? 1f : 0f;
        viewBlend = Mathf.MoveTowards(viewBlend, targetBlend, Time.deltaTime * viewTransitionSpeed * 0.5f);
        float smoothBlend = viewBlend * viewBlend * (3f - 2f * viewBlend); // SmoothStep

        if (smoothBlend > 0.0001f)
        {
            // Trong góc nhìn thứ nhất: nhân vật luôn quay theo hướng camera (yaw)
            if (IsFirstPerson)
            {
                target.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            }

            Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 eyePos = target.position + yawRot * firstPersonEyeOffset;
            finalPos = Vector3.LerpUnclamped(finalPos, eyePos, smoothBlend);
        }

        // Ẩn thân nhân vật khi camera đã gần như vào đầu (tránh nhìn xuyên mô hình)
        SetBodyHidden(hideBodyInFirstPerson && smoothBlend > 0.6f);

        if (cachedCamera != null)
        {
            cachedCamera.nearClipPlane = smoothBlend > 0.6f ? firstPersonNearClip : defaultNearClip;
        }

        transform.position = finalPos;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Ẩn/hiện mô hình nhân vật bằng ShadowCastingMode.ShadowsOnly (vẫn giữ bóng đổ dưới chân)
    /// </summary>
    private void SetBodyHidden(bool hidden)
    {
        if (hidden == bodyHidden && rendererCacheOwner == target) return;

        // Cache lại danh sách renderer khi đổi target (chỉ cấp phát 1 lần)
        if (rendererCacheOwner != target)
        {
            if (bodyHidden) RestoreBodyRenderers();
            rendererCacheOwner = target;
            cachedBodyRenderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : null;
            cachedShadowModes = cachedBodyRenderers != null
                ? new UnityEngine.Rendering.ShadowCastingMode[cachedBodyRenderers.Length]
                : null;
            bodyHidden = false;
        }

        if (cachedBodyRenderers == null) return;

        if (hidden && !bodyHidden)
        {
            for (int i = 0; i < cachedBodyRenderers.Length; i++)
            {
                var r = cachedBodyRenderers[i];
                if (r == null) continue;
                // Bỏ qua hiệu ứng phép thuật / particle để vẫn nhìn thấy khi bắn
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                cachedShadowModes[i] = r.shadowCastingMode;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            bodyHidden = true;
        }
        else if (!hidden && bodyHidden)
        {
            RestoreBodyRenderers();
        }
    }

    private void RestoreBodyRenderers()
    {
        if (cachedBodyRenderers == null) { bodyHidden = false; return; }
        for (int i = 0; i < cachedBodyRenderers.Length; i++)
        {
            var r = cachedBodyRenderers[i];
            if (r == null) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            r.shadowCastingMode = cachedShadowModes[i];
        }
        bodyHidden = false;
    }

    private Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);
#else
        return Vector2.zero;
#endif
    }

    private float ReadMouseScroll()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.scroll.ReadValue().y * 0.1f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetAxis("Mouse ScrollWheel") * 10f;
#else
        return 0f;
#endif
    }
}

