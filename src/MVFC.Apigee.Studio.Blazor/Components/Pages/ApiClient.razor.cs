namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor component for making HTTP API requests.
/// Allows the user to configure HTTP method, URL, headers, and body, send the request, and view the response.
/// </summary>
public partial class ApiClient : ComponentBase, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// List of HTTP headers to include in the request.
    /// </summary>
    private readonly List<HeaderEntry> _headers = [new HeaderEntry { Key = "Accept", Value = "application/json" }];

    /// <summary>
    /// JSON serializer options used for formatting JSON responses.
    /// </summary>
    private readonly JsonSerializerOptions _options = CreateOptions();

    /// <summary>
    /// The HTTP method to use (e.g., GET, POST, PUT, PATCH).
    /// </summary>
    private string _method = "GET";

    /// <summary>
    /// The URL to which the request will be sent.
    /// </summary>
    private string _url = new UriBuilder(Uri.UriSchemeHttp, "localhost", 8998, "/").ToString();

    /// <summary>
    /// The request body content (for methods that support a body).
    /// </summary>
    private string _body = "";

    /// <summary>
    /// The response body returned from the server.
    /// </summary>
    private string _responseBody = "";

    /// <summary>
    /// The response state containing status and headers.
    /// </summary>
    private ResponseState? _responseState;

    /// <summary>
    /// Represents the simplified state of an HTTP response for UI and persistence.
    /// </summary>
    public class ResponseState
    {
        public int StatusCode { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public bool IsSuccessStatusCode { get; set; }
        public string HeadersString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Indicates whether a request is currently being sent.
    /// </summary>
    private bool _isLoading;

    /// <summary>
    /// The time taken to receive the response, in milliseconds.
    /// </summary>
    private long _responseTimeMs;

    /// <summary>
    /// The <see cref="HttpClient"/> used to send HTTP requests.
    /// </summary>
    [Inject]
    public required HttpClient Http { get; set; }

    /// <summary>
    /// Configuration to retrieve default emulator URL.
    /// </summary>
    [Inject]
    public required IConfiguration Config { get; set; }

    /// <summary>
    /// Service for managing session state across navigations.
    /// </summary>
    [Inject]
    public required SessionStateService SessionState { get; set; }

    /// <summary>
    /// Initializes the component by loading the default URL from configuration.
    /// </summary>
    protected override void OnInitialized()
    {
        _url = Config["EmulatorRuntime:BaseUrl"] ?? new UriBuilder(Uri.UriSchemeHttp, "localhost", 8998, "/").ToString();

        if (SessionState.Has("apiclient:url"))
        {
            _url = SessionState.Get<string>("apiclient:url") ?? _url;
            _method = SessionState.Get<string>("apiclient:method") ?? "GET";
            _body = SessionState.Get<string>("apiclient:body") ?? "";

            var savedHeaders = SessionState.Get<List<HeaderEntry>>("apiclient:headers");
            if (savedHeaders != null && savedHeaders.Count != 0)
            {
                _headers.Clear();
                _headers.AddRange(savedHeaders);
            }

            _responseBody = SessionState.Get<string>("apiclient:responseBody") ?? "";
            _responseTimeMs = SessionState.Get<long>("apiclient:responseTimeMs");
            _responseState = SessionState.Get<ResponseState>("apiclient:responseState");
        }
    }

    /// <summary>
    /// Adds a new empty header entry to the request.
    /// </summary>
    private void AddHeader() =>
        _headers.Add(new HeaderEntry());

    /// <summary>
    /// Removes the specified header entry from the request.
    /// </summary>
    /// <param name="header">The header entry to remove.</param>
    private void RemoveHeader(HeaderEntry header)
    {
        if (header is not null)
            _headers.Remove(header);
    }

    /// <summary>
    /// Creates and configures the <see cref="JsonSerializerOptions"/> used for formatting JSON responses.
    /// </summary>
    /// <returns>A configured <see cref="JsonSerializerOptions"/> instance.</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    /// Sends the HTTP request using the configured method, URL, headers, and body.
    /// Updates the response fields with the result.
    /// </summary>
    private async Task SendRequest()
    {
        if (string.IsNullOrWhiteSpace(_url))
            return;

        _isLoading = true;
        _responseState = null;
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
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var sw = Stopwatch.StartNew();
            using var response = await Http.SendAsync(request);
            sw.Stop();

            _responseTimeMs = sw.ElapsedMilliseconds;
            _responseBody = await response.Content.ReadAsStringAsync();

            _responseState = new ResponseState
            {
                StatusCode = (int)response.StatusCode,
                StatusName = response.StatusCode.ToString(),
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                HeadersString = string.Join('\n', response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")),
            };

            if (IsJsonResponse(response))
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

    /// <summary>
    /// Determines if the HTTP method supports a request body.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>True if the method supports a body; otherwise, false.</returns>
    private static bool IsBodyMethod(string method) =>
        method is "POST" or "PUT" or "PATCH";

    /// <summary>
    /// Determines if the response has a JSON content type.
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <returns>True if the response is JSON; otherwise, false.</returns>
    private static bool IsJsonResponse(HttpResponseMessage? response) =>
        response?.Content?.Headers?.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Saves the current component state to the session state service.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            SessionState.Set("apiclient:url", _url);
            SessionState.Set("apiclient:method", _method);
            SessionState.Set("apiclient:body", _body);
            SessionState.Set("apiclient:headers", _headers.ToList());
            SessionState.Set("apiclient:responseBody", _responseBody);
            SessionState.Set("apiclient:responseTimeMs", _responseTimeMs);
            SessionState.Set("apiclient:responseState", _responseState);
        }

        _disposed = true;
    }
}
