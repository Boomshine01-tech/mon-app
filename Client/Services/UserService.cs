using System.Net.Http.Json;

namespace SmartNest.Client.Services
{
    public class UserService
    {
        private readonly HttpClient _httpClient;

        public UserService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<UserDto>> GetUsersAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<UserDto>>("api/users") ?? new List<UserDto>();
        }

        public async Task<UserDetailDto?> GetUserAsync(string id)
        {
            return await _httpClient.GetFromJsonAsync<UserDetailDto>($"api/users/{id}");
        }

        public async Task<HttpResponseMessage> CreateUserAsync(CreateUserDto model)
        {
            return await _httpClient.PostAsJsonAsync("api/users", model);
        }

        public async Task<HttpResponseMessage> UpdateUserAsync(string id, UpdateUserDto model)
        {
            return await _httpClient.PutAsJsonAsync($"api/users/{id}", model);
        }

        public async Task<HttpResponseMessage> DeleteUserAsync(string id)
        {
            return await _httpClient.DeleteAsync($"api/users/{id}");
        }

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoleDto>>("api/users/roles") ?? new List<RoleDto>();
        }
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class UserDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> RoleIds { get; set; } = new();
    }

    public class CreateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public List<string> RoleIds { get; set; } = new();
    }

    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public List<string> RoleIds { get; set; } = new();
    }

    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}