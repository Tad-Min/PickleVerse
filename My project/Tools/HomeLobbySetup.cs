using System;
using System.IO;
using System.Linq;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeLobbySetup
{
    const string MenuPath="Assets/Scenes/HomeScene/Menu.unity";
    static TMP_FontAsset font;
    static readonly Color Ink=ColorOf("0B1823"),Surface=ColorOf("152B35"),White=ColorOf("EFF7F5"),Muted=ColorOf("B2C7CE"),Mint=ColorOf("73DCC4"),Lime=ColorOf("C9F573");

    public static object Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var menu=SceneManager.GetActiveScene();
        if(menu.path!=MenuPath)throw new InvalidOperationException("Open Menu first.");
        if(menu.GetRootGameObjects().Any(g=>g.name=="Home Arena"))throw new InvalidOperationException("Home lobby already configured; edit existing objects in place.");
        var source=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity",OpenSceneMode.Additive);
        HomeLobbyPresentation lobby;
        try
        {
            var match=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BotMatch>(true)).Single();
            var environment=match.GetComponent<MatchEnvironmentLighting>();
            SceneManager.SetActiveScene(menu);
            var arena=UnityEngine.Object.Instantiate(source.GetRootGameObjects().Single(g=>g.name=="Arena"));
            arena.name="Home Arena";SceneManager.MoveGameObjectToScene(arena,menu);
            var stage=new GameObject("Home Female");SceneManager.MoveGameObjectToScene(stage,menu);
            stage.transform.SetPositionAndRotation(new Vector3(1.45f,0,-5.7f),Quaternion.Euler(0,180,0));
            stage.transform.localScale=Vector3.one*1.25f;
            var visual=UnityEngine.Object.Instantiate(match.human.visual.gameObject,stage.transform);visual.name="Character";
            visual.transform.localPosition=match.human.visual.localPosition;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
            foreach(var component in visual.GetComponentsInChildren<Animator>(true))component.enabled=false;
            foreach(var component in visual.GetComponentsInChildren<Animation>(true))component.enabled=false;
            foreach(var component in visual.GetComponentsInChildren<Collider>(true))component.enabled=false;
            var sun=UnityEngine.Object.Instantiate(environment.sun.gameObject);sun.name="Home Sun";SceneManager.MoveGameObjectToScene(sun,menu);
            var go=new GameObject("Home Lobby Presentation");SceneManager.MoveGameObjectToScene(go,menu);lobby=go.AddComponent<HomeLobbyPresentation>();
            lobby.femaleVisual=visual.transform;lobby.dance=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/LocalMatchContent/Clips/female/PKV_Dance_Win.anim");
            if(!lobby.dance)throw new InvalidOperationException("Dance Win clip missing.");
            lobby.daySky=environment.daySky;lobby.nightSky=environment.nightSky;lobby.lampsOff=environment.lampsOff;lobby.lampsOn=environment.lampsOn;
            lobby.sun=sun.GetComponent<Light>();
            lobby.floodlights=arena.GetComponentsInChildren<Light>(true).Where(l=>l.type==LightType.Spot).OrderBy(l=>l.name).ToArray();
            if(lobby.floodlights.Length!=4)throw new InvalidOperationException("Expected the four BotMatch corner floodlights.");
            lobby.lampFaces=arena.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="LampHousing").Select(t=>t.GetComponent<Renderer>()).ToArray();
            lobby.blendedSky=Sky(environment);
            lobby.cycleSeconds=90;
            var key=new GameObject("Home Portrait Light").AddComponent<Light>();key.transform.position=new Vector3(-.4f,2.6f,-7.8f);
            key.transform.LookAt(stage.transform.position+Vector3.up*1.1f);key.type=LightType.Spot;key.range=5;key.spotAngle=65;key.innerSpotAngle=35;
            key.intensity=2.5f;key.color=new Color(.91f,.96f,1);key.shadows=LightShadows.None;lobby.portraitLight=key;
            lobby.dance.SampleAnimation(visual,2.1f);
        }
        finally
        {
            EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(menu);
        }
        // Remove only the obsolete scene instances; keep their source prefabs/assets.
        foreach(var old in menu.GetRootGameObjects().Where(g=>g.name=="PickleballCourt"||g.name=="PickleballPaddle"||g.name=="Directional Light").ToArray())UnityEngine.Object.DestroyImmediate(old);
        var camera=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single();
        camera.transform.position=new Vector3(0,2.35f,-10.4f);camera.transform.LookAt(new Vector3(.05f,1.1f,-4.7f));camera.fieldOfView=50;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=160;
        StyleUI(menu,camera,lobby);
        PlaceCourtProps(lobby.femaleVisual);
        lobby.ApplyAtmosphere(0);EditorUtility.SetDirty(lobby);
        EditorSceneManager.MarkSceneDirty(menu);EditorSceneManager.SaveScene(menu);
        return new{scene=menu.path,arenaRenderers=GameObject.Find("Home Arena").GetComponentsInChildren<Renderer>().Length,dance=lobby.dance.name,duration=lobby.dance.length,lights=lobby.floodlights.Length};
    }

    public static object UpdateProps()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=MenuPath)throw new InvalidOperationException("Open Menu first.");
        var lobby=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        PlaceCourtProps(lobby.femaleVisual);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{handsEmpty=!lobby.femaleVisual.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.ToLower().Contains("paddle")),props=GameObject.Find("Home Court Props").transform.childCount};
    }

    static void PlaceCourtProps(Transform visual)
    {
        var props=GameObject.Find("Home Court Props")??new GameObject("Home Court Props");
        var paddle=visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Paddle"&&t.GetComponent<Renderer>());
        if(paddle)
        {
            Vector3 scale=paddle.lossyScale;
            paddle.SetParent(props.transform,false);paddle.localScale=scale;paddle.name="Home Paddle";
        }
        else paddle=props.transform.Find("Home Paddle");
        if(!paddle)throw new InvalidOperationException("Home paddle is missing.");
        paddle.SetPositionAndRotation(new Vector3(.25f,.1f,-5f),Quaternion.Euler(0,65,0));
        var paddleBounds=paddle.GetComponent<Renderer>().bounds;
        paddle.position+=Vector3.up*(.003f-paddleBounds.min.y);
        foreach(var marker in paddle.GetComponentsInChildren<Transform>(true).Where(t=>t!=paddle&&t.name=="Paddle Contact").ToArray())UnityEngine.Object.DestroyImmediate(marker.gameObject);
        var existing=props.transform.Find("Home Ball");
        var ball=existing?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LocalMatchContent/Models/Ball.glb"));
        ball.name="Home Ball";ball.transform.SetParent(props.transform,false);
        ball.transform.SetPositionAndRotation(new Vector3(.62f,.1f,-5.08f),Quaternion.identity);ball.transform.localScale=Vector3.one*.0555f;
        var renderers=ball.GetComponentsInChildren<Renderer>(true);
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        ball.transform.position+=Vector3.up*(.003f-bounds.min.y);
        foreach(var collider in props.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var light in props.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
        foreach(var camera in props.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera);
    }

    static Material Sky(MatchEnvironmentLighting source)
    {
        const string path="Assets/FrontEndContent/Home Sky Blend.mat";
        var shader=Shader.Find("PickleVerse/Home/Panoramic Blend");if(!shader)throw new InvalidOperationException("Home panoramic shader did not compile.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_DayTex",source.daySky.GetTexture("_MainTex"));material.SetTexture("_NightTex",source.nightSky.GetTexture("_MainTex"));
        material.SetFloat("_DayExposure",1);material.SetFloat("_NightExposure",.7f);material.SetFloat("_NightBlend",0);material.SetFloat("_DayRotation",0);material.SetFloat("_NightRotation",35);
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
    }

    static void StyleUI(Scene menu,Camera camera,HomeLobbyPresentation lobby)
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
        var canvas=menu.GetRootGameObjects().Single(g=>g.name=="CanvasHome").GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.8f;
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var root=(RectTransform)canvas.transform;
        var safe=NewRect("HomeSafeArea",root,Vector2.zero,Vector2.one);lobby.safeArea=safe;
        var head=(RectTransform)root.Find("Head");head.SetParent(safe,false);Anchors(head,new Vector2(0,.86f),Vector2.one);
        head.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled=false;
        var backdrop=NewBlock("HeaderBackdrop",head,Vector2.zero,Vector2.one,new Color(.025f,.065f,.095f,.88f));backdrop.SetAsFirstSibling();
        var name=(RectTransform)head.Find("NameTag");Anchors(name,new Vector2(.04f,.18f),new Vector2(.28f,.86f));
        var nameBackground=name.GetComponent<UnityEngine.UI.Image>();if(nameBackground){nameBackground.color=new Color(.075f,.16f,.20f,.85f);nameBackground.raycastTarget=false;}
        var avatar=(RectTransform)name.Find("ImgAvatar");Anchors(avatar,new Vector2(.04f,.18f),new Vector2(.205f,.82f));
        var avatarImage=avatar.GetComponent<UnityEngine.UI.Image>();avatarImage.color=Mint;avatarImage.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");avatarImage.raycastTarget=false;avatarImage.preserveAspect=true;
        Text("Initial",avatar,"P",new Vector2(.05f,0),new Vector2(.95f,1),25,Ink,TextAlignmentOptions.Center,FontStyles.Bold);
        Style(name.Find("TxtName").GetComponent<TMP_Text>(),"Người chơi",new Vector2(.26f,.43f),new Vector2(.97f,.91f),20,White,TextAlignmentOptions.Left);
        Style(name.Find("TxtLevel").GetComponent<TMP_Text>(),"PICKLEVERSE LOCAL",new Vector2(.26f,.08f),new Vector2(.97f,.43f),11,Mint,TextAlignmentOptions.Left);
        name.Find("ImgRankIcon").gameObject.SetActive(false);
        var currency=(RectTransform)head.Find("Currency");Anchors(currency,new Vector2(.76f,.24f),new Vector2(.955f,.74f));
        currency.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled=false;
        var currencyImage=currency.GetComponent<UnityEngine.UI.Image>();if(currencyImage){currencyImage.color=new Color(.08f,.18f,.23f,.9f);currencyImage.raycastTarget=false;}
        currency.Find("ImgCurrency").gameObject.SetActive(false);
        lobby.timeLabel=currency.Find("TxtCurrencyNumber").GetComponent<TMP_Text>();Style(lobby.timeLabel,"BAN NGÀY",new Vector2(.04f,0),new Vector2(.96f,1),16,Mint,TextAlignmentOptions.Center);
        Text("Brand",head,"PICKLEVERSE",new Vector2(.35f,.25f),new Vector2(.68f,.76f),25,White,TextAlignmentOptions.Center,FontStyles.Bold);
        var body=(RectTransform)root.Find("Body");body.SetParent(safe,false);Anchors(body,new Vector2(.045f,.16f),new Vector2(.955f,.83f));
        var bodyImage=body.GetComponent<UnityEngine.UI.Image>();bodyImage.color=Color.clear;bodyImage.raycastTarget=false;
        // Retain unfinished screen hierarchies, but keep them outside the Home view.
        foreach(Transform child in body)child.gameObject.SetActive(false);
        var nav=(RectTransform)root.Find("Nav");nav.SetParent(safe,false);Anchors(nav,new Vector2(.045f,.035f),new Vector2(.955f,.115f));
        var navBackground=NewBlock("NavBackdrop",safe,new Vector2(0,0),new Vector2(1,.143f),new Color(.025f,.065f,.095f,.94f));navBackground.SetAsFirstSibling();
        var welcome=NewBlock("WelcomeCard",body,new Vector2(0,.16f),new Vector2(.375f,.92f),new Color(.025f,.08f,.115f,.91f));
        Text("Eyebrow",welcome,"SÂN ĐẤU CỦA BẠN",new Vector2(.075f,.825f),new Vector2(.925f,.91f),14,Mint,TextAlignmentOptions.Left,FontStyles.Bold);
        Text("WelcomeTitle",welcome,"Chào mừng\nđến PickleVerse",new Vector2(.075f,.51f),new Vector2(.925f,.80f),34,White,TextAlignmentOptions.Left,FontStyles.Bold);
        Text("WelcomeBody",welcome,"Luyện kỹ thuật và tìm nhịp đấu của bạn.\nChọn chế độ để bắt đầu.",new Vector2(.075f,.325f),new Vector2(.925f,.50f),17,Muted,TextAlignmentOptions.TopLeft);
        var play=nav.Find("BtnPlay").GetComponent<UnityEngine.UI.Button>();play.transform.SetParent(welcome,false);lobby.playButton=play;
        var playLayout=play.GetComponent<UnityEngine.UI.LayoutElement>();if(playLayout)playLayout.ignoreLayout=true;
        Anchors((RectTransform)play.transform,new Vector2(.075f,.135f),new Vector2(.925f,.295f));
        StyleButton(play,Lime,Ink,"CHƠI NGAY  →",22);play.interactable=true;
        Text("PlayHint",welcome,"Chọn chế độ chơi  /  Enter",new Vector2(.075f,.035f),new Vector2(.925f,.115f),13,Muted,TextAlignmentOptions.Center);
        var layout=nav.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.padding=new RectOffset(0,0,0,0);layout.spacing=12;
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;
        string[] keys={"BtnHome","BtnProgression","BtnCareer","BtnCollection","BtnStore","BtnSetting"};
        string[] labels={"Trang chủ","Tiến trình","Sự nghiệp","Bộ sưu tập","Cửa hàng","Cài đặt"};
        for(int i=0;i<keys.Length;i++)
        {
            var b=nav.Find(keys[i]).GetComponent<UnityEngine.UI.Button>();
            var element=b.GetComponent<UnityEngine.UI.LayoutElement>()??b.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();element.flexibleWidth=1;element.minHeight=48;element.minWidth=70;
            StyleButton(b,i==0?new Color(.11f,.27f,.29f):new Color(.065f,.13f,.17f),i==0?Mint:new Color(.50f,.63f,.67f),labels[i],17);
            b.interactable=i==0||b.onClick.GetPersistentEventCount()>0;
        }
        Text("PendingFeatures",safe,"Các mục khác đang được phát triển",new Vector2(.49f,.145f),new Vector2(.96f,.18f),12,Muted,TextAlignmentOptions.Right);
        // Keep interactive graphics on top of their non-interactive decoration.
        nav.SetAsLastSibling();body.SetAsLastSibling();head.SetAsLastSibling();
        EditorUtility.SetDirty(canvas);EditorUtility.SetDirty(scaler);
    }

    static void StyleButton(UnityEngine.UI.Button button,Color background,Color foreground,string value,float size)
    {
        var image=button.GetComponent<UnityEngine.UI.Image>();image.sprite=null;image.color=background;image.raycastTarget=true;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.12f,1.12f);colors.selectedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.83f,.90f,.87f);colors.disabledColor=Color.white;colors.fadeDuration=.12f;button.colors=colors;
        var text=button.GetComponentInChildren<TMP_Text>(true);Style(text,value,new Vector2(.035f,0),new Vector2(.965f,1),size,foreground,TextAlignmentOptions.Center);text.fontStyle=FontStyles.Bold;
    }
    static void Style(TMP_Text text,string value,Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment)
    {
        Anchors(text.rectTransform,min,max);text.font=font;text.text=value;text.fontSize=size;text.enableAutoSizing=false;text.color=color;text.alignment=alignment;text.raycastTarget=false;
    }
    static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment,FontStyles style=FontStyles.Normal)
    {
        var rect=NewRect(name,parent,min,max);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();Style(text,value,min,max,size,color,alignment);text.fontStyle=style;return text;
    }
    static RectTransform NewRect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);Anchors(rect,min,max);return rect;
    }
    static RectTransform NewBlock(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rect=NewRect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
    }
    static void Anchors(RectTransform rect,Vector2 min,Vector2 max)
    {
        rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;
    }
    static Color ColorOf(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
}
