using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class CareerTabController : MonoBehaviour
    {
        [System.Serializable]
        public struct Tab
        {
            public Button tabButton;
            public TextMeshProUGUI tabText;
            public GameObject contentPanel;
            public GameObject underline;
        }

        public Tab[] tabs;

        public Color activeTextColor = new Color(0.0f, 1.0f, 1.0f, 1.0f); // Cyan
        public Color inactiveTextColor = new Color(0.6f, 0.6f, 0.6f, 1.0f); // Dim Gray

        private void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i; // local copy for closure
                tabs[i].tabButton.onClick.AddListener(() => SelectTab(index));
            }

            if (tabs.Length > 0)
            {
                SelectTab(0); // Select first tab by default
            }
        }

        public void SelectTab(int index)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool isActive = (i == index);
                tabs[i].contentPanel.SetActive(isActive);
                if (tabs[i].underline != null)
                {
                    tabs[i].underline.SetActive(isActive);
                }
                
                if (tabs[i].tabText != null)
                {
                    tabs[i].tabText.color = isActive ? activeTextColor : inactiveTextColor;
                }
            }
        }
    }
}
