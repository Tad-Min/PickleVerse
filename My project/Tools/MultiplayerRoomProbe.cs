using System;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
public static class MultiplayerRoomProbe
{
    public static async Task<string> Run()
    {
        var app=MultiplayerApp.Instance;
        await app.SelectModeAsync(PlayMode.Doubles);
        string code=app.Room.Code;
        await app.FindAsync();
        await Task.Delay(2000);
        string early=app.State+" "+app.Elapsed;
        await app.CancelSearchAsync();
        await app.SelectModeAsync(PlayMode.Singles);
        await app.FindAsync();
        await Task.Delay(7000);
        string later=app.State+" "+app.Elapsed+" "+app.Notice;
        await app.CancelSearchAsync();
        return "Duo room="+code+"; early="+early+"; later="+later+"; cancelled="+app.State;
    }
}
