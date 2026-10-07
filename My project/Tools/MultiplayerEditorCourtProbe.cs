using System.IO;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
public static class MultiplayerEditorCourtProbe
{
    public static async Task<string> Prepare(string receipt)
    {
        var app=MultiplayerApp.Instance;
        app.DisplayName="Editor Female"; app.Gender="Female";
        await app.PlayAsync("EditorTest"); await app.CreateCustomAsync(PlayMode.Singles); await app.ReadyAsync();
        File.WriteAllText(receipt+".code",app.Room.Code);
        return app.Room.Code;
    }
}
