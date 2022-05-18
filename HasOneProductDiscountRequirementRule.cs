using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Baroque.DiscountRules.HasOneProduct.Extensions;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using System;
using System.Linq;

namespace Nop.Plugin.Baroque.DiscountRules.HasOneProduct
{
    public partial class HasOneProductDiscountRequirementRule : BaseBaroquePlugin, IDiscountRequirementRule
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IDiscountService _discountService;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public HasOneProductDiscountRequirementRule(IActionContextAccessor actionContextAccessor,
            IDiscountService discountService,
            ILocalizationService localizationService,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper)
        {
            _actionContextAccessor = actionContextAccessor;
            _discountService = discountService;
            _localizationService = localizationService;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check discount requirement
        /// </summary>
        /// <param name="request">Object that contains all information required to check the requirement (Current customer, discount, etc)</param>
        /// <returns>Result</returns>
        public DiscountRequirementValidationResult CheckRequirement(DiscountRequirementValidationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            //invalid by default
            var result = new DiscountRequirementValidationResult();

            //try to get saved restricted product identifiers
            var restrictedProductIds = _settingService.GetSettingByKey<string>(SettingExtensions.FormatSettingName(request.DiscountRequirementId));
            if (string.IsNullOrWhiteSpace(restrictedProductIds))
            {
                //valid
                result.IsValid = true;
                return result;
            }

            if (request.Customer == null)
                return result;

            //we support three ways of specifying products:
            //1. The comma-separated list of product identifiers (e.g. 77, 123, 156).
            //2. The comma-separated list of product identifiers with quantities.
            //      {Product ID}:{Quantity}. For example, 77:1, 123:2, 156:3
            //3. The comma-separated list of product identifiers with quantity range.
            //      {Product ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
            var restrictedProducts = restrictedProductIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            if (!restrictedProducts.Any())
                return result;

            //group products in the cart by product ID
            //it could be the same product with distinct product attributes
            //that's why we get the total quantity of this product            
            var cart = _shoppingCartService.GetShoppingCart(customer: request.Customer, shoppingCartType: ShoppingCartType.ShoppingCart, storeId: request.Store.Id)
                .GroupBy(sci => sci.ProductId)
                .Select(g => new { ProductId = g.Key, TotalQuantity = g.Sum(x => x.Quantity) });

            //process
            var found = false;
            foreach (var restrictedProduct in restrictedProducts)
            {
                if (string.IsNullOrWhiteSpace(restrictedProduct))
                    continue;

                foreach (var sci in cart)
                {
                    if (restrictedProduct.Contains(":"))
                    {
                        if (restrictedProduct.Contains("-"))
                        {
                            //the third way (the quantity rage specified)
                            //{Product ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
                            if (!int.TryParse(restrictedProduct.Split(new[] { ':' })[0], out var restrictedProductId))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedProduct.Split(new[] { ':' })[1].Split(new[] { '-' })[0], out var quantityMin))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedProduct.Split(new[] { ':' })[1].Split(new[] { '-' })[1], out var quantityMax))
                                //parsing error; exit;
                                return result;

                            if (sci.ProductId == restrictedProductId && quantityMin <= sci.TotalQuantity && sci.TotalQuantity <= quantityMax)
                            {
                                found = true;
                                break;
                            }
                        }
                        else
                        {
                            //the second way (the quantity specified)
                            //{Product ID}:{Quantity}. For example, 77:1, 123:2, 156:3
                            if (!int.TryParse(restrictedProduct.Split(new[] { ':' })[0], out var restrictedProductId))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedProduct.Split(new[] { ':' })[1], out var quantity))
                                //parsing error; exit;
                                return result;

                            if (sci.ProductId == restrictedProductId && sci.TotalQuantity == quantity)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        //the first way (the quantity is not specified)
                        if (int.TryParse(restrictedProduct, out var restrictedProductId))
                        {
                            if (sci.ProductId == restrictedProductId)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (found)
                {
                    break;
                }
            }

            if (found)
            {
                //valid
                result.IsValid = true;
                return result;
            }

            return result;
        }

        /// <summary>
        /// Get URL for rule configuration
        /// </summary>
        /// <param name="discountId">Discount identifier</param>
        /// <param name="discountRequirementId">Discount requirement identifier (if editing)</param>
        /// <returns>URL</returns>
        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

            return urlHelper.Action("Configure", "DiscountRulesHasOneProduct",
                new { discountId = discountId, discountRequirementId = discountRequirementId }, _webHelper.CurrentRequestProtocol);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //discount requirements
            var discountRequirements = _discountService.GetAllDiscountRequirements()
                .Where(discountRequirement => discountRequirement.DiscountRequirementRuleSystemName.Equals(this.PluginDescriptor.SystemName));

            foreach (var discountRequirement in discountRequirements)
                _discountService.DeleteDiscountRequirement(discountRequirement);

            base.Uninstall();
        }

        #endregion
    }
}