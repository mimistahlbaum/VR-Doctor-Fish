using UnityEngine;

public abstract class HapticPlayerBase : MonoBehaviour
{
    public abstract void Play();
    public abstract void Stop();

    public virtual void Replay()
    {
        Stop();
        Play();
    }
}
