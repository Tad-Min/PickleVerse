using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UI;

public class GenerateCareerUIPrefab
{
    private static Color ColorNavyDark = new Color(0.05f, 0.1f, 0.2f, 0.6f);
    private static Color ColorCyan = new Color(0.0f, 1.0f, 1.0f, 1.0f);
    private static Color ColorGold = new Color(1.0f, 0.8f, 0.0f, 1.0f);
    private static Color ColorGreen = new Color(0.2f, 0.8f, 0.2f, 1.0f);
    private static Color ColorRed = new Color(0.8f, 0.2f, 0.2f, 1.0f);
    private static Color ColorYellow = new Color(0.8f, 0.8f, 0.2f, 1.0f);
    private static Color ColorInactive = new Color(0.6f, 0.6f, 0.6f, 1.0f);

    [MenuItem("Tools/Generate Career Component UI")]
    public static void Generate()
    {
        // Setup root
        GameObject root = new GameObject("CareerComponent", typeof(RectTransform), typeof(Image), typeof(CareerTabController));
        Stretch(root.GetComponent<RectTransform>(), 0, 0, 1, 1);
        
        Image bg = root.GetComponent<Image>();
        bg.color = ColorNavyDark;

        // Nav Bar
        GameObject navBar = CreateRect("NavBar", root.transform);
        Stretch(navBar.GetComponent<RectTransform>(), 0.1f, 0.85f, 0.9f, 0.95f);
        
        HorizontalLayoutGroup navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;
        navLayout.spacing = 20;

        // Content Area
        GameObject contentArea = CreateRect("ContentArea", root.transform);
        Stretch(contentArea.GetComponent<RectTransform>(), 0.1f, 0.1f, 0.9f, 0.82f);

        // Create Tabs
        string[] tabNames = { "ACT RANK", "MATCH HISTORY", "LEADERBOARD" };
        CareerTabController controller = root.GetComponent<CareerTabController>();
        controller.tabs = new CareerTabController.Tab[3];

        GameObject actRankPanel = CreateActRankPanel(contentArea.transform);
        GameObject matchHistoryPanel = CreateMatchHistoryPanel(contentArea.transform);
        GameObject leaderboardPanel = CreateLeaderboardPanel(contentArea.transform);

        GameObject[] panels = { actRankPanel, matchHistoryPanel, leaderboardPanel };

        for (int i = 0; i < tabNames.Length; i++)
        {
            GameObject tabBtnObj = CreateRect("Tab_" + tabNames[i], navBar.transform);
            Button btn = tabBtnObj.AddComponent<Button>();
            
            // Text
            GameObject textObj = CreateRect("Text", tabBtnObj.transform);
            Stretch(textObj.GetComponent<RectTransform>(), 0, 0, 1, 1);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = tabNames[i];
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;

            // Underline
            GameObject underlineObj = CreateRect("Underline", tabBtnObj.transform);
            RectTransform rtUnder = underlineObj.GetComponent<RectTransform>();
            rtUnder.anchorMin = new Vector2(0, 0);
            rtUnder.anchorMax = new Vector2(1, 0);
            rtUnder.pivot = new Vector2(0.5f, 0);
            rtUnder.anchoredPosition = Vector2.zero;
            rtUnder.sizeDelta = new Vector2(0, 4);
            Image underImg = underlineObj.AddComponent<Image>();
            underImg.color = ColorCyan;

            controller.tabs[i] = new CareerTabController.Tab {
                tabButton = btn,
                tabText = tmp,
                contentPanel = panels[i],
                underline = underlineObj
            };
        }

        string path = "Assets/Prefabs/UI/CareerComponent.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        Debug.Log("Saved CareerComponent Prefab at " + path);
    }

