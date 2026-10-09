using TMPro;
using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerCourtUI : MonoBehaviour
    {
        public TMP_Text score,status,server;
        public UnityEngine.UI.Button leave,returnToRoom;
        void Start()
        {
            leave.onClick.AddListener(()=>_=MultiplayerApp.Instance.ExecuteAsync(()=>MultiplayerApp.Instance.Searching?MultiplayerApp.Instance.CancelSearchAsync():MultiplayerApp.Instance.LeaveGameAsync()));
            returnToRoom.onClick.AddListener(()=> { if(NetworkMatch.Instance.BetweenGames.Value) NetworkMatch.Instance.RequestNextGame(); else _=MultiplayerApp.Instance.ExecuteAsync(()=>MultiplayerApp.Instance.ReturnToRoomAsync("Trận đã kết thúc.")); });
        }
        void Update()
        {
            var match=NetworkMatch.Instance; if(match==null) return;
            score.text=$"{match.ScoreA.Value} : {match.ScoreB.Value}";
            var app=MultiplayerApp.Instance;
            status.text=app.Searching?app.Notice+"  "+app.Elapsed:app.State==FlowState.Reconnecting?app.Notice:match.Status.Value.ToString();
            leave.GetComponentInChildren<TMP_Text>().text=app.Searching?"Hủy":"Rời trận";
            server.text=$"Giao bóng: đội {(match.ServerTeam.Value==0?"A":"B")}, người {match.ServerIndex.Value+1}  •  Lượt {match.ServerNumber.Value}";
            returnToRoom.GetComponentInChildren<TMP_Text>().text=match.BetweenGames.Value?"Game tiếp":"Về phòng";
            returnToRoom.gameObject.SetActive(match.Finished.Value || (match.BetweenGames.Value && match.IsServer)); leave.gameObject.SetActive(!match.Finished.Value && !(match.BetweenGames.Value && match.IsServer));
        }
    }
}
