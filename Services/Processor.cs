using Polly;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.SourceGeneration;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace rinha_back_end_2025.Services;

public class Processor {
  public Subject<PaymentModel> paymentQueue = new Subject<PaymentModel>();
  private readonly IHttpClientFactory _clientFactory;
  public Repository repository1;
  public int timeoutDef { get; private set; }
  public int timeoutFall { get; private set; }
  public IObservable<PaymentModel> PaymentQueue => paymentQueue.AsObservable();
  private static HttpClient clientSync = new HttpClient() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("workerSync")) };
  //private static HttpClient clientSync = new HttpClient() { BaseAddress = new Uri("http://localhost:9999") };
  private bool isDefaultinFail;
  private bool isFallbackinFail;
  public System.Timers.Timer timer;
  public bool started;
  public JsonSerializerOptions options;

  public Processor (Repository repository, IHttpClientFactory clientFactory) {
    options = new JsonSerializerOptions()
    {
      TypeInfoResolver = PaymentsSerializerContext.Default
    };
    _clientFactory = clientFactory;
    repository1 = repository;
    PaymentQueue.Buffer(TimeSpan.FromMilliseconds(100)).Subscribe(async x => SendRequestToPaymentProcessor(x));

  }

  async private Task SendRequestToPaymentProcessor (IList<PaymentModel> payments) {
    foreach (var payment in payments) {
      await Policy
           .HandleResult<bool>(c => {
             if (c == false) {
               //payment.ChangePaymentProcessor();
               return true;
             }
             return false;
           }
           )
           .Or<TimeoutException>(c => {
             if (c is TimeoutException) {
               //payment.ChangePaymentProcessor();
               return true;
             }
             return false;
           })
           .Or<TaskCanceledException>(c => {
             if (c is TaskCanceledException) {
               //payment.ChangePaymentProcessor();
               return true;
             }
             return false;
           })
           .WaitAndRetryAsync(1000, (i) => TimeSpan.FromMilliseconds(1))
           .ExecuteAsync(async () => {
             var client = _clientFactory.CreateClient(payment.CurrentPaymentToProccess);
             payment.RequestedAt = DateTime.UtcNow;
             var response = await client.PostAsJsonAsync("/payments", payment, options);
             if (!response.IsSuccessStatusCode) {
               return false;
             }
             repository1._paymentSummary.TryAdd(payment.CorrelationId, payment);
             return true;
           });

    }
  }

  //async public ValueTask<Dictionary<string, PaymentSummaryModel>> GetPaymentSummary (string from, string to) {
  //  DateTime fromDate = DateTime.Parse(from, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
  //  DateTime toDate = DateTime.Parse(to, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

  //  var summaryDefault = new PaymentSummaryModel();
  //  var summaryFallback = new PaymentSummaryModel();

  //  foreach (var payment in repository1._paymentSummary.Values) {
  //    var requestedAt = payment.RequestedAt;
  //    if (requestedAt > fromDate.AddMilliseconds(-5) && requestedAt <= toDate) {
  //      summaryDefault.AddRequest(payment);
  //    }
  //  }

  //  using var stream = new MemoryStream();
  //  using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

  //  writer.WriteStartObject();

  //  writer.WritePropertyName("default");
  //  summaryDefault.WriteTo(writer);

  //  writer.WritePropertyName("fallback");
  //  summaryFallback.WriteTo(writer);

  //  writer.WriteEndObject();
  //  writer.Flush();

  //  return new FileContentResult(stream.ToArray(), "application/json");
  //}

  public async void SyncPaymentAfterSeconds () {
    var lastExecdateTime = DateTime.UtcNow;
    await Policy
          .HandleResult<bool>(c => {
            if (c == false) {
              lastExecdateTime = DateTime.UtcNow;
              return true;
            }
            return false;
          })
          .WaitAndRetryForeverAsync(i => TimeSpan.FromMilliseconds(500))
          .ExecuteAsync(async () => {

            //var syncExec = _paymentSummary.Where(x => x.Value.RequestedAt >= lastExecdateTime.AddSeconds(-15)).ToDictionary();
            await clientSync.PostAsJsonAsync("/sync", repository1._paymentSummary, options);
            return false;

          });
  }


  async public Task SyncPaymentSummary (Dictionary<Guid, PaymentModel> values) {

    clientSync.PostAsJsonAsync($"/sync", values);
  }

  async public void UpdateTimeouBasedOnmPayments () {
    var clientDef = _clientFactory.CreateClient("default");
    var clientFall = _clientFactory.CreateClient("fallback");
    await Policy
        .HandleResult<bool>(c => c == false)  //you can add other condition
        .WaitAndRetryForeverAsync(i => TimeSpan.FromSeconds(5))
        .ExecuteAsync(async () => {
          var abc = await clientDef.GetFromJsonAsync<HCResponse>("/payments/service-health");
          var abc2 = await clientFall.GetFromJsonAsync<HCResponse>("/payments/service-health");
          isDefaultinFail = abc.failing;
          isFallbackinFail = abc2.failing;
          timeoutDef = abc.minResponseTime + 250;
          timeoutFall = abc2.minResponseTime + 250;
          return false;

        }

        );
  }
}