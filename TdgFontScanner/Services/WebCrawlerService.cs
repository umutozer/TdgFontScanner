using TdgFontScanner.Helper;

namespace TdgFontScanner.Services
{
    public class WebCrawlerService
    {
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _visitedUrls = new();
        private readonly Queue<(string Url, int Depth)> _urlQueue = new();
        private readonly Uri _rootDomain;
        private readonly int _maxDepth;

        public WebCrawlerService(string rootUrl, int maxDepth = 5)
        {
            _rootDomain = new Uri(rootUrl);
            _maxDepth = maxDepth;
            _urlQueue.Enqueue((rootUrl, 0));
            _visitedUrls.Add(rootUrl);
        }

        public async Task<List<string>> StartCrawlAsync(int maxPages = 1000)
        {
            var foundUrls = new List<string>();

            while (_urlQueue.Count > 0 && foundUrls.Count < maxPages)
            {
                var (currentUrl, depth) = _urlQueue.Dequeue();

                // Eğer derinlik sınırını aşıyorsa, işleme alma
                if (depth > _maxDepth)
                    continue;

                // Sadece gerçekten işlenen ve derinlik sınırını aşmayan URL'leri ekle
                foundUrls.Add(currentUrl);
                // Debug: Eklenen URL ve derinlik
                Console.WriteLine($"Eklendi: {currentUrl} (Derinlik: {depth})");

                // Eğer derinlik sınırına ulaşıldıysa, alt linkleri ekleme
                if (depth == _maxDepth)
                    continue;

                try
                {
                    var html = await _httpClient.GetStringAsync(currentUrl);
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);

                    var links = doc.DocumentNode.SelectNodes("//a[@href]")
                                ?.Select(a => a.GetAttributeValue("href", ""))
                                ?.Where(href => !string.IsNullOrWhiteSpace(href))
                                ?.Distinct()
                                ?.ToList();

                    if (links == null) continue;

                    foreach (var link in links)
                    {
                        string absoluteUrl = NormalizeUrl(link, _rootDomain);
                        if (absoluteUrl == null) continue;
                        if (!_visitedUrls.Contains(absoluteUrl) &&
     IsInternalLink(absoluteUrl) &&
     UrlHelper.IsHtmlPage(absoluteUrl))
                        {
                            _visitedUrls.Add(absoluteUrl);
                            _urlQueue.Enqueue((absoluteUrl, depth + 1));
                        }

                    }
                }
                catch
                {
                    // Hatalı veya erişilemeyen URL
                }
            }

            return foundUrls;
        }

        /// <summary>
        /// Linkleri mutlak (absolute) URL'ye çevirir. Hem tam URL, hem / ile başlayan hem de göreli path'leri destekler.
        /// </summary>
        private string? NormalizeUrl(string href, Uri baseUri)
        {
            try
            {
                // Geçersiz veya dış bağlantıları ele
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith("mailto:") || href.StartsWith("javascript:") || href.StartsWith("#"))
                    return null;

                // Anchor ve query temizliği
                href = href.Split('#')[0].Split('?')[0];

                // Eğer zaten tam bir URL ise
                if (Uri.TryCreate(href, UriKind.Absolute, out var absResult))
                {
                    // Sadece ana domain ile aynıysa kabul et
                    return absResult.Host == baseUri.Host ? absResult.ToString().TrimEnd('/') : null;
                }

                // Eğer / ile başlıyorsa (root-relative)
                if (href.StartsWith("/"))
                {
                    var combined = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + href);
                    return combined.ToString().TrimEnd('/');
                }

                // Göreli path (ör: 'kurumsal' gibi)
                var relative = new Uri(baseUri, href);
                return relative.ToString().TrimEnd('/');
            }
            catch
            {
                return null;
            }
        }

        private bool IsInternalLink(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host == _rootDomain.Host;
            }
            catch
            {
                return false;
            }
        }


    }


}
