using DynamicViewApi.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Any, 7273, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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