using Polly;
using rinha_back_end_2025.Model;
using rinha_back_end_2025.SourceGeneration;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace rinha_back_end_2025.Services;

public class Processor {
  public Subject<PaymentModel> paymentQueue = new Subject<PaymentModel>();
  public Subject<PaymentModel> paymentSync = new Subject<PaymentModel>();
  private readonly IHttpClientFactory _clientFactory;
  public Repository repository1;
  public IObservable<PaymentModel> PaymentQueue => paymentQueue.AsObservable();
  public IObservable<PaymentModel> PaymentSync => paymentSync.AsObservable();
  public JsonSerializerOptions options;

  public Processor (Repository repository, IHttpClientFactory clientFactory) {
    options = new JsonSerializerOptions()
    {
      TypeInfoResolver = PaymentsSerializerContext.Default
    };
    _clientFactory = clientFactory;
    repository1 = repository;
    PaymentQueue.Buffer(TimeSpan.FromMilliseconds(100)).Subscribe(async x => SendRequestToPaymentProcessor(x));
    PaymentSync.Buffer(TimeSpan.FromMilliseconds(500)).Subscribe(async x => SyncPayments(x));
  }

  async private Task SendRequestToPaymentProcessor (IList<PaymentModel> payments) {
    var newList = new List<PaymentModel>();
    var policy = Policy
      .HandleResult<bool>(c => {
        if (c == false) {
          return true;
        }
        return false;
      }
      )
      .Or<TimeoutException>(c => {
        if (c is TimeoutException) {
          return true;
        }
        return false;
      })
      .Or<TaskCanceledException>(c => {
        if (c is TaskCanceledException) {
          return true;
        }
        return false;
      })
      .WaitAndRetryAsync(1000, (i) => TimeSpan.FromMilliseconds(1));

    foreach (var payment in payments) {

      await policy.ExecuteAsync(async () => {
        var client = _clientFactory.CreateClient(payment.CurrentPaymentToProccess);
        payment.RequestedAt = DateTime.UtcNow;
        var response = await client.PostAsJsonAsync("/payments", payment, options);
        if (!response.IsSuccessStatusCode) {
          return false;
        }
        newList.Add(payment);
        repository1._paymentSummary.TryAdd(payment.CorrelationId, payment);
        return true;
      });


    }
    var httpClient = new HttpClient() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("workerSync")) };
    httpClient.PostAsJsonAsync("/sync", repository1._paymentSummary.Values.ToList(), options);
  }
  async Task SyncPayments (IList<PaymentModel> payments) {
    foreach (var payment in payments) {
      repository1._paymentSummary.TryAdd(payment.CorrelationId, payment);
    }
  }
}