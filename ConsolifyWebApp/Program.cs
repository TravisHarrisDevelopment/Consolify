using System.Text.Json;

namespace ConsolifyWebApp
{
    public class Program
    { 

        private static string _clientId = "";
        private static string _clientSecret = "";
        private static string _redirectUri = "http://127.0.0.1:8888/callback";
        private static string _spotifyToken = "";
        private static string _deviceId = "";
        private static HttpClient _spotifyClient = new HttpClient();

        public static void Main(string[] args)
        {
            _clientId = Environment.GetEnvironmentVariable("spotify_client_id");
            _clientSecret = Environment.GetEnvironmentVariable("spotify_client_secret");

            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls("http://127.0.0.1:8888");
            var app = builder.Build();

            app.UseStaticFiles();

            app.MapGet("/", () =>
            {
                Console.WriteLine($"Home page requested. Has token: {!string.IsNullOrEmpty(_spotifyToken)}");

                if (string.IsNullOrEmpty(_spotifyToken))
                {
                    return Results.Content(GetLoginPage(), "text/html");
                }
                else
                {
                    Console.WriteLine($"Token preview: {_spotifyToken.Substring(0, Math.Min(20, _spotifyToken.Length))}...");
                    return Results.Content(GetPlayerPage(), "text/html");
                }
            });

            app.MapGet("/login", () =>
            {
                var scopes = "streaming user-read-email user-read-private user-modify-playback-state user-read-playback-state";
                var authorizeUrl = "https://accounts.spotify.com/authorize" +
                    $"?client_id={_clientId}" +
                    $"&response_type=code" +
                    $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                    $"&scope={Uri.EscapeDataString(scopes)}";

                return Results.Redirect(authorizeUrl);
            });

            app.MapGet("/callback", async (string code, HttpContext context) =>
            {
                using var httpClient = new HttpClient();

                // Prepare the token request
                var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"grant_type", "authorization_code"},
                    {"code", code},
                    {"redirect_uri", _redirectUri},
                    {"client_id", _clientId},
                    {"client_secret", _clientSecret}
                });

                // Request the token
                var response = await httpClient.PostAsync(
                    "https://accounts.spotify.com/api/token",
                    tokenRequest
                );

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);

                    // Store the access token
                    _spotifyToken = tokenData.GetProperty("access_token").GetString() ?? "";

