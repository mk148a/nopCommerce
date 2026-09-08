namespace Nop.Plugin.Misc.EtsyToNopcommerce.Domains
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Receipt
    {
        [JsonProperty("receipt_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ReceiptId { get; set; }

        [JsonProperty("buyer_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? BuyerUserId { get; set; }

        [JsonProperty("buyer_email", NullValueHandling = NullValueHandling.Ignore)]
        public string BuyerEmail { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("first_line", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstLine { get; set; }

        [JsonProperty("second_line", NullValueHandling = NullValueHandling.Ignore)]
        public string SecondLine { get; set; }

        [JsonProperty("city", NullValueHandling = NullValueHandling.Ignore)]
        public string City { get; set; }

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        public string State { get; set; }

        [JsonProperty("zip", NullValueHandling = NullValueHandling.Ignore)]
        public string Zip { get; set; }

        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("formatted_address", NullValueHandling = NullValueHandling.Ignore)]
        public string FormattedAddress { get; set; }

        [JsonProperty("country_iso", NullValueHandling = NullValueHandling.Ignore)]
        public string CountryIso { get; set; }

        [JsonProperty("payment_method", NullValueHandling = NullValueHandling.Ignore)]
        public string PaymentMethod { get; set; }

        [JsonProperty("transactions", NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<Transaction> Transactions { get; set; }

    }
}