using NUnit.Framework;
using UnityEngine;
using PickleVerse.LocalMatch;

namespace PickleVerse.Tests
{
    public sealed class PaddleContactTests
    {
        [Test] public void FastBallCrossingPaddleIsDetectedBetweenSamples()
        {
            Assert.That(PaddleContact.Distance(Vector3.back,Vector3.forward,Vector3.zero,Vector3.zero),Is.LessThan(.00001f));
        }
        [Test] public void SwingCrossingBallIsDetectedBetweenSamples()
        {
            Assert.That(PaddleContact.Distance(Vector3.zero,Vector3.zero,Vector3.left,Vector3.right),Is.LessThan(.00001f));
        }
        [Test] public void ObjectsPassingSameLocationAtDifferentTimesDoNotContact()
        {
            Assert.That(PaddleContact.Distance(Vector3.zero,Vector3.right,Vector3.right,Vector3.right*2),Is.EqualTo(1).Within(.00001f));
        }
        [Test] public void StationaryNearMissRetainsItsRealDistance()
        {
            Assert.That(PaddleContact.Distance(Vector3.up,Vector3.up,Vector3.zero,Vector3.zero),Is.EqualTo(1).Within(.00001f));
        }
        [TestCase(0f)][TestCase(.1f)][TestCase(.5f)][TestCase(1f)]
        public void TranslatingCourtDoesNotChangeContact(float fraction)
        {
            Vector3 a=new Vector3(-.2f,1,-2),b=a+Vector3.forward,face=a+Vector3.forward*fraction;
            var shift=new Vector3(37,2,-19);
            Assert.That(PaddleContact.Distance(a,b,face,face),Is.EqualTo(PaddleContact.Distance(a+shift,b+shift,face+shift,face+shift)).Within(.00001f));
        }
    }
}
