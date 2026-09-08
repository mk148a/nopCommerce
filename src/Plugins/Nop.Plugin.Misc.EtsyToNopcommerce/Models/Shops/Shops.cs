using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Models.Shops
{
    public class Result
    {
        public int shop_id { get; set; }
        public string shop_name { get; set; }
        public int user_id { get; set; }
        public int create_date { get; set; }
    }




    public class Shops
    {
        public List<Result> results { get; set; }

    }
}
