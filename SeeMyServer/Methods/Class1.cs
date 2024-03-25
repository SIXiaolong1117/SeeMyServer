using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Microsoft.UI.Xaml.Resources;
using Windows.ApplicationModel.Resources.Core;

namespace SeeMyServer.Methods
{
    public static class LocalizationHelper
    {
        public static void SetLanguage(string language)
        {
            ApplicationLanguages.PrimaryLanguageOverride = language;
            ResourceContext.GetForCurrentView().Reset();
            ResourceContext.GetForViewIndependentUse().Reset();
        }
    }
}
