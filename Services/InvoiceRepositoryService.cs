using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MongoDB.Driver;
using System.Text;
using SelectPdf;
using VizInvoiceGeneratorWebAPI.Models;
using static VizInvoiceGeneratorWebAPI.Models.InvoiceDto;

namespace VizInvoiceGeneratorWebAPI.Services
{
    public class InvoiceRepositoryService
    {
        private readonly IMongoCollection<Invoice> _invoices;
        private readonly IMongoCollection<InvoiceTemplate> _invoice_template;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string endpoint;
        private readonly string apiKey;

        public InvoiceRepositoryService(MongoDbContext context, BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _invoices = context.Invoices;
            _invoice_template = context.InvoiceTemplate;
            _blobServiceClient = blobServiceClient;
            this.endpoint = configuration["Azure:FormRecognizerEndpoint"];
            this.apiKey = configuration["Azure:FormRecognizerApiKey"];
        }

        public async Task<List<Invoice>> GetAllInvoicesAsync()
        {
            try
            {
                return await _invoices.Find(_ => true).ToListAsync();
            }
            catch (Exception ex) 
            {
                throw;
            }

        }

        public async Task<Invoice> GetInvoiceById(string id)
        {
            return await _invoices.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateInvoice(Invoice invoice)
        {
            await _invoices.InsertOneAsync(invoice);
        }

        //public async Task UpdateInvoiceState(string id, string state)
        //{
        //    var filter = Builders<Invoice>.Filter.Eq("_id", id);
        //    var update = Builders<Invoice>.Update.Set("state", state);
        //    await _invoices.UpdateOneAsync(filter, update);
        //}

        public async Task<bool> UpdateInvoice(string id, Invoice updatedInvoice)
        {
            var result = await _invoices.ReplaceOneAsync(i => i.Id == id, updatedInvoice);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<Invoice> UploadInvoiceAsync(IFormFile file)
        {
            try
            {
                var fileDetail = new FileDetail
                {
                    FilePath = file.FileName,
                    FileName = file.FileName,
                    FileType = file.ContentType,
                    Content = ConvertFileToBase64(file)
                };

                var uploadedFile = UploadAttachmentToBlob(fileDetail);

                var invoiceResult = await AnalyzeInvoiceAsync(file);

                var invoice = new Invoice
                {
                    FileName = file.FileName,
                    FileUrl = uploadedFile.FilePath,
                    UploadDate = DateTime.UtcNow,
                    State = 1,
                    InvoiceResult = invoiceResult
                    //OCRResult = new OCRResult
                    //{
                    //    // ExtractedData = invoiceResult,
                    //    InvoiceResult = invoiceResult
                    //},
                };
                await _invoices.InsertOneAsync(invoice);
                return invoice;
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        public async Task<Invoice> UploadCustomInvoiceAsync(string id, IFormFile file)
        {
            try
            {
                var fileDetail = new FileDetail
                {
                    FilePath = "custom/" + file.FileName,
                    FileName = file.FileName,
                    FileType = file.ContentType,
                    Content = ConvertFileToBase64(file)
                };

                var uploadedFile = UploadAttachmentToBlob(fileDetail);

                var invoice = await GetInvoiceById(id);

                invoice.State = 3;
                invoice.CustomGeneratedInvoiceUrl = uploadedFile.FilePath;

                var result = await UpdateInvoice(id, invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        public async Task<InvoiceResult> AnalyzePreBuiltInvoiceAsync(IFormFile file)
        {
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var credential = new AzureKeyCredential(apiKey);
                    var client = new DocumentAnalysisClient(new Uri(endpoint), credential);

                    // Analyze the invoice
                    AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", stream);
                    var result = operation.Value;

                    var invoiceResult = new InvoiceResult();


                    if (result.Documents.Count > 0)
                    {
                        //invoiceResult.RawResult = result;
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

                        // Items
                        if (document.Fields.TryGetValue("Items", out DocumentField? itemsField) && itemsField.FieldType == DocumentFieldType.List)
                        {
                            foreach (DocumentField itemField in itemsField.Value.AsList())
                            {
                                if (itemField.FieldType == DocumentFieldType.Dictionary)
                                {
                                    var invoiceItem = new InvoiceItem();
                                    IReadOnlyDictionary<string, DocumentField> itemFields = itemField.Value.AsDictionary();

                                    if (itemFields.TryGetValue("Description", out DocumentField? itemDescriptionField) && itemDescriptionField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.Description = itemDescriptionField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("ProductCode", out DocumentField? itemProductCodeField) && itemProductCodeField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.ProductCode = itemProductCodeField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("Quantity", out DocumentField? itemQuantityField) && itemQuantityField.FieldType == DocumentFieldType.Double)
                                    {
                                        // invoiceItem.Quantity = itemQuantityField.Value.AsDouble();
                                    }

                                    if (itemFields.TryGetValue("Unit", out DocumentField? itemUnitField) && itemUnitField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.Unit = itemUnitField.Value.AsString();
                                    }

                                    //if (itemFields.TryGetValue("UnitPrice", out DocumentField? itemUnitPriceField) && itemUnitPriceField.FieldType == DocumentFieldType.String)
                                    //{
                                    //    var itemUnitPrice = itemUnitPriceField.Value.AsCurrency();
                                    //    invoiceItem.UnitPrice = itemUnitPrice.Amount;
                                    //}

                                    //if (itemFields.TryGetValue("Amount", out DocumentField? itemAmountField) && itemAmountField.FieldType == DocumentFieldType.String)
                                    //{
                                    //    var itemAmount = itemAmountField.Value.AsCurrency();
                                    //    invoiceItem.Amount = itemAmount.Amount;
                                    //    invoiceItem.Confidence = itemAmountField.Confidence;
                                    //}

                                    invoiceResult.Items.Add(invoiceItem);
                                }
                            }
                        }

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

                    return invoiceResult;
                }

            }
            catch (Exception ex)
            {
                throw;
            }

        }

        public async Task<CustomInvoiceResult> AnalyzeInvoiceAsync(IFormFile file)
        {
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var credential = new AzureKeyCredential(apiKey);
                    var client = new DocumentAnalysisClient(new Uri(endpoint), credential);

                    // Analyze the invoice
                    AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "custom-invoice", stream);
                    var result = operation.Value;

                    var invoiceResult = new CustomInvoiceResult();

                    if (result.Documents.Count > 0)
                    {
                        AnalyzedDocument document = result.Documents[0];

                        // VendorName
                        if (document.Fields.TryGetValue("VendorName", out DocumentField? vendorNameField) && vendorNameField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.VendorName = vendorNameField.Value.AsString();
                        }

                        // VendorTaxId
                        if (document.Fields.TryGetValue("VendorTaxId", out DocumentField? vendorTaxIdField) && vendorTaxIdField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.VendorTaxId = vendorTaxIdField.Value.AsString();
                        }

                        // VendorAddress
                        if (document.Fields.TryGetValue("VendorAddress", out DocumentField? vendorAddressField) && vendorAddressField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.VendorAddress = vendorAddressField.Value.AsString();
                        }

                        // InvoiceId
                        if (document.Fields.TryGetValue("InvoiceId", out DocumentField? invoiceIdField) && invoiceIdField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.InvoiceId = invoiceIdField.Value.AsString();
                        }

                        // InvoiceDate
                        if (document.Fields.TryGetValue("InvoiceDate", out DocumentField? invoiceDateField) && invoiceDateField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.InvoiceDate = invoiceDateField.Value.AsString();
                        }

                        // PurchaseOrder
                        if (document.Fields.TryGetValue("PurchaseOrder", out DocumentField? purchaseOrderField) && purchaseOrderField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.PurchaseOrder = purchaseOrderField.Value.AsString();
                        }

                        // PurchaseOrderDate
                        if (document.Fields.TryGetValue("PurchaseOrderDate", out DocumentField? purchaseOrderDateField) && purchaseOrderDateField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.PurchaseOrderDate = purchaseOrderDateField.Value.AsString();
                        }

                        // ShippingAddressRecipient
                        if (document.Fields.TryGetValue("ShippingAddressRecipient", out DocumentField? shippingAddressRecipientField) && shippingAddressRecipientField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.ShippingAddressRecipient = shippingAddressRecipientField.Value.AsString();
                        }

                        // ShippingAddress
                        if (document.Fields.TryGetValue("ShippingAddress", out DocumentField? shippingAddressField) && shippingAddressField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.ShippingAddress = shippingAddressField.Value.AsString();
                        }

                        // CustomerAddressRecipient
                        if (document.Fields.TryGetValue("CustomerAddressRecipient", out DocumentField? customerAddressRecipientField) && customerAddressRecipientField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.CustomerAddressRecipient = customerAddressRecipientField.Value.AsString();
                        }

                        // CustomerAddress
                        if (document.Fields.TryGetValue("CustomerAddress", out DocumentField? customerAddressField) && customerAddressField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.CustomerAddress = customerAddressField.Value.AsString();
                        }

                        // VendorAddressRecipient
                        if (document.Fields.TryGetValue("VendorAddressRecipient", out DocumentField? vendorAddressRecipientField) && vendorAddressRecipientField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.VendorAddressRecipient = vendorAddressRecipientField.Value.AsString();
                        }

                        // InvoiceTotal
                        if (document.Fields.TryGetValue("InvoiceTotal", out DocumentField? invoiceTotalField) && invoiceTotalField.FieldType == DocumentFieldType.String)
                        {
                            var invoiceTotal = invoiceTotalField.Value.AsString();
                            invoiceResult.InvoiceTotal = invoiceTotal;
                        }

                        // InvoiceTotalInWords
                        if (document.Fields.TryGetValue("InvoiceTotalInWords", out DocumentField? invoiceTotalInWordsField) && invoiceTotalInWordsField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.InvoiceTotalInWords = invoiceTotalInWordsField.Value.AsString();
                        }

                        // BillingAddress
                        if (document.Fields.TryGetValue("BillingAddress", out DocumentField? billingAddressField) && billingAddressField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.BillingAddress = billingAddressField.Value.AsString();
                        }

                        // BillingAddressRecipient
                        if (document.Fields.TryGetValue("BillingAddressRecipient", out DocumentField? billingAddressRecipientField) && billingAddressRecipientField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.BillingAddressRecipient = billingAddressRecipientField.Value.AsString();
                        }

                        // CustomerTaxId
                        if (document.Fields.TryGetValue("CustomerTaxId", out DocumentField? customerTaxIdField) && customerTaxIdField.FieldType == DocumentFieldType.String)
                        {
                            invoiceResult.CustomerTaxId = customerTaxIdField.Value.AsString();
                        }

                        // Items
                        if (document.Fields.TryGetValue("Items", out DocumentField? itemsField) && itemsField.FieldType == DocumentFieldType.List)
                        {
                            foreach (DocumentField itemField in itemsField.Value.AsList())
                            {
                                if (itemField.FieldType == DocumentFieldType.Dictionary)
                                {
                                    var invoiceItem = new InvoiceItem();
                                    IReadOnlyDictionary<string, DocumentField> itemFields = itemField.Value.AsDictionary();

                                    if (itemFields.TryGetValue("Description", out DocumentField? itemDescriptionField) && itemDescriptionField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.Description = itemDescriptionField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("ProductCode", out DocumentField? itemProductCodeField) && itemProductCodeField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.ProductCode = itemProductCodeField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("Quantity", out DocumentField? itemQuantityField) && itemQuantityField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.Quantity = itemQuantityField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("Unit", out DocumentField? itemUnitField) && itemUnitField.FieldType == DocumentFieldType.String)
                                    {
                                        invoiceItem.Unit = itemUnitField.Value.AsString();
                                    }

                                    if (itemFields.TryGetValue("UnitPrice", out DocumentField? itemUnitPriceField) && itemUnitPriceField.FieldType == DocumentFieldType.String)
                                    {
                                        var itemUnitPrice = itemUnitPriceField.Value.AsString();
                                        invoiceItem.UnitPrice = itemUnitPrice;
                                    }

                                    if (itemFields.TryGetValue("Amount", out DocumentField? itemAmountField) && itemAmountField.FieldType == DocumentFieldType.String)
                                    {
                                        var itemAmount = itemAmountField.Value.AsString();
                                        invoiceItem.Amount = itemAmount;
                                    }

                                    invoiceResult.Items.Add(invoiceItem);
                                }
                            }
                        }
                    }

                    return invoiceResult;
                }

            }
            catch (Exception ex)
            {
                throw;
            }

        }

        private string ConvertFileToBase64(IFormFile file)
        {
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                var fileBytes = ms.ToArray();
                return Convert.ToBase64String(fileBytes);
            }
        }


        public FileDetail UploadAttachmentToBlob(FileDetail fileDetail)
        {
            try
            {
                // Get blob container client
                var blobContainer = _blobServiceClient.GetBlobContainerClient("custom-invoice-generator");

                // Create container if it doesn't exist
                blobContainer.CreateIfNotExists(PublicAccessType.BlobContainer);

                // Get a blob client using the file path (file name in this case)
                var containerClient = blobContainer.GetBlobClient(fileDetail.FilePath);

                // Set content type for the blob
                var blobHttpHeader = new BlobHttpHeaders
                {
                    ContentType = fileDetail.FileType
                };

                // Convert base64 content to bytes
                var bytes = Convert.FromBase64String(fileDetail.Content);

                // Upload the file to the blob
                containerClient.Upload(new MemoryStream(bytes), blobHttpHeader);

                // Set the absolute URL of the uploaded blob
                fileDetail.FilePath = containerClient.Uri.AbsoluteUri;

                return fileDetail;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while uploading to blob", ex);
            }
        }

        public async Task<MemoryStream> GetInvoiceTemplate(string id)
        {
            // Fetch the invoice by ID
            var invoice = await _invoices.Find(i => i.Id == id).FirstOrDefaultAsync();
            if (invoice == null)
            {
                throw new Exception($"Invoice with ID {id} not found.");
            }

            // Fetch the template (assuming you're using the first one for now)
            var templateList = await _invoice_template.Find(_ => true).ToListAsync();
            if (templateList == null || !templateList.Any())
            {
                throw new Exception("Invoice template not found.");
            }

            // Populate the HTML template with the invoice data
            var customInvoiceHtml = PopulateHtmlTemplate(templateList[0]?.Template, invoice);

            // Convert the populated HTML to PDF and return the PDF stream
            var pdfStream = ConvertHtmlToPdf(customInvoiceHtml);

            return pdfStream;
        }

        private string PopulateHtmlTemplate(string template, Invoice invoice)
        {
            string populatedHtml = template
                .Replace("{{GSTIN}}", invoice.InvoiceResult.VendorTaxId) // Example placeholder for GSTIN
                .Replace("{{VENDORNAME}}", invoice.InvoiceResult.VendorName)
                .Replace("{{VENDORADDRESS}}", invoice.InvoiceResult.VendorAddress)
                .Replace("{{INVOICENO}}", invoice.InvoiceResult.InvoiceId)
                .Replace("{{INVOICEDATE}}", invoice.InvoiceResult.InvoiceDate)
                .Replace("{{PONO}}", invoice.InvoiceResult.PurchaseOrder)
                .Replace("{{DATE}}", invoice.InvoiceResult.PurchaseOrderDate)
                .Replace("{{CUSTOMERNAME}}", invoice.InvoiceResult.CustomerAddressRecipient ?? invoice.InvoiceResult.ShippingAddressRecipient)
                .Replace("{{CUSTOMERADDRESS}}", invoice.InvoiceResult.CustomerAddress ?? invoice.InvoiceResult.ShippingAddress)
                .Replace("{{TOTAL}}", invoice.InvoiceResult.InvoiceTotal)
                .Replace("{{INVOICETOTALINWORDS}}", invoice.InvoiceResult.InvoiceTotalInWords);

            StringBuilder tableRows = new StringBuilder();
            foreach (var detail in invoice.InvoiceResult.Items)
            {
                tableRows.Append($@"
                    <tr style='height:26.4pt;'>
                        <td style='width: 76px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'></td>
                        <td style='width: 96px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'></td>
                        <td colspan='2' style='width: 303px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'>{detail.Description}</td>
                        <td style='width: 50px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{detail.ProductCode:N2}</td>
                        <td style='width: 44px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{detail.Quantity:N2}</td>
                        <td style='width: 39px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{detail.Unit:N2}</td>
                        <td style='width: 74px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{detail.UnitPrice:N2}</td>
                        <td style='width: 74px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{detail.Amount:N2}</td>
                    </tr>");
            }

            populatedHtml = populatedHtml.Replace("{{TableRows}}", tableRows.ToString());

            return populatedHtml;
        }

        private MemoryStream ConvertHtmlToPdf(string htmlContent)
        {
            // Initialize the SelectPdf HtmlToPdf converter
            HtmlToPdf converter = new HtmlToPdf();

            // Set global options like page size, orientation, and color mode
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 20;
            converter.Options.MarginBottom = 20;

            // Set header and footer settings
            converter.Options.DisplayHeader = false;
            //converter.Header.DisplayOnFirstPage = true;
            //converter.Header.Height = 40;
            //PdfTextSection headerText = new PdfTextSection(500, 10, "[page] of [toPage]", new System.Drawing.Font("Arial", 9));
            //headerText.HorizontalAlign = PdfTextHorizontalAlign.Right;
            //converter.Header.Add(headerText);

            converter.Options.DisplayFooter = false;
            //converter.Footer.DisplayOnFirstPage = true;
            //converter.Footer.Height = 40;
            //PdfTextSection footerText = new PdfTextSection(250, 10, "Generated on " + DateTime.Now.ToString("yyyy-MM-dd"), new System.Drawing.Font("Arial", 9));
            //footerText.HorizontalAlign = PdfTextHorizontalAlign.Center;
            //converter.Footer.Add(footerText);

            // Convert HTML to PDF and save to memory stream
            PdfDocument doc = converter.ConvertHtmlString(htmlContent);

            var pdfStream = new MemoryStream();
            doc.Save(pdfStream);
            pdfStream.Position = 0; // Reset the stream position before returning

            // Close the PDF document to release resources
            doc.Close();

            return pdfStream;
        }


    }

}
