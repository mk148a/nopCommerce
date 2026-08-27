#nullable enable
#pragma warning disable CS8602, CS8603, CS8604, CS8625, CS8765 // LinkGenerator's nullable annotations differ across target packs.

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Widgets.GoogleAnalytics;
using Nop.Plugin.Widgets.GoogleAnalytics.Controllers;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;
using Nop.Plugin.Widgets.GoogleAnalytics.Services;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Widgets.GoogleAnalytics.Tests.Services;

[TestFixture]
public class GoogleAnalyticsPurchaseDispatchTests
{
    [Test]
    public async Task ConcurrentReservationsHaveExactlyOneDatabaseLeaseOwner()
    {
        const int contenders = 24;
        var store = new InMemoryDispatchStore(initialReadParticipants: contenders);
        var service = new GoogleAnalyticsPurchaseDispatchService(store);
        var order = PaidOrder();

        var leases = await Task.WhenAll(Enumerable.Range(0, contenders)
            .Select(_ => service.TryReserveAsync(order)));

        Assert.Multiple(() =>
        {
            Assert.That(leases.Count(lease => lease != null), Is.EqualTo(1));
            Assert.That(store.InsertAttempts, Is.EqualTo(contenders),
                "The barrier makes every contender observe no row before the unique-index insert race.");
            Assert.That(store.Get(42).Status, Is.EqualTo("leased"));
        });
    }

    [Test]
    public async Task ExpiredLeaseIsReclaimedAndOnlyTheNewCapabilityCanConfirm()
    {
        var store = new InMemoryDispatchStore(new GoogleAnalyticsPurchaseDispatch
        {
            Id = 1,
            OrderId = 42,
            Status = "leased",
            LeaseToken = "old-token",
            LeaseExpiresOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-11)
        });
        var service = new GoogleAnalyticsPurchaseDispatchService(store);

        var lease = await service.TryReserveAsync(PaidOrder());

