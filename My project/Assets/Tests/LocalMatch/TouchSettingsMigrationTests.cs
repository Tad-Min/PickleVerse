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
            d.Validate();Assert.AreEqual(2,d.version);
            for(int i=0;i<4;i++)Assert.AreEqual(SettingsData.Defaults().touchPositions[i],d.touchPositions[i]);
            var positions=(Vector2[])d.touchPositions.Clone();d.Validate();CollectionAssert.AreEqual(positions,d.touchPositions);
        }
        [Test] public void MissingOldPositionsUseNewDefaultsWithoutApplyingOffsetsTwice()
        {
            var d=SettingsData.Defaults();d.version=1;d.touchPositions=null;d.Validate();
            CollectionAssert.AreEqual(SettingsData.Defaults().touchPositions,d.touchPositions);
        }
    }
}
