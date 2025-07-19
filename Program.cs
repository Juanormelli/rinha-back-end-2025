using rinha_back_end_2025.Endpoints;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.Services;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var services = builder.Services;

// Register any additional services here

services.AddSingleton<ConcurrentQueue<PaymentModel>>();
services.AddSingleton<Processor>();
services.AddSingleton<ConcurrentDictionary<Guid, PaymentModel>>();

services.AddHttpClient("default", c => {
  c.BaseAddress = new System.Uri("http://localhost:8001");
});

services.AddHttpClient("fallback", c => {
  c.BaseAddress = new System.Uri("http://localhost:8002");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
  app.MapOpenApi();
}

app.RegisterEndpoints();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
