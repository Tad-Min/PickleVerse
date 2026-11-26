using System.IO;
using System.Linq;
using PickleVerse.Shop;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.VFX.Editor
{
    /// <summary>
    /// Assembles the Blue Phoenix paddle from its Blender export (ArtSource/Paddles/BluePhoenix): textures, materials, the
    /// shop, match and replay prefabs, and its offer in the shop catalogue. Menu: PickleVerse/Shop/Rebuild Blue Phoenix paddle.
    /// The offer is only the game's copy: the shop sells it once an admin publishes it (web admin, shop items).
    /// </summary>
    public static class BluePhoenixPaddleBuilder
    {
        public const string Id = "paddle.bluephoenix";
        const string Models = "Assets/Models/Paddle/BluePhoenix", Materials = "Assets/ShopContent/Materials", Prefabs = "Assets/ShopContent/Prefabs",
            Thumbnail = "Assets/ShopContent/Thumbnails/paddle.bluephoenix.png", Replay = "Assets/Resources/ReplayEquipment/paddle.bluephoenix.prefab",
            CatalogPath = "Assets/Resources/Shop/Shop Catalog.asset";
        /// <summary>Height of the model in metres and of every shop model in preview units.</summary>
        const float ModelHeight = .4118f, PreviewHeight = 2f;
        /// <summary>The Blender front (-y) arrives as +z; the shop camera looks along +z, so the paddle turns to face it.</summary>
        const float FrontYaw = 180;

        [MenuItem("PickleVerse/Shop/Rebuild Blue Phoenix paddle")]
        public static string Build()
        {
            var face = Material("Blue Phoenix Face", "Face");
            var grip = Material("Blue Phoenix Grip", "Grip");
            var model = Model();
            var preview = Preview(model, face, grip);
            var gameplay = Gameplay(preview);
            SaveReplay(gameplay);
            var sprite = ThumbnailSprite();
            Offer(preview, gameplay, sprite);
            AssetDatabase.SaveAssets();
            return "Blue Phoenix: " + AssetDatabase.GetAssetPath(preview) + ", thumbnail " + (sprite ? "ok" : "missing");
        }

        /// <summary>The paddle and its ball effect in one go (batch: -executeMethod ...BluePhoenixPaddleBuilder.BuildWithEffect).</summary>
        [MenuItem("PickleVerse/Shop/Rebuild Blue Phoenix paddle and effect")]
        public static void BuildWithEffect()
        {
            Debug.Log("[BluePhoenix] " + Build());
            Debug.Log("[BluePhoenix] " + PaddleSkinVfxBuilder.BuildBluePhoenixEffect());
        }

        static Texture2D Texture(string name, bool normal, bool linear, int max)
        {
            string path = Models + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !linear;
            importer.alphaSource = linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = max;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = max / 2;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string name, string part)
        {
            string path = Materials + "/" + name + ".mat";
            var shader = Shader.Find("PickleVerse/Paddle/Blue Phoenix");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", Texture("T_BluePhoenix_" + part + "_BaseColor", false, false, 2048));
            material.SetTexture("_BumpMap", Texture("T_BluePhoenix_" + part + "_Normal", true, true, 2048));
            material.SetTexture("_MaskMap", Texture("T_BluePhoenix_" + part + "_Mask", false, true, 1024));
            material.SetFloat("_BumpScale", 1);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Model()
        {
            string path = Models + "/BluePhoenixPaddle.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = importer.importLights = importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Upright, centred, two units tall, painted front and phoenix pommel towards the shop camera.</summary>
        static GameObject Preview(GameObject model, Material face, Material grip)
        {
            var root = new GameObject("BluePhoenix");
            float scale = PreviewHeight / ModelHeight;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var part = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = new Vector3(0, -PreviewHeight / 2, 0);
                part.transform.localRotation = Quaternion.Euler(0, FrontYaw, 0);
                part.transform.localScale = Vector3.one * scale;
                part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.GetComponent<MeshRenderer>().sharedMaterial = filter.name.Contains("Grip") ? grip : face;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/BluePhoenix.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Same fit as the other skins: the head covers the standard contact surface and the grip sits in the hand.</summary>
        static GameObject Gameplay(GameObject preview)
        {
            var root = new GameObject("BLUEPHOENIXGameplay");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(preview, root.transform);
            instance.transform.localScale = Vector3.one * 1.21f;
            instance.transform.localPosition = new Vector3(0, 0, -.12f);
            instance.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/BLUEPHOENIXGameplay.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void SaveReplay(GameObject gameplay)
        {
            var clone = Object.Instantiate(gameplay);
            clone.name = Id;
            PrefabUtility.SaveAsPrefabAsset(clone, Replay);
            Object.DestroyImmediate(clone);
        }

        static Sprite ThumbnailSprite()
        {
            if (!File.Exists(Thumbnail))
                return null;
            var importer = (TextureImporter)AssetImporter.GetAtPath(Thumbnail);
            var reference = (TextureImporter)AssetImporter.GetAtPath("Assets/ShopContent/Thumbnails/paddle.gemstone.png");
            EditorUtility.CopySerializedIfDifferent(reference, importer);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(Thumbnail).OfType<Sprite>().FirstOrDefault();
        }

        static void Offer(GameObject preview, GameObject gameplay, Sprite sprite)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            var offer = catalog.paddles.FirstOrDefault(p => p.id == Id);
            if (offer == null)
            {
                offer = new PaddleOffer { id = Id };
                catalog.paddles = catalog.paddles.Append(offer).ToArray();
            }
            offer.title = "BLUE PHOENIX";
            offer.collection = "ELITE COLLECTION";
            offer.description = "Phượng hoàng lửa xanh chạm nổi giữa mây bạc trên nền đá đen. Cán bọc da quấn dải lửa xanh và dây bạc, chuôi khắc biểu tượng phượng hoàng.";
            offer.tokenpadPrice = 0;
            offer.elitePrice = 900;
            offer.gemstone = true;
            offer.releaseOrder = 4;
            offer.previewPrefab = preview;
            offer.gameplayPrefab = gameplay;
            offer.thumbnail = sprite;
            offer.effectTitle = "LAM PHƯỢNG TUNG CÁNH";
            offer.effectDetails = "Đánh trúng bóng, phượng hoàng lửa xanh dang rộng đôi cánh lao ra từ mặt vợt và vỗ cánh bay theo sau bóng.\n" +
                "Đuôi lông vũ dài kéo vệt lửa xanh, song song là dải khói lửa đen; tro đen điểm than xanh rơi từ đôi cánh.\n" +
                "Bóng rực sáng xanh điện; vòng lửa xanh cùng hình phượng hoàng bùng lên tại điểm chạm.\n" +
                "Chỉ xuất hiện sau cú đánh của người cầm vợt này, khi thi đấu và luyện tập. Đối thủ đánh trả, phượng hoàng tan thành tro đen.";
            EditorUtility.SetDirty(catalog);
        }
    }
}
