using rinha_back_end_2025.Endpoints;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.Services;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.



// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Logging.ClearProviders();

var services = builder.Services;

// Register any additional services here

services.AddSingleton<ConcurrentQueue<PaymentModel>>();
services.AddSingleton<Processor>();
services.AddSingleton<ConcurrentDictionary<Guid, PaymentModel>>();

services.AddHttpClient("default", c => {
  c.BaseAddress = new System.Uri("http://payment-processor-default:8080");
});

services.AddHttpClient("fallback", c => {
  c.BaseAddress = new System.Uri("http://payment-processor-fallback:8080");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.RegisterEndpoints();

app.UseHttpsRedirection();



app.Run();
