using System.ComponentModel.DataAnnotations;

namespace DynamicViewApi.Models
{
    public class ViewQueryRequest
    {
        // Tên của view cần truy vấn
        [Required]
        public string? ViewName { get; set; }

        public Dictionary<string, object>? Parameters { get; set; }
    }
}