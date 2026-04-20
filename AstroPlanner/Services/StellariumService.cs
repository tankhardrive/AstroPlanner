namespace AstroPlanner.Services;

public class StellariumService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public string BaseUrl { get; set; } = "http://localhost:8090";

    public async Task<bool> FocusObjectAsync(string searchTerm)
    {
        try
        {
            var content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("target", searchTerm)
            ]);
            var response = await _http.PostAsync($"{BaseUrl}/api/main/focus", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
