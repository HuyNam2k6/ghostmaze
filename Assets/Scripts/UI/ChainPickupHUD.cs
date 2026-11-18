using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// HUD tương tác nhặt vật phẩm & nút kỹ năng vật phẩm động:
/// 1. Khi Con Người đến gần Dây Xích hoặc Dấu Ấn trên sàn -> hiện nút Bàn Tay ("NHẶT [F]") để bấm nhặt.
/// 2. Sau khi nhặt xong -> tự động xuất hiện thêm 1 nút/joystick vật phẩm (Pentacle + Xích [E]) trên HUD.
/// </summary>
public class ChainPickupHUD : MonoBehaviour
{
    public static ChainPickupHUD Instance { get; private set; }
    public static bool HasNearbyPickup { get; private set; }
    private static int s_PickupConsumedFrame = -1;
    public static bool ShouldSuppressBlockF => HasNearbyPickup || s_PickupConsumedFrame == Time.frameCount;

    [Header("Cấu Hình HUD (Không Fix Cứng)")]
    [Tooltip("Phím tắt nhặt vật phẩm khi nút Bàn Tay đang hiện")]
    public KeyCode pickupKey = KeyCode.F;

    [Tooltip("Vị trí nút Bàn Tay nhặt vật phẩm (tính từ giữa-dưới màn hình)")]
    public Vector2 handButtonAnchoredPos = new Vector2(0f, 175f);

    [Tooltip("Kích thước nút Bàn Tay")]
    public float handButtonSize = 108f;

    [Tooltip("Vị trí nút Joystick Vật Phẩm (tính từ góc dưới-phải màn hình)")]
    public Vector2 itemActionAnchoredPos = new Vector2(-340f, 145f);

    [Tooltip("Kích thước nút Joystick Vật Phẩm")]
    public float itemActionButtonSize = 118f;

    private Canvas hudCanvas;
    private GameObject handPickupBtnObj;
    private RectTransform handRt;
    private Text handPickupLabel;
    private GameObject itemActionBtnObj;
    private RectTransform actRt;
    private Text itemActionBadgeText;

    private ChainPickupItem currentNearbyItem;
    private bool isActionHeld;
    private bool registeredWithTouchUI;
    private int lastCharges = -1;
    private int lastMaxCharges = -1;
    private KeyCode lastPickupKey = KeyCode.None;
    private GameLocalizationManager.Language lastLanguage = (GameLocalizationManager.Language)(-1);

    private static Sprite s_HandSprite;
    private static Sprite s_PentacleChainSprite;
    private static Sprite s_CircleBgSprite;

    public static void EnsureInScene()
    {
        if (Instance != null) return;

        ChainPickupHUD existing = Object.FindAnyObjectByType<ChainPickupHUD>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject go = new GameObject("ChainPickupHUD");
        Instance = go.AddComponent<ChainPickupHUD>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildHUD();
    }

    private void OnDestroy()
    {
        HasNearbyPickup = false;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        ApplyInspectorLayout();
    }

    private void ApplyInspectorLayout()
    {
        if (handRt != null)
        {
            handRt.anchoredPosition = handButtonAnchoredPos;
            handRt.sizeDelta = new Vector2(handButtonSize, handButtonSize);
        }
        if (actRt != null)
        {
            actRt.anchoredPosition = itemActionAnchoredPos;
            actRt.sizeDelta = new Vector2(itemActionButtonSize, itemActionButtonSize);
        }
    }

