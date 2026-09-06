using System.Reflection;
using System.Net;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Stripe;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Services.Payments;
using Stripe;
using Address = Nop.Plugin.Payments.Stripe.Models.Address;
using Customer = Nop.Core.Domain.Customers.Customer;
using Discount = Nop.Core.Domain.Discounts.Discount;
using Product = Nop.Core.Domain.Catalog.Product;
using Nop.Services.Orders;

Nop.Core.Infrastructure.Singleton<Nop.Core.Configuration.AppSettings>.Instance = new(new List<Nop.Core.Configuration.IConfig> { new Nop.Core.Configuration.CacheConfig() });
var api = new FakeStripe();
// Test-only injection: ALL SDK traffic is intercepted; no network or real key is used.
StripeConfiguration.StripeClient = new StripeClient("sk_test_local_fake_only", httpClient: api);
var buyer = new Buyer { Id = "1", Customer = new Customer { Id = 1 }, billingAddress = new Address {
    Name="Test", Surname="Only", Email="test@example.invalid", Address1="Test", Country="US", City="Test", ZipCode="10001" } };
var cart = new List<ShoppingCartItem> { new() { ProductId=1, Quantity=1 } };
object Handle(MethodInfo m, object[] a) {
    if (m.Name == "ErrorAsync") Console.WriteLine("TEST diagnostic: " + a.FirstOrDefault(x=>x is Exception));
    switch (m.Name) {
      case "PrepareKey": return new CacheKey("test");
      case "GetBuyer": return Task.FromResult(buyer);
      case "GetShoppingCartAsync": return Task.FromResult<IList<ShoppingCartItem>>(cart);
      case "GetWorkingCurrencyAsync": return Task.FromResult(new Currency { CurrencyCode="USD" });
      case "GetShoppingCartTotalAsync": return Task.FromResult(((decimal?)100m, 0m, new List<Discount>(), new List<AppliedGiftCard>(), 0, 0m));
      case "ConvertFromPrimaryStoreCurrencyAsync": return Task.FromResult((decimal)a[0]);
      case "GetProductByIdAsync": return Task.FromResult(new Product { Id=1, Name="Test item", Sku="TEST", IsShipEnabled=true });
      case "GetUnitPriceAsync": return Task.FromResult((100m, 0m, new List<Discount>()));
    }
    if (m.ReturnType == typeof(Task)) return Task.CompletedTask;
    if (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition()==typeof(Task<>)) {
      var t=m.ReturnType.GetGenericArguments()[0];
      return typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(t).Invoke(null,new[]{t.IsValueType?Activator.CreateInstance(t):null});
    }
    return m.ReturnType.IsValueType ? Activator.CreateInstance(m.ReturnType) : null;
}
var ctor=typeof(StripePaymentProcessor).GetConstructors().Single();
var argsForCtor=ctor.GetParameters().Select(p => {
    if(p.ParameterType==typeof(StripePaymentSettings)) return (object)new StripePaymentSettings { UseSandbox=true, TestSecretKey="sk_test_local_fake_only" };
    if(!p.ParameterType.IsInterface) return Activator.CreateInstance(p.ParameterType);
    var x=DispatchProxy.Create(p.ParameterType,typeof(Stub)); ((Stub)x).Handler=Handle; return x;
}).ToArray();
var processor=(StripePaymentProcessor)ctor.Invoke(argsForCtor);
ProcessPaymentRequest Request(Guid id) => new() { OrderGuid=id, CustomerId=1, StoreId=1, CreditCardNumber="4242424242424242", CreditCardCvv2="123", CreditCardExpireMonth=12, CreditCardExpireYear=2030, CreditCardName="Test Only" };
void Check(bool b,string name) { if(!b) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); }
var guid=Guid.NewGuid();
var first=await processor.ProcessPaymentAsync(Request(guid));
Check(first.Success && first.NewPaymentStatus==PaymentStatus.Paid,"initial synthetic payment succeeds");
var second=await processor.ProcessPaymentAsync(Request(guid));
Check(second.Success && second.AuthorizationTransactionId==first.AuthorizationTransactionId,"retry returns original intent");
Check(api.IntentCount==1 && api.CustomerCount==1 && api.MethodCount==1,"retry creates no duplicate payment/customer/method");
var retries=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>processor.ProcessPaymentAsync(Request(guid))));
Check(retries.All(x=>x.Success && x.AuthorizationTransactionId==first.AuthorizationTransactionId) && api.IntentCount==1,"10 concurrent retries remain one intent");
await processor.ProcessPaymentAsync(Request(Guid.NewGuid()));
Check(api.IntentCount==2,"independent checkout has independent intent");
var bad=Request(Guid.Empty); var before=api.Calls;
Check(!(await processor.ProcessPaymentAsync(bad)).Success && api.Calls==before,"empty checkout id rejected before provider calls");
var changed=Request(guid); changed.CreditCardNumber="4000000000000002";
Check(!(await processor.ProcessPaymentAsync(changed)).Success && api.IntentCount==2,"changed parameters fail closed without second charge");
api.LoseNextIntentResponse=true; var lost=Guid.NewGuid();
Check(!(await processor.ProcessPaymentAsync(Request(lost))).Success,"ambiguous network response surfaced as failure");
var count=api.IntentCount;
Check((await processor.ProcessPaymentAsync(Request(lost))).Success && api.IntentCount==count,"lost response retry recovers same intent");
Console.WriteLine("No production calls, charges, orders, or configuration changes were made.");

