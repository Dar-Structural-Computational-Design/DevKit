using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private bool _isOutputError, _buttonsEnabled = true, _hasError, _isAiGenerating;
        private ThemeInfo _selectedTheme;
        private Window _ownerWindow;
        private Snippet _selectedSnippet;
        private ScriptEntry _selectedScript;
        private int _selectedTabIndex;
        private string _selectedGroup = "Scripts", _moveToGroup;
        private string _aiPrompt = "", _aiStatus = "Detecting LLMs...", _claudeApiKey = "";
        private LlmProvider _selectedProvider;
        private string _lastError = "";
        private UserSettings _settings;
        private double _totalCost;
        private bool _isTurboMode;
        private const double DAILY_LIMIT_NORMAL = 1.0;
        private const double DAILY_LIMIT_TURBO = 5.0;
        private const string TURBO_PASSWORD = "devkit2026";
        private string _manualPrompt = "", _generatedPrompt = "";

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
        public string ManualPrompt { get => _manualPrompt; set => SetProperty(ref _manualPrompt, value); }
        public string GeneratedPrompt { get => _generatedPrompt; set => SetProperty(ref _generatedPrompt, value); }
        public bool IsClaudeSelected => _selectedProvider?.Type == LlmType.ClaudeApi;
        public List<ThemeInfo> AvailableThemes { get; } = ThemeManager.GetAllThemes();
        public ThemeInfo SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && value != null && _ownerWindow != null)
                    ThemeManager.ApplyToWindow(_ownerWindow, value.Key);
            }
        }
        public double TotalCost { get => _totalCost; set { SetProperty(ref _totalCost, value); OnPropertyChanged(nameof(TotalCostDisplay)); OnPropertyChanged(nameof(DailyLimitDisplay)); } }
        public string TotalCostDisplay => $"💰 ${_totalCost:F4}";
        public bool IsTurboMode { get => _isTurboMode; set { if (SetProperty(ref _isTurboMode, value)) { OnPropertyChanged(nameof(TurboModeLabel)); OnPropertyChanged(nameof(DailyLimitDisplay)); } } }
        public string TurboModeLabel => _isTurboMode ? "TURBO ON" : "TURBO OFF";
        public double DailyLimit => _isTurboMode ? DAILY_LIMIT_TURBO : DAILY_LIMIT_NORMAL;
        public string DailyLimitDisplay => $"${_totalCost:F2} / ${DailyLimit:F2}";

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
        public ICommand ToggleTurboCommand { get; }
        public ICommand GenerateManualPromptCommand { get; }
        public ICommand CopyManualPromptCommand { get; }
        public ICommand PasteCodeFromClipboardCommand { get; }

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
            ToggleTurboCommand = new RelayCommand(ExecuteToggleTurbo);
            GenerateManualPromptCommand = new RelayCommand(ExecuteGenerateManualPrompt);
            CopyManualPromptCommand = new RelayCommand(ExecuteCopyManualPrompt);
            PasteCodeFromClipboardCommand = new RelayCommand(ExecutePasteCodeFromClipboard);

            // Load daily cost — reset if it's a new day
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_settings.DailyCostDate == today)
            {
                _totalCost = _settings.DailyCost;
                _isTurboMode = _settings.IsTurboMode;
            }
            else
            {
                _settings.DailyCost = 0;
                _settings.DailyCostDate = today;
                _settings.IsTurboMode = false;
                _settings.Save(DevKitApp.ScriptsFolderPath);
            }

            _selectedTheme = AvailableThemes[0];

            foreach (var s in SnippetService.GetAll()) Snippets.Add(s);

            SelectedSnippet = Snippets.FirstOrDefault();
            ExecuteRefreshScripts();
            RefreshGroups();
            _ = DetectLlmProviders();
        }

        // ── THEME ──
        public void SetOwnerWindow(Window w) => _ownerWindow = w;

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
            if (SelectedProvider.Type == LlmType.ClaudeApi && !CheckDailyLimit()) return;
            string prompt = AiPrompt?.Trim(); if (string.IsNullOrEmpty(prompt)) { ShowError("Enter a prompt."); return; }

            string complexReason = CheckComplexity(prompt);
            if (complexReason != null)
            {
                AiPrompt = "";
                ChatHistory.Add(new ChatMessage { Role = "user", Content = prompt });
                string msg = $"This request involves: {complexReason}\n\n{CDT_MESSAGE}";
                ChatHistory.Add(new ChatMessage { Role = "assistant", Content = msg });
                ShowError(msg);
                SetStatus("Complex request — contact CDT.", "#F9E2AF");
                return;
            }

            AiPrompt = "";
            IsAiGenerating = true; ButtonsEnabled = false; AiStatus = "Generating..."; SetStatus("AI working...", "#CBA6F7");
            ChatHistory.Add(new ChatMessage { Role = "user", Content = prompt });
            try
            {
                var resp = await _llmService.SendMessageAsync(SelectedProvider, prompt); // reminder appended internally
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
            finally { IsAiGenerating = false; ButtonsEnabled = true; }
        }
        private async Task ExecuteAiSendError()
        {
            if (SelectedProvider == null) { ShowError("No LLM selected."); return; }
            if (SelectedProvider.Type == LlmType.ClaudeApi && string.IsNullOrWhiteSpace(SelectedProvider.ApiKey)) { ShowError("Enter Claude API key."); return; }
            if (SelectedProvider.Type == LlmType.ClaudeApi && !CheckDailyLimit()) return;
            if (string.IsNullOrEmpty(_lastError)) { ShowError("No error. Test first (F5)."); return; }
            IsAiGenerating = true; ButtonsEnabled = false; AiStatus = "Fixing..."; SetStatus("AI fixing...", "#CBA6F7");
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
            finally { IsAiGenerating = false; ButtonsEnabled = true; }
        }

        // ── Manual Assistant ──
        private void ExecuteGenerateManualPrompt()
        {
            string prompt = ManualPrompt?.Trim();
            if (string.IsNullOrEmpty(prompt)) { ShowError("Enter a prompt first."); return; }

            string complexReason = CheckComplexity(prompt);
            if (complexReason != null)
            {
                GeneratedPrompt = $"BLOCKED: {complexReason}\n\n{CDT_MESSAGE}";
                SetStatus("Complex request — contact CDT.", "#F9E2AF");
                return;
            }

            GeneratedPrompt = LocalLlmService.SYSTEM_PROMPT + "\n\n---\n\nUser Request:\n" + prompt;
            ManualPrompt = "";
            SetStatus("Prompt generated — copy and paste into any AI.", "#A6E3A1");
        }

        private void ExecuteCopyManualPrompt()
        {
            if (string.IsNullOrWhiteSpace(GeneratedPrompt)) { ShowError("Generate a prompt first."); return; }
            Clipboard.SetText(GeneratedPrompt);
            SetStatus("Prompt copied to clipboard!", "#A6E3A1");
        }

        private void ExecutePasteCodeFromClipboard()
        {
            string text = Clipboard.GetText()?.Trim();
            if (string.IsNullOrEmpty(text)) { ShowError("Clipboard is empty."); return; }

            // Strip markdown fences if present
            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline > 0) text = text.Substring(firstNewline + 1);
                if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3).TrimEnd();
            }

            Code = text;
            SelectedTabIndex = 0;
            SetStatus("Code pasted from clipboard — test with F5.", "#A6E3A1");
        }

        // ── Turbo Mode ──
        private void ExecuteToggleTurbo()
        {
            if (!_isTurboMode)
            {
                var dlg = new Window
                {
                    Title = "Turbo Mode", Width = 340, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = _ownerWindow, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White
                };
                var sp = new StackPanel { Margin = new Thickness(16) };
                sp.Children.Add(new TextBlock { Text = "Enter Turbo Mode password:", Margin = new Thickness(0, 0, 0, 8), FontSize = 13 });
                var pwdBox = new PasswordBox { FontSize = 13, Padding = new Thickness(6, 4, 6, 4) };
                sp.Children.Add(pwdBox);
                var btn = new Button { Content = "Activate", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(20, 6, 20, 6), HorizontalAlignment = HorizontalAlignment.Right };
                btn.Click += (s, e) => { dlg.Tag = pwdBox.Password; dlg.DialogResult = true; };
                sp.Children.Add(btn);
                dlg.Content = sp;
                pwdBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { dlg.Tag = pwdBox.Password; dlg.DialogResult = true; } };

                if (dlg.ShowDialog() != true) return;
                if ((string)dlg.Tag != TURBO_PASSWORD) { ShowError("Incorrect password."); return; }
                IsTurboMode = true;
                AiStatus = "TURBO MODE activated — $5 daily limit";
                SetStatus("Turbo Mode ON", "#F9E2AF");
            }
            else
            {
                IsTurboMode = false;
                AiStatus = "Turbo Mode deactivated — $1 daily limit";
                SetStatus("Turbo Mode OFF", "#6C7086");
            }
            _settings.IsTurboMode = _isTurboMode;
            _settings.Save(DevKitApp.ScriptsFolderPath);
        }

        private bool CheckDailyLimit()
        {
            if (_totalCost >= DailyLimit)
            {
                ShowError($"Daily limit reached (${DailyLimit:F2}). " + (_isTurboMode ? "Limit maxed out." : "Enable Turbo Mode to increase to $5."));
                SetStatus("Daily limit reached.", "#F38BA8");
                return false;
            }
            return true;
        }

        // ── Complexity Pre-Check ──
        private static readonly string CDT_MESSAGE =
            "This request involves advanced Revit API patterns that go beyond simple scripting.\n\n" +
            "Please contact the Computational Design Team (CDT) for assistance.\n" +
            "They can build robust, production-ready tools for complex workflows.";

        private static readonly (string pattern, string reason)[] ComplexityPatterns =
        {
            // Geometry intersections between categories
            (@"\b(intersect|intersection|clash|collision|overlap)\b", "geometry intersection / clash detection"),
            (@"\b(solid.*intersect|BooleanOperation|ElementIntersects|ray\s*cast|ReferenceIntersector)\b", "solid geometry operations"),

            // Opening / penetration creation from intersections
            (@"\b(opening|penetration|sleeve|cutout)\b.*\b(wall|floor|slab|ceiling)\b", "opening/penetration creation"),
            (@"\b(wall|floor|slab|ceiling)\b.*\b(opening|penetration|sleeve|cutout)\b", "opening/penetration creation"),

            // Multi-document / linked models
            (@"\b(link|linked\s*model|RevitLinkInstance|RevitLinkType|GetLinkDocument)\b", "linked model operations"),
            (@"\b(multi.?doc|cross.?doc|multiple\s*documents?)\b", "multi-document operations"),

            // External events / modeless patterns
            (@"\b(ExternalEvent|IExternalEventHandler|modeless|DockablePane|IDockablePaneProvider)\b", "modeless / external event patterns"),
            (@"\b(Idling\s*Event|Application\.Idling|RegisterIdlingHandler)\b", "idling event handlers"),

            // IUpdater / DMU
            (@"\b(IUpdater|UpdaterRegistry|DMU|DocumentChanged)\b", "dynamic model update (DMU) patterns"),

            // External APIs / web / database
            (@"\b(http|web\s*request|rest\s*api|soap|websocket|database|sql|mongo|firebase)\b", "external API / database access"),
            (@"\b(HttpClient|WebClient|RestSharp|HttpWebRequest)\b", "web request operations"),

            // Complex file operations
            (@"\b(excel|spreadsheet|csv\s*export|xlsx|ClosedXML|NPOI|EPPlus)\b", "Excel / spreadsheet operations"),
            (@"\b(pdf|iTextSharp|PdfSharp)\b", "PDF generation"),

            // Batch processing across many categories
            (@"\b(batch|all\s*elements?\s*in\s*model|entire\s*model|every\s*(element|instance|family))\b", "model-wide batch processing"),

            // Complex UI beyond TaskDialog
            (@"\b(WPF\s*window|UserControl|dockable|ribbon\s*panel|custom\s*UI|modeless\s*dialog)\b", "custom UI / WPF window creation"),
            (@"\b(DataGrid|TreeView|ListView|TabControl|MVVM)\b", "complex UI components"),

            // Scheduling / automation
            (@"\b(schedule|automat|timer|recurring|background\s*task)\b", "scheduled / automated tasks"),

            // Advanced structural / analytical
            (@"\b(AnalyticalModel|structural\s*analysis|FEA|finite\s*element)\b", "structural analysis operations"),

            // Phasing / worksharing
            (@"\b(workshar|central\s*model|synchroniz|borrow|workset)\b", "worksharing operations"),
            (@"\b(phase\s*map|phase\s*filter|demolish|new\s*construction)\b", "phasing operations"),

            // Complex geometry creation
            (@"\b(loft|sweep|blend|revolution|extrusion.*profile|DirectShape.*complex|TessellatedShape)\b", "complex geometry creation"),
            (@"\b(adaptive\s*component|conceptual\s*mass|divided\s*surface)\b", "adaptive / conceptual mass operations"),

            // MEP-specific complex operations
            (@"\b(MEP.*intersect|pipe.*wall|duct.*wall|conduit.*wall|cable\s*tray.*wall)\b", "MEP intersection operations"),
            (@"\b(routing|auto.?route|connect.*system|mechanical\s*system|piping\s*system)\b", "MEP routing / system operations"),
        };

        private string CheckComplexity(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;
            string lower = prompt.ToLowerInvariant();

            foreach (var (pattern, reason) in ComplexityPatterns)
            {
                if (Regex.IsMatch(lower, pattern, RegexOptions.IgnoreCase))
                    return reason;
            }
            return null;
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
            _settings.DailyCost = _totalCost;
            _settings.DailyCostDate = DateTime.Now.ToString("yyyy-MM-dd");
            _settings.Save(DevKitApp.ScriptsFolderPath);
        }
    }
}
