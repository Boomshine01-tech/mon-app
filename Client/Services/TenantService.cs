using System.Net.Http.Json;

namespace SmartNest.Client.Services
{
    public class TenantService
    {
        private readonly HttpClient _httpClient;

        public TenantService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<TenantDto>> GetTenantsAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<TenantDto>>("api/tenants") ?? new List<TenantDto>();
        }

        public async Task<TenantDto?> GetTenantAsync(string id)
        {
            return await _httpClient.GetFromJsonAsync<TenantDto>($"api/tenants/{id}");
        }

        public async Task<HttpResponseMessage> CreateTenantAsync(CreateTenantDto model)
        {
            return await _httpClient.PostAsJsonAsync("api/tenants", model);
        }

        public async Task<HttpResponseMessage> UpdateTenantAsync(string id, UpdateTenantDto model)
        {
            return await _httpClient.PutAsJsonAsync($"api/tenants/{id}", model);
        }

        public async Task<HttpResponseMessage> DeleteTenantAsync(string id)
        {
            return await _httpClient.DeleteAsync($"api/tenants/{id}");
        }
    }

    public class TenantDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Hosts { get; set; } = string.Empty;
    }

    public class CreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string Hosts { get; set; } = string.Empty;
    }

    public class UpdateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string Hosts { get; set; } = string.Empty;
    }
}