using UnityEngine;

namespace PickleVerse.Shop
{
    /// <summary>Cosmetic child only: preserves the paddle socket, collider and ball-contact transforms.</summary>
    public sealed class PaddleAppearance : MonoBehaviour
    {
        public Vector3 modelPosition, modelEuler;
        public float modelScale = 1;
        Renderer[] originals; bool[] originalEnabled;
        GameObject skin;
        public string CurrentId { get; private set; } = ShopCatalog.DefaultPaddleId;
        public GameObject SkinVisual => skin;
        public static PaddleAppearance Attach(GameObject paddle)
            => paddle.GetComponent<PaddleAppearance>() ?? paddle.AddComponent<PaddleAppearance>();
        void Cache()
        {
            if (originals != null)
                return;
            originals = GetComponentsInChildren<Renderer>(true);
            originalEnabled = new bool[originals.Length];
            for (int i = 0; i < originals.Length; i++)
                originalEnabled[i] = originals[i].enabled;
        }
        public void Apply(string id)
        {
            Cache();
            var offer = PlayerEconomy.Catalog.Find(id);
            if (id != ShopCatalog.DefaultPaddleId && (!offer?.gameplayPrefab))
                id = ShopCatalog.DefaultPaddleId;
            if (CurrentId == id && (skin || id == ShopCatalog.DefaultPaddleId))
                return;
            if (skin)
            {
                skin.SetActive(false);
                Destroy(skin);
                skin = null;
            }
            CurrentId = id;
            for (int i = 0; i < originals.Length; i++)
                if (originals[i])
                    originals[i].enabled = id == ShopCatalog.DefaultPaddleId && originalEnabled[i];
            if (id == ShopCatalog.DefaultPaddleId)
                return;
            skin = Instantiate(offer.gameplayPrefab, transform, false);
            skin.name = "Equipped Paddle / " + offer.title;
            skin.transform.localPosition = modelPosition;
            skin.transform.localRotation = Quaternion.Euler(modelEuler);
            skin.transform.localScale = Vector3.one * modelScale;
            foreach (var t in skin.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = gameObject.layer;
        }
    }
}
