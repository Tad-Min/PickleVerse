using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace PickleVerse.Multiplayer.Services
{
    public sealed class PlayerIdentityService : IPlayerIdentityService
    {
        public string Profile { get; private set; }
        string displayName = "";
        public string DisplayName
        {
            get => displayName;
            set { displayName = string.IsNullOrWhiteSpace(value)?"":ResolveName(value); if (PickleVerse.Progression.ProgressionRuntime.Exists && !PickleVerse.Progression.ProgressionRuntime.Instance.Online && displayName.Length>0) PickleVerse.Progression.ProgressionRuntime.Instance.SetName(displayName); }
        }
        public string PlayerId => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";

        public async Task InitializeAsync(string profile, string environment)
        {
            profile = new string((profile ?? "").Where(char.IsLetterOrDigit).Take(24).ToArray());
            if (profile.Length == 0) throw new InvalidOperationException("Thiếu hồ sơ người chơi.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile).SetEnvironmentName(environment));
            if (AuthenticationService.Instance.Profile != profile)
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.SwitchProfile(profile);
            }
            Profile = profile;
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
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
