namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

public partial class ApiClient : ComponentBase
{
    private readonly List<HeaderEntry> _headers = [new HeaderEntry { Key = "Accept", Value = "application/json" }];
    private readonly JsonSerializerOptions _options = CreateOptions();

    private string _method = "GET";
    private string _url = "http://localhost:8998/";   
    private string _body = "";
    private string _responseBody = "";

    private HttpResponseMessage? _response;
    private bool _isLoading;   
    private long _responseTimeMs;

    [Inject]
    public required HttpClient Http { get; set; }

    private void AddHeader() => 
        _headers.Add(new HeaderEntry());

    private void RemoveHeader(HeaderEntry header)
    {
        if (header is not null)
            _headers.Remove(header);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private async Task SendRequest()
    {
        if (string.IsNullOrWhiteSpace(_url))
            return;

        _isLoading = true;
        _response = null;
        _responseBody = string.Empty;
        StateHasChanged();

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(_method), _url);

            if (IsBodyMethod(_method) && !string.IsNullOrWhiteSpace(_body))
            {
                request.Content = new StringContent(_body, Encoding.UTF8, "application/json");
            }

            foreach (var header in _headers.Where(h => !string.IsNullOrWhiteSpace(h.Key)))
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) && request.Content != null)
                {
                    try { request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value); } catch { }
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var sw = Stopwatch.StartNew();
            _response = await Http.SendAsync(request).ConfigureAwait(false);
            sw.Stop();

            _responseTimeMs = sw.ElapsedMilliseconds;
            _responseBody = await _response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (IsJsonResponse(_response))
            {
                using var parsedJson = JsonDocument.Parse(_responseBody);
                _responseBody = JsonSerializer.Serialize(parsedJson, _options);
            }
        }
        catch (Exception ex)
        {
            _responseBody = $"Error: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static bool IsBodyMethod(string method) =>
        method is "POST" or "PUT" or "PATCH";

    private static bool IsJsonResponse(HttpResponseMessage? response) =>
        response?.Content?.Headers?.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase) == true;
}
