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
  public IObservable<PaymentModel> PaymentQueue => paymentQueue.AsObservable();
  //private static HttpClient clientSync = new HttpClient() { BaseAddress = new Uri("http://localhost:9999") };
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
        repository1._paymentSummary.TryAdd(payment.CorrelationId, payment);
        return true;
      });

    }
  }

}