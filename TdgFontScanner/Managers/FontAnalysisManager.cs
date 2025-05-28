using TdgFontScanner.Models;
using TdgFontScanner.Services;

namespace TdgFontScanner.Managers
{
    public class FontAnalysisManager
    {
        private readonly PageDiscoveryManager _discoveryManager;
        private readonly FontAnalyzerService _fontAnalyzer;
        private readonly int _maxPage;
        private readonly int _maxDeep;

        public FontAnalysisManager(string domain, int maxPage = 10, int maxDeep = 3)
        {
            _discoveryManager = new PageDiscoveryManager(domain,maxPage);
            _fontAnalyzer = new FontAnalyzerService();
            _maxPage = maxPage;
            _maxDeep = maxDeep;
        }

        /// <summary>
        /// Sitemap varsa sitemap'tan, yoksa crawler'dan sayfa bulup analiz eder
        /// </summary>
        public async Task<List<PageFontReport>> AnalyzeAllPagesAsync()
        {
            // Önce sitemap ile sayfa bulmayı dene
            var discoveryPages = await _discoveryManager.GetAllPagesAsync(_discoveryManager.Domain);

            List<string> allPages;
            if (discoveryPages != null && discoveryPages.Count > 0)
            {
                Console.WriteLine($"✅ Sitemap bulundu, {discoveryPages.Count} sayfa tespit edildi.");
                allPages = discoveryPages.Take(_maxPage).ToList(); // maxPage limiti uygula
            }
            else
            {
                // Sitemap yoksa crawler ile bul
                var crawler = new WebCrawlerService(_discoveryManager.Domain, _maxDeep);
                allPages = await crawler.StartCrawlAsync(_maxPage);
                Console.WriteLine($"⚠️ Sitemap yok, crawler ile {allPages.Count} sayfa bulundu.");
            }

            Console.WriteLine($"🔎 {allPages.Count} sayfa bulundu. Analiz başlıyor...\n");

            var allReports = new List<PageFontReport>();
            foreach (var pageUrl in allPages)
            {
                Console.WriteLine($"⏳ Sayfa analiz ediliyor: {pageUrl}");
                allReports.Add(await _fontAnalyzer.AnalyzeFontsWithDetailsAsync(pageUrl));
            }
            return allReports;
        }

        /// <summary>
        /// Tüm sayfaları analiz eder ve hem bulunan hem analiz edilen sayfa sayısını döndürür
        /// </summary>
        public async Task<(List<PageFontReport> Reports, int FoundPageCount)> AnalyzeAllPagesWithStatsAsync()
        {
            var crawler = new WebCrawlerService(_discoveryManager.Domain, _maxDeep);
            var allPages = await crawler.StartCrawlAsync(_maxPage);

            Console.WriteLine($"🔎 {allPages.Count} sayfa bulundu. Analiz başlıyor...\n");

            var allReports = new List<PageFontReport>();
            foreach (var pageUrl in allPages)
            {
                Console.WriteLine($"⏳ Sayfa analiz ediliyor: {pageUrl}");
                allReports.Add(await _fontAnalyzer.AnalyzeFontsWithDetailsAsync(pageUrl));
            }
            return (allReports, allPages.Count);
        }
    }

}
