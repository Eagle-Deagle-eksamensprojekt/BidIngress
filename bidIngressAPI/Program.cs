using NLog.Web;
using NLog;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// NLog
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings()
        .GetCurrentClassLogger();
        logger.Debug("init main"); // NLog setup

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// definer port
//builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Registrér at I ønsker at bruge NLOG som logger fremadrettet (før builder.build)
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Vault-integration
var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN") 
                 ?? throw new Exception("Vault token not found");
var vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL") 
               ?? "http://vault:8200"; // Standard Vault URL

var authMethod = new TokenAuthMethodInfo(vaultToken);
var vaultClientSettings = new VaultClientSettings(vaultUrl, authMethod);
var vaultClient = new VaultClient(vaultClientSettings);

var kv2Secret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: "Secrets", mountPoint: "secret");
var jwtSecret = kv2Secret.Data.Data["jwtSecret"]?.ToString() ?? throw new Exception("jwtSecret not found in Vault.");
var jwtIssuer = kv2Secret.Data.Data["jwtIssuer"]?.ToString() ?? throw new Exception("jwtIssuer not found in Vault.");


// Register JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = "http://localhost", // Tilpas efter behov
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication(); // Auhorization
app.UseAuthorization();

app.MapControllers();

app.Run();
