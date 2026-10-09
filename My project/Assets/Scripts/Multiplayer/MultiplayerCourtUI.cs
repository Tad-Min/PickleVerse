using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerCourtUI : MonoBehaviour
    {
        public TMP_Text score,status,server;
        public UnityEngine.UI.Button leave,returnToRoom;
        TMP_Text leaveLabel,returnLabel;
        void Start()
        {
            if(leave)
            {
                leaveLabel=leave.GetComponentInChildren<TMP_Text>(true);
                leave.onClick.AddListener(LeaveClicked);
            }
            if(returnToRoom)
            {
                returnLabel=returnToRoom.GetComponentInChildren<TMP_Text>(true);
                returnToRoom.onClick.AddListener(ReturnClicked);
            }
        }
        void LeaveClicked()
        {
            var app=MultiplayerApp.Instance;
            if(!app) { SceneManager.LoadScene("PlayMultiplayer"); return; }
            _=app.ExecuteAsync(()=>app.Searching?app.CancelSearchAsync():app.LeaveGameAsync());
        }
        void ReturnClicked()
        {
            var app=MultiplayerApp.Instance;
            if(!app) { SceneManager.LoadScene("PlayMultiplayer"); return; }
            var match=NetworkMatch.Instance;
            if(match && match.IsSpawned && match.BetweenGames.Value && !match.Finished.Value)
            {
                if(match.IsServer && !app.Busy) match.RequestNextGame();
                return;
            }
            _=app.ExecuteAsync(()=>app.ReturnToRoomAsync("Trận đã kết thúc."));
        }
        void OnDestroy()
        {
            if(leave) leave.onClick.RemoveListener(LeaveClicked);
            if(returnToRoom) returnToRoom.onClick.RemoveListener(ReturnClicked);
        }
        void Update()
        {
            var app=MultiplayerApp.Instance;
            var match=NetworkMatch.Instance;
            bool hasMatch=match && match.IsSpawned;
            bool nextGame=hasMatch && match.BetweenGames.Value && !match.Finished.Value && match.IsServer;
            // NGO can despawn the match before the court UI is unloaded.
            bool ended=hasMatch?match.Finished.Value:!app || (!app.Searching && app.State!=FlowState.Reconnecting);
            if(score && hasMatch) score.text=$"{match.ScoreA.Value} : {match.ScoreB.Value}";
            if(status) status.text=!app?"Chưa có phiên thi đấu.":app.Searching?app.Notice+"  "+app.Elapsed:
                !hasMatch || app.State==FlowState.Reconnecting?app.Notice:match.Status.Value.ToString();
            if(leaveLabel) leaveLabel.text=app && app.Searching?"Hủy":"Rời trận";
            if(server) server.text=hasMatch?$"Giao bóng: đội {(match.ServerTeam.Value==0?"A":"B")}, người {match.ServerIndex.Value+1}  •  Lượt {match.ServerNumber.Value}":"";
            if(returnLabel) returnLabel.text=nextGame?"Game tiếp":"Về phòng";
            if(returnToRoom)
            {
                returnToRoom.gameObject.SetActive(ended || nextGame);
                returnToRoom.interactable=!app || !app.Busy;
            }
            if(leave) leave.gameObject.SetActive(!ended && !nextGame);
        }
    }
}
