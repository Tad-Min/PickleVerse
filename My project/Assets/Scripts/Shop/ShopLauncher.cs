using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Shop
{
    public sealed class ShopLauncher : MonoBehaviour
    {public void OpenShop(){SceneManager.LoadScene("Shop");}public void OpenCollection(){SceneManager.LoadScene("Collection");}}
}
