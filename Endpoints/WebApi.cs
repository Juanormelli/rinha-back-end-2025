using Microsoft.AspNetCore.Mvc;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.Services;
using System.Collections.Concurrent;

namespace rinha_back_end_2025.Endpoints;

public static class WebApi {
  public static void RegisterEndpoints (this WebApplication app) {
    app.MapPost("/payments", async ([FromBody] PaymentModel model, [FromServices] Processor processor) => {

      processor.ProcessPayment(model);
      Results.Ok();

    });
    app.MapGet("/payments-summary", async ([FromQuery] string from, [FromQuery] string to, [FromServices] Processor processor) => {
      var abc = await processor.GetPaymentSummary(from, to);
      return Results.Ok(abc);



    });
    app.MapGet("/sync", ([FromServices] ConcurrentDictionary<Guid, PaymentModel> abc) => {
      return Results.Ok(abc);
    });
    // Add more endpoints as needed
  }
}
