using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Chỉ Số Máu Người Chơi (Player Health)")]
    [Tooltip("Máu tối đa của người chơi")]
    [Range(50f, 500f)] public float maxHealth = 100f;
    public float currentHealth = 100f;
    public bool isDead { get; private set; } = false;

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

    // Trạng thái Camera Quan Sát Đồng Đội khi đã tử trận
    public bool IsSpectatingAllies { get; private set; } = false;
    private readonly List<PhotonMultiplayerChatVoice.AlliedSpectateTarget> spectateTargets = new List<PhotonMultiplayerChatVoice.AlliedSpectateTarget>();
    private int spectatedActorNumber = -1;
    private int spectateIndex = 0;
    private GameObject spectateHudObj;
    private Text spectateTitleText;
    private Text spectateHintText;
    private Button spectatePrevBtn;
    private Button spectateNextBtn;

    private void Awake()
    {
        Instance = this;
        isDead = false;
        currentHealth = maxHealth;
        displayedHealthRatio = 1f;
        characterController = GetComponent<CharacterController>();
        gameObject.tag = "Player";
    }

    private void OnDestroy()
    {
        if (spectateHudObj != null)
        {
            Destroy(spectateHudObj);
            spectateHudObj = null;
        }
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        AutoBindHUDIfNeeded();
        RefreshHealthUIImmediate();
    }

    private void Update()
    {
        // Nội suy thanh máu mượt mà
        float targetRatio = isDead ? 0f : ((maxHealth > 0f) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f);
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

        // Cho phép đổi camera quan sát đồng đội bằng phím [Q]/[Mũi tên trái] và [E]/[Mũi tên phải]
        if (isDead && IsSpectatingAllies && !ThirdPersonCamera.isUIPanelOpen
            && (SurvivalTimerUI.Instance == null || !SurvivalTimerUI.Instance.IsFinished))
        {
            HandleSpectateKeyboardSwitch();
        }
    }

    private void HandleSpectateKeyboardSwitch()
    {
        bool prevPressed = false;
        bool nextPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) prevPressed = true;
            if (kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) nextPressed = true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) prevPressed = true;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) nextPressed = true;
