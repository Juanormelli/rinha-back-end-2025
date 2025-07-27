using rinha_back_end_2025.Model;
using System.Collections.Concurrent;

namespace rinha_back_end_2025;

public class Repository {
  public ConcurrentDictionary<Guid, PaymentModel> _paymentSummary { get; set; } = new ConcurrentDictionary<Guid, PaymentModel>(200, 20000);

}
