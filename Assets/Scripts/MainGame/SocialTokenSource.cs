using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FusionTamagotchi.Client
{
    // Native SDK implementation must finish the user's interactive login first.
    // Google: return ID token for the configured web/server audience.
    // Kakao: return the logged-in user's access token. Never return an app secret.
    public abstract class SocialTokenSource : MonoBehaviour
    {
        public abstract Task<string> SignInAsync(string provider, CancellationToken token);
    }
}
