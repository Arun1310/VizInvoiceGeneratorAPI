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

        public async Task<bool> UpdateInvoice(string id, Invoice updatedInvoice)
        {
            try
            {
                var result = await _invoices.ReplaceOneAsync(i => i.Id == id, updatedInvoice);
                return result.IsAcknowledged && (result.ModifiedCount > 0 || result.MatchedCount > 0);
            }
            catch (Exception ex)
            {
                throw;
            }

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

                // Get the mapped attributes instead of a specific CustomInvoiceResult
                var attributes = await AnalyzeInvoiceAsync(file);

                var invoice = new Invoice
                {
                    FileName = file.FileName,
                    FileUrl = uploadedFile.FilePath,
                    UploadDate = DateTime.UtcNow,
                    State = 1,
                    Attributes = attributes, // Store the mapped attributes list here
                    CustomGeneratedInvoiceUrl = null
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

                invoice.State = 4;
                invoice.CustomGeneratedInvoiceUrl = uploadedFile.FilePath;

                var result = await UpdateInvoice(id, invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        public async Task<List<Attributes>> AnalyzeInvoiceAsync(IFormFile file)
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

                    var attributesList = new List<Attributes>();

                    if (result.Documents.Count > 0)
                    {
                        AnalyzedDocument document = result.Documents[0];

                        // Iterate over all fields in the document only once
                        foreach (var field in document.Fields)
                        {
                            // For each field, check if there are bounding regions and process them
                            List<PositionData> positionDataList = new();
                            if (field.Value.BoundingRegions != null)
                            {
                                foreach (var region in field.Value.BoundingRegions)
                                {
                                    // Get the specific page dimensions for the bounding region's page number
                                    var page = result.Pages.FirstOrDefault(p => p.PageNumber == region.PageNumber);
                                    if (page != null)
                                    {
                                        var pageWidth = page.Width;
                                        var pageHeight = page.Height;

                                        positionDataList.Add(new PositionData
                                        {
                                            PageIndex = region.PageNumber - 1,
                                            Left = region.BoundingPolygon.Min(point => point.X) / pageWidth * 100,
                                            Top = region.BoundingPolygon.Min(point => point.Y) / pageHeight * 100,
                                            Width = (region.BoundingPolygon.Max(point => point.X) - region.BoundingPolygon.Min(point => point.X)) / pageWidth * 100,
                                            Height = (region.BoundingPolygon.Max(point => point.Y) - region.BoundingPolygon.Min(point => point.Y)) / pageHeight * 100
                                        });
                                    }
                                }
                            }

                            var attribute = new Attributes
                            {
                                AttributeName = field.Key,
                                AttributeValue = field.Value.Content ?? "",
                                Position = positionDataList,
                                IsAttributeMapped = false
                            };

                            // Check if the field is a list (e.g., items array in the invoice)
                            if (field.Value.FieldType == DocumentFieldType.List && field.Value.Value.AsList() != null)
                            {
                                attribute.Children = new List<Attributes>();

                                // Loop over each item in the list
                                foreach (var listItem in field.Value.Value.AsList())
                                {
                                    if (listItem.FieldType == DocumentFieldType.Dictionary && listItem.Value.AsDictionary() != null)
                                    {
                                        var childAttributes = new Attributes { Children = new List<Attributes>() };

                                        // Loop over each key-value pair in the dictionary item
                                        foreach (var itemField in listItem.Value.AsDictionary())
                                        {
                                            List<PositionData> childPositionDataList = new();
                                            if (itemField.Value.BoundingRegions != null)
                                            {
                                                foreach (var childRegion in itemField.Value.BoundingRegions)
                                                {
                                                    var childPage = result.Pages.FirstOrDefault(p => p.PageNumber == childRegion.PageNumber);
                                                    if (childPage != null)
                                                    {
                                                        var childPageWidth = childPage.Width;
                                                        var childPageHeight = childPage.Height;

                                                        childPositionDataList.Add(new PositionData
                                                        {
                                                            PageIndex = childRegion.PageNumber - 1,
                                                            Left = childRegion.BoundingPolygon.Min(point => point.X) / childPageWidth * 100,
                                                            Top = childRegion.BoundingPolygon.Min(point => point.Y) / childPageHeight * 100,
                                                            Width = (childRegion.BoundingPolygon.Max(point => point.X) - childRegion.BoundingPolygon.Min(point => point.X)) / childPageWidth * 100,
                                                            Height = (childRegion.BoundingPolygon.Max(point => point.Y) - childRegion.BoundingPolygon.Min(point => point.Y)) / childPageHeight * 100
                                                        });
                                                    }
                                                }
                                            }

                                            childAttributes.Children.Add(new Attributes
                                            {
                                                AttributeName = itemField.Key,
                                                AttributeValue = itemField.Value.Content ?? "",
                                                Position = childPositionDataList,
                                                IsAttributeMapped = false
                                            });
                                        }
                                        attribute.Children.Add(childAttributes);
                                    }
                                }
                            }

                            attributesList.Add(attribute);
                        }
                    }


                    return attributesList;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private List<PositionData> GetPositionData(IReadOnlyList<BoundingRegion>? boundingRegions, float? pageWidth, float? pageHeight)
        {
            if (boundingRegions == null)
            {
                return new List<PositionData>();
            }

            return boundingRegions.Select(region =>
            {
                var xCoordinates = region.BoundingPolygon.Select(point => point.X).ToList();
                var yCoordinates = region.BoundingPolygon.Select(point => point.Y).ToList();

                return new PositionData
                {
                    PageIndex = region.PageNumber - 1,
                    Left = (double)xCoordinates.Min() / pageWidth * 100,
                    Top = (double)yCoordinates.Min() / pageHeight * 100,
                    Width = (double)(xCoordinates.Max() - xCoordinates.Min()) / pageWidth * 100,
                    Height = (double)(yCoordinates.Max() - yCoordinates.Min()) / pageHeight * 100
                };


            }).ToList();
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
            string populatedHtml = template;

            // Check if Attributes is not null and has items
            if (invoice.Attributes != null && invoice.Attributes.Any())
            {
                foreach (var attribute in invoice.Attributes)
                {
                    switch (attribute.AttributeName)
                    {
                        case "VendorTaxId":
                            populatedHtml = populatedHtml.Replace("{{GSTIN}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "VendorName":
                            populatedHtml = populatedHtml.Replace("{{VENDORNAME}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "VendorAddress":
                            populatedHtml = populatedHtml.Replace("{{VENDORADDRESS}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "InvoiceId":
                            populatedHtml = populatedHtml.Replace("{{INVOICENO}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "InvoiceDate":
                            populatedHtml = populatedHtml.Replace("{{INVOICEDATE}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "PurchaseOrder":
                            populatedHtml = populatedHtml.Replace("{{PONO}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "PurchaseOrderDate":
                            populatedHtml = populatedHtml.Replace("{{DATE}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "CustomerAddressRecipient":
                        case "ShippingAddressRecipient":
                            populatedHtml = populatedHtml.Replace("{{CUSTOMERNAME}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "CustomerAddress":
                        case "ShippingAddress":
                            populatedHtml = populatedHtml.Replace("{{CUSTOMERADDRESS}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "InvoiceTotal":
                            populatedHtml = populatedHtml.Replace("{{TOTAL}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "InvoiceTotalInWords":
                            populatedHtml = populatedHtml.Replace("{{INVOICETOTALINWORDS}}", attribute.AttributeValue ?? string.Empty);
                            break;
                        case "Items":
                            StringBuilder tableRows = new StringBuilder();
                            if (attribute.Children != null && attribute.Children.Any())
                            {
                                foreach (var detail in attribute.Children)
                                {
                                    string description = string.Empty;
                                    string productCode = string.Empty;
                                    string quantity = string.Empty;
                                    string unit = string.Empty;
                                    string unitPrice = string.Empty;
                                    string amount = string.Empty;

                                    foreach (var itemAttribute in detail.Children)
                                    {
                                        switch (itemAttribute.AttributeName)
                                        {
                                            case "Description":
                                                description = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                            case "ProductCode":
                                                productCode = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                            case "Quantity":
                                                quantity = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                            case "Unit":
                                                unit = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                            case "UnitPrice":
                                                unitPrice = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                            case "Amount":
                                                amount = itemAttribute.AttributeValue ?? string.Empty;
                                                break;
                                        }
                                    }
                                    tableRows.Append($@"
                                        <tr style='height:26.4pt;'>
                                            <td style='width: 76px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'></td>
                                            <td style='width: 96px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'></td>
                                            <td colspan='2' style='width: 303px; border: 1.5pt solid black; padding: 1.6pt 1.2pt 0in 1.25pt;'>{description}</td>
                                            <td style='width: 50px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{productCode}</td>
                                            <td style='width: 44px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{quantity}</td>
                                            <td style='width: 39px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{unit}</td>
                                            <td style='width: 74px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{unitPrice}</td>
                                            <td style='width: 74px; border: 1.5pt solid black; padding: 1.6pt 8px; text-align: center;'>{amount}</td>
                                        </tr>");
                                }
                            }
                            populatedHtml = populatedHtml.Replace("{{TableRows}}", tableRows.ToString());
                            break;

                    }
                }
            }

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
