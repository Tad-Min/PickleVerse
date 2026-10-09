using UnityEngine;

namespace PickleVerse.Audio
{
    /// <summary>Distance-based steps: no footsteps while blocked, idle, paused or teleported.</summary>
    public sealed class MovementFoley
    {
        Vector3 previous;
        bool initialized,moving,wasRunning;
        float distance;
        public void Reset(){initialized=moving=wasRunning=false;distance=0;}
        public void Step(Vector3 position,bool running,float dt,bool canMove=true)
        {
            if(!initialized){previous=position;initialized=true;return;}
            float travelled=Vector3.Distance(position,previous);previous=position;
            if(!canMove||dt<=0||travelled>1||travelled/dt<.2f){distance=0;moving=wasRunning=false;return;}
            if(running&&!wasRunning)SfxPlayer.Play(GameSfx.ShoeSkid,position);
            if(!moving&&!running)SfxPlayer.Play(GameSfx.Footstep,position);
            moving=true;wasRunning=running;distance+=travelled;
            float stride=running?1.65f:1.4f;
            if(distance>=stride){distance%=stride;SfxPlayer.Play(running?GameSfx.ShoeSkid:GameSfx.Footstep,position);}
        }
    }

    public sealed class ServeCountdownCue
    {
        int last=-1;
        public void Reset(){last=-1;SfxPlayer.Stop(GameSfx.Countdown);}
        public void Step(float elapsed)
        {
            int second=Mathf.FloorToInt(elapsed+.0001f);
            if(second<0||second>=10||second==last)return;
            last=second;SfxPlayer.Play(GameSfx.Countdown);
        }
    }
}
