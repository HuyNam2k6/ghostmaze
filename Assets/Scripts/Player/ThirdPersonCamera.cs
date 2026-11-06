using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ThirdPersonCamera : MonoBehaviour
{
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

    /// <summary>
    /// Biến trạng thái toàn cục báo hiệu bảng UI / Cài đặt đang mở để giải phóng chuột và dừng xoay camera
    /// </summary>
    public static bool isUIPanelOpen = false;

    // Góc quay hiện tại
    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private float currentDistance;
    private static readonly RaycastHit[] s_CameraHitBuffer = new RaycastHit[8];

    private void Start()
    {
        isUIPanelOpen = false;
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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Nhấn phím Escape để mở/khóa con trỏ chuột
        HandleCursorLockToggle();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (ThirdPersonPlayerController.Instance != null)
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
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    private void HandleCameraZoom()
    {
        if (isUIPanelOpen) return;

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

        transform.position = finalPos;
        transform.rotation = rotation;
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

