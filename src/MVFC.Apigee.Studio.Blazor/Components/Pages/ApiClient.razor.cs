namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor component for making HTTP API requests.
/// Allows the user to configure HTTP method, URL, headers, and body, send the request, and view the response.
/// </summary>
public partial class ApiClient : ComponentBase
{
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
    private string _url = "http://localhost:8998/";   

    /// <summary>
    /// The request body content (for methods that support a body).
    /// </summary>
    private string _body = "";

    /// <summary>
    /// The response body returned from the server.
    /// </summary>
    private string _responseBody = "";

    /// <summary>
    /// The HTTP response message received after sending the request.
    /// </summary>
    private HttpResponseMessage? _response;

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
}
