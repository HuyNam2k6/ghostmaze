using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý toàn bộ tiến trình trận đấu trốn tìm ma bí ngô (Ghost Maze Match Flow):
/// - Pha 1 (10s đầu): Cả 4 người xuất phát trong Vòng Tròn Nhỏ ở giữa map (Central Plaza), không thể ra ngoài. Ma ngủ say.
/// - Pha 2 (20s tiếp theo = từ 10s -> 30s): Vòng tròn mở, người chơi tỏa ra khắp mê cung tìm chỗ trốn hú! Ma vẫn chưa đi săn.
/// - Pha 3 (Sau 30s = 3 phút sinh tồn): Ma tỉnh giấc gầm thét, mắt đỏ rực và bắt đầu truy lùng người!
/// - Pha 4 (Hết 3 phút hoặc hết người):
///     + Hết người: MA WIN! Bế ma về giữa map.
///     + Hết 3 phút còn người: NGƯỜI WIN! Bế tất cả người sống về giữa map. Nhấn [P] chơi lại.
/// </summary>
public class SurvivalTimerUI : MonoBehaviour
{
    public static SurvivalTimerUI Instance { get; private set; }

    public enum MatchPhase
    {
        Lobby,          // Đang ở sảnh chờ phòng
        CircleWait_10s, // 10s đầu: Ở trong vòng tròn nhỏ giữa map, chưa được ra ngoài
        Hide_20s,       // 20s tiếp: Vòng tròn mở, tỏa ra tìm chỗ trốn hú (Ma vẫn ngủ say)
        Hunting_3Min,   // 3 phút: Ma tỉnh giấc đi săn con người
        Finished        // Kết thúc trận đấu
    }

    [Header("Cấu Hình Các Pha Trận Đấu")]
    [Tooltip("Giai đoạn hiện tại của trận đấu")]
    public MatchPhase currentPhase = MatchPhase.Lobby;

    [Tooltip("Thời gian chờ trong vòng tròn ban đầu (giây, mặc định 10s)")]
    public float circleWaitDuration = 10f;

    [Tooltip("Thời gian tản ra tìm chỗ trốn hú trước khi ma thức giấc (giây, mặc định 20s)")]
    public float hideDuration = 20f;

    [Tooltip("Thời gian ma đi săn người sống sót (giây, mặc định 180s = 3 phút)")]
    [Range(10f, 900f)] public float huntingDuration = 180f;

    [Tooltip("Bán kính vòng tròn nhỏ an toàn ban đầu ở giữa map (m)")]
    [Range(2f, 8f)] public float startCircleRadius = 3.8f;

    [Tooltip("Tự động bắt đầu trận đấu ngay khi vào game (nếu không dùng sảnh chờ Photon)")]
    public bool autoStart = false;

    [Header("Vị Trí Giữa Map (Central Plaza)")]
    [Tooltip("Tọa độ giữa map để dịch chuyển (bế) người hoặc ma khi bắt đầu / kết thúc")]
    public Vector3 mapCenterPosition = new Vector3(0f, 0.45f, 0f);

    [Header("Tham Chiếu Giao Diện (HUD Elements)")]
    [Tooltip("Text hiển thị đồng hồ đếm ngược (VD: 03:00)")]
    public Text timerDigitsText;

    [Tooltip("Text tiêu đề phụ phía trên đồng hồ")]
    public Text timerLabelText;

    [Tooltip("Thanh tiến trình thời gian bên dưới đồng hồ")]
    public RectTransform timerProgressFill;

    // Trạng thái nội bộ
    private float phaseTimer = 0f;
    private float remainingTime = 180f;
    private int lastDisplayedSecond = -1;
    private bool isRunning = false;
    private bool isFinished = false;
    public bool IsFinished => isFinished;

    private GameObject endResultBannerObj;
    private GameObject floatingNoticeBannerObj;
    private Text floatingNoticeText;
    private float floatingNoticeTimer = 0f;

    // Hiệu ứng vòng tròn rào chắn giữa map
    private GameObject startCircleVFXRoot;
    private LineRenderer circleLineRenderer;

