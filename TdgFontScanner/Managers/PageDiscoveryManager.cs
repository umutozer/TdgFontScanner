using TdgFontScanner.Services;

namespace TdgFontScanner.Managers
{
    public class PageDiscoveryManager
    {
        private readonly SitemapReaderService _sitemapService = new();
        private readonly WebCrawlerService _crawlerService;
        public string Domain { get; }
        public int MaxPage { get; set; }
        public PageDiscoveryManager(string domain,int maxPage)
        {
            Domain = domain;
            _crawlerService = new WebCrawlerService(domain, maxDepth: 4);
            MaxPage = maxPage;
        }


        public async Task<List<string>> GetAllPagesAsync(string domain)
        {
            var sitemapUrls = await _sitemapService.TryReadAllUrlsFromSitemapAsync(domain);

            if (sitemapUrls != null && sitemapUrls.Count > 0)
            {
                Console.WriteLine($"✅ Sitemap bulundu, {sitemapUrls.Count} sayfa tespit edildi.");
                return sitemapUrls;
            }

            Console.WriteLine("⚠️ Sitemap bulunamadı, crawler devreye giriyor.");
            return await _crawlerService.StartCrawlAsync(MaxPage);
        }
    }

}
