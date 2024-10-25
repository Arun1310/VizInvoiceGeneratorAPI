using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static VizInvoiceGeneratorWebAPI.Models.InvoiceDto;
namespace VizInvoiceGeneratorWebAPI.Services;

public class FormRecognizerService
{
    private readonly string endpoint;
    private readonly string apiKey;

    public FormRecognizerService(string endpoint, string apiKey)
    {
        this.endpoint = endpoint;
        this.apiKey = apiKey;
    }

    public async Task<object> AnalyzeInvoiceAsync(Stream invoiceStream)
    {
        try
        {
            var credential = new AzureKeyCredential(apiKey);
            var client = new DocumentAnalysisClient(new Uri(endpoint), credential);

            // Analyze the invoice
            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", invoiceStream);
            var result = operation.Value;

            var invoiceResult = new InvoiceResult();

            if (result.Documents.Count > 0)
            {
                AnalyzedDocument document = result.Documents[0];

                // VendorName
                if (document.Fields.TryGetValue("VendorName", out DocumentField? vendorNameField) && vendorNameField.FieldType == DocumentFieldType.String)
                {
                    invoiceResult.VendorName = vendorNameField.Value.AsString();
                    invoiceResult.VendorNameConfidence = vendorNameField.Confidence;
                }

                // CustomerName
                if (document.Fields.TryGetValue("CustomerName", out DocumentField? customerNameField) && customerNameField.FieldType == DocumentFieldType.String)
                {
                    invoiceResult.CustomerName = customerNameField.Value.AsString();
                    invoiceResult.CustomerNameConfidence = customerNameField.Confidence;
                }

                //// CustomerAddress
                //if (document.Fields.TryGetValue("AmountDue", out DocumentField? customerAddressField) && customerAddressField.FieldType == DocumentFieldType.String)
                //{
                //    invoiceResult.CustomerAddress = customerAddressField.Value.AsString();
                //    invoiceResult.CustomerAddressConfidence = customerAddressField.Confidence;
                //}

                // Items
                //if (document.Fields.TryGetValue("Items", out DocumentField? itemsField) && itemsField.FieldType == DocumentFieldType.List)
                //{
                //    foreach (DocumentField itemField in itemsField.Value.AsList())
                //    {
                //        if (itemField.FieldType == DocumentFieldType.Dictionary)
                //        {
                //            var invoiceItem = new InvoiceItem();
                //            IReadOnlyDictionary<string, DocumentField> itemFields = itemField.Value.AsDictionary();

                //            if (itemFields.TryGetValue("Description", out DocumentField? itemDescriptionField) && itemDescriptionField.FieldType == DocumentFieldType.String)
                //            {
                //                invoiceItem.Description = itemDescriptionField.Value.AsString();
                //            }

                //            if (itemFields.TryGetValue("ProductCode", out DocumentField? itemProductCodeField) && itemProductCodeField.FieldType == DocumentFieldType.String)
                //            {
                //                invoiceItem.ProductCode = itemProductCodeField.Value.AsString();
                //            }

                //            if (itemFields.TryGetValue("Quantity", out DocumentField? itemQuantityField) && itemQuantityField.FieldType == DocumentFieldType.Double)
                //            {
                //                invoiceItem.Quantity = itemQuantityField.Value.AsDouble();
                //            }

                //            if (itemFields.TryGetValue("Unit", out DocumentField? itemUnitField) && itemUnitField.FieldType == DocumentFieldType.String)
                //            {
                //                invoiceItem.Unit = itemUnitField.Value.AsString();
                //            }

                //            if (itemFields.TryGetValue("UnitPrice", out DocumentField? itemUnitPriceField) && itemUnitPriceField.FieldType == DocumentFieldType.Currency)
                //            {
                //                var itemUnitPrice = itemUnitPriceField.Value.AsCurrency();
                //                invoiceItem.UnitPrice = itemUnitPrice.Amount;
                //            }

                //            if (itemFields.TryGetValue("Amount", out DocumentField? itemAmountField) && itemAmountField.FieldType == DocumentFieldType.Currency)
                //            {
                //                var itemAmount = itemAmountField.Value.AsCurrency();
                //                invoiceItem.Amount = itemAmount.Amount;
                //                invoiceItem.Confidence = itemAmountField.Confidence;
                //            }

                //            invoiceResult.Items.Add(invoiceItem);
                //        }
                //    }
                //}

                // SubTotal
                if (document.Fields.TryGetValue("SubTotal", out DocumentField? subTotalField) && subTotalField.FieldType == DocumentFieldType.Currency)
                {
                    var subTotal = subTotalField.Value.AsCurrency();
                    invoiceResult.SubTotal = subTotal.Amount;
                    invoiceResult.SubTotalConfidence = subTotalField.Confidence;
                }

                // TotalTax
                if (document.Fields.TryGetValue("TotalTax", out DocumentField? totalTaxField) && totalTaxField.FieldType == DocumentFieldType.Currency)
                {
                    var totalTax = totalTaxField.Value.AsCurrency();
                    invoiceResult.TotalTax = totalTax.Amount;
                    invoiceResult.TotalTaxConfidence = totalTaxField.Confidence;
                }

                // InvoiceTotal
                if (document.Fields.TryGetValue("InvoiceTotal", out DocumentField? invoiceTotalField) && invoiceTotalField.FieldType == DocumentFieldType.Currency)
                {
                    var invoiceTotal = invoiceTotalField.Value.AsCurrency();
                    invoiceResult.InvoiceTotal = invoiceTotal.Amount;
                    invoiceResult.InvoiceTotalConfidence = invoiceTotalField.Confidence;
                }
            }

            return result;
        }
        catch (Exception ex) 
        {
            throw;
        }
      
    }
}
