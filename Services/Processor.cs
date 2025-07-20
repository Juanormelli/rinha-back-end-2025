using Polly;
using rinha_back_end_2025.Model;
using System.Collections.Concurrent;

namespace rinha_back_end_2025.Services;

public class Processor {
  private readonly IHttpClientFactory _clientFactory;
  private ConcurrentDictionary<Guid, PaymentModel> _paymentSummary { get; set; } = new ConcurrentDictionary<Guid, PaymentModel>(200, 20000);
  public int timeoutDef { get; private set; }
  public int timeoutFall { get; private set; }

  private static HttpClient clientSync = new HttpClient() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("workerSync")) };
  private bool isDefaultinFail;
  private bool isFallbackinFail;


  public Processor (ConcurrentDictionary<Guid, PaymentModel> paymentSummary, IHttpClientFactory clientFactory) {

    _paymentSummary = paymentSummary;
    _clientFactory = clientFactory;
    this.SyncPaymentSummary();
    this.UpdateTimeouBasedOnmPayments();
  }

  async public Task<bool> ProcessPayment (PaymentModel payment) {
    await this.SendRequestToPaymentProcessor(payment);
    return true;
  }

  async private Task<bool> SendRequestToPaymentProcessor (PaymentModel payment) {
    await Policy
          .HandleResult<bool>(c => {
            if (c == false) {
              payment.ChangePaymentProcessor();
              return true;
            }
            return false;
          }
          )
          .Or<TimeoutException>(c => {
            if (c is TimeoutException) {
              payment.ChangePaymentProcessor();
              return true;
            }
            return false;
          })
          .Or<TaskCanceledException>(c => {
            if (c is TaskCanceledException) {
              payment.ChangePaymentProcessor();
              return true;
            }
            return false;
          })
          .WaitAndRetryAsync(2, (i) => TimeSpan.FromMilliseconds(1))
          .ExecuteAsync(async () => {
            if (timeoutDef > 1500 && payment.CurrentPaymentToProccess == "default")
              return false;
            if (isDefaultinFail && isFallbackinFail)
              return false;

            var client = _clientFactory.CreateClient(payment.CurrentPaymentToProccess);
            var response = await client.PostAsJsonAsync("/payments", payment);
            if (!response.IsSuccessStatusCode) {
              return false;
            }
            _paymentSummary.TryAdd(payment.CorrelationId, payment);
            return true;
          });
    return true;
  }

  async public ValueTask<Dictionary<string, PaymentSummaryModel>> GetPaymentSummary (string from, string to) {
    DateTime fromDate = DateTime.Parse(from).ToUniversalTime();
    DateTime toDate = DateTime.Parse(to).ToUniversalTime();

    var summaryPayment = new Dictionary<string, PaymentSummaryModel>() {
      { "default", new PaymentSummaryModel() },
      { "fallback", new PaymentSummaryModel() }

    };

    var summary = _paymentSummary.Values.Where(x => x.RequestedAt >= fromDate && x.RequestedAt <= toDate);

    foreach (var x in summary) {
      summaryPayment[x.CurrentPaymentToProccess].AddRequest(x);
    }
    return summaryPayment;
  }

  async public void SyncPaymentSummary () {


    await Policy
            .HandleResult<bool>(c => c == false)
            .WaitAndRetryForeverAsync(i => TimeSpan.FromMilliseconds(1))
            .ExecuteAsync(async () => {
              try {
                var abc = await clientSync.GetFromJsonAsync<Dictionary<Guid, PaymentModel>>($"/sync?from={DateTime.UtcNow.AddMilliseconds(-500).ToString("o")}");
                foreach (var value in abc) {
                  _paymentSummary.TryAdd(value.Key, value.Value);
                }
                return false;
              } catch (HttpRequestException) {
                return false;
              }
            });
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