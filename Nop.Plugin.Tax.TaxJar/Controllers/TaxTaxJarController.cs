﻿using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Tax.TaxJar.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Tax.TaxJar.Controllers
{

    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class TaxTaxJarController : BasePluginController
    {
        #region Fields

        private readonly ICountryService _countryService;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly TaxJarSettings _taxJarSettings;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public TaxTaxJarController(ICountryService countryService,
            ILocalizationService localizationService,
            ISettingService settingService,
            TaxJarSettings taxJarSettings,
            IPermissionService permissionService)
        {
            this._countryService = countryService;
            this._localizationService = localizationService;
            this._settingService = settingService;
            this._taxJarSettings = taxJarSettings;
            this._permissionService = permissionService;
        }

        #endregion

        #region Utilities

        [NonAction]
        protected void PrepareAddress(TestAddressModel model)
        {
            model.AvailableCountries = _countryService.GetAllCountries(showHidden: true)
                .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToList();
            model.AvailableCountries.Insert(0, new SelectListItem { Text = _localizationService.GetResource("Admin.Address.SelectCountry"), Value = "0" });
        }

        #endregion

        #region Methods

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var model = new TaxTaxJarModel { ApiToken = _taxJarSettings.ApiToken };
            PrepareAddress(model.TestAddress);

            return View("~/Plugins/Tax.TaxJar/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public IActionResult Configure(TaxTaxJarModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            _taxJarSettings.ApiToken = model.ApiToken;
            _settingService.SaveSetting(_taxJarSettings);

            PrepareAddress(model.TestAddress);
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return View("~/Plugins/Tax.TaxJar/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("test")]
        public IActionResult TestRequest(TaxTaxJarModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            var testResult = new StringBuilder();
            var country = _countryService.GetCountryById(model.TestAddress.CountryId);
            var taxJarManager = new TaxJarManager { Api = _taxJarSettings.ApiToken };
            var result = taxJarManager.GetTaxRate(
                country?.TwoLetterIsoCode, 
                model.TestAddress.City, 
                null, 
                model.TestAddress.Zip);
            if (result.IsSuccess)
            {
                if (result.Rate.IsUsCanada)
                {
                    testResult.AppendFormat("State: {0}<br />", result.Rate.State);
                    testResult.AppendFormat("County: {0}<br />", result.Rate.County);
                    testResult.AppendFormat("City: {0}<br />", result.Rate.City);
                    testResult.AppendFormat("State rate: {0}<br />", result.Rate.StateRate);
                    testResult.AppendFormat("County rate: {0}<br />", result.Rate.CountyRate);
                    testResult.AppendFormat("City rate: {0}<br />", result.Rate.CityRate);
                    testResult.AppendFormat("Combined district rate: {0}<br />", result.Rate.CombinedDistrictRate);
                    testResult.AppendFormat("<b>Total rate: {0}<b/>", result.Rate.CombinedRate);
                }
                else
                {
                    testResult.AppendFormat("Country: {0}<br />", result.Rate.CountryName);
                    testResult.AppendFormat("Reduced rate: {0}<br />", result.Rate.ReducedRate);
                    testResult.AppendFormat("Super reduced rate: {0}<br />", result.Rate.SuperReducedRate);
                    testResult.AppendFormat("Parking rate: {0}<br />", result.Rate.ParkingRate);
                    testResult.AppendFormat("<b>Standard rate: {0}<b/>", result.Rate.StandardRate);
                }
            }
            else
                testResult.Append(result.ErrorMessage);

            model.TestingResult = testResult.ToString();
            PrepareAddress(model.TestAddress);

            return View("~/Plugins/Tax.TaxJar/Views/Configure.cshtml", model);
        }

        #endregion
    }
}