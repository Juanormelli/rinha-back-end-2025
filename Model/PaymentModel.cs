namespace rinha_back_end_2025.Model;

public class PaymentModel {
  public Guid CorrelationId { get; set; }
  public decimal Amount { get; set; }
  public DateTime RequestedAt { get; set; }  // ISO 8601 format
  public string CurrentPaymentToProccess { get; set; } = "default";

  public PaymentModel () {

  }

  public void ChangePaymentProcessor () {
    this.CurrentPaymentToProccess = this.CurrentPaymentToProccess == "default" ? "fallback" : "default";
  }


}
