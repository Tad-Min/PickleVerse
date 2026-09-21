using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UI;

public class GenerateCollectionUIPrefab
{
    private static Color ColorNavyDark = new Color(0.05f, 0.1f, 0.2f, 1f);
    private static Color ColorNavyBg = new Color(0.05f, 0.1f, 0.2f, 0.6f);
    private static Color ColorCyan = new Color(0.0f, 1.0f, 1.0f, 1.0f);
    private static Color ColorCream = new Color(0.95f, 0.95f, 0.9f, 1f);
    private static Color ColorGrey = new Color(0.6f, 0.6f, 0.6f, 1.0f);
    private static Color ColorDarkTrans = new Color(0.1f, 0.1f, 0.15f, 0.6f);
    private static Color ColorYellow = new Color(0.8f, 0.8f, 0.2f, 1.0f);

    [MenuItem("Tools/Generate Collection Component UI")]
    public static void Generate()
    {
        GameObject root = new GameObject("CollectionComponent", typeof(RectTransform));
        Stretch(root.GetComponent<RectTransform>(), 0, 0, 1, 1);

        // Sub-container to match the ContentArea margins of Career
        GameObject contentArea = CreateRect("ContentArea", root.transform);
        Stretch(contentArea.GetComponent<RectTransform>(), 0.1f, 0.1f, 0.9f, 0.9f);

        GameObject leftPanel = CreateRect("LeftPanel", contentArea.transform);
        RectTransform leftRt = leftPanel.GetComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0, 0); leftRt.anchorMax = new Vector2(0, 1);
        leftRt.pivot = new Vector2(0, 0.5f);
        leftRt.sizeDelta = new Vector2(420, 0);
        leftRt.anchoredPosition = Vector2.zero;

