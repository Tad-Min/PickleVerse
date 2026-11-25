using System.IO;
using System.Linq;
using PickleVerse.Shop;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.VFX.Editor
{
    /// <summary>
    /// Assembles the Dragon Ruby paddle from its Blender export (ArtSource/Paddles/DragonRuby): textures, materials, the shop,
    /// match and replay prefabs, and its offer in the shop catalogue. Menu: PickleVerse/Shop/Rebuild Dragon Ruby paddle.
    /// </summary>
    public static class DragonRubyPaddleBuilder
    {
        public const string Id = "paddle.dragonruby";
        const string Models = "Assets/Models/Paddle/DragonRuby", Materials = "Assets/ShopContent/Materials", Prefabs = "Assets/ShopContent/Prefabs",
            Thumbnail = "Assets/ShopContent/Thumbnails/paddle.dragonruby.png", Replay = "Assets/Resources/ReplayEquipment/paddle.dragonruby.prefab",
            CatalogPath = "Assets/Resources/Shop/Shop Catalog.asset";
        /// <summary>Height of the model in metres and of every shop model in preview units.</summary>
        const float ModelHeight = .4118f, PreviewHeight = 2f;

        [MenuItem("PickleVerse/Shop/Rebuild Dragon Ruby paddle")]
        public static string Build()
        {
            var face = Material("Dragon Ruby Face", "Face", 1.0f);
            var grip = Material("Dragon Ruby Grip", "Grip", 1.0f);
            var model = Model();
            var preview = Preview(model, face, grip);
            var gameplay = Gameplay(preview);
            SaveReplay(gameplay);
            var sprite = ThumbnailSprite();
            Offer(preview, gameplay, sprite);
            AssetDatabase.SaveAssets();
            return "Dragon Ruby: " + AssetDatabase.GetAssetPath(preview) + ", thumbnail " + (sprite ? "ok" : "missing");
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

        static Material Material(string name, string part, float bump)
        {
            string path = Materials + "/" + name + ".mat";
            var shader = Shader.Find("PickleVerse/Paddle/Dragon Ruby");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", Texture("T_DragonRuby_" + part + "_BaseColor", false, false, 2048));
            material.SetTexture("_BumpMap", Texture("T_DragonRuby_" + part + "_Normal", true, true, 2048));
            material.SetTexture("_MaskMap", Texture("T_DragonRuby_" + part + "_Mask", false, true, 1024));
            material.SetFloat("_BumpScale", bump);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Model()
        {
            string path = Models + "/DragonRubyPaddle.fbx";
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

        /// <summary>Upright, centred, two units tall, painted front and dragon pommel towards the shop camera.</summary>
        static GameObject Preview(GameObject model, Material face, Material grip)
        {
            var root = new GameObject("DragonRuby");
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
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/DragonRuby.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>The Blender front (-y) arrives as +z; the shop camera looks along +z, so the paddle turns to face it.</summary>
        const float FrontYaw = 180;

        /// <summary>Same fit as the other skins: the head covers the standard contact surface and the grip sits in the hand.</summary>
        static GameObject Gameplay(GameObject preview)
        {
            var root = new GameObject("DRAGONRUBYGameplay");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(preview, root.transform);
            instance.transform.localScale = Vector3.one * 1.21f;
            instance.transform.localPosition = new Vector3(0, 0, -.12f);
            instance.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/DRAGONRUBYGameplay.prefab");
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
            offer.title = "DRAGON RUBY";
            offer.collection = "ELITE COLLECTION";
            offer.description = "Rồng phương Đông chạm nổi trên mặt sơn mài đen. Thân rồng hồng ngọc quấn quanh cán bọc da, đầu rồng sừng vàng ngự ở chuôi.";
            offer.tokenpadPrice = 0;
            offer.elitePrice = 800;
            offer.gemstone = true;
            offer.releaseOrder = 3;
            offer.previewPrefab = preview;
            offer.gameplayPrefab = gameplay;
            offer.thumbnail = sprite;
            offer.effectTitle = "XÍCH LONG TRUY CHÂU";
            offer.effectDetails = "Đánh trúng bóng, rồng đỏ lao ra từ mặt vợt và đuổi theo bóng.\n" +
                "Thân rồng uốn lượn theo đường sóng hình sin, kéo theo vệt lửa đỏ và dải vàng đối xứng.\n" +
                "Bóng hóa viên ngọc rực sáng; đầu rồng gầm cùng vòng lửa bùng lên tại điểm chạm.\n" +
                "Chỉ xuất hiện sau cú đánh của người cầm vợt này, khi thi đấu và luyện tập. Đối thủ đánh trả, rồng tan thành tro lửa.";
            EditorUtility.SetDirty(catalog);
        }
    }
}
