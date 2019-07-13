using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace IdentityClient.Sample.Uwp
{
    public static class Settings
    {
        public static string GetStoredIdentityTokens()
        {
            try
            {
                return ApplicationData.Current.LocalSettings.Values["StoredIdentity"] as string;
            }
            catch { return null; }
        }

        public static void SaveStoredIdentityTokens(string tokens)
        {
            ApplicationData.Current.LocalSettings.Values["StoredIdentity"] = tokens;
        }
    }
}
