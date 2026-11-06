using UnityEngine;
using UnityEngine.UI;

public class SurvivalTimerUI : MonoBehaviour
{
    public static SurvivalTimerUI Instance { get; private set; }

    [Header("Cấu Hình Bộ Đếm Thời Gian (3 Phút = 180 Giây)")]
    [Tooltip("Tổng thời gian đếm ngược tính bằng giây (180s = 3 phút)")]
    [Range(10f, 900f)] public float durationSeconds = 180f;

    [Tooltip("Tự động đếm ngược ngay khi vào game")]
    public bool autoStart = true;

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

    private readonly Color normalColor  = new Color(0.95f, 0.98f, 1.00f, 1f);
    private readonly Color warningColor = new Color(1.00f, 0.78f, 0.22f, 1f);
    private readonly Color urgentColor  = new Color(1.00f, 0.25f, 0.28f, 1f);
    private readonly Color winColor     = new Color(0.20f, 0.92f, 0.50f, 1f);

    private void Awake()
    {
        Instance = this;
        remainingTime = durationSeconds;
    }

    private void Start()
    {
        AutoBindReferencesIfNeeded();
        ResetTimer(autoStart);
    }

    private void Update()
    {
        if (!isRunning || isFinished) return;

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

    private void OnTimerCompleted()
    {
        if (timerDigitsText != null)
        {
            timerDigitsText.text = "00:00";
            timerDigitsText.color = winColor;
        }

        if (timerLabelText != null)
        {
            timerLabelText.text = "🏆 ĐÃ SỐNG SÓT QUA 3 PHÚT!";
            timerLabelText.color = winColor;
        }

        if (timerProgressFill != null)
        {
            timerProgressFill.anchorMax = new Vector2(0f, 1f);
        }

        Debug.Log("<color=#2ECC71>[SurvivalTimer] Chúc mừng! Bạn đã sống sót thành công hết 3 phút!</color>");
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

