using Polly;
using rinha_back_end_2025.Model;
using System.Collections.Concurrent;

namespace rinha_back_end_2025.Services;

public class Processor {
  private readonly IHttpClientFactory _clientFactory;
  private ConcurrentQueue<PaymentModel> _paymentQueue { get; set; } = new ConcurrentQueue<PaymentModel>();
  private ConcurrentDictionary<Guid, PaymentModel> _paymentSummary { get; set; } = new ConcurrentDictionary<Guid, PaymentModel>();
  public int timeoutDef { get; private set; }
  public int timeoutFall { get; private set; }

  private HttpClient clientSync;


  public Processor (ConcurrentQueue<PaymentModel> paymentsQueue, ConcurrentDictionary<Guid, PaymentModel> paymentSummary, IHttpClientFactory clientFactory) {

    _paymentQueue = paymentsQueue;
    _paymentSummary = paymentSummary;
    _clientFactory = clientFactory;

    clientSync = new HttpClient();
    clientSync.BaseAddress = new Uri(Environment.GetEnvironmentVariable("workerSync"));

    this.SyncPaymentSummary();
    this.UpdateTimeouBasedOnmPayments();
  }

  async public Task<bool> ProcessPayment (PaymentModel payment) {
    // Add the payment to the queue
    _paymentQueue.Enqueue(payment);
    // Update the summary for the current payment processor 
    this.SendRequestToPaymentProcessor();


    return true;
  }

  async private Task<bool> SendRequestToPaymentProcessor () {
    if (!_paymentQueue.TryDequeue(out PaymentModel payment))
      return false;


    Policy
          .HandleResult<bool>(c => c == false)
          .Or<TimeoutException>(c => c is TimeoutException)
          .Or<TaskCanceledException>(c => c is TaskCanceledException)
          .WaitAndRetryAsync(3, (i) => TimeSpan.FromMilliseconds(10))
          .ExecuteAsync(async () => {
            if (payment.CurrentPaymentToProccess == "Default") {
              var client = _clientFactory.CreateClient("default");
              if (timeoutDef > 1500) {
                var responseHighTime = client.PostAsJsonAsync("/payments", payment);
                _paymentSummary.TryAdd(payment.CorrelationId, payment);
                return true;
              }
              var response = await client.PostAsJsonAsync("/payments", payment);
              if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity) {
                return true;
              }
              if (!response.IsSuccessStatusCode) {
                payment.ChangePaymentProcessor();
                return false;
              }
              _paymentSummary.TryAdd(payment.CorrelationId, payment);
              return true;
            } else {
              var client = _clientFactory.CreateClient("fallback");
              if (timeoutFall > 1500) {
                var responseHighTime = client.PostAsJsonAsync("/payments", payment);
                _paymentSummary.TryAdd(payment.CorrelationId, payment);
                return true;
              }
              var response = await client.PostAsJsonAsync("/payments", payment);
              if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableContent) {
                return true;

              }
              if (!response.IsSuccessStatusCode) {
                payment.ChangePaymentProcessor();
                return false;
              }

              _paymentSummary.TryAdd(payment.CorrelationId, payment);
              return true;
            }
          });
    return true;
  }

  async public Task<Dictionary<string, PaymentSummaryModel>> GetPaymentSummary (string from, string to) {
    DateTime fromDate = DateTime.Parse(from).ToUniversalTime();
    DateTime toDate = DateTime.Parse(to).ToUniversalTime();

    var summaryPayment = new Dictionary<string, PaymentSummaryModel>() {
      {"default", new PaymentSummaryModel() },
      { "fallback", new PaymentSummaryModel() }

    };

    var summary = _paymentSummary.Values.Where(x => x.RequestedAt >= fromDate && x.RequestedAt <= toDate);

    foreach (var x in summary) {
      if (x.CurrentPaymentToProccess == "Default") {
        summaryPayment["default"].AddRequest(x);
      } else {
        summaryPayment["fallback"].AddRequest(x);
      }
    }
    return summaryPayment;
  }

  async public void SyncPaymentSummary () {


    Policy
           .HandleResult<bool>(c => c == false)
           .WaitAndRetryForeverAsync(i => TimeSpan.FromMilliseconds(1))
           .ExecuteAsync(async () => {
             try {

               var abc = await clientSync.GetFromJsonAsync<Dictionary<Guid, PaymentModel>>($"/sync");
               foreach (var value in abc) {
                 if (!_paymentSummary.TryAdd(value.Key, value.Value)) {
                   continue;
                 }
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
    Policy
        .HandleResult<bool>(c => c == false)  //you can add other condition
        .WaitAndRetryForeverAsync(i => TimeSpan.FromSeconds(10))
        .ExecuteAsync(async () => {
          var abc = await clientDef.GetFromJsonAsync<HCResponse>("/payments/service-health");
          var abc2 = await clientFall.GetFromJsonAsync<HCResponse>("/payments/service-health");

          timeoutDef = abc.minResponseTime + 250;
          timeoutFall = abc2.minResponseTime + 250;
          return false;

        }
        );
  }
}