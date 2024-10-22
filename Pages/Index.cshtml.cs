using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Graph.Models;
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

    [BindProperty]
    public bool UseSearch { get; set; }

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
        List<User> foundUsers = await SearchByString(search);

        ViewData["UserIds"] = foundUsers.Select(u => u.Id).ToArray();
        ViewData["Users"] = foundUsers;
    }

    private async Task<List<User>> SearchByString(string search)
    {
        GraphServiceClient client = GetGraphClient();

        string[] props = new string[] { "id", "mail", "accountEnabled", "givenName", "surname", "department", "jobTitle", "EmployeeId" };
        var i = 0;

        List<User> foundUsers = new List<User>();

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
        return foundUsers;
    }

    public async Task OnPost()
    {
        string ids = this.IdInput;
        List<User> foundUsers = new List<User>();
        List<string> multiple = new List<string>();
        List<string> notfound = new List<string>();
        if (string.IsNullOrWhiteSpace(ids)) return;

        var idlist = ids.Split('\n', ',', ';').Where(l => !string.IsNullOrWhiteSpace(l)).Select(s => s.Trim());

        if (this.UseSearch)
        {
            foreach (var id in idlist)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                try
                {
                    var us = await SearchByString(id);
                    if (us.Count > 1)
                    {
                        multiple.Add($"{id} (multiple found {us.Count}: {string.Join(", ", us.Select(u => u.GivenName + " " + u.Surname + " " + u.Mail).ToArray())} )");
                    }
                    else if (us.Count == 0)
                    {
                        notfound.Add(id + " (not found)");
                    }
                    else
                    {
                        foundUsers.Add(us.Single());
                    }
                }catch(Exception ex)
                {
                    notfound.Add(id);
                    Console.WriteLine("FAILED TO FIND " + id);
                }
            }
            ViewData["UserIds"] = foundUsers.Select(f => f.Mail).ToArray().Concat(multiple).Concat(notfound).ToArray();
            ViewData["Users"] = foundUsers;
            return;
        }

        GraphServiceClient client = GetGraphClient();


        var userIds = idlist.Select(i => i.Trim()).ToArray();
        string[] props = new string[] { "id", "mail", "accountEnabled", "givenName", "surname", "department", "jobTitle", "EmployeeId" };
        var i = 0;
        var batchsize = 15;
        while (i < userIds.Length)
        {
            var hasEmail = ids.IndexOf("@") > 0;

            var expressions = userIds.Skip(i).Take(batchsize)
                                     .Select(x =>
                                         {
                                             if (x.IndexOf("@") > -1) return $"mail eq '{x}'";
                                             else if (long.TryParse(x, out _)) return $"employeeId eq '{x}'";
                                             return $"id eq '{x}'";
                                         });

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
