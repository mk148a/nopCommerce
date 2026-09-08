using Nop.Core.Domain.Discounts;
using Nop.Services.Discounts;

namespace Nop.Plugin.Misc.Brevo.Services;

/// <summary>
/// Creates bounded, one-use seasonal coupons and optionally prepares a Brevo campaign.
/// This service never sends an email unless that option is explicitly enabled in settings.
/// </summary>
public sealed class SeasonalCampaignService
{
    public const decimal MaximumDiscountPercentage = 15m;

    private readonly BrevoManager _brevoManager;
    private readonly IDiscountService _discountService;

    public SeasonalCampaignService(BrevoManager brevoManager, IDiscountService discountService)
    {
        _brevoManager = brevoManager;
        _discountService = discountService;
    }

    public async Task<SeasonalCampaignResult> PrepareNextCampaignAsync(CampaignAutomationSettings settings,
        bool requireWithinLeadTime, CancellationToken cancellationToken = default)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        var occasion = SeasonalOccasionCalendar.GetNext(DateTime.UtcNow);
        if (requireWithinLeadTime && GetCampaignStartsAtUtc(occasion, settings) > DateTime.UtcNow)
            return SeasonalCampaignResult.NotDue(occasion);

        var marker = $"Brevo seasonal campaign:{occasion.Key}";
        var existing = (await _discountService.GetAllDiscountsAsync(showHidden: true, isActive: null))
            .FirstOrDefault(discount => discount.AdminComment?.StartsWith(marker, StringComparison.Ordinal) == true);
        if (existing is not null)
        {
            await AlignBaseCouponWithCampaignWindowAsync(existing, occasion, settings);

            if (requireWithinLeadTime && settings.ScheduleBrevoEmail)
            {
                var (batchCampaignId, batchError) = await ScheduleDailyQuotaBatchAsync(occasion, existing, settings, cancellationToken);
                return SeasonalCampaignResult.Existing(occasion, existing, batchCampaignId, batchError);
            }

            if (settings.CreateBrevoDraft && !existing.AdminComment.Contains("; Brevo campaign:", StringComparison.Ordinal))
            {
                var (existingCampaignId, existingCampaignError) = await _brevoManager.CreateSeasonalCampaignAsync(occasion, existing, settings, cancellationToken);
                if (existingCampaignId.HasValue)
                {
                    existing.AdminComment = $"{marker}; Brevo campaign:{existingCampaignId.Value}";
                    await _discountService.UpdateDiscountAsync(existing);
                }

                return SeasonalCampaignResult.Existing(occasion, existing, existingCampaignId, existingCampaignError);
            }

            return SeasonalCampaignResult.Existing(occasion, existing);
        }

        var percentage = Math.Min(MaximumDiscountPercentage, Math.Max(0.01m, settings.DiscountPercentage));
        var durationHours = Math.Clamp(settings.CouponDurationHours, 1, 720);
        var prefix = NormalizePrefix(settings.CouponPrefix);
        var startAtUtc = GetCampaignStartsAtUtc(occasion, settings);
        var discount = new Discount
        {
            Name = $"{occasion.Name} – {percentage:0.##}% ({durationHours}h)",
            AdminComment = marker,
            DiscountType = DiscountType.AssignedToOrderTotal,
            UsePercentage = true,
            DiscountPercentage = percentage,
            RequiresCouponCode = true,
            CouponCode = $"{prefix}{occasion.CodeSuffix}",
            IsCumulative = false,
            DiscountLimitation = DiscountLimitationType.NTimesPerCustomer,
            LimitationTimes = 1,
            StartDateUtc = startAtUtc,
            EndDateUtc = startAtUtc.AddHours(durationHours),
            IsActive = true
        };

        await _discountService.InsertDiscountAsync(discount);

        long? campaignId = null;
        string campaignError = null;
        // Manual preparation makes a safe draft. The scheduled task creates an exact daily batch only
        // while the configured lead window is open.
        if (requireWithinLeadTime && settings.ScheduleBrevoEmail)
        {
            (campaignId, campaignError) = await ScheduleDailyQuotaBatchAsync(occasion, discount, settings, cancellationToken);
        }
        else if (settings.CreateBrevoDraft)
        {
            (campaignId, campaignError) = await _brevoManager.CreateSeasonalCampaignAsync(
                occasion, discount, settings, cancellationToken);
        }

        if (campaignId.HasValue && !(requireWithinLeadTime && settings.ScheduleBrevoEmail))
        {
            discount.AdminComment = $"{marker}; Brevo campaign:{campaignId.Value}";
            await _discountService.UpdateDiscountAsync(discount);
        }

