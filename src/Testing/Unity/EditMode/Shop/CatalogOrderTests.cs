using NUnit.Framework;
using UnityEngine;
using PickleVerse.Shop;

namespace PickleVerse.Tests
{
    public sealed class CatalogOrderTests
    {
        [Test] public void NewestProductDoesNotDependOnCatalogPosition()
        {var c=ScriptableObject.CreateInstance<ShopCatalog>();try{c.paddles=new[]{new PaddleOffer{releaseOrder=5},new PaddleOffer{releaseOrder=20},new PaddleOffer{releaseOrder=9}};Assert.That(c.NewestFirst(),Is.EqualTo(new[]{1,2,0}));}finally{Object.DestroyImmediate(c);}}
        [Test] public void EqualReleaseOrderKeepsStableCatalogOrder()
        {var c=ScriptableObject.CreateInstance<ShopCatalog>();try{c.paddles=new[]{new PaddleOffer{releaseOrder=5},new PaddleOffer{releaseOrder=5}};Assert.That(c.NewestFirst(),Is.EqualTo(new[]{0,1}));}finally{Object.DestroyImmediate(c);}}
        [Test] public void EmptyCatalogHasNoFeaturedProduct()
        {var c=ScriptableObject.CreateInstance<ShopCatalog>();try{c.paddles=System.Array.Empty<PaddleOffer>();Assert.That(c.NewestFirst(),Is.Empty);}finally{Object.DestroyImmediate(c);}}
    }
}
