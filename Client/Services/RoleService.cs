using System.Net.Http.Json;
using SmartNest.Server.Models;

namespace SmartNest.Client.Services
{
    public class RoleService
    {
        private readonly HttpClient _httpClient;

        public RoleService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<ApplicationRole>> GetRolesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<ApplicationRole>>("api/roles") ?? new List<ApplicationRole>();
        }

        public async Task<ApplicationRole?> GetRoleAsync(string id)
        {
            return await _httpClient.GetFromJsonAsync<ApplicationRole>($"api/roles/{id}");
        }

        public async Task<HttpResponseMessage> CreateRoleAsync(ApplicationRole role)
        {
            return await _httpClient.PostAsJsonAsync("api/roles", role);
        }

        public async Task<HttpResponseMessage> DeleteRoleAsync(string id)
        {
            return await _httpClient.DeleteAsync($"api/roles/{id}");
        }
    }
}