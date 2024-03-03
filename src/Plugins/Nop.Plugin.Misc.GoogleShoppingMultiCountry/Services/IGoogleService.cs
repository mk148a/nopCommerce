using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services
{
    public partial interface IGoogleService
    {
        Task DeleteGoogleProductAsync(GoogleFeedProductRecord googleFeedProductRecord);

        Task<IList<GoogleFeedProductRecord>> GetAllAsync();

        Task<GoogleFeedProductRecord> GetByIdAsync(int googleFeedProductRecordId);

        Task<GoogleFeedProductRecord> GetByProductIdAsync(int productId);

        Task InsertGoogleProductRecordAsync(GoogleFeedProductRecord googleFeedProductRecord);

        Task UpdateGoogleProductRecordAsync(GoogleFeedProductRecord googleFeedProductRecord);

        Task<IList<string>> GetTaxonomyListAsync();
        Task<string> CreateTaxonomyEntityAsync(); 
        Task<IList<GoogleTaxonomyRecord>> GetTaxonomyListEntityAsync(); 
        Task<GoogleTaxonomyRecord> GetByCategoryIdAsync(int categoryId);
        Task<CategoryGoogleTaxonomyRecordMapping> GetGoogleTaxonomyRecordMappingByCategoryIdAsync(int categoryId);
        Task<IList<CategoryGoogleTaxonomyRecordMapping>> GetGoogleTaxonomyRecordMappingsAsync();
        Task InsertCategoryGoogleTaxonomyRecordMappingAsync(CategoryGoogleTaxonomyRecordMapping categoryGoogleTaxonomyRecordMapping);
        Task UpdateCategoryGoogleTaxonomyRecordMappingAsync(CategoryGoogleTaxonomyRecordMapping categoryGoogleTaxonomyRecordMapping);
        string GetFullTaxonomyNameByTaxonomyId(int taxonomyId);
    }
}
