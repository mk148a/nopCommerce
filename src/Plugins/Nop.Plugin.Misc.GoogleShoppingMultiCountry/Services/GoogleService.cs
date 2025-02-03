using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Caching;
using Nop.Data;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;
using Nop.Services.Caching;
using Nop.Services.Catalog;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services
{
    public partial class GoogleService : IGoogleService
    {
        #region Fields

        private readonly IRepository<GoogleFeedProductRecord> _gpRepository;
        private readonly IRepository<GoogleTaxonomyRecord> _googleTaxonomyRepository;
        private readonly IRepository<CategoryGoogleTaxonomyRecordMapping> _categoryGoogleTaxonomyRecordMappingRepository;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly ICategoryService _categoryService;
        #endregion

        #region Ctor

        public GoogleService(IRepository<GoogleFeedProductRecord> gpRepository, IRepository<GoogleTaxonomyRecord> googleTaxonomyRepository, IRepository<CategoryGoogleTaxonomyRecordMapping> categoryGoogleTaxonomyRecordMappingRepository, IStaticCacheManager staticCacheManager, ICategoryService categoryService)
        {
            _gpRepository = gpRepository;
            _googleTaxonomyRepository = googleTaxonomyRepository;
            _categoryGoogleTaxonomyRecordMappingRepository = categoryGoogleTaxonomyRecordMappingRepository;
            _staticCacheManager = staticCacheManager;
            _categoryService = categoryService;
        }

        #endregion

        #region Utilities

        private async Task<string> GetEmbeddedFileContentAsync(string resourceName)
        {
            var fullResourceName = $"Nop.Plugin.Misc.GoogleShoppingMultiCountry.Files.{resourceName}";
            var assem = GetType().Assembly;
            using var stream = assem.GetManifestResourceStream(fullResourceName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        #endregion

        #region Methods

        public virtual async Task DeleteGoogleProductAsync(GoogleFeedProductRecord googleFeedProductRecord)
        {
            if (googleFeedProductRecord == null)
                throw new ArgumentNullException(nameof(googleFeedProductRecord));

            await _gpRepository.DeleteAsync(googleFeedProductRecord);
        }

        public virtual async Task<IList<GoogleFeedProductRecord>> GetAllAsync()
        {
            var query = from gp in _gpRepository.Table
                        orderby gp.Id
                        select gp;
            var records = await query.ToListAsync();
            return records;
        }

        public virtual async Task<GoogleFeedProductRecord> GetByIdAsync(int googleFeedProductRecordId)
        {
            if (googleFeedProductRecordId == 0)
                return null;

            return await _gpRepository.GetByIdAsync(googleFeedProductRecordId);
        }

        public virtual async Task<GoogleFeedProductRecord> GetByProductIdAsync(int productId)
        {
            if (productId == 0)
                return null;

            var query = from gp in _gpRepository.Table
                        where gp.ProductId == productId
                        orderby gp.Id
                        select gp;
            var record = await query.FirstOrDefaultAsync();
            return record;
        }

        public virtual async Task InsertGoogleProductRecordAsync(GoogleFeedProductRecord googleFeedProductRecord)
        {
            if (googleFeedProductRecord == null)
                throw new ArgumentNullException(nameof(googleFeedProductRecord));

            await _gpRepository.InsertAsync(googleFeedProductRecord);
        }

        public virtual async Task UpdateGoogleProductRecordAsync(GoogleFeedProductRecord googleFeedProductRecord)
        {
            if (googleFeedProductRecord == null)
                throw new ArgumentNullException(nameof(googleFeedProductRecord));

            await _gpRepository.UpdateAsync(googleFeedProductRecord);
        }

        public virtual async Task<IList<string>> GetTaxonomyListAsync()
        {
            var fileContent = await GetEmbeddedFileContentAsync("taxonomy.txt");
            if (string.IsNullOrEmpty(fileContent))
                return new List<string>();

            //parse the file
            var result = fileContent.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            return result;
        }
        public virtual async Task<IList<GoogleTaxonomyRecord>> GetTaxonomyListEntityAsync()
        {
            return await _googleTaxonomyRepository.GetAllAsync(query => query);
        }

        public virtual async Task<string> CreateTaxonomyEntityAsync()
        {
            string result = "";
           var taxonomyList= await GetTaxonomyListAsync();

           if (taxonomyList != null)
           {
               foreach (var taxonomy in taxonomyList)
               {
                   GoogleTaxonomyRecord newCategory= new GoogleTaxonomyRecord();
                  int categoryId= int.Parse(taxonomy.Split(";")[0]);
                  string categoryName= taxonomy.Split(";")[1];
                  if (_googleTaxonomyRepository.Table.Any(x=>x.GoogleTaxonomyId == categoryId))
                  {
                      continue;
                  }

                  if (!categoryName.Contains(">"))
                  {
                      newCategory.Name= categoryName;
                      newCategory.GoogleTaxonomyId=categoryId;
                      newCategory.ParentId = 0;
                      await _googleTaxonomyRepository.InsertAsync(newCategory);
                  }
                  else
                  {
                      var lastSubCategoryName= categoryName.Split(">").Last();
                      var previousSubCategoryName = categoryName.Split(">").ElementAt(categoryName.Split(">").Length - 2);

                      newCategory.Name= lastSubCategoryName;
                      newCategory.GoogleTaxonomyId = categoryId;
                      var previousSubCategory = await _googleTaxonomyRepository.Table.SingleAsync(x => x.Name == previousSubCategoryName);
                      if (previousSubCategory != null)
                      {
                          newCategory.ParentId= previousSubCategory.GoogleTaxonomyId;
                          
                          await _googleTaxonomyRepository.InsertAsync(newCategory);

                          var newSavedCategory = await _googleTaxonomyRepository.Table.SingleAsync(x => x.GoogleTaxonomyId == categoryId);
                          if (previousSubCategory.SubCategories==null)
                          {
                              previousSubCategory.SubCategories = new List<GoogleTaxonomyRecord>();
                          }
                            previousSubCategory.SubCategories.Add(newSavedCategory);
                            await _googleTaxonomyRepository.UpdateAsync(previousSubCategory);
                      }
                      else
                      {
                          Console.WriteLine(lastSubCategoryName+" kategorisi için "+ previousSubCategoryName+" üst kategorisi bulunamadı");
                      }



                  }

                
               







               }
           }


            return result;
        }

        public virtual string GetFullTaxonomyNameByTaxonomyId(int taxonomyId)
        {
            var cacheKey = new CacheKey($"GoogleTaxonomyFullName.{taxonomyId}");
            return _staticCacheManager.Get(cacheKey, () =>
            {
                string result = "";
                var query = from gp in _googleTaxonomyRepository.Table
                    where gp.GoogleTaxonomyId == taxonomyId
                    orderby gp.Id
                    select gp;

                var thisTaxonomy = query.FirstOrDefault();
                if (thisTaxonomy == null)
                    return result;

                result = thisTaxonomy.Name;

                while (thisTaxonomy.ParentId != 0)
                {
                    query = from gp in _googleTaxonomyRepository.Table
                        where gp.GoogleTaxonomyId == thisTaxonomy.ParentId
                        orderby gp.Id
                        select gp;
                    thisTaxonomy = query.FirstOrDefault();
                    if (thisTaxonomy != null)
                    {
                        result = thisTaxonomy.Name + ">" + result;
                    }
                    else
                    {
                        break;
                    }
                }

                return result;
            });
        }
  public virtual async Task<GoogleTaxonomyRecord> GetByCategoryIdAsync(int categoryId)
        {
            if (categoryId == 0)
                return null;

            var query = from gp in _categoryGoogleTaxonomyRecordMappingRepository.Table
                        where gp.CategoryId == categoryId
                        orderby gp.Id
                        select gp;
            var record = await query.FirstOrDefaultAsync();
            if (record == null)
                return null;
          

            var query1 = from gp in _googleTaxonomyRepository.Table
                where gp.GoogleTaxonomyId == record.GoogleTaxonomyRecordId
                         orderby gp.Id
                select gp;
            var record1 = await query1.FirstOrDefaultAsync();
            return record1;
        }   
  public virtual async Task<IList<CategoryGoogleTaxonomyRecordMapping>> GetGoogleTaxonomyRecordMappingsAsync()
  {
      return await _categoryGoogleTaxonomyRecordMappingRepository.GetAllAsync(query => query);
  }

        public virtual async Task<CategoryGoogleTaxonomyRecordMapping> GetGoogleTaxonomyRecordMappingByCategoryIdAsync(int categoryId)
        {
            if (categoryId == 0)
                return null;

            var query = from gp in _categoryGoogleTaxonomyRecordMappingRepository.Table
                where gp.CategoryId == categoryId
                orderby gp.Id
                select gp;
            var record = await query.FirstOrDefaultAsync();
            if (record == null)
                return null;

            return record;
        }

        public virtual async Task InsertCategoryGoogleTaxonomyRecordMappingAsync(CategoryGoogleTaxonomyRecordMapping categoryGoogleTaxonomyRecordMapping)
        {
            if (categoryGoogleTaxonomyRecordMapping == null)
                throw new ArgumentNullException(nameof(categoryGoogleTaxonomyRecordMapping));

            await _categoryGoogleTaxonomyRecordMappingRepository.InsertAsync(categoryGoogleTaxonomyRecordMapping);
        }

        public virtual async Task UpdateCategoryGoogleTaxonomyRecordMappingAsync(CategoryGoogleTaxonomyRecordMapping categoryGoogleTaxonomyRecordMapping)
        {
            if (categoryGoogleTaxonomyRecordMapping == null)
                throw new ArgumentNullException(nameof(categoryGoogleTaxonomyRecordMapping));

            await _categoryGoogleTaxonomyRecordMappingRepository.UpdateAsync(categoryGoogleTaxonomyRecordMapping);
        }

        public virtual async Task<IList<GoogleTaxonomyRecord>> SearchGoogleTaxonomy(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return new List<GoogleTaxonomyRecord>();

            searchTerm = searchTerm.ToLower();
            
            // Önce veritabanından tüm kayıtları çekelim
            var allRecords = await _googleTaxonomyRepository.GetAllAsync(query => query);
            
            // Sonra memory'de filtreleme yapalım
            var result = allRecords.Where(taxonomy => 
                taxonomy.Name.ToLower().Contains(searchTerm) || 
                GetFullTaxonomyNameByTaxonomyId(taxonomy.GoogleTaxonomyId).ToLower().Contains(searchTerm))
                .ToList();

            return result;
        }

        public virtual async Task<string> GetTaxonomyIdForProduct(int productId, int categoryId)
        {
            // Önce ürüne özel taxonomy kontrolü
            var googleProduct = await GetByProductIdAsync(productId);
            if (googleProduct != null && !string.IsNullOrEmpty(googleProduct.Taxonomy))
                return googleProduct.Taxonomy;

            // Ürünün kategorisine ait taxonomy kontrolü
            var categoryMapping = await GetGoogleTaxonomyRecordMappingByCategoryIdAsync(categoryId);
            if (categoryMapping != null)
            {
                var taxonomyRecord = await _googleTaxonomyRepository.Table
                    .FirstOrDefaultAsync(x => x.GoogleTaxonomyId == categoryMapping.GoogleTaxonomyRecordId);
                if (taxonomyRecord != null)
                    return taxonomyRecord.GoogleTaxonomyId.ToString();
            }

            // Üst kategorileri kontrol et
            var category = await _categoryService.GetCategoryByIdAsync(categoryId);
            while (category?.ParentCategoryId > 0)
            {
                category = await _categoryService.GetCategoryByIdAsync(category.ParentCategoryId);
                if (category == null) break;

                categoryMapping = await GetGoogleTaxonomyRecordMappingByCategoryIdAsync(category.Id);
                if (categoryMapping != null)
                {
                    var taxonomyRecord = await _googleTaxonomyRepository.Table
                        .FirstOrDefaultAsync(x => x.GoogleTaxonomyId == categoryMapping.GoogleTaxonomyRecordId);
                    if (taxonomyRecord != null)
                        return taxonomyRecord.GoogleTaxonomyId.ToString();
                }
            }

            return null;
        }
    }



#endregion
}

