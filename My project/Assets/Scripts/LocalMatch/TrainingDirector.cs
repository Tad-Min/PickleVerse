using System;
using System.Linq;
using PickleVerse.Audio;
using PickleVerse.Progression;
using PickleVerse.Rules;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PickleVerse.LocalMatch
{
    public enum TrainingPhase { Catalogue, Briefing, Practice, Recovery, Finished }
    public static class TrainingLaunch
    {
        public static string LessonId;
        public static void Continue(){LessonId=TrainingEngine.Recommended(TrainingEngine.Ensure(ProgressionRuntime.Instance.Player));PickleVerse.Shop.LobbyNavigation.OpenTraining();}
        public static void Open(string id){LessonId=id;PickleVerse.Shop.LobbyNavigation.OpenTraining();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){LessonId=null;}
    }
    public sealed class TrainingDirector : MonoBehaviour,ISettingsHost,IMatchTouchInput,ISettingsLeaveConfirmation
    {
        public MatchActor player,coach,partner,opponent;
        public Transform ball;
        public TrailRenderer trail;
        public Camera view;
        public TrainingHud hud;
        public LineRenderer targetRing,receiveRing,recoveryRing,kitchenNear,kitchenFar,teamLink;
        public TouchControl[] touchControls;
        public GameObject touchRoot;
        public TrainingPhase Phase {get;private set;}
        public TrainingDrill Lesson {get;private set;}
        public TrainingAttempt Session {get;private set;}
        public bool Paused {get;private set;}
        public string Feedback {get;private set;}="Chọn một bài để bắt đầu.";
        public Vector3 ReceivePosition {get;private set;}
        public Vector3 RecoveryPosition {get;private set;}
        public Vector3 TargetPosition {get;private set;}
        public int ExpectedMask {get;private set;}
        public float ReleaseIn=>incoming?Mathf.Max(0,feed.duration-flightTime-expected.ContactDelay):0;
        public bool Receiving=>incoming;
        public bool Returning=>outgoing;
        public bool PartnerTurn {get;private set;}
        public bool IsGameplay=>true;
        public bool IsPaused=>Paused;
        public bool CanSignOut=>false;
        public string LeaveConfirmation=>"Tiến trình đã đạt trong bài được lưu. Bạn có thể tiếp tục bài từ Tiến trình.";
        public int TouchShotMask {get;set;}
        public Vector2 TouchMovement {get;set;}
        public bool TouchRun {get;set;}
        readonly ShotInput input=new ShotInput();
        SinglesRules rules;
        ShotIntent expected,chosen;
        TutorialFeed feed;
        Vector3 ballPosition,ballVelocity,serveOrigin,cameraStart;
        Quaternion cameraRotation;
        float cameraFov,elapsed,flightTime,wait,recoveryTime,serveTime,lastSave,renderTime,partnerSide=-1;
        bool incoming,outgoing,bounced,coachLaunched,hit,releasedServe,candidate,recovering,needsRecovery,outcomeAnnounced,leaving;
        TrainingError lastError;
        Vector3 trialPlayerOrigin;
        MatchActor receiver,feeder;
        void Awake()
        {
            MatchRenderBudget.ConfigurePractice(gameObject);
            player.Initialize();coach.Initialize();partner.Initialize();opponent.Initialize();
            player.LocalHitFeedback=true;
            PickleVerse.Shop.EquippedPaddle.Attach(player.paddle);
            foreach(var control in touchControls)if(control)control.Input=this;
            hud.Bind(this);Catalogue();
        }
        void Start(){if(!string.IsNullOrEmpty(TrainingLaunch.LessonId)){string id=TrainingLaunch.LessonId;TrainingLaunch.LessonId=null;Select(id);}}
        void Update()
        {
            if(SettingsOverlay.IsOpen||SettingsOverlay.Loading||Paused)return;
            if(GameControls.Pressed(GameAction.Settings)){SettingsOverlay.Open(this);return;}
            Vector2 movement=TouchMovement;
            if(GameControls.Held(GameAction.Left))movement.x--;if(GameControls.Held(GameAction.Right))movement.x++;
            if(GameControls.Held(GameAction.Forward))movement.y++;if(GameControls.Held(GameAction.Backward))movement.y--;
            Advance(Mathf.Min(Time.deltaTime,.05f),GameControls.ShotMask|TouchShotMask,Vector2.ClampMagnitude(movement,1),GameControls.Held(GameAction.Run)||TouchRun,GameControls.TapMask);
        }
        public void Catalogue()
        {
            MusicPlayer.SetTrainingExercise(false);SfxPlayer.StopAll();hud.announcements.Clear();
            SaveActive();Phase=TrainingPhase.Catalogue;Lesson=null;Session=null;ResetFlight();ShowDoubles(false);elapsed=lastSave=renderTime=0;CaptureCamera();hud.ShowCatalogue();
            player.ResetAt(new Vector3(1.2f,0,-5.2f),-1);coach.ResetAt(new Vector3(-1.2f,0,4.4f),1);SetTouch(false);
        }
        public void Select(string id)
        {
            var lesson=TrainingCurriculum.Find(id);if(lesson==null)return;
            var data=TrainingEngine.Ensure(ProgressionRuntime.Instance.Player);if(!TrainingEngine.Unlocked(data,lesson)){Feedback="Hoàn thành bài yêu cầu để mở khóa.";hud.ShowCatalogue();return;}
            MusicPlayer.SetTrainingExercise(false);SfxPlayer.StopAll();hud.announcements.Clear();
            SaveActive();Lesson=lesson;Session=null;Phase=TrainingPhase.Briefing;elapsed=lastSave=renderTime=0;Feedback=lesson.coaching;ResetFlight();ShowDoubles(lesson.Doubles);SetTouch(false);CaptureCamera();
            player.ResetAt(new Vector3(1.2f,0,-5.2f),-1);coach.ResetAt(new Vector3(-1.2f,0,4.4f),1);partner.ResetAt(new Vector3(-1.3f,0,-5.2f),-1);opponent.ResetAt(new Vector3(1.3f,0,4.4f),1);
            TargetPosition=new Vector3(-1.35f,.035f,lesson.skill==TrainingSkill.Serve?4.75f:1.45f);DrawRing(targetRing,TargetPosition,.9f);hud.ShowBriefing();
        }
        public void StartLesson(bool retry=false)
        {
            if(Lesson==null)return;var runtime=ProgressionRuntime.Instance;
            MusicPlayer.SetTrainingExercise(true);SfxPlayer.StopAll();hud.announcements.Clear();
            var active=TrainingEngine.Ensure(runtime.Player).active;
            if(!retry&&active!=null&&!active.completed&&active.lessonId==Lesson.id)Session=Clone(active);
            else{runtime.UpdateWallet(p=>TrainingEngine.Start(p,Lesson.id,retry));Session=Clone(runtime.Player.training.active);}
            Phase=TrainingPhase.Practice;elapsed=lastSave=renderTime=0;PartnerTurn=false;Feedback="Theo vùng sáng và chỉ báo timing.";PrepareTrial(true);SetTouch(Application.isMobilePlatform);hud.ShowPractice();CaptureCamera();
        }
        public void Retry(){StartLesson(true);}
        public void NextLesson(){var id=TrainingEngine.Recommended(TrainingEngine.Ensure(ProgressionRuntime.Instance.Player));Select(id);}
        public void Advance(float dt,int mask=0,Vector2 movement=default,bool run=false,int tap=0)
        {
            if(Paused||dt<=0)return;elapsed+=dt;hud.announcements.Advance(dt);
            // Bound acceleration, pose blending and contact steps on a slow frame.
            // The camera and HUD still update only once per displayed frame.
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt*60));float step=dt/steps;
            for(int i=0;i<steps;i++)
            {
                if(Phase==TrainingPhase.Practice||Phase==TrainingPhase.Recovery)
                {
                    Session.seconds+=step;player.Step(movement,run,step,true,MatchMovement.PracticeBounds(player.end));
                    coach.Step(Vector2.zero,false,step,false);
                    if(opponent.gameObject.activeSelf)opponent.Step(Vector2.zero,false,step,false);
                    MovePartner(step);
                    if(Phase==TrainingPhase.Practice){if(i==0&&tap!=0)Input(mask|tap);Input(mask);Simulation(step);}
                    else Recovery(step);
                }
                else
                {
                    player.Step(Vector2.zero,false,step,false);coach.Step(Vector2.zero,false,step,false);
                    if(partner.gameObject.activeSelf)partner.Step(Vector2.zero,false,step,false);
                    if(opponent.gameObject.activeSelf)opponent.Step(Vector2.zero,false,step,false);
                }
            }
            if((Phase==TrainingPhase.Practice||Phase==TrainingPhase.Recovery)&&elapsed-lastSave>10){lastSave=elapsed;SaveActive();}
            CameraStep(dt);if(elapsed-renderTime>.08f){renderTime=elapsed;hud.Refresh();}
            if(teamLink.gameObject.activeSelf){teamLink.SetPosition(0,player.transform.position+Vector3.up*.06f);teamLink.SetPosition(1,partner.transform.position+Vector3.up*.06f);}
        }
        static ShotIntent Intent(int mask,bool serve)
        {var resolver=new ShotInput();resolver.Tick(mask,false,serve,out _);resolver.Tick(mask&24,false,serve,out var shot);return shot;}
        void PrepareTrial(bool initial=false)
        {
            ResetFlight();Phase=TrainingPhase.Practice;wait=.7f;serveTime=0;outcomeAnnounced=false;input.Cancel();input.Tick(0,false,false,out _);
            ExpectedMask=Lesson.Mask(Session.trials);expected=Intent(ExpectedMask,Lesson.skill==TrainingSkill.Serve);chosen=expected;
            bool soft=expected.kind==ShotKind.Dink||expected.kind==ShotKind.Reset||expected.kind==ShotKind.Volley;
            float depth=Lesson.skill==TrainingSkill.Serve?-7.3f:soft?-2.95f:-5.05f;
            if(Lesson.skill==TrainingSkill.DoublesTransition&&expected.kind!=ShotKind.Drop)depth=-2.95f;
            bool side=Lesson.skill==TrainingSkill.Footwork||Lesson.skill==TrainingSkill.DoublesCover;
            float x=side?(Session.trials%2==0?1.35f:-1.35f):1.15f;
            ReceivePosition=new Vector3(x,0,depth);RecoveryPosition=new Vector3(Lesson.Doubles?x:side?0:1.15f,0,Lesson.skill==TrainingSkill.Approach||Lesson.skill==TrainingSkill.DoublesTransition?-2.8f:depth);
            if(initial||!side)player.ResetAt(ReceivePosition,-1);else player.ResetAt(new Vector3(0,0,depth),-1);
            coach.ResetAt(new Vector3(-1.2f,0,4.5f),1);opponent.ResetAt(new Vector3(1.4f,0,4.2f),1);
            if(initial){partnerSide=-1;partner.ResetAt(new Vector3(-1.3f,0,depth),-1);}
            player.RefreshPose();coach.RefreshPose();receiver=PartnerTurn?partner:player;feeder=Lesson.Doubles&&Session.trials%2==1?opponent:coach;
            var idealContact=ReceivePosition+Quaternion.Euler(0,0,0)*player.ContactOffset(expected);
            if(PartnerTurn)idealContact=partner.transform.TransformPoint(partner.ContactOffset(expected));
            if(Lesson.skill==TrainingSkill.Serve)idealContact=player.transform.position+new Vector3(.3f,.7f,.3f);
            TargetPosition=ShotTrajectory.Target(PartnerTurn?partner:player,expected,idealContact,(PartnerTurn?partner:player).transform.position.x);
            TargetPosition=new Vector3(TargetPosition.x,.035f,TargetPosition.z);trialPlayerOrigin=player.transform.position;
            DrawRing(receiveRing,ReceivePosition+Vector3.up*.035f,.6f);DrawRing(targetRing,TargetPosition,Lesson.tier<2?.9f:.7f);DrawRing(recoveryRing,RecoveryPosition+Vector3.up*.035f,.6f);recoveryRing.gameObject.SetActive(false);
            rules=Lesson.skill==TrainingSkill.Serve?new SinglesRules(0):Lesson.skill==TrainingSkill.Drop||Lesson.skill==TrainingSkill.Approach||Lesson.skill==TrainingSkill.DoublesTransition&&expected.kind==ShotKind.Drop?ThirdShot():TutorialCurriculum.RallyReady();
            if(Lesson.skill==TrainingSkill.Serve)rules.PrepareServe(-1);
            ballPosition=Lesson.skill==TrainingSkill.Serve?player.HeldBallPosition:feeder.HeldBallPosition;ball.position=ballPosition;ball.gameObject.SetActive(true);
            Feedback=PartnerTurn?"Bóng của đồng đội AI • giữ cùng hàng, không tranh bóng.":"Bóng "+(Session.trials+1)+" / "+Lesson.trials+" • "+GameControls.ShotKeys(ExpectedMask);
        }
        static SinglesRules ThirdShot()
        {var r=new SinglesRules(0);r.PrepareServe(-1);r.Serve(0,new CourtPoint(1.2f,0,-7.3f),new CourtPoint(1.4f,0,-7.3f),new CourtPoint(1.3f,.7f,-7.1f),1);r.Bounce(new CourtPoint(-1.3f,Court.BallRadius,4));r.Hit(1,new CourtPoint(-1.3f,.9f,4),false);return r;}
        void Input(int mask)
        {
            if(Phase!=TrainingPhase.Practice||PartnerTurn||hit||outgoing)return;
            if(!input.Tick(mask,GameControls.Pressed(GameAction.CancelShot),Lesson.skill==TrainingSkill.Serve,out var shot))
            {if((mask&7)!=0&&input.SelectionMask==0&&!string.IsNullOrEmpty(input.Feedback))Fail(TrainingError.WrongInput);return;}
            if(input.CommittedMask!=ExpectedMask){Fail(TrainingError.WrongInput);return;}
            if(Lesson.skill==TrainingSkill.Serve){if(!player.Swinging){chosen=shot;player.Swing(shot,player.HeldBallPosition);serveTime=0;}return;}
            if(!incoming){Fail(TrainingError.Early);return;}
            float remaining=feed.duration-flightTime;
            if(remaining>shot.ContactDelay+.23f){Fail(TrainingError.Early);return;}
            if(remaining<.10f){Fail(TrainingError.Late);return;}
            chosen=shot;player.Swing(shot,feed.At(feed.duration),remaining);
        }
        void Simulation(float dt)
        {
            if(Phase!=TrainingPhase.Practice)return;
            if(Lesson.skill==TrainingSkill.Serve&&!outgoing)
            {
                Vector3 previousServePosition=ballPosition;
                serveTime+=dt;ServeBallMotion.Step(player,ref releasedServe,ref serveOrigin,ref ballPosition,ref ballVelocity,dt);
                if(player.Swinging&&player.SwingTime>=.8f&&player.CanContact(previousServePosition,ballPosition,.45f,out _))
                {
                    ballPosition=new Vector3(serveOrigin.x,ballPosition.y,serveOrigin.z);
                    var fault=rules.Serve(0,player.Feet(true),player.Feet(false),Point(ballPosition),serveTime,player.PaddleVelocity.y>0,player.PaddleHeadTop<=player.hand.position.y+.015f);
                    if(fault!=Fault.None){Fail(fault==Fault.ServeFoot?TrainingError.ServeFeet:TrainingError.Early,true);return;}Session.contacts++;Session.timingTotal+=Math.Abs(player.SwingTime-player.ContactDelay);player.ConsumeContact(false,ballPosition);LaunchReturn(player,chosen);
                }
                else if(serveTime>10){Fail(TrainingError.Late);return;}
            }
            else if(!incoming&&!outgoing)
            {
                wait-=dt;
                if(wait<=0&&!coachLaunched&&!feeder.Swinging)feeder.Swing(new ShotIntent(ShotKind.Drive),ReceivePosition);
                if(!coachLaunched&&feeder.ContactActive&&feeder.SwingTime>=feeder.ContactDelay)
                {
                    feeder.ConsumeContact(false);coachLaunched=true;
                    Vector3 contact=PartnerTurn?partner.transform.TransformPoint(partner.ContactOffset(expected)):ReceivePosition+player.ContactOffset(expected);
                    feed=TutorialFeed.Create(feeder.paddleFace.position,contact,expected.AfterBounce);incoming=true;flightTime=0;Trail(true);
                }
            }
            if(incoming)
            {
                Vector3 previousBall=ballPosition;
                flightTime+=dt;ballPosition=feed.At(flightTime);
                if(feed.hasBounce&&!bounced&&flightTime>=feed.bounceTime){bounced=true;rules.Bounce(Point(feed.bouncePoint));}
                if(PartnerTurn&&!partner.Swinging&&feed.duration-flightTime<=expected.ContactDelay+.01f)partner.Swing(expected,feed.At(feed.duration));
                receiver=PartnerTurn?partner:player;
                if(!hit&&receiver.CanContact(previousBall,ballPosition,PaddleContact.MaxAssist,out _)&&Mathf.Abs(flightTime-feed.duration)<.17f)
                {
                    // Consume every physical contact once, using the shared BotMatch sound selector.
                    ballPosition=receiver.ContactPoint;
                    if(expected.AfterBounce&&!bounced){receiver.ConsumeContact(!bounced,ballPosition,false);Fail(TrainingError.Early);return;}
                    if((expected.BeforeBounce||expected.kind==ShotKind.Smash)&&bounced){receiver.ConsumeContact(!bounced,ballPosition,false);Fail(TrainingError.Late);return;}
                    var fault=rules.Hit(0,Point(ballPosition),receiver.InKitchen);
                    receiver.ConsumeContact(!bounced,ballPosition,fault==Fault.None);
                    if(fault!=Fault.None){Fail(fault==Fault.KitchenVolley?TrainingError.Kitchen:TrainingError.Early,true);return;}
                    if(!PartnerTurn){Session.contacts++;Session.timingTotal+=Math.Abs(flightTime-feed.duration);}
                    LaunchReturn(receiver,PartnerTurn?expected:chosen);
                }
                else if(flightTime>feed.duration+.23f){Fail(TrainingError.Late);return;}
            }
            if(outgoing)
            {
                Vector3 old=ballPosition;BallFlight.Integrate(ref ballPosition,ref ballVelocity,dt);
                if(player.VolleyMomentum&&player.InKitchen){Fail(TrainingError.Momentum);return;}
                if(old.z<0&&ballPosition.z>=0){float t=-old.z/(ballPosition.z-old.z);Vector3 cross=Vector3.Lerp(old,ballPosition,t);if(Mathf.Abs(cross.x)<Court.NetHalfSpan&&cross.y-Court.BallRadius<Court.NetHeight(cross.x)){Fail(TrainingError.Net);return;}}
                if(ballPosition.y<=Court.BallRadius)
                {
                    ballPosition.y=Court.BallRadius;var fault=rules.Bounce(Point(ballPosition));outgoing=false;Trail(false);
                    if(fault!=Fault.None){Fail(fault==Fault.Out?TrainingError.Out:TrainingError.Target,true);return;}
                    bool aim=Vector2.Distance(new Vector2(ballPosition.x,ballPosition.z),new Vector2(TargetPosition.x,TargetPosition.z))<(Lesson.tier<2?.95f:.75f);
                    if(!aim){Fail(TrainingError.Target);return;}
                    Candidate();
                }
            }
            ball.position=ballPosition;
        }
        void LaunchReturn(MatchActor actor,ShotIntent shot)
        {
            hit=true;incoming=false;outgoing=true;
            Vector3 target=ShotTrajectory.Target(actor,shot,ballPosition,actor.transform.position.x);
            ballVelocity=ShotTrajectory.Launch(shot,ballPosition,target);Trail(true);
        }
        void Candidate()
        {
            candidate=true;Phase=TrainingPhase.Recovery;recoveryTime=0;
            needsRecovery=Lesson.skill==TrainingSkill.Footwork||Lesson.skill==TrainingSkill.Approach||Lesson.Doubles;
            recoveryRing.gameObject.SetActive(needsRecovery);Feedback=needsRecovery?"Cú đánh đạt • di chuyển vào vòng hồi vị và giữ đội hình.":"Đúng kỹ thuật và vùng đích.";
            if(!needsRecovery)AnnounceOutcome();
        }
        void Fail(TrainingError error,bool refereeFault=false)
        {
            if(Phase!=TrainingPhase.Practice)return;
            candidate=false;lastError=error;Session.Error(error);Phase=TrainingPhase.Recovery;recoveryTime=0;needsRecovery=false;incoming=outgoing=false;Trail(false);
            // Coaching errors (input, timing, aim, positioning) are not referee faults.
            if(refereeFault||error==TrainingError.Kitchen||error==TrainingError.Momentum||error==TrainingError.Net||error==TrainingError.Out||error==TrainingError.ServeFeet)SfxPlayer.Play(GameSfx.FaultWhistle);
            Feedback=TrainingCurriculum.ErrorName(error)+" • "+TrainingCurriculum.Tip(error);
            AnnounceOutcome();
        }
        void AnnounceOutcome()
        {
            if(outcomeAnnounced)return;outcomeAnnounced=true;
            if(PartnerTurn)return;
            hud.announcements.Enqueue(candidate?"THÀNH CÔNG":"CHƯA ĐÚNG • THỬ LẠI",
                candidate?"Tôi biết bạn làm được mà!":TrainingCurriculum.ErrorName(lastError)+"\nKhông sao bạn ơi, rồi bạn cũng quen thôi.",
                candidate?AnnouncementTone.Success:AnnouncementTone.Fault,candidate?2.1f:2.8f);
        }
        void Recovery(float dt)
        {
            recoveryTime+=dt;bool recovered=Vector3.Distance(player.transform.position,RecoveryPosition)<.6f;
            if(Lesson.Doubles){float gap=Vector3.Distance(player.transform.position,partner.transform.position);recovered&=Mathf.Abs(player.transform.position.z-partner.transform.position.z)<.75f&&gap>.9f&&gap<3.5f;}
            if(!outcomeAnnounced&&candidate&&needsRecovery&&!recovered&&recoveryTime<4)return;
            if(recoveryTime<1.3f)return;
            if(!outcomeAnnounced&&candidate&&needsRecovery&&!recovered){candidate=false;lastError=Lesson.Doubles?TrainingError.Spacing:TrainingError.Movement;Session.Error(lastError);Feedback=TrainingCurriculum.Tip(lastError);}
            AnnounceOutcome();
            if(hud.announcements.Busy)return;
            if(PartnerTurn){PartnerTurn=false;PrepareTrial();return;}
            Session.trials++;if(candidate)Session.successes++;SaveActive();
            if(Session.trials>=Lesson.trials){Finish();return;}
            PartnerTurn=Lesson.skill==TrainingSkill.DoublesTransition&&Session.trials%3==0;
            PrepareTrial();
        }
        void Finish()
        {
            var runtime=ProgressionRuntime.Instance;runtime.UpdateWallet(p=>TrainingEngine.Complete(p,Session));Phase=TrainingPhase.Finished;ResetFlight();SetTouch(false);CaptureCamera();
            Feedback=Session.passed?(Session.review?"Ôn lại hoàn thành • tiến bộ đã được lưu, không có phần thưởng.":"Hoàn thành • "+Session.rewardTokenpad+" Tokenpad và "+Session.rewardXp+" XP đã vào ví."):"Chưa đạt • cần "+Lesson.required+" cú thành công. Xem lỗi và luyện lại.";hud.ShowResults();
        }
        public void SaveActive()
        {
            if(Session==null||Session.completed||!ProgressionRuntime.Exists)return;
            var copy=Clone(Session);ProgressionRuntime.Instance.UpdateWallet(p=>TrainingEngine.Ensure(p).active=copy);
        }
        void MovePartner(float dt)
        {
            if(!partner.gameObject.activeSelf)return;
            float playerX=player.transform.position.x;
            float x=Mathf.Clamp(playerX+partnerSide*2.4f,-2.8f,2.8f);
            // Keep the current lane until the sideline leaves too little room.
            // Switching only at the edge avoids oscillation around centre court.
            if(Mathf.Abs(x-playerX)<1.1f){partnerSide=-partnerSide;x=Mathf.Clamp(playerX+partnerSide*2.4f,-2.8f,2.8f);}
            Vector3 goal=new Vector3(x,0,Mathf.Min(-2.7f,player.transform.position.z));
            Vector3 d=goal-partner.transform.position;partner.Step(new Vector2(d.x,d.z).normalized*Mathf.Clamp01(d.magnitude*2),false,dt,true,MatchMovement.PracticeBounds(partner.end));
        }
        void ShowDoubles(bool show){partner.gameObject.SetActive(show);opponent.gameObject.SetActive(show);teamLink.gameObject.SetActive(show);}
        void ResetFlight(){incoming=outgoing=coachLaunched=hit=bounced=releasedServe=false;input.Cancel();Trail(false);ball.gameObject.SetActive(false);targetRing.gameObject.SetActive(false);receiveRing.gameObject.SetActive(false);recoveryRing.gameObject.SetActive(false);}
        void Trail(bool emitting){if(trail){trail.emitting=emitting;if(!emitting)trail.Clear();}}
        static TrainingAttempt Clone(TrainingAttempt a)=>JsonUtility.FromJson<TrainingAttempt>(JsonUtility.ToJson(a));
        static CourtPoint Point(Vector3 p)=>new CourtPoint(p.x,p.y,p.z);
        public static void DrawRing(LineRenderer ring,Vector3 center,float radius){ring.gameObject.SetActive(true);ring.positionCount=49;for(int i=0;i<49;i++){float a=i*Mathf.PI*2/48;ring.SetPosition(i,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}}
        void CaptureCamera(){cameraStart=view.transform.position;cameraRotation=view.transform.rotation;cameraFov=view.fieldOfView;}
        void CameraStep(float dt)
        {
            Vector3 goal,look;float fov=56;
            if(Phase==TrainingPhase.Catalogue){goal=new Vector3(-6,11,-12);look=new Vector3(0,.2f,0);}
            else if(Phase==TrainingPhase.Briefing){goal=elapsed<1.7f?new Vector3(-6,11,-10):elapsed<3.4f?new Vector3(-4.2f,6,-3.8f):new Vector3(1.1f,5.3f,-9.5f);look=elapsed<3.4f?new Vector3(0,.3f,0):new Vector3(0,.65f,-.4f);}
            else if(Phase==TrainingPhase.Finished){goal=player.transform.position+new Vector3(0,2.3f,-4.5f);look=player.transform.position+Vector3.up*.85f;}
            else{goal=new Vector3(player.transform.position.x,5.3f,player.transform.position.z-4.25f);look=new Vector3(player.transform.position.x,.6f,-.5f);if(Lesson.Doubles){goal.x=0;goal.y=6.5f;fov=64;look.x=0;}}
            float weight=1-Mathf.Exp(-dt*3);view.transform.position=Vector3.Lerp(view.transform.position,goal,weight);view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(look-view.transform.position),weight);view.fieldOfView=Mathf.Lerp(view.fieldOfView,fov,weight);
        }
        void SetTouch(bool show){if(touchRoot)touchRoot.SetActive(show);TouchShotMask=0;TouchMovement=Vector2.zero;TouchRun=false;}
        public void ToggleTouch(){if(Phase==TrainingPhase.Practice||Phase==TrainingPhase.Recovery)SetTouch(!touchRoot.activeSelf);}
        public void SetPaused(bool value){Paused=value;TouchShotMask=0;TouchMovement=Vector2.zero;TouchRun=false;input.Cancel();hud.announcements.Suspended=value;if(value){SaveActive();SfxPlayer.StopAll();hud.announcements.panel.gameObject.SetActive(false);}else hud.announcements.Advance(0);}
        public void LeaveToMenu(){if(leaving||PickleVerse.Shop.LobbyNavigation.IsBusy)return;SaveActive();leaving=true;Paused=true;PickleVerse.Shop.LobbyNavigation.ReturnToLobby();}
        public void BackToModes(){if(leaving||PickleVerse.Shop.LobbyNavigation.IsBusy)return;SaveActive();leaving=true;Paused=true;PickleVerse.Shop.LobbyNavigation.OpenModes();}
        public System.Threading.Tasks.Task SignOutAsync()=>System.Threading.Tasks.Task.CompletedTask;
        void OnApplicationFocus(bool focused){if(!focused){SaveActive();SetPaused(true);}}
        void OnApplicationPause(bool paused){if(paused){SaveActive();SetPaused(true);}}
        void OnDestroy(){if(!leaving)SaveActive();}
    }
}
