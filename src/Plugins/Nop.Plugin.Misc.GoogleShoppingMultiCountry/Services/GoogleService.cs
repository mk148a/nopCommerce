using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Data;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services
{
    public partial class GoogleService : IGoogleService
    {
        #region Fields

        private readonly IRepository<GoogleFeedProductRecord> _gpRepository;
        private readonly IRepository<GoogleTaxonomyRecord> _googleTaxonomyRepository;
        #endregion

        #region Ctor

        public GoogleService(IRepository<GoogleFeedProductRecord> gpRepository, IRepository<GoogleTaxonomyRecord> googleTaxonomyRepository)
        {
            _gpRepository = gpRepository;
            _googleTaxonomyRepository = googleTaxonomyRepository;
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

        #endregion
    }
}
