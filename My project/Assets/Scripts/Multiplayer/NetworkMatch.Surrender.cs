using System;
using System.Linq;
using Fusion;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    // "Đầu hàng": the state authority keeps the request of each team; the clients of that team only show it.
    public sealed partial class NetworkMatch : ISurrenderCourt
    {
        const string SurrenderAsk = "surrender-ask", SurrenderAgree = "surrender-agree", SurrenderRefuse = "surrender-refuse";
        const string SurrenderOpened = "surrender-open", SurrenderClosed = "surrender-end";
        SurrenderDesk surrenderDesk;
        bool surrendered;

        /// <summary>The request of the local player's team (client side).</summary>
        public SurrenderRequest Surrender { get; } = new SurrenderRequest();
        public bool CanSurrender => IsSpawned && Started && !Finished && LocalMember != null;
        public string LocalPlayer => MultiplayerApp.Instance ? MultiplayerApp.Instance.PlayerId ?? "" : "";
        RoomMember localMember;
        RoomMember LocalMember => localMember ??= Roster?.Find(LocalPlayer);
        public RoomMember SurrenderPartner
        {
            get
            {
                var local = LocalMember;
                return local == null ? null : Roster.members.FirstOrDefault(m => m.team == local.team && !m.bot && m.id != local.id);
            }
        }

        public void AskToSurrender()
        {
            if (CanSurrender)
                Send(SurrenderAsk);
        }
        public void AgreeToSurrender()
        {
            if (CanSurrender)
                Send(SurrenderAgree);
        }
        public void RefuseToSurrender()
        {
            if (CanSurrender)
                Send(SurrenderRefuse);
        }

        SurrenderDesk Desk
        {
            get
            {
                if (surrenderDesk != null)
                    return surrenderDesk;
                surrenderDesk = new SurrenderDesk(Roster, () => Time.realtimeSinceStartupAsDouble,
                    id => players.TryGetValue(id, out var player) && player && player.Connected);
                surrenderDesk.Opened = (id, proposer, seconds, agreed) =>
                    SendToPlayer(id, SurrenderOpened, proposer, seconds, string.Join(",", agreed));
                surrenderDesk.Closed = (id, end, actor, seconds) => SendToPlayer(id, SurrenderClosed, end.ToString(), actor, seconds);
                surrenderDesk.Surrendered = SurrenderTeam;
                return surrenderDesk;
            }
        }

        void SendToPlayer(string id, string kind, params object[] values)
        {
            foreach (var entry in MultiplayerApp.Instance.ClientIdentities)
                if (entry.Value == id)
                    PhotonSession.Find(Runner).Send(PlayerRef.FromEncoded((int)entry.Key), Object, kind, values);
        }

        void SurrenderFrom(PlayerRef source, string kind)
        {
            if (!Started || Finished || !MultiplayerApp.Instance.ClientIdentities.TryGetValue((ulong)source.RawEncoded, out var id))
                return;
            if (kind == SurrenderAsk)
                Desk.Ask(id);
            else if (kind == SurrenderAgree)
                Desk.Agree(id);
            else if (kind == SurrenderRefuse)
                Desk.Refuse(id);
        }

        void SurrenderTeam(int team, string[] ids)
        {
            if (Finished)
                return;
            foreach (var id in ids)
                forfeitedPlayers.Add(id);
            surrendered = true;
            // The match may be waiting for an opponent to reconnect; that wait must not end the match a second time.
            reservations.Clear();
            Paused = false;
            Rules.Forfeit(team, SurrenderText.Reason(ids.Select(id => Roster.Find(id)?.name).Where(n => !string.IsNullOrEmpty(n)).ToArray()));
            Finish();
        }

        void ReceiveSurrender(string kind, JArray a)
        {
            if (a.Count != 3)
                return;
            if (kind == SurrenderOpened)
                Surrender.Show(a[0].Value<string>(), a[1].Value<double>(),
                    a[2].Value<string>()?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), Time.realtimeSinceStartupAsDouble);
            else if (kind == SurrenderClosed && Enum.TryParse(a[0].Value<string>(), out SurrenderEnd end))
                Surrender.Finish(end, a[1].Value<string>(), a[2].Value<double>());
        }
    }
}
