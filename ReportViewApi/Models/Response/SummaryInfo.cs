namespace DynamicViewApi.Models.Response
{
    public class SummaryInfo
    {
        public string Description { get; set; } = "N/A";
        public int TotalFields { get; set; }
        public string DataSource { get; set; } = "N/A";
        public List<string> MainCollections { get; set; } = [];
        public string FilterCriteria { get; set; } = "N/A";
    }
}