        GameObject rightPanel = CreateRect("RightPanel", contentArea.transform);
        RectTransform rightRt = rightPanel.GetComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0, 0); rightRt.anchorMax = new Vector2(1, 1);
        rightRt.offsetMin = new Vector2(460, 0); // 420 (left panel) + 40 (gap)
        rightRt.offsetMax = new Vector2(0, 0);

        CreateLeftPanel(leftPanel.transform);
        CreateRightPanel(rightPanel.transform);

        string path = "Assets/Prefabs/UI/CollectionComponent.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        Debug.Log("Saved CollectionComponent Prefab at " + path);
    }

    private static void CreateLeftPanel(Transform parent)
    {
        // Top Filter Bar (SHOW UNOWNED)
        GameObject unownedBtn = CreateRect("BtnShowUnowned", parent);
        RectTransform unownedRt = unownedBtn.GetComponent<RectTransform>();
        unownedRt.anchorMin = new Vector2(0, 1); unownedRt.anchorMax = new Vector2(1, 1);
        unownedRt.pivot = new Vector2(0.5f, 1);
        unownedRt.offsetMin = new Vector2(20, -70); unownedRt.offsetMax = new Vector2(-20, -20); // Height 50, top/left/right pad 20
        Image unownedBg = unownedBtn.AddComponent<Image>();
        unownedBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        unownedBtn.AddComponent<Button>();

        TextMeshProUGUI unownedTxt = CreateText("Text", unownedBtn.transform, "SHOW UNOWNED", ColorGrey, 20, TextAlignmentOptions.Left);
        Stretch(unownedTxt.rectTransform, 0, 0, 1, 1);
        unownedTxt.rectTransform.offsetMin = new Vector2(20, 0);

        GameObject unownedIcon = CreateRect("Icon", unownedBtn.transform);
        Place(unownedIcon.GetComponent<RectTransform>(), 1, 0.5f, 30, 30, -25, 0, new Vector2(1, 0.5f));
        Image uiImg = unownedIcon.AddComponent<Image>();
        uiImg.color = ColorGrey;

        // Icon Filters
        GameObject filterRow = CreateRect("FilterRow", parent);
        RectTransform filterRt = filterRow.GetComponent<RectTransform>();
        filterRt.anchorMin = new Vector2(0, 1); filterRt.anchorMax = new Vector2(1, 1);
        filterRt.pivot = new Vector2(0.5f, 1);
        filterRt.offsetMin = new Vector2(20, -150); filterRt.offsetMax = new Vector2(-20, -90); // Height 60, below unownedBtn (70+20=90)
        
        HorizontalLayoutGroup hlg = filterRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 15;

        for (int i = 0; i < 5; i++)
        {
            GameObject filterIcon = CreateRect("FilterIcon_" + i, filterRow.transform);
            filterIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);
            Image img = filterIcon.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            if (i == 0) // ALL is selected
            {
                Outline outline = filterIcon.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(2, -2);
            }
        }

        // Grid Area
        GameObject scroll = CreateRect("ScrollView", parent);
        RectTransform scrollRt = scroll.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(20, 20); scrollRt.offsetMax = new Vector2(-20, -170); // 20px from bottom, 170px from top (150+20)
        
        ScrollRect sr = scroll.AddComponent<ScrollRect>();
        sr.horizontal = false;
        
        GameObject viewport = CreateRect("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 1, 1);
        viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-15, 0); // space for scrollbar
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateRect("Content", viewport.transform);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        Stretch(rtContent, 0, 1, 1, 1); // anchor top
        rtContent.pivot = new Vector2(0.5f, 1);
        
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 110);
        grid.spacing = new Vector2(15, 15);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.MinSize;

        sr.content = rtContent;
        sr.viewport = viewport.GetComponent<RectTransform>();

        // Scrollbar (White bar on the right)
        GameObject scrollbar = CreateRect("Scrollbar", scroll.transform);
        RectTransform sbRt = scrollbar.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1, 0); sbRt.anchorMax = new Vector2(1, 1);
        sbRt.pivot = new Vector2(1, 0.5f);
        sbRt.offsetMin = new Vector2(-6, 0); sbRt.offsetMax = new Vector2(0, 0);
        Image sbImg = scrollbar.AddComponent<Image>();
        sbImg.color = new Color(0.2f, 0.2f, 0.2f, 1);

        GameObject handle = CreateRect("Handle", scrollbar.transform);
        Stretch(handle.GetComponent<RectTransform>(), 0, 0.6f, 1, 1);
        Image hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;

        // Generate Grid Items
        for (int i = 0; i < 20; i++)
        {
            GameObject item = CreateRect("Item_" + i, content.transform);
            Image img = item.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark item bg

            Outline outline = item.AddComponent<Outline>();
            if (i == 0)
            {
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(4, -4);
            }
            else
            {
                outline.effectColor = ColorGrey;
                outline.effectDistance = new Vector2(1, -1);
            }
        }
    }

    private static void CreateRightPanel(Transform parent)
    {
        GameObject bg = CreateRect("Background", parent);
        Stretch(bg.GetComponent<RectTransform>(), 0, 0, 1, 1);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = ColorNavyBg;

        // Top Title Area
        GameObject titleArea = CreateRect("TitleArea", parent);
        RectTransform titleRt = titleArea.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.offsetMin = new Vector2(40, -100); titleRt.offsetMax = new Vector2(-40, -40); // 40px padding from top/left/right

        GameObject rarityIcon = CreateRect("RarityIcon", titleArea.transform);
        Place(rarityIcon.GetComponent<RectTransform>(), 0, 0.5f, 50, 50, 0, 0, new Vector2(0, 0.5f));
        Image rarityImg = rarityIcon.AddComponent<Image>();
        rarityImg.color = ColorYellow;

        TextMeshProUGUI title = CreateText("SkinName", titleArea.transform, "TÊN SKIN", Color.white, 48, TextAlignmentOptions.Left);
        Stretch(title.rectTransform, 0, 0, 1, 1);
        title.rectTransform.offsetMin = new Vector2(70, 0); // Spacing after icon
        title.fontStyle = FontStyles.Bold;

        // Action Area
        GameObject actionArea = CreateRect("ActionArea", parent);
        RectTransform actionRt = actionArea.GetComponent<RectTransform>();
        actionRt.anchorMin = new Vector2(0, 0); actionRt.anchorMax = new Vector2(1, 0);
        actionRt.pivot = new Vector2(0.5f, 0);
        actionRt.offsetMin = new Vector2(40, 40); actionRt.offsetMax = new Vector2(-40, 120); // Height 80, bottom padding 40

        // Favorite Button
        GameObject favBtnObj = CreateRect("BtnFavorite", actionArea.transform);
        Place(favBtnObj.GetComponent<RectTransform>(), 1, 0.5f, 70, 70, -290, 0, new Vector2(1, 0.5f));
        Image favBg = favBtnObj.AddComponent<Image>();
        favBg.color = ColorNavyDark;
        Outline favOutline = favBtnObj.AddComponent<Outline>();
        favOutline.effectColor = new Color(0.7f, 0.8f, 0.8f, 1f);
        favOutline.effectDistance = new Vector2(1, -1);
        
        GameObject starIcon = CreateRect("StarIcon", favBtnObj.transform);
        Stretch(starIcon.GetComponent<RectTransform>(), 0.25f, 0.25f, 0.75f, 0.75f);
        Image starImg = starIcon.AddComponent<Image>();
        starImg.color = ColorCream;

        // Equip Button
        GameObject equipBtnObj = CreateRect("BtnEquipState", actionArea.transform);
        Place(equipBtnObj.GetComponent<RectTransform>(), 1, 0.5f, 260, 70, 0, 0, new Vector2(1, 0.5f));
        
        Image equipBg = equipBtnObj.AddComponent<Image>();
        equipBtnObj.AddComponent<Button>();

        GameObject borderObj = CreateRect("Border", equipBtnObj.transform);
        Stretch(borderObj.GetComponent<RectTransform>(), 0, 0, 1, 1);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.type = Image.Type.Sliced;
        
        TextMeshProUGUI equipTxt = CreateText("Text", equipBtnObj.transform, "EQUIP", ColorNavyDark, 28, TextAlignmentOptions.Center);
        Stretch(equipTxt.rectTransform, 0, 0, 1, 1);
        equipTxt.fontStyle = FontStyles.Bold;

        GameObject handleL = CreateRect("HandleLeft", equipBtnObj.transform);
        Place(handleL.GetComponent<RectTransform>(), 0, 0.5f, 6, 24, -3, 0, new Vector2(0.5f, 0.5f));
        handleL.AddComponent<Image>();

        GameObject handleR = CreateRect("HandleRight", equipBtnObj.transform);
        Place(handleR.GetComponent<RectTransform>(), 1, 0.5f, 6, 24, 3, 0, new Vector2(0.5f, 0.5f));
        handleR.AddComponent<Image>();

        UIGradient grad = equipBtnObj.AddComponent<UIGradient>();
        grad.enabled = false;
        Outline outl = equipBtnObj.AddComponent<Outline>();
        outl.enabled = false;
        outl.effectDistance = new Vector2(4, -4);

        EquipButtonBehavior behavior = equipBtnObj.AddComponent<EquipButtonBehavior>();
        behavior.background = equipBg;
        behavior.border = borderImg;
        behavior.text = equipTxt;
        behavior.hoverGradient = grad;
        behavior.hoverOutline = outl;
        behavior.button = equipBtnObj.GetComponent<Button>();
        behavior.decorativeHandles = new GameObject[] { handleL, handleR };
        
        behavior.currentState = EquipButtonBehavior.EquipState.Equip;
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
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