        return SeasonalCampaignResult.Created(occasion, discount, campaignId, campaignError);
    }

    /// <summary>
    /// Schedules at most one exact recipient snapshot per UTC day. The live Brevo account balance
    /// determines the batch size; previously scheduled snapshots are read back from the coupon marker
    /// so contacts are never sent the same seasonal offer twice.
    /// </summary>
    private async Task<(long? CampaignId, string Error)> ScheduleDailyQuotaBatchAsync(SeasonalOccasion occasion,
        Discount discount, CampaignAutomationSettings settings, CancellationToken cancellationToken)
    {
        if (settings.BrevoListId <= 0)
            return (null, "Daily quota delivery requires a Brevo consent list ID; segment-only delivery cannot create exact recipient batches.");

        var todayMarker = $"Brevo delivery day:{DateTime.UtcNow:yyyyMMdd}";
        if (discount.AdminComment?.Contains(todayMarker, StringComparison.Ordinal) == true)
            return (null, null);

        cancellationToken.ThrowIfCancellationRequested();
        var (remainingCredits, quotaError) = await _brevoManager.GetRemainingDailyEmailCreditsAsync();
        if (!string.IsNullOrWhiteSpace(quotaError))
            return (null, quotaError);
        if (remainingCredits <= 0)
            return (null, null);

        var (sourceEmails, sourceError) = await _brevoManager.GetEligibleListEmailsAsync(settings.BrevoListId);
        if (!string.IsNullOrWhiteSpace(sourceError))
            return (null, sourceError);

        var alreadyPlanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batchListId in GetBatchListIds(discount.AdminComment))
        {
            var (batchEmails, batchError) = await _brevoManager.GetEligibleListEmailsAsync(batchListId);
            if (!string.IsNullOrWhiteSpace(batchError))
                return (null, batchError);

            alreadyPlanned.UnionWith(batchEmails);
        }

        var recipients = sourceEmails.Where(email => !alreadyPlanned.Contains(email))
            .Take(remainingCredits)
            .ToList();
        if (recipients.Count == 0)
            return (null, null);

        var batchName = $"Hood seasonal {occasion.Key} {DateTime.UtcNow:yyyyMMdd}";
        var (recipientListId, recipientListError) = await _brevoManager.CreateRecipientListAsync(batchName, recipients);
        if (!recipientListId.HasValue)
            return (null, recipientListError);

        // A small delay gives Brevo time to index the new static list before it evaluates recipients.
        var scheduledAtUtc = DateTime.UtcNow.AddMinutes(10);
        var batchDiscount = await GetOrCreateDailyBatchCouponAsync(occasion, settings, scheduledAtUtc);
        var (exclusionListId, exclusionListError) = await _brevoManager.EnsureSeasonalExclusionListAsync(settings);
        if (!exclusionListId.HasValue)
            return (null, exclusionListError);
        var (campaignId, campaignError) = await _brevoManager.CreateSeasonalCampaignAsync(occasion, batchDiscount,
            settings, recipientListId, exclusionListId, scheduledAtUtc, $"batch {DateTime.UtcNow:yyyyMMdd} ({recipients.Count} recipients)", cancellationToken);
        if (!campaignId.HasValue)
            return (null, campaignError);

        discount.AdminComment = $"{discount.AdminComment}; Brevo batch list:{recipientListId.Value}; {todayMarker}";
        await _discountService.UpdateDiscountAsync(discount);
        return (campaignId, null);
    }

    /// <summary>
    /// A month-long campaign can contain several daily recipient batches. A single 72-hour code would
    /// expire before later batches receive it, so each UTC batch gets a separate, one-use code whose
    /// validity starts when its email is scheduled.
    /// </summary>
    private async Task<Discount> GetOrCreateDailyBatchCouponAsync(SeasonalOccasion occasion,
        CampaignAutomationSettings settings, DateTime startsAtUtc)
    {
        var day = startsAtUtc.ToUniversalTime().ToString("yyyyMMdd");
        var marker = $"Brevo seasonal batch coupon:{occasion.Key}:{day}";
        var existing = (await _discountService.GetAllDiscountsAsync(showHidden: true, isActive: null))
            .FirstOrDefault(discount => string.Equals(discount.AdminComment, marker, StringComparison.Ordinal));
        if (existing is not null)
            return existing;

        var percentage = Math.Min(MaximumDiscountPercentage, Math.Max(0.01m, settings.DiscountPercentage));
        var durationHours = Math.Clamp(settings.CouponDurationHours, 1, 720);
        var prefix = NormalizePrefix(settings.CouponPrefix);
        var discount = new Discount
        {
            Name = $"{occasion.Name} – daily batch – {percentage:0.##}% ({durationHours}h)",
            AdminComment = marker,
            DiscountType = DiscountType.AssignedToOrderTotal,
            UsePercentage = true,
            DiscountPercentage = percentage,
            RequiresCouponCode = true,
            CouponCode = $"{prefix}{occasion.CodeSuffix}{startsAtUtc:MMdd}",
            IsCumulative = false,
            DiscountLimitation = DiscountLimitationType.NTimesPerCustomer,
            LimitationTimes = 1,
            StartDateUtc = startsAtUtc,
            EndDateUtc = startsAtUtc.AddHours(durationHours),
            IsActive = true
        };

        await _discountService.InsertDiscountAsync(discount);
        return discount;
    }

    private async Task AlignBaseCouponWithCampaignWindowAsync(Discount discount, SeasonalOccasion occasion,
        CampaignAutomationSettings settings)
    {
        var startAtUtc = GetCampaignStartsAtUtc(occasion, settings);
        var endAtUtc = startAtUtc.AddHours(Math.Clamp(settings.CouponDurationHours, 1, 720));
        if (discount.StartDateUtc == startAtUtc && discount.EndDateUtc == endAtUtc)
            return;

        discount.StartDateUtc = startAtUtc;
        discount.EndDateUtc = endAtUtc;
        await _discountService.UpdateDiscountAsync(discount);
    }

    private static DateTime GetCampaignStartsAtUtc(SeasonalOccasion occasion, CampaignAutomationSettings settings)
    {
        if (settings.CampaignStartOverrideUtc.HasValue &&
            string.Equals(settings.CampaignStartOverrideOccasionKey, occasion.Key, StringComparison.Ordinal))
            return DateTime.SpecifyKind(settings.CampaignStartOverrideUtc.Value, DateTimeKind.Utc);

        return occasion.StartsAtUtc.AddDays(-Math.Clamp(settings.LeadTimeDays, 0, 90));
    }

    private static IEnumerable<long> GetBatchListIds(string adminComment)
    {
        const string prefix = "Brevo batch list:";
        return (adminComment ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.StartsWith(prefix, StringComparison.Ordinal))
            .Select(part => long.TryParse(part[prefix.Length..], out var value) ? value : 0)
            .Where(value => value > 0);
    }

    private static string NormalizePrefix(string value)
    {
        var lettersAndNumbers = new string((value ?? string.Empty).ToUpperInvariant()
            .Where(char.IsLetterOrDigit).Take(12).ToArray());
        return string.IsNullOrEmpty(lettersAndNumbers) ? "HOOD" : lettersAndNumbers;
    }
}

