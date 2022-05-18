using FluentValidation;
using Nop.Plugin.Baroque.DiscountRules.HasOneProduct.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Baroque.DiscountRules.HasOneProduct.Validators
{
    /// <summary>
    /// Represents requirement product model validator
    /// </summary>
    public class RequirementProductValidator : BaseNopValidator<ConfigurationModel.RequirementProductModel>
    {
        public RequirementProductValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.MinQuantity).GreaterThan(0).WithMessage(localizationService.GetResource("Nop.Plugin.Baroque.DiscountRules.HasOneProduct.RequirementProduct.MinQuantity.MustBeGreaterThanZero"));
            RuleFor(x => x.MaxQuantity).GreaterThan(0).WithMessage(localizationService.GetResource("Nop.Plugin.Baroque.DiscountRules.HasOneProduct.RequirementProduct.MaxQuantity.MustBeGreaterThanZero"));
            RuleFor(x => x.MaxQuantity).Must((model, field, context) =>
            {
                //return error when max value lower than minimum value
                if (model.MinQuantity.HasValue && field.HasValue && field.Value < model.MinQuantity.Value)
                    return false;

                return true;
            }).WithMessage(localizationService.GetResource("Nop.Plugin.Baroque.DiscountRules.HasOneProduct.RequirementProduct.MaxQuantity.MustBeGreaterMinQuantity"));
        }
    }
}
