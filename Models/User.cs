﻿using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace VizInvoiceGeneratorWebAPI.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
        public Guid TenantId { get; set; }
    }
}
