using System.IO;
using System.Net.Http;
using System.Text.Json;
using CyberManager.Shared;

namespace CyberManager.Admin.Services;

/// <summary>Imagen encontrada en Openverse, lista para mostrarse o descargarse.</summary>
public sealed record WebImageResult(
    string Id,
    string Title,
    string Creator,
    string License,
    string ThumbnailUrl,
    string ImageUrl,
    int Width,
    int Height)
{
    /// <summary>Nombre corto de la licencia para mostrar en la cuadrícula.</summary>
    public string LicenseLabel => License.ToLowerInvariant() switch
    {
        "cc0" => "CC0",
        "pdm" => "Dominio público",
        _ => License
    };

    /// <summary>Título y autor para el texto de ayuda de cada miniatura.</summary>
    public string Description => string.IsNullOrWhiteSpace(Creator)
        ? Title
        : $"{Title} · {Creator}";
}

/// <summary>
/// Busca imágenes en Openverse (<c>api.openverse.org</c>) limitadas a licencias CC0 y
/// de dominio público, para poder usarlas sin pedir atribución. Las imágenes se
/// descargan y se guardan siempre en PNG.
/// </summary>
public static class WebImageSearch
{
    private const string Endpoint = "https://api.openverse.org/v1/images/";
    private const int MaxImageBytes = 16 * 1024 * 1024;
    private const int MaxThumbnailBytes = 4 * 1024 * 1024;

    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp"
    };

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        // Openverse pide un User-Agent identificable para atender la búsqueda.
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            $"CyberManager/{AppInfo.Version} (https://github.com/Kis4r4gi/CyberManager)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, image/*");
        return client;
    }

    /// <summary>
    /// Devuelve las imágenes CC0 o de dominio público que coinciden con el texto. El
    /// servicio puede tardar o responder 504; se reintenta antes de mostrar el error.
    /// Openverse limita a 20 los resultados por página para peticiones anónimas.
    /// </summary>
    public static async Task<IReadOnlyList<WebImageResult>> SearchAsync(string query,
        int pageSize = 20, CancellationToken ct = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
            throw new InvalidOperationException("Escriba qué imagen desea buscar.");

        var size = Math.Clamp(pageSize, 1, 20);
        var url = $"{Endpoint}?q={Uri.EscapeDataString(term)}&license=cc0,pdm&page_size={size}";

        using var response = await GetWithRetryAsync(url, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!json.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            return Array.Empty<WebImageResult>();

        var list = new List<WebImageResult>();
        foreach (var item in results.EnumerateArray())
        {
            var imageUrl = Text(item, "url");
            if (string.IsNullOrWhiteSpace(imageUrl) || !IsWebUrl(imageUrl)) continue;

            // Openverse también indexa SVG y TIFF; WPF solo muestra imágenes raster.
            if (!LooksLikeRaster(imageUrl)) continue;

            var thumbUrl = Text(item, "thumbnail");
            if (string.IsNullOrWhiteSpace(thumbUrl) || !IsWebUrl(thumbUrl))
                thumbUrl = imageUrl;

            list.Add(new WebImageResult(
                Text(item, "id") ?? string.Empty,
                Text(item, "title") ?? "Sin título",
                Text(item, "creator") ?? string.Empty,
                Text(item, "license") ?? string.Empty,
                thumbUrl,
                imageUrl,
                Integer(item, "width"),
                Integer(item, "height")));
        }

        return list;
    }

    /// <summary>
    /// Descarga la miniatura para la cuadrícula. Devuelve null si no se puede obtener;
    /// la imagen principal todavía se puede elegir y descargar al aceptar.
    /// </summary>
    public static async Task<byte[]?> TryDownloadThumbnailAsync(WebImageResult result,
        CancellationToken ct)
    {
        try
        {
            return await DownloadBytesAsync(result.ThumbnailUrl, MaxThumbnailBytes, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Descarga la imagen elegida y la devuelve como PNG. Si el archivo original falla
    /// o es demasiado grande se usa la miniatura. El lado mayor se limita a
    /// <paramref name="maxSize"/> píxeles para que el icono sea liviano.
    /// </summary>
    public static async Task<byte[]> DownloadPngAsync(WebImageResult result, int maxSize = 512,
        CancellationToken ct = default)
    {
        var data = await TryDownloadBytesAsync(result.ImageUrl, MaxImageBytes, ct);
        if (data is null || !ImageInspector.Detect(data).Valid)
            data = await TryDownloadBytesAsync(result.ThumbnailUrl, MaxThumbnailBytes, ct);

        if (data is null || !ImageInspector.Detect(data).Valid)
            throw new InvalidOperationException(
                "No se pudo descargar la imagen. Elija otra o intente de nuevo.");

        return ProductImage.ToPng(data, maxSize);
    }

    private static async Task<byte[]?> TryDownloadBytesAsync(string url, int maxBytes,
        CancellationToken ct)
    {
        try
        {
            return await DownloadBytesAsync(url, maxBytes, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, int maxBytes,
        CancellationToken ct)
    {
        if (!IsWebUrl(url))
            throw new InvalidOperationException("La dirección de la imagen no es válida.");

        using var response = await GetWithRetryAsync(url, ct);
        if (response.Content.Headers.ContentLength is long length && length > maxBytes)
            throw new InvalidOperationException("La imagen es demasiado grande.");

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > maxBytes)
                throw new InvalidOperationException("La imagen es demasiado grande.");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Hace la petición y reintenta cuando Openverse responde 429/5xx o la conexión
    /// falla. Los errores definitivos se explican en español para mostrarlos al usuario.
    /// </summary>
    private static async Task<HttpResponseMessage> GetWithRetryAsync(string url,
        CancellationToken ct)
    {
        Exception? last = null;
        int? lastStatus = null;
        var lastDetail = string.Empty;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode) return response;

                var status = (int)response.StatusCode;

                // Openverse a veces responde 401/403 cuando limita las peticiones
                // anónimas; se reintenta antes de mostrar el error al usuario.
                if (status is 401 or 403 or 429 or >= 500)
                {
                    lastStatus = status;
                    lastDetail = await ReadDetailAsync(response, ct);
                    response.Dispose();

                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), ct);
                    continue;
                }

                var detail = await ReadDetailAsync(response, ct);
                response.Dispose();

                throw new InvalidOperationException(status == 404
                    ? "El servicio de imágenes no encontró el recurso solicitado."
                    : $"El servicio de imágenes respondió con error {status}. {detail}");
            }
            catch (HttpRequestException ex)
            {
                last = ex;
                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), ct);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                last = ex;
                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), ct);
            }
        }

        if (lastStatus is int code)
        {
            var detail = string.IsNullOrWhiteSpace(lastDetail) ? string.Empty : $" {lastDetail}";
            throw new InvalidOperationException(
                $"El servicio de imágenes no está disponible en este momento (error {code}). " +
                $"Intente de nuevo en unos minutos.{detail}");
        }

        throw new InvalidOperationException(
            "No se pudo conectar con el servicio de imágenes (Openverse). " +
            "Revise la conexión a internet e intente de nuevo.", last);
    }

    /// <summary>Lee un fragmento del cuerpo del error para explicar mejor la causa.</summary>
    private static async Task<string> ReadDetailAsync(HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            return detail.Length > 200 ? detail[..200] : detail;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? Text(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Integer(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static bool IsWebUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool LooksLikeRaster(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var extension = Path.GetExtension(uri.AbsolutePath);
        return extension.Length == 0 || RasterExtensions.Contains(extension);
    }
}
