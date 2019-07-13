using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IdentityClient.Sample.Uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            var dontWait = GetParentsAsync();
        }

        private async Task GetParentsAsync()
        {
            IsEnabled = false;
            Parents.Text = "Loading...";
            try
            {
                var client = new HttpClient();

                using (var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:52123/api/parents"))
                {
                    using (var response = await (Application.Current as App).IdentityClient.SendRequestAsync(request))
                    {
                        response.EnsureSuccessStatusCode();

                        JArray parents = JArray.Parse(await response.Content.ReadAsStringAsync());
                        Parents.Text = string.Join(", ", parents.Values<string>());
                    }
                }
            }
            catch (Exception ex)
            {
                Parents.Text = ex.ToString();
            }

            IsEnabled = true;
        }
    }
}
