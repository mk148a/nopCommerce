using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Controllers;
using Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework.Mvc.Filters;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
[NonParallelizable]
public class StripeBnplCostReportTests : BaseNopTest
{
    [OneTimeSetUp]
    public async Task EnsurePaymentRecordSchema()
    {
        var dataProvider = GetService<INopDataProvider>();
        await dataProvider.ExecuteNonQueryAsync("""
            CREATE TABLE IF NOT EXISTS StripeBnplPaymentRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProviderId INTEGER NOT NULL,
                SessionId TEXT NULL,
                PaymentIntentId TEXT NULL,
                ChargeId TEXT NULL,
                BalanceTransactionId TEXT NULL,
                PaymentMethodType TEXT NULL,
                Status TEXT NOT NULL,
                AmountMinor INTEGER NOT NULL,
                FeeMinor INTEGER NULL,
                NetMinor INTEGER NULL,
                RefundedAmountMinor INTEGER NOT NULL,
                PendingRefundAmountMinor INTEGER NULL,
                RefundClaimedOnUtc TEXT NULL,
                Currency TEXT NOT NULL,
                SettlementCurrency TEXT NULL,
                ExchangeRate NUMERIC NULL,
                FeeDetailsJson TEXT NULL,
                FeeDataStatus TEXT NOT NULL,
                FeeDataError TEXT NULL,
                FeeReconciliationAttemptCount INTEGER NOT NULL,
                FeeLastAttemptOnUtc TEXT NULL,
                FeeCompletedOnUtc TEXT NULL,
                IsSandbox INTEGER NOT NULL,
                CreatedOnUtc TEXT NOT NULL,
                UpdatedOnUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Test_StripeBnplPaymentRecord_OrderId
                ON StripeBnplPaymentRecord(OrderId);
            """);
    }

