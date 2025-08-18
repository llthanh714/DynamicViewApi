using DynamicViewApi.Middleware; // Thêm dòng này
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Kestrel để lắng nghe trên cả HTTP và HTTPS
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Cấu hình HTTPS trên cổng 7274
    var certConfig = builder.Configuration.GetSection("Kestrel:Certificate");
    var certPath = certConfig["Path"];
    var kestrelCertPassword = Environment.GetEnvironmentVariable("__KESTREL_CERT_PASSWORD__", EnvironmentVariableTarget.Machine);
    var certPassword = certConfig["Password"]?.Replace("__KESTREL_CERT_PASSWORD__", kestrelCertPassword);

    if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword))
    {
        serverOptions.Listen(IPAddress.Any, 7274, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            listenOptions.UseHttps(certPath, certPassword);
        });
    }
    else
    {
        Console.WriteLine("Kestrel certificate not configured. HTTPS will not be available.");
    }

    // Cấu hình HTTP trên cổng 7273
    serverOptions.Listen(IPAddress.Any, 7273, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
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

// KHÔNG sử dụng UseHttpsRedirection nữa
// app.UseHttpsRedirection();

// Sử dụng Middleware kiểm tra IP
app.UseMiddleware<IPWhitelistMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();