using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevKit.Models;

namespace DevKit.Services
{
    public class LocalLlmService
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        private static readonly (string Name, string BaseUrl, LlmType Type)[] KnownEndpoints =
        {
            ("Ollama",    "http://localhost:11434", LlmType.OllamaLocal),
            ("LM Studio", "http://localhost:1234",  LlmType.LmStudio),
            ("Jan",       "http://localhost:1337",  LlmType.OpenAiCompatible),
            ("LocalAI",   "http://localhost:8080",  LlmType.OpenAiCompatible),
        };

        public const string SYSTEM_PROMPT = @"You are a helpful Revit development assistant inside a tool called DevKit.
You can have normal conversations, answer questions, discuss Revit API concepts, and help plan solutions.

When the user asks you to WRITE or GENERATE code, follow these rules for the code output:
- The user writes only the BODY of an IExternalCommand.Execute method that returns Result.
- These variables are already declared: UIApplication uiApp, UIDocument uiDoc, Document doc
- The code is wrapped in a try/catch that already returns Result.Succeeded at the end and Result.Failed on exception.
- Do NOT add return Result.Succeeded at the end — it is already handled by the wrapper.
- Do NOT add return Result.Failed or try/catch — the wrapper handles exceptions.
- If you need an early exit, use: return Result.Cancelled;
- NEVER use bare ""return;"" — always use ""return Result.Cancelled;"" or ""return Result.Succeeded;"" for early exits.- These usings are already included: System, System.Collections.Generic, System.Linq, System.IO,
  Autodesk.Revit.UI, Autodesk.Revit.UI.Selection, Autodesk.Revit.DB, Autodesk.Revit.DB.Architecture,
  Autodesk.Revit.DB.Structure.
- Output ONLY the C# code body. No class, no method signature, no markdown fences.
- You are not allowed to use classes, create the code in one full structured code.
- Use Transaction for any model modification.
- Use TaskDialog.Show() to display results to the user.
- Any classes from external packages, use the full qualified class name.
- For Excel operations, use NPOI Package.
- For JSON operations, use Newtonsoft.Json Package.
-You may create a UI window if the user asks you to, but you have to fully create in C# with out creating a new class.
- Any new UI window you create, make it minimal as possible and without any styles

COMPLEXITY RULES — IMPORTANT:
- You can ONLY create simple, single-purpose tools. Simple means: collect/filter elements, read/set parameters, change types, move/copy/delete, create basic elements (walls, columns, beams by points), show info via TaskDialog, color/override elements in views, simple transactions.
- If the user asks for something complex, DO NOT generate code. Instead, respond with a short message explaining why it is complex and tell them to contact the Computational Design Team (CDT).
- If borderline, err on the side of refusing and recommending CDT.

BEHAVIOR RULES:
- If the user's request is unclear or ambiguous, ASK CLARIFYING QUESTIONS before writing code.
- If the user is just chatting, asking questions, or discussing ideas, respond naturally as a helpful assistant.
- Only output raw code when you are confident you understand exactly what the user wants.
- When outputting code, output ONLY the code — no explanations mixed in.
-These Variables names are already taken : commandData, message, elements
- If you need to explain something alongside code, put the explanation FIRST, then a line that says exactly: ---CODE---
  Then put only the code after that marker.
- When the user reports an error, fix the code and output the COMPLETE fixed code body.
- When the user asks to modify existing code, output the COMPLETE modified code body.
- NEVER wrap code in markdown fences like ```csharp or ```. Output raw code only.
- NEVER add explanatory text mixed into the code. If you must explain, put it BEFORE the code separated by ---CODE---
- When modifying existing code, output the COMPLETE modified code body — raw code only, no markdown, no commentary inside the code.
- NEVER define classes, structs, enums, interfaces, or methods outsode of the method's body. All code must be inline within the method body.
- If you need a selection filter, use an anonymous/lambda approach or inline the logic. Do NOT create a separate ISelectionFilter class.
- For selection filters, use this pattern instead of creating a class:
  var ref = uiDoc.Selection.PickObject(ObjectType.Element, ""Select an element"");
  Element elem = doc.GetElement(ref);
  if (elem.Category.Id.IntegerValue != (int)BuiltInCategory.OST_Floors) { TaskDialog.Show(""Error"", ""Not a floor""); return Result.Cancelled; }";


        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

        public LocalLlmService()
        {
            _history.Add(new ChatMessage { Role = "system", Content = SYSTEM_PROMPT });
        }

