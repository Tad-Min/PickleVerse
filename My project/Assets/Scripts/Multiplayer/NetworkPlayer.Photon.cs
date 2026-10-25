using Fusion;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        void Send(string kind, params object[] values) => PhotonSession.Find(Runner).Send(Object.StateAuthority, Object, kind, values);
        void Broadcast(string kind, params object[] values)
        {
            foreach (var peer in Runner.ActivePlayers)
                PhotonSession.Find(Runner).Send(peer, Object, kind, values);
        }
        static Vector3 V(JArray a, int i) => new Vector3(a[i].Value<float>(), a[i + 1].Value<float>(), a[i + 2].Value<float>());
        public void ReceivePhotonMessage(PlayerRef source, string kind, string payload)
        {
            var a = JArray.Parse(payload);
            if (HasStateAuthority && source == ControllerPeer)
            {
                if (kind == "shot" && a.Count == 6)
                    ShotFrom(a[0].Value<int>(), a[1].Value<bool>(), a[2].Value<bool>(), new Vector2(a[3].Value<float>(), a[4].Value<float>()), a[5].Value<bool>());
                else if (kind == "timeout" && a.Count == 0)
                    TimeoutFrom();
                else if (kind == "resume-timeout" && a.Count == 0)
                    ResumeTimeoutFrom();
            }
            if (source != Object.StateAuthority)
                return;
            if (kind == "teleport" && a.Count == 5)
                TeleportEffect(V(a, 0), a[3].Value<uint>(), a[4].Value<int>());
            else if (kind == "swing" && a.Count == 8)
                SwingEffect(a[0].Value<int>(), a[1].Value<bool>(), a[2].Value<bool>(), V(a, 3), a[6].Value<float>(), a[7].Value<double>());
            else if (kind == "contact" && a.Count == 10)
                ContactEffect(a[0].Value<bool>(), V(a, 1), a[4].Value<bool>(), V(a, 5), a[8].Value<double>(), a[9].Value<byte>());
            else if (kind == "split" && a.Count == 0)
                SplitStepEffect();
            else if (kind == "react" && a.Count == 2)
                ReactEffect(a[0].Value<bool>(), a[1].Value<bool>());
        }
        void ShotServerRpc(int kind, bool attack, bool cross, Vector2 axes, bool run) => Send("shot", kind, attack, cross, axes.x, axes.y, run);
        void TimeoutServerRpc() => Send("timeout");
        void ResumeTimeoutServerRpc() => Send("resume-timeout");
        void TeleportClientRpc(Vector3 p, uint sequence, int end) => Broadcast("teleport", p.x, p.y, p.z, sequence, end);
        void SwingClientRpc(int kind, bool attack, bool cross, Vector3 p, float delay, double time) => Broadcast("swing", kind, attack, cross, p.x, p.y, p.z, delay, time);
        void ContactClientRpc(bool volley, Vector3 p, bool successful, Vector3 velocity, double time, byte grade) => Broadcast("contact", volley, p.x, p.y, p.z, successful, velocity.x, velocity.y, velocity.z, time, grade);
        void SplitStepClientRpc() => Broadcast("split");
        void ReactClientRpc(bool won, bool over) => Broadcast("react", won, over);
    }
}
