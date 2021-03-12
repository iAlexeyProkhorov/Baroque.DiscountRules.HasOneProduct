﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Opensoftware.DiscountRules.HasOneProduct
{
    public abstract class BaseOswPlugin : BasePlugin
    {
        #region Fields

        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Additional fields

        public static FileInfo _originalAssemblyFile { get; private set; }

        public static PluginDescriptor OswPluginDescriptor { get; private set; }

        #endregion

        #region Constructor

        public BaseOswPlugin()
        {
            this._localizationService = EngineContext.Current.Resolve<ILocalizationService>();
            this._languageService = EngineContext.Current.Resolve<ILanguageService>();

            //get current plugin location in file system
            string pluginLibraryLocationPath = Assembly.GetExecutingAssembly().Location;

            //get plugin library name
            string pluginLibraryName = Path.GetFileName(pluginLibraryLocationPath);
        }

        static BaseOswPlugin()
        {
            //get current plugin location in file system
            string pluginLibraryLocationPath = Assembly.GetExecutingAssembly().Location;

            //get plugin library name
            string pluginLibraryName = Path.GetFileName(pluginLibraryLocationPath);

            //get plugin library folder
            string pluginLibraryFolder = Path.GetDirectoryName(pluginLibraryLocationPath);
            var parentFolder = Directory.GetParent(pluginLibraryFolder);

            //search for this file in nop plugins folder
            var foundedFiles = Directory.GetFiles(parentFolder.FullName, pluginLibraryName, SearchOption.AllDirectories);

            //create default plugin descriptor
            OswPluginDescriptor = new PluginDescriptor(Assembly.GetExecutingAssembly());

            //get plugin descriptor from plugin description file
            if (foundedFiles.Any())
            {
                string nopPluginLibraryPath = string.Empty;
                foreach (var file in foundedFiles)
                    if (!file.Contains("\\Plugins\\bin\\"))
                    {
                        nopPluginLibraryPath = file;
                        break;
                    }

                if (!string.IsNullOrEmpty(nopPluginLibraryPath))
                {
                    _originalAssemblyFile = new FileInfo(nopPluginLibraryPath);

                    var pluginDescriptorFile = $"{_originalAssemblyFile.DirectoryName}\\{ NopPluginDefaults.DescriptionFileName }";

                    if (!File.Exists(pluginDescriptorFile))
                        throw new Exception($"Plugin descriptor for {pluginLibraryName} aren't found");

                    var descriptorText = File.ReadAllText(pluginDescriptorFile);


                    OswPluginDescriptor = PluginDescriptor.GetPluginDescriptorFromText(descriptorText);

                    OswPluginDescriptor.Installed = IsPluginInstalled();
                }
            }
        }

        #endregion

        #region Utilites

        /// <summary>
        /// Generate localization file path by language culture
        /// </summary>
        /// <param name="culture">Language culture. For example 'en-US'</param>
        /// <returns>Localization Xml file path</returns>
        protected string GenerateLocalizationXmlFilePathByCulture(string culture = "en-US")
        {
            string fileName = string.Format("localization.{0}.xml", culture);
            string contentDirectoryPath = $"{_originalAssemblyFile.DirectoryName}\\Content\\";

            return $"{contentDirectoryPath}{fileName}";
        }

        /// <summary>
        /// Install available plugin localizations
        /// </summary>
        protected virtual void InstallLocalization()
        {
            var allLanguages = _languageService.GetAllLanguages();
            var language = allLanguages.FirstOrDefault();

            //if shop have no available languages method generate exception
            if (language == null)
                throw new Exception("Your store have no available language.");

            foreach (var l in allLanguages)
            {
                //check file existing
                bool isLocalizationFileExist = File.Exists(GenerateLocalizationXmlFilePathByCulture(l.LanguageCulture));

                //get localization file path. If no one localization have no appropriate xml file method will install only default english localization
                string localizationPath = isLocalizationFileExist ? GenerateLocalizationXmlFilePathByCulture(l.LanguageCulture) : GenerateLocalizationXmlFilePathByCulture();

                //if file exists it's imports in database
                var localizationFile = File.ReadAllBytes(localizationPath);
                using (var stream = new MemoryStream(localizationFile))
                {
                    using (var sr = new StreamReader(stream, Encoding.UTF8))
                    {
                        _localizationService.ImportResourcesFromXml(l, sr);
                    }
                }
            }
        }

        /// <summary>
        /// Uninstall plugin localization
        /// </summary>
        protected virtual void UninstallLocalization()
        {
            var localizationPath = GenerateLocalizationXmlFilePathByCulture();

            //if standart english localization file isn't exist method generate exception
            if (!File.Exists(localizationPath))
                throw new Exception("File isn't exist.");

            var localizationFile = File.ReadAllBytes(localizationPath);

            using (var stream = new MemoryStream(localizationFile))
            {
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string result = sr.ReadToEnd();
                    XmlDocument xLang = new XmlDocument();
                    xLang.LoadXml(result);

                    //get localization keys
                    XmlNodeList xNodeList = xLang.SelectNodes("Language/LocaleResource");
                    foreach (XmlNode elem in xNodeList)
                        if (elem.Name == "LocaleResource")
                        {
                            var localResource = elem.Attributes["Name"].Value;
                            _localizationService.DeletePluginLocaleResource(localResource);
                        }
                }
            }
        }

        /// <summary>
        /// Check current plugin installation status in 'InstalledPlugins.txt' document
        /// </summary>
        /// <param name="systemName">Plugin system name</param>
        /// <returns>True - when plugin marked like installed</returns>
        protected static bool IsPluginInstalled()
        {
            return IsPluginInstalled(OswPluginDescriptor.SystemName);
        }

        #endregion

        #region Methods

        public override void Install()
        {
            this.InstallLocalization();
            base.Install();
        }

        public override void Uninstall()
        {
            this.UninstallLocalization();
            base.Uninstall();
        }

        /// <summary>
        /// Check InstalledPlugins.txt file list on plugin system name
        /// </summary>
        /// <param name="systemName">Plugin system name</param>
        /// <returns>True when plugin added to list</returns>
        public static bool IsPluginInstalled(string systemName)
        {
            string installedPluginsFile = CommonHelper.DefaultFileProvider.MapPath(NopPluginDefaults.PluginsInfoFilePath);

            if (File.Exists(installedPluginsFile))
            {
                var redisPluginsInfo = JsonConvert.DeserializeObject<PluginsInfo>(File.ReadAllText(installedPluginsFile));


                return redisPluginsInfo.InstalledPluginNames.Contains(systemName);
            }

            return false;
        }

        #endregion
    }
}
