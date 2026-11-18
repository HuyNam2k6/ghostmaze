using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nhân vật Máy (Human Bot) xuất hiện trong trận khi người chơi bấm [Thêm Máy] ở phòng chờ:
/// - Tự động đi tuần / chạy trốn khi thấy Ma Bí Ngô lại gần.
/// - Có máu (100 HP) để Ma Bí Ngô săn đuổi và hạ gục giống người chơi thật.
/// </summary>
public class HumanBotTarget : MonoBehaviour
{
    public static readonly List<HumanBotTarget> ActiveBots = new List<HumanBotTarget>();

    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float walkSpeed = 3.8f;
    public float fleeSpeed = 5.8f;
    [Tooltip("Tầm phát hiện Ma Bí Ngô bình thường để bỏ chạy (m)")]
    public float ghostDetectRange = 11.0f;
    [Tooltip("Tầm phát hiện khi Ma Bí Ngô đang ngụy trang hoặc ẩn thân (m)")]
    public float disguisedGhostDetectRange = 2.0f;
    public string botDisplayName = "Bot";
    public bool isDead { get; private set; }

    private Animator botAnimator;
    private bool hasSpeedParam;
    private bool hasGroundedParam;
    private bool hasTakeHitParam;
    private bool hasDeathParam;
    private CharacterController controller;
    private Transform nameplateTransform;
    private Vector3 wanderTarget;
    private Vector3 spawnOrigin;
    private Vector3 mapCenter;
    private float nextWanderPickTime;
    private float lastHitTime = -10f;
    private static Camera s_CachedMainCamera;
    private static readonly int s_SpeedParamHash = Animator.StringToHash("Speed");
    private static readonly int s_IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int s_TakeHitHash = Animator.StringToHash("TakeHit");
    private static readonly int s_DeathHash = Animator.StringToHash("Death");
    private static readonly RaycastHit[] s_BotRayHits = new RaycastHit[8];

    private static Camera GetMainCam()
    {
        if (s_CachedMainCamera == null || !s_CachedMainCamera.isActiveAndEnabled)
        {
            s_CachedMainCamera = Camera.main;
        }
        return s_CachedMainCamera;
    }

    public static void ClearAllBots()
    {
        for (int i = ActiveBots.Count - 1; i >= 0; i--)
        {
            if (ActiveBots[i] != null)
            {
                Destroy(ActiveBots[i].gameObject);
            }
        }
        ActiveBots.Clear();
    }

