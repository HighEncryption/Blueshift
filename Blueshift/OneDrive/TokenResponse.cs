namespace Blueshift.OneDrive
{
    using System.Security.Cryptography;
    using System.Text;

    using Newtonsoft.Json;

    public class AuthenticationResult
    {
        public string Code { get; set; }
    }

    public class TokenResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int Expires { get; set; }

        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("id_token")]
        public string IdToken { get; set; }

        [JsonProperty("is_encrypted", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsEncrypted { get; set; }

        [JsonProperty("acquire_time")]
        public DateTime AcquireTime { get; set; }

        public void Protect()
        {
            if (this.IsEncrypted)
            {
                return;
            }

            this.AccessToken = TokenHelper.Protect(this.AccessToken);
            this.RefreshToken = TokenHelper.Protect(this.RefreshToken);

            if (!string.IsNullOrWhiteSpace(this.IdToken))
            {
                this.IdToken = TokenHelper.Protect(this.IdToken);
            }

            this.IsEncrypted = true;
        }

        public void Unprotect()
        {
            if (!this.IsEncrypted)
            {
                return;
            }

            this.AccessToken = TokenHelper.Unprotect(this.AccessToken);
            this.RefreshToken = TokenHelper.Unprotect(this.RefreshToken);

            if (!string.IsNullOrWhiteSpace(this.IdToken))
            {
                this.IdToken = TokenHelper.Unprotect(this.IdToken);
            }

            this.IsEncrypted = false;
        }

        public TokenResponse DuplicateToken()
        {
            Pre.BreakIf(this.AcquireTime == DateTime.MinValue);

            return new TokenResponse()
            {
                TokenType = this.TokenType,
                Expires = this.Expires,
                Scopes = this.Scopes,
                AccessToken = this.AccessToken,
                RefreshToken = this.RefreshToken,
                IdToken = this.IdToken,
                IsEncrypted = this.IsEncrypted,
                AcquireTime = this.AcquireTime,
            };
        }

        public void SaveProtectedToken(string path)
        {
            TokenResponse duplicateToken = this.DuplicateToken();
            duplicateToken.Protect();

            var tokenContent = JsonConvert.SerializeObject(duplicateToken, Formatting.Indented);
            File.WriteAllText(path, tokenContent);
        }

        public static TokenResponse LoadFromFile(string path)
        {
            string tokenContent = File.ReadAllText(path);

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(tokenContent);

            Pre.ThrowIfArgumentNull(tokenResponse, nameof(tokenResponse));

            if (!tokenResponse.IsEncrypted)
            {
                // The token file is NOT encrypted. Immediately encrypt and  save the file back
                // to disk for security before using it.
                tokenResponse.Protect();
                tokenContent = JsonConvert.SerializeObject(tokenResponse, Formatting.Indented);
                File.WriteAllText(path, tokenContent);
            }

            tokenResponse.Unprotect();

            return tokenResponse;
        }

        public string GetAccessTokenHash()
        {
            var input = Encoding.ASCII.GetBytes(this.AccessToken);
            using var sha1 = SHA1.Create();
            var output = sha1.ComputeHash(input);
            return BitConverter.ToString(output).Replace("-", "");
        }

        public string GetRefreshTokenHash()
        {
            var input = Encoding.ASCII.GetBytes(this.RefreshToken);
            using var sha1 = SHA1.Create();
            var output = sha1.ComputeHash(input);
            return BitConverter.ToString(output).Replace("-", "");
        }
    }
}