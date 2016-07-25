using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PokemonGo.RocketAPI.Helpers
{
    public static class HttpClientHelper
    {
        public static async Task<TResponse> PostFormEncodedAsync<TResponse>(string url, ISettings settings, params KeyValuePair<string, string>[] keyValuePairs)
        {

            string proxyUri = settings.ProxyUri;

            NetworkCredential proxyCreds = new NetworkCredential(
                settings.ProxyLogin,
                settings.ProxyPass
            );

            WebProxy proxy = new WebProxy(proxyUri, false)
            {
                UseDefaultCredentials = false,
                Credentials = proxyCreds,
            };

            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip,
                AllowAutoRedirect = false,
                Proxy = settings.UseProxy ? proxy : null
            };

            using (var tempHttpClient = new HttpClient(handler))
            {
                var response = await tempHttpClient.PostAsync(url, new FormUrlEncodedContent(keyValuePairs));
                return await response.Content.ReadAsAsync<TResponse>();
            }
        }
    }
}
