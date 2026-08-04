namespace Nop.Plugin.Misc.EtsyToNopcommerce.Domains
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Transaction
    {
        [JsonProperty("transaction_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? TransactionId { get; set; }

        [JsonProperty("buyer_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? BuyerUserId { get; set; }

        [JsonProperty("receipt_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ReceiptId { get; set; }

        [JsonProperty("listing_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ListingId { get; set; }

        [JsonProperty("sku", NullValueHandling = NullValueHandling.Ignore)]
        public string Sku { get; set; }

        [JsonProperty("product_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ProductId { get; set; }
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }
    }
}