    private readonly Color normalColor    = new Color(0.95f, 0.98f, 1.00f, 1f);
    private readonly Color warningColor   = new Color(1.00f, 0.78f, 0.22f, 1f);
    private readonly Color urgentColor    = new Color(1.00f, 0.25f, 0.28f, 1f);
    private readonly Color winColor       = new Color(0.20f, 0.92f, 0.50f, 1f);
    private readonly Color ghostWinColor  = new Color(1.00f, 0.28f, 0.34f, 1f);
    private readonly Color circleLineColor = new Color(0.10f, 0.85f, 1.00f, 0.95f);

    private void Awake()
    {
        Instance = this;
        remainingTime = huntingDuration;
        currentPhase = MatchPhase.Lobby;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        AutoBindReferencesIfNeeded();

        // Nếu chế độ phòng chờ Photon không bật yêu cầu đủ phòng, tự động bắt đầu sequence
        if (autoStart)
        {
            StartMatchSequence();
        }
        else
        {
            // Ban đầu đóng băng ma trước khi trận đấu kích hoạt
            PumpkinGhostAI.FreezeAllGhosts();
            if (timerLabelText != null)
            {
                timerLabelText.text = "⚔️ ĐANG CHỜ TRẬN ĐẤU BẮT ĐẦU";
                timerLabelText.color = new Color(0.75f, 0.85f, 1.0f, 0.95f);
            }
            if (timerDigitsText != null)
            {
                timerDigitsText.text = "--:--";
                timerDigitsText.color = normalColor;
            }
        }
    }

