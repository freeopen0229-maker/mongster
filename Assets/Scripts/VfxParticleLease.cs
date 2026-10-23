using UnityEngine;
namespace Mongle
{
    public sealed class VfxParticleLease : MonoBehaviour
    {
        public ParticleSystem system;
        public float releaseAt;
        void Update(){if(Time.unscaledTime>=releaseAt)VfxLibrary.Return(this);}
    }
}
