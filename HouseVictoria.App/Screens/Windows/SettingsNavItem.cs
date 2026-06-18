namespace HouseVictoria.App.Screens.Windows
{
    public sealed class SettingsNavItem
    {
        public SettingsNavItem(string id, string title, string subtitle, string iconGlyph)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            IconGlyph = iconGlyph;
        }

        public string Id { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string IconGlyph { get; }
    }
}
