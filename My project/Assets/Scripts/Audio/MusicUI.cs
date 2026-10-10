using System;
using System.Collections.Generic;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PickleVerse.Audio
{
    // Close Esc after front-end controllers have observed the modal input scope.
    [DefaultExecutionOrder(20000)]
    public sealed class MusicUI : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform safeArea,mini,miniViewport,miniCaption,discTransform,playlistContent;
        public GameObject panel,miniTextRoot;
        public Image miniDisc,disc,accent;
        public TMP_Text miniTitle,miniState,title,artist,genre,status,elapsed,duration,playLabel,libraryCount;
        public Button miniButton,closeButton,previousButton,playButton,nextButton,settingsButton;
        public Slider progress;
        public MusicTrackRow rowPrefab;
        public bool IsOpen=>panel.activeSelf;
        public float MarqueeOffset=>miniCaption.anchoredPosition.x;
        readonly List<MusicTrackRow> rows=new List<MusicTrackRow>();
        MusicPlayer player;
        GameObject previousSelection;
        string scene;
        string displayedTrack;
        float marqueeTime,textWidth,progressTick;
        int previousSeconds=-1;
        Rect lastSafeArea;
        Vector2 lastResolution;
        void Awake()
        {
            panel.SetActive(false);
            miniButton.onClick.AddListener(Open);closeButton.onClick.AddListener(Close);
            previousButton.onClick.AddListener(()=>player.Previous());nextButton.onClick.AddListener(()=>player.Next());playButton.onClick.AddListener(()=>player.TogglePause());
            settingsButton.onClick.AddListener(()=>{Close();SettingsOverlay.Open();});
            progress.onValueChanged.AddListener(value=>player?.Seek(value));
        }
        public void Bind(MusicPlayer value)
        {
            player=value;player.Changed+=Refresh;
            libraryCount.text="THƯ VIỆN  /  "+player.Library.tracks.Length+" BÀI";
            foreach(var track in player.Library.tracks)
            {
                int index=rows.Count;var row=Instantiate(rowPrefab,playlistContent);row.gameObject.SetActive(true);row.Bind(track,index,()=>player.Select(index));rows.Add(row);
            }
            Refresh();
        }
        public void ApplyScene(string name)
        {
            if(scene!=name)Close();scene=name;
            miniTextRoot.SetActive(name=="Menu");
            mini.anchorMin=mini.anchorMax=name=="Menu"?new Vector2(.045f,.21f):new Vector2(.975f,.28f);
            mini.pivot=name=="Menu"?new Vector2(0,.5f):new Vector2(1,.5f);
            mini.sizeDelta=new Vector2(name=="Menu"?286:48,48);mini.anchoredPosition=Vector2.zero;
            Visibility();BindCamera();
        }
        public void Open()
        {
            if(player==null||!player.Allowed||SettingsOverlay.IsOpen||SettingsOverlay.Loading||!FrontEndModalInput.TryAcquire(this))return;
            previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            panel.SetActive(true);Refresh();Canvas.ForceUpdateCanvases();
            foreach(var text in panel.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }
        public void Close()
        {
            if(panel)panel.SetActive(false);FrontEndModalInput.Release(this);
            if(EventSystem.current&&previousSelection&&previousSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection=null;
        }
        void Refresh()
        {
            if(player==null||player.Track==null)return;var track=player.Track;
            if(displayedTrack!=track.id)
            {
                displayedTrack=track.id;
                miniDisc.sprite=disc.sprite=track.disc;discTransform.localRotation=Quaternion.identity;miniDisc.rectTransform.localRotation=Quaternion.identity;
                miniTitle.text=track.title+"  •  "+track.artist+"  •  "+track.genre;marqueeTime=0;miniCaption.anchoredPosition=Vector2.zero;
                textWidth=miniTitle.GetPreferredValues(miniTitle.text).x;miniCaption.sizeDelta=new Vector2(Mathf.Max(miniViewport.rect.width,textWidth),miniCaption.sizeDelta.y);
            }
            title.text=track.title;artist.text=track.artist;genre.text=track.genre.ToUpperInvariant();accent.color=track.accent;
            playLabel.text=player.UserPaused?"PHÁT":"TẠM DỪNG";
            miniState.text=player.Loading?"ĐANG TẢI":player.UserPaused?"ĐÃ TẠM DỪNG":"ĐANG PHÁT";
            status.text=!string.IsNullOrEmpty(player.Error)?player.Error:player.Loading?"Đang chuẩn bị bài nhạc…":player.UserPaused?"Nhấn Phát để tiếp tục.":"Tự động phát tiếp • Nhạc dừng khi vào trận đấu";
            duration.text=TimeText(player.Duration);
            for(int i=0;i<rows.Count;i++)rows[i].Select(i==player.CurrentIndex,!player.UserPaused&&!player.Loading);
            previousSeconds=-1;
        }
        void Update()
        {
            if(player==null)return;
            Visibility();if(!canvas.enabled)return;
            BindCamera();ApplySafeArea();
            if(IsOpen&&Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
            bool spinning=player.Playing&&!player.Loading;
            if(spinning){float angle=-18*Time.unscaledDeltaTime;miniDisc.rectTransform.Rotate(0,0,angle);if(IsOpen)discTransform.Rotate(0,0,angle);}
            if(scene=="Menu"&&!IsOpen)
            {
                float overflow=textWidth-miniViewport.rect.width;
                if(overflow>8)
                {
                    marqueeTime+=Time.unscaledDeltaTime;float travel=overflow/28;
                    float t=Mathf.Repeat(marqueeTime,travel+3);float x=t<1.5f?0:t<travel+1.5f?-28*(t-1.5f):-overflow;
                    miniCaption.anchoredPosition=new Vector2(x,0);
                }
            }
            if(!IsOpen)return;
            progressTick+=Time.unscaledDeltaTime;if(progressTick<.1f)return;progressTick=0;
            int seconds=(int)player.Position;if(seconds!=previousSeconds){previousSeconds=seconds;elapsed.text=TimeText(player.Position);}
            progress.SetValueWithoutNotify(player.Duration>0?player.Position/player.Duration:0);
        }
        void Visibility()
        {
            bool visible=player!=null&&player.Allowed&&!SettingsOverlay.IsOpen&&!SettingsOverlay.Loading;
            if(!visible&&IsOpen)Close();if(canvas.enabled!=visible)canvas.enabled=visible;
        }
        void BindCamera()
        {
            var camera=Camera.main;if(!camera)return;
            if(canvas.worldCamera!=camera){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.025f;}
        }
        void ApplySafeArea()
        {
            var resolution=new Vector2(Screen.width,Screen.height);var area=Screen.safeArea;
            if(resolution==lastResolution&&area==lastSafeArea||resolution.x<=0||resolution.y<=0)return;
            lastResolution=resolution;lastSafeArea=area;safeArea.anchorMin=new Vector2(area.xMin/resolution.x,area.yMin/resolution.y);safeArea.anchorMax=new Vector2(area.xMax/resolution.x,area.yMax/resolution.y);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        static string TimeText(float value){int seconds=Mathf.Max(0,(int)value);return(seconds/60)+":"+(seconds%60).ToString("00");}
        void OnDestroy(){Close();if(player!=null)player.Changed-=Refresh;}
    }
}
