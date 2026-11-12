using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class GhostMazeSetupChatVoiceInScene
{
    static GhostMazeSetupChatVoiceInScene()
    {
        EditorApplication.delayCall += DelayCheckAndSetup;
    }

    private static void DelayCheckAndSetup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isLoaded && scene.name == "SampleScene")
        {
            SetupChatVoiceInScene(false);
        }
    }

    [MenuItem("GhostMaze/Bật Thanh Chat Lên Màn Hình (Kéo Thả Tự Do)")]
    public static void MenuSetupChatVoiceInScene()
    {
        SetupChatVoiceInScene(true);
    }

    public static void SetupChatVoiceInScene(bool forceNotify)
    {
        // 1. Tìm Canvas chính trong Scene (ưu tiên Canvas_GameSettings)
        GameObject canvasGo = GameObject.Find("Canvas_GameSettings");
        if (canvasGo == null)
        {
            Canvas anyCanvas = Object.FindAnyObjectByType<Canvas>();
            if (anyCanvas != null) canvasGo = anyCanvas.gameObject;
        }

        if (canvasGo == null)
        {
            if (forceNotify) Debug.LogWarning("[GhostMaze] Không tìm thấy Canvas trong Scene!");
            return;
        }

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        UIFontHelper.ConfigureCrispCanvas(canvas, scaler);

        // 2. Kiểm tra xem PhotonChatVoicePanel đã có trên Canvas chưa
        Transform existingPanel = canvasGo.transform.Find("PhotonChatVoicePanel");
        if (existingPanel != null)
        {
            existingPanel.gameObject.SetActive(true);
            UIFontHelper.ApplyCrispnessRecursively(existingPanel.gameObject, 12);
            if (forceNotify)
            {
                Selection.activeGameObject = existingPanel.gameObject;
                Debug.Log("<color=#00FF88>[GhostMaze] Thanh Chat đã có sẵn trên màn hình! Bạn có thể kéo thả và căn chỉnh tự do.</color>");
            }
            return;
        }

        // 3. Nạp Font chữ sắc nét (Roboto / GameFont)
        Font crispFont = UIFontHelper.GetCrispFont(14);

        // 4. Khởi tạo Khung Chat & Voice chính (PhotonChatVoicePanel)
        GameObject panel = new GameObject("PhotonChatVoicePanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create PhotonChatVoicePanel");
        panel.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 190f);
        panelRect.sizeDelta = new Vector2(440f, 96f);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
        panelBg.raycastTarget = true;

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.2f, 0.5f, 0.8f, 0.45f);
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // ==========================================
        // HÀNG TRÊN: ChatInputRow (Ô nhập tin nhắn + Nút gửi)
        // ==========================================
        GameObject chatInputRow = new GameObject("ChatInputRow");
        chatInputRow.transform.SetParent(panel.transform, false);
        RectTransform inputRowRect = chatInputRow.AddComponent<RectTransform>();
        inputRowRect.anchorMin = new Vector2(0f, 1f);
        inputRowRect.anchorMax = new Vector2(1f, 1f);
        inputRowRect.pivot = new Vector2(0.5f, 1f);
        inputRowRect.anchoredPosition = new Vector2(0f, -6f);
        inputRowRect.sizeDelta = new Vector2(-12f, 40f);

        // Ô Nhập liệu (InputField)
        GameObject inputBoxObj = new GameObject("ChatInputField");
        inputBoxObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform inputBoxRect = inputBoxObj.AddComponent<RectTransform>();
        inputBoxRect.anchorMin = new Vector2(0f, 0f);
        inputBoxRect.anchorMax = new Vector2(0.83f, 1f);
        inputBoxRect.offsetMin = Vector2.zero;
        inputBoxRect.offsetMax = new Vector2(-4f, 0f);

        Image inputBg = inputBoxObj.AddComponent<Image>();
        inputBg.color = new Color(0.10f, 0.15f, 0.22f, 0.95f);

        InputField inputField = inputBoxObj.AddComponent<InputField>();
        inputField.characterLimit = 40;
        inputField.lineType = InputField.LineType.SingleLine;

        // Placeholder Text
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputBoxObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10f, 2f);
        phRect.offsetMax = new Vector2(-10f, -2f);

        Text phText = phObj.AddComponent<Text>();
        phText.font = crispFont;
        phText.fontSize = 13;
        phText.fontStyle = FontStyle.Normal;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.color = new Color(0.72f, 0.80f, 0.90f, 0.85f);
        phText.text = "💬 Nhấn [Enter] / chạm để Chat...";
        phText.raycastTarget = false;

        // Input Text
        GameObject inTxtObj = new GameObject("Text");
        inTxtObj.transform.SetParent(inputBoxObj.transform, false);
        RectTransform inTxtRect = inTxtObj.AddComponent<RectTransform>();
        inTxtRect.anchorMin = Vector2.zero;
        inTxtRect.anchorMax = Vector2.one;
        inTxtRect.offsetMin = new Vector2(10f, 2f);
        inTxtRect.offsetMax = new Vector2(-10f, -2f);

        Text inText = inTxtObj.AddComponent<Text>();
        inText.font = crispFont;
        inText.fontSize = 14;
        inText.alignment = TextAnchor.MiddleLeft;
        inText.color = Color.white;
        inText.supportRichText = false;
        inText.raycastTarget = false;

        inputField.textComponent = inText;
        inputField.placeholder = phText;

        // Nút GỬI (SendButton)
        GameObject sendBtnObj = new GameObject("SendButton");
        sendBtnObj.transform.SetParent(chatInputRow.transform, false);
        RectTransform sendRect = sendBtnObj.AddComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(0.84f, 0f);
        sendRect.anchorMax = new Vector2(1f, 1f);
        sendRect.offsetMin = Vector2.zero;
        sendRect.offsetMax = Vector2.zero;

        Image sendBg = sendBtnObj.AddComponent<Image>();
        sendBg.color = new Color(0.18f, 0.50f, 0.92f, 0.96f);

        sendBtnObj.AddComponent<Button>();

        GameObject sendTxtObj = new GameObject("Text");
        sendTxtObj.transform.SetParent(sendBtnObj.transform, false);
        RectTransform stRect = sendTxtObj.AddComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        Text sendTxt = sendTxtObj.AddComponent<Text>();
        sendTxt.font = crispFont;
        sendTxt.fontSize = 17;
        sendTxt.fontStyle = FontStyle.Bold;
        sendTxt.alignment = TextAnchor.MiddleCenter;
        sendTxt.color = Color.white;
        sendTxt.text = "✈";
        sendTxt.raycastTarget = false;

        // ==========================================
        // HÀNG DƯỚI: BottomControlBar (4 nút điều khiển)
        // ==========================================
        GameObject bottomBar = new GameObject("BottomControlBar");
        bottomBar.transform.SetParent(panel.transform, false);
        RectTransform bottomRect = bottomBar.AddComponent<RectTransform>();
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(1f, 0f);
        bottomRect.pivot = new Vector2(0.5f, 0f);
        bottomRect.anchoredPosition = new Vector2(0f, 6f);
        bottomRect.sizeDelta = new Vector2(-12f, 36f);

        // 1. ChatBadgeButton
        CreateBottomBarButton(bottomBar.transform, "ChatBadgeButton", new Vector2(0f, 0f), new Vector2(0.28f, 1f),
            new Vector2(0f, 0f), new Vector2(-2f, 0f), new Color(0.12f, 0.18f, 0.25f, 0.92f),
            "Chat [Enter] ●", 11, new Color(0.35f, 0.95f, 0.60f, 1f), crispFont);

        // 2. MicToggleButton
        CreateBottomBarButton(bottomBar.transform, "MicToggleButton", new Vector2(0.28f, 0f), new Vector2(0.68f, 1f),
            new Vector2(2f, 0f), new Vector2(-2f, 0f), new Color(0.65f, 0.22f, 0.22f, 0.92f),
            "🎙 MIC: TẮT [V]", 11, Color.white, crispFont);

        // 3. VoiceSettingsButton
        CreateBottomBarButton(bottomBar.transform, "VoiceSettingsButton", new Vector2(0.68f, 0f), new Vector2(0.88f, 1f),
            new Vector2(2f, 0f), new Vector2(-2f, 0f), new Color(0.18f, 0.48f, 0.88f, 0.92f),
            "🔊 VOICE", 11, Color.white, crispFont);

        // 4. MinimizeChatButton
        CreateBottomBarButton(bottomBar.transform, "MinimizeChatButton", new Vector2(0.88f, 0f), new Vector2(1f, 1f),
            new Vector2(2f, 0f), Vector2.zero, new Color(0.16f, 0.22f, 0.30f, 0.92f),
            "▼", 12, Color.white, crispFont);

        // SpeakingIndicator (Dòng chữ hiển thị người nói)
        GameObject speakObj = new GameObject("SpeakingIndicator");
        speakObj.transform.SetParent(panel.transform, false);
        RectTransform speakRect = speakObj.AddComponent<RectTransform>();
        speakRect.anchorMin = new Vector2(0f, 1f);
        speakRect.anchorMax = new Vector2(1f, 1f);
        speakRect.pivot = new Vector2(0.5f, 0f);
        speakRect.anchoredPosition = new Vector2(0f, 3f);
        speakRect.sizeDelta = new Vector2(0f, 20f);

        Text speakTxt = speakObj.AddComponent<Text>();
        speakTxt.font = crispFont;
        speakTxt.fontSize = 12;
        speakTxt.fontStyle = FontStyle.Italic;
        speakTxt.alignment = TextAnchor.MiddleLeft;
        speakTxt.color = new Color(0.3f, 1f, 0.55f, 1f);
        speakTxt.text = "";
        speakTxt.raycastTarget = false;

        // ChatHistoryViewport (Lịch sử tin nhắn nổi trên ô nhập)
        GameObject vpObj = new GameObject("ChatHistoryViewport");
        vpObj.transform.SetParent(panel.transform, false);
        RectTransform vpRect = vpObj.AddComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0f, 1f);
        vpRect.anchorMax = new Vector2(1f, 1f);
        vpRect.pivot = new Vector2(0.5f, 0f);
        vpRect.anchoredPosition = new Vector2(0f, 26f);
        vpRect.sizeDelta = new Vector2(0f, 178f);

        Image vpBg = vpObj.AddComponent<Image>();
        vpBg.color = new Color(0.03f, 0.05f, 0.09f, 0.45f);
        vpBg.raycastTarget = false;
        vpObj.AddComponent<RectMask2D>();

        GameObject histObj = new GameObject("ChatHistoryText");
        histObj.transform.SetParent(vpObj.transform, false);
        RectTransform histRect = histObj.AddComponent<RectTransform>();
        histRect.anchorMin = new Vector2(0f, 0f);
        histRect.anchorMax = new Vector2(1f, 0f);
        histRect.pivot = new Vector2(0.5f, 0f);
        histRect.anchoredPosition = new Vector2(0f, 4f);
        histRect.sizeDelta = new Vector2(-14f, 0f);

        Text histTxt = histObj.AddComponent<Text>();
        histTxt.font = crispFont;
        histTxt.fontSize = 14;
        histTxt.lineSpacing = 1.08f;
        histTxt.alignment = TextAnchor.LowerLeft;
        histTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        histTxt.verticalOverflow = VerticalWrapMode.Overflow;
        histTxt.supportRichText = true;
        histTxt.color = new Color(0.95f, 0.97f, 1f, 0.98f);
        histTxt.raycastTarget = false;

        Shadow histShadow = histObj.AddComponent<Shadow>();
        histShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        histShadow.effectDistance = new Vector2(1f, -1f);

        ContentSizeFitter histFitter = histObj.AddComponent<ContentSizeFitter>();
        histFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        histFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Lưu lại Scene
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorSceneManager.SaveOpenScenes();

        Selection.activeGameObject = panel;
        Debug.Log("<color=#00FF88>[GhostMaze] ĐÃ TẠO VÀ BẬT THANH CHAT & VOICE TRÊN MÀN HÌNH (Canvas_GameSettings)! Bạn có thể thoải mái chọn, di chuyển hoặc đổi kích thước trên Unity Editor Scene.</color>");
    }

    private static GameObject CreateBottomBarButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color bgColor, string text, int fontSize, Color textColor, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = true;

        btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.text = text;
        txt.raycastTarget = false;

        return btnObj;
    }
}
#endif
