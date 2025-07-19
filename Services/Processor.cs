using Polly;
using Polly.Retry;
using rinha_back_end_2025.Model;
using System.Collections.Concurrent;

namespace rinha_back_end_2025.Services;

public class Processor {
  private readonly IHttpClientFactory _clientFactory;
  private ConcurrentQueue<PaymentModel> _paymentQueue { get; set; } = new ConcurrentQueue<PaymentModel>();
  private ConcurrentDictionary<Guid, PaymentModel> _paymentSummary { get; set; } = new ConcurrentDictionary<Guid, PaymentModel>();
  private ResiliencePipeline _pipebuilder;
  private ResiliencePipeline _pipebuilderSync;
  private IServiceProvider _serviceProvider;
  public Processor (ConcurrentQueue<PaymentModel> paymentsQueue, ConcurrentDictionary<Guid, PaymentModel> paymentSummary, IHttpClientFactory clientFactory) {

    _paymentQueue = paymentsQueue;
    _paymentSummary = paymentSummary;
    _clientFactory = clientFactory;

    var optionsOnRetry = new RetryStrategyOptions
    {
      ShouldHandle = args => args.Outcome switch {
        { Exception: HttpRequestException } => PredicateResult.True(),
        _ => PredicateResult.False()
      },
      MaxRetryAttempts = int.MaxValue
    };

    _pipebuilder = new ResiliencePipelineBuilder()
      .AddRetry(optionsOnRetry)
      .Build();


    var optionsOnRetrySync = new RetryStrategyOptions
    {
      MaxRetryAttempts = int.MaxValue,
      Delay = TimeSpan.FromSeconds(1),
    };

    _pipebuilderSync = new ResiliencePipelineBuilder()
  .AddRetry(optionsOnRetrySync)
  .Build();

    _clientFactory = clientFactory;

    this.SyncPaymentSummary();
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

    await Policy
          .HandleResult<bool>(c => c == false)  //you can add other condition
          .WaitAndRetryForeverAsync(i => TimeSpan.FromSeconds(25))
          .ExecuteAsync(async () => {
            if (payment.CurrentPaymentToProccess == "Default") {
              var client = _clientFactory.CreateClient("default");

              var response = await client.PostAsJsonAsync("payments", payment);
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

              var response = await client.PostAsJsonAsync("payments", payment);
              if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableContent) {
                return false;
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
    DateTime fromDate = DateTime.Parse(from);
    DateTime toDate = DateTime.Parse(to);

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
    var client = new HttpClient();
    client.BaseAddress = new Uri("http://localhost:5001");

    await Policy
           .HandleResult<bool>(c => c == false)
           .WaitAndRetryForeverAsync(i => TimeSpan.FromSeconds(1))
           .ExecuteAsync(async () => {
             try {

               var abc = await client.GetFromJsonAsync<ConcurrentDictionary<Guid, PaymentModel>>("/sync");
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
}

