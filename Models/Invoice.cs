using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static VizInvoiceGeneratorWebAPI.Models.InvoiceDto;
namespace VizInvoiceGeneratorWebAPI.Models
{
    public class Invoice
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string? FileName { get; set; }
        public string? FileUrl { get; set; }
        public DateTime UploadDate { get; set; }

        public int State { get; set; }

        // public OCRResult? OCRResult { get; set; }
        public CustomInvoiceResult? InvoiceResult { get; set; }

        public List<AttributeMapping>? AttributeMappings { get; set; }

        public string? CustomGeneratedInvoiceUrl { get; set; }
    }

    public enum InvoiceState
    {
        Processing = 0,
        Processed = 1,
        AttributeMapped = 2,
        Completed = 3
    }

    public class OCRResult
    {
        //public Dictionary<string, string> ExtractedData { get; set; }
        public InvoiceResult InvoiceResult { get; set; }
    }

    public class AttributeMapping
    {
        public string? AttributeName { get; set; }
        public string? MappedValue { get; set; }
    }

    public class InvoiceTemplate
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? Template { get; set; }
    }
}
