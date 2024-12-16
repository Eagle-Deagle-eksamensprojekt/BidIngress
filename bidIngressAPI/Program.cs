using NLog.Web;
using NLog;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
