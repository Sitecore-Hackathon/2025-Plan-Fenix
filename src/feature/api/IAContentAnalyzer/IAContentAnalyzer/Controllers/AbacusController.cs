using IAContentAnalyzer.Configuration;
using IAContentAnalyzer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using System.Text;

namespace IAContentAnalyzer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AbacusController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AbacusApiSettings _apiSettings;
        private readonly ILogger<AbacusController> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public AbacusController(
            IHttpClientFactory httpClientFactory,
            IOptions<AbacusApiSettings> apiSettings,
            ILogger<AbacusController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiSettings = apiSettings.Value;
            _logger = logger;

            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(response =>
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || // 503
                    response.StatusCode == System.Net.HttpStatusCode.Conflict || // 409 - Add this
                    (int)response.StatusCode == 424)  // Failed Dependency
                .WaitAndRetryAsync(
                    3, // number of retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * 15), // longer exponential backoff
                    onRetry: async (outcome, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Attempt {retryCount}: Request failed with status {outcome.Result?.StatusCode}");

                        try
                        {
                            // Check if we got a 409 Conflict error
                            if (outcome.Result?.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                var content = await outcome.Result.Content.ReadAsStringAsync();
                                _logger.LogWarning($"Deployment is still initializing: {content}");

                                // Wait for 30 seconds as suggested by the error message
                                _logger.LogInformation("Waiting 30 seconds for deployment to initialize...");
                                await Task.Delay(TimeSpan.FromSeconds(30));
                            }
                            else
                            {
                                // For other errors, try to start the deployment
                                _logger.LogWarning("Attempting to start deployment...");
                                await StartDeploymentAsync();

                                // Wait for the deployment to become active with a longer timeout
                                _logger.LogInformation("Waiting for deployment to become active...");
                                await WaitForDeploymentAsync(TimeSpan.FromSeconds(180)); // 3 minutes

                                // Add additional delay to ensure it's fully ready
                                _logger.LogInformation("Adding additional delay to ensure deployment is fully ready...");
                                await Task.Delay(10000);
                            }

                            _logger.LogInformation("Ready to retry request");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error during deployment restart: {ex.Message}");
                        }
                    }
                );
        }

        private async Task StartDeploymentAsync()
        {
            try
            {
                // Correct URL format with query parameter  
                var url = $"https://api.abacus.ai/api/v0/startDeployment?deploymentId={_apiSettings.DeploymentId}";

                // Create request with proper apiKey header  
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("apiKey", _apiSettings.ApiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to start deployment. Status: {response.StatusCode}, Content: {responseContent}");
                    throw new Exception($"Failed to start deployment: {response.StatusCode}");
                }

                _logger.LogInformation("Successfully initiated deployment start");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in StartDeploymentAsync: {ex.Message}");
                throw;
            }
        }

        private async Task WaitForDeploymentAsync(TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var url = $"https://api.abacus.ai/api/v0/describeDeployment?deploymentId={_apiSettings.DeploymentId}";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("apiKey", _apiSettings.ApiKey);

                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        _logger.LogDebug($"Full deployment response: {content}");

                        // Parse the JSON
                        var jsonResponse = JObject.Parse(content);

                        // Try to find the status field
                        string? status = null;

                        if (jsonResponse["deployment"] != null)
                        {
                            status = jsonResponse["deployment"]["status"]?.ToString();
                            _logger.LogInformation($"Found status in deployment.status: {status}");
                        }

                        if (string.IsNullOrEmpty(status) && jsonResponse["result"] != null)
                        {
                            status = jsonResponse["result"]["status"]?.ToString();
                            _logger.LogInformation($"Found status in result.status: {status}");
                        }

                        if (string.IsNullOrEmpty(status))
                        {
                            status = jsonResponse["status"]?.ToString();
                            _logger.LogInformation($"Found status directly: {status}");
                        }

                        _logger.LogInformation($"Current deployment status: {status ?? "unknown"}");

                        // Check if deployment is active
                        if (!string.IsNullOrEmpty(status) &&
                            (status.ToLower() == "active" || status.ToLower() == "running"))
                        {
                            _logger.LogInformation("Deployment is now active");
                            return;
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning($"Failed to get deployment status: {response.StatusCode}, {errorContent}");
                    }

                    // Wait longer between checks
                    _logger.LogInformation("Waiting for deployment to become active...");
                    await Task.Delay(10000); // Wait 10 seconds before checking again
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error checking deployment status: {ex.Message}");
                    await Task.Delay(5000);
                }
            }

            throw new TimeoutException("Deployment failed to become active within the specified timeout.");
        }

        [HttpPost("classify")]
        public async Task<IActionResult> ClassifyText([FromBody] ClassifyRequest request)
        {
            try
            {
                _logger.LogInformation("Classification request received for text: {TextPreview}", 
                    request.Text?.Substring(0, Math.Min(50, request.Text?.Length ?? 0)) + "...");

                // 1. Prepare request payload
                var responseBody = await SendClassificationRequestWithRetry(request.Text);

                // 2. Extract taxonomy labels
                string[]? labelsArray = ExtractTaxonomyLabels(responseBody);

                // 3. Return response
                if (labelsArray != null && labelsArray.Length > 0)
                {
                    _logger.LogInformation("Classification successful. Found {Count} labels: {Labels}", 
                        labelsArray.Length, string.Join(", ", labelsArray));

                    return Ok(labelsArray);
                }

                _logger.LogWarning("No taxonomy labels found in the response");

                return NotFound("No taxonomy labels found in the response");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Timeout waiting for deployment: {ex.Message}");
                return StatusCode(503, "Service temporarily unavailable. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying text");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        private async Task<string> SendClassificationRequestWithRetry(string? text)
        {
            _logger.LogDebug("Sending classification request to Abacus API");

            var payload = new
            {
                arguments = null as object,
                keywordArguments = new
                {
                    page_content = text
                }
            };

            var jsonString = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var url = $"{_apiSettings.ApiUrl}?deploymentToken={_apiSettings.DeploymentToken}&deploymentId={_apiSettings.DeploymentId}";

            // Execute with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug($"Sending request to {url}");
                var result = await _httpClient.PostAsync(url, content);
                _logger.LogDebug($"API response status: {result.StatusCode}");

                // Important: Return the result even if it's not successful
                // The retry policy will handle retrying based on the status code
                return result;
            });

            // Now check if the final response is successful
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"API request failed after retries. Status: {response.StatusCode}, Content: {errorContent}");
                throw new HttpRequestException($"API request failed: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("API response: {Response}", responseContent);

            return responseContent;
        }

        private string[]? ExtractTaxonomyLabels(string? responseBody)
        {
            if (responseBody == null)
            {
                _logger.LogWarning("Response body is null");

                return null;
            }

            try
            {
                // Parse the JSON
                JObject jsonObject = JObject.Parse(responseBody);

                // Get the segment property which contains a JSON string
                string segmentJsonString = jsonObject["result"]["segments"][0]["segment"].ToString();

                // Parse this inner JSON string
                JObject segmentObject = JObject.Parse(segmentJsonString);

                // Extract the taxonomy_labels
                JArray taxonomyLabels = (JArray)segmentObject["taxonomy_labels"];

                _logger.LogDebug("Extracted {Count} taxonomy labels", taxonomyLabels?.Count ?? 0);

                // Convert to string array
                return taxonomyLabels?.ToObject<string[]>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting taxonomy labels from response");
                throw;
            }
        }
    }
}