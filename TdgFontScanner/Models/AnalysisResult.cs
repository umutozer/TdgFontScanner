using System;

namespace TdgFontScanner.Models
{
    /// <summary>
    /// Font analiz sonuçlarını saklamak için kullanılan model sınıfı
    /// </summary>
    public class AnalysisResult
    {
        /// <summary>
        /// Analiz sonucunun benzersiz kimliği
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Analiz yapılan domain
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Maksimum sayfa sayısı
        /// </summary>
        public int MaxPage { get; set; }

        /// <summary>
        /// Maksimum derinlik
        /// </summary>
        public int MaxDeep { get; set; }

        /// <summary>
        /// Analiz sonuçlarının JSON formatında saklanması
        /// </summary>
        public string AnalysisData { get; set; }

        /// <summary>
        /// Analiz tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 