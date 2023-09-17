using System.Diagnostics.CodeAnalysis;

namespace NitradoDnsUpdater;

public static class ClientExtensions
{
    public static async Task<string?> GetOptStringAsync(this HttpClient client, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
    {
        var res = await client.GetAsync(requestUri);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
    }
}