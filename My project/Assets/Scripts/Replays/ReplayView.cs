using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
namespace PickleVerse.Replays
{
    public static class ReplayLaunch
    {
        public static string PlayerId,MatchId;
        public static Action Return;
        public static void Select(string player,string match,Action onReturn=null){PlayerId=player;MatchId=match;Return=onReturn;}
        public static void Close(){var callback=Return;Return=null;PlayerId=MatchId=null;if(callback!=null)callback();else SceneManager.LoadScene("Menu");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){PlayerId=MatchId=null;Return=null;}
    }
    public sealed class ReplayView : MonoBehaviour
    {
        ReplayMetadata metadata;
        IReplayRepository repository;
        Transform[] actors,paddles;
        Transform[][] bones;
        ReplayChunk current,next;
        int chunkIndex=-1;
        double playhead;
        float speed=1;
        bool playing;
        TMP_Text label,playLabel;
        Slider seek;
        public double Playhead => playhead;
        public float PlaybackSpeed => speed;
        public bool IsPlaying => playing;
        public int CachedFrames => (current?.frames.Count??0)+(next?.frames.Count??0);
        void Start()
        {
            BuildControls();repository=ReplayStorage.Repository;
            try
            {
                metadata=repository.Read(ReplayLaunch.PlayerId,ReplayLaunch.MatchId);
                GetComponent<ReplayEnvironment>()?.Apply(metadata?.night??false);
                if(metadata==null||metadata.chunks.Count==0)throw new InvalidDataException("Chưa có dữ liệu replay cho trận này.");
                if(metadata.actors.Count<2||metadata.actors.Count>4)throw new InvalidDataException("Đội hình replay không hợp lệ.");
                actors=new Transform[metadata.actors.Count];paddles=new Transform[actors.Length];bones=new Transform[actors.Length][];
                for(int i=0;i<actors.Length;i++)
                {
                    var info=metadata.actors[i];var prefab=Resources.Load<GameObject>("ReplayActors/"+info.visualId);
                    if(!prefab)throw new InvalidDataException("Thiếu hình nhân vật replay: "+info.visualId);
                    actors[i]=Instantiate(prefab,transform).transform;actors[i].name="Replay / "+info.name;
                    paddles[i]=ReplayRecorder.Find(actors[i],info.paddlePath);
                    if(paddles[i]&&!string.IsNullOrEmpty(info.paddleId)&&info.paddleId!="paddle.default")
                    {
                        var skin=Resources.Load<GameObject>("ReplayEquipment/"+info.paddleId);
                        if(skin)
                        {
                            foreach(var renderer in paddles[i].GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                            var instance=Instantiate(skin,paddles[i],false);instance.name=info.skinName;
                        }
                    }
                    bones[i]=new Transform[info.bones.Length];for(int b=0;b<bones[i].Length;b++)bones[i][b]=ReplayRecorder.Find(actors[i],info.bones[b]);
                }
                seek.maxValue=(float)metadata.duration;seek.interactable=true;Seek(0);playing=true;
            }
            catch(Exception e){Error(e);}
        }
        void Update()
        {
            if(!playing||metadata==null)return;
            Seek(Math.Min(metadata.duration,playhead+Time.unscaledDeltaTime*speed));
            if(playhead>=metadata.duration){playing=false;playLabel.text="PHÁT";}
        }
        public void Seek(double time)
        {
            if(metadata==null)return;
            try
            {
                playhead=Math.Max(0,Math.Min(time,metadata.duration));int index=0;
                for(int i=1;i<metadata.chunks.Count&&metadata.chunks[i].start<=playhead;i++)index=i;
                if(index!=chunkIndex)
                {
                    current=index==chunkIndex+1&&next!=null?next:repository.ReadChunk(metadata,metadata.chunks[index]);
                    next=index+1<metadata.chunks.Count?repository.ReadChunk(metadata,metadata.chunks[index+1]):null;chunkIndex=index;
                }
                int lower=0;while(lower+1<current.frames.Count&&current.frames[lower+1].time<=playhead)lower++;
                var a=current.frames[lower];var b=lower+1<current.frames.Count?current.frames[lower+1]:next?.frames[0]??a;
                Validate(a);Validate(b);
                float t=b.discontinuity||b.time<=a.time?0:Mathf.Clamp01((float)((playhead-a.time)/(b.time-a.time)));
                for(int i=0;i<actors.Length;i++)
                {
                    ApplyPose(actors[i],a.actors[i].root,b.actors[i].root,0,t,true);
                    for(int j=0;j<bones[i].Length;j++)if(bones[i][j])ApplyPose(bones[i][j],a.actors[i].bones,b.actors[i].bones,j*10,t,false);
                    if(paddles[i])paddles[i].gameObject.SetActive(a.actors[i].paddleVisible);
                }
                var ballObject=transform.Find("Ball");if(ballObject)
                {
                    ballObject.position=Vector3.Lerp(new Vector3(a.ball[0],a.ball[1],a.ball[2]),new Vector3(b.ball[0],b.ball[1],b.ball[2]),t);
                    if(a.ballRotation?.Length==4&&b.ballRotation?.Length==4)ballObject.rotation=Quaternion.Slerp(new Quaternion(a.ballRotation[0],a.ballRotation[1],a.ballRotation[2],a.ballRotation[3]),new Quaternion(b.ballRotation[0],b.ballRotation[1],b.ballRotation[2],b.ballRotation[3]),t);
                    if(a.ballScale?.Length==3&&b.ballScale?.Length==3)ballObject.localScale=Vector3.Lerp(new Vector3(a.ballScale[0],a.ballScale[1],a.ballScale[2]),new Vector3(b.ballScale[0],b.ballScale[1],b.ballScale[2]),t);
                }
                label.text=$"REPLAY{(metadata.complete?"":" · CHƯA HOÀN TẤT")}   {playhead:0.0} / {metadata.duration:0.0}s · {speed:0.0}×\n{a.scoreA} – {a.scoreB} điểm · {a.gamesA} – {a.gamesB} game\n{a.status}";
                seek.SetValueWithoutNotify((float)playhead);
            }
            catch(Exception e){Error(e);}
        }
        void Validate(ReplayFrame f)
        {
            if(f.ball==null||f.ball.Length!=3||f.actors==null||f.actors.Length!=actors.Length)throw new InvalidDataException("Snapshot replay không hợp lệ.");
            for(int i=0;i<actors.Length;i++)if(f.actors[i]?.root?.Length!=10||f.actors[i].bones==null||f.actors[i].bones.Length!=bones[i].Length*10)throw new InvalidDataException("Pose replay không hợp lệ.");
        }
        static void ApplyPose(Transform target,float[] a,float[] b,int offset,float t,bool world)
        {
            var p=Vector3.Lerp(new Vector3(a[offset],a[offset+1],a[offset+2]),new Vector3(b[offset],b[offset+1],b[offset+2]),t);
            var q=Quaternion.Slerp(new Quaternion(a[offset+3],a[offset+4],a[offset+5],a[offset+6]),new Quaternion(b[offset+3],b[offset+4],b[offset+5],b[offset+6]),t);
            if(world)target.SetPositionAndRotation(p,q);else{target.localPosition=p;target.localRotation=q;}
            target.localScale=Vector3.Lerp(new Vector3(a[offset+7],a[offset+8],a[offset+9]),new Vector3(b[offset+7],b[offset+8],b[offset+9]),t);
        }
        void Error(Exception e){playing=false;if(label)label.text="Không thể xem replay\n"+e.Message;if(seek)seek.interactable=false;Debug.LogWarning("Replay: "+e.Message);}
        RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        TMP_Text Text(string name,Transform parent,string text,Vector2 min,Vector2 max,int size=20)
        {
            var rect=Rect(name,parent,min,max);var tmp=rect.gameObject.AddComponent<TextMeshProUGUI>();tmp.font=TMP_Settings.defaultFontAsset;tmp.text=text;tmp.fontSize=size;tmp.color=Color.white;tmp.alignment=TextAlignmentOptions.Center;tmp.raycastTarget=false;return tmp;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.06f,.24f,.28f,.96f);
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(action);Text("Label",rect,text,Vector2.zero,Vector2.one);return button;
        }
        void BuildControls()
        {
            var canvas=new GameObject("Replay Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.transform.SetParent(transform,false);
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if(!EventSystem.current){var events=new GameObject("Replay EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);}
            label=Text("Status",canvas.transform,"Đang mở replay…",new Vector2(.18f,.82f),new Vector2(.82f,.99f),22);
            var back=Button("Back",canvas.transform,"QUAY LẠI HISTORY",new Vector2(.02f,.92f),new Vector2(.19f,.98f),ReplayLaunch.Close);
            var play=Button("Play",canvas.transform,"DỪNG",new Vector2(.04f,.025f),new Vector2(.13f,.085f),()=>{if(metadata==null)return;if(!playing&&playhead>=metadata.duration)Seek(0);playing=!playing;playLabel.text=playing?"DỪNG":"PHÁT";});playLabel=play.GetComponentInChildren<TMP_Text>();
            float[] rates={.5f,1,2};for(int i=0;i<3;i++){float rate=rates[i];Button("Speed"+i,canvas.transform,rate+"×",new Vector2(.70f+i*.07f,.025f),new Vector2(.76f+i*.07f,.085f),()=>speed=rate);}
            var track=Rect("Seek",canvas.transform,new Vector2(.16f,.035f),new Vector2(.67f,.075f));track.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.1f,.22f,.25f);
            var fill=Rect("Fill",track,Vector2.zero,Vector2.one);fill.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.5f,.85f,.72f);
            var handle=Rect("Handle",track,Vector2.zero,Vector2.one);handle.sizeDelta=new Vector2(15,0);var handleImage=handle.gameObject.AddComponent<UnityEngine.UI.Image>();handleImage.color=Color.white;
            seek=track.gameObject.AddComponent<Slider>();seek.fillRect=fill;seek.handleRect=handle;seek.targetGraphic=handleImage;seek.interactable=false;seek.onValueChanged.AddListener(v=>Seek(v));
        }
        void OnDestroy(){if(actors!=null)foreach(var actor in actors)if(actor)Destroy(actor.gameObject);}
    }
}