    private void Update()
    {
        // 1. Cho phép bấm phím P để chơi lại khi trận đấu đã kết thúc
        if (isFinished)
        {
            if (ReadRestartInputDown())
            {
                RestartMatch();
            }
            return;
        }

        // 2. Cập nhật Banner thông báo nổi bật tự mờ dần
        if (floatingNoticeTimer > 0f)
        {
            floatingNoticeTimer -= Time.deltaTime;
            if (floatingNoticeTimer <= 0f && floatingNoticeBannerObj != null)
            {
                floatingNoticeBannerObj.SetActive(false);
            }
        }

        if (!isRunning) return;

        // =========================================================================
        // PHA 1: 10 GIÂY ĐẦU - Ở TRONG VÒNG TRÒN GIỮA MAP, MA NGỦ SAY
        // =========================================================================
        if (currentPhase == MatchPhase.CircleWait_10s)
        {
            phaseTimer -= Time.deltaTime;

            // Đảm bảo ma tiếp tục bị phong ấn
            PumpkinGhostAI.FreezeAllGhosts();

            // Ràng buộc vị trí người chơi không được vượt ra ngoài bán kính vòng tròn
            ConstrainLocalPlayerInsideStartCircle();

            // Hiển thị đếm ngược 10s
            int sec = Mathf.CeilToInt(Mathf.Max(0f, phaseTimer));
            if (sec != lastDisplayedSecond)
            {
                lastDisplayedSecond = sec;
                if (timerDigitsText != null)
                {
                    timerDigitsText.text = $"00:{sec:00}";
                    timerDigitsText.color = warningColor;
                }
                if (timerLabelText != null)
                {
                    timerLabelText.text = "⏳ CHUẨN BỊ XUẤT PHÁT (TRONG VÒNG TRÒN)";
                    timerLabelText.color = warningColor;
                }
            }

            if (timerProgressFill != null && circleWaitDuration > 0f)
            {
                float ratio = Mathf.Clamp01(phaseTimer / circleWaitDuration);
                SetProgressRatio(ratio);
            }

            if (phaseTimer <= 0f)
            {
                // Hết 10s -> Chuyển sang Pha 2: Mở vòng tròn cho người chơi chạy trốn!
                currentPhase = MatchPhase.Hide_20s;
                phaseTimer = hideDuration;
                lastDisplayedSecond = -1;

                // Tắt rào chắn ma thuật
                if (startCircleVFXRoot != null)
                {
                    startCircleVFXRoot.SetActive(false);
                }

                ShowFloatingBanner("🏃 VÒNG TRÒN ĐÃ MỞ! HÃY TỎA RA TÌM CHỖ TRỐN HÚ!\n(Ma sẽ tỉnh giấc sau 20s)", new Color(0.15f, 0.95f, 0.75f), 4.0f);
                Debug.Log("<color=#2ECC71>====================================================\n🏃 [GIAI ĐOẠN 2] VÒNG TRÒN ĐÃ MỞ! NGƯỜI CHƠI CÓ 20 GIÂY ĐỂ TRỐN HÚ!\n====================================================</color>");
            }
            return;
        }

        // =========================================================================
        // PHA 2: 20 GIÂY TIẾP THEO (TỪ 10s -> 30s) - TỎA RA TRỐN HÚ, MA VẪN NGỦ SAY
        // =========================================================================
        if (currentPhase == MatchPhase.Hide_20s)
        {
            phaseTimer -= Time.deltaTime;

            // Ma vẫn tiếp tục bị khóa cứng trong 20s này
            PumpkinGhostAI.FreezeAllGhosts();

            int sec = Mathf.CeilToInt(Mathf.Max(0f, phaseTimer));
            if (sec != lastDisplayedSecond)
            {
                lastDisplayedSecond = sec;
                if (timerDigitsText != null)
                {
                    timerDigitsText.text = $"00:{sec:00}";
                    timerDigitsText.color = new Color(0.2f, 0.92f, 0.95f, 1f);
                }
                if (timerLabelText != null)
                {
                    timerLabelText.text = "🏃 VÒNG TRÒN ĐÃ MỞ! MA TỈNH GIẤC SAU:";
                    timerLabelText.color = new Color(0.2f, 0.92f, 0.95f, 1f);
                }
            }

            if (timerProgressFill != null && hideDuration > 0f)
            {
                float ratio = Mathf.Clamp01(phaseTimer / hideDuration);
                SetProgressRatio(ratio);
            }

            if (phaseTimer <= 0f)
            {
                // Hết 30s (10s + 20s) -> Chuyển sang Pha 3: MA TỈNH GIẤC ĐI SĂN!
                currentPhase = MatchPhase.Hunting_3Min;
                remainingTime = huntingDuration;
                lastDisplayedSecond = -1;

                // Thả ma ra săn người!
                PumpkinGhostAI.ReleaseAllGhostsToHunt();

                ShowFloatingBanner("🔥 MA ĐÃ TỈNH GIẤC! BẮT ĐẦU ĐI SĂN CON NGƯỜI! 🔥", new Color(1f, 0.30f, 0.20f), 4.5f);
                Debug.Log("<color=#FF4757>====================================================\n🔥 [GIAI ĐOẠN 3] HẾT 30 GIÂY: MA ĐÃ TỈNH GIẤC! BẮT ĐẦU SĂN LÙNG NGƯỜI TRONG 3 PHÚT!\n====================================================</color>");
            }
            return;
        }

        // =========================================================================
        // PHA 3: 3 PHÚT SINH TỒN (180s) - MA ĐI SĂN NGƯỜI
        // =========================================================================
        if (currentPhase == MatchPhase.Hunting_3Min)
        {
            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isRunning = false;
                currentPhase = MatchPhase.Finished;
                isFinished = true;
                UpdateTimerDisplay(0);
                OnTimerCompleted();
                return;
            }

            if (timerProgressFill != null && huntingDuration > 0f)
            {
                float ratio = Mathf.Clamp01(remainingTime / huntingDuration);
                SetProgressRatio(ratio);
            }

            int currentSec = Mathf.CeilToInt(remainingTime);
            if (currentSec != lastDisplayedSecond)
            {
                lastDisplayedSecond = currentSec;
                UpdateTimerDisplay(currentSec);
            }

            // Hiệu ứng nhấp nháy cảnh báo khi còn dưới 30 giây cuối
            if (remainingTime <= 30f && timerDigitsText != null)
            {
                float pulse = (Mathf.Sin(Time.time * 6.5f) + 1f) * 0.5f;
                timerDigitsText.color = Color.Lerp(warningColor, urgentColor, pulse);
            }
        }
    }

    public void ResetTimer(bool startImmediately = true)
    {
        if (startImmediately)
        {
            StartMatchSequence();
        }
        else
        {
            isRunning = false;
            currentPhase = MatchPhase.Lobby;
        }
    }

    /// <summary>
    /// Kích hoạt chuỗi trận đấu:
    /// 1. Dịch chuyển người chơi về vòng tròn an toàn ở giữa map.
    /// 2. Bật hiệu ứng rào chắn ma thuật vòng tròn.
    /// 3. Khóa chân ma bí ngô.
    /// 4. Đếm ngược 10s đầu tiên.
    /// </summary>
    public void StartMatchSequence()
    {
        Debug.Log("<color=#48C5FF>====================================================\n⚔️ [TRẬN ĐẤU BẮT ĐẦU] BẮT ĐẦU CHUỖI TRẬN ĐẤU:\n- Pha 1 (10s đầu): Tập trung trong vòng tròn giữa map.\n- Pha 2 (20s trốn hú): Vòng tròn mở, tản ra tìm chỗ nấp.\n- Pha 3 (Sau 30s): Ma tỉnh giấc đi săn trong 3 phút!\n====================================================</color>");

        isFinished = false;
        isRunning = true;
        currentPhase = MatchPhase.CircleWait_10s;
        phaseTimer = circleWaitDuration;
        remainingTime = huntingDuration;
        lastDisplayedSecond = -1;

        if (endResultBannerObj != null)
        {
            Destroy(endResultBannerObj);
        }

        // Hồi sinh và đặt lại máu người chơi nếu bị hạ từ trước
        PlayerHealth ph = PlayerHealth.Instance;
        if (ph == null) ph = FindAnyObjectByType<PlayerHealth>();
        if (ph != null)
        {
            ph.ResetHealth();
        }

        // Dịch chuyển người chơi về tâm vòng tròn giữa map (Central Plaza)
        Vector3 center = ResolveMapCenter();
        if (ThirdPersonPlayerController.Instance != null)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
            SafeTeleport(ThirdPersonPlayerController.Instance.gameObject, center + randomOffset, Quaternion.identity);
        }

        // Khóa toàn bộ quái ma bí ngô
        PumpkinGhostAI.FreezeAllGhosts();

        // Kích hoạt hiệu ứng rào chắn vòng tròn ma thuật
        EnsureStartCircleVFX();
        if (startCircleVFXRoot != null)
        {
            startCircleVFXRoot.SetActive(true);
        }

        ShowFloatingBanner("⏳ VÒNG TRÒN AN TOÀN: CHUẨN BỊ XUẤT PHÁT!\n(Đứng trong vòng tròn trong 10 giây đầu)", warningColor, 4.0f);
    }

    /// <summary>
    /// Ràng buộc người chơi không thể đi ra khỏi bán kính vòng tròn trong 10s đầu
    /// </summary>
    private void ConstrainLocalPlayerInsideStartCircle()
    {
        if (ThirdPersonPlayerController.Instance == null) return;

        Vector3 center = ResolveMapCenter();
        Vector3 pPos = ThirdPersonPlayerController.Instance.transform.position;
        Vector2 offset = new Vector2(pPos.x - center.x, pPos.z - center.z);
        float maxAllowedRadius = startCircleRadius - 0.25f;

        if (offset.sqrMagnitude > maxAllowedRadius * maxAllowedRadius)
        {
            offset = offset.normalized * maxAllowedRadius;
            Vector3 clampedPos = new Vector3(center.x + offset.x, pPos.y, center.z + offset.y);
            SafeTeleport(ThirdPersonPlayerController.Instance.gameObject, clampedPos, ThirdPersonPlayerController.Instance.transform.rotation);
        }
    }

    /// <summary>
    /// Tạo hiệu ứng vòng tròn phát sáng và rào chắn hình trụ trong suốt ở giữa map
    /// </summary>
    private void EnsureStartCircleVFX()
    {
        if (startCircleVFXRoot != null) return;

        Vector3 center = ResolveMapCenter();
        startCircleVFXRoot = new GameObject("StartCircleZone_VFX");
        startCircleVFXRoot.transform.position = center;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. Vòng tròn sáng dưới mặt đất (LineRenderer xoay tròn)
        GameObject ringObj = new GameObject("CircleGroundRing");
        ringObj.transform.SetParent(startCircleVFXRoot.transform, false);
        circleLineRenderer = ringObj.AddComponent<LineRenderer>();
        circleLineRenderer.useWorldSpace = false;
        circleLineRenderer.loop = true;
        circleLineRenderer.startWidth = 0.16f;
        circleLineRenderer.endWidth = 0.16f;

        if (shader != null)
        {
            Material lineMat = new Material(shader);
            lineMat.color = circleLineColor;
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", circleLineColor);
            circleLineRenderer.sharedMaterial = lineMat;
        }

        int segments = 48;
        circleLineRenderer.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(rad) * startCircleRadius;
            float z = Mathf.Sin(rad) * startCircleRadius;
            circleLineRenderer.SetPosition(i, new Vector3(x, 0.08f, z));
        }

        // 2. Rào chắn ma thuật hình trụ trong suốt xung quanh (Cylinder Barrier)
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "TranslucentBarrierCylinder";
        cyl.transform.SetParent(startCircleVFXRoot.transform, false);
        cyl.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        cyl.transform.localScale = new Vector3(startCircleRadius * 2f, 1.4f, startCircleRadius * 2f);

        Collider cylCol = cyl.GetComponent<Collider>();
        if (cylCol != null) Destroy(cylCol);

        var mr = cyl.GetComponent<MeshRenderer>();
        if (mr != null && shader != null)
        {
            Material cylMat = new Material(shader);
            Color barrierCol = new Color(0.12f, 0.75f, 1.0f, 0.22f);
            cylMat.color = barrierCol;
            if (cylMat.HasProperty("_BaseColor")) cylMat.SetColor("_BaseColor", barrierCol);
            mr.sharedMaterial = cylMat;
        }

        // 3. Đèn sáng tâm ma thuật (Point Light)
        GameObject lightObj = new GameObject("CircleCenterLight");
        lightObj.transform.SetParent(startCircleVFXRoot.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        Light ptLight = lightObj.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.color = new Color(0.2f, 0.85f, 1.0f);
        ptLight.range = 10f;
        ptLight.intensity = 2.5f;
    }

    private void SetProgressRatio(float ratio)
    {
        if (timerProgressFill == null) return;
        timerProgressFill.anchorMin = Vector2.zero;
        timerProgressFill.anchorMax = new Vector2(ratio, 1f);
        timerProgressFill.offsetMin = Vector2.zero;
        timerProgressFill.offsetMax = Vector2.zero;
    }

    private void UpdateTimerDisplay(int totalSecondsRemaining)
    {
        if (timerDigitsText == null) return;

        totalSecondsRemaining = Mathf.Max(0, totalSecondsRemaining);
        int minutes = totalSecondsRemaining / 60;
        int seconds = totalSecondsRemaining % 60;

        timerDigitsText.text = $"{minutes:00}:{seconds:00}";

        if (totalSecondsRemaining > 60)
        {
            timerDigitsText.color = normalColor;
        }
        else if (totalSecondsRemaining > 30)
        {
            timerDigitsText.color = warningColor;
        }

        if (timerLabelText != null)
        {
            timerLabelText.text = "⏱ THỜI GIAN SINH TỒN TRƯỚC MA BÍ NGÔ";
            timerLabelText.color = new Color(0.85f, 0.92f, 1.0f, 0.95f);
        }
    }

    /// <summary>
    /// Xử lý khi hết 3 phút sinh tồn:
    /// - Nếu còn người sống sót -> NGƯỜI WIN! Bế về giữa map!
    /// - Nếu không còn người -> MA WIN! Bế ma về giữa map!
    /// </summary>
    private void OnTimerCompleted()
    {
        int aliveHumans = GetAliveHumanPlayerCount();
        if (aliveHumans > 0)
        {
            TriggerHumanVictory();
        }
        else
        {
            TriggerGhostVictory();
        }
    }

    /// <summary>
    /// Gọi khi một người chơi bị tử trận -> Kiểm tra nếu hết toàn bộ người thì Ma lập tức Win!
    /// </summary>
    public void OnPlayerDied(PlayerHealth deadPlayer)
    {
        if (isFinished) return;
        StartCoroutine(RoutineCheckAllHumansDead());
    }

    private IEnumerator RoutineCheckAllHumansDead()
    {
        yield return new WaitForSeconds(0.6f);
        if (isFinished) yield break;

        int aliveHumans = GetAliveHumanPlayerCount();
        if (aliveHumans <= 0)
        {
            isRunning = false;
            currentPhase = MatchPhase.Finished;
            isFinished = true;
            TriggerGhostVictory();
        }
    }

    /// <summary>
    /// NGƯỜI CHIẾN THẮNG:
    /// - Hiển thị "Người win" trên màn hình.
    /// - Bế người chơi sống sót về giữa map (Central Plaza).
    /// - Vô hiệu hóa đòn tấn công của ma bí ngô.
    /// </summary>
    public void TriggerHumanVictory()
    {
        isFinished = true;
        isRunning = false;
        currentPhase = MatchPhase.Finished;

        if (timerDigitsText != null)
        {
            timerDigitsText.text = "00:00";
            timerDigitsText.color = winColor;
        }

        if (timerLabelText != null)
        {
            timerLabelText.text = "🏆 NGƯỜI CHIẾN THẮNG!";
            timerLabelText.color = winColor;
        }

        if (timerProgressFill != null)
        {
            timerProgressFill.anchorMax = new Vector2(0f, 1f);
        }

        Debug.Log("<color=#2ECC71>====================================================\n🏆 [KẾT QUẢ] HẾT 3 PHÚT: NGƯỜI CHIẾN THẮNG (HUMANS WIN)!\nBế tất cả người sống sót về giữa map (Central Plaza)!\n====================================================</color>");

        // 1. Bế người chơi sống sót về giữa map
        Vector3 center = ResolveMapCenter();
        PlayerHealth[] allHealth = Object.FindObjectsByType<PlayerHealth>();
        int idx = 0;
        int totalAlive = GetAliveHumanPlayerCount();

        for (int i = 0; i < allHealth.Length; i++)
        {
            PlayerHealth p = allHealth[i];
            if (p != null && !p.isDead && p.gameObject.activeInHierarchy)
            {
                float xOffset = (idx - (totalAlive - 1) * 0.5f) * 2.2f;
                Vector3 targetPos = center + new Vector3(xOffset, 0f, 2.5f);
                SafeTeleport(p.gameObject, targetPos, Quaternion.Euler(0f, 180f, 0f));

                // Bật trạng thái bất tử sau khi thắng
                ThirdPersonPlayerController ctrl = p.GetComponent<ThirdPersonPlayerController>();
                if (ctrl != null && ctrl.playerAnimator != null)
                {
                    ctrl.playerAnimator.speed = 1.0f;
                    ctrl.playerAnimator.CrossFadeInFixedTime("Idle", 0.15f);
                }
                idx++;
            }
        }

        // 2. Vô hiệu hóa / phong ấn toàn bộ Ma Bí Ngô
        PumpkinGhostAI[] allGhosts = Object.FindObjectsByType<PumpkinGhostAI>();
        for (int i = 0; i < allGhosts.Length; i++)
        {
            if (allGhosts[i] != null)
            {
                allGhosts[i].Freeze(9999f);
                allGhosts[i].SetEyeColor(Color.cyan);
            }
        }

        // 3. Hiển thị Banner kết quả cực đẹp ở giữa màn hình
        ShowEndGameBanner("👑 NGƯỜI WIN!", "ĐÃ SỐNG SÓT THÀNH CÔNG QUA 3 PHÚT TRƯỚC MA BÍ NGÔ!", winColor, new Color(0.12f, 0.95f, 0.45f, 0.95f));
    }

    /// <summary>
    /// MA CHIẾN THẮNG:
    /// - Hiển thị "Ma win" trên màn hình.
    /// - Bế Ma Bí Ngô về giữa map (Central Plaza).
    /// </summary>
    public void TriggerGhostVictory()
    {
        isFinished = true;
        isRunning = false;
        currentPhase = MatchPhase.Finished;

        if (timerDigitsText != null)
        {
            timerDigitsText.text = "00:00";
            timerDigitsText.color = ghostWinColor;
        }

        if (timerLabelText != null)
        {
            timerLabelText.text = "💀 MA CHIẾN THẮNG!";
            timerLabelText.color = ghostWinColor;
        }

        if (timerProgressFill != null)
        {
            timerProgressFill.anchorMax = new Vector2(0f, 1f);
        }

        Debug.Log("<color=#FF4757>====================================================\n💀 [KẾT QUẢ] KHÔNG CÒN NGƯỜI SỐNG SÓT: MA WIN (GHOST WIN)!\nBế Ma Bí Ngô về giữa map (Central Plaza)!\n====================================================</color>");

        // 1. Bế Ma Bí Ngô về giữa map
        Vector3 center = ResolveMapCenter();
        PumpkinGhostAI[] allGhosts = Object.FindObjectsByType<PumpkinGhostAI>();
        for (int i = 0; i < allGhosts.Length; i++)
        {
            if (allGhosts[i] != null)
            {
                Vector3 targetPos = center + new Vector3(0f, 0f, 2.5f);
                SafeTeleport(allGhosts[i].gameObject, targetPos, Quaternion.Euler(0f, 180f, 0f));
                allGhosts[i].PlayAnimState("Attack", 0.1f);
            }
        }

        // 2. Hiển thị Banner kết quả Ma Thắng
        ShowEndGameBanner("💀 MA WIN!", "TẤT CẢ NGƯỜI CHƠI ĐÃ BỊ HẠ GỤC!", ghostWinColor, new Color(1.0f, 0.35f, 0.15f, 0.95f));
    }

    /// <summary>
    /// Đếm số người chơi con người đang còn sống
    /// </summary>
    public int GetAliveHumanPlayerCount()
    {
        PlayerHealth[] allHealth = Object.FindObjectsByType<PlayerHealth>();
        int alive = 0;
        for (int i = 0; i < allHealth.Length; i++)
        {
            if (allHealth[i] != null && !allHealth[i].isDead && allHealth[i].gameObject.activeInHierarchy)
            {
                alive++;
            }
        }
        return alive;
    }

    /// <summary>
    /// Tính toán điểm giữa map (Central Plaza) đảm bảo sát mặt sàn paver (Y ~ 0.45m)
    /// </summary>
    public Vector3 ResolveMapCenter()
    {
        Vector3 testPos = new Vector3(0f, 15f, 0f);
        if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.15f;
        }
        return mapCenterPosition;
    }

    /// <summary>
    /// Dịch chuyển (bế) đối tượng an toàn, tương thích CharacterController
    /// </summary>
    public static void SafeTeleport(GameObject targetGo, Vector3 destination, Quaternion rotation)
    {
        if (targetGo == null) return;
        CharacterController cc = targetGo.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        targetGo.transform.position = destination;
        targetGo.transform.rotation = rotation;

        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Hiển thị Banner thông báo nổi bật ở giữa phía trên màn hình trong thời gian ngắn
    /// </summary>
    public void ShowFloatingBanner(string message, Color textColor, float duration = 4.0f)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (floatingNoticeBannerObj == null)
        {
            floatingNoticeBannerObj = new GameObject("SurvivalFloatingNoticeBanner");
            floatingNoticeBannerObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = floatingNoticeBannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 130f);
            rt.sizeDelta = new Vector2(760f, 85f);

            Image bg = floatingNoticeBannerObj.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);
            bg.raycastTarget = false;

            Outline ol = floatingNoticeBannerObj.AddComponent<Outline>();
            ol.effectColor = new Color(0.2f, 0.7f, 1f, 0.8f);
            ol.effectDistance = new Vector2(2f, -2f);

            GameObject textGo = new GameObject("NoticeText");
            textGo.transform.SetParent(floatingNoticeBannerObj.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(15f, 5f);
            textRt.offsetMax = new Vector2(-15f, -5f);

            floatingNoticeText = textGo.AddComponent<Text>();
            floatingNoticeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            floatingNoticeText.fontSize = 20;
            floatingNoticeText.fontStyle = FontStyle.Bold;
            floatingNoticeText.alignment = TextAnchor.MiddleCenter;
            floatingNoticeText.supportRichText = true;
        }

        if (floatingNoticeText != null)
        {
            floatingNoticeText.text = message;
            floatingNoticeText.color = textColor;
        }

        floatingNoticeBannerObj.SetActive(true);
        floatingNoticeTimer = duration;
    }

    /// <summary>
    /// Hiển thị bảng thông báo chiến thắng hoành tráng ở giữa màn hình
    /// </summary>
    private void ShowEndGameBanner(string title, string subtitle, Color titleColor, Color outlineColor)
    {
        if (endResultBannerObj != null)
        {
            Destroy(endResultBannerObj);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        endResultBannerObj = new GameObject("EndGameResultBanner");
        endResultBannerObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = endResultBannerObj.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, 40f);
        panelRt.sizeDelta = new Vector2(720f, 220f);

        Image bgImage = endResultBannerObj.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.05f, 0.09f, 0.94f);
        bgImage.raycastTarget = false;

        Outline outline = endResultBannerObj.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);

        // 1. Text Tiêu đề (NGƯỜI WIN / MA WIN)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(endResultBannerObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(15f, 0f);
        titleRt.offsetMax = new Vector2(-15f, -15f);

        Text titleTxt = titleObj.AddComponent<Text>();
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.text = title;
        titleTxt.fontSize = 46;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = titleColor;

        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = Color.black;
        titleOutline.effectDistance = new Vector2(2f, -2f);

        // 2. Text Phụ đề giải thích
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(endResultBannerObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0.20f);
        subRt.anchorMax = new Vector2(1f, 0.52f);
        subRt.offsetMin = new Vector2(20f, 0f);
        subRt.offsetMax = new Vector2(-20f, 0f);

        Text subTxt = subObj.AddComponent<Text>();
        subTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subTxt.text = subtitle;
        subTxt.fontSize = 20;
        subTxt.fontStyle = FontStyle.Bold;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.color = new Color(0.88f, 0.92f, 0.98f, 0.95f);

        // 3. Text Hướng dẫn chơi lại & Nút Click Chơi Lại
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(endResultBannerObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0.22f);
        hintRt.offsetMin = new Vector2(10f, 10f);
        hintRt.offsetMax = new Vector2(-10f, 0f);

        Text hintTxt = hintObj.AddComponent<Text>();
        hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintTxt.text = "👉 Nhấn phím [ P ] hoặc Click vào đây để Chơi Lại";
        hintTxt.fontSize = 15;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(0.95f, 0.85f, 0.35f, 0.95f);

        Button clickBtn = hintObj.AddComponent<Button>();
        clickBtn.onClick.AddListener(RestartMatch);
    }

    /// <summary>
    /// Đọc phím P an toàn tương thích 100% cả New Input System và Legacy Input Manager
    /// </summary>
    private bool ReadRestartInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.pKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKeyDown(KeyCode.P)) return true;
        }
        catch {}
#endif
        return false;
    }

    /// <summary>
    /// Tải lại trận đấu
    /// </summary>
    public void RestartMatch()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void AutoBindReferencesIfNeeded()
    {
        if (timerDigitsText == null)
        {
            GameObject go = GameObject.Find("TimerDigitsText");
            if (go != null) timerDigitsText = go.GetComponent<Text>();
        }

        if (timerLabelText == null)
        {
            GameObject go = GameObject.Find("TimerLabelText");
            if (go != null) timerLabelText = go.GetComponent<Text>();
        }

        if (timerProgressFill == null)
        {
            GameObject go = GameObject.Find("TimerProgressFill");
            if (go != null) timerProgressFill = go.GetComponent<RectTransform>();
        }
    }
}
