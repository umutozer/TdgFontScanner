namespace TdgFontScanner.Helper
{
    public static class UrlHelper
    {
        private static readonly string[] _excludedExtensions = new[]
        {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".mp4", ".mp3", ".avi", ".mov",
        ".css", ".js", ".json", ".xml", ".woff", ".woff2", ".ttf", ".eot"
    };

        public static bool IsHtmlPage(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath.ToLower();

                return !_excludedExtensions.Any(ext => path.EndsWith(ext));
            }
            catch
            {
                return false;
            }
        }
    }

}
