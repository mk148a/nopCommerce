using System.ComponentModel.DataAnnotations;
using Nop.Core;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Domains
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Nop.Plugin.Misc.EtsyToNopcommerce.Services;

 
    public class EtsyReview:BaseEntity
    {
        private readonly IProductReviewsTransactionsMappingService _productReviewsTransactionsMappingService;
        public EtsyReview()
        {
            
        }
        public EtsyReview(IProductReviewsTransactionsMappingService productReviewsTransactionsMappingService)
        {
            this._productReviewsTransactionsMappingService = productReviewsTransactionsMappingService;
        }
        [JsonProperty("shop_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ShopId { get; set; }

        [JsonProperty("listing_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ListingId { get; set; }

        
        [JsonProperty("transaction_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? TransactionId { get; set; }

        [JsonProperty("buyer_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? BuyerUserId { get; set; }

        [JsonProperty("rating", NullValueHandling = NullValueHandling.Ignore)]
        public int? Rating { get; set; }

        [JsonProperty("review", NullValueHandling = NullValueHandling.Ignore)]
        public string Review { get; set; }

        [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

        [JsonProperty("image_url_fullxfull")]
        public string ImageUrlFullxfull { get; set; }

        [JsonProperty("create_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreateTimestamp { get; set; }

        [JsonProperty("created_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreatedTimestamp { get; set; }

        [JsonProperty("update_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? UpdateTimestamp { get; set; }

        [JsonProperty("updated_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? UpdatedTimestamp { get; set; }

        public bool IsChecked { get; set; }
        public string Sku { get; set; }

    }
}
