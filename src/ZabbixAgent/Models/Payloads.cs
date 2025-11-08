namespace ZabbixAgent.Models;

public class ZabbixPassiveCheckRequest
{
    [JsonPropertyName("request")]
    public string Request { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<ZabbixPassiveCheckItem> Data { get; set; } = [];
}

public class ZabbixPassiveCheckItem
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; }
}

public class ZabbixPassiveCheckResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = "success";

    [JsonPropertyName("version")]
    public string Version { get; set; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

    [JsonPropertyName("data")]
    public List<ZabbixPassiveCheckResult> Data { get; set; } = [];
}

public class ZabbixPassiveCheckResult
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
