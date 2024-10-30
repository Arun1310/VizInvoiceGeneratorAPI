using Microsoft.AspNetCore.Mvc;
using VizInvoiceGeneratorWebAPI.Models;
using VizInvoiceGeneratorWebAPI.Services;

namespace VizInvoiceGeneratorWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly FormRecognizerService _formRecognizerService;
        private readonly InvoiceRepositoryService _invoiceRepositoryService;

        public InvoiceController(FormRecognizerService formRecognizerService, InvoiceRepositoryService invoiceRepositoryService)
        {
            _formRecognizerService = formRecognizerService;
            _invoiceRepositoryService = invoiceRepositoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllInvoices()
        {
            var invoices = await _invoiceRepositoryService.GetAllInvoicesAsync();
            return Ok(invoices);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoiceById(string id)
        {
            var invoice = await _invoiceRepositoryService.GetInvoiceById(id);
            if (invoice == null)
            {
                return NotFound("Invoice not found.");
            }

            return Ok(invoice);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInvoice(string id, [FromBody] Invoice updatedInvoice)
        {
            // Check if the invoice exists
            var invoice = await _invoiceRepositoryService.GetInvoiceById(id);
            if (invoice == null)
            {
                return NotFound("Invoice not found.");
            }

            // Update the invoice fields with new data
           // invoice.InvoiceResult = updatedInvoice.InvoiceResult;
            invoice.State = updatedInvoice.State;
            invoice.CustomGeneratedInvoiceUrl = updatedInvoice.CustomGeneratedInvoiceUrl;

            var result = await _invoiceRepositoryService.UpdateInvoice(id, invoice);

            if (!result)
            {
                return StatusCode(500, "An error occurred while updating the invoice.");
            }

            return Ok(invoice);
        }

        [HttpGet("GetCustomInvoice/{id}")]
        public async Task<IActionResult> GetInvoicePdf(string id)
        {
            var pdfStream = await _invoiceRepositoryService.GetInvoiceTemplate(id);

            // Use a unique file name based on the invoice number
            var invoice = await _invoiceRepositoryService.GetInvoiceById(id);
            if (invoice == null)
            {
                return NotFound("Invoice not found.");
            }

            return File(pdfStream, "application/pdf", $"Custom_{invoice.FileName}.pdf");
        }

        [HttpPost("Analyze")]
        public async Task<IActionResult> AnalyzeInvoice([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a valid invoice document.");
            }

            using (var stream = file.OpenReadStream())
            {
                var result = await _formRecognizerService.AnalyzeInvoiceAsync(stream);
                return Ok(result);
            }
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> UploadInvoiceAsync([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a valid invoice document.");
            }

            var result = await _invoiceRepositoryService.UploadInvoiceAsync(file);
            return Ok(result);
        }

        [HttpPost("UploadCustomInvoice/{id}")]
        public async Task<IActionResult> UploadCustomInvoiceAsync(string id, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a valid invoice document.");
            }

            var result = await _invoiceRepositoryService.UploadCustomInvoiceAsync(id, file);
            return Ok(result);
        }
    }
}
