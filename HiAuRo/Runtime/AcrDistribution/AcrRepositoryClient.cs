using System.Net.Http;
using System.Text.Json;

namespace HiAuRo.Runtime.AcrDistribution;

public sealed class AcrRepositoryClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PublisherIndexDto?> GetPublisherIndexAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PublisherIndexDto>(stream, JsonOptions, cancellationToken);
    }

    public async Task<AcrManifestDto?> GetManifestAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<AcrManifestDto>(stream, JsonOptions, cancellationToken);
    }
}
