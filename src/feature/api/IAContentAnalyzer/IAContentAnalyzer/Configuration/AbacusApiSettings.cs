namespace IAContentAnalyzer.Configuration
{
    public class AbacusApiSettings
    {
        public string DeploymentToken { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = "https://api.abacus.ai/api/v0/execute_agent";
    }
}