                    // Redirect to home page (which will now show the player)
                    return Results.Redirect("/");
                }
                else
                {
                    return Results.Text($"Error getting token: {response.StatusCode}");
                }

                //// Set up the HTTP client with the token
                //_spotifyClient.DefaultRequestHeaders.Authorization =
                //    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _spotifyToken);
            });

            app.MapGet("/api/token", () => new { token = _spotifyToken });

            app.MapPost("/api/device", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                _deviceId = data.GetProperty("deviceId").GetString() ?? "";

                Console.WriteLine($"Device registered: {_deviceId}");
                return Results.Ok();
            });


            // Play a specific playlist/album/track
            app.MapPost("/api/play", async (HttpRequest request) =>
            {
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var uri = data.GetProperty("uri").GetString();  // spotify:playlist:xxxxx

                var spotifyBody = new
                {
                    context_uri = uri
                };

                var json = JsonSerializer.Serialize(spotifyBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                //var response = await _spotifyClient.PutAsync(
                //    $"https://api.spotify.com/v1/me/player/play?device_id={_deviceId}",
                //    content
                //);

                var response = await SpotifyPutAsync(
                    $"https://api.spotify.com/v1/me/player/play?device_id={_deviceId}",
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Playing: {uri}");
                    return Results.Ok(new { status = "playing" });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Play error: {response.StatusCode} - {error}");
                    return Results.Problem($"Failed to play: {response.StatusCode}");
                }
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            // Pause playback
            app.MapPost("/api/pause", async () =>
            {
                try
                {
                    var response = await SpotifyPutAsync(
                        $"https://api.spotify.com/v1/me/player/pause?device_id={_deviceId}"
                    );

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Paused");
                        return Results.Ok(new { status = "paused" });
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Pause error: {response.StatusCode} - {error}");
                        return Results.Problem($"Failed to pause: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            // Resume playback
            app.MapPost("/api/resume", async () =>
            {
                try
                {
                    var response = await SpotifyPutAsync(
                        $"https://api.spotify.com/v1/me/player/play?device_id={_deviceId}"
                    );

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Resumed");
                        return Results.Ok(new { status = "playing" });
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Resume error: {response.StatusCode} - {error}");
                        return Results.Problem($"Failed to resume: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.Run();
        }

        private static string GetHtmlPage()
        {
            return """
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Spotify Web Player</title>
                    <style>
                        body { font-family: Arial, sans-serif; padding: 20px; }
                        button { padding: 10px 20px; margin: 5px; font-size: 16px; cursor: pointer; }
                        input { padding: 10px; width: 400px; margin: 5px; }
                        #status { margin: 20px 0; font-weight: bold; }
                        .controls { margin-top: 20px; }
                    </style>
                </head>
                <body>
                    <h1>Spotify Web Player</h1>
                    <div id='status'>Loading...</div>
                   
                    <div class="controls">
                        <h3>Play a Playlist/Album:</h3>
                        <input type="text" id="spotifyUri" placeholder="spotify:playlist:2gAzGXzCz4wVHKlKDR57z7" />
                        <button onclick="playUri()">Play</button>

                        <h3>Playback Controls:</h3>
                        <button onclick="pausePlayback()">Pause</button>
                        <button onclick="resumePlayback()">Resume</button>
                    </div>

                    <!-- Load Spotify SDK first -->
                    <script src='https://sdk.scdn.co/spotify-player.js'></script>
            
                    <!-- Then load your player.js which handles everything -->
                    <script src='/player.js'></script>

                    <script>
                        async function playUri() {
                            const uri = document.getElementById('spotifyUri').value;
                            if (!uri) {
                                alert('Please enter a Spotify URI');
                                return;
                            }

                            console.log('Attempting to play:', uri);

                            try{
                                const response = await fetch('/api/play', {
                                    method: 'POST',
                                    headers: { 'Content-Type': 'application/json' },
                                    body: JSON.stringify({ uri: uri })
                                });

                                if (response.ok) {
                                    console.log('Play command sent successfully');
                                } else {
                                    const error = await response.text();
                                    console.error('Failed to play:', error);
                                    alert('Failed to play: ' + error);
                                }
                            } catch (error) {
                                console.error('Error:', error);
                                alert('Error: ' + error);
                            }
                        }

                        async function pausePlayback() {
                            await fetch('/api/pause', { method: 'POST' });
                        }

                        async function resumePlayback() {
                            await fetch('/api/resume', { method: 'POST' });
                        }
                    </script>
                </body>
                </html>
            """;
        }
        private static string GetLoginPage()
        {
            return """
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Consolify - Login</title>
                    <style>
                        body { font-family: Arial; text-align: center; padding: 50px; }
                        button { padding: 15px 30px; font-size: 18px; cursor: pointer; }
                    </style>
                </head>
                <body>
                    <h1>Consolify Web Player</h1>
                    <p>Connect your Spotify account to continue</p>
                    <button onclick="window.location.href='/login'">Login with Spotify</button>
                </body>
                </html>
                """;
        }

        private static string GetPlayerPage()
        {
            // Move your existing GetHtmlPage content here
            return GetHtmlPage(); // Your existing player HTML
        }

        private static async Task<HttpResponseMessage> SpotifyPutAsync(string url, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _spotifyToken);
            if (content != null)
                request.Content = content;

            return await _spotifyClient.SendAsync(request);
        }
        private static async Task<HttpResponseMessage> SpotifyPutAsync(string url)
        {
            return await SpotifyPutAsync(url, null);
        }
    }
}