        Assert.That(lease, Is.Not.Null);
        Assert.That(await service.TryConfirmAsync(42, "old-token"), Is.False);
        Assert.That(await service.TryConfirmAsync(42, lease.Token), Is.True);
        Assert.That(await service.TryReserveAsync(PaidOrder()), Is.Null);
    }

    [Test]
    public async Task LegacyConfirmedRowIsNeverReissued()
    {
        var store = new InMemoryDispatchStore(new GoogleAnalyticsPurchaseDispatch
        {
            Id = 1,
            OrderId = 42,
            Status = "confirmed",
            CreatedOnUtc = DateTime.UtcNow.AddDays(-1)
        });

        Assert.That(await new GoogleAnalyticsPurchaseDispatchService(store).TryReserveAsync(PaidOrder()), Is.Null);
    }

    [Test]
    public void ConfirmationFactoryUsesNamedRouteAndPreservesPathBase()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/licensing-api";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(service => service.GetAndStoreTokens(context))
            .Returns(new AntiforgeryTokenSet("request-token", "cookie-token", "custom-antiforgery-field", "cookie"));
        var links = new CapturingLinkGenerator();
        var factory = new GoogleAnalyticsPurchaseDispatchConfirmationFactory(antiforgery.Object, accessor, links);

        var confirmation = factory.Create();

        Assert.Multiple(() =>
        {
            Assert.That(confirmation.Url, Is.EqualTo("/licensing-api/google-analytics/purchase-dispatch/confirm"));
            Assert.That(confirmation.RequestVerificationToken, Is.EqualTo("request-token"));
            Assert.That(confirmation.RequestVerificationFieldName, Is.EqualTo("custom-antiforgery-field"));
            Assert.That(links.Address?.ToString(), Does.Contain(GoogleAnalyticsDefaults.PurchaseDispatchConfirmationRouteName));
            Assert.That(links.PathBase, Is.EqualTo(new PathString("/licensing-api")));
        });
    }

    [Test]
    public async Task ConfirmationControllerRequiresCustomerOwnershipAndFormCapability()
    {
        var order = PaidOrder();
        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = order.CustomerId });
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        dispatch.Setup(service => service.TryConfirmAsync(order.Id, "token")).ReturnsAsync(true);
        var controller = new GoogleAnalyticsPurchaseDispatchController(orders.Object, dispatch.Object, workContext.Object);

        var result = await controller.Confirm(new PurchaseDispatchConfirmationRequest { OrderId = order.Id, Token = "token" });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(typeof(GoogleAnalyticsPurchaseDispatchController).GetMethod(nameof(controller.Confirm))
                .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true), Is.Not.Empty);
        });
        dispatch.Verify(service => service.TryConfirmAsync(order.Id, "token"), Times.Once);
    }

    [Test]
    public async Task ConfirmationControllerRejectsAnotherCustomersFormCapability()
    {
        var order = PaidOrder();
        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = order.CustomerId + 1 });
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        var controller = new GoogleAnalyticsPurchaseDispatchController(orders.Object, dispatch.Object, workContext.Object);

        Assert.That(await controller.Confirm(new PurchaseDispatchConfirmationRequest { OrderId = order.Id, Token = "token" }),
            Is.TypeOf<NotFoundResult>());
        dispatch.Verify(service => service.TryConfirmAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    private static Order PaidOrder() => new()
    {
        Id = 42,
        CustomerId = 17,
        OrderStatus = OrderStatus.Processing,
        PaymentStatus = PaymentStatus.Paid
    };

    private sealed class CapturingLinkGenerator : LinkGenerator
    {
        public object? Address { get; private set; }
        public PathString? PathBase { get; private set; }

        public override string GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address,
            RouteValueDictionary values, RouteValueDictionary ambientValues, PathString? pathBase,
            FragmentString fragment, LinkOptions options)
        {
            Address = address;
            PathBase = pathBase;
            return $"{pathBase}/google-analytics/purchase-dispatch/confirm";
        }

        public override string GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values,
            PathString pathBase, FragmentString fragment, LinkOptions options)
        {
            Address = address;
            PathBase = pathBase;
            return $"{pathBase}/google-analytics/purchase-dispatch/confirm";
        }

        public override string GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address,
            RouteValueDictionary values, RouteValueDictionary? ambientValues, string? scheme, HostString? host,
            PathString? pathBase, FragmentString fragment, LinkOptions? options)
        {
            return null;
        }

        public override string GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values,
            string scheme, HostString host, PathString pathBase, FragmentString fragment, LinkOptions? options)
        {
            return null;
        }
    }

    private sealed class InMemoryDispatchStore : IGoogleAnalyticsPurchaseDispatchStore
    {
        private readonly object _gate = new();
        private readonly int _initialReadParticipants;
        private readonly TaskCompletionSource _initialReadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private GoogleAnalyticsPurchaseDispatch? _dispatch;
        private int _initialReadCount;
        private int _insertAttempts;

        public InMemoryDispatchStore(GoogleAnalyticsPurchaseDispatch? dispatch = null, int initialReadParticipants = 0)
        {
            _dispatch = dispatch;
            _initialReadParticipants = initialReadParticipants;
        }

        public int InsertAttempts => Volatile.Read(ref _insertAttempts);

        public GoogleAnalyticsPurchaseDispatch Get(int orderId)
        {
            lock (_gate)
                return Copy(_dispatch?.OrderId == orderId ? _dispatch : null);
        }

        public async Task<GoogleAnalyticsPurchaseDispatch> GetByOrderIdAsync(int orderId)
        {
            // Deterministically force the production unique-index/re-read path:
            // every initial contender reads null before any may insert. Later
            // re-reads after a duplicate insert see the durable winner.
            if (_initialReadParticipants > 0 && Interlocked.Increment(ref _initialReadCount) <= _initialReadParticipants)
            {
                if (Volatile.Read(ref _initialReadCount) == _initialReadParticipants)
                    _initialReadBarrier.TrySetResult();
                await _initialReadBarrier.Task;
                return null;
            }

            return Get(orderId);
        }

        public Task InsertAsync(GoogleAnalyticsPurchaseDispatch dispatch)
        {
            Interlocked.Increment(ref _insertAttempts);
            lock (_gate)
            {
                if (_dispatch?.OrderId == dispatch.OrderId)
                    throw new InvalidOperationException("IX_GoogleAnalyticsPurchaseDispatch_OrderId");

                dispatch.Id = 1;
                _dispatch = Copy(dispatch);
            }

            return Task.CompletedTask;
        }

        public Task<int> RenewExpiredLeaseAsync(GoogleAnalyticsPurchaseDispatch existing, string token, DateTime expiresOnUtc)
        {
            lock (_gate)
            {
                if (_dispatch == null || _dispatch.Id != existing.Id || _dispatch.Status != existing.Status ||
                    _dispatch.LeaseToken != existing.LeaseToken || _dispatch.LeaseExpiresOnUtc != existing.LeaseExpiresOnUtc)
                    return Task.FromResult(0);

                _dispatch.Status = "leased";
                _dispatch.LeaseToken = token;
                _dispatch.LeaseExpiresOnUtc = expiresOnUtc;
                _dispatch.ConfirmedOnUtc = null;
                return Task.FromResult(1);
            }
        }

        public Task<int> ConfirmLeaseAsync(int orderId, string token, DateTime confirmedOnUtc)
        {
            lock (_gate)
            {
                if (_dispatch?.OrderId != orderId || _dispatch.Status != "leased" || _dispatch.LeaseToken != token ||
                    _dispatch.LeaseExpiresOnUtc <= confirmedOnUtc)
                    return Task.FromResult(0);

                _dispatch.Status = "confirmed";
                _dispatch.LeaseToken = null;
                _dispatch.LeaseExpiresOnUtc = null;
                _dispatch.ConfirmedOnUtc = confirmedOnUtc;
                return Task.FromResult(1);
            }
        }

        private static GoogleAnalyticsPurchaseDispatch Copy(GoogleAnalyticsPurchaseDispatch source) => source == null ? null : new()
        {
            Id = source.Id,
            OrderId = source.OrderId,
            Status = source.Status,
            LeaseToken = source.LeaseToken,
            LeaseExpiresOnUtc = source.LeaseExpiresOnUtc,
            ConfirmedOnUtc = source.ConfirmedOnUtc,
            CreatedOnUtc = source.CreatedOnUtc
        };
    }
}
