namespace Nop.Plugin.Misc.EtsyToNopcommerce.Models.Receipts
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;

    

    public partial class Receipts
    {
        [JsonProperty("count", NullValueHandling = NullValueHandling.Ignore)]
        public long? Count { get; set; }

        [JsonProperty("results", NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<Receipt> Results { get; set; }
    }
}