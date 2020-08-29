using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using WebSite.Models;

namespace WebSite.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private IConfiguration _configuration;

        public const string SQL_IMPERSONATION_SCOPE = "https://database.windows.net/user_impersonation";

#if newMSAL
        private Microsoft.Identity.Web.ITokenAcquisition _tokenAcquisition;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, Microsoft.Identity.Web.ITokenAcquisition tokenAcquisition)
        {
            _logger = logger;
            _configuration = configuration;
            _tokenAcquisition = tokenAcquisition;
        }
#else
        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
#endif

        [Authorize]
        public async Task<IActionResult> Index()
        {
#if newMSAL
            string accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new string[] {SQL_IMPERSONATION_SCOPE});
            ViewData["TokenType"] = "User New MSAL";
#else
            //Get the access token used to call this API
            string token = HttpContext.User.FindFirstValue("access_token");
            string assertionType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
            ConfidentialClientApplicationBuilder confidentAppBuilder = ConfidentialClientApplicationBuilder.Create(_configuration["AzureAd:ClientId"]).WithClientSecret(_configuration["AzureAd:ClientSecret"]);
            var app = confidentAppBuilder.Build();

            var userAssertion = new UserAssertion(token, assertionType);
            var sqlAuthRequest = app.AcquireTokenOnBehalfOf(new string[] { SQL_IMPERSONATION_SCOPE }, userAssertion).WithAuthority($"{_configuration["AzureAd:Instance"]}{_configuration["AzureAd:TenantId"]}/oauth2/authorize");
            var authRetVal = await sqlAuthRequest.ExecuteAsync();
            string accessToken = authRetVal.AccessToken;
            ViewData["TokenType"] = "User";
#endif
            using (SqlConnection conn = new SqlConnection(_configuration["ConnectionString"]))
            {
                conn.AccessToken = accessToken;
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("SELECT SYSTEM_USER", conn))
                {
                    var userName = await cmd.ExecuteScalarAsync();
                    ViewData["SQLUserName"] = userName.ToString();
                }
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
