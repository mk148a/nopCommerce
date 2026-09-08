using Nop.Core;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Domain;

/// <summary>
/// Google Bot müşteri entity'si
/// </summary>
public class GoogleBotCustomer : BaseEntity
{
    /// <summary>
    /// Gets or sets the customer identifier
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the user agent
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the IP address
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the created on UTC
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
} 