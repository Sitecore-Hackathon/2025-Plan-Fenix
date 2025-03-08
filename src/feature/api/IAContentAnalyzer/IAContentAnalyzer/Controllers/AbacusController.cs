using IAContentAnalyzer.Configuration;
using IAContentAnalyzer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private const string DeploymentToken = "[YOUR_DEPLOYMENT_TOKEN]";
        private const string DeploymentId = "[YOUR_DEPLOYMENT_ID]";
        private const string ApiUrl = "https://api.abacus.ai/api/v0/execute_agent";

        public AbacusController(
            IHttpClientFactory httpClientFactory,
            IOptions<AbacusApiSettings> apiSettings,
            ILogger<AbacusController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiSettings = apiSettings.Value;
            _logger = logger;
        }

        [HttpPost("classify")]
        public async Task<IActionResult> ClassifyText([FromBody] ClassifyRequest request)
        {
            try
            {
                _logger.LogInformation("Classification request received for text: {TextPreview}", 
                    request.Text?.Substring(0, Math.Min(50, request.Text?.Length ?? 0)) + "...");

                // 1. Prepare request payload
                var responseBody = await SendClassificationRequest(request.Text);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying text");

                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        private async Task<string> SendClassificationRequest(string? text)
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
            var response = await _httpClient.PostAsync(url, content);
            
            _logger.LogDebug("API response status: {StatusCode}", response.StatusCode);
            
            response.EnsureSuccessStatusCode();

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