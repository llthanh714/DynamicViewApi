namespace DynamicViewApi.Models.Response
{
    public class FieldInfo
    {
        // Các thuộc tính này có thể được mở rộng để lấy từ 'Extended Properties' trong SQL Server
        public string Description { get; set; } = "N/A";
        public string Source { get; set; } = "N/A";
        public string Type { get; set; } = "N/A";
    }
}