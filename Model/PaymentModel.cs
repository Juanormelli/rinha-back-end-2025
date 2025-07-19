namespace rinha_back_end_2025.Model;

public struct PaymentModel {
  public Guid CorrelationId { get; set; }
  public decimal Amount { get; set; }
  public DateTime RequestedAt { get; set; } = DateTime.UtcNow; // ISO 8601 format

  [System.Text.Json.Serialization.JsonIgnore]
  public string CurrentPaymentToProccess { get; set; } = "Default";

  public PaymentModel () {

  }

  public void ChangePaymentProcessor () {
    this.CurrentPaymentToProccess = this.CurrentPaymentToProccess == "Default" ? this.CurrentPaymentToProccess = "Fallback" : CurrentPaymentToProccess = "Default";
  }

}
