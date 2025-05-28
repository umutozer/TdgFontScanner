namespace TdgFontScanner.Models
{
    /// <summary>
    /// Font kullanım özeti için model sınıfı
    /// </summary>
    public class FontSummary
    {
        /// <summary>
        /// Font adı
        /// </summary>
        public string FontName { get; set; } = "";

        /// <summary>
        /// Toplam kullanım sayısı
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Fontun kullanıldığı toplam sayfa sayısı
        /// </summary>
        public int UsedInPages { get; set; }

        /// <summary>
        /// Fontun kullanım yüzdesi
        /// </summary>
        public double UsagePercentage { get; set; }

        /// <summary>
        /// Fontun sistem fontu olup olmadığı
        /// </summary>
        public bool IsSystemFont { get; set; }

        /// <summary>
        /// Fontun kullanıldığı sayfaların listesi
        /// </summary>
        public List<string> UsedInUrls { get; set; } = new List<string>();

        /// <summary>
        /// Fontun ortalama kullanım sayısı (sayfa başına)
        /// </summary>
        public double AverageUsagePerPage => UsedInPages > 0 ? (double)TotalCount / UsedInPages : 0;

        /// <summary>
        /// Font dosyasının tahmini boyutu (MB)
        /// </summary>
        public double EstimatedSize { get; set; }

        /// <summary>
        /// Font dosyasının formatı (woff2, ttf, otf vb.)
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Font dosyasının karakter seti
        /// </summary>
        public string? CharacterSet { get; set; }

        /// <summary>
        /// Font dosyasının stil sayısı (regular, bold, italic vb.)
        /// </summary>
        public int StyleCount { get; set; }

        /// <summary>
        /// Font dosyasının toplam boyutu (tüm stiller dahil) (MB)
        /// </summary>
        public double TotalSize => EstimatedSize * StyleCount;
    }
}
