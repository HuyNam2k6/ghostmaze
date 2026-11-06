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

    // Bộ nhớ đệm Animation tránh gọi lặp
    private string currentAnimState = "";

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PumpkinGhostConfig>();
        }

        spawnPosition = transform.position;
        ApplyColliderSettingsFromConfig();
    }

    private void OnValidate()
    {
        // Cập nhật ngay tức thì trong Scene View khi người thiết kế chỉnh thông số trong Inspector
        if (controller == null) controller = GetComponent<CharacterController>();
        if (config != null && controller != null)
        {
            ApplyColliderSettingsFromConfig();
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
            controller.Move(steerDir * currentMoveSpeed * Time.deltaTime);
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
            controller.Move(steerDir * currentMoveSpeed * Time.deltaTime);
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
            controller.Move(moveStep);

            elapsed += Time.deltaTime;

            if (!hitPlayer && targetPlayer != null)
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
            yield return null;
        }

        isLeaping = false;

        if (!hitPlayer)
        {
            yield return StartCoroutine(RoutineOutOfBreath());
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
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

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
                    controller.Move(steer * (config.chaseSpeed * 1.3f) * Time.deltaTime);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }

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

    private bool CheckObstacleHit(Vector3 origin, Vector3 dir, float radius, float dist)
    {
        if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, dist, config.obstacleLayers))
        {
            // Bỏ qua nếu chạm vào người chơi mục tiêu
            if (targetPlayer != null && (hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer)))
            {
                return false;
            }
            // Bỏ qua nếu chạm vào chính mình
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return false;
            }
            return true;
        }
        return false;
    }

    private bool CanSeePlayer(out float distance)
    {
        distance = float.MaxValue;
        if (targetPlayer == null) return false;

        distance = Vector3.Distance(transform.position, targetPlayer.position);

        // 1. Phát hiện cực gần (nghe tiếng chân)
        if (distance <= config.closeProximityDistance)
        {
            return true;
        }

        // 2. Kiểm tra nón tầm nhìn
        if (distance <= config.detectionRadius)
        {
            Vector3 dirToPlayer = (targetPlayer.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle <= config.fieldOfViewAngle * 0.5f)
            {
                // Kiểm tra có bị tường chắn tầm mắt không
                Vector3 eyePos = transform.TransformPoint(config.visionEyeOffset);
                Vector3 playerEyePos = targetPlayer.TransformPoint(config.playerTargetEyeOffset);

                if (!Physics.Linecast(eyePos, playerEyePos, out RaycastHit hit, config.visionBlockerLayers) ||
                    hit.transform == targetPlayer || hit.transform.IsChildOf(targetPlayer))
                {
                    return true;
                }
            }
        }

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
        if (isLeaping) return;

        bool grounded = controller.isGrounded;
        if (!grounded)
        {
            // Bổ sung kiểm tra Raycast mặt đất từ cấu hình
            Vector3 origin = transform.TransformPoint(config.groundCheckOffset);
            grounded = Physics.Raycast(origin, Vector3.down, config.groundCheckDistance, config.groundLayers);
        }

        if (grounded)
        {
            verticalVelocity.y = -2.5f;
        }
        else
        {
            verticalVelocity.y += -config.gravity * Time.deltaTime;
        }
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void CheckAbyssProtection()
    {
        if (transform.position.y < config.abyssThresholdY)
        {
            controller.enabled = false;
            transform.position = spawnPosition;
            verticalVelocity = Vector3.zero;
            controller.enabled = true;
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

    private void ApplyColliderSettingsFromConfig()
    {
        if (controller == null || config == null) return;
        controller.radius = config.colliderRadius;
        controller.height = config.colliderHeight;
        controller.center = config.colliderCenter;
        controller.slopeLimit = config.slopeLimit;
        controller.stepOffset = config.stepOffset;
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
    }
}

