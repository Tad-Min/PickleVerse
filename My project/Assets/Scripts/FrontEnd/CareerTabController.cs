using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    // Keeps the authored Career prefab's serialized tab bindings valid.
    public sealed class CareerTabController : MonoBehaviour
    {
        [Serializable] public struct Tab
        {
            public Button tabButton;
            public TextMeshProUGUI tabText;
            public GameObject contentPanel, underline;
        }
        public Tab[] tabs;
        public Color activeTextColor = Color.cyan, inactiveTextColor = new Color(.6f, .6f, .6f);
        UnityAction[] listeners;
        void Start()
        {
            if (tabs == null) return;
            listeners = new UnityAction[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                listeners[i] = () => SelectTab(index);
                if (tabs[i].tabButton) tabs[i].tabButton.onClick.AddListener(listeners[i]);
            }
            if (tabs.Length > 0) SelectTab(0);
        }
        public void SelectTab(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == index;
                if (tabs[i].contentPanel) tabs[i].contentPanel.SetActive(selected);
                if (tabs[i].underline) tabs[i].underline.SetActive(selected);
                if (tabs[i].tabText) tabs[i].tabText.color = selected ? activeTextColor : inactiveTextColor;
            }
        }
        void OnDestroy()
        {
            if (listeners == null) return;
            for (int i = 0; i < listeners.Length; i++)
                if (tabs[i].tabButton) tabs[i].tabButton.onClick.RemoveListener(listeners[i]);
        }
    }
}
