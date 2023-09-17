using System.CommandLine;
using System.Net;
using System.Text.Json.Nodes;

namespace NitradoDnsUpdater;

internal class Program : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _domain;

    public Program(HttpClient client, string domain)
    {
        _client = client;
        _domain = domain;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public static async Task<int> Main(string[] args)
    {
        var tokenOption = new Option<string>("--nitradoToken") { IsRequired = true };
        tokenOption.AddAlias("-n");
        var domainOption = new Option<string>("--domain") { IsRequired = true };
        domainOption.AddAlias("-d");
        var rootCommand = new RootCommand("Updates the A record of the given domain via Nitrado DNS Nameserver.");
        rootCommand.AddOption(tokenOption);
        rootCommand.AddOption(domainOption);

        var exitCode = -1;

        rootCommand.SetHandler(async (a, b) => { exitCode = await Main2(a, b); }, tokenOption, domainOption);
        await rootCommand.InvokeAsync(args);

        return exitCode;
    }

    private static async Task<int> Main2(string nitradoToken, string domain)
    {
        if (await GetCurrentPublicIp() is not { } currentIp)
        {
            await Console.Error.WriteLineAsync("Couldn't fetch current IP");
            return -1;
        }

        const string baseAddress = "https://api.nitrado.net/";
        var client = new HttpClient();
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {nitradoToken}");

        using var p = new Program(client, domain);
        await p.UpdateDns(currentIp);
        return 0;
    }


    private async Task UpdateDns(string newIp)
    {
        if (await GetCurrentDnsEntry() is not { } currentIp)
        {
            await Console.Error.WriteLineAsync("Couldn't get current DNS entry");
            return;
        }

        if (currentIp == newIp)
            return;

        var res = await UpdateRecord("@", "A", currentIp, "@", "A", newIp);

        if (!res.IsSuccessStatusCode)
            await Console.Error.WriteLineAsync($"Error setting new IP:\n{res}");
        else
            Console.WriteLine($"Successfully updated ip from {currentIp} to {newIp}");
    }


    private async Task<string?> GetCurrentDnsEntry()
    {
        var res = await _client.GetOptStringAsync($"domain/{_domain}/records");
        if (res == null)
            return null;
        var allRecords = JsonNode.Parse(res)?.AsObject()?["message"];
        var existingRecord = allRecords?.AsArray().Select(ele => ele?.AsObject()).FirstOrDefault(obj => obj?["name"]?.GetValue<string>() == "@");
        return existingRecord?["content"]?.GetValue<string>();
    }


    private Task<HttpResponseMessage> UpdateRecord(string oldName, string oldType, string oldContent, string name, string type, string content)
    {
        var parameters = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "name", name },
            { "type", type },
            { "content", content },
            { "name_old", oldName },
            { "type_old", oldType },
            { "content_old", oldContent }
        });
        return _client.PutAsync($"domain/{_domain}/records", parameters);
    }

    private static async Task<string?> GetCurrentPublicIp()
    {
        using var client = new HttpClient();
        var res = await client.GetOptStringAsync("https://api.ipify.org");
        if (res is null) return null;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (!IPAddress.TryParse(res, out var ipAddress))
            return null;

        return ipAddress.ToString();
    }
}