        public void ClearHistory()
        {
            _history.Clear();
            _history.Add(new ChatMessage { Role = "system", Content = SYSTEM_PROMPT });
        }

        public async Task<List<LlmProvider>> DetectProvidersAsync()
        {
            var providers = new List<LlmProvider>();
            foreach (var (name, baseUrl, type) in KnownEndpoints)
            {
                try
                {
                    var models = await GetModelsAsync(baseUrl, type);
                    foreach (var modelId in models)
                        providers.Add(new LlmProvider { Name = name, BaseUrl = baseUrl, ModelId = modelId, Type = type, IsAvailable = true });
                }
                catch { }
            }
            return providers;
        }

        public static List<LlmProvider> GetClaudeProviders(string apiKey = null, string preferredModel = null)
        {
            var models = new[] { "claude-sonnet-4-20250514", "claude-haiku-4-5-20251001" };
            var providers = models.Select(m => new LlmProvider
            {
                Name = "Claude", BaseUrl = "https://api.anthropic.com", ModelId = m,
                Type = LlmType.ClaudeApi, IsAvailable = true, ApiKey = apiKey ?? ""
            }).ToList();

            if (!string.IsNullOrEmpty(preferredModel))
            {
                var p = providers.FirstOrDefault(x => x.ModelId == preferredModel);
                if (p != null) { providers.Remove(p); providers.Insert(0, p); }
            }
            return providers;
        }

        /// <summary>
        /// Sends a message and returns the raw response text.
        /// The caller decides whether it's code or conversation.
        /// </summary>
        public async Task<LlmResponse> SendMessageAsync(LlmProvider provider, string userMessage)
        {
            string reminder = "\n\n[RULES: Raw code only. No markdown fences. No classes/structs/enums. " +
                    "Inline everything. No bare return; — use return Result.Cancelled; for early exit. " +
                    "Validate selections with LINQ after PickObject, not ISelectionFilter.]";

            _history.Add(new ChatMessage { Role = "user", Content = userMessage + reminder });

            // Trim: keep system prompt + last 5 messages
            TrimHistory(5);

            try
            {
                LlmResponse response;
                if (provider.Type == LlmType.ClaudeApi) response = await CallClaudeApiAsync(provider);
                else if (provider.Type == LlmType.OllamaLocal) response = await CallOllamaChatAsync(provider);
                else response = await CallOpenAiChatAsync(provider);

                _history.Add(new ChatMessage { Role = "assistant", Content = response.Text });
                return response;
            }
            catch { _history.RemoveAt(_history.Count - 1); throw; }
        }

        /// <summary>
        /// Extracts code from a response that may contain conversation + ---CODE--- marker.
        /// Returns (chatText, codeText). If no marker, tries to detect if it's pure code.
        /// </summary>
        public static (string chat, string code) SplitResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ("", "");

            string text = raw.Trim();

            // Check for ---CODE--- separator
            if (text.Contains("---CODE---"))
            {
                int idx = text.IndexOf("---CODE---");
                string chat = text.Substring(0, idx).Trim();
                string code = CleanCode(text.Substring(idx + "---CODE---".Length).Trim());
                return (chat, code);
            }

            // Try to clean as pure code
            string cleaned = CleanCode(text);

            // Heuristic: does it look like C# code?
            bool looksLikeCode = cleaned.Contains(";") &&
                (cleaned.Contains("var ") || cleaned.Contains("new ") ||
                 cleaned.Contains("TaskDialog") || cleaned.Contains("FilteredElementCollector") ||
                 cleaned.Contains("Transaction") || cleaned.Contains("using ("));

            if (looksLikeCode)
                return ("", cleaned);

