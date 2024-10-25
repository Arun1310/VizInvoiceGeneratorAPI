using Azure.Storage.Blobs;
using VizInvoiceGeneratorWebAPI.Services;
using VizInvoiceGeneratorWebAPI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Localization;
using System.Globalization;
using VizInvoiceGeneratorWebAPI.Resources;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Localization;
using VizInvoiceGeneratorWebAPI.Interfaces;
using VizInvoiceGeneratorWebAPI.Services.Auth;

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
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddTransient<ITokenService, TokenService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("EnableCORS", builder =>
    {
        builder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VizInvoiceGeneratorWebAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                          new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new string[] {}

                    }
                });
});
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var validIssuer = builder.Configuration["JWTSettings:ValidIssuer"];
    var validAudience = builder.Configuration["JWTSettings:ValidAudience"];
    var secret = builder.Configuration["JWTSettings:Secret"];

    if (string.IsNullOrEmpty(validIssuer))
    {
        throw new ArgumentException("JWT ValidIssuer is not configured.");
    }

    if (string.IsNullOrEmpty(validAudience))
    {
        throw new ArgumentException("JWT ValidAudience is not configured.");
    }

    if (string.IsNullOrEmpty(secret))
    {
        throw new ArgumentException("JWT Secret is not configured.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = validIssuer,
        ValidAudience = validAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#region Localization
builder.Services.AddSingleton<IdentityLocalizationService>();
builder.Services.AddLocalization(o =>
{
    // We will put our translations in a folder called Resources
    o.ResourcesPath = "Resources";
});
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddSingleton<IStringLocalizer, JsonStringLocalizer>();
builder.Services.AddMvc()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix,
    opts => { opts.ResourcesPath = "Resources"; })
.AddDataAnnotationsLocalization(options =>
{
});
CultureInfo.CurrentCulture = new CultureInfo("en-US");
#endregion

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

#region Localization
IList<CultureInfo> supportedCultures = new List<CultureInfo>
    {
        new CultureInfo("en-US"),
        new CultureInfo("en-IN"),
        new CultureInfo("fr-FR"),
    };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
app.UseRequestLocalization(localizationOptions);

var requestProvider = new RouteDataRequestCultureProvider();
localizationOptions.RequestCultureProviders.Insert(0, requestProvider);

// Apply localization settings directly
app.UseRequestLocalization(localizationOptions);

#endregion

app.MapControllers();

app.Run();
