using System;
using System.Linq.Expressions;
using FluentValidation;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Models;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Validators;

public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
{
	public ConfigurationValidator(ILocalizationService localizationService)
	{
		DefaultValidatorOptions.WithMessage<ConfigurationModel, string>(DefaultValidatorExtensions.NotEmpty<ConfigurationModel, string>((IRuleBuilder<ConfigurationModel, string>)(object)((AbstractValidator<ConfigurationModel>)(object)this).RuleFor<string>((Expression<Func<ConfigurationModel, string>>)((ConfigurationModel x) => x.GTMContainerId))), localizationService.GetResourceAsync("Admin.NopStation.GoogleTagManager.Configuration.GTMContainerId.Required").Result);
	}
}
