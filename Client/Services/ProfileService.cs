using System.Net.Http.Json;

namespace SmartNest.Client.Services
{
    public class ProfileService
    {
        private readonly HttpClient _httpClient;

        public ProfileService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<UserProfileDto?> GetCurrentUserAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("Account/CurrentUser", null);
        
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserProfileDto>();
                }
        
                Console.WriteLine($"Failed to get user profile. Status: {response.StatusCode}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return null;
            }
        }

        public async Task<HttpResponseMessage> ChangePasswordAsync(ChangePasswordDto model)
        {
            return await _httpClient.PostAsJsonAsync("api/profile/change-password", model);
        }
    }

    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class IdentityErrorDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}