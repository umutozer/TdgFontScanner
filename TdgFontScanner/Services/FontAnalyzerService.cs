using System.Text.RegularExpressions;
using TdgFontScanner.Models;

namespace TdgFontScanner.Services
{
    public class FontAnalyzerService
    {
        private readonly HttpClient _httpClient = new();

        public async Task<PageFontReport> AnalyzeFontsWithDetailsAsync(string pageUrl)
        {
            var pageReport = new PageFontReport(pageUrl);

            try
            {
                var html = await _httpClient.GetStringAsync(pageUrl);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // 1. <style> blokları
                var styleNodes = doc.DocumentNode.SelectNodes("//style");
                if (styleNodes != null)
                {
                    foreach (var style in styleNodes)
                    {
                        var css = style.InnerText;
                        ExtractFontFamilies(css, pageReport);
                        ExtractFontFace(css, pageReport);
                    }
                }

                // 2. <link rel="stylesheet"> blokları
                var linkNodes = doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']");
                if (linkNodes != null)
                {
                    foreach (var link in linkNodes)
                    {
                        var href = link.GetAttributeValue("href", null);
                        if (!string.IsNullOrEmpty(href))
                        {
                            var cssUrl = NormalizeCssUrl(pageUrl, href);
                            var cssContent = await TryDownloadCss(cssUrl);
                            ExtractFontFamilies(cssContent, pageReport);
                            ExtractFontFace(cssContent, pageReport);
                        }
                    }
                }

                // 3. Inline style=""
                var inlineStyleNodes = doc.DocumentNode.SelectNodes("//*[@style]");
                if (inlineStyleNodes != null)
                {
                    foreach (var node in inlineStyleNodes)
                    {
                        var styleAttr = node.GetAttributeValue("style", "");
                        ExtractFontFamilies(styleAttr, pageReport);
                    }
                }
            }
            catch (Exception ex)
            {
                pageReport.Errors.Add(ex.Message);
            }

            return pageReport;
        }

        private void ExtractFontFamilies(string cssContent, PageFontReport report)
        {
            if (string.IsNullOrWhiteSpace(cssContent)) return;

            var regex = new Regex(@"font-family\s*:\s*([^;]+);", RegexOptions.IgnoreCase);
            var matches = regex.Matches(cssContent);

            foreach (Match match in matches)
            {
                var raw = match.Groups[1].Value;
                var fonts = raw.Split(',')
                               .Select(f => f.Trim().Trim('\'', '"'))
                               .Where(f => !string.IsNullOrEmpty(f));

                foreach (var font in fonts)
                {
                    if (!report.FontUsage.ContainsKey(font))
                        report.FontUsage[font] = 0;

                    report.FontUsage[font]++;
                }
            }
        }

        private void ExtractFontFace(string cssContent, PageFontReport report)
        {
            if (string.IsNullOrWhiteSpace(cssContent)) return;

            var regexFontFace = new Regex(@"@font-face\s*{[^}]*}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var fontFaceBlocks = regexFontFace.Matches(cssContent);

            foreach (Match block in fontFaceBlocks)
            {
                var content = block.Value;

                var nameMatch = Regex.Match(content, @"font-family\s*:\s*['""]?([^;'""]+)['""]?;", RegexOptions.IgnoreCase);
                var urlMatch = Regex.Match(content, @"url\(([^)]+)\)", RegexOptions.IgnoreCase);

                if (nameMatch.Success)
                {
                    var fontName = nameMatch.Groups[1].Value.Trim().Trim('\'', '"');
                    string? fontUrl = urlMatch.Success ? urlMatch.Groups[1].Value.Trim().Trim('\'', '"') : null;

                    report.EmbeddedFonts.Add(new EmbeddedFont
                    {
                        FontName = fontName,
                        FontUrl = fontUrl
                    });
                }
            }
        }

        private string NormalizeCssUrl(string pageUrl, string cssHref)
        {
            try
            {
                var baseUri = new Uri(pageUrl);
                var cssUri = new Uri(baseUri, cssHref);
                return cssUri.ToString();
            }
            catch
            {
                return cssHref;
            }
        }

        private async Task<string> TryDownloadCss(string cssUrl)
        {
            try
            {
                return await _httpClient.GetStringAsync(cssUrl);
            }
            catch
            {
                return string.Empty;
            }
        }
    }


}
