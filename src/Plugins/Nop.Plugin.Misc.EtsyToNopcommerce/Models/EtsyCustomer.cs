using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Models
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Nop.Core;

    public partial class EtsyCustomer : BaseEntity
    {
        public long BuyerUserId { get; set; }
        public string BuyerEmail { get; set; }
        public string BuyerName { get; set; }
        public string OrderedItems { get; set; }
        public string RatingAndReviews { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
    }


}
