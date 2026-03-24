using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DevKit.Helpers;
using DevKit.Models;
using DevKit.Services;

namespace DevKit.ViewModels
{
    public class EditorViewModel : ViewModelBase
    {
        private readonly RoslynCompilerService _compiler;
        private readonly ScriptManagerService _scriptManager;
        private readonly LocalLlmService _llmService;
        private readonly Dispatcher _dispatcher;

        private string _code = "TaskDialog.Show(\"Hello\", \"Hello from DevKit!\\nDocument: \" + doc.Title);";
        private string _buttonName = "My Script";
        private string _outputText = "", _statusText = "Ready", _statusColor = "#6C7086";
        private bool _isOutputError, _buttonsEnabled = true, _hasError, _isAiGenerating, _isDarkTheme = true;
        private Snippet _selectedSnippet;
        private ScriptEntry _selectedScript;
        private int _selectedTabIndex;
        private string _selectedGroup = "Scripts", _moveToGroup;
        private string _aiPrompt = "", _aiStatus = "Detecting LLMs...", _claudeApiKey = "";
        private LlmProvider _selectedProvider;
        private string _lastError = "";
        private UserSettings _settings;
        private double _totalCost;

        public string Code { get => _code; set => SetProperty(ref _code, value); }
        public string ButtonName { get => _buttonName; set => SetProperty(ref _buttonName, value); }
        public string OutputText { get => _outputText; set => SetProperty(ref _outputText, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public bool IsOutputError { get => _isOutputError; set => SetProperty(ref _isOutputError, value); }
        public bool ButtonsEnabled { get => _buttonsEnabled; set => SetProperty(ref _buttonsEnabled, value); }
        public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }
        public ScriptEntry SelectedScript { get => _selectedScript; set => SetProperty(ref _selectedScript, value); }
        public bool HasError { get => _hasError; set => SetProperty(ref _hasError, value); }
        public bool IsAiGenerating { get => _isAiGenerating; set => SetProperty(ref _isAiGenerating, value); }
        public string AiPrompt { get => _aiPrompt; set => SetProperty(ref _aiPrompt, value); }
        public string AiStatus { get => _aiStatus; set => SetProperty(ref _aiStatus, value); }
        public string ClaudeApiKey { get => _claudeApiKey; set => SetProperty(ref _claudeApiKey, value); }
        public string SelectedGroup { get => _selectedGroup; set => SetProperty(ref _selectedGroup, value); }
        public string MoveToGroup { get => _moveToGroup; set => SetProperty(ref _moveToGroup, value); }
        public bool IsClaudeSelected => _selectedProvider?.Type == LlmType.ClaudeApi;
        public bool IsDarkTheme { get => _isDarkTheme; set => SetProperty(ref _isDarkTheme, value); }
        public double TotalCost { get => _totalCost; set { SetProperty(ref _totalCost, value); OnPropertyChanged(nameof(TotalCostDisplay)); } }
        public string TotalCostDisplay => $"💰 ${_totalCost:F4}";
        public string ThemeIcon => _isDarkTheme ? "☀" : "🌙";

        public Snippet SelectedSnippet
        {
            get => _selectedSnippet;
            set { if (SetProperty(ref _selectedSnippet, value) && value != null) { Code = value.Code; ButtonName = value.Name; SelectedTabIndex = 0; _selectedSnippet = null; OnPropertyChanged(nameof(SelectedSnippet)); } }
        }
        public LlmProvider SelectedProvider
        {
            get => _selectedProvider;
            set { if (SetProperty(ref _selectedProvider, value)) OnPropertyChanged(nameof(IsClaudeSelected)); }
        }

        public ObservableCollection<Snippet> Snippets { get; } = new ObservableCollection<Snippet>();
        public ObservableCollection<ScriptEntry> SavedScripts { get; } = new ObservableCollection<ScriptEntry>();
        public ObservableCollection<LlmProvider> LlmProviders { get; } = new ObservableCollection<LlmProvider>();
        public ObservableCollection<ChatMessage> ChatHistory { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<string> Groups { get; } = new ObservableCollection<string>();

        public ICommand TestCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand LoadScriptCommand { get; }
        public ICommand DeleteScriptCommand { get; }
        public ICommand RefreshScriptsCommand { get; }
        public ICommand AiGenerateCommand { get; }
        public ICommand AiSendErrorCommand { get; }
        public ICommand AiClearChatCommand { get; }
        public ICommand RefreshLlmCommand { get; }
        public ICommand SaveApiKeyCommand { get; }
        public ICommand CreateGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand MoveScriptCommand { get; }
        public ICommand ExportScriptCommand { get; }
        public ICommand ImportPackageCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        public EditorViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _compiler = DevKitApp.Compiler;
            _scriptManager = DevKitApp.ScriptManager;
            _llmService = new LocalLlmService();
            _settings = UserSettings.Load(DevKitApp.ScriptsFolderPath);
            _claudeApiKey = _settings.ClaudeApiKey ?? "";

            TestCommand = new RelayCommand(ExecuteTest);
            AddCommand = new RelayCommand(ExecuteAdd);
            ClearCommand = new RelayCommand(() => { OutputText = ""; IsOutputError = false; HasError = false; SetStatus("Ready"); });
            LoadScriptCommand = new RelayCommand(ExecuteLoadScript);
            DeleteScriptCommand = new RelayCommand(ExecuteDeleteScript);
            RefreshScriptsCommand = new RelayCommand(ExecuteRefreshScripts);
            AiGenerateCommand = new RelayCommand(async () => await ExecuteAiGenerate());
            AiSendErrorCommand = new RelayCommand(async () => await ExecuteAiSendError());
            AiClearChatCommand = new RelayCommand(() => { _llmService.ClearHistory(); ChatHistory.Clear(); AiStatus = "Chat cleared."; });
            RefreshLlmCommand = new RelayCommand(async () => await DetectLlmProviders());
            SaveApiKeyCommand = new RelayCommand(ExecuteSaveApiKey);
            CreateGroupCommand = new RelayCommand(ExecuteCreateGroup);
            DeleteGroupCommand = new RelayCommand(ExecuteDeleteGroup);
            MoveScriptCommand = new RelayCommand(ExecuteMoveScript);
            ExportScriptCommand = new RelayCommand(ExecuteExportScript);
            ImportPackageCommand = new RelayCommand(ExecuteImportPackage);
            ToggleThemeCommand = new RelayCommand<object>(ExecuteToggleTheme);

            foreach (var s in SnippetService.GetAll()) Snippets.Add(s);

            SelectedSnippet = Snippets.FirstOrDefault();
            ExecuteRefreshScripts();
            RefreshGroups();
            _ = DetectLlmProviders();
        }

        // ── THEME ──
        private void ExecuteToggleTheme(object param)
        {
            if (param is Window w)
            {
                IsDarkTheme = !IsDarkTheme;
                ThemeManager.ApplyToWindow(w, IsDarkTheme);
                OnPropertyChanged(nameof(ThemeIcon));
            }
        }

        // ── TEST ──
        private void ExecuteTest()
        {
            string code = Code?.Trim();
            if (string.IsNullOrEmpty(code)) { ShowError("No code to compile."); return; }
            SetStatus("Compiling...", "#F9E2AF"); ButtonsEnabled = false; HasError = false;
            try
            {
                var result = _compiler.CompileForTest(code, RoslynCompilerService.GenerateClassName());
                if (!result.Success) { ShowError("COMPILATION ERRORS:\n\n" + result.ErrorSummary); _lastError = result.ErrorSummary; HasError = true; SetStatus("Compilation failed.", "#F38BA8"); ButtonsEnabled = true; return; }
                ShowOutput("Compilation succeeded. Executing..."); SetStatus("Executing...", "#F9E2AF");
                SharedState.CompiledAssembly = result.CompiledAssembly; SharedState.ClassName = result.ClassName;
                SharedState.OnResultCallback = (ok, msg) => _dispatcher.Invoke(() =>
                {
                    if (ok) ShowOutput("Compilation succeeded. Executing...\n[OK] " + msg);
                    else { ShowError("Compilation succeeded. Executing...\n[FAIL] " + msg); _lastError = msg; HasError = true; }
                    SetStatus(ok ? "Test passed!" : "Test failed.", ok ? "#A6E3A1" : "#F38BA8");
                    ButtonsEnabled = true;
                });
                DevKitApp.TestEvent.Raise();
            }
            catch (Exception ex) { ShowError($"Error:\n{ex.Message}"); _lastError = ex.Message; HasError = true; SetStatus("Error.", "#F38BA8"); ButtonsEnabled = true; }
        }

        // ── ADD ──
        private void ExecuteAdd()
        {
            string code = Code?.Trim(), name = ButtonName?.Trim();
            if (string.IsNullOrEmpty(code)) { ShowError("No code."); return; }
            if (string.IsNullOrEmpty(name)) { ShowError("Enter a button name."); return; }
            SetStatus("Compiling to DLL...", "#F9E2AF"); ButtonsEnabled = false; HasError = false;
            try
            {
                string className = RoslynCompilerService.GenerateClassName();
                string dllFileName = $"{MakeSafe(name)}_{Guid.NewGuid().ToString("N").Substring(0, 6)}.dll";
                string dllPath = Path.Combine(DevKitApp.ScriptsFolderPath, dllFileName);
                var result = _compiler.CompileForBuild(code, className, dllPath);
                if (!result.Success) { ShowError("COMPILATION ERRORS:\n\n" + result.ErrorSummary); _lastError = result.ErrorSummary; HasError = true; SetStatus("Compilation failed.", "#F38BA8"); ButtonsEnabled = true; return; }
                string group = SelectedGroup ?? "Scripts";
                var entry = _scriptManager.AddScript(name, result.ClassName, dllFileName, code, group);
                SharedState.DllPath = dllPath; SharedState.ClassName = result.ClassName; SharedState.ButtonName = name; SharedState.ButtonId = entry.Id; SharedState.GroupName = group;
                SharedState.OnResultCallback = (ok, msg) => _dispatcher.Invoke(() =>
                {
                    if (ok) ShowOutput($"[OK] {msg}");
                    else ShowError($"[FAIL] {msg}");
                    SetStatus(ok ? "Button added!" : "Failed.", ok ? "#A6E3A1" : "#F38BA8");
                    ButtonsEnabled = true; ExecuteRefreshScripts();
                });
                DevKitApp.AddEvent.Raise();
            }
            catch (Exception ex) { ShowError($"Error:\n{ex.Message}"); _lastError = ex.Message; HasError = true; SetStatus("Error.", "#F38BA8"); ButtonsEnabled = true; }
        }

        // ── SCRIPTS ──
        private void ExecuteRefreshScripts() { SavedScripts.Clear(); foreach (var e in _scriptManager.LoadManifest()) SavedScripts.Add(e); }
        private void ExecuteLoadScript()
        {
            if (SelectedScript == null) { ShowError("Select a script."); return; }
            string src = _scriptManager.GetScriptSource(SelectedScript.Id);
            if (src != null) { Code = src; ButtonName = SelectedScript.ButtonName; SelectedTabIndex = 0; SetStatus($"Loaded: {SelectedScript.ButtonName}", "#A6E3A1"); }
            else ShowError("Source not found.");
        }
        private void ExecuteDeleteScript()
        {
            if (SelectedScript == null) { ShowError("Select a script."); return; }
            if (MessageBox.Show($"Delete '{SelectedScript.ButtonName}'?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SharedState.DeleteScriptId = SelectedScript.Id;
            SharedState.OnResultCallback = (ok, msg) => _dispatcher.Invoke(() => { ShowOutput(msg); SetStatus(ok ? "Deleted." : "Failed.", ok ? "#A6E3A1" : "#F38BA8"); ExecuteRefreshScripts(); });
            DevKitApp.DeleteEvent.Raise();
        }

        // ── EXPORT/IMPORT ──
        private void ExecuteExportScript()
        {
            if (SelectedScript == null) { ShowError("Select a script to export."); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export Tool Package", FileName = SelectedScript.ButtonName + ".dkpkg", Filter = "DevKit Package (*.dkpkg)|*.dkpkg" };
            if (dlg.ShowDialog() != true) return;
            try { new ToolPackageService(_scriptManager, DevKitApp.ScriptsFolderPath).ExportScripts(new List<ScriptEntry> { SelectedScript }, dlg.FileName); ShowOutput($"Exported to:\n{dlg.FileName}"); SetStatus($"Exported '{SelectedScript.ButtonName}'", "#A6E3A1"); }
            catch (Exception ex) { ShowError($"Export failed: {ex.Message}"); }
        }
        private void ExecuteImportPackage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Import Tool Package", Filter = "DevKit Package (*.dkpkg)|*.dkpkg" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var imported = new ToolPackageService(_scriptManager, DevKitApp.ScriptsFolderPath).ImportPackage(dlg.FileName, SelectedGroup ?? "Scripts");
                ExecuteRefreshScripts(); RefreshGroups();

                // Register each imported script on the ribbon immediately
                foreach (var entry in imported)
                {
                    string dllPath = Path.Combine(DevKitApp.ScriptsFolderPath, entry.DllFileName);
                    if (!File.Exists(dllPath)) continue;

                    string group = entry.Group ?? "Scripts";
                    if (!DevKitApp.GroupPulldowns.ContainsKey(group))
                    {
                        SharedState.GroupName = group;
                        SharedState.OnResultCallback = null;
                        DevKitApp.CreateGroupEvent.Raise();
                    }

                    SharedState.DllPath = dllPath;
                    SharedState.ClassName = entry.ClassName;
                    SharedState.ButtonName = entry.ButtonName;
                    SharedState.ButtonId = entry.Id;
                    SharedState.GroupName = group;
                    SharedState.OnResultCallback = null;
                    DevKitApp.AddEvent.Raise();
                }

                ShowOutput($"Imported {imported.Count} tool(s):\n" + string.Join("\n", imported.Select(s => $"  • {s.ButtonName}")) + "\n\nButtons added to ribbon!");
                SetStatus($"Imported {imported.Count} tool(s)", "#A6E3A1");
            }
            catch (Exception ex) { ShowError($"Import failed: {ex.Message}"); }
        }

        // ── GROUPS ──
        private void RefreshGroups() { Groups.Clear(); foreach (var g in _scriptManager.LoadGroups()) Groups.Add(g); if (!Groups.Contains(SelectedGroup)) SelectedGroup = Groups.FirstOrDefault() ?? "Scripts"; }
        private void ExecuteCreateGroup()
        {
            string name = "";
            var dlg = new Window { Title = "New Group", Width = 360, Height = 160, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E)) };
            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
            var tb = new System.Windows.Controls.TextBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x25)), Foreground = System.Windows.Media.Brushes.White, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x45, 0x47, 0x5A)) };
            var btn = new System.Windows.Controls.Button { Content = "Create", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(20, 8, 20, 8) };
            btn.Click += (s, e) => { name = tb.Text; dlg.DialogResult = true; };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "Group name:", Foreground = System.Windows.Media.Brushes.White, FontSize = 13, Margin = new Thickness(0, 0, 0, 8) });
            sp.Children.Add(tb); sp.Children.Add(btn); dlg.Content = sp;
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim(); _scriptManager.AddGroup(name);
            SharedState.GroupName = name; SharedState.OnResultCallback = (ok, msg) => _dispatcher.Invoke(() => { if (ok) SetStatus($"Group '{name}' added!", "#A6E3A1"); else ShowError(msg); });
            DevKitApp.CreateGroupEvent.Raise(); RefreshGroups(); SelectedGroup = name;
        }
        private void ExecuteDeleteGroup()
        {
            if (string.IsNullOrEmpty(SelectedGroup) || SelectedGroup == "Scripts") { ShowError("Cannot delete default group."); return; }
            if (MessageBox.Show($"Delete group '{SelectedGroup}'?", "Delete Group", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _scriptManager.RemoveGroup(SelectedGroup); RefreshGroups(); ExecuteRefreshScripts(); SetStatus("Group deleted.", "#A6E3A1");
        }
        private void ExecuteMoveScript()
        {
            try
            {
                if (SelectedScript == null) { ShowError("Select a script."); return; }
                if (string.IsNullOrEmpty(MoveToGroup)) { ShowError("Select target group."); return; }
                if (SelectedScript.Group == MoveToGroup) { ShowError("Already in that group."); return; }
                string old = SelectedScript.Group, bn = SelectedScript.ButtonName; _scriptManager.MoveScript(SelectedScript.Id, MoveToGroup); ExecuteRefreshScripts(); SetStatus($"Moved '{bn}' [{old}] → [{MoveToGroup}]", "#A6E3A1");
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        // ── AI ──
        private async Task DetectLlmProviders()
        {
            AiStatus = "Scanning..."; LlmProviders.Clear();
            try
            {
                foreach (var p in LocalLlmService.GetClaudeProviders(_claudeApiKey, _settings.ClaudeModel)) LlmProviders.Add(p);
                foreach (var p in await _llmService.DetectProvidersAsync()) if (!LlmProviders.Any(x => x.BaseUrl == p.BaseUrl && x.ModelId == p.ModelId)) LlmProviders.Add(p);
                if (LlmProviders.Count > 0) { var local = LlmProviders.FirstOrDefault(p => p.Type != LlmType.ClaudeApi); SelectedProvider = !string.IsNullOrEmpty(_claudeApiKey) ? LlmProviders[0] : local ?? LlmProviders[0]; AiStatus = $"{LlmProviders.Count} model(s) available"; }
                else AiStatus = "No LLMs found.";


                SelectedProvider = LlmProviders.FirstOrDefault();

            }
            catch (Exception ex) { AiStatus = $"Error: {ex.Message}"; }
        }
        private void ExecuteSaveApiKey()
        {
            string key = ClaudeApiKey?.Trim() ?? ""; _claudeApiKey = key; _settings.ClaudeApiKey = key; _settings.Save(DevKitApp.ScriptsFolderPath);
            foreach (var p in LlmProviders.Where(p => p.Type == LlmType.ClaudeApi)) p.ApiKey = key;
            AiStatus = string.IsNullOrEmpty(key) ? "API key removed." : "API key saved!";
        }
        private async Task ExecuteAiGenerate()
        {
            if (SelectedProvider == null) { ShowError("No LLM selected."); return; }
            if (SelectedProvider.Type == LlmType.ClaudeApi && string.IsNullOrWhiteSpace(SelectedProvider.ApiKey)) { ShowError("Enter Claude API key and click Save."); return; }
            string prompt = AiPrompt?.Trim(); if (string.IsNullOrEmpty(prompt)) { ShowError("Enter a prompt."); return; }
            IsAiGenerating = true; AiStatus = "Generating..."; SetStatus("AI working...", "#CBA6F7");
            ChatHistory.Add(new ChatMessage { Role = "user", Content = prompt });
            try
            {
                var resp = await _llmService.SendMessageAsync(SelectedProvider, prompt);
                string raw = resp.Text;
                if (SelectedProvider.Type == LlmType.ClaudeApi)
                    AddCost(SelectedProvider.ModelId, resp.InputTokens, resp.OutputTokens);

                if (string.IsNullOrWhiteSpace(raw)) { ShowError("Empty response."); ChatHistory.Add(new ChatMessage { Role = "assistant", Content = "(empty)" }); return; }
                var (chat, code) = LocalLlmService.SplitResponse(raw);
                ChatHistory.Add(new ChatMessage { Role = "assistant", Content = raw });
                if (!string.IsNullOrEmpty(code))
                {
                    Code = code; SelectedTabIndex = 0; AiStatus = "Code generated! Test with F5."; SetStatus("Code ready — review and test.", "#A6E3A1");
                    ShowOutput(!string.IsNullOrEmpty(chat) ? $"{chat}\n\n[Code placed in editor]" : "Code placed in editor. Press F5 to test.");
                }
                else { AiStatus = "Response received."; SetStatus("AI responded.", "#7AA2F7"); ShowOutput(chat); }
            }
            catch (Exception ex) { ShowError($"AI error:\n{ex.Message}"); AiStatus = $"Error: {ex.Message}"; SetStatus("AI failed.", "#F38BA8"); }
            finally { IsAiGenerating = false; }
        }
        private async Task ExecuteAiSendError()
        {
            if (SelectedProvider == null) { ShowError("No LLM selected."); return; }
            if (SelectedProvider.Type == LlmType.ClaudeApi && string.IsNullOrWhiteSpace(SelectedProvider.ApiKey)) { ShowError("Enter Claude API key."); return; }
            if (string.IsNullOrEmpty(_lastError)) { ShowError("No error. Test first (F5)."); return; }
            IsAiGenerating = true; AiStatus = "Fixing..."; SetStatus("AI fixing...", "#CBA6F7");
            ChatHistory.Add(new ChatMessage { Role = "user", Content = $"[Error Fix]\n{_lastError}" });
            try
            {
                var resp = await _llmService.SendMessageAsync(SelectedProvider, _llmService.BuildErrorFixMessage(Code?.Trim() ?? "", _lastError));
                string raw = resp.Text;
                if (SelectedProvider.Type == LlmType.ClaudeApi)
                    AddCost(SelectedProvider.ModelId, resp.InputTokens, resp.OutputTokens);

                var (_, code) = LocalLlmService.SplitResponse(raw);
                string fixedCode = !string.IsNullOrEmpty(code) ? code : raw;
                if (!string.IsNullOrWhiteSpace(fixedCode))
                {
                    Code = fixedCode; SelectedTabIndex = 0; HasError = false;
                    AiStatus = "Fixed! Test with F5."; SetStatus("Code fixed — test it.", "#A6E3A1");
                    ShowOutput("Code fixed by AI. Press F5 to test.");
                    ChatHistory.Add(new ChatMessage { Role = "assistant", Content = fixedCode });
                }
                else { ShowError("Empty fix."); }
            }
            catch (Exception ex) { ShowError($"AI error:\n{ex.Message}"); AiStatus = $"Error: {ex.Message}"; }
            finally { IsAiGenerating = false; }
        }

        // ── Helpers ──
        private void SetStatus(string t, string c = "#6C7086") { StatusText = t; StatusColor = c; }
        private void ShowOutput(string t) { OutputText = t; IsOutputError = false; }
        private void ShowError(string t) { OutputText = t; IsOutputError = true; }
        private string MakeSafe(string n) { foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n.Replace(' ', '_'); }
        private void AddCost(string modelId, int inputTokens, int outputTokens)
        {
            double inputRate = 0, outputRate = 0;
            if (modelId.Contains("haiku")) { inputRate = 1.0; outputRate = 5.0; }
            else if (modelId.Contains("sonnet")) { inputRate = 3.0; outputRate = 15.0; }
            else if (modelId.Contains("opus-4-5")) { inputRate = 5.0; outputRate = 25.0; }
            else if (modelId.Contains("opus")) { inputRate = 15.0; outputRate = 75.0; }

            TotalCost += (inputTokens * inputRate / 1_000_000) + (outputTokens * outputRate / 1_000_000);
        }
    }
}
