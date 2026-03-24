using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevKit.ViewModels;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace DevKit.Views
{
    public partial class EditorWindow : Window
    {
        private EditorViewModel _vm;
        private bool _suppressSync;

        public EditorWindow()
        {
            InitializeComponent();
            _vm = new EditorViewModel();
            DataContext = _vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.SetOwnerWindow(this);

            // Configure editor options
            txtCode.Options.EnableHyperlinks = false;
            txtCode.Options.EnableEmailHyperlinks = false;
            txtCode.Options.ConvertTabsToSpaces = true;
            txtCode.Options.IndentationSize = 4;
            txtCode.Options.ShowBoxForControlCharacters = false;
            txtCode.Options.HighlightCurrentLine = true;
            txtCode.Options.AllowScrollBelowDocument = false;

            // Set initial text from ViewModel
            _suppressSync = true;
            txtCode.Text = _vm.Code ?? "";
            _suppressSync = false;
            txtCode.TextArea.Caret.Offset = txtCode.Document.TextLength;

            // Sync editor -> ViewModel
            txtCode.TextChanged += (s, ev) =>
            {
                if (!_suppressSync)
                {
                    _suppressSync = true;
                    _vm.Code = txtCode.Text;
                    _suppressSync = false;
                }
            };

            // Sync ViewModel -> editor
            _vm.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(EditorViewModel.Code) && !_suppressSync)
                {
                    _suppressSync = true;
                    if (txtCode.Text != _vm.Code)
                    {
                        txtCode.Text = _vm.Code ?? "";
                        txtCode.TextArea.Caret.Offset = txtCode.Document.TextLength;
                    }
                    _suppressSync = false;
                }
            };

            // Apply theme colors to editor on load and on theme change
            ApplyEditorTheme();
            _vm.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(EditorViewModel.SelectedTheme))
                    Dispatcher.BeginInvoke(new Action(ApplyEditorTheme));
            };

            txtCode.Focus();

            pwdApiKey.Password = _vm.ClaudeApiKey ?? "";
            if (_vm.ChatHistory is INotifyCollectionChanged col)
                col.CollectionChanged += (s2, ev2) => { if (lstChat.Items.Count > 0) lstChat.ScrollIntoView(lstChat.Items[lstChat.Items.Count - 1]); };
        }

        private void ApplyEditorTheme()
        {
            var bg = Brush("BgSurface");
            var fg = Brush("TextPrimary");
            var caret = Brush("Caret");
            var lineGutter = Brush("LineGutter");
            var lineFg = Brush("TextDim");
            var elevated = Brush("BgElevated");
            var accent = BrushColor("Accent");
            var selection = Brush("BgCard");

            // Editor surface — must set on both TextEditor and TextArea
            txtCode.Background = bg ?? Brushes.White;
            txtCode.Foreground = fg ?? Brushes.Black;
            txtCode.LineNumbersForeground = lineFg ?? Brushes.Gray;

            var area = txtCode.TextArea;
            area.Background = Brushes.Transparent;
            area.SetValue(System.Windows.Controls.Panel.BackgroundProperty, Brushes.Transparent);

            // Caret
            if (caret is SolidColorBrush cb)
                area.Caret.CaretBrush = cb;

            // Selection
            if (selection != null)
                area.SelectionBrush = selection;
            area.SelectionForeground = null;

            // Current line highlight
            if (elevated is SolidColorBrush hl)
            {
                var c = hl.Color;
                area.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B));
                area.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B)), 1);
            }

            // Line number gutter background
            if (lineGutter != null)
            {
                foreach (var margin in area.LeftMargins)
                    if (margin is FrameworkElement fe)
                        fe.SetValue(System.Windows.Controls.Control.BackgroundProperty, lineGutter);
            }

            // Build theme-matched syntax highlighting
            txtCode.SyntaxHighlighting = BuildCSharpHighlighting();
        }

        private IHighlightingDefinition BuildCSharpHighlighting()
        {
            var keyword  = HexFromResource("Accent");
            var typeName = HexFromResource("AccentTeal");
            var str      = HexFromResource("AccentGreen");
            var comment  = HexFromResource("TextMuted");
            var number   = HexFromResource("AccentYellow");
            var preproc  = HexFromResource("AccentPurple");
            var red      = HexFromResource("AccentRed");

            var xshd = "<?xml version=\"1.0\"?>\n" +
"<SyntaxDefinition name=\"CSharp\" extensions=\".cs\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">\n" +
"  <Color name=\"Comment\"     foreground=\"" + comment  + "\" fontStyle=\"italic\" />\n" +
"  <Color name=\"String\"      foreground=\"" + str      + "\" />\n" +
"  <Color name=\"Char\"        foreground=\"" + str      + "\" />\n" +
"  <Color name=\"Number\"      foreground=\"" + number   + "\" />\n" +
"  <Color name=\"Keyword\"     foreground=\"" + keyword  + "\" fontWeight=\"bold\" />\n" +
"  <Color name=\"TypeKeyword\" foreground=\"" + typeName + "\" />\n" +
"  <Color name=\"Preprocessor\" foreground=\"" + preproc + "\" />\n" +
"  <Color name=\"ValueKeyword\" foreground=\"" + red     + "\" />\n" +
"  <RuleSet ignoreCase=\"false\">\n" +
"    <Span color=\"Comment\" begin=\"//\" />\n" +
"    <Span color=\"Comment\" begin=\"/\\*\" end=\"\\*/\" multiline=\"true\" />\n" +
"    <Span color=\"String\" begin=\"&quot;\" end=\"&quot;\" />\n" +
"    <Span color=\"String\" begin=\"@&quot;\" end=\"&quot;\" />\n" +
"    <Span color=\"Char\" begin=\"'\" end=\"'\" />\n" +
"    <Span color=\"Preprocessor\" begin=\"^\\s*\\#\" />\n" +
"    <Rule color=\"Number\">\\b0[xX][0-9a-fA-F]+|\\b[0-9]+(\\.[0-9]+)?([eE][+-]?[0-9]+)?[fFdDmMuUlL]*\\b</Rule>\n" +
"    <Keywords color=\"Keyword\">\n" +
"      <Word>abstract</Word><Word>as</Word><Word>base</Word><Word>bool</Word><Word>break</Word>\n" +
"      <Word>byte</Word><Word>case</Word><Word>catch</Word><Word>char</Word><Word>checked</Word>\n" +
"      <Word>class</Word><Word>const</Word><Word>continue</Word><Word>decimal</Word><Word>default</Word>\n" +
"      <Word>delegate</Word><Word>do</Word><Word>double</Word><Word>else</Word><Word>enum</Word>\n" +
"      <Word>event</Word><Word>explicit</Word><Word>extern</Word><Word>finally</Word><Word>fixed</Word>\n" +
"      <Word>float</Word><Word>for</Word><Word>foreach</Word><Word>goto</Word><Word>if</Word>\n" +
"      <Word>implicit</Word><Word>in</Word><Word>int</Word><Word>interface</Word><Word>internal</Word>\n" +
"      <Word>is</Word><Word>lock</Word><Word>long</Word><Word>namespace</Word><Word>new</Word>\n" +
"      <Word>object</Word><Word>operator</Word><Word>out</Word><Word>override</Word><Word>params</Word>\n" +
"      <Word>private</Word><Word>protected</Word><Word>public</Word><Word>readonly</Word><Word>ref</Word>\n" +
"      <Word>return</Word><Word>sbyte</Word><Word>sealed</Word><Word>short</Word><Word>sizeof</Word>\n" +
"      <Word>stackalloc</Word><Word>static</Word><Word>string</Word><Word>struct</Word><Word>switch</Word>\n" +
"      <Word>throw</Word><Word>try</Word><Word>typeof</Word><Word>uint</Word><Word>ulong</Word>\n" +
"      <Word>unchecked</Word><Word>unsafe</Word><Word>ushort</Word><Word>using</Word><Word>var</Word>\n" +
"      <Word>virtual</Word><Word>void</Word><Word>volatile</Word><Word>while</Word><Word>yield</Word>\n" +
"      <Word>async</Word><Word>await</Word><Word>nameof</Word><Word>when</Word><Word>where</Word>\n" +
"      <Word>get</Word><Word>set</Word><Word>add</Word><Word>remove</Word><Word>partial</Word>\n" +
"      <Word>global</Word><Word>dynamic</Word><Word>this</Word>\n" +
"    </Keywords>\n" +
"    <Keywords color=\"ValueKeyword\">\n" +
"      <Word>true</Word><Word>false</Word><Word>null</Word><Word>value</Word>\n" +
"    </Keywords>\n" +
"    <Keywords color=\"TypeKeyword\">\n" +
"      <Word>List</Word><Word>Dictionary</Word><Word>IEnumerable</Word><Word>IList</Word>\n" +
"      <Word>Task</Word><Word>Action</Word><Word>Func</Word><Word>Nullable</Word>\n" +
"      <Word>String</Word><Word>Int32</Word><Word>Boolean</Word><Word>Object</Word>\n" +
"      <Word>Console</Word><Word>Math</Word><Word>Convert</Word><Word>Enumerable</Word>\n" +
"      <Word>Exception</Word><Word>Type</Word><Word>Array</Word><Word>DateTime</Word>\n" +
"    </Keywords>\n" +
"  </RuleSet>\n" +
"</SyntaxDefinition>";

            using (var reader = new System.Xml.XmlTextReader(new System.IO.StringReader(xshd)))
            {
                return ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }

        private string HexFromResource(string key)
        {
            try
            {
                var brush = FindResource(key) as SolidColorBrush;
                if (brush != null)
                {
                    var c = brush.Color;
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
            catch { }
            return "#888888";
        }

        private System.Windows.Media.Brush Brush(string key)
        {
            try { return FindResource(key) as System.Windows.Media.Brush; }
            catch { return null; }
        }

        private Color? BrushColor(string key)
        {
            try { return (FindResource(key) as SolidColorBrush)?.Color; }
            catch { return null; }
        }

        private void TxtAiPrompt_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                _vm.AiGenerateCommand.Execute(null);
            }
        }

        private void BtnSaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClaudeApiKey = pwdApiKey.Password;
            _vm.SaveApiKeyCommand.Execute(null);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) { _vm.TestCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { _vm.AddCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) { _vm.ClearCommand.Execute(null); e.Handled = true; }
        }
    }
}
