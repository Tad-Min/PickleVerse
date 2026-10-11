using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class BotMatchVictoryValidation
{
    static readonly List<object> checks=new List<object>();
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static void Hud(BotMatch m){typeof(MatchHud).GetMethod("Update",Hidden).Invoke(m.hud,null);}
    static Quaternion[] Pose(MatchActor actor)=>actor.visual.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).Select(t=>t.localRotation).ToArray();
    static bool Changed(Quaternion[] a,Quaternion[] b)=>a.Zip(b,(x,y)=>Quaternion.Angle(x,y)).Any(x=>x>.1f);
    static void Reset(BotMatch m,int server)
    {
        typeof(BotMatch).GetField("initialServer",Hidden).SetValue(m,server);m.Restart();m.autoPlayer=true;
    }
    static void Finish(BotMatch m,int winner,int end)
    {
        Reset(m,winner);
        m.human.end=end;m.bot.end=-end;
        m.human.ResetAt(new Vector3(1.45f,0,m.human.end*6.1f),m.human.end);m.bot.ResetAt(new Vector3(-1.45f,0,m.bot.end*6.1f),m.bot.end);
        m.Rules.Games[winner]=1;m.Rules.Points[winner]=10;m.Rules.Fail(1-winner,Fault.Out);
        typeof(BotMatch).GetMethod("FinishRally",Hidden).Invoke(m,null);Hud(m);
    }
    static void Click(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var r=(RectTransform)button.transform;
        var pos=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,r.TransformPoint(r.rect.center));
        var data=new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();
        canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(data,hits);
        Check(button.name+" receives UI clicks",hits.Count>0&&hits[0].gameObject==button.gameObject);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play BotMatch first.");checks.Clear();
        var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();if(!m)throw new Exception("BotMatch is not loaded.");
        bool auto=m.autoPlayer;string wallet=PlayerPrefs.GetString(PickleVerse.Shop.LocalEconomyStorage.Key,"");
        string settings=PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"");
        try
        {
            Reset(m,0);m.Rules.Points[0]=10;m.Rules.Fail(1,Fault.Out);typeof(BotMatch).GetMethod("FinishRally",Hidden).Invoke(m,null);Hud(m);
            Check("One game win does not start the winner view",m.Phase==MatchPhase.BetweenGames&&!m.victory.Active&&!m.hud.resultActions.activeSelf);
            foreach(int winner in new[]{0,1})foreach(int end in new[]{-1,1})
            {
                string tag=winner+"/"+end;Finish(m,winner,end);var v=m.victory;
                Check("Result always presents the local player "+tag,m.Phase==MatchPhase.MatchOver&&m.Rules.Winner==winner&&m.Rules.Games[winner]==2&&v.Winner==m.Actor(winner)&&v.PresentedActor==m.human&&v.PlayerWon==(winner==0));
                var camera=v.followCamera.GetComponent<Camera>();var heldPosition=camera.transform.position;var heldRotation=camera.transform.rotation;var playerPosition=m.human.transform.position;var opponentPosition=m.bot.transform.position;
                Check("Result waits with the gameplay scene intact "+tag,v.Active&&!v.Ready&&!v.TransitionStarted&&!m.hud.resultActions.activeSelf&&!v.followCamera.enabled&&m.ball.gameObject.activeSelf&&!v.portraitLight.enabled);
                v.Advance(10);m.hud.announcements.Advance(1.21f);v.Advance(10);
                Check("Camera and player remain still during the match banner "+tag,!v.TransitionStarted&&!v.Ready&&Vector3.Distance(heldPosition,camera.transform.position)<.001f&&Quaternion.Angle(heldRotation,camera.transform.rotation)<.01f&&Vector3.Distance(playerPosition,m.human.transform.position)<.001f);
                m.hud.announcements.Advance(2.0f);v.Advance(10);
                Check("Camera waits through the banner exit animation "+tag,m.hud.announcements.Busy&&!v.TransitionStarted&&Vector3.Distance(heldPosition,camera.transform.position)<.001f);
                m.hud.announcements.Advance(.3f);v.Advance(0);Hud(m);
                Check("Empty announcement queue starts the camera transition "+tag,!m.hud.announcements.Busy&&v.TransitionStarted&&!v.Ready&&Vector3.Distance(heldPosition,camera.transform.position)<.001f);
                v.Advance(v.cameraSeconds);Hud(m);
                Check("Victory view begins after announcements "+tag,v.Ready&&m.hud.resultActions.activeSelf&&v.DanceElapsed==0);
                Check("Vietnamese win or loss title "+tag,v.winnerTitle.text==(winner==0?"Thắng":"Thua")&&m.hud.resultSummary.text.StartsWith("BẠN"));
                Check("Player uses the appropriate result animation "+tag,winner==0?v.CurrentClip==v.femaleWin:v.CurrentClip.name=="PKV_Fault_Reaction");
                var first=Pose(v.PresentedActor);v.Advance(winner==0?.8f:.6f);
                Check("Player result animation moves "+tag,Changed(first,Pose(v.PresentedActor)));
                if(winner==1){v.Advance(2);var finalPose=Pose(v.PresentedActor);v.Advance(2);Check("Loss holds its final reaction without celebrating "+tag,!Changed(finalPose,Pose(v.PresentedActor))&&v.CurrentClip.name=="PKV_Fault_Reaction");}
                else{v.Advance(v.femaleWin.length+.4f);Check("Female repeats Dance Win "+tag,v.CurrentClip==v.femaleWin);}
                Check("Camera remains on the player's side "+tag,camera.transform.position.z*end>0&&v.PresentedActor.transform.position.z*end>4.7f);
                Check("Opponent stays in its original place "+tag,Vector3.Distance(opponentPosition,m.bot.transform.position)<.001f);
                var head=v.PresentedActor.visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Head");var point=camera.WorldToViewportPoint(head.position);
                Check("Local player is centered and visible "+tag,point.z>0&&point.x>.37f&&point.x<.63f&&point.y>.55f&&point.y<.95f);
                float dance=v.DanceElapsed;m.SetPaused(true);v.Advance(2);Check("Pause holds celebration "+tag,Mathf.Approximately(v.DanceElapsed,dance));m.SetPaused(false);
                if(winner==0){v.Advance(2.4f-Mathf.Repeat(v.DanceElapsed,2.4f)+.1f);float shine=v.sheenMask.anchoredPosition.x;v.Advance(.2f);Check("Win retains its golden light sweep "+tag,v.sheenMask.gameObject.activeSelf&&Mathf.Abs(shine-v.sheenMask.anchoredPosition.x)>1&&!v.defeatShade.gameObject.activeSelf&&v.winnerTitle.fontSharedMaterial==v.winTitleMaterial);}
                else{float darkness=v.defeatShade.color.a;v.Advance(.5f);Check("Loss is gray and dark without a light sweep "+tag,!v.sheenMask.gameObject.activeSelf&&v.defeatShade.gameObject.activeSelf&&v.winnerTitle.fontSharedMaterial==v.lossTitleMaterial&&Mathf.Abs(darkness-v.defeatShade.color.a)>.0001f&&v.winnerTitle.colorGradient.topLeft.r<.65f);}
                var stage=v.PresentedActor.transform.position;
                for(int i=0;i<60;i++){v.Advance(.3f);v.PresentedActor.visual.GetComponent<PickleVerse.CharacterMotion.CharacterSecondaryMotion>().Step(1f/60);}
                Check("Result stays on its stage with finite bones "+tag,Vector3.Distance(v.PresentedActor.transform.position,stage)<.001f&&v.PresentedActor.visual.GetComponentsInChildren<Transform>().All(t=>float.IsFinite(t.position.x)&&float.IsFinite(t.position.y)&&float.IsFinite(t.position.z)));
                Check("Only one camera and listener "+tag,UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1);
                m.Restart();Hud(m);
                Check("Restart restores gameplay presentation "+tag,!v.Active&&!v.Ready&&v.followCamera.enabled&&!v.portraitLight.enabled&&m.ball.gameObject.activeSelf&&m.human.paddle.activeSelf&&m.bot.paddle.activeSelf&&!m.hud.resultActions.activeSelf&&m.Phase==MatchPhase.PreServe);
            }
            Finish(m,1,1);m.Restart();Hud(m);Check("Restart during announcements restores gameplay safely",!m.victory.Active&&!m.victory.TransitionStarted&&m.victory.followCamera.enabled&&m.ball.gameObject.activeSelf&&!m.hud.announcements.Busy);
            Finish(m,0,1);m.hud.announcements.Advance(4);m.victory.Advance(0);m.victory.Advance(1.3f);m.victory.Advance(.5f);Hud(m);await Task.Delay(200);
            var actions=m.hud.resultActions.transform;
            Check("Player result UI contains only two actions",actions.GetComponentsInChildren<UnityEngine.UI.Button>().Length==2&&m.victory.winnerTitle.text=="Thắng");
            Canvas.ForceUpdateCanvases();Check("All result text fits",actions.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing&&t.font.HasCharacters(t.text,out uint[] missingCharacters,true,false)));
            var replay=actions.Find("Replay").GetComponent<UnityEngine.UI.Button>();Click(replay);await Task.Delay(200);
            Check("Replay starts the original entrance camera",m.Phase==MatchPhase.Cinematic&&m.opening.IsPlaying&&!m.victory.Active&&!m.hud.resultActions.activeSelf&&!m.victory.portraitLight.enabled);
            m.opening.Skip();await Task.Delay(100);Check("Skipping replay intro restores normal follow camera",m.Phase==MatchPhase.PreServe&&m.victory.followCamera.enabled&&m.ball.gameObject.activeSelf);
            Finish(m,1,-1);m.hud.announcements.Advance(4);m.victory.Advance(0);m.victory.Advance(1.3f);m.victory.Advance(.5f);Hud(m);await Task.Delay(200);
            Click(m.hud.resultActions.transform.Find("ChooseMode").GetComponent<UnityEngine.UI.Button>());await Task.Delay(500);
            Check("Choose mode opens mode selection",SceneManager.GetActiveScene().name=="ModeSelection"&&UnityEngine.Object.FindAnyObjectByType<BotMatchVictory>()==null);
            Check("Checks preserve real wallet and graphics",wallet==PlayerPrefs.GetString(PickleVerse.Shop.LocalEconomyStorage.Key,"")&&settings==PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,""));
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/BotMatchVictory");File.WriteAllText("../UnityWork/BotMatchVictory/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{if(m){m.autoPlayer=auto;m.Restart();}}
    }
}
