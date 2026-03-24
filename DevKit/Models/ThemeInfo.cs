using System.Windows.Media;

namespace DevKit.Models
{
    public class ThemeInfo
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public Color BgColor { get; set; }
        public Color FgColor { get; set; }
        public Color AccentColor { get; set; }
        public Color CardColor { get; set; }
        public Color BorderColor { get; set; }

        public Brush BgBrush => new SolidColorBrush(BgColor);
        public Brush FgBrush => new SolidColorBrush(FgColor);
        public Brush AccentBrush => new SolidColorBrush(AccentColor);
        public Brush CardBrush => new SolidColorBrush(CardColor);
        public Brush BorderBrush => new SolidColorBrush(BorderColor);
    }
}
