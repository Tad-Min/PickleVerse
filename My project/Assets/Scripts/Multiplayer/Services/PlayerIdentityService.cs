using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Services;
using PickleVerse.Progression;

namespace PickleVerse.Multiplayer.Services
{
    public sealed class PlayerIdentityService : IPlayerIdentityService
    {
        public string Profile { get; private set; }
        string displayName = "";
        public string DisplayName
        {
            get => displayName;
            set { displayName = string.IsNullOrWhiteSpace(value)?"":ResolveName(value); }
        }
        public string PlayerId => AccountRuntime.Instance&&AccountRuntime.Instance.Account.IsSignedIn?AccountRuntime.Instance.Account.Profile.UserId.ToString("N"):"";

        public async Task InitializeAsync(string profile, string environment)
        {
            await ProgressionRuntime.Instance.EnsureReadyAsync();
            Profile=PlayerId;displayName=ProgressionRuntime.Instance.Player.playerName;
        }

        public string ResolveName(string name)
        {
            name = new string((name ?? "").Trim().Where(c => !char.IsControl(c) && c != '\u200b').Take(18).ToArray());
            if (name.Length > 0) return name;
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var bytes = Guid.NewGuid().ToByteArray();
            return new string(bytes.Take(4).Select(b => alphabet[b % alphabet.Length]).ToArray());
        }
    }
}
