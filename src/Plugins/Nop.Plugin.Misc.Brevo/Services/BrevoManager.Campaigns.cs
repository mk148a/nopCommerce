using System.Net;
using System.Text;
using brevo_csharp.Api;
using brevo_csharp.Model;
using Nop.Core.Domain.Discounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nop.Plugin.Misc.Brevo.Services;

public partial class BrevoManager
{
    /// <summary>
    /// Creates a responsive Brevo campaign. A configured Brevo template is preferred, but the
    /// plugin also supplies a safe default so seasonal campaigns never depend on manual markup.
    /// With no scheduled date the result remains a draft in Brevo.
    /// </summary>
    public Task<(long? CampaignId, string Error)> CreateSeasonalCampaignAsync(SeasonalOccasion occasion,
        Discount discount, CampaignAutomationSettings settings, CancellationToken cancellationToken = default) =>
        CreateSeasonalCampaignAsync(occasion, discount, settings, null, null, null, null, cancellationToken);

    /// <summary>
    /// Creates a draft, or schedules one already bounded recipient batch. The public draft overload
    /// deliberately cannot schedule mail: only the daily quota planner passes a recipient list and
    /// a send time.
    /// </summary>
    public async Task<(long? CampaignId, string Error)> CreateSeasonalCampaignAsync(SeasonalOccasion occasion,
        Discount discount, CampaignAutomationSettings settings, long? recipientListId, long? exclusionListId, DateTime? scheduledAtUtc,
        string batchSuffix, CancellationToken cancellationToken = default)
    {
        if (settings.BrevoSenderId <= 0 || (!recipientListId.HasValue && settings.BrevoSegmentId <= 0 && settings.BrevoListId <= 0))
            return (null, "Brevo list or segment and sender IDs must be configured before a campaign can be prepared.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Draft creation intentionally omits recipients. Only the quota planner supplies a bounded
            // batch and a near-term schedule, preventing a manual preparation from becoming a send.
            var scheduleCampaign = recipientListId.HasValue && scheduledAtUtc.HasValue;
            if (scheduleCampaign)
                return await CreateScheduledSeasonalCampaignAsync(occasion, discount, settings, recipientListId.Value,
                    exclusionListId, scheduledAtUtc.Value, batchSuffix, cancellationToken);

            var client = await CreateApiClientAsync(configuration => new EmailCampaignsApi(configuration));
            var recipients = scheduleCampaign
                ? new CreateEmailCampaignRecipients(
                    exclusionListIds: exclusionListId.HasValue ? new List<long?> { exclusionListId.Value } : new List<long?>(),
                    listIds: new List<long?> { recipientListId.Value },
                    segmentIds: new List<long?>())
                : null;
            var campaign = new CreateEmailCampaign(
                // Some Brevo accounts do not have the optional campaign-tag feature enabled.
                // Omitting it keeps campaign creation portable; the name and UTM campaign remain
                // stable, human-readable identifiers for reporting and de-duplication.
                tag: null,
                sender: new CreateEmailCampaignSender(id: settings.BrevoSenderId),
                name: $"Hood Archery | {occasion.Name} | {discount.CouponCode}" +
                    (string.IsNullOrWhiteSpace(batchSuffix) ? string.Empty : $" | {batchSuffix}"),
                htmlContent: settings.BrevoTemplateId > 0 ? null : BuildDefaultSeasonalHtml(occasion, discount),
                htmlUrl: null,
                templateId: settings.BrevoTemplateId > 0 ? settings.BrevoTemplateId : null,
                scheduledAt: scheduleCampaign ? scheduledAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00") : null,
                subject: $"{occasion.Name}: {discount.DiscountPercentage:0.##}% off for a limited time",
                previewText: $"Use code {discount.CouponCode} before it expires.",
                replyTo: null,
                toField: null,
                recipients: recipients,
                attachmentUrl: null,
                inlineImageActivation: false,
                mirrorActive: true,
                footer: null,
                header: null,
                utmCampaign: $"seasonal-{occasion.Key}",
                _params: new
                {
                    COUPON_CODE = discount.CouponCode,
                    DISCOUNT_PERCENTAGE = discount.DiscountPercentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    COUPON_ENDS_AT_UTC = discount.EndDateUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'")
                },
                sendAtBestTime: false,
                abTesting: false,
                subjectA: null,
                subjectB: null,
                splitRule: null,
                winnerCriteria: null,
                winnerDelay: null,
                ipWarmupEnable: false,
                initialQuota: null,
                increaseRate: null,
                unsubscriptionPageId: null,
                updateFormId: null);

            var result = await client.CreateEmailCampaignAsync(campaign);
            return (result?.Id, null);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"Brevo seasonal campaign error for '{occasion.Key}'", exception,
                await _workContext.GetCurrentCustomerAsync());
            return (null, exception.Message);
        }
    }

    /// <summary>
    /// Uses Brevo's current HTTP contract for scheduled batches. The legacy SDK serializes empty
    /// recipient arrays that the live API rejects; this request intentionally sends only the two
    /// applicable recipient-list properties.
    /// </summary>
    private async Task<(long? CampaignId, string Error)> CreateScheduledSeasonalCampaignAsync(SeasonalOccasion occasion,
        Discount discount, CampaignAutomationSettings settings, long recipientListId, long? exclusionListId,
        DateTime scheduledAtUtc, string batchSuffix, CancellationToken cancellationToken)
    {
        if (!exclusionListId.HasValue)
            return (null, "Brevo requires an exclusion-list ID for a scheduled campaign.");

        var brevoSettings = await _settingService.LoadSettingAsync<BrevoSettings>();
        if (string.IsNullOrWhiteSpace(brevoSettings.ApiKey))
            return (null, "Brevo API key is not configured.");

        var payload = new JObject
        {
            ["name"] = $"Hood Archery | {occasion.Name} | {discount.CouponCode} | {batchSuffix}",
            ["sender"] = new JObject
            {
                ["id"] = settings.BrevoSenderId
            },
            ["subject"] = $"{occasion.Name}: {discount.DiscountPercentage:0.##}% off for a limited time",
            ["previewText"] = $"Use code {discount.CouponCode} before it expires.",
            ["scheduledAt"] = scheduledAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00"),
            ["recipients"] = new JObject
            {
                ["listIds"] = new JArray(recipientListId),
                ["exclusionListIds"] = new JArray(exclusionListId.Value)
            },
            ["inlineImageActivation"] = false,
            ["mirrorActive"] = true,
            ["utmCampaign"] = $"seasonal-{occasion.Key}",
            ["params"] = new JObject
            {
                ["COUPON_CODE"] = discount.CouponCode,
                ["DISCOUNT_PERCENTAGE"] = discount.DiscountPercentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                ["COUPON_ENDS_AT_UTC"] = discount.EndDateUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'")
            }
        };

        if (settings.BrevoTemplateId > 0)
            payload["templateId"] = settings.BrevoTemplateId;
        else
            payload["htmlContent"] = BuildDefaultSeasonalHtml(occasion, discount);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        httpClient.DefaultRequestHeaders.Add(BrevoDefaults.ApiKeyHeader, brevoSettings.ApiKey);
        using var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("https://api.brevo.com/v3/emailCampaigns", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (null, $"Brevo scheduled campaign request failed ({(int)response.StatusCode}): {responseBody}");

        var campaignId = JObject.Parse(responseBody).Value<long?>("id");
        return campaignId.HasValue
            ? (campaignId.Value, null)
            : (null, "Brevo did not return a scheduled campaign ID.");
    }

    private string BuildDefaultSeasonalHtml(SeasonalOccasion occasion, Discount discount)
    {
        var code = WebUtility.HtmlEncode(discount.CouponCode);
        var occasionName = WebUtility.HtmlEncode(occasion.Name);
        var percentage = discount.DiscountPercentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var storeUrl = WebUtility.HtmlEncode(_webHelper.GetStoreLocation().TrimEnd('/') +
            $"/?utm_source=brevo&utm_medium=email&utm_campaign=seasonal-{occasion.Key}");

        return $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#f5f3ef;font-family:Arial,Helvetica,sans-serif;color:#213027;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f5f3ef"><tr><td align="center" style="padding:24px 12px">
                <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden">
                  <tr><td style="background:#123c2b;padding:24px 36px;text-align:center;color:#fff;font-size:22px;font-weight:bold;letter-spacing:.4px">HOOD ARCHERY SHOP</td></tr>
                  <tr><td style="padding:40px 36px;text-align:center">
                    <p style="margin:0 0 12px;color:#756b5d;font-size:13px;text-transform:uppercase;letter-spacing:1.4px">Seasonal offer</p>
                    <h1 style="margin:0 0 16px;font-size:30px;line-height:1.2;color:#213027">{occasionName} savings for archers</h1>
                    <p style="margin:0 auto 24px;max-width:440px;font-size:16px;line-height:1.55;color:#4d584f">Thank you for being part of Hood Archery Shop. Enjoy {percentage}% off your next order for a limited time.</p>
                    <div style="margin:0 auto 24px;padding:15px 20px;max-width:300px;border:2px dashed #16855f;border-radius:8px;color:#123c2b;font-size:22px;font-weight:bold;letter-spacing:2px">{code}</div>
                    <p style="margin:0 0 28px;font-size:13px;line-height:1.5;color:#756b5d">One use per customer. Your code expires 72 hours after this offer begins.</p>
                    <a href="{storeUrl}" style="display:inline-block;background:#16855f;color:#fff;text-decoration:none;border-radius:5px;padding:14px 26px;font-weight:bold">SHOP THE COLLECTION</a>
                  </td></tr>
                  <tr><td style="padding:20px 36px;background:#f8f8f6;text-align:center;color:#756b5d;font-size:12px;line-height:1.5">You are receiving this offer because you have marketing consent with Hood Archery Shop. Manage preferences or unsubscribe using the links in this email.</td></tr>
                </table>
              </td></tr></table>
            </body></html>
            """;
    }

    /// <summary>
    /// Reads the current account send limit instead of relying on a copied number from the Brevo UI.
    /// Brevo exposes this as the active <c>sendLimit</c> credit balance in <c>/v3/account</c>.
    /// </summary>
    public async Task<(int RemainingCredits, string Error)> GetRemainingDailyEmailCreditsAsync()
    {
        try
        {
            var client = await CreateApiClientAsync(configuration => new AccountApi(configuration));
            var account = await client.GetAccountAsync();
            var credits = account?.Plan?
                .Where(plan => string.Equals(plan.CreditsType.ToString(), "SendLimit", StringComparison.OrdinalIgnoreCase))
                .Select(plan => plan.Credits ?? 0)
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            return (Math.Max(0, (int)Math.Floor(credits)), null);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Brevo seasonal quota lookup failed", exception,
                await _workContext.GetCurrentCustomerAsync());
            return (0, exception.Message);
        }
    }

    /// <summary>
    /// Returns active, deliverable addresses from a consent list. No addresses are logged.
    /// </summary>
    public async Task<(IList<string> Emails, string Error)> GetEligibleListEmailsAsync(long listId)
    {
        try
        {
            var client = await CreateApiClientAsync(configuration => new ContactsApi(configuration));
            const long pageSize = 500;
            long offset = 0;
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var template = new { contacts = new[] { new { email = string.Empty, emailBlacklisted = false } } };

            while (true)
            {
                var contacts = await client.GetContactsFromListAsync(listId, null, pageSize, offset, "asc");
                var page = JsonConvert.DeserializeAnonymousType(contacts.ToJson(), template)?.contacts ?? [];
                foreach (var contact in page)
                {
                    if (!contact.emailBlacklisted && !string.IsNullOrWhiteSpace(contact.email))
                        emails.Add(contact.email.Trim());
                }

                if (page.Length < pageSize)
                    break;

                offset += page.Length;
            }

            return (emails.ToList(), null);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Brevo seasonal contact-list lookup failed", exception,
                await _workContext.GetCurrentCustomerAsync());
            return ([], exception.Message);
        }
    }

    /// <summary>
    /// Creates a private static list in the first existing Brevo folder and adds already-consented
    /// contacts to it. The list becomes the exact recipient snapshot for one daily batch.
    /// </summary>
    public async Task<(long? ListId, string Error)> CreateRecipientListAsync(string name, IList<string> emails)
    {
        if (emails is null || emails.Count == 0)
            return (null, "The recipient batch is empty.");

        try
        {
            var client = await CreateApiClientAsync(configuration => new ContactsApi(configuration));
            var folders = await client.GetFoldersAsync(50, 0, "asc");
            var folderTemplate = new { folders = new[] { new { id = 0L } } };
            var folderId = JsonConvert.DeserializeAnonymousType(folders.ToJson(), folderTemplate)?.folders
                .Select(folder => folder.id)
                .FirstOrDefault();
            if (folderId <= 0)
                return (null, "Brevo does not have a contact folder for recipient batch lists.");

            var list = await client.CreateListAsync(new CreateList(name, folderId));
            if (!list?.Id.HasValue ?? true)
                return (null, "Brevo did not return a recipient batch list ID.");

            foreach (var emailBatch in emails.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(150))
            {
                await client.AddContactToListAsync(list.Id.Value,
                    new AddContactToList(emailBatch.ToList(), new List<long?>()));
            }
            return (list.Id.Value, null);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Brevo seasonal recipient-list creation failed", exception,
                await _workContext.GetCurrentCustomerAsync());
            return (null, exception.Message);
        }
    }

    /// <summary>
    /// Brevo requires an explicit exclusion-list ID when a campaign is scheduled, even when no
    /// contacts must be excluded. A single empty list is created once and persisted in settings.
    /// </summary>
    public async Task<(long? ListId, string Error)> EnsureSeasonalExclusionListAsync(CampaignAutomationSettings settings)
    {
        if (settings.BrevoExclusionListId > 0)
            return (settings.BrevoExclusionListId, null);

        try
        {
            var client = await CreateApiClientAsync(configuration => new ContactsApi(configuration));
            var folders = await client.GetFoldersAsync(50, 0, "asc");
            var folderTemplate = new { folders = new[] { new { id = 0L } } };
            var folderId = JsonConvert.DeserializeAnonymousType(folders.ToJson(), folderTemplate)?.folders
                .Select(folder => folder.id)
                .FirstOrDefault();
            if (folderId <= 0)
                return (null, "Brevo does not have a contact folder for the campaign exclusion list.");

            var list = await client.CreateListAsync(new CreateList("Hood seasonal campaign exclusions", folderId));
            if (!list?.Id.HasValue ?? true)
                return (null, "Brevo did not return an exclusion-list ID.");

            settings.BrevoExclusionListId = list.Id.Value;
            await _settingService.SaveSettingAsync(settings);
            return (list.Id.Value, null);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Brevo seasonal exclusion-list creation failed", exception,
                await _workContext.GetCurrentCustomerAsync());
            return (null, exception.Message);
        }
    }

    public async Task<string> AddEmailsToListAsync(long listId, IList<string> emails)
    {
        if (emails is null || emails.Count == 0)
            return null;

        try
        {
            var client = await CreateApiClientAsync(configuration => new ContactsApi(configuration));
            foreach (var emailBatch in emails.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(150))
            {
                await client.AddContactToListAsync(listId,
                    new AddContactToList(emailBatch.ToList(), new List<long?>()));
            }
            return null;
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("Brevo seasonal delivery-ledger update failed", exception,
                await _workContext.GetCurrentCustomerAsync());
            return exception.Message;
        }
    }
}
