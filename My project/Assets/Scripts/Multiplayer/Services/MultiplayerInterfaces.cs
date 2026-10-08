using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PickleVerse.Multiplayer.Services
{
    /// <summary>Names are public player aliases. Authentication IDs remain stable for reconnect.</summary>
    public interface IPlayerIdentityService
    {
        string PlayerId { get; }
        string Profile { get; }
        string DisplayName { get; set; }
        Task InitializeAsync(string profile, string environment);
        string ResolveName(string name);
    }

    public interface IMultiplayerState
    {
        event Action Changed;
        FlowState State { get; }
        PlayMode Mode { get; }
        bool Custom { get; }
        bool IsCaptain { get; }
        bool Busy { get; }
        bool Searching { get; }
        string PlayerId { get; }
        string DisplayName { get; set; }
        string Notice { get; }
        string Elapsed { get; }
        string RoomCode { get; }
        List<RoomMember> Members();
    }

    /// <summary>Unity Sessions/Netcode boundary. Views never call this adapter directly.</summary>
    public interface IMultiplayerBackend : IMultiplayerState
    {
        Task PlayAsync(string profile);
        Task SwitchRoomAsync(PlayMode mode, bool custom);
        Task JoinRoomAsync(string code);
        Task UpdateNameAsync(string name);
        Task SetTeamAsync(string playerId, int team);
        Task KickAsync(string playerId);
        Task StartCustomAsync();
        Task LeaveRoomAsync();
        Task FindAsync();
        Task CancelSearchAsync();
        Task ExecuteAsync(Func<Task> action);
        void SetNotice(string message);
    }

    public interface IRoomService
    {
        IMultiplayerState State { get; }
        Task OpenAsync(string profile, string name);
        Task SetNameAsync(string name);
        Task SelectSinglesAsync();
        Task SelectDoublesAsync();
        Task SelectCustomAsync(PlayMode mode);
        Task JoinAsync(string code);
        Task SwapTeamAsync();
        Task KickAsync(string playerId);
        Task StartCustomAsync();
        Task LeaveAsync();
    }

    public interface IMatchmakingService
    {
        IMultiplayerState State { get; }
        Task FindAsync();
        Task CancelAsync();
    }
}