            // It's just conversation
            return (text, "");
        }
        private void TrimHistory(int maxMessages)
        {
            // Count non-system messages
            var nonSystem = _history.Where(m => m.Role != "system").ToList();
            if (nonSystem.Count <= maxMessages) return;

            // Keep system prompt + last N messages
            var system = _history.Where(m => m.Role == "system").ToList();
            var keep = nonSystem.Skip(nonSystem.Count - maxMessages).ToList();

            _history.Clear();
            _history.AddRange(system);
            _history.AddRange(keep);
        }
        public string BuildErrorFixMessage(string currentCode, string errorText)
        {
            return $"The following code has an error. Fix it and return the COMPLETE corrected code body.\n\nCURRENT CODE:\n{currentCode}\n\nERROR:\n{errorText}";
        }

        // ── API Calls ──

        private async Task<LlmResponse> CallClaudeApiAsync(LlmProvider provider)
        {
            string sys = _history.Where(m => m.Role == "system").Select(m => m.Content).FirstOrDefault() ?? "";
            var msgs = _history.Where(m => m.Role != "system").Select(m => new { role = m.Role, content = m.Content }).ToArray();
            var payload = new { model = provider.ModelId, max_tokens = 4096, system = sys, messages = msgs };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            { Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json") };
            req.Headers.Add("x-api-key", provider.ApiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Claude API error ({resp.StatusCode}): {await resp.Content.ReadAsStringAsync()}");

            var obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var blocks = obj["content"]?.ToObject<JArray>();
            string text = blocks == null ? "" : string.Join("\n", blocks.Where(b => b["type"]?.ToString() == "text").Select(b => b["text"]?.ToString() ?? ""));

            // Extract token usage
            int inputTokens = obj["usage"]?["input_tokens"]?.ToObject<int>() ?? 0;
            int outputTokens = obj["usage"]?["output_tokens"]?.ToObject<int>() ?? 0;

            return new LlmResponse { Text = text, InputTokens = inputTokens, OutputTokens = outputTokens };
        }

        private async Task<LlmResponse> CallOllamaChatAsync(LlmProvider provider)
        {
            var msgs = _history.Select(m => new { role = m.Role, content = m.Content }).ToArray();
            var payload = new { model = provider.ModelId, messages = msgs, stream = false, options = new { temperature = 0.3 } };
            var resp = await _http.PostAsync($"{provider.BaseUrl}/api/chat",
                new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            var obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return new LlmResponse
            {
                Text = obj["message"]?["content"]?.ToString() ?? "",
                InputTokens = obj["prompt_eval_count"]?.ToObject<int>() ?? 0,
                OutputTokens = obj["eval_count"]?.ToObject<int>() ?? 0
            };
        }

        private async Task<LlmResponse> CallOpenAiChatAsync(LlmProvider provider)
        {
            var msgs = _history.Select(m => new { role = m.Role, content = m.Content }).ToArray();
            var payload = new { model = provider.ModelId, messages = msgs, temperature = 0.3, max_tokens = 2000 };
            var resp = await _http.PostAsync($"{provider.BaseUrl}/v1/chat/completions",
                new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            var obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return new LlmResponse
            {
                Text = obj["choices"]?[0]?["message"]?["content"]?.ToString() ?? "",
                InputTokens = obj["usage"]?["prompt_tokens"]?.ToObject<int>() ?? 0,
                OutputTokens = obj["usage"]?["completion_tokens"]?.ToObject<int>() ?? 0
            };
        }

        private async Task<List<string>> GetModelsAsync(string baseUrl, LlmType type)
        {
            string url = type == LlmType.OllamaLocal ? $"{baseUrl}/api/tags" : $"{baseUrl}/v1/models";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var arr = type == LlmType.OllamaLocal ? obj["models"]?.ToObject<JArray>() : obj["data"]?.ToObject<JArray>();
            string key = type == LlmType.OllamaLocal ? "name" : "id";
            return arr?.Select(m => m[key]?.ToString()).Where(n => n != null).ToList() ?? new List<string>();
        }

        private static string CleanCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            string code = raw.Trim();

            if (code.StartsWith("```"))
            {
                int nl = code.IndexOf('\n');
                if (nl > 0) code = code.Substring(nl + 1);
                if (code.EndsWith("```")) code = code.Substring(0, code.Length - 3);
            }

            var lines = code.Split(new[] { '\n' }, StringSplitOptions.None).ToList();
            // Don't strip usings — the compiler now extracts them via {2} placeholder
            string joined = string.Join("\n", lines).Trim();

            if (joined.Contains("public class ") && joined.Contains("IExternalCommand"))
            {
                int bc = 0, bs = -1, be = -1;
                for (int i = 0; i < joined.Length; i++)
                {
                    if (joined[i] == '{') { bc++; if (bc == 3) bs = i + 1; }
                    else if (joined[i] == '}') { if (bc == 3) { be = i; break; } bc--; }
                }
                if (bs > 0 && be > bs) joined = joined.Substring(bs, be - bs).Trim();
            }
            return joined.Trim();
        }
    }
}
