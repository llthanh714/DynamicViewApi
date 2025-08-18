using System.Text.Json.Serialization;

namespace DynamicViewApi.Models.Response
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Metadata? Metadata { get; set; }
    }
}