    private void Update()
    {
        TryRegisterWithTouchUI();

        ThirdPersonPlayerController player = ThirdPersonPlayerController.Instance;
        bool isEditingHUD = VirtualJoystickTouchUI.Instance != null && VirtualJoystickTouchUI.Instance.IsEditingHUDLayout;

        // Khi đang ở chế độ Chỉnh sửa bố cục HUD trong Cài Đặt:
        // Ép hiện cả nút Bàn Tay và nút Dấu Ấn & Xích để người chơi thấy, kéo thả và chỉnh kích cỡ!
        if (isEditingHUD)
        {
            HasNearbyPickup = false;
            if (handPickupBtnObj != null && !handPickupBtnObj.activeSelf) handPickupBtnObj.SetActive(true);
            UpdateHandPickupLabelIfNeeded();
            if (itemActionBtnObj != null && !itemActionBtnObj.activeSelf) itemActionBtnObj.SetActive(true);
            if (player != null)
            {
                UpdateItemBadgeTextIfNeeded(player.currentChainCharges, player.maxChainCharges);
            }
            ReleaseActionHold(player);
            return;
        }

        if (player == null || !ChainPickupItem.IsLocalHumanAlive(player) || ThirdPersonCamera.isUIPanelOpen)
        {
            HasNearbyPickup = false;
            if (handPickupBtnObj != null && handPickupBtnObj.activeSelf) handPickupBtnObj.SetActive(false);
            if (itemActionBtnObj != null && itemActionBtnObj.activeSelf) itemActionBtnObj.SetActive(false);
            ReleaseActionHold(player);
            return;
        }

        // 1. Kiểm tra vật phẩm ở gần để hiện nút Bàn Tay
        currentNearbyItem = ChainPickupItem.FindNearestForHuman(player);
        HasNearbyPickup = currentNearbyItem != null;

        if (handPickupBtnObj != null)
        {
            if (HasNearbyPickup)
            {
                if (!handPickupBtnObj.activeSelf) handPickupBtnObj.SetActive(true);
                UpdateHandPickupLabelIfNeeded();

                if (ReadPickupKeyDown(pickupKey))
                {
                    OnHandPickupClicked();
                }
            }
            else if (handPickupBtnObj.activeSelf)
            {
                handPickupBtnObj.SetActive(false);
            }
        }

        // 2. Hiện/ẩn nút Joystick Vật Phẩm (chỉ xuất hiện sau khi đã nhặt Dấu Ấn hoặc Dây Xích)
        bool shouldShowItemButton = player.hasPentacleSeal || player.currentChainCharges > 0;
        if (itemActionBtnObj != null)
        {
            if (itemActionBtnObj.activeSelf != shouldShowItemButton)
            {
                itemActionBtnObj.SetActive(shouldShowItemButton);
                if (!shouldShowItemButton) ReleaseActionHold(player);
            }

            if (shouldShowItemButton)
            {
                UpdateItemBadgeTextIfNeeded(player.currentChainCharges, player.maxChainCharges);
            }
        }
    }

    private void UpdateHandPickupLabelIfNeeded()
    {
        if (handPickupLabel == null) return;
        var currentLang = GameLocalizationManager.CurrentLanguage;
        if (lastPickupKey != pickupKey || lastLanguage != currentLang)
        {
            lastPickupKey = pickupKey;
            lastLanguage = currentLang;
            handPickupLabel.text = string.Format(GameLocalizationManager.T("HUD_PICKUP_HAND", "NHẶT DẤU ẤN [{0}]"), pickupKey);
        }
    }

    private void UpdateItemBadgeTextIfNeeded(int charges, int maxCharges)
    {
        if (itemActionBadgeText == null) return;
        if (lastCharges != charges || lastMaxCharges != maxCharges)
        {
            lastCharges = charges;
            lastMaxCharges = maxCharges;
            itemActionBadgeText.text = $"{charges}/{maxCharges}";
        }
    }

