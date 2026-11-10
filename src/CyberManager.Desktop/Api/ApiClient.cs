using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberManager.Desktop.Api;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public ApiException(string message, HttpStatusCode code) : base(message) => StatusCode = code;
}

public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public string BaseUrl { get; }
    public string? Token { get; set; }
    public bool HasToken => !string.IsNullOrWhiteSpace(Token);
    public Dictionary<string, string> Headers { get; } = new();

    public ApiClient(string baseUrl)
    {
        BaseUrl = Normalize(baseUrl);
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public static string Normalize(string url)
    {
        url = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url)) url = "http://127.0.0.1:5080";
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "http://" + url;

        // En la red local el servidor usa el puerto 5080. Si el usuario solo escribe
        // la IP o el nombre del equipo, se agrega el puerto automáticamente.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttps && uri.IsDefaultPort)
                url = new UriBuilder(uri) { Port = 5080 }.Uri.ToString();
        }

        return url.TrimEnd('/') + "/";
    }

    private HttpRequestMessage Build(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (Token is not null) request.Headers.Authorization = new("Bearer", Token);
        foreach (var header in Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var response = await _http.SendAsync(Build(method, path, body));
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new ApiException("Respuesta vacía del servidor.", response.StatusCode);
    }

    private async Task SendAsync(HttpMethod method, string path, object? body)
    {
        using var response = await _http.SendAsync(Build(method, path, body));
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var text = await response.Content.ReadAsStringAsync();
        var message = text;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var error)) message = error.GetString() ?? text;
        }
        catch { /* texto plano */ }

        message = string.IsNullOrWhiteSpace(message) ? $"Error {(int)response.StatusCode}" : message;
        if (response.StatusCode == HttpStatusCode.Forbidden) message = "No tiene permisos para esta operación.";
        throw new ApiException(message, response.StatusCode);
    }

    public Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path, null);
    public Task<T> PostAsync<T>(string path, object body) => SendAsync<T>(HttpMethod.Post, path, body);
    public Task PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);
    public Task<T> PutAsync<T>(string path, object body) => SendAsync<T>(HttpMethod.Put, path, body);
    public Task PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);
    public Task DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    /// <summary>Descarga un recurso binario (imágenes, respaldos, etc.).</summary>
    public async Task<byte[]> GetBytesAsync(string path)
    {
        using var request = Build(HttpMethod.Get, path, null);
        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>Envía un archivo como formulario multipart junto con campos adicionales.</summary>
    public async Task<T> PostFileAsync<T>(string path, string fieldName, string fileName,
        string contentType, byte[] data, IEnumerable<KeyValuePair<string, string>>? fields = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(file, fieldName, fileName);

        if (fields is not null)
        {
            foreach (var field in fields)
                content.Add(new StringContent(field.Value ?? string.Empty), field.Key);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/')) { Content = content };
        if (Token is not null) request.Headers.Authorization = new("Bearer", Token);
        foreach (var header in Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new ApiException("Respuesta vacía del servidor.", response.StatusCode);
    }
}
