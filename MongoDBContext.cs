using MongoDB.Driver;
using VizInvoiceGeneratorWebAPI.Models;

namespace VizInvoiceGeneratorWebAPI
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration config)
        {
            var client = new MongoClient(config["MongoDB:ConnectionString"]);
            _database = client.GetDatabase(config["MongoDB:Database"]);
        }

        public IMongoCollection<Invoice> Invoices => _database.GetCollection<Invoice>("invoices");
        public IMongoCollection<InvoiceTemplate> InvoiceTemplate => _database.GetCollection<InvoiceTemplate>("invoice_template");
    }

}
