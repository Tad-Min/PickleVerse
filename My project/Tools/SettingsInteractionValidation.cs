using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class SettingsInteractionValidation
{
    public static async Task<object> Run()
    {
        var ui=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();
        if(!Application.isPlaying||!ui)throw new InvalidOperationException("Open settings first.");
        var checks=new List<string>();
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {
            ui.ShowTab(3);ui.Keyboard();ui.BeginBinding((int)GameAction.Drive);
            await Task.Delay(120);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputSystem.Update();
            typeof(SettingsScreen).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            if(ui.Capturing||ui.Draft.KeyFor(GameAction.Drive)!=Key.F)throw new Exception("Physical key capture failed.");
            checks.Add("Physical F key captured by settings Update");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            ui.Touch();Canvas.ForceUpdateCanvases();
            var target=ui.touchBoard.TransformPoint(new Vector3(ui.touchBoard.rect.xMin+ui.touchBoard.rect.width*.65f,ui.touchBoard.rect.yMin+ui.touchBoard.rect.height*.35f,0));
            var camera=ui.GetComponent<Canvas>().worldCamera;
            var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,target)};
            e.pointerPressRaycast=new RaycastResult{module=ui.GetComponent<UnityEngine.UI.GraphicRaycaster>()};
            ExecuteEvents.Execute(ui.touchHandles[1].gameObject,e,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(ui.touchHandles[1].gameObject,e,ExecuteEvents.dragHandler);
            var expected=ui.Draft.leftHanded?.35f:.65f;
            if(Vector2.Distance(ui.Draft.touchPositions[1],new Vector2(expected,.35f))>.01f)throw new Exception("Drag event did not update normalized position.");
            checks.Add("Pointer drag updates touch position through event handlers");
            ui.Discard();ui.ShowTab(0);
            bool signedIn=Unity.Services.Core.UnityServices.State==Unity.Services.Core.ServicesInitializationState.Initialized&&Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
            if(!signedIn&&SettingsOverlay.Host?.CanSignOut==true)
            {
                ui.SignOut();if(!ui.confirmOverlay.activeSelf)throw new Exception("Logout confirmation missing.");
                ui.AcceptConfirm();await Task.Delay(300);
                if(!ui.status.text.StartsWith("Đã đăng xuất"))throw new Exception(ui.status.text);
                checks.Add("Guest logout executes lobby callback without creating authentication session");
            }
            var result=new{passed=true,count=checks.Count,checks=checks.ToArray()};
            Directory.CreateDirectory("../UnityWork/Settings");File.WriteAllText("../UnityWork/Settings/interaction-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{InputSystem.RemoveDevice(keyboard);ui.Discard();}
    }
}
