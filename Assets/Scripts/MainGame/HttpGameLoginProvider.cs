using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FusionTamagotchi.Client
{
    /// <summary>Login transport example. Connect a real form and native SDK token source in the scene.</summary>
    public sealed class HttpGameLoginProvider : GameLoginProvider
    {
        public enum LoginMethod { Email, Google, Kakao }
        public LoginMethod method;
        public InputField emailInput, passwordInput;
        public SocialTokenSource socialTokenSource;
        CancellationTokenSource enabledLifetime;
        bool busy;
        [Serializable] sealed class EmailBody { public string email, password; }
        [Serializable] sealed class SocialBody { public string provider, token; }

        void OnEnable() { enabledLifetime = new CancellationTokenSource(); }
        void OnDisable() { enabledLifetime?.Cancel(); enabledLifetime?.Dispose(); enabledLifetime = null; }
        public void UseEmail() { method = LoginMethod.Email; }
        public void UseGoogle() { method = LoginMethod.Google; }
        public void UseKakao() { method = LoginMethod.Kakao; }

        public override async Task<GameLoginResult> LoginAsync(CancellationToken token)
        {
            if (busy || !isActiveAndEnabled || enabledLifetime == null)
                throw new InvalidOperationException("로그인이 진행 중이거나 로그인 화면이 닫혔습니다.");
            busy = true;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, enabledLifetime.Token))
            {
                try
                {
                    var config = NetworkConfig.Load();
                    string path, body;
                    if (method == LoginMethod.Email)
                    {
                        if (!emailInput || !passwordInput) throw new InvalidOperationException("이메일 로그인 입력창을 연결해 주세요.");
                        path = "/v2/auth/email/login";
                        body = JsonUtility.ToJson(new EmailBody { email = emailInput.text.Trim(), password = passwordInput.text });
                        passwordInput.text = "";
                    }
                    else
                    {
                        if (!socialTokenSource) throw new InvalidOperationException("구글·카카오 로그인 SDK를 연결해 주세요.");
                        string provider = method == LoginMethod.Google ? "google" : "kakao";
                        string providerToken = await socialTokenSource.SignInAsync(provider, linked.Token);
                        linked.Token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(providerToken)) throw new InvalidOperationException("소셜 로그인이 완료되지 않았습니다.");
                        path = "/v2/auth/social/login";
                        body = JsonUtility.ToJson(new SocialBody { provider = provider, token = providerToken });
                    }
                    using (var request = new UnityWebRequest(config.apiBaseUrl.TrimEnd('/') + path, "POST"))
                    {
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.redirectLimit = 0; request.timeout = 20;
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            if (linked.IsCancellationRequested) { request.Abort(); linked.Token.ThrowIfCancellationRequested(); }
                            await Task.Yield();
                        }
                        linked.Token.ThrowIfCancellationRequested();
                        if (request.result != UnityWebRequest.Result.Success)
                            throw new InvalidOperationException("로그인을 완료하지 못했습니다. (" + request.responseCode + ")");
                        var result = JsonUtility.FromJson<GameLoginResult>(request.downloadHandler.text);
                        if (result == null) throw new InvalidOperationException("로그인 응답 형식이 올바르지 않습니다.");
                        result.Validate(); return result;
                    }
                }
                finally { busy = false; }
            }
        }
    }
}
