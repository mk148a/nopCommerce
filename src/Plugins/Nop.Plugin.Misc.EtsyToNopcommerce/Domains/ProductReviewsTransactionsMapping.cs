using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Domains
{
    public class ProductReviewsTransactionsMapping:BaseEntity
    {
        public long TransactionId { get; set; }
        public int ProductReviewId { get; set; }
    }
}
