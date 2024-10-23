using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace VizInvoiceGeneratorWebAPI.Models
{
    public class InvoiceDto
    {
        public class InvoiceItem
        {
            public string Description { get; set; }
            public string ProductCode { get; set; }
            public string Quantity { get; set; }
            public string Unit { get; set; }
            public string? UnitPrice { get; set; }
            public string? Amount { get; set; }
        }

        public class InvoiceResult
        {
            public string VendorName { get; set; }
            public float? VendorNameConfidence { get; set; }
            public string CustomerName { get; set; }
            public string? CustomerAddress { get; set; }
            public float? CustomerAddressConfidence { get; set; }
            public float? CustomerNameConfidence { get; set; }
            public List<InvoiceItem> Items { get; set; } = new();
            public double? SubTotal { get; set; }
            public float? SubTotalConfidence { get; set; }
            public double? TotalTax { get; set; }
            public float? TotalTaxConfidence { get; set; }
            public double InvoiceTotal { get; set; }
            public float? InvoiceTotalConfidence { get; set; }
            //public AnalyzeResult RawResult { get; set; }
        }

        public class CustomInvoiceResult
        {
            public string? VendorName { get; set; }
            public string? VendorTaxId { get; set; }
            public string? VendorAddress { get; set; }
            public string? InvoiceId { get; set; }
            public string? InvoiceDate { get; set; }
            public string? PurchaseOrder { get; set; }
            public string? PurchaseOrderDate { get; set; }
            public string? ShippingAddressRecipient { get; set; }
            public string? ShippingAddress { get; set; }
            public string? CustomerAddressRecipient { get; set; }
            public string? CustomerAddress { get; set; }
            public string? VendorAddressRecipient { get; set; }
            public string? InvoiceTotal { get; set; }
            public string? InvoiceTotalInWords { get; set; }
            public string? BillingAddress { get; set; }
            public string? BillingAddressRecipient { get; set; }
            public string? CustomerTaxId { get; set; }
            public List<InvoiceItem> Items { get; set; } = new();
        }

    }
}
