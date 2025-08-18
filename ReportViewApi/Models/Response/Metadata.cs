using System.Reflection;

namespace DynamicViewApi.Models.Response
{
    public class Metadata
    {
        public Dictionary<string, FieldInfo> Fields { get; set; } = [];
        public SummaryInfo Summary { get; set; } = new();
    }
}