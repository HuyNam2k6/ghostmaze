using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Chỉ Số Máu Người Chơi (Player Health)")]
    [Tooltip("Máu tối đa của người chơi")]
    [Range(50f, 500f)] public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Thời Gian Bất Tử Ngắn Sau Khi Trúng Đòn (Invulnerability)")]
    [Tooltip("Thời gian miễn sát thương liên tiếp (giây)")]
    [Range(0.2f, 2.5f)] public float invulnerableDuration = 0.8f;
    private float lastDamageTime = -10f;

    [Header("Giao Diện Thanh Máu HUD (Player Health UI)")]
    [Tooltip("RectTransform của thanh máu bên trong (co giãn theo tỷ lệ máu)")]
    public RectTransform healthFillRect;

    [Tooltip("Image của thanh máu để đổi màu Xanh -> Vàng -> Đỏ")]
    public Image healthFillImage;

    [Tooltip("Text hiển thị con số máu (VD: 100 / 100 HP)")]
    public Text healthValueText;

    [Tooltip("Hiệu ứng chớp đỏ màn hình khi bị ma cào trúng")]
    public Image damageVignette;

    [Header("Hiệu Ứng 3 Dấu Móng Vuốt Cào (Claw Scratch Effect)")]
    [Tooltip("Image hiển thị 3 vệt móng vuốt cào ở góc màn hình khi bị ma cào")]
    public Image clawScratchImage;

    [Tooltip("Sprite hình 3 vệt móng vuốt cào đẫm máu")]
    public Sprite clawScratchSprite;

    [Tooltip("Thời gian mờ dần của vết cào (giây)")]
    [Range(0.4f, 2.5f)] public float clawScratchDuration = 0.9f;

    [Header("Âm Thanh Phản Hồi")]
    public AudioSource audioSource;
    public AudioClip hurtSound;

    private CharacterController characterController;
    private float displayedHealthRatio = 1f;
    private float vignetteAlpha = 0f;
    private float scratchAlpha = 0f;

    private readonly Color healthyColor = new Color(0.18f, 0.85f, 0.45f, 1f); // Xanh lục
    private readonly Color warningColor = new Color(0.95f, 0.75f, 0.18f, 1f); // Vàng cam
    private readonly Color dangerColor  = new Color(0.92f, 0.18f, 0.22f, 1f); // Đỏ máu

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;
        displayedHealthRatio = 1f;
        characterController = GetComponent<CharacterController>();
        gameObject.tag = "Player";
    }

    private void Start()
    {
        AutoBindHUDIfNeeded();
        RefreshHealthUIImmediate();
    }

    private void Update()
    {
        // Nội suy thanh máu mượt mà
        float targetRatio = (maxHealth > 0f) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        if (!Mathf.Approximately(displayedHealthRatio, targetRatio))
        {
            displayedHealthRatio = Mathf.MoveTowards(displayedHealthRatio, targetRatio, Time.deltaTime * 3.5f);
            UpdateFillBarVisual(displayedHealthRatio);
        }

        // Làm mờ dần hiệu ứng chớp đỏ khi bị thương
        if (damageVignette != null && vignetteAlpha > 0.001f)
        {
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.deltaTime * 2.2f);
            Color c = damageVignette.color;
            c.a = vignetteAlpha;
            damageVignette.color = c;
        }

        // Làm mờ dần hiệu ứng 3 vết cào móng vuốt ở góc màn hình
        if (clawScratchImage != null && scratchAlpha > 0.001f)
        {
            scratchAlpha = Mathf.MoveTowards(scratchAlpha, 0f, Time.deltaTime / Mathf.Max(0.1f, clawScratchDuration));
            Color sc = clawScratchImage.color;
            sc.a = scratchAlpha;
            clawScratchImage.color = sc;
            clawScratchImage.transform.localScale = Vector3.MoveTowards(clawScratchImage.transform.localScale, Vector3.one, Time.deltaTime * 0.9f);
            if (scratchAlpha <= 0.001f && clawScratchImage.gameObject.activeSelf)
            {
                clawScratchImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Nhận sát thương khi bị GhostKid tấn công hoặc nhảy vồ trúng
    /// </summary>
    public void TakeDamage(float amount, Vector3 attackerPos)
    {
        ThirdPersonPlayerController playerCtrl = GetComponent<ThirdPersonPlayerController>();
        if (playerCtrl == null) playerCtrl = ThirdPersonPlayerController.Instance;

        // Nếu đang trong trạng thái Đỡ Đòn (Block): Triệt tiêu sát thương và giữ chân con ma trong 3 giây
        if (playerCtrl != null && playerCtrl.isBlocking)
        {
            playerCtrl.OnBlockSuccessful(attackerPos);
            return;
        }

        if (Time.time < lastDamageTime + invulnerableDuration) return;
        lastDamageTime = Time.time;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        vignetteAlpha = 0.55f;
        scratchAlpha = 1.0f;

        if (clawScratchImage != null)
        {
            clawScratchImage.gameObject.SetActive(true);
            Color sc = clawScratchImage.color;
            sc.a = 1.0f;
            clawScratchImage.color = sc;
            clawScratchImage.transform.localScale = Vector3.one * 1.15f; // Giật nảy nhẹ khi trúng đòn cào
        }

        if (playerCtrl != null && playerCtrl.playerAnimator != null && !string.IsNullOrEmpty(playerCtrl.takeHitTriggerName))
        {
            playerCtrl.playerAnimator.SetTrigger(playerCtrl.takeHitTriggerName);
        }

        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = vignetteAlpha;
            damageVignette.color = c;
        }

        if (audioSource != null && hurtSound != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        // Đẩy lùi nhẹ nhân vật khi trúng đòn
        if (characterController != null && characterController.enabled && gameObject.activeInHierarchy)
        {
            Vector3 knockDir = (transform.position - attackerPos);
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude > 0.001f)
            {
                knockDir = knockDir.normalized;
                knockDir.y = 0.1f;
                characterController.Move(knockDir * 1.1f);
            }
        }

        UpdateHealthText();
        Debug.Log($"<color=#FF4757>[PlayerHealth] Bị ma tấn công (-{amount:F0} HP)! Máu còn: {currentHealth:F0}/{maxHealth:F0}</color>");

        if (currentHealth <= 0f)
        {
            if (playerCtrl != null && playerCtrl.playerAnimator != null && !string.IsNullOrEmpty(playerCtrl.deathTriggerName))
            {
                playerCtrl.playerAnimator.SetTrigger(playerCtrl.deathTriggerName);
            }
            HandlePlayerDown();
        }
    }

    public void HealFull()
    {
        currentHealth = maxHealth;
        RefreshHealthUIImmediate();
    }

    private void HandlePlayerDown()
    {
        Debug.Log("<color=#FF4757>[PlayerHealth] Bạn đã hết máu! Hồi sinh tại vị trí xuất phát và làm mới máu...</color>");
        currentHealth = maxHealth;
        displayedHealthRatio = 1f;
        RefreshHealthUIImmediate();

        ThirdPersonPlayerController playerCtrl = GetComponent<ThirdPersonPlayerController>();
        if (playerCtrl != null && characterController != null)
        {
            characterController.enabled = false;
            transform.position = playerCtrl.respawnPoint;
            characterController.enabled = true;
            playerCtrl.ValidateAndRepairController();
        }
    }

    public void RefreshHealthUIImmediate()
    {
        displayedHealthRatio = (maxHealth > 0f) ? Mathf.Clamp01(currentHealth / maxHealth) : 1f;
        UpdateFillBarVisual(displayedHealthRatio);
        UpdateHealthText();
    }

    private void UpdateFillBarVisual(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (healthFillRect != null)
        {
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = new Vector2(ratio, 1f);
            healthFillRect.offsetMin = Vector2.zero;
            healthFillRect.offsetMax = Vector2.zero;
        }

        if (healthFillImage != null)
        {
            if (ratio > 0.55f)
            {
                healthFillImage.color = healthyColor;
            }
            else if (ratio > 0.25f)
            {
                healthFillImage.color = warningColor;
            }
            else
            {
                healthFillImage.color = dangerColor;
            }
        }
    }

    private void UpdateHealthText()
    {
        if (healthValueText != null)
        {
            int cur = Mathf.CeilToInt(currentHealth);
            int max = Mathf.RoundToInt(maxHealth);
            healthValueText.text = $"{cur} / {max} HP";
        }
    }

    private void AutoBindHUDIfNeeded()
    {
        if (healthFillRect == null)
        {
            GameObject fillGo = GameObject.Find("PlayerHP_Fill");
            if (fillGo != null)
            {
                healthFillRect = fillGo.GetComponent<RectTransform>();
                healthFillImage = fillGo.GetComponent<Image>();
            }
        }

        if (healthValueText == null)
        {
            GameObject txtGo = GameObject.Find("PlayerHP_ValueText");
            if (txtGo != null)
            {
                healthValueText = txtGo.GetComponent<Text>();
            }
        }

        if (damageVignette == null)
        {
            GameObject vigGo = GameObject.Find("PlayerDamageVignette");
            if (vigGo != null)
            {
                damageVignette = vigGo.GetComponent<Image>();
            }
        }

        if (clawScratchImage == null)
        {
            GameObject scratchGo = GameObject.Find("PlayerClawScratch");
            if (scratchGo != null)
            {
                clawScratchImage = scratchGo.GetComponent<Image>();
            }
            else
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    GameObject newScratchGo = new GameObject("PlayerClawScratch");
                    newScratchGo.transform.SetParent(canvas.transform, false);
                    RectTransform rt = newScratchGo.AddComponent<RectTransform>();

                    // Cố định ở góc trên bên phải màn hình (Top-Right Corner)
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-15f, -15f);
                    rt.sizeDelta = new Vector2(380f, 380f);

                    Image img = newScratchGo.AddComponent<Image>();
                    img.raycastTarget = false;

                    if (clawScratchSprite != null)
                    {
                        img.sprite = clawScratchSprite;
                    }
                    else
                    {
#if UNITY_EDITOR
                        img.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/ClawScratch.png");
#endif
                    }

                    img.color = new Color(1f, 1f, 1f, 0f);
                    newScratchGo.SetActive(false);
                    clawScratchImage = img;
                }
            }
        }
    }
}
