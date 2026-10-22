using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WalletPlatform.IntegrationTests;

internal static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client,
        string path,
        T body)
    {
        var content = JsonContent.Create(body, options: Options);
        return await client.PostAsync(path, content);
    }

    public static async Task<HttpResponseMessage> GetAuthorizedAsync(
        this HttpClient client,
        string path,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
