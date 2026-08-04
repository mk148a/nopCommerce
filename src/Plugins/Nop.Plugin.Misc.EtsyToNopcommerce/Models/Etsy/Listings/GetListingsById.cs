using System.Runtime.Serialization;
using DocumentFormat.OpenXml.Math;
using Nop.Core;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization.Formatters.Binary;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class StringArrayConverter : JsonConverter
    {
        private string _delimiter = " ; ";
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Array)
            {
                var arrayString = token.ToObject<string[]>();

                return string.Join(_delimiter, arrayString);
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is string[] stringArray)
            {
                var convertedString = string.Join(", ", stringArray);
                writer.WriteValue(convertedString);
            }
        }
    }

    public static class SerializerDeserializerExtensions
    {
        public static byte[] Serializer(this object _object)
        {
            byte[] bytes;
            using (var _MemoryStream = new MemoryStream())
            {
                IFormatter _BinaryFormatter = new BinaryFormatter();
                _BinaryFormatter.Serialize(_MemoryStream, _object);
                bytes = _MemoryStream.ToArray();
            }
            return bytes;
        }

        public static T Deserializer<T>(this byte[] _byteArray)
        {
            T ReturnValue;
            using (var _MemoryStream = new MemoryStream(_byteArray))
            {
                IFormatter _BinaryFormatter = new BinaryFormatter();
                ReturnValue = (T)_BinaryFormatter.Deserialize(_MemoryStream);
            }
            return ReturnValue;
        }
    }
    public class GetListingsById
    {
        [JsonProperty("count", NullValueHandling = NullValueHandling.Ignore)]
        public long? Count { get; set; }

        [JsonProperty("results", NullValueHandling = NullValueHandling.Ignore)]
        public List<EtsyListing> Results { get; set; }
    }

    public class EtsyListing : BaseEntity
    {
        [JsonProperty("listing_id", NullValueHandling = NullValueHandling.Ignore)]
        public long ListingId { get; set; }

        [JsonProperty("user_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? UserId { get; set; }

        [JsonProperty("shop_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ShopId { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        public string State { get; set; }

        [JsonProperty("creation_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreationTimestamp { get; set; }

        [JsonProperty("created_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreatedTimestamp { get; set; }

        [JsonProperty("ending_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? EndingTimestamp { get; set; }

        [JsonProperty("original_creation_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? OriginalCreationTimestamp { get; set; }

        [JsonProperty("last_modified_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? LastModifiedTimestamp { get; set; }

        [JsonProperty("updated_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? UpdatedTimestamp { get; set; }

        [JsonProperty("state_timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public long? StateTimestamp { get; set; }

        [JsonProperty("quantity", NullValueHandling = NullValueHandling.Ignore)]
        public long? Quantity { get; set; }

        [JsonProperty("shop_section_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ShopSectionId { get; set; }

        [JsonProperty("featured_rank", NullValueHandling = NullValueHandling.Ignore)]
        public long? FeaturedRank { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri Url { get; set; }

        [JsonProperty("num_favorers", NullValueHandling = NullValueHandling.Ignore)]
        public long? NumFavorers { get; set; }

        [JsonProperty("non_taxable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? NonTaxable { get; set; }

        [JsonProperty("is_taxable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsTaxable { get; set; }

        [JsonProperty("is_customizable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCustomizable { get; set; }

        [JsonProperty("is_personalizable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPersonalizable { get; set; }

        [JsonProperty("personalization_is_required", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PersonalizationIsRequired { get; set; }

        [JsonProperty("personalization_char_count_max")]
        public string PersonalizationCharCountMax { get; set; }

        [JsonProperty("personalization_instructions")]
        public string PersonalizationInstructions { get; set; }

        [JsonProperty("listing_type", NullValueHandling = NullValueHandling.Ignore)]
        public string ListingType { get; set; }


        [JsonConverter(typeof(StringArrayConverter))]
        [JsonProperty("tags")]
        public string Tags { get; set; }

        [JsonConverter(typeof(StringArrayConverter))]
        [JsonProperty("materials", NullValueHandling = NullValueHandling.Ignore)]
        public string Materials { get; set; }

        [JsonProperty("shipping_profile_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ShippingProfileId { get; set; }

        [JsonProperty("return_policy_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ReturnPolicyId { get; set; }

        [JsonProperty("processing_min", NullValueHandling = NullValueHandling.Ignore)]
        public long? ProcessingMin { get; set; }

        [JsonProperty("processing_max", NullValueHandling = NullValueHandling.Ignore)]
        public long? ProcessingMax { get; set; }

        [JsonProperty("who_made", NullValueHandling = NullValueHandling.Ignore)]
        public string WhoMade { get; set; }

        [JsonProperty("when_made", NullValueHandling = NullValueHandling.Ignore)]
        public string WhenMade { get; set; }

        [JsonProperty("is_supply", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSupply { get; set; }

        [JsonProperty("item_weight")] public string ItemWeight { get; set; }

        [JsonProperty("item_weight_unit")] public string ItemWeightUnit { get; set; }

        [JsonProperty("item_length")] public string ItemLength { get; set; }

        [JsonProperty("item_width")] public string ItemWidth { get; set; }

        [JsonProperty("item_height")] public string ItemHeight { get; set; }

        [JsonProperty("item_dimensions_unit")] public string ItemDimensionsUnit { get; set; }

        [JsonProperty("is_private", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPrivate { get; set; }

        [JsonProperty("style", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringArrayConverter))]
        public string Style { get; set; }

        [JsonProperty("file_data", NullValueHandling = NullValueHandling.Ignore)]
        public string FileData { get; set; }

        [JsonProperty("has_variations", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasVariations { get; set; }

        [JsonProperty("should_auto_renew", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShouldAutoRenew { get; set; }

        [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

        [JsonProperty("price", NullValueHandling = NullValueHandling.Ignore)]
        public EtsyPrice Price { get; set; }


        [JsonProperty("taxonomy_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? TaxonomyId { get; set; }

        [JsonConverter(typeof(StringArrayConverter))]
        [JsonProperty("production_partners", NullValueHandling = NullValueHandling.Ignore)]
        public string ProductionPartners { get; set; }

        [JsonConverter(typeof(StringArrayConverter))]
        [JsonProperty("skus", NullValueHandling = NullValueHandling.Ignore)]
        public string Skus { get; set; }

        [JsonProperty("views", NullValueHandling = NullValueHandling.Ignore)]
        public long? Views { get; set; }

        [JsonProperty("shipping_profile")] public string ShippingProfile { get; set; }

        [JsonProperty("shop")] public string Shop { get; set; }

        [JsonProperty("images")] public string Images { get; set; }

        [JsonProperty("videos")] public string Videos { get; set; }

        [JsonProperty("user")] public string User { get; set; }

        [JsonProperty("translations")] public string Translations { get; set; }

        [JsonProperty("inventory", NullValueHandling = NullValueHandling.Ignore)]
        public EtsyInventory Inventory { get; set; }
        public bool IsChecked { get; set; }

    }

    public partial class EtsyInventory : BaseEntity
    {
        [JsonProperty("products", NullValueHandling = NullValueHandling.Ignore)]
        public List<EtsyProduct> Products { get; set; }

        [JsonProperty("price_on_property", NullValueHandling = NullValueHandling.Ignore)]
        public string PriceOnProperty { get; set; }

        [JsonProperty("quantity_on_property", NullValueHandling = NullValueHandling.Ignore)]
        public string QuantityOnProperty { get; set; }

        [JsonProperty("sku_on_property", NullValueHandling = NullValueHandling.Ignore)]
        public string SkuOnProperty { get; set; }

        [JsonProperty("listing")] public string Listing { get; set; }
    }

    public partial class EtsyProduct : BaseEntity
    {

        [JsonProperty("product_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? ProductId { get; set; }

        [JsonProperty("sku", NullValueHandling = NullValueHandling.Ignore)]
        public string Sku { get; set; }

        [JsonProperty("is_deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsDeleted { get; set; }

        [JsonProperty("offerings", NullValueHandling = NullValueHandling.Ignore)]
        public List<EtsyOffering> Offerings { get; set; }

        [JsonProperty("property_values", NullValueHandling = NullValueHandling.Ignore)]
        public List<EtsyPropertyValue> PropertyValues { get; set; }
    }

    public partial class EtsyOffering : BaseEntity
    {
        [JsonProperty("offering_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? OfferingId { get; set; }

        [JsonProperty("quantity", NullValueHandling = NullValueHandling.Ignore)]
        public long? Quantity { get; set; }

        [JsonProperty("is_enabled", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsEnabled { get; set; }

        [JsonProperty("is_deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsDeleted { get; set; }

        [JsonProperty("price", NullValueHandling = NullValueHandling.Ignore)]
        public EtsyPrice Price { get; set; }

    }

    public partial class EtsyPrice : BaseEntity
    {
        [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
        public long? Amount { get; set; }

        [JsonProperty("divisor", NullValueHandling = NullValueHandling.Ignore)]
        public long? Divisor { get; set; }

        [JsonProperty("currency_code", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrencyCode { get; set; }
    }

    public partial class EtsyPropertyValue : BaseEntity
    {
        [JsonProperty("property_id", NullValueHandling = NullValueHandling.Ignore)]
        public long? PropertyId { get; set; }

        [JsonProperty("property_name", NullValueHandling = NullValueHandling.Ignore)]
        public string PropertyName { get; set; }

        [JsonProperty("scale_id")] public long? ScaleId { get; set; }

        [JsonProperty("scale_name")] public string ScaleName { get; set; }

        [JsonProperty("value_ids", NullValueHandling = NullValueHandling.Ignore)]
        public List<long> ValueIds { get; set; }

        [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
        public string Values { get; set; }

    }
}