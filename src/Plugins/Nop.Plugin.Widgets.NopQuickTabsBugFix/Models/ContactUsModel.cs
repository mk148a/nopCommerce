using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Spreadsheet;
using MySqlX.XDevAPI.Common;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.NopQuickTabsBugFix.Models
{
    public record ContactUsModel : BaseNopModel
    {


        [DataType(DataType.EmailAddress)]
        [NopResourceDisplayName("ContactUs.Email")]
        public string Email { get; set; }

        [NopResourceDisplayName("ContactUs.Subject")]
        public string Subject { get; set; }

        public bool SubjectEnabled { get; set; }

        [NopResourceDisplayName("ContactUs.Enquiry")]
        public string Enquiry { get; set; }

        [NopResourceDisplayName("ContactUs.FullName")]
        public string FullName { get; set; }

        public bool DisplayCaptcha { get; set; }

        public bool SuccessfullySent { get; set; }

        public string Result { get; set; }


        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("ContactUsModel");
            stringBuilder.Append(" { ");
          

            stringBuilder.Append('}');
            return stringBuilder.ToString();
        }

   
        protected override bool PrintMembers(StringBuilder builder)
        {
           

            builder.Append("Email = ");
            builder.Append((object?)Email);
            builder.Append(", Subject = ");
            builder.Append((object?)Subject);
            builder.Append(", SubjectEnabled = ");
            builder.Append(SubjectEnabled.ToString());
            builder.Append(", Enquiry = ");
            builder.Append((object?)Enquiry);
            builder.Append(", FullName = ");
            builder.Append((object?)FullName);
            builder.Append(", DisplayCaptcha = ");
            builder.Append(DisplayCaptcha.ToString());
            builder.Append(", SuccessfullySent = ");
            builder.Append(SuccessfullySent.ToString());
            builder.Append(", Result = ");
            builder.Append((object?)Result);
            return true;
        }

        [CompilerGenerated]
        public override int GetHashCode()
        {
            return (((((((((BaseNopModel)this).GetHashCode() * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Email)) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Subject)) * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(SubjectEnabled)) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Enquiry)) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FullName)) * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(DisplayCaptcha)) * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(SuccessfullySent)) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Result);
        }

     
      
        public virtual bool Equals(ContactUsModel? other)
        {
            if ((object)this != other)
            {
                if (((BaseNopModel)this).Equals((BaseNopModel)(object)other) && EqualityComparer<string>.Default.Equals(Email, other.Email) && EqualityComparer<string>.Default.Equals(Subject, other.Subject) && EqualityComparer<bool>.Default.Equals(SubjectEnabled, other.SubjectEnabled) && EqualityComparer<string>.Default.Equals(Enquiry, other.Enquiry) && EqualityComparer<string>.Default.Equals(FullName, other.FullName) && EqualityComparer<bool>.Default.Equals(DisplayCaptcha, other.DisplayCaptcha) && EqualityComparer<bool>.Default.Equals(SuccessfullySent, other.SuccessfullySent))
                {
                    return EqualityComparer<string>.Default.Equals(Result, other.Result);
                }

                return false;
            }

            return true;
        }

        protected ContactUsModel(ContactUsModel original)
            : base((BaseNopModel)(object)original)
        {
            Email = original.Email;
            Subject = original.Subject;
            SubjectEnabled = original.SubjectEnabled;
            Enquiry = original.Enquiry;
            FullName = original.FullName;
            DisplayCaptcha = original.DisplayCaptcha;
            SuccessfullySent = original.SuccessfullySent;
            Result = original.Result;
        }
    }
   
}
