using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);


var kestrelCertPassword = Environment.GetEnvironmentVariable("__KESTREL_CERT_PASSWORD__", EnvironmentVariableTarget.Machine);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var certConfig = builder.Configuration.GetSection("Kestrel:Certificate");
    var certPath = certConfig["Path"];
    var certPassword = certConfig["Password"]?.Replace("__KESTREL_CERT_PASSWORD__", kestrelCertPassword);

    if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(certPassword))
    {
        Console.WriteLine("Kestrel certificate path or password is not configured. HTTPS will not be available.");
        return;
    }

    serverOptions.ConfigureHttpsDefaults(https =>
    {
        https.ServerCertificate = new X509Certificate2(certPath, certPassword);
    });

    serverOptions.Listen(IPAddress.Any, 7273, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
