namespace DevKit.Models
{
    public class LlmProvider
    {
        public string Name { get; set; }
        public string ModelId { get; set; }
        public string ApiKey { get; set; }
        public string CostTier { get; set; }
        public string DisplayName => $"☁ {Name} ({ModelId}) — {CostTier}";
    }
}
