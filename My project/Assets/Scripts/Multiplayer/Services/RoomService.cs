using System;
using System.Linq;
using System.Threading.Tasks;

namespace PickleVerse.Multiplayer.Services
{
    /// <summary>Room use cases, independent of Canvas, buttons and Unity scene hierarchy.</summary>
    public sealed class RoomService : IRoomService
    {
        readonly IMultiplayerBackend backend;
        readonly IPlayerIdentityService identity;
        public IMultiplayerState State => backend;
        public RoomService(IMultiplayerBackend backend, IPlayerIdentityService identity)
        { this.backend = backend; this.identity = identity; }

        public Task OpenAsync(string profile, string name) => backend.ExecuteAsync(async () =>
        {
            backend.DisplayName = identity.ResolveName(name);
            await backend.PlayAsync(profile);
        });
        public Task SetNameAsync(string name) => backend.ExecuteAsync(() => backend.UpdateNameAsync(identity.ResolveName(name)));
        public Task SelectSinglesAsync() => Switch(PlayMode.Singles, false);
        public Task SelectDoublesAsync() => Switch(PlayMode.Doubles, false);
        public Task SelectCustomAsync(PlayMode mode) => Switch(mode, true);
        Task Switch(PlayMode mode, bool custom) => backend.ExecuteAsync(() => backend.SwitchRoomAsync(mode, custom));
        public Task JoinAsync(string code) => backend.ExecuteAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Nhập mã phòng trước.");
            return backend.JoinRoomAsync(code);
        });
        public Task SwapTeamAsync() => backend.ExecuteAsync(() =>
        {
            var me = backend.Members().FirstOrDefault(m => m.id == backend.PlayerId);
            if (me == null || !backend.Custom) throw new InvalidOperationException("Chỉ đổi đội trong custom.");
            return backend.SetTeamAsync(me.id, me.team == 0 ? 1 : 0);
        });
        public Task KickAsync(string id) => backend.ExecuteAsync(() => backend.KickAsync(id));
        public Task StartCustomAsync() => backend.ExecuteAsync(backend.StartCustomAsync);
        public Task LeaveAsync() => backend.ExecuteAsync(async () => { await backend.LeaveRoomAsync(); backend.SetNotice("Đã rời phòng."); });
    }
}