    private static bool ReadPickupKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (key == KeyCode.F && kb.fKey.wasPressedThisFrame) return true;
            if (key == KeyCode.G && kb.gKey.wasPressedThisFrame) return true;
            if (key == KeyCode.R && kb.rKey.wasPressedThisFrame) return true;
            if (key == KeyCode.E && kb.eKey.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(key)) return true;
#endif
        return false;
    }

    private void OnHandPickupClicked()
    {
        ThirdPersonPlayerController player = ThirdPersonPlayerController.Instance;
        if (player == null || currentNearbyItem == null) return;

        if (currentNearbyItem.TryManualCollect(player))
        {
            s_PickupConsumedFrame = Time.frameCount;
            currentNearbyItem = null;
            HasNearbyPickup = false;
            if (handPickupBtnObj != null) handPickupBtnObj.SetActive(false);
        }
    }

    private void ReleaseActionHold(ThirdPersonPlayerController player)
    {
        if (isActionHeld)
        {
            isActionHeld = false;
            if (player != null) player.SetVirtualBlock(false);
        }
    }

    private void BuildHUD()
    {
        EnsureSprites();

        GameObject canvasGo = new GameObject("ChainPickupHUD_Canvas");
        canvasGo.transform.SetParent(transform, false);
        hudCanvas = canvasGo.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 105;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        UIFontHelper.ConfigureCrispCanvas(hudCanvas, scaler);

        canvasGo.AddComponent<GraphicRaycaster>();

        Font uiFont = UIFontHelper.GetCrispFont(24);

        // 1. Nút Bàn Tay Nhặt Vật Phẩm (giữa dưới màn hình)
        handPickupBtnObj = new GameObject("HandPickupButton");
        handPickupBtnObj.transform.SetParent(canvasGo.transform, false);
        handRt = handPickupBtnObj.AddComponent<RectTransform>();
        handRt.anchorMin = new Vector2(0.5f, 0f);
        handRt.anchorMax = new Vector2(0.5f, 0f);
        handRt.pivot = new Vector2(0.5f, 0.5f);
        handRt.anchoredPosition = handButtonAnchoredPos;
        handRt.sizeDelta = new Vector2(handButtonSize, handButtonSize);

        Image handBg = handPickupBtnObj.AddComponent<Image>();
        handBg.sprite = s_CircleBgSprite;
        handBg.color = new Color(0.12f, 0.04f, 0.04f, 0.85f);

        Button handBtn = handPickupBtnObj.AddComponent<Button>();
        handBtn.onClick.AddListener(OnHandPickupClicked);

        GameObject handIconGo = new GameObject("HandIcon");
        handIconGo.transform.SetParent(handPickupBtnObj.transform, false);
        RectTransform iconRt = handIconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(62f, 62f);
        Image handIconImg = handIconGo.AddComponent<Image>();
        handIconImg.sprite = s_HandSprite;
        handIconImg.color = new Color(1.0f, 0.82f, 0.45f, 1f);
        handIconImg.raycastTarget = false;

        GameObject handTextGo = new GameObject("HandLabel");
        handTextGo.transform.SetParent(handPickupBtnObj.transform, false);
        RectTransform txtRt = handTextGo.AddComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.5f, 0f);
        txtRt.anchorMax = new Vector2(0.5f, 0f);
        txtRt.pivot = new Vector2(0.5f, 1f);
        txtRt.anchoredPosition = new Vector2(0f, -8f);
        txtRt.sizeDelta = new Vector2(340f, 34f);

        handPickupLabel = handTextGo.AddComponent<Text>();
        handPickupLabel.font = uiFont;
        handPickupLabel.fontSize = 20;
        handPickupLabel.fontStyle = FontStyle.Bold;
        handPickupLabel.alignment = TextAnchor.MiddleCenter;
        handPickupLabel.color = new Color(1f, 0.88f, 0.55f, 1f);
        handPickupLabel.text = "NHẶT [F]";
        handPickupLabel.raycastTarget = false;
        Outline handOutline = handTextGo.AddComponent<Outline>();
        handOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);

        handPickupBtnObj.SetActive(false);

        // 2. Nút Joystick Vật Phẩm Pentacle + Xích (góc dưới phải, chỉ hiện sau khi nhặt)
        itemActionBtnObj = new GameObject("PentacleChainActionButton");
        itemActionBtnObj.transform.SetParent(canvasGo.transform, false);
        actRt = itemActionBtnObj.AddComponent<RectTransform>();
        actRt.anchorMin = new Vector2(1f, 0f);
        actRt.anchorMax = new Vector2(1f, 0f);
        actRt.pivot = new Vector2(0.5f, 0.5f);
        actRt.anchoredPosition = itemActionAnchoredPos;
        actRt.sizeDelta = new Vector2(itemActionButtonSize, itemActionButtonSize);

        Image actBg = itemActionBtnObj.AddComponent<Image>();
        actBg.sprite = s_CircleBgSprite;
        actBg.color = new Color(0.55f, 0.06f, 0.06f, 0.85f);

        GameObject actIconGo = new GameObject("PentacleIcon");
        actIconGo.transform.SetParent(itemActionBtnObj.transform, false);
        RectTransform actIconRt = actIconGo.AddComponent<RectTransform>();
        actIconRt.anchorMin = new Vector2(0.5f, 0.5f);
        actIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        actIconRt.sizeDelta = new Vector2(76f, 76f);
        Image actIconImg = actIconGo.AddComponent<Image>();
        actIconImg.sprite = s_PentacleChainSprite;
        actIconImg.color = new Color(1.0f, 0.35f, 0.25f, 1f);
        actIconImg.raycastTarget = false;

        // Nhãn [E]
        GameObject keyGo = new GameObject("KeyHint");
        keyGo.transform.SetParent(itemActionBtnObj.transform, false);
        RectTransform keyRt = keyGo.AddComponent<RectTransform>();
        keyRt.anchorMin = new Vector2(0.5f, 0f);
        keyRt.anchorMax = new Vector2(0.5f, 0f);
        keyRt.anchoredPosition = new Vector2(0f, 12f);
        keyRt.sizeDelta = new Vector2(60f, 24f);
        Text keyTxt = keyGo.AddComponent<Text>();
        keyTxt.font = uiFont;
        keyTxt.fontSize = 18;
        keyTxt.fontStyle = FontStyle.Bold;
        keyTxt.alignment = TextAnchor.MiddleCenter;
        keyTxt.color = Color.white;
        keyTxt.text = "[E]";
        keyTxt.raycastTarget = false;

        // Badge số lượng (1/5)
        GameObject badgeGo = new GameObject("ChargeBadge");
        badgeGo.transform.SetParent(itemActionBtnObj.transform, false);
        RectTransform badgeRt = badgeGo.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(1f, 1f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.anchoredPosition = new Vector2(-14f, -14f);
        badgeRt.sizeDelta = new Vector2(56f, 28f);
        itemActionBadgeText = badgeGo.AddComponent<Text>();
        itemActionBadgeText.font = uiFont;
        itemActionBadgeText.fontSize = 18;
        itemActionBadgeText.fontStyle = FontStyle.Bold;
        itemActionBadgeText.alignment = TextAnchor.MiddleCenter;
        itemActionBadgeText.color = new Color(1f, 0.90f, 0.45f, 1f);
        itemActionBadgeText.text = "0/5";
        itemActionBadgeText.raycastTarget = false;
        Outline badgeOutline = badgeGo.AddComponent<Outline>();
        badgeOutline.effectColor = Color.black;

        // PointerDown / PointerUp để nhấn giữ trên cả Mobile lẫn PC
        EventTrigger trigger = itemActionBtnObj.AddComponent<EventTrigger>();
        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(_ =>
        {
            isActionHeld = true;
            if (ThirdPersonPlayerController.Instance != null)
            {
                ThirdPersonPlayerController.Instance.SetVirtualBlock(true);
            }
        });
        trigger.triggers.Add(downEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener(_ => ReleaseActionHold(ThirdPersonPlayerController.Instance));
        trigger.triggers.Add(upEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => ReleaseActionHold(ThirdPersonPlayerController.Instance));
        trigger.triggers.Add(exitEntry);

        itemActionBtnObj.SetActive(false);
        TryRegisterWithTouchUI();
    }

    private void TryRegisterWithTouchUI()
    {
        if (registeredWithTouchUI || VirtualJoystickTouchUI.Instance == null) return;
        if (handRt == null || actRt == null) return;

        registeredWithTouchUI = true;
        VirtualJoystickTouchUI.Instance.RegisterExternalHUDTarget(
            "HandPickup",
            GameLocalizationManager.T("HUD_EDIT_HAND_BTN", "Nút Bàn Tay Nhặt [F]"),
            handRt,
            handButtonAnchoredPos,
            1f
        );
        VirtualJoystickTouchUI.Instance.RegisterExternalHUDTarget(
            "PentacleChainAction",
            GameLocalizationManager.T("HUD_EDIT_SEAL_BTN", "Nút Dấu Ấn & Xích [E]"),
            actRt,
            itemActionAnchoredPos,
            1f
        );
    }

    private static void EnsureSprites()
    {
        if (s_CircleBgSprite == null)
        {
            s_CircleBgSprite = CreateCircleRingSprite(128);
        }
        if (s_HandSprite == null)
        {
            s_HandSprite = CreateHandIconSprite(96);
        }
        if (s_PentacleChainSprite == null)
        {
            s_PentacleChainSprite = CreatePentacleIconSprite(96);
        }
    }

    private static Sprite CreateCircleRingSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                Color c = new Color(0f, 0f, 0f, 0f);
                if (d <= 0.98f)
                {
                    float border = d >= 0.88f ? 1f : 0.45f;
                    c = new Color(1f, 1f, 1f, border);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHandIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                bool palm = Mathf.Abs(nx) < 0.34f && ny > -0.55f && ny < 0.08f;
                bool f1 = Mathf.Abs(nx + 0.25f) < 0.08f && ny >= 0.05f && ny < 0.55f;
                bool f2 = Mathf.Abs(nx + 0.08f) < 0.08f && ny >= 0.05f && ny < 0.68f;
                bool f3 = Mathf.Abs(nx - 0.09f) < 0.08f && ny >= 0.05f && ny < 0.64f;
                bool f4 = Mathf.Abs(nx - 0.26f) < 0.07f && ny >= 0.05f && ny < 0.48f;
                bool thumb = (nx < -0.25f && nx > -0.58f && ny > -0.35f && ny < 0.05f && Mathf.Abs((ny + 0.25f) - (nx + 0.35f) * 0.9f) < 0.14f);

                tex.SetPixel(x, y, (palm || f1 || f2 || f3 || f4 || thumb) ? Color.white : clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreatePentacleIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        float half = (size - 1) * 0.5f;
        float r = half * 0.85f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                if (Mathf.Abs(d - r) < 2.6f) tex.SetPixel(x, y, Color.white);
            }
        }

        Vector2[] pts = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.PI * 0.5f + i * (Mathf.PI * 2f / 5f);
            pts[i] = new Vector2(half + Mathf.Cos(a) * r, half + Mathf.Sin(a) * r);
        }
        for (int i = 0; i < 5; i++)
        {
            DrawLine(tex, pts[i], pts[(i + 2) % 5], 2.2f);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
        int w = tex.width;
        int h = tex.height;
        int rad = Mathf.CeilToInt(thickness);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            int cx = Mathf.RoundToInt(p.x);
            int cy = Mathf.RoundToInt(p.y);
            for (int dy = -rad; dy <= rad; dy++)
            {
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < w && py >= 0 && py < h && dx * dx + dy * dy <= thickness * thickness)
                    {
                        tex.SetPixel(px, py, Color.white);
                    }
                }
            }
        }
    }
}
