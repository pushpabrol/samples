using System;
using System.Collections.Generic;
using System.Linq;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Threading.Tasks;

namespace SampleMvcApp.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, Duration = 0, NoStore = true)]
    public class HomeController : Controller
    {

        private IConfiguration _configuration;

        public HomeController(IConfiguration Configuration)
        {
            _configuration = Configuration;
        }

        [Authorize]
        public IActionResult Index()
        {
            var ssoClients = User.Claims.FirstOrDefault(c => c.Type == "current_clients_alt")?.Value;
            ViewBag.NoClients = 0;
            if (ssoClients != null && ssoClients.Count() > 0)
            {
                var clientsData = JsonConvert.DeserializeObject<Dictionary<string, ClientInfo>>(ssoClients);
                var hasSaml = clientsData.Any(x => x.Value.appType == "samlp");
                ViewBag.hasSaml = hasSaml;
                ViewBag.hasWSFed = clientsData.Any(x => x.Value.appType == "wsfed");
                ViewBag.NoClients = clientsData.Count();
                ViewBag.Clients = clientsData;
                ViewBag.ClientsJson = JsonConvert.SerializeObject(clientsData, Formatting.Indented);

            }
            else {
                ViewBag.Clients = new Dictionary<string,ClientInfo>();
                ViewBag.ClientsJson = new Object();
            }
            ViewBag.ClientId = _configuration["Auth0:ClientId"];
            ViewBag.BaseUrl = HttpContext.Request.BaseUrl() + "Home/LoggedOut";
            ViewBag.IssuerDomain = "https://" + _configuration["Auth0:Domain"];
            ViewBag.returnTo = HttpContext.Request.Query["returnTo"];
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
        public IActionResult LoggedOut()
        {
            return View();
        }


        public async Task<ActionResult> ClientsAsync()
        {

            var client = new HttpClient();
            
                client.BaseAddress = new System.Uri($"https://{_configuration["Auth0:Domain"]}/");
                var response = await client.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                            { "grant_type", "client_credentials" },
                            { "client_id",  _configuration["Auth0:MgmtApiClientId"] },
                            { "client_secret",  _configuration["Auth0:MgmtApiClientSecret"] },
                            { "audience", $"https://{_configuration["Auth0:Domain"]}/api/v2/" }
                    }
                ));

                var content = await response.Content.ReadAsStringAsync();
                var jsonResult = JObject.Parse(content);

                var mgmtToken = jsonResult["access_token"].Value<string>();
                using (var mgmtClient = new ManagementApiClient(mgmtToken, new System.Uri($"https://{_configuration["Auth0:Domain"]}/api/v2")))
                {

                var request = new GetClientsRequest();
                request.Fields = "client_id";
                request.IncludeFields = true;
                
                var clients = await mgmtClient.Clients.GetAllAsync(request, pagination: new Auth0.ManagementApi.Paging.PaginationInfo(0, 1, true));
                var total = clients.Paging.Total;
                int perPage = 100;
                List<Client> totalCLients = new List<Client>();

                var totalPages = (int)Math.Ceiling((decimal)total / (decimal)perPage);

                    for (int i = 0; i < totalPages; i++)
                    {

                        request = new GetClientsRequest();
                        request.Fields = "name,client_id,addons,client_metadata";
                        request.IncludeFields = true;
                        var clientsAgain = await mgmtClient.Clients.GetAllAsync(request, pagination: new Auth0.ManagementApi.Paging.PaginationInfo(i, perPage, false));
                        totalCLients.AddRange(clientsAgain);

                    }
                
                var clientsSAML = totalCLients.Where(x => x.AddOns is not null && x.AddOns.SamlP is not null);
                var clientsWsFed = totalCLients.Where(x => x.AddOns is not null && x.AddOns.WsFed is not null);
                //var clientsFiltered = totalCLients.Where(x => x.ClientMetaData is not null && x.ClientMetaData["appType"] != null);
                ViewBag.clientsFiltered = clientsSAML.Union(clientsWsFed).DistinctBy((Client arg) => arg.ClientId).Where(x => x.ClientMetaData is not null && x.ClientMetaData["appType"] != null);
                return View();

            }


        }
    }

     

    public class ClientInfo
    {
        public ClientInfo() {
            this.id = Guid.NewGuid();
        }
        public Guid id { get; set; }
        [JsonProperty("name")]
        public string name { get; set; }
        [JsonProperty("logout_url")]
        public string logoutUrl { get; set; }
        [JsonProperty("appType")]
        public string appType { get; set; }
    }
}

public static class HttpRequestExtensions
{
    public static string BaseUrl(this HttpRequest req)
    {
        if (req == null) return null;
        var uriBuilder = new UriBuilder(req.Scheme, req.Host.Host, req.Host.Port ?? -1);
        if (uriBuilder.Uri.IsDefaultPort)
        {
            uriBuilder.Port = -1;
        }

        return uriBuilder.Uri.AbsoluteUri;
    }
}
