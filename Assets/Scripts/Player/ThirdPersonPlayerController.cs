using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Tốc Độ Di Chuyển (Movement Speeds)")]
    [Tooltip("Tốc độ đi bộ bình thường (m/s)")]
    [Range(2f, 8f)]
    public float walkSpeed = 4.5f;

    [Tooltip("Tốc độ khi giữ phím Shift để chạy nước rút (m/s)")]
    [Range(5f, 14f)]
    public float sprintSpeed = 7.5f;

    [Tooltip("Tốc độ xoay người theo hướng di chuyển")]
    [Range(5f, 25f)]
    public float rotationSpeed = 14f;

    [Header("Nhảy & Trọng Lực (Jump & Gravity)")]
    [Tooltip("Độ cao nhảy (m)")]
    [Range(0.5f, 3.5f)]
    public float jumpHeight = 1.25f;

    [Tooltip("Gia tốc trọng lực")]
    public float gravity = -20f;

    [Header("Hồi Sinh Rơi Vực (Abyss Fall & Respawn)")]
    [Tooltip("Độ cao Y rơi xuống vực sẽ kích hoạt hồi sinh")]
    public float fallThresholdY = -15f;

    [Tooltip("Vị trí hồi sinh an toàn trên đảo trung tâm")]
    public Vector3 respawnPoint = new Vector3(0f, 1.2f, 5.0f);

    [Header("Tham Chiếu Camera")]
    [Tooltip("Camera chính dùng để tính hướng di chuyển tương đối")]
    public Transform cameraTransform;

    [Header("Đèn Lồng Cầm Tay (Lantern Light)")]
    [Tooltip("Nguồn sáng đèn lồng cầm tay đi đêm")]
    public Light lanternLight;

    [Header("Cấu Hình Va Chạm (Character Controller)")]
    [Tooltip("Nếu bật, script sẽ cập nhật thông số CharacterController theo các trường bên dưới. Nếu tắt, giữ nguyên thiết lập trên component.")]
    public bool overrideColliderSettings = false;
    [Range(0.1f, 1.5f)] public float colliderRadius = 0.35f;
    [Range(0.5f, 3.0f)] public float colliderHeight = 1.8f;
    public Vector3 colliderCenter = new Vector3(0f, 0.9f, 0f);
    [Range(10f, 80f)] public float slopeLimit = 46f;
    [Range(0.1f, 1.0f)] public float stepOffset = 0.38f;

    // Thành phần nội bộ
    private CharacterController characterController;
    private Vector3 verticalVelocity;
    private bool isGrounded;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (overrideColliderSettings && characterController != null)
        {
            characterController.radius = colliderRadius;
            characterController.height = colliderHeight;
            characterController.center = colliderCenter;
            characterController.slopeLimit = slopeLimit;
            characterController.stepOffset = stepOffset;
            characterController.minMoveDistance = 0.001f;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (respawnPoint == Vector3.zero)
        {
            respawnPoint = transform.position;
        }
    }

    private void Update()
    {
        CheckAbyssFall();
        HandleGroundCheck();
        HandleMovement();
        HandleJumpAndGravity();
    }

    /// <summary>
    /// Kiểm tra rơi khỏi vực sâu để hồi sinh người chơi về vị trí an toàn
    /// </summary>
    private void CheckAbyssFall()
    {
        if (transform.position.y < fallThresholdY)
        {
            characterController.enabled = false;
            transform.position = respawnPoint;
            verticalVelocity = Vector3.zero;
            characterController.enabled = true;
            Debug.Log("<color=yellow>[Player] Đã rơi xuống vực sâu! Đã tự động hồi sinh trở lại đảo trung tâm.</color>");
        }
    }

    /// <summary>
    /// Kiểm tra tiếp đất bằng CharacterController kết hợp Raycast từ CemeteryMapManager
    /// </summary>
    private void HandleGroundCheck()
    {
        isGrounded = characterController.isGrounded;

        // Bổ trợ bằng Raycast của CemeteryMapManager nếu có
        if (!isGrounded && CemeteryMapManager.Instance != null)
        {
            if (CemeteryMapManager.Instance.RaycastGround(transform.position, out RaycastHit hit, 0.35f))
            {
                isGrounded = true;
            }
        }

        if (isGrounded && verticalVelocity.y < 0f)
        {
            // Lực đè nhẹ giữ vững nhân vật bám vào mặt dốc và cầu đá
            verticalVelocity.y = -2.5f;
        }
    }

    /// <summary>
    /// Xử lý di chuyển tương đối theo hướng Camera (Góc nhìn thứ 3)
    /// </summary>
    private void HandleMovement()
    {
        if (ThirdPersonCamera.isUIPanelOpen) return;

        Vector2 input = ReadMoveInput();
        bool isSprinting = ReadSprintInput();
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 moveDirection = Vector3.zero;

        if (input.sqrMagnitude > 0.01f)
        {
            if (cameraTransform != null)
            {
                // Lấy hướng tiến và ngang của camera chiếu phẳng lên mặt đất (bỏ trục Y)
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();

                moveDirection = (camForward * input.y + camRight * input.x).normalized;
            }
            else
            {
                moveDirection = new Vector3(input.x, 0f, input.y).normalized;
            }

            // Xoay nhân vật mượt mà về hướng di chuyển
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Thực hiện di chuyển phẳng
        Vector3 horizontalMove = moveDirection * currentSpeed;
        characterController.Move(horizontalMove * Time.deltaTime);
    }

    /// <summary>
    /// Xử lý nhảy và trọng lực kéo xuống vực
    /// </summary>
    private void HandleJumpAndGravity()
    {
        if (isGrounded && ReadJumpInput())
        {
            // v = sqrt(2 * h * -g)
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Tác động trọng lực
        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    // =========================================================================
    // HỆ THỐNG ĐỌC INPUT TỰ ĐỘNG (TƯƠNG THÍCH MỌI PHIÊN BẢN INPUT SYSTEM)
    // =========================================================================

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            return input.normalized;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        return input.normalized;
#else
        return input;
#endif
    }

    private bool ReadJumpInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetButtonDown("Jump")) return true;
#endif

        return false;
    }

    private bool ReadSprintInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true;
#endif

        return false;
    }
}
