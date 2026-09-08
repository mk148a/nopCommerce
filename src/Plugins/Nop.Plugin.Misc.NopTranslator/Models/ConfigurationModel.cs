using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.NopTranslator.Models
{
    /// <summary>
    /// Represents configuration model
    /// </summary>
    public record ConfigurationModel : BaseNopModel
    {
        #region Ctor

        public ConfigurationModel()
        {
          
          
        }

        #endregion

        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }
 
        public string ApiUrl { get; set; }
        public bool ApiUrl_OverrideForStore { get; set; }




        #endregion
    }
}