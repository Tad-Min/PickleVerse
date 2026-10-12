using System.Linq;
using PickleVerse.Progression;
using PickleVerse.LocalMatch;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PickleVerse.FrontEnd
{
    public sealed partial class PlayerProgressionMenu
    {
        public Button trainingTab;
        public void ConfigureTrainingUI()
        {
            if(!trainingTab)
            {
                var existing=left.Find("Training Progress");trainingTab=existing?existing.GetComponent<Button>():MakeButton("Training Progress",left,"LUYỆN TẬP",()=>{},new Vector2(.08f,.205f),new Vector2(.92f,.295f),Card,White,16);
            }
            if(Application.isPlaying)trainingTab.onClick.AddListener(()=>Open(View.Training));
            SetTrainingTabRect(trainingTab,.205f,.295f);SetTrainingTabRect(dailyTab,.105f,.195f);SetTrainingTabRect(weeklyTab,.005f,.095f);
            if(!Application.isPlaying)trainingTab.gameObject.SetActive(false);
        }
        static void SetTrainingTabRect(Button b,float bottom,float top){var r=(RectTransform)b.transform;r.anchorMin=new Vector2(.08f,bottom);r.anchorMax=new Vector2(.92f,top);r.offsetMin=r.offsetMax=Vector2.zero;}
        void BuildTrainingProgress(PlayerProfile player)
        {
            var data=TrainingEngine.Ensure(player);int complete=data.lessons.Count(r=>r.bestStars>0);string resume=TrainingEngine.Recommended(data);
            summaryText.text=$"{complete}/{TrainingCurriculum.Lessons.Length} bài đã đạt\nTiếp theo: {TrainingCurriculum.Find(resume).title}";
            Fill(summaryFill,complete/(float)TrainingCurriculum.Lessons.Length);
            Label(bonusButton).text="TIẾP TỤC LUYỆN";bonusButton.interactable=true;bonusButton.onClick.RemoveAllListeners();bonusButton.onClick.AddListener(()=>{Close();TrainingLaunch.Continue();});
            Tab(trainingTab,true);Tab(dailyTab,false);Tab(weeklyTab,false);
            Stat("Thời gian luyện tập",ProgressionRuntime.FormatTime(data.lessons.Sum(r=>r.seconds)+(data.active!=null&&!data.active.completed?data.active.seconds:0)));
            var mostWrong=data.lessons.OrderByDescending(r=>r.errors.Sum(e=>e.count)).FirstOrDefault();
            var mostRetried=data.lessons.OrderByDescending(r=>r.retries).FirstOrDefault();
            Stat("Sai nhiều nhất",mostWrong==null||mostWrong.errors.Count==0?"Chưa có lỗi":TrainingCurriculum.Find(mostWrong.id).title);
            Stat("Làm lại nhiều nhất",mostRetried==null||mostRetried.retries==0?"Chưa làm lại":TrainingCurriculum.Find(mostRetried.id).title+" · "+mostRetried.retries+" lượt");
            foreach(var d in TrainingCurriculum.Lessons)
            {
                var r=data.lessons.Find(x=>x.id==d.id);var row=Row("Training "+d.id,125);bool unlocked=TrainingEngine.Unlocked(data,d);
                Text("Title",row,d.title,20,White,new Vector2(.025f,.65f),new Vector2(.73f,.97f),Vector2.zero,Vector2.zero);
                string stars=r==null||r.bestStars==0?"Chưa đạt":r.bestStars+"/3 sao";
                Text("Mastery",row,stars+" • Tốt nhất "+(r==null?"0%":r.bestAccuracy.ToString("P0"))+" • "+(r?.attempts??0)+" lượt • Làm lại "+(r?.retries??0),15,Muted,new Vector2(.025f,.35f),new Vector2(.75f,.65f),Vector2.zero,Vector2.zero);
                var error=r?.errors.OrderByDescending(e=>e.count).FirstOrDefault();
                string detail=error==null?"Chưa ghi nhận lỗi":TrainingCurriculum.ErrorName(error.error)+" • "+error.count+" lần";
                if(r!=null&&r.accuracyHistory.Count>0)detail+="\nGần đây: "+string.Join(" → ",r.accuracyHistory.TakeLast(4).Select(v=>v.ToString("P0")));
                Text("Errors",row,detail,14,Mint,new Vector2(.025f,.025f),new Vector2(.75f,.36f),Vector2.zero,Vector2.zero);
                var b=MakeButton("Practice",row,unlocked?"LUYỆN LẠI":"CHƯA MỞ",()=>{Close();TrainingLaunch.Open(d.id);},new Vector2(.78f,.26f),new Vector2(.97f,.77f),unlocked?Mint:Surface,unlocked?Ink:Muted,14);b.interactable=unlocked;
            }
        }
    }
}