    private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, float anchorX, float anchorY, float width, float height, float posX = 0, float posY = 0, Vector2? pivot = null)
    {
        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);
        if (pivot.HasValue) rt.pivot = pivot.Value;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(posX, posY);
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, Color color, float size, TextAlignmentOptions align)
    {
        GameObject go = CreateRect(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    private static GameObject CreateActRankPanel(Transform parent)
    {
        GameObject panel = CreateRect("ActRankPanel", parent);
        Stretch(panel.GetComponent<RectTransform>(), 0, 0, 1, 1);

        // Icon
        GameObject iconObj = CreateRect("RankIcon", panel.transform);
        Place(iconObj.GetComponent<RectTransform>(), 0.5f, 0.6f, 200, 200);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.color = ColorGold;

        // Rank Name
        TextMeshProUGUI title = CreateText("RankName", panel.transform, "RADIANT", ColorGold, 48, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0.5f, 0.35f, 400, 60);
        title.fontStyle = FontStyles.Bold;

        // Progress Bar
        CreateProgressBar(panel.transform, new Vector2(0.5f, 0.2f));

        return panel;
    }

    private static GameObject CreateMatchHistoryPanel(Transform parent)
    {
        GameObject panel = CreateRect("MatchHistoryPanel", parent);
        Stretch(panel.GetComponent<RectTransform>(), 0, 0, 1, 1);

        // Header (Rank + Progress)
        GameObject header = CreateRect("Header", panel.transform);
        Stretch(header.GetComponent<RectTransform>(), 0, 0.85f, 1, 1);
        
        GameObject iconObj = CreateRect("RankIcon", header.transform);
        Place(iconObj.GetComponent<RectTransform>(), 0, 0.5f, 60, 60, 40, 0, new Vector2(0, 0.5f));
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.color = ColorGold;

        TextMeshProUGUI title = CreateText("RankName", header.transform, "RADIANT", ColorGold, 32, TextAlignmentOptions.Left);
        Place(title.rectTransform, 0, 0.5f, 300, 50, 120, 0, new Vector2(0, 0.5f));
        title.fontStyle = FontStyles.Bold;

        CreateProgressBar(header.transform, new Vector2(0.7f, 0.5f));

        // Scroll View Setup
        GameObject scroll = CreateRect("ScrollView", panel.transform);
        Stretch(scroll.GetComponent<RectTransform>(), 0, 0, 1, 0.85f);
        ScrollRect sr = scroll.AddComponent<ScrollRect>();
        Image srBg = scroll.AddComponent<Image>();
        srBg.color = new Color(0,0,0,0);
        
        GameObject viewport = CreateRect("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 1, 1);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateRect("Content", viewport.transform);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);
        rtContent.offsetMin = Vector2.zero; rtContent.offsetMax = Vector2.zero; // VERY IMPORTANT FIX
        rtContent.anchoredPosition = Vector2.zero;
        
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 10;
        
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = viewport.GetComponent<RectTransform>();

        // Generate 8 rows
        for (int i = 0; i < 8; i++)
        {
            Color resColor = (i % 3 == 0) ? ColorGreen : ((i % 3 == 1) ? ColorRed : ColorYellow);
            string resStr = (i % 3 == 0) ? "VICTORY" : ((i % 3 == 1) ? "DEFEAT" : "DRAW");
            CreateMatchRow(content.transform, i, resColor, resStr);
        }

        return panel;
    }

    private static GameObject CreateLeaderboardPanel(Transform parent)
    {
        GameObject panel = CreateRect("LeaderboardPanel", parent);
        Stretch(panel.GetComponent<RectTransform>(), 0, 0, 1, 1);

        // Headers
        GameObject headers = CreateRect("Headers", panel.transform);
        Stretch(headers.GetComponent<RectTransform>(), 0, 0.9f, 1, 1);
        
        var lblRank = CreateText("LblRank", headers.transform, "RANK", ColorInactive, 18, TextAlignmentOptions.Center);
        Stretch(lblRank.rectTransform, 0f, 0f, 0.15f, 1f);

        var lblRating = CreateText("LblRating", headers.transform, "RATING", ColorInactive, 18, TextAlignmentOptions.Center);
        Stretch(lblRating.rectTransform, 0.15f, 0f, 0.35f, 1f);

        var lblPlayer = CreateText("LblPlayer", headers.transform, "PLAYER", ColorInactive, 18, TextAlignmentOptions.Left);
        Stretch(lblPlayer.rectTransform, 0.35f, 0f, 0.7f, 1f);
        lblPlayer.rectTransform.offsetMin = new Vector2(80, 0); // Align with Player Name text

        var lblGames = CreateText("LblGames", headers.transform, "GAMES WON", ColorInactive, 18, TextAlignmentOptions.Right);
        Stretch(lblGames.rectTransform, 0.7f, 0f, 1.0f, 1f);
        lblGames.rectTransform.offsetMax = new Vector2(-40, 0);

        // Scroll
        GameObject scroll = CreateRect("ScrollView", panel.transform);
        Stretch(scroll.GetComponent<RectTransform>(), 0, 0.1f, 1, 0.9f);
        ScrollRect sr = scroll.AddComponent<ScrollRect>();
        
        GameObject viewport = CreateRect("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 1, 1);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateRect("Content", viewport.transform);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);
        rtContent.offsetMin = Vector2.zero; rtContent.offsetMax = Vector2.zero; // VERY IMPORTANT FIX
        rtContent.anchoredPosition = Vector2.zero;
        
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.spacing = 5;
        
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = viewport.GetComponent<RectTransform>();

        // Generate Rows
        for (int i = 1; i <= 12; i++)
        {
            CreateLeaderboardRow(content.transform, i);
        }

        // Pagination
        GameObject pagination = CreateRect("Pagination", panel.transform);
        Stretch(pagination.GetComponent<RectTransform>(), 0, 0, 1, 0.1f);

        TextMeshProUGUI pageTxt = CreateText("PageTxt", pagination.transform, "PAGE 1 / 1,501", Color.white, 24, TextAlignmentOptions.Center);
        Place(pageTxt.rectTransform, 0.5f, 0.5f, 200, 40);

        GameObject leftBtnObj = CreateRect("BtnPrev", pagination.transform);
        Place(leftBtnObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 50, 50, -150, 0);
        Image imgLeft = leftBtnObj.AddComponent<Image>();
        imgLeft.color = ColorInactive;
        leftBtnObj.AddComponent<Button>();
        TextMeshProUGUI txtLeft = CreateText("Txt", leftBtnObj.transform, "<", Color.white, 24, TextAlignmentOptions.Center);
        Stretch(txtLeft.rectTransform, 0, 0, 1, 1);

        GameObject rightBtnObj = CreateRect("BtnNext", pagination.transform);
        Place(rightBtnObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 50, 50, 150, 0);
        Image imgRight = rightBtnObj.AddComponent<Image>();
        imgRight.color = ColorInactive;
        rightBtnObj.AddComponent<Button>();
        TextMeshProUGUI txtRight = CreateText("Txt", rightBtnObj.transform, ">", Color.white, 24, TextAlignmentOptions.Center);
        Stretch(txtRight.rectTransform, 0, 0, 1, 1);

        return panel;
    }

    private static void CreateProgressBar(Transform parent, Vector2 anchorPosition)
    {
        GameObject progressArea = CreateRect("ProgressArea", parent);
        Place(progressArea.GetComponent<RectTransform>(), anchorPosition.x, anchorPosition.y, 400, 60);

        TextMeshProUGUI lbl = CreateText("Lbl", progressArea.transform, "RANK RATING", ColorInactive, 18, TextAlignmentOptions.Left);
        Stretch(lbl.rectTransform, 0, 0.5f, 0.5f, 1f);

        TextMeshProUGUI val = CreateText("Val", progressArea.transform, "15/100", Color.white, 18, TextAlignmentOptions.Right);
        Stretch(val.rectTransform, 0.5f, 0.5f, 1f, 1f);

        GameObject barBg = CreateRect("BarBg", progressArea.transform);
        Stretch(barBg.GetComponent<RectTransform>(), 0, 0, 1, 0.4f);
        Image imgBg = barBg.AddComponent<Image>();
        imgBg.color = new Color(0.2f, 0.2f, 0.2f, 1);

        GameObject barFill = CreateRect("BarFill", barBg.transform);
        Stretch(barFill.GetComponent<RectTransform>(), 0, 0, 0.15f, 1); // 15% fill
        Image imgFill = barFill.AddComponent<Image>();
        imgFill.color = ColorCyan;
    }

    private static void CreateMatchRow(Transform parent, int index, Color resColor, string resStr)
    {
        GameObject row = CreateRect("Row_" + index, parent);
        RectTransform rtRow = row.GetComponent<RectTransform>();
        rtRow.sizeDelta = new Vector2(0, 80); // Fixed height, width driven by VLG

        // Normal Background
        Image bgImg = row.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.3f);

        // Hover Highlight setup
        GameObject hoverVisuals = CreateRect("HoverVisuals", row.transform);
        Stretch(hoverVisuals.GetComponent<RectTransform>(), 0, 0, 1, 1);
        Image hoverImg = hoverVisuals.AddComponent<Image>();
        UIGradient grad = hoverVisuals.AddComponent<UIGradient>();
        grad.colorLeft = ColorNavyDark;
        grad.colorRight = ColorCyan;
        Outline outline = hoverVisuals.AddComponent<Outline>();
        outline.effectColor = ColorCyan;
        outline.effectDistance = new Vector2(2, -2);
        hoverVisuals.SetActive(false); // Default off

        HoverHighlight hh = row.AddComponent<HoverHighlight>();
        hh.hoverVisuals = hoverVisuals;
        
        // Result Strip
        GameObject strip = CreateRect("Strip", row.transform);
        Stretch(strip.GetComponent<RectTransform>(), 0, 0, 0, 1); // Anchor at x=0
        strip.GetComponent<RectTransform>().sizeDelta = new Vector2(10, 0); // Override width
        Image stripImg = strip.AddComponent<Image>();
        stripImg.color = resColor;

        // Avatar
        GameObject avatar = CreateRect("Avatar", row.transform);
        Place(avatar.GetComponent<RectTransform>(), 0, 0.5f, 60, 60, 40, 0, new Vector2(0, 0.5f));
        Image avatarImg = avatar.AddComponent<Image>(); // Placeholder

        // WL Score
        TextMeshProUGUI wl = CreateText("WL", row.transform, "55 / 22", ColorInactive, 24, TextAlignmentOptions.Left);
        Place(wl.rectTransform, 0, 0.5f, 150, 40, 120, 0, new Vector2(0, 0.5f));

        // Result
        TextMeshProUGUI result = CreateText("Result", row.transform, resStr, resColor, 28, TextAlignmentOptions.Center);
        Place(result.rectTransform, 0.5f, 0.5f, 200, 40, 0, 0);
        result.fontStyle = FontStyles.Bold;

        // Score
        TextMeshProUGUI score = CreateText("Score", row.transform, "SCORE: 2,340", Color.white, 24, TextAlignmentOptions.Right);
        Place(score.rectTransform, 1, 0.5f, 250, 40, -40, 0, new Vector2(1, 0.5f));
    }

    private static void CreateLeaderboardRow(Transform parent, int rank)
    {
        GameObject row = CreateRect("Row_" + rank, parent);
        RectTransform rtRow = row.GetComponent<RectTransform>();
        rtRow.sizeDelta = new Vector2(0, 70); 
        Image bgImg = row.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.3f);

        GameObject hoverVisuals = CreateRect("HoverVisuals", row.transform);
        Stretch(hoverVisuals.GetComponent<RectTransform>(), 0, 0, 1, 1);
        Image hoverImg = hoverVisuals.AddComponent<Image>();
        UIGradient grad = hoverVisuals.AddComponent<UIGradient>();
        grad.colorLeft = ColorNavyDark;
        grad.colorRight = ColorCyan;
        Outline outline = hoverVisuals.AddComponent<Outline>();
        outline.effectColor = ColorCyan;
        outline.effectDistance = new Vector2(2, -2);
        hoverVisuals.SetActive(false);

        HoverHighlight hh = row.AddComponent<HoverHighlight>();
        hh.hoverVisuals = hoverVisuals;

        // Rank
        TextMeshProUGUI rnk = CreateText("Rank", row.transform, rank.ToString(), Color.white, 22, TextAlignmentOptions.Center);
        Stretch(rnk.rectTransform, 0f, 0f, 0.15f, 1f);
        
        // Rating
        TextMeshProUGUI rtg = CreateText("Rating", row.transform, (1200 - rank * 12).ToString(), Color.white, 22, TextAlignmentOptions.Center);
        Stretch(rtg.rectTransform, 0.15f, 0f, 0.35f, 1f);
        
        // Player
        GameObject playerContainer = CreateRect("PlayerContainer", row.transform);
        Stretch(playerContainer.GetComponent<RectTransform>(), 0.35f, 0f, 0.7f, 1f);
        playerContainer.GetComponent<RectTransform>().offsetMin = new Vector2(20, 0);

        GameObject avatar = CreateRect("Avatar", playerContainer.transform);
        Place(avatar.GetComponent<RectTransform>(), 0, 0.5f, 50, 50, 0, 0, new Vector2(0, 0.5f));
        avatar.AddComponent<Image>(); // Placeholder

        TextMeshProUGUI pName = CreateText("PlayerName", playerContainer.transform, "Player_" + rank, Color.white, 22, TextAlignmentOptions.Left);
        Stretch(pName.rectTransform, 0, 0, 1, 1);
        pName.rectTransform.offsetMin = new Vector2(60, 0);
        
        // Games Won
        TextMeshProUGUI won = CreateText("Won", row.transform, (100 - rank * 3) + " GAMES WON", Color.white, 22, TextAlignmentOptions.Right);
        Stretch(won.rectTransform, 0.7f, 0f, 1.0f, 1f);
        won.rectTransform.offsetMax = new Vector2(-40, 0);
    }
}
