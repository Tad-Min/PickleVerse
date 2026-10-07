using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerTestUI : MonoBehaviour
    {
        public GameObject menuPanel,roomPanel;
        public TMP_InputField profileInput,nameInput,codeInput;
        public TMP_Text roomTitle,roomCode,memberText,statusText,timerText,genderText;
        public UnityEngine.UI.Button play,singles,doubles,find,cancel,copy,join,customSingles,customDoubles,ready,start,teamA,teamB,leave,gender;
        public UnityEngine.UI.Toggle holdModifiers;
        public GameObject[] memberActions;
        public UnityEngine.UI.Button[] assignA,assignB,kick;
        MultiplayerApp app;
        async void Start()
        {
            app=MultiplayerApp.Ensure(); app.Changed+=Refresh;
            string requested="Test"+System.Diagnostics.Process.GetCurrentProcess().Id;
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-pkvProfile") requested=args[i+1];
            profileInput.text=app.ActiveProfile??requested; nameInput.text=app.ActiveProfile!=null?app.DisplayName:requested;
            play.onClick.AddListener(()=>Run(()=>Enter()));
            singles.onClick.AddListener(()=>Run(()=>app.SelectModeAsync(PlayMode.Singles)));
            doubles.onClick.AddListener(()=>Run(()=>app.SelectModeAsync(PlayMode.Doubles)));
            find.onClick.AddListener(()=>Run(app.FindAsync)); cancel.onClick.AddListener(Cancel);
            copy.onClick.AddListener(()=>GUIUtility.systemCopyBuffer=app.Room?.Code??"");
            join.onClick.AddListener(()=>Run(()=>app.JoinRoomAsync(codeInput.text)));
            customSingles.onClick.AddListener(()=>Run(()=>app.CreateCustomAsync(PlayMode.Singles)));
            customDoubles.onClick.AddListener(()=>Run(()=>app.CreateCustomAsync(PlayMode.Doubles)));
            ready.onClick.AddListener(()=>Run(app.ReadyAsync)); start.onClick.AddListener(()=>Run(app.StartCustomAsync));
            teamA.onClick.AddListener(()=>Run(()=>app.SetTeamAsync(app.PlayerId,0))); teamB.onClick.AddListener(()=>Run(()=>app.SetTeamAsync(app.PlayerId,1)));
            leave.onClick.AddListener(()=>Run(async()=>{ await app.LeaveRoomAsync(); app.SetNotice("Đã rời phòng nhóm."); }));
            gender.onClick.AddListener(()=>{ app.Gender=app.Gender=="Male"?"Female":"Male"; Refresh(); });
            holdModifiers.isOn=PlayerPrefs.GetInt("PKV_Modifier",0)==1;
            holdModifiers.onValueChanged.AddListener(value=>PlayerPrefs.SetInt("PKV_Modifier",value?1:0));
            for(int i=0;i<assignA.Length;i++)
            {
                int row=i;
                assignA[i].onClick.AddListener(()=>Run(()=>app.SetTeamAsync(app.Members()[row].id,0)));
                assignB[i].onClick.AddListener(()=>Run(()=>app.SetTeamAsync(app.Members()[row].id,1)));
                kick[i].onClick.AddListener(()=>Run(()=>app.KickAsync(app.Members()[row].id)));
            }
            if(MultiplayerApp.OpenRoomOnLoad) { MultiplayerApp.OpenRoomOnLoad=false; bool openDefault=MultiplayerApp.OpenDefaultRoomOnLoad; MultiplayerApp.OpenDefaultRoomOnLoad=false; if(openDefault||app.State==FlowState.Menu) await app.ExecuteAsync(Enter); }
            Refresh();
        }
        Task Enter()
        { app.DisplayName=string.IsNullOrWhiteSpace(nameInput.text)?"Player":nameInput.text.Substring(0,Math.Min(18,nameInput.text.Length)); return app.PlayAsync(profileInput.text); }
        async void Run(Func<Task> action) { await app.ExecuteAsync(action); }
        async void Cancel() { try { await app.CancelSearchAsync(); } catch(Exception e) { app.SetNotice(e.Message); } }
        void Update() { if(app!=null) { timerText.text=app.Elapsed; statusText.text=app.Notice; } }
        void OnDestroy() { if(app!=null) app.Changed-=Refresh; }
        void Refresh()
        {
            bool menu=app.State==FlowState.Menu;
            menuPanel.SetActive(menu); roomPanel.SetActive(!menu);
            genderText.text=app.Gender=="Male"?"Nhân vật: Male":"Nhân vật: Female";
            roomTitle.text=(app.Custom?"CUSTOM ":"PHÒNG ")+(app.Mode==PlayMode.Singles?"1vs1":"2vs2");
            roomCode.text=app.Room!=null?"Mã phòng: "+app.Room.Code:"Chơi một mình";
            var members=app.Members();
            memberText.text=string.Join("\n\n",members.Select((p,i)=>$"{i+1}. {p.name}  {(p.id==app.PlayerId?"(Bạn)":"")}\n{(app.Custom?"Đội "+(p.team==0?"A":p.team==1?"B":"—")+(p.ready?" • Sẵn sàng":" • Chưa sẵn sàng"):"Cùng đội")}"));
            bool idle=app.State==FlowState.WaitingRoom&&!app.Busy;
            singles.interactable=doubles.interactable=app.CanChangeMode;
            find.gameObject.SetActive(!app.Custom); find.interactable=idle&&app.IsCaptain;
            cancel.gameObject.SetActive(app.Searching&&!app.Custom); cancel.interactable=true;
            timerText.gameObject.SetActive(app.Searching&&!app.Custom);
            join.interactable=customSingles.interactable=customDoubles.interactable=idle;
            copy.interactable=app.Room!=null;
            ready.gameObject.SetActive(app.Custom); start.gameObject.SetActive(app.Custom);
            teamA.gameObject.SetActive(app.Custom); teamB.gameObject.SetActive(app.Custom);
            ready.interactable=idle&&members.Any(p=>p.id==app.PlayerId&&p.team>=0);
            teamA.interactable=teamB.interactable=idle;
            start.interactable=idle&&app.IsCaptain&&members.Count==2*(int)app.Mode&&members.All(p=>p.ready)&&members.Count(p=>p.team==0)==(int)app.Mode&&members.Count(p=>p.team==1)==(int)app.Mode;
            leave.interactable=idle&&app.Room!=null; play.interactable=!app.Busy;
            for(int i=0;i<memberActions.Length;i++) { memberActions[i].SetActive(app.Custom&&app.IsCaptain&&i<members.Count&&members[i].id!=app.PlayerId); assignA[i].interactable=assignB[i].interactable=kick[i].interactable=idle; }
        }
    }
}
