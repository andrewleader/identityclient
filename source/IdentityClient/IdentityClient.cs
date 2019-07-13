using IdentityModel.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IdentityClient
{
    public class IdentityClient
    {
        public class StoredData
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public string TokenType { get; set; }
            public DateTime ExpiresOn { get; set; }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }

            public static StoredData FromJson(string json)
            {
                var answer = JsonConvert.DeserializeObject<StoredData>(json);
                answer.EnsureValid();
                return answer;
            }

            public void EnsureValid()
            {
                if (string.IsNullOrWhiteSpace(AccessToken))
                {
                    throw new InvalidOperationException("AccessToken isn't specified");
                }

                if (string.IsNullOrWhiteSpace(RefreshToken))
                {
                    throw new InvalidOperationException("RefreshToken isn't specified");
                }

                if (string.IsNullOrWhiteSpace(TokenType))
                {
                    throw new InvalidOperationException("TokenType isn't specified");
                }
            }

            public bool IsNotExpired()
            {
                return ExpiresOn > DateTime.UtcNow;
            }
        }

        public Uri BaseUri { get; private set; }
        public string ClientId { get; private set; }
        public StoredData Data { get; private set; }
        private HttpClient _client;

        /// <summary>
        /// Initializes an IdentityClient
        /// </summary>
        /// <param name="url">The auth url, like https://example.com/oauth2 </param>
        private IdentityClient(HttpClient client, string clientId, string url)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            _client = client;
            ClientId = clientId;
            BaseUri = new Uri(url, UriKind.Absolute);
        }

        public async Task<string> GetAccessTokenAsync()
        {
            lock (this)
            {
                if (Data == null)
                {
                    throw new IdentityClientLostAuthorizationException();
                }

                if (Data.IsNotExpired())
                {
                    return Data.AccessToken;
                }
            }

            // Request new 
            return await RefreshAccessTokenAsync();
        }

        private Task<string> _currentRefreshAccessTokenTask;
        public async Task<string> RefreshAccessTokenAsync()
        {
            Task<string> curr;
            bool created = false;
            lock (this)
            {
                if (_currentRefreshAccessTokenTask == null)
                {
                    _currentRefreshAccessTokenTask = RefreshAccessTokenHelperAsync();
                    created = true;
                }

                curr = _currentRefreshAccessTokenTask;
            }

            try
            {
                return await curr;
            }
            finally
            {
                if (created)
                {
                    lock (this)
                    {
                        _currentRefreshAccessTokenTask = null;
                    }
                }
            }
        }

        private async Task<string> RefreshAccessTokenHelperAsync()
        {
            var data = Data;
            if (data != null)
            {
                var tokenResponse = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest()
                {
                    Address = new Uri(BaseUri, "connect/token").ToString(),
                    RefreshToken = data.RefreshToken,
                    ClientId = ClientId
                });

                if (tokenResponse.IsError)
                {
                    if (tokenResponse.Exception != null)
                    {
                        throw tokenResponse.Exception;
                    }
                    throw new IdentityClientLostAuthorizationException();
                }

                data = new StoredData()
                {
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken ?? data.RefreshToken,
                    ExpiresOn = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    TokenType = tokenResponse.TokenType ?? data.TokenType
                };
                data.EnsureValid();
                this.Data = data;
                return tokenResponse.AccessToken;
            }

            throw new IdentityClientLostAuthorizationException();
        }

        public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
        {
            _client.SetBearerToken(await GetAccessTokenAsync());

            HttpResponseMessage response = null;

            response = await _client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                try
                {
                    lock (this)
                    {
                        if (Data != null)
                        {
                            // Change it to expired so it'll re-request using refresh token
                            Data.ExpiresOn = DateTime.MinValue;
                        }
                    }

                    throw new IdentityClientAccessTokenExpiredException();
                }
                finally
                {
                    response.Dispose();
                }
            }

            return response;
        }

        public static async Task<IdentityClient> LoginAsync(HttpClient client, string clientId, string url, string username, string password, string[] scopes)
        {
            var answer = new IdentityClient(client, clientId, url);

            var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest()
            {
                Address = new Uri(answer.BaseUri, "connect/token").ToString(),
                ClientId = clientId,
                UserName = username,
                Password = password,
                Scope = string.Join(" ", scopes.Concat(new string[] { "offline_access" })), // Offline access needed to receive refresh token
                GrantType = "authorization_code"
            });

            if (tokenResponse.IsError)
            {
                if (tokenResponse.Exception != null)
                {
                    throw tokenResponse.Exception;
                }
                if (tokenResponse.Error == "invalid_grant" && tokenResponse.ErrorDescription == "invalid_username_or_password")
                {
                    throw new IdentityClientInvalidUsernameOrPasswordException();
                }
                throw new Exception(tokenResponse.ErrorDescription ?? tokenResponse.Error);
            }

            var data = new StoredData()
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresOn = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                TokenType = tokenResponse.TokenType
            };
            data.EnsureValid();
            answer.Data = data;
            return answer;
        }

        public static IdentityClient FromStored(HttpClient client, string clientId, string url, string storedDataJson)
        {
            return new IdentityClient(client, clientId, url)
            {
                Data = StoredData.FromJson(storedDataJson)
            };
        }
    }
}
