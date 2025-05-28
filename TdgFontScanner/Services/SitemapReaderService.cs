using System.Xml.Linq;
using TdgFontScanner.Helper;

namespace TdgFontScanner.Services
{
    public class SitemapReaderService
    {
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _discoveredUrls = new();

        public async Task<List<string>> TryReadAllUrlsFromSitemapAsync(string domainUrl)
        {
            string sitemapUrl = domainUrl.TrimEnd('/') + "/sitemap.xml";

            try
            {
                return await ReadSitemapRecursiveAsync(sitemapUrl);
            }
            catch
            {
                return new List<string>(); // sitemap yoksa boş liste döner
            }
        }

        private async Task<List<string>> ReadSitemapRecursiveAsync(string sitemapUrl)
        {
            var urls = new List<string>();

            if (_discoveredUrls.Contains(sitemapUrl))
                return urls;

            _discoveredUrls.Add(sitemapUrl);

            try
            {
                var response = await _httpClient.GetAsync(sitemapUrl);
                if (!response.IsSuccessStatusCode) return urls;

                var content = await response.Content.ReadAsStringAsync();
                var xdoc = XDocument.Parse(content);

                var locElements = xdoc.Descendants()
                    .Where(e => e.Name.LocalName == "loc")
                    .Select(e => e.Value.Trim())
                    .ToList();

                // Sadece .xml ile bitenler alt sitemap olarak kabul ediliyor
                var subSitemapLocs = locElements
                    .Where(loc => loc.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var htmlUrls = locElements
                    .Where(loc => !loc.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && UrlHelper.IsHtmlPage(loc))
                    .ToList();

                var subSitemapTasks = subSitemapLocs
                    .Select(loc => ReadSitemapRecursiveAsync(loc))
                    .ToList();

                var subResults = await Task.WhenAll(subSitemapTasks);
                foreach (var subList in subResults)
                    urls.AddRange(subList);

                urls.AddRange(htmlUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sitemap hatası: {sitemapUrl} - {ex.Message}");
            }

            return urls.Distinct().ToList();
        }

        private async Task<bool> IsSitemap(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();
                var xdoc = XDocument.Parse(content);
                var root = xdoc.Root?.Name.LocalName;

                return root == "urlset" || root == "sitemapindex";
            }
            catch
            {
                return false;
            }
        }
    }
}
