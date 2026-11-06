using NUnit.Framework;
using PickleVerse.Progression;

public sealed class QuickCoachingRulesTests
{
    [Test] public void MissingAndEarlierRallyDataDoNotProduceTips()
    {
        Assert.That(QuickCoachingRules.Tip(null), Is.Empty);
        Assert.That(QuickCoachingRules.Tip(new[] { new ShotTelemetry { outcome = "Net" } }, 1), Is.Empty);
        Assert.That(QuickCoachingRules.Tip(new[] { new ShotTelemetry() }), Is.Empty);
    }
    [Test] public void AssistedTimingIsNotAHumanTechniqueFinding()
    {
        var shot = new ShotTelemetry { outcome = "LateServe", timingError = .3f, assisted = true };
        Assert.That(QuickCoachingRules.Tip(new[] { shot }), Is.Empty);
        shot.assisted = false;
        Assert.That(QuickCoachingRules.Tip(new[] { shot }), Does.Contain("trễ"));
    }
    [Test] public void StandingInKitchenRequiresAVolleyToBeAFault()
    {
        var shot = new ShotTelemetry { kitchen = true, volley = false };
        Assert.That(QuickCoachingRules.Tip(new[] { shot }), Is.Empty);
        shot.volley = true;
        Assert.That(QuickCoachingRules.Tip(new[] { shot }), Does.Contain("vô lê"));
    }
}
