using System;
using Newtonsoft.Json;

namespace ImportSeries.Models
{
    public enum TransactionType
    {
        UpdateBookshelf,
        UpdateFinishedDate
    }

    public class PendingTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TransactionType Type { get; set; }
        public string Payload { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; } = 0;
    }

    public class UpdateBookshelfPayload
    {
        public string Action { get; set; }
        public string BookshelfId { get; set; }
        public string WorkId { get; set; }
    }

    public class UpdateFinishedDatePayload
    {
        public string WorkId { get; set; }
        public DateTimeOffset StatusDate { get; set; }
        public string EventId { get; set; }
    }
}