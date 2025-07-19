namespace rinha_back_end_2025.Model;

public class PaymentSummaryModel {
  public int TotalRequests { get; set; }
  public decimal TotalAmount { get; set; }
  public PaymentSummaryModel () {

  }
  public void AddRequest (PaymentModel payment) {
    this.TotalRequests++;
    this.TotalAmount += payment.Amount;
  }
}
