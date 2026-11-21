using NUnit.Framework;
using PickleVerse.Settings;

public sealed class SceneWarmupTests
{
    [Test] public void DoesNotFinishFromOneFastFrame()
    {var gate=new SceneWarmup();gate.Observe(1f/60);Assert.That(gate.Ready(60),Is.False);}
    [Test] public void SmoothFramesPrepareScene()
    {var gate=new SceneWarmup();for(int i=0;i<60;i++)gate.Observe(1f/60);Assert.That(gate.Ready(120),Is.True);}
    [Test] public void ActivationSpikeMustLeaveWindowBeforeReveal()
    {
        var gate=new SceneWarmup();for(int i=0;i<60;i++)gate.Observe(1f/60);gate.Observe(.4f);
        Assert.That(gate.Ready(60),Is.False);for(int i=0;i<36;i++)gate.Observe(1f/60);Assert.That(gate.Ready(60),Is.True);
    }
    [Test] public void IntentionallyCappedThirtyFpsIsReady()
    {var gate=new SceneWarmup();for(int i=0;i<40;i++)gate.Observe(1f/30);Assert.That(gate.Ready(30),Is.True);}
    [Test] public void SlowerDeviceMustSettleBeforeReveal()
    {
        var gate=new SceneWarmup();for(int i=0;i<100;i++)gate.Observe(1f/30);Assert.That(gate.Ready(60),Is.False);
        for(int i=0;i<100;i++)gate.Observe(1f/30);Assert.That(gate.Ready(60),Is.True);
    }
    [Test] public void AlternatingStallsDoNotBecomeReadyJustBecauseTimePassed()
    {var gate=new SceneWarmup();for(int i=0;i<400;i++)gate.Observe(i%2==0?.01f:.045f);Assert.That(gate.Ready(60),Is.False);}
    [Test] public void InvalidSamplesCannotAdvanceLoading()
    {var gate=new SceneWarmup();gate.Observe(float.NaN);gate.Observe(float.PositiveInfinity);gate.Observe(0);gate.Observe(-1);Assert.That(gate.Elapsed,Is.Zero);Assert.That(gate.Ready(60),Is.False);}
}
