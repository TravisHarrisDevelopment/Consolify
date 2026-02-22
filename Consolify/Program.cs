using System.Text;
using System.Text.Json;

namespace Consolify
{
     class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string baseUrl = "http://127.0.0.1:8888";

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            var command = args[0].ToLower();

            try
            {
                switch (command)
                {
                    case "play":
                        if (args.Length > 1)
                            await Play(args[1]);
                        else
                            await Play("");
                            //Console.WriteLine("Usage: consolify play <spotify:uri>");
                        break;

                    case "pause":
                        await Pause();
                        break;

                    case "resume":
                        await Resume();
                        break;

                    case "help":
                        ShowHelp();
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        ShowHelp();
                        break;
                }
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Error: Could not connect to web player. Is it running on http://127.0.0.1:8888?");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static async Task Play(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                uri = "spotify:playlist:2gAzGXzCz4wVHKlKDR57z7"; //default to NIN
                //uri = "spotify:album:0vNBQof86Lv5gLuf26ML7o";
            }
            var body = new { uri = uri };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/api/play", content);

            if (response.IsSuccessStatusCode)
                Console.WriteLine($"Playing: {uri}");
            else
                Console.WriteLine($"Failed to play: {response.StatusCode}");
        }

        static async Task Pause()
        {
            var response = await client.PostAsync($"{baseUrl}/api/pause", null);

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Paused");
            else
                Console.WriteLine($"Failed to pause: {response.StatusCode}");
        }

        static async Task Resume()
        {
            var response = await client.PostAsync($"{baseUrl}/api/resume", null);

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Resumed");
            else
                Console.WriteLine($"Failed to resume: {response.StatusCode}");
        }

        static void ShowHelp()
        {
            Console.WriteLine("Consolify CLI - Control your Spotify Web Player");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  consolify play <spotify:uri>  - Play a playlist/album");
            Console.WriteLine("  consolify pause               - Pause playback");
            Console.WriteLine("  consolify resume              - Resume playback");
            Console.WriteLine("  consolify help                - Show this help");
            Console.WriteLine("\nExample:");
            Console.WriteLine("  consolify play spotify:playlist:37i9dQZF1DXcBWIGoYBM5M");
        }
    }
}