public sealed record SeasonalCampaignResult(SeasonalOccasion Occasion, Discount Discount, bool IsDue,
    bool WasCreated, long? BrevoCampaignId, string BrevoCampaignError)
{
    public static SeasonalCampaignResult NotDue(SeasonalOccasion occasion) => new(occasion, null, false, false, null, null);
    public static SeasonalCampaignResult Existing(SeasonalOccasion occasion, Discount discount, long? campaignId = null, string error = null) => new(occasion, discount, true, false, campaignId, error);
    public static SeasonalCampaignResult Created(SeasonalOccasion occasion, Discount discount, long? campaignId, string error) => new(occasion, discount, true, true, campaignId, error);
}

public sealed record SeasonalOccasion(string Key, string Name, string CodeSuffix, DateTime StartsAtUtc);

/// <summary>
/// A deliberately small, global retail calendar. Region-specific occasions remain manual
/// because their dates and marketing permissions differ by market.
/// </summary>
public static class SeasonalOccasionCalendar
{
    public static SeasonalOccasion GetNext(DateTime utcNow)
    {
        var candidates = new List<SeasonalOccasion>();
        foreach (var year in new[] { utcNow.Year, utcNow.Year + 1 })
        {
            candidates.Add(At("new-year", "New Year", "NY" + year.ToString()[^2..], year, 1, 1));
            candidates.Add(At("valentines-day", "Valentine's Day", "VD" + year.ToString()[^2..], year, 2, 14));
            candidates.Add(At("easter", "Easter", "ES" + year.ToString()[^2..], EasterSunday(year)));
            candidates.Add(At("halloween", "Halloween", "HW" + year.ToString()[^2..], year, 10, 31));
            var blackFriday = Thanksgiving(year).AddDays(1);
            candidates.Add(At("black-friday", "Black Friday", "BF" + year.ToString()[^2..], blackFriday));
            candidates.Add(At("cyber-monday", "Cyber Monday", "CM" + year.ToString()[^2..], blackFriday.AddDays(3)));
            candidates.Add(At("christmas", "Christmas", "XM" + year.ToString()[^2..], year, 12, 25));
        }

        return candidates.Where(occasion => occasion.StartsAtUtc > utcNow).OrderBy(occasion => occasion.StartsAtUtc).First();
    }

    private static SeasonalOccasion At(string key, string name, string suffix, int year, int month, int day) =>
        At(key, name, suffix, new DateTime(year, month, day));

    private static SeasonalOccasion At(string key, string name, string suffix, DateTime date) =>
        new(key + "-" + date.Year, name, suffix, DateTime.SpecifyKind(date.Date.AddHours(9), DateTimeKind.Utc));

    private static DateTime Thanksgiving(int year)
    {
        var novemberFirst = new DateTime(year, 11, 1);
        var firstThursdayOffset = ((int)DayOfWeek.Thursday - (int)novemberFirst.DayOfWeek + 7) % 7;
        return novemberFirst.AddDays(firstThursdayOffset + 21);
    }

    private static DateTime EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }
}
