using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.Storage;

namespace SeeMyServer.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        ResourceLoader resourceLoader = new ResourceLoader();

        public SettingsPage()
        {
            this.InitializeComponent();

            materialStatusSet();
            languageStatusSet();
        }
        // 材料ComboBox列表List
        public List<string> material { get; } = new List<string>()
        {
            "Mica",
            "Mica Alt",
            "Acrylic"
        };

        public List<string> language { get; } = new List<string>()
        {
            "简体中文",
            "English"
        };

        private void languageStatusSet()
        {
            if (!languageStatusSetList())
            {
                // 未设置
                localSettings.Values["languageChange"] = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
                languageStatusSetList();
            }
        }
        private bool languageStatusSetList()
        {
            // 读取本地设置数据，调整ComboBox状态
            string languageChangeValue = localSettings.Values["languageChange"] as string;

            switch (languageChangeValue)
            {
                case "zh-Hans-CN":
                    languageChange.SelectedItem = language[0];
                    return true;
                case "en-US":
                    languageChange.SelectedItem = language[1];
                    return true;
                default:
                    return false;
            }
        }
        private void materialStatusSet()
        {
            // 读取本地设置数据，调整ComboBox状态
            string materialStatus = localSettings.Values["materialStatus"] as string;
            switch (materialStatus)
            {
                case "Mica":
                    localSettings.Values["materialStatus"] = "Mica";
                    backgroundMaterial.SelectedItem = material[0];
                    break;
                case "Mica Alt":
                default:
                    localSettings.Values["materialStatus"] = "Mica Alt";
                    backgroundMaterial.SelectedItem = material[1];
                    break;
                case "Acrylic":
                    localSettings.Values["materialStatus"] = "Acrylic";
                    backgroundMaterial.SelectedItem = material[2];
                    break;
            }
        }


        // 背景材料设置ComboBox改动事件
        private void backgroundMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string materialStatus = e.AddedItems[0].ToString();
            switch (materialStatus)
            {
                case "Mica":
                    if (localSettings.Values["materialStatus"] as string != "Mica")
                    {
                        localSettings.Values["materialStatus"] = "Mica";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Mica";
                    }
                    break;
                case "Mica Alt":
                default:
                    if (localSettings.Values["materialStatus"] as string != "Mica Alt")
                    {
                        localSettings.Values["materialStatus"] = "Mica Alt";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Mica Alt";
                    }
                    break;
                case "Acrylic":
                    if (localSettings.Values["materialStatus"] as string != "Acrylic")
                    {
                        localSettings.Values["materialStatus"] = "Acrylic";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Acrylic";
                    }
                    break;
            }
        }

        private void languageChange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string languageStatus = e.AddedItems[0].ToString();
            switch (languageStatus)
            {
                case "简体中文":
                    if (localSettings.Values["languageChange"] as string != "zh-Hans-CN")
                    {
                        localSettings.Values["languageChange"] = "zh-Hans-CN";
                        ApplicationLanguages.PrimaryLanguageOverride = localSettings.Values["languageChange"] as string;
                        Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", localSettings.Values["languageChange"] as string);
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["languageChange"] = "zh-Hans-CN";
                    }
                    break;
                case "English":
                default:
                    if (localSettings.Values["languageChange"] as string != "en-US")
                    {
                        localSettings.Values["languageChange"] = "en-US";
                        ApplicationLanguages.PrimaryLanguageOverride = localSettings.Values["languageChange"] as string;
                        Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", localSettings.Values["languageChange"] as string);
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["languageChange"] = "en-US";
                    }
                    break;
            }
        }
    }
}