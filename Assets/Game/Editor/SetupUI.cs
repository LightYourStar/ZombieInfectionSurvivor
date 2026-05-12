using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.UI;
using Game.Core;
using Game.Gameplay.Player;

public static class SetupUI
{
    [MenuItem("Tools/Setup UI")]
    public static void Execute()
    {
        // 调整摄像机
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 22f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            EditorUtility.SetDirty(cam);
        }

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("Canvas not found"); return; }
        var canvasRT = canvas.GetComponent<RectTransform>();

        // ===== StartPanel =====
        var startPanel = CreatePanel("StartPanel", canvasRT, true);
        var startBtnGO = CreateButton("StartButton", startPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(200, 80), "开始游戏", new Color(0.2f, 0.7f, 0.3f));
        var startPanelComp = startPanel.AddComponent<StartPanel>();

        // ===== HUDPanel =====
        var hudPanel = CreatePanel("HUDPanel", canvasRT, false);
        var hudRT = hudPanel.GetComponent<RectTransform>();
        var timeTextComp = CreateText("TimeText", hudRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(200, 50), "04:00", 32, Color.white);
        var levelTextComp = CreateText("LevelText", hudRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(150, 40), "Lv.1", 24, Color.white);
        var expTextComp = CreateText("ExpText", hudRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -60), new Vector2(200, 40), "EXP: 0", 20, Color.white);
        var goldTextComp = CreateText("GoldText", hudRT, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(200, 40), "金币: 0", 24, Color.yellow);
        var hudPanelComp = hudPanel.AddComponent<HUDPanel>();

        // ===== UpgradePanel =====
        var upgradePanel = new GameObject("UpgradePanel", typeof(RectTransform), typeof(Image));
        upgradePanel.transform.SetParent(canvasRT, false);
        var upgRT = upgradePanel.GetComponent<RectTransform>();
        upgRT.anchorMin = new Vector2(0.1f, 0.3f);
        upgRT.anchorMax = new Vector2(0.9f, 0.7f);
        upgRT.offsetMin = Vector2.zero;
        upgRT.offsetMax = Vector2.zero;
        upgradePanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        upgradePanel.SetActive(false);
        var upgradePanelComp = upgradePanel.AddComponent<UpgradePanel>();

        var btnArray = new Button[3];
        var txtArray = new Text[3];
        string[] names = { "感染半径 +", "移动速度 +", "僵尸上限 +" };
        for (int i = 0; i < 3; i++)
        {
            var btn = CreateButton($"OptionBtn{i}", upgRT, Vector2.zero, Vector2.zero, names[i], new Color(0.3f, 0.5f, 0.8f));
            var btnRT = btn.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.05f + i * 0.32f, 0.15f);
            btnRT.anchorMax = new Vector2(0.05f + i * 0.32f + 0.28f, 0.85f);
            btnRT.anchoredPosition = Vector2.zero;
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;
            btnArray[i] = btn.GetComponent<Button>();
            txtArray[i] = btn.GetComponentInChildren<Text>();
        }

        // ===== SettlementPanel =====
        var settlementPanel = CreatePanel("SettlementPanel", canvasRT, false);
        var settRT = settlementPanel.GetComponent<RectTransform>();
        var settImg = settlementPanel.AddComponent<Image>();
        settImg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        var infText = CreateText("InfectionCountText", settRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(300, 50), "感染: 0", 28, Color.white);
        var sGoldText = CreateText("GoldText", settRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(300, 50), "金币: 0", 24, Color.yellow);
        var sExpText = CreateText("ExpText", settRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(300, 50), "经验: 0", 24, Color.cyan);
        var contBtn = CreateButton("ContinueButton", settRT, new Vector2(0, -100), new Vector2(200, 60), "继续", new Color(0.2f, 0.7f, 0.3f));
        var settlementPanelComp = settlementPanel.AddComponent<SettlementPanel>();

        // ===== VirtualJoystick =====
        var jsBG = new GameObject("VirtualJoystick", typeof(RectTransform), typeof(Image));
        jsBG.transform.SetParent(canvasRT, false);
        var jsBGRT = jsBG.GetComponent<RectTransform>();
        jsBGRT.anchorMin = Vector2.zero;
        jsBGRT.anchorMax = Vector2.zero;
        jsBGRT.pivot = new Vector2(0.5f, 0.5f);
        jsBGRT.anchoredPosition = new Vector2(200, 200);
        jsBGRT.sizeDelta = new Vector2(200, 200);
        jsBG.GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);

        var jsHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        jsHandle.transform.SetParent(jsBGRT, false);
        var handleRT = jsHandle.GetComponent<RectTransform>();
        handleRT.anchoredPosition = Vector2.zero;
        handleRT.sizeDelta = new Vector2(60, 60);
        jsHandle.GetComponent<Image>().color = new Color(1, 1, 1, 0.7f);

        var joystickComp = jsBG.AddComponent<VirtualJoystick>();

        // ===== Wire references via SerializedObject =====
        var gsrGO = GameObject.Find("GameSystemRunner");
        var gsr = gsrGO.GetComponent<GameSystemRunner>();
        var uiMgr = gsrGO.GetComponent<UIManager>();
        var stateManager = gsrGO.GetComponent<GameStateManager>();

        // UIManager
        var uiSO = new SerializedObject(uiMgr);
        uiSO.FindProperty("m_startPanel").objectReferenceValue = startPanel;
        uiSO.FindProperty("m_hudPanel").objectReferenceValue = hudPanel;
        uiSO.FindProperty("m_upgradePanel").objectReferenceValue = upgradePanel;
        uiSO.FindProperty("m_settlementPanel").objectReferenceValue = settlementPanel;
        uiSO.ApplyModifiedProperties();

        // GameSystemRunner
        var gsrSO = new SerializedObject(gsr);
        gsrSO.FindProperty("m_stateManager").objectReferenceValue = stateManager;
        gsrSO.FindProperty("m_uiManager").objectReferenceValue = uiMgr;
        gsrSO.FindProperty("m_hudPanel").objectReferenceValue = hudPanelComp;
        gsrSO.FindProperty("m_upgradePanel").objectReferenceValue = upgradePanelComp;
        gsrSO.FindProperty("m_settlementPanel").objectReferenceValue = settlementPanelComp;
        gsrSO.ApplyModifiedProperties();

        // StartPanel
        var spSO = new SerializedObject(startPanelComp);
        spSO.FindProperty("m_startButton").objectReferenceValue = startBtnGO.GetComponent<Button>();
        spSO.FindProperty("m_stateManager").objectReferenceValue = stateManager;
        spSO.ApplyModifiedProperties();

        // HUDPanel
        var hudSO = new SerializedObject(hudPanelComp);
        hudSO.FindProperty("m_timeText").objectReferenceValue = timeTextComp;
        hudSO.FindProperty("m_expText").objectReferenceValue = expTextComp;
        hudSO.FindProperty("m_levelText").objectReferenceValue = levelTextComp;
        hudSO.FindProperty("m_goldText").objectReferenceValue = goldTextComp;
        hudSO.ApplyModifiedProperties();

        // UpgradePanel
        var upgSO = new SerializedObject(upgradePanelComp);
        var btnsProp = upgSO.FindProperty("m_optionButtons");
        btnsProp.arraySize = 3;
        for (int i = 0; i < 3; i++) btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = btnArray[i];
        var txtsProp = upgSO.FindProperty("m_optionTexts");
        txtsProp.arraySize = 3;
        for (int i = 0; i < 3; i++) txtsProp.GetArrayElementAtIndex(i).objectReferenceValue = txtArray[i];
        upgSO.ApplyModifiedProperties();

        // SettlementPanel
        var settSO = new SerializedObject(settlementPanelComp);
        settSO.FindProperty("m_infectionCountText").objectReferenceValue = infText;
        settSO.FindProperty("m_goldText").objectReferenceValue = sGoldText;
        settSO.FindProperty("m_expText").objectReferenceValue = sExpText;
        settSO.FindProperty("m_continueButton").objectReferenceValue = contBtn.GetComponent<Button>();
        settSO.ApplyModifiedProperties();

        // VirtualJoystick
        var jsSO = new SerializedObject(joystickComp);
        jsSO.FindProperty("m_handle").objectReferenceValue = handleRT;
        jsSO.FindProperty("m_radius").floatValue = 100f;
        jsSO.ApplyModifiedProperties();

        // PlayerController -> joystick
        var playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            var pc = playerGO.GetComponent<PlayerController>();
            if (pc != null)
            {
                var pcSO = new SerializedObject(pc);
                pcSO.FindProperty("m_joystick").objectReferenceValue = joystickComp;
                pcSO.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupUI] UI 搭建完成，所有引用已注入，场景已保存！");
    }

    private static GameObject CreatePanel(string name, RectTransform parent, bool active)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.SetActive(active);
        return go;
    }

    private static Text CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, string text, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    private static GameObject CreateButton(string name, RectTransform parent, Vector2 pos, Vector2 size, string label, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = bgColor;

        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(rt, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var txt = txtGO.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return go;
    }
}
