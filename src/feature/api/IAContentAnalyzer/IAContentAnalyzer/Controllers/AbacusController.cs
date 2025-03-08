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
            _retryPolicy = CreateRetryPolicy();
        }

        [HttpPost("classify")]
        public async Task<IActionResult> ClassifyText([FromBody] ClassifyRequest request)
        {
            try
            {
                LogClassificationRequest(request);
                var responseBody = await SendClassificationRequestWithRetry(request.Text);
                string[]? labelsArray = ExtractTaxonomyLabels(responseBody);

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

        #region Private Methods

        private AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(IsRetryableStatusCode)
                .WaitAndRetryAsync(
                    3, // number of retries
                    CalculateRetryDelay,
                    OnRetryAsync
                );
        }

        private bool IsRetryableStatusCode(HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || // 503
                   response.StatusCode == System.Net.HttpStatusCode.Conflict || // 409
                   (int)response.StatusCode == 424; // Failed Dependency
        }

        private TimeSpan CalculateRetryDelay(int retryAttempt)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * 5); // exponential backoff  
        }

        private async Task OnRetryAsync(DelegateResult<HttpResponseMessage> outcome, TimeSpan timeSpan, int retryCount, Context context)
        {
            _logger.LogWarning($"Attempt {retryCount}: Request failed with status {outcome.Result?.StatusCode}");

            try
            {
                if (outcome.Result?.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    await HandleConflictError(outcome.Result);
                }
                else
                {
                    await RestartDeployment();
                }

                _logger.LogInformation("Ready to retry request");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during deployment restart: {ex.Message}");
            }
        }

        private async Task HandleConflictError(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogWarning($"Deployment is still initializing: {content}");

            _logger.LogInformation("Waiting 15 seconds for deployment to initialize...");
            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        private async Task RestartDeployment()
        {
            _logger.LogWarning("Attempting to start deployment...");
            await StartDeploymentAsync();

            _logger.LogInformation("Waiting for deployment to become active...");
            // Just wait for the deployment to become active  
            await WaitForDeploymentAsync(TimeSpan.FromSeconds(90));

            _logger.LogInformation("Deployment is active and ready for requests");
        }

        private void LogClassificationRequest(ClassifyRequest request)
        {
            _logger.LogInformation("Classification request received for text: {TextPreview}",
                request.Text?.Substring(0, Math.Min(50, request.Text?.Length ?? 0)) + "...");
        }

        private async Task StartDeploymentAsync()
        {
            try
            {
                var url = $"{_apiSettings.ApiUrl}/startDeployment?deploymentId={_apiSettings.DeploymentId}";
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
                    var deploymentStatus = await GetDeploymentStatus();

                    if (!string.IsNullOrEmpty(deploymentStatus) &&
                        (deploymentStatus.ToLower() == "active" || deploymentStatus.ToLower() == "running"))
                    {
                        _logger.LogInformation("Deployment is now active");
                        return;
                    }

                    _logger.LogInformation("Waiting for deployment to become active...");
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error checking deployment status: {ex.Message}");
                    await Task.Delay(3000);
                }
            }

            throw new TimeoutException("Deployment failed to become active within the specified timeout.");
        }

        private async Task<string?> GetDeploymentStatus()
        {
            var url = $"{_apiSettings.ApiUrl}/describeDeployment?deploymentId={_apiSettings.DeploymentId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apiKey", _apiSettings.ApiKey);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to get deployment status: {response.StatusCode}, {errorContent}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug($"Full deployment response: {content}");

            return ExtractDeploymentStatus(content);
        }

        private string? ExtractDeploymentStatus(string content)
        {
            try
            {
                var jsonResponse = JObject.Parse(content);
                string? status = null;

                // Try different paths to find the status
                if (jsonResponse["deployment"] != null)
                {
                    status = jsonResponse["deployment"]["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        _logger.LogInformation($"Found status in deployment.status: {status}");
                        return status;
                    }
                }

                if (jsonResponse["result"] != null)
                {
                    status = jsonResponse["result"]["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        _logger.LogInformation($"Found status in result.status: {status}");
                        return status;
                    }
                }

                status = jsonResponse["status"]?.ToString();
                if (!string.IsNullOrEmpty(status))
                {
                    _logger.LogInformation($"Found status directly: {status}");
                }

                _logger.LogInformation($"Current deployment status: {status ?? "unknown"}");
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing deployment status: {ex.Message}");
                return null;
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

            var url = $"{_apiSettings.ApiUrl}/execute_agent?deploymentToken={_apiSettings.DeploymentToken}&deploymentId={_apiSettings.DeploymentId}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug($"Sending request to {url}");
                var result = await _httpClient.PostAsync(url, content);
                _logger.LogDebug($"API response status: {result.StatusCode}");
                return result;
            });

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
                JObject jsonObject = JObject.Parse(responseBody);
                string segmentJsonString = jsonObject["result"]["segments"][0]["segment"].ToString();
                JObject segmentObject = JObject.Parse(segmentJsonString);
                JArray taxonomyLabels = (JArray)segmentObject["taxonomy_labels"];

                _logger.LogDebug("Extracted {Count} taxonomy labels", taxonomyLabels?.Count ?? 0);
                return taxonomyLabels?.ToObject<string[]>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting taxonomy labels from response");
                throw;
            }
        }

        #endregion
    }
}