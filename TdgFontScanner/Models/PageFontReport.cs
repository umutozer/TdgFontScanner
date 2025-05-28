namespace TdgFontScanner.Models
{
    public class PageFontReport
    {
        public string PageUrl { get; set; }
        public Dictionary<string, int> FontUsage { get; set; } = new();
        public List<EmbeddedFont> EmbeddedFonts { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public PageFontReport(string url)
        {
            PageUrl = url;
        }
    }
}
