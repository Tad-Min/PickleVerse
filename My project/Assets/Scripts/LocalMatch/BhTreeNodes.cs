using System;
using Unity.Behavior;
using Unity.Properties;
namespace PickleVerse.LocalMatch
{
    [Serializable] public abstract class BhTreeAction : Unity.Behavior.Action
    {
        protected BhTreeBot Bot => GameObject.GetComponent<BhTreeBot>();
        protected abstract bool Execute();
        protected override Status OnStart()=>Execute()?Status.Success:Status.Failure;
    }
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Chờ trận",category:"PickleVerse",id:"01b00000000000000000000000000001")]
    public partial class BhWait : BhTreeAction {protected override bool Execute()=>Bot.Wait();}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Giao bóng",category:"PickleVerse",id:"01b00000000000000000000000000002")]
    public partial class BhServe : BhTreeAction {protected override bool Execute()=>Bot.Serve();}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Smash hợp lệ",category:"PickleVerse",id:"01b00000000000000000000000000003")]
    public partial class BhSmash : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Smash);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Reset khi bị ép",category:"PickleVerse",id:"01b00000000000000000000000000004")]
    public partial class BhReset : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Reset);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Third-shot drop",category:"PickleVerse",id:"01b00000000000000000000000000005")]
    public partial class BhDrop : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Drop);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Dink gần lưới",category:"PickleVerse",id:"01b00000000000000000000000000006")]
    public partial class BhDink : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Dink);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Volley hợp lệ",category:"PickleVerse",id:"01b00000000000000000000000000007")]
    public partial class BhVolley : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Volley);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Drive sau nảy",category:"PickleVerse",id:"01b00000000000000000000000000008")]
    public partial class BhDrive : BhTreeAction {protected override bool Execute()=>Bot.Shot(ShotKind.Drive);}
    [Serializable,GeneratePropertyBag,NodeDescription(name:"Hồi vị",category:"PickleVerse",id:"01b00000000000000000000000000009")]
    public partial class BhRecover : BhTreeAction {protected override bool Execute()=>Bot.Recover();}
}
