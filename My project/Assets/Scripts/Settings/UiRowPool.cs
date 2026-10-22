using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>Bounded by visible list sizes. Reopening and filtering reuse both row objects and their text.</summary>
    public sealed class UiRowPool : MonoBehaviour
    {
        readonly Dictionary<string,List<RectTransform>> shapes=new Dictionary<string,List<RectTransform>>();
        readonly Dictionary<string,int> cursors=new Dictionary<string,int>();
        readonly List<RectTransform> active=new List<RectTransform>();
        public int CreatedCount {get;private set;}
        public int ActiveCount=>active.Count;
        public void ReleaseAll()
        {
            foreach(var row in active)
            {
                foreach(var button in row.GetComponentsInChildren<Button>(true))button.onClick.RemoveAllListeners();
                row.gameObject.SetActive(false);
            }
            active.Clear();cursors.Clear();
        }
        public RectTransform Take(string shape,string name,float height)
        {
            if(!shapes.TryGetValue(shape,out var list)){list=new List<RectTransform>();shapes.Add(shape,list);}
            cursors.TryGetValue(shape,out int at);RectTransform row;
            if(at<list.Count)row=list[at];
            else
            {
                row=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(LayoutElement)).GetComponent<RectTransform>();
                row.SetParent(transform,false);list.Add(row);CreatedCount++;
            }
            cursors[shape]=at+1;row.name=name;row.SetAsLastSibling();row.gameObject.SetActive(true);
            var layout=row.GetComponent<LayoutElement>();layout.preferredHeight=layout.minHeight=height;layout.flexibleHeight=0;
            LobbyPanelStyle.SurfaceStyle(row);active.Add(row);return row;
        }
    }
}
