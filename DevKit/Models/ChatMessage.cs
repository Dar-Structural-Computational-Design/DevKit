namespace DevKit.Models
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant";
        public string DisplayLabel => IsUser ? "You" : IsAssistant ? "AI" : "System";
    }
}
