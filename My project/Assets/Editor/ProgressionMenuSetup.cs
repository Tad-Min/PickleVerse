using System;
using System.Linq;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PickleVerse.EditorTools
{
    public static class ProgressionMenuSetup
    {
        [MenuItem("PickleVerse/Progression/Configure Menu")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play trước khi cấu hình Menu.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/UI/Menu.unity") scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
            var canvas=scene.GetRootGameObjects().Single(g=>g.name=="CanvasHome").GetComponent<Canvas>();
            var safe=canvas.transform.Find("HomeSafeArea");
            var menu=canvas.GetComponent<PlayerProgressionMenu>()??canvas.gameObject.AddComponent<PlayerProgressionMenu>();
            menu.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
            menu.rounded=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FrontEndContent/Multiplayer/RoundedFill.png");
            var head=safe.Find("Head"); var name=head.Find("NameTag");
            menu.avatar=name.Find("ImgAvatar").GetComponent<Image>().sprite;
            menu.menuName=name.Find("TxtName").GetComponent<TMP_Text>(); menu.menuLevel=name.Find("TxtLevel").GetComponent<TMP_Text>();
            menu.profileButton=name.GetComponent<Button>()??name.gameObject.AddComponent<Button>(); menu.profileButton.targetGraphic=name.GetComponent<Image>();
            menu.profileButton.targetGraphic.raycastTarget=true;
            foreach(var t in name.GetComponentsInChildren<TMP_Text>(true)) t.raycastTarget=false;
            if(!menu.wallet) menu.wallet=menu.Text("Wallet",head,"TOKENPAD  0    ELITE TOKEN  0",16,Color.white,new Vector2(.33f,.25f),new Vector2(.59f,.77f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            menu.wallet.gameObject.SetActive(!canvas.GetComponentInChildren<PickleVerse.Shop.WalletBar>(true));
            menu.wallet.rectTransform.anchorMin=new Vector2(.33f,.08f); menu.wallet.rectTransform.anchorMax=new Vector2(.59f,.34f);
            menu.wallet.rectTransform.offsetMin=menu.wallet.rectTransform.offsetMax=Vector2.zero;
            if(!menu.editButton) menu.editButton=menu.MakeButton("BtnEditPlayer",head,"CHỈNH PLAYER",()=>{},new Vector2(.60f,.27f),new Vector2(.735f,.77f),new Color(.075f,.16f,.20f),new Color(.45f,.86f,.77f),13);
            var welcome=safe.Find("Body/WelcomeCard"); var play=(RectTransform)welcome.Find("BtnPlay");
            play.anchorMin=new Vector2(.075f,.10f); play.anchorMax=new Vector2(.625f,.27f); play.offsetMin=play.offsetMax=Vector2.zero;
            if(!menu.missionsButton)
            {
                menu.missionsButton=menu.MakeButton("BtnMissions",welcome,"NHIỆM VỤ",()=>{},new Vector2(.645f,.10f),new Vector2(.925f,.27f),new Color(.075f,.16f,.20f),new Color(.79f,.96f,.45f),12);
                var label=menu.missionsButton.GetComponentInChildren<TMP_Text>().rectTransform; label.anchorMax=new Vector2(1,.42f);
            }
            var oldIcon=menu.missionsButton.transform.Find("Clipboard"); if(oldIcon) UnityEngine.Object.DestroyImmediate(oldIcon.gameObject);
            var oldCheck=menu.missionsButton.transform.Find("Check"); if(oldCheck) UnityEngine.Object.DestroyImmediate(oldCheck.gameObject);
            Clipboard(menu,menu.missionsButton.transform);
            menu.careerButton=safe.Find("Nav/BtnCareer").GetComponent<Button>();
            for(int i=menu.careerButton.onClick.GetPersistentEventCount()-1;i>=0;i--) UnityEventTools.RemovePersistentListener(menu.careerButton.onClick,i);
            menu.careerButton.interactable=true;
            var caption=menu.careerButton.GetComponentInChildren<TMP_Text>(); caption.text="Lịch sử đấu"; caption.color=new Color(.45f,.86f,.77f);
            if(!menu.modal) menu.BuildShell();
            var face=menu.left.Find("Avatar"); face.GetComponent<Image>().color=name.Find("ImgAvatar").GetComponent<Image>().color;
            if(!face.Find("Initial")) menu.Text("Initial",face,"P",34,new Color(.025f,.065f,.095f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            const string configPath="Assets/Resources/Progression Config.asset";
            if(!AssetDatabase.LoadAssetAtPath<ProgressionConfig>(configPath)) AssetDatabase.CreateAsset(ProgressionConfig.Defaults(),configPath);
            EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Đã cấu hình Profile, Missions, Career và player cục bộ trên Menu.");
        }
        static void Clipboard(PlayerProgressionMenu menu,Transform parent)
        {
            var book=menu.Panel("Clipboard",parent,new Color(.94f,.97f,.96f),new Vector2(.38f,.48f),new Vector2(.62f,.93f)); book.localRotation=Quaternion.Euler(0,0,9);
            menu.Panel("Clip",book,new Color(.45f,.86f,.77f),new Vector2(.25f,.83f),new Vector2(.75f,1.06f));
            for(int i=0;i<3;i++) menu.Panel("Line"+i,book,new Color(.08f,.19f,.23f),new Vector2(.17f,.18f+i*.19f),new Vector2(.62f,.23f+i*.19f));
            var check=menu.Rect("Check",parent,new Vector2(.53f,.51f),new Vector2(.70f,.90f));
            var a=menu.Panel("Short",check,new Color(.79f,.96f,.45f),new Vector2(.08f,.13f),new Vector2(.52f,.32f)); a.localRotation=Quaternion.Euler(0,0,-42);
            var b=menu.Panel("Long",check,new Color(.79f,.96f,.45f),new Vector2(.40f,.30f),new Vector2(.99f,.49f)); b.localRotation=Quaternion.Euler(0,0,48);
        }
    }
}
