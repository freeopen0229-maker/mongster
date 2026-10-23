using System;
using UnityEngine;
namespace FusionTamagotchi.Client
{
    [Serializable] public sealed class NetworkConfig
    {
        public string apiBaseUrl,environment;
        public static NetworkConfig Load()
        {
            var asset=Resources.Load<TextAsset>("NetworkConfig");
            if(!asset)throw new InvalidOperationException("NetworkConfig.json이 없습니다.");
            var config=JsonUtility.FromJson<NetworkConfig>(asset.text);config.Validate();return config;
        }
        public void Validate()
        {
            if(environment!="staging"&&environment!="production")throw new ArgumentException("Unknown network environment.");
            ValidateEndpoint(apiBaseUrl);
        }
        public static void ValidateEndpoint(string value)
        {
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.IsLoopback||
               uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase)||
               !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||uri.AbsolutePath!="/")
                throw new ArgumentException("실제 HTTPS API 주소를 NetworkConfig.json에 설정해 주세요. localhost는 사용할 수 없습니다.");
        }
    }
    [Serializable] public sealed class GameLoginResult
    {
        public string accountId,accessToken,selectedPetId;
        public void Validate(){if(!Mongle.Slice.PetIds.Valid(accountId)||string.IsNullOrWhiteSpace(accessToken)||(!string.IsNullOrEmpty(selectedPetId)&&!Mongle.Slice.PetIds.Valid(selectedPetId)))throw new ArgumentException("Invalid login result.");}
    }
    // Implement using the deployed identity provider. No fixed token or simulated successful login.
    public abstract class GameLoginProvider:MonoBehaviour
    {
        public abstract System.Threading.Tasks.Task<GameLoginResult> LoginAsync(System.Threading.CancellationToken token);
    }
}
