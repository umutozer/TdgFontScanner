using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Xml;
using TdgFontScanner.Managers;
using TdgFontScanner.Models;
using TdgFontScanner.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using OfficeOpenXml;
using System.IO;
using System;

namespace TdgFontScanner.Controllers
{
    public class FontController : Controller
    {
        private readonly AnalysisStorageService _storageService;

        public FontController()
        {
            _storageService = new AnalysisStorageService();
        }

        [HttpGet]
        public IActionResult Index(int maxPage = 10, int maxDeep = 3)
        {
            ViewBag.MaxPage = maxPage;
            ViewBag.MaxDeep = maxDeep;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Analyze(string domain, int maxPage = 10, int maxDeep = 3)
        {
            // 1 günden eski analizleri temizle
            await _storageService.CleanupOldResultsAsync(1);

            if (string.IsNullOrWhiteSpace(domain))
                return RedirectToAction("Index");

            if (!domain.StartsWith("http"))
                domain = "https://" + domain;

            var manager = new FontAnalysisManager(domain, maxPage, maxDeep);
            var reports = await manager.AnalyzeAllPagesAsync();
            var foundPageCount = reports.Count;

            // Font kullanım özeti
            var fontSummary = reports
                .SelectMany(r => r.FontUsage)
                .GroupBy(f => f.Key)
                .Select(g => new FontSummary
                {
                    FontName = g.Key,
                    TotalCount = g.Sum(x => x.Value),
                    UsedInPages = reports.Count(r => r.FontUsage.ContainsKey(g.Key)),
                    IsSystemFont = IsSystemFont(g.Key)
                })
                .OrderByDescending(f => f.TotalCount)
                .ToList();

            // Sayfa bazlı istatistikler
            var totalFonts = fontSummary.Sum(f => f.TotalCount);
            var totalPages = reports.Count;
            var pagesWithFonts = reports.Count(r => r.FontUsage.Any());
            var systemFonts = fontSummary.Count(f => f.IsSystemFont);
            var customFonts = fontSummary.Count(f => !f.IsSystemFont);

            var pageStats = new
            {
                // Temel Metrikler
                TotalPages = totalPages,
                PagesWithFonts = pagesWithFonts,
                PagesWithoutFonts = totalPages - pagesWithFonts,
                FontCoverage = (pagesWithFonts * 100.0) / totalPages,

                // Font Dağılımı
                TotalFonts = totalFonts,
                SystemFonts = systemFonts,
                CustomFonts = customFonts,
                SystemFontRatio = (systemFonts * 100.0) / (systemFonts + customFonts),

                // Kullanım Analizi
                AverageFontsPerPage = reports.Average(r => r.FontUsage?.Count ?? 0),
                MostUsedFont = fontSummary.OrderByDescending(f => f.TotalCount).FirstOrDefault()?.FontName,
                MostUsedFontCount = fontSummary.Max(f => f.TotalCount),
                MostDiversePage = reports.OrderByDescending(r => r.FontUsage?.Count ?? 0).FirstOrDefault()?.PageUrl,
                MostDiversePageFontCount = reports.Max(r => r.FontUsage?.Count ?? 0)
            };

            // Font kategorileri
            var fontCategories = fontSummary
                .GroupBy(f => f.IsSystemFont ? "Sistem Fontları" : "Özel Fontlar")
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.FontSummary = fontSummary;
            ViewBag.PageStats = pageStats;
            ViewBag.FontCategories = fontCategories;
            ViewBag.FoundPageCount = foundPageCount;
            ViewBag.AnalyzedPageCount = reports.Count;
            ViewBag.Domain = domain;
            ViewBag.MaxPage = maxPage;
            ViewBag.MaxDeep = maxDeep;

            // Analiz sonuçlarını JSON olarak hazırla
            var analysisData = new
            {
                Domain = domain,
                MaxPage = maxPage,
                MaxDeep = maxDeep,
                PageStats = pageStats,
                FontSummary = fontSummary,
                Reports = reports
            };

            // JSON verisini oluştur
            var jsonData = JsonConvert.SerializeObject(analysisData);

            // Analiz sonucunu veritabanına kaydet
            var result = new AnalysisResult
            {
                Domain = domain,
                MaxPage = maxPage,
                MaxDeep = maxDeep,
                AnalysisData = jsonData
            };

            var analysisId = await _storageService.SaveAnalysisResultAsync(result);

            // Kullanıcıya açıklayıcı bilgi mesajı
            if (foundPageCount < maxPage)
            {
                ViewBag.CrawlInfo = $"Sitede toplam {foundPageCount} sayfa bulundu ve analiz edildi. Seçtiğiniz maksimum sayfa sayısı ({maxPage}) ve derinlik ({maxDeep}) değerlerinden bağımsız olarak, sitenin iç link yapısı veya erişim kısıtlamaları nedeniyle daha fazla sayfa bulunamamış olabilir.";
            }
            else
            {
                ViewBag.CrawlInfo = $"Sitede {foundPageCount} sayfa bulundu ve {reports.Count} sayfa analiz edildi. Maksimum sayfa ve derinlik limitiniz uygulanmıştır.";
            }

            // JSON verisini ve analiz ID'sini ViewBag'e ekle
            ViewBag.AnalysisData = jsonData;
            ViewBag.AnalysisId = analysisId;
            
            return PartialView("_ResultPartial", reports);
        }

        private bool IsSystemFont(string fontName)
        {
            // Sistem fontu kontrolü
            var systemFonts = new List<string> { "Arial", "Times New Roman", "Helvetica", "Verdana", "Tahoma", "Calibri", "Segoe UI", "Georgia", "Courier New", "Impact" };
            return systemFonts.Contains(fontName);
        }

        /// <summary>
        /// Font analiz sonuçlarını PDF olarak dışa aktarır
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportPdf([FromForm] string analysisId)
        {
            // 1 günden eski analizleri temizle
            await _storageService.CleanupOldResultsAsync(1);

            var result = await _storageService.GetAnalysisResultAsync(analysisId);
            if (result == null)
            {
                return NotFound("Analiz sonucu bulunamadı.");
            }

            var data = JsonConvert.DeserializeObject<dynamic>(result.AnalysisData);
            var domain = data.Domain.ToString();
            var pageStats = data.PageStats;
            var fontSummary = data.FontSummary.ToObject<List<FontSummary>>();

            // PDF oluşturma işlemi
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // Başlık ekleme
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph($"Font Analiz Raporu - {domain}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20f;
                document.Add(title);

                // İstatistikleri ekleme
                Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                Paragraph stats = new Paragraph();
                stats.Add(new Chunk($"Toplam Sayfa : {pageStats.TotalPages}\n", normalFont));
                stats.Add(new Chunk($"Font Kullanılan Sayfa Sayısı: {pageStats.PagesWithFonts}\n", normalFont));
                stats.Add(new Chunk($"Sistem Fontlar: {pageStats.SystemFonts}\n", normalFont));
                stats.Add(new Chunk($"Özel Fontlar: {pageStats.CustomFonts}\n", normalFont));
                stats.SpacingAfter = 20f;
                document.Add(stats);

                // Font listesini ekleme
                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;
                table.AddCell(new PdfPCell(new Phrase("Font Adı", normalFont)) { BackgroundColor = BaseColor.LightGray });
                table.AddCell(new PdfPCell(new Phrase("Kullanım Sayısı", normalFont)) { BackgroundColor = BaseColor.LightGray });
                table.AddCell(new PdfPCell(new Phrase("Sayfa Sayısı", normalFont)) { BackgroundColor = BaseColor.LightGray });
                table.AddCell(new PdfPCell(new Phrase("Tür", normalFont)) { BackgroundColor = BaseColor.LightGray });

                foreach (var font in fontSummary)
                {
                    table.AddCell(font.FontName);
                    table.AddCell(font.TotalCount.ToString());
                    table.AddCell(font.UsedInPages.ToString());
                    table.AddCell(font.IsSystemFont ? "Sistem Fontu" : "Özel Font");
                }

                document.Add(table);
                document.Close();

                byte[] bytes = ms.ToArray();
                return File(bytes, "application/pdf", $"font_analysis_{domain.Replace("https://", "").Replace("http://", "")}.pdf");
            }
        }

        /// <summary>
        /// Font analiz sonuçlarını Excel olarak dışa aktarır
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportExcel([FromForm] string analysisId)
        {
            // 1 günden eski analizleri temizle
            await _storageService.CleanupOldResultsAsync(1);

            var result = await _storageService.GetAnalysisResultAsync(analysisId);
            if (result == null)
            {
                return NotFound("Analiz sonucu bulunamadı.");
            }

            var data = JsonConvert.DeserializeObject<dynamic>(result.AnalysisData);
            var domain = data.Domain.ToString();
            var pageStats = data.PageStats;
            var fontSummary = data.FontSummary.ToObject<List<FontSummary>>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Font Analizi");

                // Başlık
                worksheet.Cells[1, 1].Value = $"Font Analiz Raporu - {domain}";
                worksheet.Cells[1, 1, 1, 4].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // İstatistikler
                worksheet.Cells[3, 1].Value = "Toplam Sayfa";
                worksheet.Cells[3, 2].Value = Convert.ToInt32(pageStats.TotalPages);
                worksheet.Cells[4, 1].Value = "Font Kullanılan Sayfa Sayısı";
                worksheet.Cells[4, 2].Value = Convert.ToInt32(pageStats.PagesWithFonts);
                worksheet.Cells[5, 1].Value = "Sistem Fontları";
                worksheet.Cells[5, 2].Value = Convert.ToInt32(pageStats.SystemFonts);
                worksheet.Cells[6, 1].Value = "Özel Fontlar";
                worksheet.Cells[6, 2].Value = Convert.ToInt32(pageStats.CustomFonts);
                worksheet.Cells[7, 1].Value = "Ortalama Font Kullanımı";
                worksheet.Cells[7, 2].Value = Math.Round(Convert.ToDouble(pageStats.AverageFontsPerPage), 1);
                worksheet.Cells[8, 1].Value = "En Çok Kullanılan Font";
                worksheet.Cells[8, 2].Value = Convert.ToString(pageStats.MostUsedFont);
                worksheet.Cells[9, 1].Value = "En Çok Kullanım Sayısı";
                worksheet.Cells[9, 2].Value = Convert.ToInt32(pageStats.MostUsedFontCount);

                // İstatistik başlıklarını formatla
                worksheet.Cells[3, 1, 9, 1].Style.Font.Bold = true;
                worksheet.Cells[3, 1, 9, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[3, 1, 9, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                // Font listesi başlıkları
                worksheet.Cells[11, 1].Value = "Font Adı";
                worksheet.Cells[11, 2].Value = "Kullanım Sayısı";
                worksheet.Cells[11, 3].Value = "Sayfa Sayısı";
                worksheet.Cells[11, 4].Value = "Tür";

                // Font listesi başlıklarını formatla
                worksheet.Cells[11, 1, 11, 4].Style.Font.Bold = true;
                worksheet.Cells[11, 1, 11, 4].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[11, 1, 11, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                // Font listesi
                int row = 12;
                foreach (var font in fontSummary)
                {
                    worksheet.Cells[row, 1].Value = font.FontName;
                    worksheet.Cells[row, 2].Value = font.TotalCount;
                    worksheet.Cells[row, 3].Value = font.UsedInPages;
                    worksheet.Cells[row, 4].Value = font.IsSystemFont ? "Sistem Fontu" : "Özel Font";
                    row++;
                }

                // Sütun genişliklerini ayarla
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Excel dosyasını oluştur
                var bytes = package.GetAsByteArray();
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    $"font_analysis_{domain.Replace("https://", "").Replace("http://", "")}.xlsx");
            }
        }
    }
}
