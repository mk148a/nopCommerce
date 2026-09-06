using System;
using System.Linq.Expressions;
using FluentValidation;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Models;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Validators;

public class ExportFileValidator : BaseNopValidator<FileInformationModel>
{
	public ExportFileValidator(ILocalizationService localizationService)
	{
		DefaultValidatorOptions.WithMessage<FileInformationModel, string>(DefaultValidatorExtensions.NotEmpty<FileInformationModel, string>((IRuleBuilder<FileInformationModel, string>)(object)((AbstractValidator<FileInformationModel>)(object)this).RuleFor<string>((Expression<Func<FileInformationModel, string>>)((FileInformationModel x) => x.GAContainerId))), localizationService.GetResourceAsync("Admin.NopStation.GoogleTagManager.Configuration.GTMContainerId.Required").Result);
	}
}
