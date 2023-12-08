using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Plugins;


namespace Nop.Plugin.Misc.ProductCatalogCreator
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class ProductCatalogCreator : BasePlugin
    {
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IActionContextAccessor _actionContextAccessor;

        public ProductCatalogCreator(IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionContextAccessor)
        {
            _urlHelperFactory = urlHelperFactory;
            _actionContextAccessor = actionContextAccessor;
        }
        public override async Task InstallAsync()
        {
            // Burada kurulum ile ilgili kodlarınızı ekleyin
            // Örneğin: ayarları kaydetmek, veritabanı tabloları oluşturmak vb.

            await base.InstallAsync();
        }

        // Eklentinin kaldırılması için
        public override async Task UninstallAsync()
        {
            // Burada kaldırma ile ilgili kodlarınızı ekleyin
            // Örneğin: ayarları sıfırlamak, veritabanı tablolarını silmek vb.

            await base.UninstallAsync();
        }

     
        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl("Plugin.Misc.ProductCatalogCreator.Index");
        }
    }
}
