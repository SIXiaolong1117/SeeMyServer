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
        // ���ñ�����������
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        ResourceLoader resourceLoader = new ResourceLoader();

        public SettingsPage()
        {
            this.InitializeComponent();

            materialStatusSet();
            languageStatusSet();
        }
        // ����ComboBox�б�List
        public List<string> material { get; } = new List<string>()
        {
            "Mica",
            "Mica Alt",
            "Acrylic"
        };

        public List<string> language { get; } = new List<string>()
        {
            "�������ģ��й���",
            "English (US)"
        };

        private void languageStatusSet()
        {
            if (!languageStatusSetList())
            {
                // δ����
                localSettings.Values["languageChange"] = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
                // �Ƿ����룬�ӳ�����
                //throw new Exception(Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                languageStatusSetList();
            }
        }
        private bool languageStatusSetList()
        {
            // ��ȡ�����������ݣ�����ComboBox״̬
            if (localSettings.Values["languageChange"] as string == "zh-Hans-CN")
            {
                languageChange.SelectedItem = language[0];
                return true;
            }
            else if (localSettings.Values["languageChange"] as string == "en-US")
            {
                languageChange.SelectedItem = language[1];
                return true;
            }
            else
            {
                return false;
            }
        }

        private void materialStatusSet()
        {
            // ��ȡ�����������ݣ�����ComboBox״̬
            if (localSettings.Values["materialStatus"] as string == "Mica")
            {
                backgroundMaterial.SelectedItem = material[0];
            }
            else if (localSettings.Values["materialStatus"] as string == "Mica Alt")
            {
                backgroundMaterial.SelectedItem = material[1];
            }
            else if (localSettings.Values["materialStatus"] as string == "Acrylic")
            {
                backgroundMaterial.SelectedItem = material[2];
            }
            else
            {
                // �Ƿ����룬����Ĭ�ϲ���ΪMica Alt
                localSettings.Values["materialStatus"] = "Mica Alt";
                backgroundMaterial.SelectedItem = material[1];
                // �Ƿ����룬�ӳ�����
                //throw new Exception($"Wrong material type: {localSettings.Values["materialStatus"]}");
            }
        }

        // ������������ComboBox�Ķ��¼�
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
                default:
                    throw new Exception($"Invalid argument: {materialStatus}");
            }
        }

        private void languageChange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string languageStatus = e.AddedItems[0].ToString();
            switch (languageStatus)
            {
                case "�������ģ��й���":
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
                case "English (US)":
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
                default:
                    throw new Exception($"Invalid argument: {languageChange}");
            }
        }
    }
}