    [Test]
    public void ReportRequiresAdministratorAndPaymentMethodPermission()
    {
        var controllerType = typeof(StripeBnplCostReportController);
        var action = controllerType.GetMethod(nameof(StripeBnplCostReportController.Index));
        var permission = action!.GetCustomAttributes<CheckPermissionAttribute>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(controllerType.GetCustomAttributes<AuthorizeAdminAttribute>(), Is.Not.Empty);
            Assert.That(permission.PermissionSystemName,
                Does.Contain(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS));
        });
    }

    [Test]
    public async Task IndexUsesNormalizedQueryAndBuildsReportModel()
    {
        StripeBnplCostReportQuery captured = null;
        var record = CreateRecord(CreatePositiveId(), BnplProvider.Klarna, true,
            StripeBnplFeeDataStatus.Complete, DateTime.UtcNow);
        var store = new Mock<IStripeBnplPaymentRecordStore>();
        store.Setup(service => service.SearchCostReportAsync(It.IsAny<StripeBnplCostReportQuery>()))
            .Callback<StripeBnplCostReportQuery>(query => captured = query)
            .ReturnsAsync(new StripeBnplCostReportPage(new[] { record }, 1));
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrdersByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new List<Order>
            {
                new() { Id = record.OrderId, CustomOrderNumber = "BNPL-1001" }
            });
        var controller = new StripeBnplCostReportController(orderService.Object, store.Object);

        var result = await controller.Index(new StripeBnplCostReportModel
        {
            ProviderId = (int)BnplProvider.Klarna,
            IsSandbox = true,
            FeeDataStatus = " COMPLETE ",
            PageNumber = -10,
            PageSize = 999
        });

        var view = result as ViewResult;
        var model = view?.Model as StripeBnplCostReportModel;
        Assert.Multiple(() =>
        {
            Assert.That(view?.ViewName, Is.EqualTo(StripeBnplCostReportController.CostReportViewPath));
            Assert.That(captured, Is.EqualTo(new StripeBnplCostReportQuery(null, BnplProvider.Klarna,
                true, StripeBnplFeeDataStatus.Complete, 0, 100)));
            Assert.That(model?.Rows, Has.Count.EqualTo(1));
            Assert.That(model?.Rows[0].OrderNumber, Is.EqualTo("BNPL-1001"));
            Assert.That(model?.Rows[0].Fee, Is.EqualTo("6.5 USD"));
            Assert.That(model?.Rows[0].Net, Is.EqualTo("93.5 USD"));
        });
    }

    [Test]
    public async Task StoreFiltersEnvironmentProviderAndStatusBeforePaging()
    {
        var repository = GetService<IRepository<StripeBnplPaymentRecord>>();
        var seed = CreatePositiveId();
        var now = DateTime.UtcNow;
        var records = new[]
        {
            CreateRecord(seed, BnplProvider.Klarna, true, StripeBnplFeeDataStatus.Complete, now),
            CreateRecord(seed + 1, BnplProvider.Klarna, true, StripeBnplFeeDataStatus.Complete, now.AddMinutes(-1)),
            CreateRecord(seed + 2, BnplProvider.Klarna, false, StripeBnplFeeDataStatus.Complete, now.AddMinutes(-2)),
            CreateRecord(seed + 3, BnplProvider.Affirm, true, StripeBnplFeeDataStatus.Complete, now.AddMinutes(-3)),
            CreateRecord(seed + 4, BnplProvider.Klarna, true, StripeBnplFeeDataStatus.Pending, now.AddMinutes(-4))
        };

        try
        {
            await repository.InsertAsync(records, publishEvent: false);
            var page = await new StripeBnplPaymentRecordStore(repository).SearchCostReportAsync(
                new StripeBnplCostReportQuery(null, BnplProvider.Klarna, true,
                    StripeBnplFeeDataStatus.Complete, 1, 1));

            Assert.Multiple(() =>
            {
                Assert.That(page.TotalCount, Is.EqualTo(2));
                Assert.That(page.Records, Has.Count.EqualTo(1));
                Assert.That(page.Records[0].OrderId, Is.EqualTo(seed + 1));
            });
        }
        finally
        {
            var orderIds = records.Select(record => record.OrderId).ToArray();
            var persisted = await repository.GetAllAsync(query => query.Where(item => orderIds.Contains(item.OrderId)));
            if (persisted.Count > 0)
                await repository.DeleteAsync(persisted, publishEvent: false);
        }
    }

    [Test]
    public void RowModelParsesOnlyKnownFeeFieldsAndMalformedJsonFailsClosed()
    {
        var record = CreateRecord(101, BnplProvider.Afterpay, true,
            StripeBnplFeeDataStatus.Complete, DateTime.UtcNow);
        record.FeeDetailsJson = """[{"Amount":650,"Currency":"usd","Type":"stripe_fee","Description":"Processing <script>"}]""";

        var row = StripeBnplCostReportController.CreateRowModel(record, "TEST-101");
        var malformed = StripeBnplCostReportController.ParseFeeDetails("{not-json");

        Assert.Multiple(() =>
        {
            Assert.That(row.Amount, Is.EqualTo("100 USD"));
            Assert.That(row.FeeDetails, Has.Count.EqualTo(1));
            Assert.That(row.FeeDetails[0].Amount, Is.EqualTo("6.5 USD"));
            Assert.That(row.FeeDetails[0].Type, Is.EqualTo("stripe_fee"));
            Assert.That(row.FeeDetails[0].Description, Is.EqualTo("Processing <script>"));
            Assert.That(malformed, Is.Empty);
        });
    }

    [Test]
    public void RazorReportRendersSafeStructuredFieldsAndNoRawFeeJson()
    {
        var source = ReadRepositoryFile("src", "Plugins", "Nop.Plugin.Misc.StripeBnplCore", "Areas",
            "Admin", "Views", "StripeBnplCostReport", "Index.cshtml");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("@row.Fee"));
            Assert.That(source, Does.Contain("@row.Net"));
            Assert.That(source, Does.Contain("@row.ExchangeRate"));
            Assert.That(source, Does.Contain("@detail.Description"));
            Assert.That(source, Does.Contain("asp-controller=\"Order\""));
            Assert.That(source, Does.Not.Contain("FeeDetailsJson"));
            Assert.That(source, Does.Not.Contain("Html.Raw"));
            Assert.That(source, Does.Not.Contain("RestrictedKey"));
            Assert.That(source, Does.Not.Contain("WebhookSecret"));
        });
    }

    private static StripeBnplPaymentRecord CreateRecord(int orderId, BnplProvider provider, bool isSandbox,
        string feeDataStatus, DateTime createdOnUtc) => new()
    {
        OrderId = orderId,
        Provider = provider,
        SessionId = $"cs_{orderId}",
        PaymentIntentId = $"pi_{orderId}",
        ChargeId = $"ch_{orderId}",
        BalanceTransactionId = feeDataStatus == StripeBnplFeeDataStatus.Complete ? $"txn_{orderId}" : null,
        PaymentMethodType = StripeBnplDefaults.GetProviderSlug(provider),
        Status = "paid",
        AmountMinor = 10_000,
        FeeMinor = feeDataStatus == StripeBnplFeeDataStatus.Complete ? 650 : null,
        NetMinor = feeDataStatus == StripeBnplFeeDataStatus.Complete ? 9_350 : null,
        RefundedAmountMinor = 0,
        Currency = "usd",
        SettlementCurrency = feeDataStatus == StripeBnplFeeDataStatus.Complete ? "usd" : null,
        ExchangeRate = feeDataStatus == StripeBnplFeeDataStatus.Complete ? 1m : null,
        FeeDataStatus = feeDataStatus,
        FeeReconciliationAttemptCount = feeDataStatus == StripeBnplFeeDataStatus.Complete ? 1 : 0,
        FeeCompletedOnUtc = feeDataStatus == StripeBnplFeeDataStatus.Complete ? createdOnUtc : null,
        IsSandbox = isSandbox,
        CreatedOnUtc = createdOnUtc,
        UpdatedOnUtc = createdOnUtc
    };

    private static int CreatePositiveId()
    {
        var value = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
        return value < 100_000 ? value + 100_000 : value;
    }

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".git")) &&
               !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;

        Assert.That(directory, Is.Not.Null, "The source contract test must run from a repository checkout.");
        var path = Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
        Assert.That(File.Exists(path), Is.True, $"Missing report view: {path}");
        return File.ReadAllText(path);
    }
}
