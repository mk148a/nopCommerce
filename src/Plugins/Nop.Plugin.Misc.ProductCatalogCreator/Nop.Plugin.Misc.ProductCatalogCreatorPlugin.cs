using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Services.Cms;
using Nop.Services.Plugins;


namespace Nop.Plugin.Misc.ProductCatalogCreator
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class CustomPlugin : BasePlugin
    {
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
    }
}
