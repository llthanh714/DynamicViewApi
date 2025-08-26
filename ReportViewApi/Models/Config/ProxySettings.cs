namespace DynamicViewApi.Models.Config
{
    public class ProxySettings
    {
        public List<ProxyEndpoint> Endpoints { get; set; } = [];
    }

    public class ProxyEndpoint
    {
        public string OrgCode { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}