using Microsoft.AspNetCore.Mvc;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.Services;
using rinha_back_end_2025.SourceGeneration;
using System.Text.Json;

namespace rinha_back_end_2025.Endpoints;

public static class WebApi {
  public static JsonSerializerOptions options = new JsonSerializerOptions()
  {
    TypeInfoResolver = PaymentsSerializerContext.Default
  };

  public static void RegisterEndpoints (this WebApplication app) {
    app.MapPost("/payments", ([FromBody] PaymentModel model, [FromServices] Processor processor) => {
      Results.Ok();
      processor.paymentQueue.OnNext(model);

    });

    app.MapGet("/payments-summary", async ([FromQuery] string? from, [FromQuery] string? to, [FromServices] Processor processor) => {
      DateTime fromDate = DateTime.Parse(from, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
      DateTime toDate = DateTime.Parse(to, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

      var summaryDefault = new PaymentSummaryModel();
      var summaryFallback = new PaymentSummaryModel();

      var httpClient = new HttpClient() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("workerSync")) };
      var abc = await httpClient.GetFromJsonAsync<List<PaymentModel>>($"/sync?from={from}&to={to}", options);

      foreach (var payment in abc) {
        summaryDefault.AddRequest(payment);
      }

      foreach (var payment in processor.repository1._paymentSummary.Values) {
        var requestedAt = payment.RequestedAt;
        if (requestedAt >= fromDate && requestedAt <= toDate) {
          summaryDefault.AddRequest(payment);
        }
      }
      using var stream = new MemoryStream();
      using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

      writer.WriteStartObject();

      writer.WritePropertyName("default");
      summaryDefault.WriteTo(writer);

      writer.WritePropertyName("fallback");
      summaryFallback.WriteTo(writer);

      writer.WriteEndObject();
      writer.Flush();

      return Results.File(stream.ToArray(), "application/json");

    });

    app.MapGet("/sync", async ([FromQuery] string? from, [FromQuery] string? to, [FromServices] Processor processor) => {
      DateTime fromDate = DateTime.Parse(from, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
      DateTime toDate = DateTime.Parse(to, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
      var newList = new List<PaymentModel>();
      foreach (var payment in processor.repository1._paymentSummary.Values) {
        var requestedAt = payment.RequestedAt;
        if (requestedAt >= fromDate && requestedAt <= toDate) {
          newList.Add(payment);
        }
      }

      return Results.Json(newList, options);
    });
  }
}