#endif

        if (prevPressed) SwitchSpectateTarget(-1);
        else if (nextPressed) SwitchSpectateTarget(1);
    }

    /// <summary>
    /// Nhận sát thương khi bị GhostKid tấn công hoặc nhảy vồ trúng
    /// </summary>
    public void TakeDamage(float amount, Vector3 attackerPos)
    {
        if (isDead) return;

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

        if (playerCtrl != null && playerCtrl.playerAnimator != null)
        {
            bool isMoving = playerCtrl.ReadMoveInput().sqrMagnitude > 0.01f;
            if (isMoving)
            {
                // NẾU ĐANG DI CHUYỂN: Chuyển ngay/giữ animation di chuyển (Run), KHÔNG đợi hết animation TakeHit mới chịu di chuyển!
                playerCtrl.playerAnimator.CrossFadeInFixedTime("Run", 0.08f);
            }
            else if (!string.IsNullOrEmpty(playerCtrl.takeHitTriggerName))
            {
                // NẾU ĐỨNG YÊN: Mới phát giật mình TakeHit
                playerCtrl.playerAnimator.SetTrigger(playerCtrl.takeHitTriggerName);
            }
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
            isDead = true;
            Debug.Log("<color=#FF4757>====================================================\n[PlayerHealth] Người chơi đã tử trận! Không hồi sinh nữa.\nChuyển Camera và quyền điều khiển sang Ma Bí Ngô!\n====================================================</color>");

            if (playerCtrl != null && playerCtrl.playerAnimator != null && !string.IsNullOrEmpty(playerCtrl.deathTriggerName))
            {
                playerCtrl.playerAnimator.SetTrigger(playerCtrl.deathTriggerName);
            }

            // Khóa hoàn toàn điều khiển người
            if (playerCtrl != null)
            {
                playerCtrl.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // Thông báo chết lên mạng Photon trước khi khởi động camera quan sát đồng đội
            if (PhotonMultiplayerChatVoice.Instance != null)
            {
                PhotonMultiplayerChatVoice.Instance.NotifyLocalHumanDied();
            }

            // Nếu đang trong phòng Multiplayer: chuyển sang Camera Quan Sát Đồng Đội cùng phe (không cướp Ma Bí Ngô)
            if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom)
            {
                EnterAlliedSpectateMode();
            }
            else
            {
                // Chế độ Offline đơn thuần: giữ fallback chuyển sang Ma Bí Ngô
                TransferControlToPumpkinGhost();
            }

            // Thông báo cho SurvivalTimerUI kiểm tra điều kiện kết thúc trận đấu (Nếu hết người -> Ma Win!)
            if (SurvivalTimerUI.Instance != null)
            {
                SurvivalTimerUI.Instance.OnPlayerDied(this);
            }
        }
    }

    /// <summary>
    /// Äáº·t láº¡i mÃ¡u vÃ  tráº¡ng thÃ¡i ngÆ°á»i chÆ¡i khi báº¯t Ä‘áº§u hiá»‡p/tráº­n Ä‘áº¥u má»›i
    /// </summary>
    public void ResetHealth()
    {
        isDead = false;
        IsSpectatingAllies = false;
        spectatedActorNumber = -1;
        spectateIndex = 0;
        spectateTargets.Clear();
        if (spectateHudObj != null) spectateHudObj.SetActive(false);

        currentHealth = maxHealth;
        displayedHealthRatio = 1f;
        vignetteAlpha = 0f;
        scratchAlpha = 0f;

        if (damageVignette != null)
        {
            Color vc = damageVignette.color;
            vc.a = 0f;
            damageVignette.color = vc;
        }

        if (clawScratchImage != null)
        {
            clawScratchImage.gameObject.SetActive(false);
        }

        ThirdPersonPlayerController playerCtrl = GetComponent<ThirdPersonPlayerController>();
        if (playerCtrl == null) playerCtrl = ThirdPersonPlayerController.Instance;
        if (playerCtrl != null)
        {
            playerCtrl.enabled = true;
            if (playerCtrl.playerAnimator != null)
            {
                playerCtrl.playerAnimator.speed = 1.0f;
                playerCtrl.playerAnimator.CrossFadeInFixedTime("Idle", 0.15f);
            }
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Chuyá»ƒn láº¡i Camera vá» ngÆ°á»i chÆ¡i náº¿u Ä‘ang bÃ¡m theo Ma
        if (ThirdPersonCamera.Instance != null)
        {
            ThirdPersonCamera.Instance.SetTarget(transform, new Vector3(0f, 1.45f, 0f));
        }

        GameObject takeoverBanner = GameObject.Find("GhostTakeoverBanner");
        if (takeoverBanner != null)
        {
            Destroy(takeoverBanner);
        }

        UpdateHealthText();
    }

    private void EnterAlliedSpectateMode()
    {
        IsSpectatingAllies = true;
        spectatedActorNumber = -1;
        spectateIndex = 0;

        if (healthValueText != null)
        {
            healthValueText.text = "💀 ĐÃ TỬ TRẬN";
        }

        RefreshSpectateTargetListAndUI();
    }

    /// <summary>
    /// Gọi khi có đồng đội mới xuất hiện, bị hạ, hoặc rời phòng để tự động chuyển cam sang đồng đội khác còn sống.
    /// </summary>
    public void OnSpectateRosterChanged()
    {
        if (!isDead) return;
        if (PhotonMultiplayerChatVoice.Instance == null || !PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom) return;
        RefreshSpectateTargetListAndUI();
    }

    public void EnsureValidSpectateTarget()
    {
        if (!isDead) return;
        if (PhotonMultiplayerChatVoice.Instance == null || !PhotonMultiplayerChatVoice.Instance.IsInPhotonRoom) return;
        RefreshSpectateTargetListAndUI();
    }

    public void StopSpectateUIOnMatchFinished()
    {
        IsSpectatingAllies = false;
        if (spectateHudObj != null)
        {
            spectateHudObj.SetActive(false);
        }
    }

    public void SwitchSpectateTarget(int step)
    {
        if (!isDead || !IsSpectatingAllies) return;
        if (PhotonMultiplayerChatVoice.Instance == null) return;

        PhotonMultiplayerChatVoice.Instance.GetAlliedAliveSpectateTargets(spectateTargets);
        if (spectateTargets.Count == 0)
        {
            UpdateSpectateHUDDisplay();
            return;
        }

        int curIdx = spectateTargets.FindIndex(t => t.actorNumber == spectatedActorNumber);
        if (curIdx < 0) curIdx = Mathf.Clamp(spectateIndex, 0, spectateTargets.Count - 1);

        spectateIndex = ((curIdx + step) % spectateTargets.Count + spectateTargets.Count) % spectateTargets.Count;
        AttachCameraToSpectateIndex(spectateIndex);
        UpdateSpectateHUDDisplay();
    }

    private void RefreshSpectateTargetListAndUI()
    {
        if (PhotonMultiplayerChatVoice.Instance == null) return;
        PhotonMultiplayerChatVoice.Instance.GetAlliedAliveSpectateTargets(spectateTargets);

        if (spectateTargets.Count == 0)
        {
            spectatedActorNumber = -1;
            spectateIndex = 0;
            UpdateSpectateHUDDisplay();
            return;
        }

        int existingIdx = spectatedActorNumber > 0
            ? spectateTargets.FindIndex(t => t.actorNumber == spectatedActorNumber)
            : -1;

        spectateIndex = (existingIdx >= 0) ? existingIdx : (Mathf.Clamp(spectateIndex, 0, spectateTargets.Count - 1));
        AttachCameraToSpectateIndex(spectateIndex);
        UpdateSpectateHUDDisplay();
    }

    private void AttachCameraToSpectateIndex(int index)
    {
        if (index < 0 || index >= spectateTargets.Count) return;

        var chosen = spectateTargets[index];
        spectatedActorNumber = chosen.actorNumber;

        ThirdPersonCamera cam = ThirdPersonCamera.Instance != null
            ? ThirdPersonCamera.Instance
            : (Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null);

        if (cam != null && chosen.targetTransform != null)
        {
            cam.SetSpectateTarget(chosen.targetTransform, new Vector3(0f, 1.45f, 0f));
        }
    }

    private void UpdateSpectateHUDDisplay()
    {
        if (SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.IsFinished)
        {
            if (spectateHudObj != null) spectateHudObj.SetActive(false);
            return;
        }

        EnsureSpectateHUDBuilt();
        if (spectateHudObj == null) return;
        spectateHudObj.SetActive(true);

        int count = spectateTargets.Count;
        bool canSwitch = count > 1;
        if (spectatePrevBtn != null) spectatePrevBtn.interactable = canSwitch;
        if (spectateNextBtn != null) spectateNextBtn.interactable = canSwitch;

        if (spectateTitleText != null)
        {
            if (count > 0 && spectateIndex >= 0 && spectateIndex < count)
            {
                var cur = spectateTargets[spectateIndex];
                spectateTitleText.text = $"👁 ĐANG XEM ĐỒNG ĐỘI: {cur.nickName} ({spectateIndex + 1}/{count})";
                spectateTitleText.color = new Color(0.55f, 0.96f, 1f, 1f);
            }
            else
            {
                spectateTitleText.text = "💀 ĐÃ TỬ TRẬN — KHÔNG CÒN ĐỒNG ĐỘI CÙNG PHE";
                spectateTitleText.color = new Color(1f, 0.55f, 0.45f, 1f);
            }
        }

        if (spectateHintText != null)
        {
            spectateHintText.text = count > 1
                ? (Application.isMobilePlatform
                    ? "Bấm nút 2 bên để đổi đồng đội  •  Chụm 2 ngón thu phóng rộng (1.5x)"
                    : "[Q] / [E] hoặc bấm nút để đổi đồng đội  •  Lăn chuột chỉnh góc rộng (1.5x)")
                : (Application.isMobilePlatform
                    ? "Vuốt để xoay góc nhìn  •  Chụm 2 ngón thu phóng rộng (1.5x)"
                    : "Chờ đồng đội chiến đấu  •  Lăn chuột chỉnh góc nhìn rộng (1.5x)");
        }
    }

    private void EnsureSpectateHUDBuilt()
    {
        if (spectateHudObj != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        spectateHudObj = new GameObject("AlliedSpectateHUD");
        spectateHudObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = spectateHudObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 22f);
        rt.sizeDelta = new Vector2(600f, 88f);

        Image bg = spectateHudObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.13f, 0.90f);
        bg.raycastTarget = true;

        Outline outline = spectateHudObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.85f, 1.0f, 0.88f);
        outline.effectDistance = new Vector2(2f, -2f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        spectatePrevBtn = CreateSpectateNavButton(
            spectateHudObj.transform, "Btn_SpectatePrev", "◀ TRƯỚC",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(12f, 0f), font, () => SwitchSpectateTarget(-1));

        spectateNextBtn = CreateSpectateNavButton(
            spectateHudObj.transform, "Btn_SpectateNext", "SAU ▶",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f), font, () => SwitchSpectateTarget(1));

        GameObject centerInfoGo = new GameObject("SpectateInfo");
        centerInfoGo.transform.SetParent(spectateHudObj.transform, false);
        RectTransform infoRt = centerInfoGo.AddComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0f, 0f);
        infoRt.anchorMax = new Vector2(1f, 1f);
        infoRt.offsetMin = new Vector2(126f, 8f);
        infoRt.offsetMax = new Vector2(-126f, -8f);

        GameObject titleGo = new GameObject("SpectateTitle");
        titleGo.transform.SetParent(centerInfoGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.45f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        spectateTitleText = titleGo.AddComponent<Text>();
        spectateTitleText.font = font;
        spectateTitleText.fontSize = 18;
        spectateTitleText.fontStyle = FontStyle.Bold;
        spectateTitleText.alignment = TextAnchor.MiddleCenter;
        spectateTitleText.raycastTarget = false;

        GameObject hintGo = new GameObject("SpectateHint");
        hintGo.transform.SetParent(centerInfoGo.transform, false);
        RectTransform hintRt = hintGo.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0.45f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;

        spectateHintText = hintGo.AddComponent<Text>();
        spectateHintText.font = font;
        spectateHintText.fontSize = 12;
        spectateHintText.alignment = TextAnchor.MiddleCenter;
        spectateHintText.color = new Color(0.82f, 0.90f, 0.96f, 0.85f);
        spectateHintText.raycastTarget = false;
    }

    private Button CreateSpectateNavButton(
        Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(104f, 52f);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0.14f, 0.36f, 0.56f, 0.96f);

        Button btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtGo = new GameObject("Label");
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(4f, 2f);
        txtRt.offsetMax = new Vector2(-4f, -2f);

        Text txt = txtGo.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 14;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;

        return btn;
    }

    /// <summary>
    /// Chuyển giao quyền điều khiển và góc quay Camera sang Ma Bí Ngô (PumpkinGhostAI)
    /// </summary>
    private void TransferControlToPumpkinGhost()
    {
        PumpkinGhostAI targetGhost = null;
        if (PumpkinGhostAI.ActiveGhosts != null && PumpkinGhostAI.ActiveGhosts.Count > 0)
        {
            targetGhost = PumpkinGhostAI.ActiveGhosts[0];
        }
        if (targetGhost == null)
        {
            targetGhost = FindAnyObjectByType<PumpkinGhostAI>();
        }

        if (targetGhost != null)
        {
            targetGhost.TakeOverAsPlayer();

            // Chuyển ánh sáng sang chế độ Bên Ma (Mắt ma soi rõ toàn map)
            if (CemeteryMapManager.Instance != null)
            {
                CemeteryMapManager.Instance.SwitchToGhostLighting(1.5f);
            }

            // Chuyển Camera sang bám theo Ma Bí Ngô
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.SetTarget(targetGhost.transform, new Vector3(0f, 1.25f, 0f));
            }
            else if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.SetTarget(targetGhost.transform, new Vector3(0f, 1.25f, 0f));
                }
            }

            ShowGhostTakeoverBanner();
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] Không tìm thấy đối tượng PumpkinGhostAI nào trong Scene để chuyển giao!");
        }
    }

    private void ShowGhostTakeoverBanner()
    {
        if (healthValueText != null)
        {
            healthValueText.text = "👻 MA BÍ NGÔ (BẤT TỬ)";
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            GameObject bannerGo = GameObject.Find("GhostTakeoverBanner");
            if (bannerGo == null)
            {
                bannerGo = new GameObject("GhostTakeoverBanner");
                bannerGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = bannerGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 75f);
                rt.sizeDelta = new Vector2(640f, 95f);

                Image bg = bannerGo.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.04f, 0.02f, 0.92f);
                bg.raycastTarget = false;

                Outline outline = bannerGo.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.48f, 0.08f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);

                GameObject textGo = new GameObject("BannerText");
                textGo.transform.SetParent(bannerGo.transform, false);
                RectTransform textRt = textGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(10f, 5f);
                textRt.offsetMax = new Vector2(-10f, -5f);

                Text bannerText = textGo.AddComponent<Text>();
                bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (bannerText.font == null) bannerText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                bannerText.fontSize = 18;
                bannerText.alignment = TextAnchor.MiddleCenter;
                bannerText.fontStyle = FontStyle.Bold;
                bannerText.color = new Color(1f, 0.85f, 0.35f, 1f);
                bannerText.text = "👻 BẠN ĐÃ TỬ TRẬN! LINH HỒN ĐÃ CHIẾM HỮU MA BÍ NGÔ!\n[WASD]: Di chuyển  |  [Chuột trái]: Cào xé  |  [Space]: Nhảy vồ  |  [Chuột phải / E]: Ẩn thân";
                bannerText.raycastTarget = false;

                StartCoroutine(RoutineDismissBanner(bannerGo, 7.0f));
            }
            else
            {
                bannerGo.SetActive(true);
                StartCoroutine(RoutineDismissBanner(bannerGo, 7.0f));
            }
        }
    }

    private System.Collections.IEnumerator RoutineDismissBanner(GameObject bannerGo, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bannerGo != null)
        {
            bannerGo.SetActive(false);
        }
    }

    public void HealFull()
    {
        isDead = false;
        currentHealth = maxHealth;
        RefreshHealthUIImmediate();
    }

    private void HandlePlayerDown()
    {
        // Giữ lại làm hàm fallback nếu cần hồi sinh thủ công
        currentHealth = maxHealth;
        displayedHealthRatio = 1f;
        RefreshHealthUIImmediate();

        if (CemeteryMapManager.Instance != null)
        {
            CemeteryMapManager.Instance.SwitchToHumanDarkLighting(1.2f);
        }

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
                Canvas canvas = GameSettingsManager.Instance != null ? GameSettingsManager.Instance.GetComponent<Canvas>() : null;
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
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
                        if (img.sprite == null)
                        {
                            img.sprite = CreateFallbackClawScratchSprite();
                        }
                    }

                    img.color = new Color(1f, 1f, 1f, 0f);
                    newScratchGo.SetActive(false);
                    clawScratchImage = img;
                }
            }
        }
    }

    private static Sprite s_FallbackClawScratchSprite;

    private static Sprite CreateFallbackClawScratchSprite()
    {
        if (s_FallbackClawScratchSprite != null) return s_FallbackClawScratchSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

        Vector2[] starts = { new Vector2(18f, 108f), new Vector2(36f, 114f), new Vector2(56f, 104f) };
        Vector2[] ends   = { new Vector2(96f, 22f),  new Vector2(112f, 30f), new Vector2(114f, 48f) };

        for (int s = 0; s < 3; s++)
        {
            Vector2 a = starts[s];
            Vector2 b = ends[s];
            Vector2 ab = b - a;
            float lenSq = Mathf.Max(1f, ab.sqrMagnitude);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                    Vector2 proj = a + ab * t;
                    float dist = Vector2.Distance(p, proj);
                    float width = Mathf.Sin(t * Mathf.PI) * 5.2f + 1.2f;
                    if (dist <= width)
                    {
                        float alpha = Mathf.Clamp01(1f - dist / width) * Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 1.25f);
                        Color col = new Color(0.95f, 0.14f, 0.12f, alpha);
                        int idx = y * size + x;
                        if (col.a > pixels[idx].a) pixels[idx] = col;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        s_FallbackClawScratchSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return s_FallbackClawScratchSprite;
    }
}
