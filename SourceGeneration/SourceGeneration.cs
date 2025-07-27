using rinha_back_end_2025.Model;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace rinha_back_end_2025.SourceGeneration;

[JsonSerializable(typeof(PaymentModel))]
[JsonSerializable(typeof(Repository))]

[JsonSerializable(typeof(PaymentModel[]))]
[JsonSerializable(typeof(List<PaymentModel>))]
[JsonSerializable(typeof(HCResponse))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(ConcurrentDictionary<Guid, PaymentModel>))]
[JsonSerializable(typeof(IEnumerable<PaymentModel>))]
[JsonSerializable(typeof(Dictionary<Guid, PaymentModel>))]
[JsonSerializable(typeof(ConcurrentDictionary<string, PaymentSummaryModel>))]
[JsonSerializable(typeof(Dictionary<string, PaymentSummaryModel>))]
[JsonSerializable(typeof(Dictionary<string, PaymentSummaryModel>))]

partial class PaymentsSerializerContext : JsonSerializerContext {
}