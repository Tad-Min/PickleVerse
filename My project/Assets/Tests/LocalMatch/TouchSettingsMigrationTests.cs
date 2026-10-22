using NUnit.Framework;
using PickleVerse.Settings;
using UnityEngine;

namespace PickleVerse.LocalMatch.Tests
{
    public sealed class TouchSettingsMigrationTests
    {
        [Test] public void OldDefaultsMigrateOnceToFourButtonFan()
        {
            var d=SettingsData.Defaults();d.version=1;
            d.touchPositions[0]=new Vector2(.08f,.15f);d.touchPositions[1]=new Vector2(.944f,.085f);
            d.touchPositions[2]=new Vector2(.858f,.085f);d.touchPositions[3]=new Vector2(.772f,.085f);
            d.Validate();Assert.AreEqual(3,d.version);
            for(int i=0;i<4;i++)Assert.AreEqual(SettingsData.Defaults().touchPositions[i],d.touchPositions[i]);
            var positions=(Vector2[])d.touchPositions.Clone();d.Validate();CollectionAssert.AreEqual(positions,d.touchPositions);
        }
        [Test] public void MissingOldPositionsUseNewDefaultsWithoutApplyingOffsetsTwice()
        {
            var d=SettingsData.Defaults();d.version=1;d.touchPositions=null;d.Validate();
            CollectionAssert.AreEqual(SettingsData.Defaults().touchPositions,d.touchPositions);
        }
        [Test] public void PreviousFanKeepsCustomOffsetsAndGetsAnIndependentRunButton()
        {
            var d=SettingsData.Defaults();d.version=2;
            var previous=new[]{new Vector2(.14f,.277f),new Vector2(.846f,.277f),new Vector2(.683f,.405f),new Vector2(.851f,.541f)};
            for(int i=0;i<4;i++)d.touchPositions[i]=previous[i]+new Vector2(-.02f,.01f);
            d.touchPositions[4]=new Vector2(.4f,.4f);d.Validate();
            var defaults=SettingsData.Defaults();
            for(int i=0;i<4;i++)Assert.Less(Vector2.Distance(defaults.touchPositions[i]+new Vector2(-.02f,.01f),d.touchPositions[i]),.0001f);
            Assert.AreEqual(defaults.touchPositions[4],d.touchPositions[4]);
            var once=(Vector2[])d.touchPositions.Clone();d.Validate();CollectionAssert.AreEqual(once,d.touchPositions);
        }
    }
}
