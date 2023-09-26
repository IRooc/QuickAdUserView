using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Graph.Models.TermStore;
using Microsoft.Identity.Client;
using System.Text;

namespace QuickUserView.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    [BindProperty]
    public string IdInput { get; set; }

    [BindProperty]
    public string SearchInput { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void OnGet()
    {

    }

    public async Task OnPostSearch()
    {
        string search = this.SearchInput;
        if (string.IsNullOrWhiteSpace(search)) return;

        GraphServiceClient client = GetGraphClient();

        string[] props = new string[] { "id", "mail", "accountEnabled", "givenName", "surname", "department", "jobTitle" };
        var i = 0;

        List<Microsoft.Graph.Models.User> foundUsers = new List<Microsoft.Graph.Models.User>();

        var term = search.Trim().Replace("'", "''");
        var filter = $"startswith(givenName,'{term}') or startswith(surname,'{term}') or startswith(mail,'{term}') or displayName:'{term}'";
        var users = await client.Users.GetAsync(config =>
        {
            config.Headers.Add("ConsistencyLevel", "eventual");
            config.QueryParameters.Select = props;
            config.QueryParameters.Count = true;
            config.QueryParameters.Search = $"\"displayName:{search}\" OR \"mail:{search}\"";
            config.QueryParameters.Top = 10;

        });

        if (users?.Value != null) foundUsers.AddRange(users.Value);

        ViewData["UserIds"] = foundUsers.Select(u => u.Id).ToArray();
        ViewData["Users"] = foundUsers;
    }

    public async Task OnPost()
    {
        string ids = this.IdInput;
        if (string.IsNullOrWhiteSpace(ids)) return;

        GraphServiceClient client = GetGraphClient();

        var idlist = ids.Split('\n', ',', ';').Where(l => !string.IsNullOrWhiteSpace(l));

        var userIds = idlist.Select(i => i.Trim()).ToArray();
        string[] props = new string[] { "id", "mail", "accountEnabled", "givenName", "surname", "department", "jobTitle" };
        var i = 0;
        var batchsize = 15;
        List<Microsoft.Graph.Models.User> foundUsers = new List<Microsoft.Graph.Models.User>();
        while (i < userIds.Length)
        {
            var expressions = userIds.Skip(i).Take(batchsize)
                                     .Where(i => Guid.TryParse(i, out _))
                                     .Select(x => $"Id eq '{x}'");

            var filter = string.Join(" or ", expressions);

            var users = await client.Users.GetAsync(config =>
            {
                config.Headers.Add("ConsistencyLevel", "eventual");
                config.QueryParameters.Select = props;
                config.QueryParameters.Count = true;
                config.QueryParameters.Filter = filter;
                config.QueryParameters.Top = batchsize;

            });

            if (users?.Value != null) foundUsers.AddRange(users.Value);
            i += batchsize;
        }
        ViewData["UserIds"] = userIds;
        ViewData["Users"] = foundUsers;
    }

    private GraphServiceClient GetGraphClient()
    {
        var tenantId = _configuration["TenantId"];
        var clientId = _configuration["ClientId"];
        var clientSecret = _configuration["ClientSecret"];

        var confidentialClientApplication = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithClientSecret(clientSecret)
            .Build();

        var scopes = new string[] { "https://graph.microsoft.com/.default" };
        var tokenCredentialOptions = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, tokenCredentialOptions);
        var client = new GraphServiceClient(clientSecretCredential, scopes);
        return client;
    }
}
