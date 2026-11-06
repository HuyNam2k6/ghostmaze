using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PumpkinGhostAI : MonoBehaviour
{
    public enum GhostState
    {
        Patrol,         // Đi tuần tra quanh khu vực (gaminWALK / gaminIDLE)
        Suspicious,     // Khựng lại ngoảnh đầu quan sát (gaminIDLE)
        Chase,          // Mắt đỏ rực lao tới truy đuổi Player (gaminRUN)
        Attack,         // Cận chiến cào xé (gaminATTACK)
        Leap,           // Nhảy vồ áp sát (gaminJUMP)
        OutOfBreath,    // Vồ hụt kiệt sức thở dốc (gaminOUTOFBREATH)
        VanishHide      // Biến mất vào bóng đêm lướt ra sau lưng (gaminHIDE)
    }

    [Header("1. Cấu Hình Toàn Diện (Pumpkin Ghost Config)")]
    [Tooltip("Tệp cấu hình ScriptableObject chứa toàn bộ thông số AI, tốc độ, cảm biến và đòn đánh (không fix cứng code)")]
    public PumpkinGhostConfig config;

    [Header("2. Trạng Thái Hiện Tại (Current State)")]
    public GhostState currentState = GhostState.Chase;

    [Header("3. Mục Tiêu (Target Player)")]
    [Tooltip("Transform của người chơi. Nếu để trống, script sẽ tự động tìm theo playerTag cấu hình")]
    public Transform targetPlayer;

    [Header("4. Nguồn Sáng Ma Quái (Eye & Aura Lights)")]
    [Tooltip("Đèn mắt trái")]
    public Light leftEyeLight;
    [Tooltip("Đèn mắt phải")]
    public Light rightEyeLight;
    [Tooltip("Quầng sáng ma mị dưới chân")]
    public Light auraLight;

    [Header("5. Âm Thanh Ma Quái (Audio Clips)")]
    public AudioSource audioSource;
    public AudioClip aggroScreechClip;
    public AudioClip attackSwipeClip;
    public AudioClip leapClip;
    public AudioClip outOfBreathClip;
    public AudioClip vanishClip;

    [Header("6. Hiệu Ứng Bị Giam Cầm / Khóa Cứng (Freeze VFX)")]
    [Tooltip("GameObject chứa vòng xích ma thuật phát sáng khóa cứng thân ma khi người chơi nhấn E")]
    public GameObject freezeVFXRoot;
    [Tooltip("Đèn hào quang phong ấn ma thuật")]
    public Light freezeLight;
    [Tooltip("Màu ánh sáng khi bị khóa (Mặc định: Xanh lam băng giá)")]
    public Color freezeVFXColor = new Color(0.2f, 0.75f, 1.0f);

    // Component nội bộ
    private CharacterController controller;
    private Animator animator;
    private Vector3 spawnPosition;
    private Vector3 currentPatrolDestination;
    private Coroutine activeActionRoutine;

    // Bộ đếm thời gian
    private float stateTimer = 0f;
    private float lastAttackTime = -10f;
    private float lastLeapTime = -5f;
    private float lastHideTime = -5f;

    // Vật lý & Di chuyển
    private Vector3 verticalVelocity = Vector3.zero;
    private float currentMoveSpeed = 0f;
    private bool isLeaping = false;

    // Trạng thái bị người chơi giữ chân (Block) hoặc choáng váng (Rising Wave)
    public bool isFrozen { get; private set; } = false;
    private Coroutine freezeRoutine;
    private Renderer[] cachedBodyRenderers;
    private Material runtimeFreezeMaterial;
    private PumpkinGhostConfig runtimeConfigInstance;

    // Bộ đệm Raycast tĩnh tái sử dụng tối ưu tuyệt đối 0 GC Allocation mỗi frame
    private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[16];

    // Bộ nhớ đệm Animation tránh gọi lặp
    private string currentAnimState = "";

    // Bộ đệm thời gian kiểm tra tầm nhìn tối ưu hóa CPU
    private float nextVisionCheckTime = 0f;
    private bool lastVisionResult = false;
    private float lastVisionDistance = float.MaxValue;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (config == null)
        {
            runtimeConfigInstance = ScriptableObject.CreateInstance<PumpkinGhostConfig>();
            config = runtimeConfigInstance;
        }

        CacheBodyRenderers();
        spawnPosition = transform.position;
        ApplyColliderSettingsFromConfig();
    }

    private void OnEnable()
    {
        ApplyColliderSettingsFromConfig();
    }

    private void OnDestroy()
    {
        if (runtimeFreezeMaterial != null)
        {
            Destroy(runtimeFreezeMaterial);
        }
        if (runtimeConfigInstance != null)
        {
            Destroy(runtimeConfigInstance);
        }
    }

    private void CacheBodyRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        var list = new System.Collections.Generic.List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null) continue;
            if (freezeVFXRoot != null && r.transform.IsChildOf(freezeVFXRoot.transform)) continue;
            if (r.name.StartsWith("MagicBindingRing")) continue;
            list.Add(r);
        }
        cachedBodyRenderers = list.ToArray();
    }

    private void SetGhostBodyVisible(bool visible)
    {
        if (cachedBodyRenderers == null || cachedBodyRenderers.Length == 0)
        {
            CacheBodyRenderers();
        }
        for (int i = 0; i < cachedBodyRenderers.Length; i++)
        {
            if (cachedBodyRenderers[i] != null)
            {
                cachedBodyRenderers[i].enabled = visible;
            }
        }
    }

    private void SafeMove(Vector3 motion)
    {
        if (controller != null && controller.enabled && gameObject.activeInHierarchy)
        {
            controller.Move(motion);
        }
    }

    private void Start()
    {
        FindPlayerTarget();
        PickNewPatrolDestination();

        // Mặc định tự động săn đuổi người chơi nếu bật defaultChasePlayer
        if (config != null && config.defaultChasePlayer)
        {
            EnterChaseState();
        }
        else
        {
            currentState = GhostState.Patrol;
            SetEyeColor(config != null ? config.normalEyeColor : Color.cyan);
            PlayAnimState("Idle", 0.2f);
        }
    }

    private void Update()
    {
        CheckAbyssProtection();
        ApplyGravity();

        // Nếu đang bị người chơi giữ chân (Block 3s) hoặc choáng đòn (RisingWave 5s)
        if (isFrozen)
        {
            if (freezeVFXRoot != null && freezeVFXRoot.activeSelf)
            {
                freezeVFXRoot.transform.Rotate(0f, 150f * Time.deltaTime, 0f);
            }
            return;
        }

        if (targetPlayer == null)
        {
            FindPlayerTarget();
        }

        switch (currentState)
        {
            case GhostState.Patrol:
                UpdatePatrol();
                break;
            case GhostState.Suspicious:
                UpdateSuspicious();
                break;
            case GhostState.Chase:
                UpdateChase();
                break;
            case GhostState.Attack:
            case GhostState.Leap:
            case GhostState.OutOfBreath:
            case GhostState.VanishHide:
                // Được điều khiển mượt mà qua Coroutine chuyên biệt
                break;
        }
    }

    // =========================================================================
    // 1. TUẦN TRA (PATROL STATE)
    // =========================================================================

    private void UpdatePatrol()
    {
        SetEyeColor(config.normalEyeColor);
        SetEyeIntensity(config.normalEyeIntensity);

        // Nếu bật mặc định đuổi theo hoặc nhìn thấy Player -> chuyển trạng thái
        if (config.defaultChasePlayer || CanSeePlayer(out _))
        {
            EnterSuspiciousState();
            return;
        }

        Vector3 toDest = currentPatrolDestination - transform.position;
        toDest.y = 0f;

        if (toDest.magnitude < config.stoppingDistance)
        {
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, config.acceleration * Time.deltaTime);
            SetAnimatorSpeed(currentMoveSpeed);
            PlayAnimState("Idle", 0.2f);

            stateTimer += Time.deltaTime;
            if (stateTimer >= config.waypointWaitTime)
            {
                PickNewPatrolDestination();
                stateTimer = 0f;
            }
        }
        else
        {
            Vector3 desiredDir = toDest.normalized;
            Vector3 steerDir = ComputeSteeredDirection(desiredDir);
            RotateTowardsDirection(steerDir);

            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, config.patrolSpeed, config.acceleration * Time.deltaTime);
            SafeMove(steerDir * currentMoveSpeed * Time.deltaTime);
            SetAnimatorSpeed(currentMoveSpeed);
            PlayAnimState("Walk", 0.2f);
        }
    }

    private void PickNewPatrolDestination()
    {
        Vector2 circle = Random.insideUnitCircle * config.patrolRadius;
        currentPatrolDestination = spawnPosition + new Vector3(circle.x, 0f, circle.y);

        // Kiểm tra độ cao mặt đất tại điểm tuần tra
        Vector3 rayOrigin = currentPatrolDestination + Vector3.up * 8f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, config.groundLayers))
        {
            currentPatrolDestination = hit.point;
        }
    }

    // =========================================================================
    // 2. NGHI NGỜ (SUSPICIOUS STATE)
    // =========================================================================

    private void EnterSuspiciousState()
    {
        currentState = GhostState.Suspicious;
        stateTimer = 0f;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);
        SetEyeColor(config.warnEyeColor);
        PlayAnimState("Idle", 0.15f);
    }

    private void UpdateSuspicious()
    {
        if (targetPlayer == null)
        {
            currentState = GhostState.Patrol;
            return;
        }

        RotateTowardsPosition(targetPlayer.position);
        stateTimer += Time.deltaTime;

        if (stateTimer >= 0.4f)
        {
            EnterChaseState();
        }
    }

    // =========================================================================
    // 3. TRUY ĐUỔI PLAYER (CHASE STATE - MẶC ĐỊNH ĐUỔI THEO PLAYER)
    // =========================================================================

    private void EnterChaseState()
    {
        currentState = GhostState.Chase;
        SetEyeColor(config.aggroEyeColor);
        SetEyeIntensity(config.aggroEyeIntensity);

        if (animator != null && !string.IsNullOrEmpty(config.isChasingParamName))
        {
            animator.SetBool(config.isChasingParamName, true);
        }

        if (audioSource != null && aggroScreechClip != null)
        {
            audioSource.PlayOneShot(aggroScreechClip);
        }
    }

    private void UpdateChase()
    {
        if (targetPlayer == null)
        {
            FindPlayerTarget();
            if (targetPlayer == null) return;
        }

        Vector3 toPlayer = targetPlayer.position - transform.position;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        float dist = flatToPlayer.magnitude;

        // 1. Mất dấu người chơi nếu tắt defaultChasePlayer và người chơi chạy quá xa
        if (!config.defaultChasePlayer && dist > config.losePlayerDistance)
        {
            if (animator != null && !string.IsNullOrEmpty(config.isChasingParamName))
            {
                animator.SetBool(config.isChasingParamName, false);
            }
            currentState = GhostState.Patrol;
            PickNewPatrolDestination();
            return;
        }

        // 2. Đòn cào cận chiến khi áp sát
        if (dist <= config.attackRange && Time.time >= lastAttackTime + config.attackCooldown)
        {
            StartAction(RoutineAttack());
            return;
        }

        // 3. Kỹ năng nhảy vồ áp sát ở tầm trung
        if (config.enableLeap &&
            dist >= config.leapMinDistance && dist <= config.leapMaxDistance &&
            Time.time >= lastLeapTime + config.leapCooldown)
        {
            StartAction(RoutineLeap());
            return;
        }

        // 4. Kỹ năng ẩn thân độn thổ tập kích khi người chơi ở xa
        if (config.enableVanishHide &&
            dist >= config.vanishTriggerDistance &&
            Time.time >= lastHideTime + config.hideCooldown)
        {
            StartAction(RoutineVanishHide());
            return;
        }

        // 5. Chạy đuổi theo người chơi kết hợp thuật toán lách vật cản thông minh
        if (dist > config.stoppingDistance)
        {
            Vector3 desiredDir = flatToPlayer.normalized;
            Vector3 steerDir = ComputeSteeredDirection(desiredDir);

            RotateTowardsDirection(steerDir);

            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, config.chaseSpeed, config.acceleration * Time.deltaTime);
            SafeMove(steerDir * currentMoveSpeed * Time.deltaTime);
            SetAnimatorSpeed(currentMoveSpeed);
            PlayAnimState("Run", 0.15f);
        }
        else
        {
            RotateTowardsPosition(targetPlayer.position);
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, config.acceleration * Time.deltaTime);
            SetAnimatorSpeed(0f);
            PlayAnimState("Idle", 0.15f);
        }

        // 6. Áp dụng cơ chế đẩy lùi mềm bằng Raycast (Raycast Separation) để giảm tải va chạm giữa các Collider
        if (config.useRaycastSeparation && targetPlayer != null)
        {
            ApplyRaycastSeparation();
        }
    }

    // =========================================================================
    // 4. ĐÒN CẬN CHIẾN (ATTACK)
    // =========================================================================

    private IEnumerator RoutineAttack()
    {
        currentState = GhostState.Attack;
        lastAttackTime = Time.time;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);

        if (targetPlayer != null)
        {
            RotateTowardsPositionImmediate(targetPlayer.position);
        }

        if (animator != null && !string.IsNullOrEmpty(config.attackTriggerName))
        {
            animator.SetTrigger(config.attackTriggerName);
        }
        PlayAnimState("Attack", 0.1f, true);

        if (audioSource != null && attackSwipeClip != null)
        {
            audioSource.PlayOneShot(attackSwipeClip);
        }

        yield return new WaitForSeconds(config.attackHitDelay);

        if (targetPlayer != null)
        {
            RotateTowardsPosition(targetPlayer.position);

            if (config.useRaycastAttack)
            {
                ExecuteRaycastAttackHitCheck();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, targetPlayer.position);
                if (dist <= config.attackRange * 1.35f)
                {
                    PlayerHealth pHealth = targetPlayer.GetComponent<PlayerHealth>();
                    if (pHealth == null) pHealth = PlayerHealth.Instance;
                    if (pHealth != null)
                    {
                        pHealth.TakeDamage(config.attackDamage, transform.position);
                    }
                }
            }
        }

        float remainingAnimTime = Mathf.Max(0.05f, config.attackTotalDuration - config.attackHitDelay);
        yield return new WaitForSeconds(remainingAnimTime);

        currentState = GhostState.Chase;
        activeActionRoutine = null;
    }

    // =========================================================================
    // 5. KỸ NĂNG: NHẢY VỒ ÁP SÁT (LEAP)
    // =========================================================================

    private IEnumerator RoutineLeap()
    {
        currentState = GhostState.Leap;
        lastLeapTime = Time.time;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);

        if (targetPlayer != null)
        {
            RotateTowardsPositionImmediate(targetPlayer.position);
        }

        if (animator != null && !string.IsNullOrEmpty(config.jumpTriggerName))
        {
            animator.SetTrigger(config.jumpTriggerName);
        }
        PlayAnimState("Jump", 0.1f, true);

        if (audioSource != null && leapClip != null)
        {
            audioSource.PlayOneShot(leapClip);
        }

        Vector3 leapDir = transform.forward;
        if (targetPlayer != null)
        {
            Vector3 toPlayer = targetPlayer.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f) leapDir = toPlayer.normalized;
        }

        isLeaping = true;
        float elapsed = 0f;
        float duration = config.leapDuration;
        bool hitPlayer = false;

        while (elapsed < duration)
        {
            float verticalArc = Mathf.Sin((elapsed / duration) * Mathf.PI) * config.leapArcHeight;
            Vector3 moveStep = (leapDir * config.leapSpeed + Vector3.up * verticalArc) * Time.deltaTime;
            SafeMove(moveStep);

            elapsed += Time.deltaTime;

            if (!hitPlayer && targetPlayer != null)
            {
                if (config.useRaycastAttack)
                {
                    hitPlayer = ExecuteRaycastLeapHitCheck(leapDir);
                }
                else
                {
                    float dist = Vector3.Distance(transform.position, targetPlayer.position);
                    if (dist <= config.stoppingDistance)
                    {
                        hitPlayer = true;
                        PlayerHealth pHealth = targetPlayer.GetComponent<PlayerHealth>();
                        if (pHealth == null) pHealth = PlayerHealth.Instance;
                        if (pHealth != null)
                        {
                            pHealth.TakeDamage(config.leapDamage, transform.position);
                        }
                    }
                }
            }
            yield return null;
        }

        isLeaping = false;

        if (!hitPlayer)
        {
            yield return RoutineOutOfBreath();
        }
        else
        {
            currentState = GhostState.Chase;
        }

        activeActionRoutine = null;
    }

    // =========================================================================
    // 6. THỞ DỐC KHI VỒ TRƯỢT (OUT OF BREATH)
    // =========================================================================

    private IEnumerator RoutineOutOfBreath()
    {
        currentState = GhostState.OutOfBreath;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);
        SetEyeColor(config.stunEyeColor);
        SetEyeIntensity(config.normalEyeIntensity * 0.7f);

        if (animator != null && !string.IsNullOrEmpty(config.stunTriggerName))
        {
            animator.SetTrigger(config.stunTriggerName);
        }
        PlayAnimState("OutOfBreath", 0.15f, true);

        if (audioSource != null && outOfBreathClip != null)
        {
            audioSource.PlayOneShot(outOfBreathClip);
        }

        yield return new WaitForSeconds(config.outOfBreathDuration);

        SetEyeColor(config.aggroEyeColor);
        SetEyeIntensity(config.aggroEyeIntensity);
        currentState = GhostState.Chase;
    }

    // =========================================================================
    // 7. KỸ NĂNG: ẨN THÂN TẬP KÍCH (SHADOW VANISH)
    // =========================================================================

    private IEnumerator RoutineVanishHide()
    {
        currentState = GhostState.VanishHide;
        lastHideTime = Time.time;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);

        if (animator != null && !string.IsNullOrEmpty(config.hideTriggerName))
        {
            animator.SetTrigger(config.hideTriggerName);
        }
        PlayAnimState("Hide", 0.15f, true);

        if (audioSource != null && vanishClip != null)
        {
            audioSource.PlayOneShot(vanishClip);
        }

        yield return new WaitForSeconds(0.5f);

        SetEyeIntensity(0f);
        SetGhostBodyVisible(false);

        float elapsed = 0f;
        while (elapsed < config.hideDuration)
        {
            if (targetPlayer != null)
            {
                Vector3 behindPos = targetPlayer.position - targetPlayer.forward * config.ambushBehindDistance;
                Vector3 toBehind = behindPos - transform.position;
                toBehind.y = 0f;
                if (toBehind.sqrMagnitude > 0.5f)
                {
                    Vector3 steer = ComputeSteeredDirection(toBehind.normalized);
                    SafeMove(steer * (config.chaseSpeed * 1.3f) * Time.deltaTime);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetGhostBodyVisible(true);

        SetEyeIntensity(config.aggroEyeIntensity);
        SetEyeColor(config.aggroEyeColor);

        if (targetPlayer != null)
        {
            RotateTowardsPositionImmediate(targetPlayer.position);
        }

        if (animator != null && !string.IsNullOrEmpty(config.unHideTriggerName))
        {
            animator.SetTrigger(config.unHideTriggerName);
        }
        PlayAnimState("Idle", 0.15f, true);

        if (audioSource != null && aggroScreechClip != null)
        {
            audioSource.PlayOneShot(aggroScreechClip);
        }

        yield return new WaitForSeconds(0.3f);
        currentState = GhostState.Chase;
        activeActionRoutine = null;
    }

    // =========================================================================
    // HỆ THỐNG LÁCH VẬT CẢN (OBSTACLE AVOIDANCE) & TẦM NHÌN
    // =========================================================================

    private void StartAction(IEnumerator routine)
    {
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
        }
        activeActionRoutine = StartCoroutine(routine);
    }

    /// <summary>
    /// Thuật toán lách vật cản thông minh: dùng SphereCast quét theo tọa độ cấu hình
    /// tự động thử các góc lệch để tìm hướng di chuyển thông thoáng nhất mà không bị kẹt tường.
    /// </summary>
    private Vector3 ComputeSteeredDirection(Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.001f) return transform.forward;

        Vector3 origin = transform.TransformPoint(config.obstacleCheckOffset);
        float checkDist = config.obstacleAvoidanceDistance;
        float radius = config.obstacleCheckRadius;

        if (CheckObstacleHit(origin, desiredDir, radius, checkDist))
        {
            float[] angles = config.avoidanceAngles;
            if (angles != null && angles.Length > 0)
            {
                for (int i = 0; i < angles.Length; i++)
                {
                    Vector3 testDir = Quaternion.Euler(0f, angles[i], 0f) * desiredDir;
                    if (!CheckObstacleHit(origin, testDir, radius, checkDist * 0.9f))
                    {
                        return testDir.normalized;
                    }
                }
            }
        }

        return desiredDir;
    }

    /// <summary>
    /// Đẩy lùi mềm bằng Raycast giữa Ma và Người chơi:
    /// Triệt tiêu hiện tượng 2 Collider va đập đè lên nhau trong PhysX, giúp duy trì FPS mượt mà ổn định.
    /// </summary>
    private void ApplyRaycastSeparation()
    {
        if (targetPlayer == null) return;

        Vector3 myPos = transform.position;
        Vector3 playerPos = targetPlayer.position;
        Vector3 diff = playerPos - myPos;
        diff.y = 0f;
        float dist = diff.magnitude;

        if (dist < config.separationDistance && dist > 0.001f)
        {
            Vector3 pushDir = -diff.normalized;
            float pushStrength = (1.0f - (dist / config.separationDistance)) * config.separationForce;

            // Kiểm tra Raycast sau lưng xem có vướng tường không trước khi trượt lùi
            Vector3 rayOrigin = transform.TransformPoint(config.obstacleCheckOffset);
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, pushDir, s_HitBuffer, 0.45f, config.obstacleLayers, QueryTriggerInteraction.Ignore);
            bool wallBehind = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit h = s_HitBuffer[i];
                if (h.collider == null) continue;
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                if (h.transform == targetPlayer || h.transform.IsChildOf(targetPlayer)) continue;

                wallBehind = true;
                break;
            }

            if (!wallBehind)
            {
                SafeMove(pushDir * pushStrength * Time.deltaTime);
            }
            else
            {
                // Nếu sau lưng vướng tường, trượt vuông góc (Tangent slide) né sang bên
                Vector3 tangent = Vector3.Cross(pushDir, Vector3.up).normalized;
                SafeMove(tangent * (pushStrength * 0.7f) * Time.deltaTime);
            }
        }
    }

    private Vector3 GetPlayerWorldTargetPoint()
    {
        if (targetPlayer == null) return transform.position;
        CharacterController pcc = targetPlayer.GetComponent<CharacterController>();
        if (pcc != null && pcc.enabled)
        {
            return pcc.bounds.center;
        }
        return targetPlayer.position + (targetPlayer.rotation * config.playerTargetEyeOffset);
    }

    /// <summary>
    /// Kiểm tra va chạm đòn cào bằng SphereCast NonAlloc: Tối ưu 0 GC, không tạo Collider Trigger tạm,
    /// tự động chặn đòn nếu có tường chắn ở giữa và xác định điểm va chạm thực tế.
    /// </summary>
    private bool ExecuteRaycastAttackHitCheck()
    {
        if (targetPlayer == null) return false;

        Vector3 eyePos = transform.TransformPoint(config.visionEyeOffset);
        Vector3 targetPoint = GetPlayerWorldTargetPoint();
        Vector3 swipeDir = (targetPoint - eyePos);
        float distanceToTarget = swipeDir.magnitude;

        if (distanceToTarget > config.attackRaycastDistance)
        {
            return false;
        }

        if (distanceToTarget > 0.001f) swipeDir.Normalize();

        int layerMask = config.playerLayer | config.visionBlockerLayers;
        int hitCount = Physics.SphereCastNonAlloc(
            eyePos,
            config.attackRaycastRadius,
            swipeDir,
            s_HitBuffer,
            config.attackRaycastDistance,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit closestHit = default;
        bool hitFound = false;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            bool isWall = ((1 << hit.collider.gameObject.layer) & config.visionBlockerLayers) != 0;
            bool isPlayer = (hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer) || hit.collider.CompareTag(config.playerTag));

            if (isWall && !isPlayer)
            {
                if (hit.distance < distanceToTarget)
                {
                    // Tường chắn trước người chơi -> trượt đòn đánh
                    return false;
                }
            }

            if (isPlayer)
            {
                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestHit = hit;
                    hitFound = true;
                }
            }
        }

        if (hitFound)
        {
            PlayerHealth pHealth = targetPlayer.GetComponent<PlayerHealth>();
            if (pHealth == null) pHealth = PlayerHealth.Instance;
            if (pHealth != null)
            {
                Vector3 hitPoint = closestHit.point;
                if (hitPoint == Vector3.zero) hitPoint = targetPlayer.position;
                pHealth.TakeDamage(config.attackDamage, transform.position);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra va chạm vồ trúng Player bằng SphereCast NonAlloc theo hướng nhảy
    /// </summary>
    private bool ExecuteRaycastLeapHitCheck(Vector3 leapDir)
    {
        if (targetPlayer == null) return false;

        Vector3 origin = transform.TransformPoint(config.obstacleCheckOffset);
        float sweepDist = (config.leapSpeed * Time.deltaTime) + 0.45f;

        int layerMask = config.playerLayer | config.visionBlockerLayers;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            config.leapRaycastRadius,
            leapDir,
            s_HitBuffer,
            sweepDist,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            if (hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer) || hit.collider.CompareTag(config.playerTag))
            {
                PlayerHealth pHealth = targetPlayer.GetComponent<PlayerHealth>();
                if (pHealth == null) pHealth = PlayerHealth.Instance;
                if (pHealth != null)
                {
                    pHealth.TakeDamage(config.leapDamage, transform.position);
                }
                return true;
            }
        }

        return false;
    }

    private bool CheckObstacleHit(Vector3 origin, Vector3 dir, float radius, float dist)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, dir, s_HitBuffer, dist, config.obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = s_HitBuffer[i];
            if (hit.collider == null) continue;

            if (targetPlayer != null && (hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer)))
            {
                continue;
            }
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private bool CanSeePlayer(out float distance)
    {
        if (Time.time < nextVisionCheckTime)
        {
            distance = lastVisionDistance;
            return lastVisionResult;
        }

        nextVisionCheckTime = Time.time + (config != null ? config.visionCheckInterval : 0.05f);

        distance = float.MaxValue;
        if (targetPlayer == null)
        {
            lastVisionResult = false;
            lastVisionDistance = float.MaxValue;
            return false;
        }

        distance = Vector3.Distance(transform.position, targetPlayer.position);
        lastVisionDistance = distance;

        // 1. Phát hiện cực gần (nghe tiếng chân)
        if (distance <= config.closeProximityDistance)
        {
            lastVisionResult = true;
            return true;
        }

        // 2. Kiểm tra nón tầm nhìn
        if (distance <= config.detectionRadius)
        {
            Vector3 dirToPlayer = (targetPlayer.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle <= config.fieldOfViewAngle * 0.5f)
            {
                Vector3 eyePos = transform.TransformPoint(config.visionEyeOffset);
                Vector3 playerEyePos = targetPlayer.position + (targetPlayer.rotation * config.playerTargetEyeOffset);
                Vector3 rayDir = (playerEyePos - eyePos);
                float rayDist = rayDir.magnitude;
                if (rayDist > 0.001f) rayDir.Normalize();

                int count = Physics.RaycastNonAlloc(eyePos, rayDir, s_HitBuffer, rayDist, config.visionBlockerLayers, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = s_HitBuffer[i];
                    if (hit.collider == null) continue;
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                    if (hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer)) continue;

                    blocked = true;
                    break;
                }

                if (!blocked)
                {
                    lastVisionResult = true;
                    return true;
                }
            }
        }

        lastVisionResult = false;
        return false;
    }

    private void RotateTowardsDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, config.rotationSpeed * Time.deltaTime);
        }
    }

    private void RotateTowardsPosition(Vector3 targetPos)
    {
        RotateTowardsDirection(targetPos - transform.position);
    }

    private void RotateTowardsPositionImmediate(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (animator != null && !string.IsNullOrEmpty(config.speedParamName))
        {
            animator.SetFloat(config.speedParamName, speed);
        }
    }

    private void PlayAnimState(string stateName, float fadeDuration = 0.15f, bool forceReplay = false)
    {
        if (animator == null) return;
        if (!forceReplay && currentAnimState == stateName) return;

        currentAnimState = stateName;
        animator.CrossFadeInFixedTime(stateName, fadeDuration, 0);
    }

    private void ApplyGravity()
    {
        if (isLeaping || controller == null || !controller.enabled) return;

        bool grounded = controller.isGrounded;
        if (!grounded)
        {
            // Bổ sung kiểm tra Raycast mặt đất từ cấu hình (NonAlloc 0 GC)
            Vector3 origin = transform.TransformPoint(config.groundCheckOffset);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, s_HitBuffer, config.groundCheckDistance, config.groundLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = s_HitBuffer[i];
                if (h.collider != null && h.transform != transform && !h.transform.IsChildOf(transform))
                {
                    grounded = true;
                    break;
                }
            }
        }

        if (grounded)
        {
            verticalVelocity.y = -2.5f;
        }
        else
        {
            verticalVelocity.y += -config.gravity * Time.deltaTime;
        }
        SafeMove(verticalVelocity * Time.deltaTime);
    }

    private void CheckAbyssProtection()
    {
        if (transform.position.y < config.abyssThresholdY)
        {
            if (controller != null) controller.enabled = false;
            transform.position = spawnPosition;
            verticalVelocity = Vector3.zero;
            if (controller != null) controller.enabled = true;
            ApplyColliderSettingsFromConfig();
            currentState = config.defaultChasePlayer ? GhostState.Chase : GhostState.Patrol;
        }
    }

    private void FindPlayerTarget()
    {
        if (!string.IsNullOrEmpty(config.playerTag))
        {
            GameObject playerGo = GameObject.FindWithTag(config.playerTag);
            if (playerGo != null)
            {
                targetPlayer = playerGo.transform;
                return;
            }
        }

        ThirdPersonPlayerController fallback = FindAnyObjectByType<ThirdPersonPlayerController>();
        if (fallback != null)
        {
            targetPlayer = fallback.transform;
        }
    }

    public void ApplyColliderSettingsFromConfig()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null || config == null) return;

        controller.radius = config.colliderRadius;
        controller.height = config.colliderHeight;
        controller.center = config.colliderCenter;
        controller.slopeLimit = config.slopeLimit;

        Vector3 lossy = transform.lossyScale;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)));

        float scaledRadius = controller.radius * scaleXZ;
        float scaledCylinderHeight = Mathf.Max(0f, controller.height * scaleY - scaledRadius * 2f);
        float maxAllowedStepOffset = scaledCylinderHeight + scaledRadius * 2f;

        float desiredStep = Mathf.Min(config.stepOffset, maxAllowedStepOffset * 0.75f);
        bool needsReactivate = controller.stepOffset > maxAllowedStepOffset * 0.9f;
        controller.stepOffset = Mathf.Max(0.01f, desiredStep);

        if (scaledRadius > 0.005f && controller.skinWidth > scaledRadius * 0.8f)
        {
            controller.skinWidth = Mathf.Max(0.005f, scaledRadius * 0.25f);
            needsReactivate = true;
        }

        if (Application.isPlaying && needsReactivate && controller.enabled && gameObject.activeInHierarchy)
        {
            controller.enabled = false;
            controller.enabled = true;
        }
    }

    private void SetEyeColor(Color c)
    {
        if (leftEyeLight != null) leftEyeLight.color = c;
        if (rightEyeLight != null) rightEyeLight.color = c;
        if (auraLight != null) auraLight.color = c;
    }

    private void SetEyeIntensity(float intensity)
    {
        if (leftEyeLight != null) leftEyeLight.intensity = intensity;
        if (rightEyeLight != null) rightEyeLight.intensity = intensity;
    }

    // =========================================================================
    // 8. TƯƠNG TÁC PHẢN ĐÒN VỚI PLAYER (BLOCK & RISING WAVE)
    // =========================================================================

    /// <summary>
    /// Giữ chân / đóng băng con ma trong số giây chỉ định (từ chiêu Block của Player)
    /// </summary>
    public void Freeze(float duration)
    {
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
            activeActionRoutine = null;
        }
        isLeaping = false;
        SetGhostBodyVisible(true);

        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
        }
        freezeRoutine = StartCoroutine(RoutineFreeze(duration));
    }

    /// <summary>
    /// Đẩy lùi con ma ra xa theo cự ly chỉ định và làm đứng im trong số giây (từ chiêu RisingWave của Player)
    /// </summary>
    public void ApplyKnockbackAndFreeze(Vector3 pushDirection, float pushDistance, float freezeDuration)
    {
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
            activeActionRoutine = null;
        }
        isLeaping = false;
        SetGhostBodyVisible(true);

        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
        }
        freezeRoutine = StartCoroutine(RoutineKnockbackAndFreeze(pushDirection, pushDistance, freezeDuration));
    }

    private IEnumerator RoutineFreeze(float duration)
    {
        isFrozen = true;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);
        SetFreezeVFX(true);
        SetEyeColor(config != null ? config.stunEyeColor : freezeVFXColor);
        if (config != null) SetEyeIntensity(config.normalEyeIntensity * 0.8f);

        if (animator != null && config != null && !string.IsNullOrEmpty(config.stunTriggerName))
        {
            animator.SetTrigger(config.stunTriggerName);
        }
        PlayAnimState("OutOfBreath", 0.15f, true);

        if (audioSource != null && outOfBreathClip != null)
        {
            audioSource.PlayOneShot(outOfBreathClip);
        }

        yield return new WaitForSeconds(duration);

        isFrozen = false;
        SetFreezeVFX(false);
        SetEyeColor(config != null ? config.aggroEyeColor : Color.red);
        if (config != null) SetEyeIntensity(config.aggroEyeIntensity);
        currentState = GhostState.Chase;
        freezeRoutine = null;
    }

    private IEnumerator RoutineKnockbackAndFreeze(Vector3 pushDirection, float pushDistance, float freezeDuration)
    {
        isFrozen = true;
        currentMoveSpeed = 0f;
        SetAnimatorSpeed(0f);
        SetFreezeVFX(true);
        SetEyeColor(config != null ? config.stunEyeColor : freezeVFXColor);
        if (config != null) SetEyeIntensity(config.normalEyeIntensity * 0.8f);

        if (animator != null && config != null && !string.IsNullOrEmpty(config.stunTriggerName))
        {
            animator.SetTrigger(config.stunTriggerName);
        }
        PlayAnimState("OutOfBreath", 0.1f, true);

        if (audioSource != null && outOfBreathClip != null)
        {
            audioSource.PlayOneShot(outOfBreathClip);
        }

        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude > 0.001f) pushDirection.Normalize();

        float knockDuration = 0.22f;
        float elapsed = 0f;
        float speed = pushDistance / knockDuration;

        while (elapsed < knockDuration)
        {
            SafeMove(pushDirection * (speed * Time.deltaTime));
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(Mathf.Max(0.1f, freezeDuration - knockDuration));

        isFrozen = false;
        SetFreezeVFX(false);
        SetEyeColor(config != null ? config.aggroEyeColor : Color.red);
        if (config != null) SetEyeIntensity(config.aggroEyeIntensity);
        currentState = GhostState.Chase;
        freezeRoutine = null;
    }

    /// <summary>
    /// Bật/tắt hiệu ứng vòng xích ma thuật và đèn phong ấn quanh thân ma
    /// </summary>
    private void SetFreezeVFX(bool active)
    {
        EnsureFreezeVFX();
        if (freezeVFXRoot != null)
        {
            freezeVFXRoot.SetActive(active);
        }
        if (freezeLight != null)
        {
            freezeLight.enabled = active;
        }
    }

    private void EnsureFreezeVFX()
    {
        if (freezeVFXRoot != null) return;

        Transform existing = transform.Find("FreezeVFX_Root");
        if (existing != null)
        {
            freezeVFXRoot = existing.gameObject;
            freezeLight = freezeVFXRoot.GetComponentInChildren<Light>();
            return;
        }

        GameObject root = new GameObject("FreezeVFX_Root");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        // Vòng phong ấn 1 (ngang ngực)
        GameObject ring1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring1.name = "MagicBindingRing_1";
        ring1.transform.SetParent(root.transform, false);
        ring1.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        ring1.transform.localScale = new Vector3(1.15f, 0.035f, 1.15f);
        Collider c1 = ring1.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);

        // Vòng phong ấn 2 (ngang eo, xoay nghiêng)
        GameObject ring2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring2.name = "MagicBindingRing_2";
        ring2.transform.SetParent(root.transform, false);
        ring2.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        ring2.transform.localRotation = Quaternion.Euler(22f, 45f, 15f);
        ring2.transform.localScale = new Vector3(1.25f, 0.035f, 1.25f);
        Collider c2 = ring2.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);

        // Gán Material phát sáng URP Lit màu xanh lam (quản lý vòng đời giải phóng chống rò rỉ bộ nhớ)
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        runtimeFreezeMaterial = new Material(urpLit);
        runtimeFreezeMaterial.color = freezeVFXColor;
        runtimeFreezeMaterial.EnableKeyword("_EMISSION");
        runtimeFreezeMaterial.SetColor("_EmissionColor", freezeVFXColor * 4.0f);
        ring1.GetComponent<MeshRenderer>().sharedMaterial = runtimeFreezeMaterial;
        ring2.GetComponent<MeshRenderer>().sharedMaterial = runtimeFreezeMaterial;

        // Đèn tỏa sáng ma mị
        GameObject lightObj = new GameObject("FreezePointLight");
        lightObj.transform.SetParent(root.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        Light pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = freezeVFXColor;
        pLight.intensity = 4.5f;
        pLight.range = 5.0f;
        pLight.shadows = LightShadows.None;

        freezeLight = pLight;
        freezeVFXRoot = root;
        freezeVFXRoot.SetActive(false);
    }

    // =========================================================================
    // VẼ GIZMOS TRỰC QUAN TRONG UNITY SCENE VIEW
    // =========================================================================

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;

        // Vòng tuần tra
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? spawnPosition : transform.position, config.patrolRadius);

        // Vòng phát hiện
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, config.detectionRadius);

        // Vòng tầm đánh
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, config.attackRange);

        // Vòng nhảy vồ
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, config.leapMaxDistance);

        // Cảm biến vật cản
        Vector3 obsOrigin = transform.TransformPoint(config.obstacleCheckOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(obsOrigin, config.obstacleCheckRadius);
        Gizmos.DrawLine(obsOrigin, obsOrigin + transform.forward * config.obstacleAvoidanceDistance);

        // Cảm biến mặt đất
        Vector3 gOrigin = transform.TransformPoint(config.groundCheckOffset);
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(gOrigin, gOrigin + Vector3.down * config.groundCheckDistance);

        // Vùng cự ly đẩy lùi mềm bằng Raycast (Raycast Separation)
        if (config.useRaycastSeparation)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, config.separationDistance);
        }

        // Tầm quét đòn đánh bằng Raycast/SphereCast
        if (config.useRaycastAttack)
        {
            Vector3 eyePos = transform.TransformPoint(config.visionEyeOffset);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(eyePos, config.attackRaycastRadius);
            Gizmos.DrawLine(eyePos, eyePos + transform.forward * config.attackRaycastDistance);
            Gizmos.DrawWireSphere(eyePos + transform.forward * config.attackRaycastDistance, config.attackRaycastRadius);
        }
    }
}

