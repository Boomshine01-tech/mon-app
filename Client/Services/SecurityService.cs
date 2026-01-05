using System;
using System.Web;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using Radzen;

using SmartNest.Server.Models;

namespace SmartNest.Client
{
    public partial class SecurityService
    {

        private readonly HttpClient httpClient;

        private readonly Uri baseUri;

        private readonly NavigationManager navigationManager;

        public ApplicationUser User { get; private set; } = new ApplicationUser { Name = "Anonymous" };
        

        public ClaimsPrincipal Principal { get; private set; } = new ClaimsPrincipal(new ClaimsIdentity());


        private bool IsDevelopment()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
        public SecurityService(NavigationManager navigationManager, IHttpClientFactory factory)
        {
            this.baseUri = new Uri($"{navigationManager.BaseUri}odata/Identity/");
            this.httpClient = factory.CreateClient("SmartNest.Server");
            this.navigationManager = navigationManager;
        }

        public bool IsInRole(params string[] roles)
        {
#if DEBUG
            if (User.Name == "admin")
            {
                return true;
            }
#endif

            if (roles.Contains("Everybody"))
            {
                return true;
            }

            if (!IsAuthenticated())
            {
                return false;
            }

            if (roles.Contains("Authenticated"))
            {
                return true;
            }

            return roles.Any(role => Principal.IsInRole(role));
        }

        public bool IsAuthenticated()
        {
            return Principal?.Identity?.IsAuthenticated == true;
        }

        public async Task InitializeAsync(AuthenticationState result)
{
    try
    {
        Console.WriteLine("🔄 InitializeAsync appelé");
        
        var user = result.User;
        Console.WriteLine($"   User authenticated: {user?.Identity?.IsAuthenticated}");
        
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst("sub")?.Value 
                      ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("id")?.Value;
            
            Console.WriteLine($"   User ID trouvé: {userId ?? "NULL"}");
            
            if (!string.IsNullOrEmpty(userId))
            {
                var appUser = await GetUserById(userId);
                
                if (appUser != null)
                {
                    Console.WriteLine($"✅ Utilisateur chargé: {appUser.UserName}");
                    // Stocker l'utilisateur si nécessaire
                }
                else
                {
                    Console.WriteLine("⚠️  GetUserById a retourné null");
                }
            }
            else
            {
                Console.WriteLine("⚠️  Impossible de trouver l'ID utilisateur dans les claims");
            }
        }
        else
        {
            Console.WriteLine("ℹ️  Utilisateur non authentifié");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERREUR InitializeAsync: {ex.Message}");
        Console.WriteLine($"   Type: {ex.GetType().Name}");
        Console.WriteLine($"   Stack: {ex.StackTrace}");
        
        // ⚠️ IMPORTANT : Ne pas relancer l'exception
        // Laissez l'app continuer même si l'init échoue
    }
}


        public ApplicationTenant Tenant { get; set; }