public class Stub : DispatchProxy {
 public Func<MethodInfo,object[],object> Handler;
 protected override object Invoke(MethodInfo m,object[] a)=>Handler(m,a);
}
public class FakeStripe : Stripe.IHttpClient {
 readonly Dictionary<string,(string Body,string Result)> memo=new(); readonly object gate=new();
 public int IntentCount,CustomerCount,MethodCount,Calls; public bool LoseNextIntentResponse;
 public async Task<StripeResponse> MakeRequestAsync(StripeRequest r,CancellationToken cancellationToken=default) {
  var body=r.Content==null?"":await r.Content.ReadAsStringAsync(cancellationToken);
  await Task.Yield(); // force overlapping continuations in the concurrent-retry test
  lock(gate) {
   Calls++; var key=r.Uri.AbsolutePath+":"+r.StripeHeaders["Idempotency-Key"];
   if(memo.TryGetValue(key,out var prior)) {
    if(prior.Body!=body) throw new StripeException("Idempotency parameter mismatch");
    return new StripeResponse(HttpStatusCode.OK,null,prior.Result);
   }
   var path=r.Uri.AbsolutePath; string json;
   if(path=="/v1/payment_methods") json=$"{{\"id\":\"pm_{++MethodCount}\",\"object\":\"payment_method\"}}";
   else if(path=="/v1/customers") json=$"{{\"id\":\"cus_{++CustomerCount}\",\"object\":\"customer\"}}";
   else if(path.EndsWith("/attach")) json="{\"id\":\"pm_attached\",\"object\":\"payment_method\"}";
   else if(path=="/v1/payment_intents") json=$"{{\"id\":\"pi_{++IntentCount}\",\"object\":\"payment_intent\",\"status\":\"succeeded\",\"amount\":10000,\"currency\":\"usd\"}}";
   else throw new Exception("Unexpected API path: "+path);
   memo[key]=(body,json);
   if(path=="/v1/payment_intents" && LoseNextIntentResponse) { LoseNextIntentResponse=false; throw new HttpRequestException("Synthetic response loss after processing"); }
   return new StripeResponse(HttpStatusCode.OK,null,json);
  }
 }
 public Task<StripeStreamedResponse> MakeStreamingRequestAsync(StripeRequest r,CancellationToken t=default)=>throw new NotSupportedException();
}
