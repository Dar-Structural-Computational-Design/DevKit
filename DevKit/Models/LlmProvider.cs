namespace DevKit.Models
{
    public class LlmProvider
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public string ModelId { get; set; }
        public LlmType Type { get; set; }
        public bool IsAvailable { get; set; }
        public string ApiKey { get; set; }
        public string DisplayName => Type == LlmType.ClaudeApi ? $"☁ {Name} ({ModelId})" : $"{Name} ({ModelId})";
    }

    public enum LlmType { OllamaLocal, LmStudio, OpenAiCompatible, ClaudeApi }
}