        public async Task<ApplicationAuthenticationState> GetAuthenticationStateAsync()
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/CurrentUser");

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, uri));

            return await response.ReadAsync<ApplicationAuthenticationState>();
        }

        public void Logout()
        {
            navigationManager.NavigateTo("Account/Logout", true);
        }

        public void Login()
        {
            navigationManager.NavigateTo("Login", true);
        }

        public async Task<IEnumerable<ApplicationRole>> GetRoles()
        {
            var uri = new Uri(baseUri, $"ApplicationRoles");

            uri = uri.GetODataUri(filter: $"TenantId eq {Tenant.Id}");

            var response = await httpClient.GetAsync(uri);

            var result = await response.ReadAsync<ODataServiceResult<ApplicationRole>>();

            return result.Value;
        }

        public async Task<ApplicationRole> CreateRole(ApplicationRole role)
        {

            if(Tenant != null)
            {
                role.TenantId = Tenant.Id;
            }
            var uri = new Uri(baseUri, $"ApplicationRoles");

            var content = new StringContent(ODataJsonSerializer.Serialize(role), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(uri, content);

            return await response.ReadAsync<ApplicationRole>();
        }

        public async Task<HttpResponseMessage> DeleteRole(string id)
        {
            var uri = new Uri(baseUri, $"ApplicationRoles('{id}')");

            return await httpClient.DeleteAsync(uri);
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsers()
        {
            var uri = new Uri(baseUri, $"ApplicationUsers");


            uri = uri.GetODataUri(filter: $"TenantId eq {Tenant.Id}");

            var response = await httpClient.GetAsync(uri);

            var result = await response.ReadAsync<ODataServiceResult<ApplicationUser>>();

            return result.Value;
        }

        public async Task<ApplicationUser> CreateUser(ApplicationUser user)
        {

            if(Tenant != null)
            {
                user.TenantId = Tenant.Id;
            }
            var uri = new Uri(baseUri, $"ApplicationUsers");

            var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(uri, content);

            return await response.ReadAsync<ApplicationUser>();
        }

        public async Task<HttpResponseMessage> DeleteUser(string id)
        {
            var uri = new Uri(baseUri, $"ApplicationUsers('{id}')");

            return await httpClient.DeleteAsync(uri);
        }

        public async Task<ApplicationUser?> GetUserById(string id)
{
    try
    {
        Console.WriteLine($"🔍 GetUserById appelé avec ID: {id}");
        
        var url = $"/api/account/user/{id}";
        Console.WriteLine($"🌐 URL: {url}");
        
        var response = await httpClient.GetAsync(url);
        Console.WriteLine($"📊 Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"❌ Erreur API: {errorContent}");
            return null; // ⚠️ Retourner null au lieu de crasher
        }
        
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"✅ Réponse reçue (longueur: {content.Length})");
        
        var user = await response.Content.ReadFromJsonAsync<ApplicationUser>();
        Console.WriteLine($"✅ User parsé: {user?.UserName ?? "NULL"}");
        
        return user;
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ Erreur HTTP: {ex.Message}");
        return null; // ⚠️ Ne pas crasher l'app
    }
    catch (System.Text.Json.JsonException ex)
    {
        Console.WriteLine($"❌ Erreur parsing JSON: {ex.Message}");
        return null; // ⚠️ Ne pas crasher l'app
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erreur inattendue: {ex.Message}");
        Console.WriteLine($"   Stack: {ex.StackTrace}");
        return null; // ⚠️ Ne pas crasher l'app
    }
}

        public async Task<ApplicationUser> UpdateUser(string id, ApplicationUser user)
        {
            var uri = new Uri(baseUri, $"ApplicationUsers('{id}')");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri)
            {
                Content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await response.ReadAsync<ApplicationUser>();
        }

        // Tenants
        public async System.Threading.Tasks.Task<IEnumerable<ApplicationTenant>> GetTenants()
        {
            var uri = new Uri(baseUri, $"ApplicationTenants");
            uri = uri.GetODataUri();

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            var response = await httpClient.SendAsync(httpRequestMessage);

            var result = await response.ReadAsync<ODataServiceResult<ApplicationTenant>>();

            return result.Value;
        }

        public async System.Threading.Tasks.Task<ApplicationTenant> CreateTenant(ApplicationTenant? tenant = default(ApplicationTenant))
        {
            var uri = new Uri(baseUri, $"ApplicationTenants");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uri);

            httpRequestMessage.Content = new StringContent(ODataJsonSerializer.Serialize(tenant), Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await response.ReadAsync<ApplicationTenant>();
        }

        public async System.Threading.Tasks.Task<HttpResponseMessage> DeleteTenant(string? id = default(string))
        {
            var uri = new Uri(baseUri, $"ApplicationTenants({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        public async System.Threading.Tasks.Task<ApplicationTenant> GetTenantById(string? id = default(string))
        {
            var uri = new Uri(baseUri, $"ApplicationTenants({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            var response = await httpClient.SendAsync(httpRequestMessage);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            return await response.ReadAsync<ApplicationTenant>();
        }

        public async System.Threading.Tasks.Task<HttpResponseMessage> UpdateTenant(string? id = default(string), ApplicationTenant? tenant = default(ApplicationTenant))
        {
            var uri = new Uri(baseUri, $"ApplicationTenants({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri);

            httpRequestMessage.Content = new StringContent(ODataJsonSerializer.Serialize(tenant), Encoding.UTF8, "application/json");

            return await httpClient.SendAsync(httpRequestMessage);
        }
        public async Task ChangePassword(string oldPassword, string newPassword)
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/ChangePassword");

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "oldPassword", oldPassword },
                { "newPassword", newPassword }
            });

            var response = await httpClient.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                throw new ApplicationException(message);
            }
        }

        public async Task Register(string userName, string password)
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/Register");

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "userName", userName },
                { "password", password }
            });

            var response = await httpClient.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                throw new ApplicationException(message);
            }
        }

        public async Task ResetPassword(string userName)
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/ResetPassword");

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "userName", userName }
            });

            var response = await httpClient.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                throw new ApplicationException(message);
            }
        }
    }
}
