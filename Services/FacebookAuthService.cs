// Services/FacebookAuthService.cs
using System.Text.Json;

namespace ShopAPI.Services
{
    // Shape of Facebook user info response
    public class FacebookUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Picture { get; set; }
    }

    public class FacebookAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public FacebookAuthService(
            HttpClient httpClient,
            IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        // ── Validate token & get user info from Facebook ──
        public async Task<FacebookUserInfo?> ValidateTokenAsync(string accessToken)
        {
            var appId = _config["Facebook:AppId"];
            var appSecret = _config["Facebook:AppSecret"];

            // STEP 1: Verify token is real & belongs to OUR app
            var debugUrl = "https://graph.facebook.com/debug_token" +
                           $"?input_token={accessToken}" +
                           $"&access_token={appId}|{appSecret}";
            // appId|appSecret = our app-level access token

            try
            {
                var debugResponse = await _httpClient.GetAsync(debugUrl);
                if (!debugResponse.IsSuccessStatusCode)
                    return null;

                var debugJson = await debugResponse.Content
                                    .ReadFromJsonAsync<JsonElement>();

                var data = debugJson.GetProperty("data");
                var isValid = data.GetProperty("is_valid").GetBoolean();
                var tokenAppId = data.GetProperty("app_id").GetString();

                // Token must be valid AND issued for our app specifically
                if (!isValid || tokenAppId != appId)
                    return null;

                // STEP 2: Fetch user info using verified token
                var userUrl = "https://graph.facebook.com/me" +
                              "?fields=id,name,email,picture.type(large)" +
                              $"&access_token={accessToken}";

                var userResponse = await _httpClient.GetAsync(userUrl);
                if (!userResponse.IsSuccessStatusCode)
                    return null;

                var userJson = await userResponse.Content
                                   .ReadFromJsonAsync<JsonElement>();

                // STEP 3: Map to our model
                return new FacebookUserInfo
                {
                    Id = userJson.GetProperty("id").GetString()!,
                    Name = userJson.GetProperty("name").GetString()!,

                    // Email is optional — user might not grant permission
                    Email = userJson.TryGetProperty("email", out var email)
                                ? email.GetString() : null,

                    // Picture is nested: picture → data → url
                    Picture = userJson.TryGetProperty("picture", out var pic)
                              && pic.TryGetProperty("data", out var picData)
                              && picData.TryGetProperty("url", out var url)
                                  ? url.GetString() : null
                };
            }
            catch
            {
                return null; // network error or unexpected JSON shape
            }
        }
    }
}