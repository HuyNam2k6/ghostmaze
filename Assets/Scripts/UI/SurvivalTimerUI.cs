using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý thời gian trận đấu (3 phút = 180s) và điều kiện thắng/thua:
/// - Sau 3 phút: Nếu còn người sống sót -> NGƯỜI WIN! Bế tất cả người về giữa map (Central Plaza).
/// - Trong hoặc sau 3 phút: Nếu không còn người sống sót -> MA WIN! Bế Ma Bí Ngô về giữa map.
/// </summary>
public class SurvivalTimerUI : MonoBehaviour
{
    public static SurvivalTimerUI Instance { get; private set; }

    [Header("Cấu Hình Bộ Đếm Thời Gian (3 Phút = 180 Giây)")]
    [Tooltip("Tổng thời gian đếm ngược tính bằng giây (180s = 3 phút)")]
    [Range(10f, 900f)] public float durationSeconds = 180f;

    [Tooltip("Tự động đếm ngược ngay khi vào game")]
    public bool autoStart = true;

    [Header("Vị Trí Giữa Map (Central Plaza)")]
    [Tooltip("Tọa độ giữa map để dịch chuyển (bế) người hoặc ma khi kết thúc trận đấu")]
    public Vector3 mapCenterPosition = new Vector3(0f, 0.45f, 0f);

    [Header("Tham Chiếu Giao Diện (HUD Elements)")]
    [Tooltip("Text hiển thị đồng hồ đếm ngược (VD: 03:00)")]
    public Text timerDigitsText;

    [Tooltip("Text tiêu đề phụ phía trên đồng hồ")]
    public Text timerLabelText;

    [Tooltip("Thanh tiến trình thời gian bên dưới đồng hồ")]
    public RectTransform timerProgressFill;

    // Trạng thái nội bộ
    private float remainingTime;
    private int lastDisplayedSecond = -1;
    private bool isRunning = false;
    private bool isFinished = false;
    public bool IsFinished => isFinished;

    private GameObject endResultBannerObj;

    private readonly Color normalColor  = new Color(0.95f, 0.98f, 1.00f, 1f);
    private readonly Color warningColor = new Color(1.00f, 0.78f, 0.22f, 1f);
    private readonly Color urgentColor  = new Color(1.00f, 0.25f, 0.28f, 1f);
    private readonly Color winColor     = new Color(0.20f, 0.92f, 0.50f, 1f);
    private readonly Color ghostWinColor = new Color(1.00f, 0.28f, 0.34f, 1f);

    private void Awake()
    {
        Instance = this;
        remainingTime = durationSeconds;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        AutoBindReferencesIfNeeded();
        ResetTimer(autoStart);
    }

    private void Update()
    {
        // Cho phép bấm phím P để chơi lại khi trận đấu đã kết thúc (tương thích cả New Input System và Legacy)
        if (isFinished)
        {
            if (ReadRestartInputDown())
            {
                RestartMatch();
            }
            return;
        }

        if (!isRunning) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isRunning = false;
            isFinished = true;
            UpdateTimerDisplay(0);
            OnTimerCompleted();
            return;
        }

        // Cập nhật thanh tiến trình thời gian mượt mà
        if (timerProgressFill != null && durationSeconds > 0f)
        {
            float ratio = Mathf.Clamp01(remainingTime / durationSeconds);
            timerProgressFill.anchorMin = Vector2.zero;
            timerProgressFill.anchorMax = new Vector2(ratio, 1f);
            timerProgressFill.offsetMin = Vector2.zero;
            timerProgressFill.offsetMax = Vector2.zero;
        }

        // Chỉ cập nhật chuỗi Text khi số giây nguyên thay đổi (Tối ưu 0 GC mỗi frame!)
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

    public void ResetTimer(bool startImmediately = true)
    {
        remainingTime = Mathf.Max(1f, durationSeconds);
        lastDisplayedSecond = -1;
        isFinished = false;
        isRunning = startImmediately;

        if (endResultBannerObj != null)
        {
            Destroy(endResultBannerObj);
        }

        if (timerLabelText != null)
        {
            timerLabelText.text = "⏱ THỜI GIAN SINH TỒN";
            timerLabelText.color = new Color(0.75f, 0.85f, 1.0f, 0.95f);
        }

        if (timerDigitsText != null)
        {
            timerDigitsText.color = normalColor;
        }

        if (timerProgressFill != null)
        {
            timerProgressFill.anchorMin = Vector2.zero;
            timerProgressFill.anchorMax = Vector2.one;
            timerProgressFill.offsetMin = Vector2.zero;
            timerProgressFill.offsetMax = Vector2.zero;
        }

        int initialSec = Mathf.CeilToInt(remainingTime);
        lastDisplayedSecond = initialSec;
        UpdateTimerDisplay(initialSec);
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
            PlayerHealth ph = allHealth[i];
            if (ph != null && !ph.isDead && ph.gameObject.activeInHierarchy)
            {
                float xOffset = (idx - (totalAlive - 1) * 0.5f) * 2.2f;
                Vector3 targetPos = center + new Vector3(xOffset, 0f, 2.5f);
                SafeTeleport(ph.gameObject, targetPos, Quaternion.Euler(0f, 180f, 0f));

                // Bật trạng thái bất tử sau khi thắng
                ThirdPersonPlayerController ctrl = ph.GetComponent<ThirdPersonPlayerController>();
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