    public static void SpawnBots(int count, Transform templatePlayerRoot, Vector3 centerPos)
    {
        ClearAllBots();
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = FindSafeSpawnPoint(centerPos, i, count);
            GameObject botGo = new GameObject($"HumanBot_{i + 1}");
            botGo.transform.position = spawnPos;

            CharacterController cc = botGo.AddComponent<CharacterController>();
            CharacterController templateCC = templatePlayerRoot != null ? templatePlayerRoot.GetComponent<CharacterController>() : null;
            if (templateCC != null && templatePlayerRoot != null)
            {
                float sy = Mathf.Abs(templatePlayerRoot.lossyScale.y);
                float sxz = Mathf.Max(Mathf.Abs(templatePlayerRoot.lossyScale.x), Mathf.Abs(templatePlayerRoot.lossyScale.z));
                cc.height = Mathf.Max(0.8f, templateCC.height * sy);
                cc.radius = Mathf.Max(0.2f, templateCC.radius * sxz);
                cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            }
            else
            {
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
            }
            cc.slopeLimit = 65f;
            cc.stepOffset = 0.35f;

            // Sao chép mô hình nhân vật từ Player mẫu với đúng lossyScale thực tế (tránh bị to gấp nhiều lần)
            if (templatePlayerRoot != null)
            {
                Animator srcAnim = templatePlayerRoot.GetComponentInChildren<Animator>(true);
                if (srcAnim != null)
                {
                    GameObject modelCopy = Instantiate(srcAnim.gameObject, botGo.transform, false);
                    modelCopy.name = "BotModel";
                    modelCopy.transform.localPosition = Vector3.zero;
                    modelCopy.transform.localRotation = Quaternion.identity;
                    modelCopy.transform.localScale = srcAnim.transform.lossyScale;
                    modelCopy.SetActive(true);

                    foreach (var comp in modelCopy.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (comp == null) continue;
                        if (comp is ThirdPersonPlayerController || comp is PlayerHealth || comp is PlayerAnimationEventForwarder || comp is PentacleHandSealVFX || comp is GhostChainStunVFX)
                        {
                            Destroy(comp);
                        }
                    }
                    foreach (Transform t in modelCopy.GetComponentsInChildren<Transform>(true))
                    {
                        if (t != null && (t.name == "PentacleHandSeal_Root" || t.name == "GhostBindingChains_Root"))
                        {
                            Destroy(t.gameObject);
                        }
                    }
                    foreach (var col in modelCopy.GetComponentsInChildren<Collider>(true))
                    {
                        col.enabled = false;
                    }
                    foreach (var l in modelCopy.GetComponentsInChildren<Light>(true))
                    {
                        l.enabled = false;
                    }
                }
            }

            HumanBotTarget bot = botGo.AddComponent<HumanBotTarget>();
            bot.botDisplayName = $"{PhotonMultiplayerChatVoice.GetBotDisplayName(i)} [Bot]";
            bot.spawnOrigin = spawnPos;
            bot.mapCenter = centerPos;
            bot.Init(cc);
        }
    }

    public static bool TryHitNearestBot(Vector3 originPos, float range, float damage)
    {
        HumanBotTarget best = null;
        float bestSqr = range * range;

        for (int i = ActiveBots.Count - 1; i >= 0; i--)
        {
            HumanBotTarget bot = ActiveBots[i];
            if (bot == null || bot.isDead || !bot.gameObject.activeInHierarchy) continue;

            Vector3 botCenter = bot.transform.position + Vector3.up * 0.9f;
            float sqr = (botCenter - originPos).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = bot;
            }
        }

        if (best != null)
        {
            best.TakeDamage(damage, originPos);
            return true;
        }
        return false;
    }

    private void OnEnable()
    {
        if (!ActiveBots.Contains(this)) ActiveBots.Add(this);
    }

    private void OnDisable()
    {
        ActiveBots.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveBots.Remove(this);
    }

    private void Init(CharacterController cc)
    {
        controller = cc;
        botAnimator = GetComponentInChildren<Animator>(true);
        if (botAnimator != null)
        {
            botAnimator.applyRootMotion = false;
            var animParams = botAnimator.parameters;
            for (int i = 0; i < animParams.Length; i++)
            {
                int h = animParams[i].nameHash;
                if (h == s_SpeedParamHash) hasSpeedParam = true;
                else if (h == s_IsGroundedHash) hasGroundedParam = true;
                else if (h == s_TakeHitHash) hasTakeHitParam = true;
                else if (h == s_DeathHash) hasDeathParam = true;
            }
            if (hasGroundedParam) botAnimator.SetBool(s_IsGroundedHash, true);
        }
        currentHealth = maxHealth;
        CreateNameplate();
        PickNextWanderTarget();
    }

    private void CreateNameplate()
    {
        GameObject nameGo = new GameObject("BotNameplate");
        nameGo.transform.SetParent(transform, false);
        nameGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        nameGo.transform.localScale = Vector3.one * 0.03f;

        TextMesh tm = nameGo.AddComponent<TextMesh>();
        tm.text = botDisplayName;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.75f, 0.95f, 1.0f, 1f);
        tm.fontStyle = FontStyle.Bold;
        nameplateTransform = nameGo.transform;
    }

    private void Update()
    {
        if (isDead || controller == null || !controller.enabled) return;

        // Dừng di chuyển khi trận đấu đã kết thúc
        if (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.IsFinished)
        {
            if (botAnimator != null && hasSpeedParam) botAnimator.SetFloat(s_SpeedParamHash, 0f);
            Camera finCam = GetMainCam();
            if (nameplateTransform != null && finCam != null)
            {
                nameplateTransform.rotation = Quaternion.LookRotation(nameplateTransform.position - finCam.transform.position);
            }
            return;
        }

        // Bảo vệ rơi vực: nếu lỡ rơi xuống vực sâu thì dịch chuyển lại điểm an toàn
        if (transform.position.y < -5f)
        {
            controller.enabled = false;
            transform.position = spawnOrigin != Vector3.zero ? spawnOrigin : (mapCenter + Vector3.up * 0.5f);
            controller.enabled = true;
            PickNextWanderTarget();
        }

        Transform ghostTransform = FindActiveGhostTransform(out bool ghostIsStealthed);
        bool fleeing = false;
        Vector3 moveDir = Vector3.zero;

        if (ghostTransform != null)
        {
            Vector3 away = transform.position - ghostTransform.position;
            away.y = 0f;
            float dist = away.magnitude;
            float detectDist = ghostIsStealthed ? disguisedGhostDetectRange : ghostDetectRange;
            if (dist < detectDist && dist > 0.01f)
            {
                fleeing = true;
                moveDir = away / dist;
            }
        }

        if (!fleeing)
        {
            Vector3 toTarget = wanderTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 2.25f || Time.time >= nextWanderPickTime)
            {
                PickNextWanderTarget();
                toTarget = wanderTarget - transform.position;
                toTarget.y = 0f;
            }
            if (toTarget.sqrMagnitude > 0.01f)
            {
                moveDir = toTarget.normalized;
            }
        }

        // Trong 10s đầu (CircleWait_10s), giữ Bot ở bên trong vòng tròn an toàn giữa map
        if (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.currentPhase == SurvivalTimerUI.MatchPhase.CircleWait_10s)
        {
            float maxR = Mathf.Max(3.5f, SurvivalTimerUI.Instance.startCircleRadius - 1.2f);
            Vector3 offsetFromCenter = (transform.position + moveDir * 1.2f) - mapCenter;
            offsetFromCenter.y = 0f;
            if (offsetFromCenter.sqrMagnitude > maxR * maxR)
            {
                moveDir = (-offsetFromCenter).normalized;
                PickNextWanderTarget();
            }
        }

        // Tránh tường phía trước hoặc mép vực không có sàn đỡ phía trước (0 GC, loại bỏ va chạm với chính thân Bot)
        if (moveDir.sqrMagnitude > 0.01f)
        {
            bool wallAhead = RaycastEnvironmentIgnoreSelf(transform.position + Vector3.up * 0.8f, moveDir, 1.3f);
            bool groundAhead = RaycastEnvironmentIgnoreSelf(transform.position + moveDir * 1.4f + Vector3.up * 1.0f, Vector3.down, 3.5f);
            if (wallAhead || !groundAhead)
            {
                float turnAngle = !groundAhead ? (Random.value > 0.5f ? 140f : -140f) : (Random.value > 0.5f ? 95f : -95f);
                Vector3 rotated = Quaternion.Euler(0f, turnAngle, 0f) * moveDir;
                moveDir = rotated.normalized;
                if (!fleeing) PickNextWanderTarget();
            }
        }

        float speed = fleeing ? fleeSpeed : walkSpeed;
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        Vector3 velocity = moveDir * speed + Vector3.down * 9.8f;
        controller.Move(velocity * Time.deltaTime);

        if (botAnimator != null && hasSpeedParam)
        {
            botAnimator.SetFloat(s_SpeedParamHash, moveDir.sqrMagnitude > 0.01f ? speed : 0f);
        }

        Camera mainCam = GetMainCam();
        if (nameplateTransform != null && mainCam != null)
        {
            nameplateTransform.rotation = Quaternion.LookRotation(nameplateTransform.position - mainCam.transform.position);
        }
    }

    private bool RaycastEnvironmentIgnoreSelf(Vector3 origin, Vector3 dir, float maxDist)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, s_BotRayHits, maxDist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = s_BotRayHits[i].collider;
            if (col == null) continue;
            if (col == controller || col.transform == transform || col.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    public void TakeDamage(float amount, Vector3 attackerPos)
    {
        if (isDead || Time.time < lastHitTime + 0.5f) return;
        lastHitTime = Time.time;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        if (botAnimator != null && hasTakeHitParam)
        {
            botAnimator.SetTrigger(s_TakeHitHash);
        }

        if (currentHealth <= 0f)
        {
            isDead = true;
            ActiveBots.Remove(this);
            if (controller != null) controller.enabled = false;
            if (botAnimator != null)
            {
                if (hasSpeedParam) botAnimator.SetFloat(s_SpeedParamHash, 0f);
                if (hasDeathParam) botAnimator.SetTrigger(s_DeathHash);
            }
            if (SurvivalTimerUI.Instance != null)
            {
                SurvivalTimerUI.Instance.OnPlayerDied(null);
            }
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            {
                PlayerHealth.Instance.OnSpectateRosterChanged();
            }
            StartCoroutine(RoutineDestroyAfterDeath());
        }
    }

    private IEnumerator RoutineDestroyAfterDeath()
    {
        yield return new WaitForSeconds(2.0f);
        Destroy(gameObject);
    }

    private void PickNextWanderTarget()
    {
        Vector2 rnd = Random.insideUnitCircle * 18f;
        wanderTarget = transform.position + new Vector3(rnd.x, 0f, rnd.y);
        nextWanderPickTime = Time.time + Random.Range(3.5f, 6.5f);
    }

    private static Transform FindActiveGhostTransform(out bool isStealthed)
    {
        isStealthed = false;
        if (PumpkinGhostAI.PlayerControlledGhost != null && PumpkinGhostAI.PlayerControlledGhost.gameObject.activeInHierarchy)
        {
            isStealthed = PumpkinGhostAI.PlayerControlledGhost.IsDisguisedOrHidden;
            return PumpkinGhostAI.PlayerControlledGhost.transform;
        }
        var ghosts = PumpkinGhostAI.ActiveGhosts;
        for (int i = 0; i < ghosts.Count; i++)
        {
            PumpkinGhostAI ai = ghosts[i];
            if (ai != null && ai.gameObject.activeInHierarchy)
            {
                isStealthed = ai.IsDisguisedOrHidden;
                return ai.transform;
            }
        }
        return null;
    }

    private static Vector3 FindSafeSpawnPoint(Vector3 centerPos, int index, int total)
    {
        float angle = (index / (float)Mathf.Max(1, total)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
        float dist = Random.Range(8f, 16f);
        Vector3 candidate = centerPos + new Vector3(Mathf.Cos(angle) * dist, 6f, Mathf.Sin(angle) * dist);
        if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 14f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.1f;
        }
        return centerPos + new Vector3(Mathf.Cos(angle) * 6f, 0.1f, Mathf.Sin(angle) * 6f);
    }
}
