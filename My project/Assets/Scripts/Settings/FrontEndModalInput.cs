using UnityEngine;

namespace PickleVerse.Settings
{
    /// <summary>Keyboard navigation belongs to a front-end modal until it closes.</summary>
    public static class FrontEndModalInput
    {
        static object owner;
        public static bool IsOpen=>owner!=null;
        public static bool TryAcquire(object target)
        {if(owner!=null&&!ReferenceEquals(owner,target))return false;owner=target;return true;}
        public static void Release(object target){if(ReferenceEquals(owner,target))owner=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){owner=null;}
    }
}
