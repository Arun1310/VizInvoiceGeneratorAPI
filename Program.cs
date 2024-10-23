using Azure.Storage.Blobs;
using VizInvoiceGeneratorWebAPI.Services;
using VizInvoiceGeneratorWebAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<FormRecognizerService>(sp =>
    new FormRecognizerService(
        builder.Configuration["Azure:FormRecognizerEndpoint"],
        builder.Configuration["Azure:FormRecognizerApiKey"]));

builder.Services.AddSingleton<MongoDbContext>();

// Register the InvoiceRepository
builder.Services.AddScoped<InvoiceRepositoryService>();

builder.Services.AddScoped(options =>
{
    return new BlobServiceClient(builder.Configuration["AzureBlobStorageConnectionString"]);
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("EnableCORS", builder =>
    {
        builder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("EnableCORS");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
