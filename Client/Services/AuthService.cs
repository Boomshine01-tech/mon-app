using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SmartNest.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;

        public AuthService(HttpClient httpClient)
        {
            Console.WriteLine("🏗️ AuthService constructor called");
            _httpClient = httpClient;
            Console.WriteLine($"🌐 HttpClient BaseAddress: {_httpClient.BaseAddress}");
        }

        public async Task<AuthResult> RegisterAsync(string email, string password)
        {
            Console.WriteLine("=" .PadRight(60, '='));
            Console.WriteLine("📝 RegisterAsync - START");
            Console.WriteLine("=" .PadRight(60, '='));
            
            try
            {
                Console.WriteLine($"📧 Email: {email}");
                Console.WriteLine($"🔒 Password length: {password?.Length ?? 0}");
                
                var request = new RegisterRequest
                {
                    UserName = email,
                    Password = password!
                };
                
                Console.WriteLine($"📦 Request object created:");
                Console.WriteLine($"   - UserName: {request.UserName}");
                Console.WriteLine($"   - Password: {new string('*', request.Password!.Length)}");

                Console.WriteLine($"🌐 Calling API: Account/Register");
                Console.WriteLine($"🌐 Full URL: {_httpClient.BaseAddress}Account/Register");
                
                var response = await _httpClient.PostAsJsonAsync("Account/Register", request);
                
                Console.WriteLine($"📥 Response received");
                Console.WriteLine($"   - Status Code: {response.StatusCode} ({(int)response.StatusCode})");
                Console.WriteLine($"   - Is Success: {response.IsSuccessStatusCode}");
                Console.WriteLine($"   - Reason Phrase: {response.ReasonPhrase}");

                if (response.IsSuccessStatusCode)
                {
                    var successContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✅ SUCCESS - Response content: {successContent}");
                    Console.WriteLine("=" .PadRight(60, '='));
                    return AuthResult.Success();
                }

                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    var errorMessage = errorResponse?.error ?? "Unknown error";
                    Console.WriteLine($"❌ ERROR (JSON): {errorMessage}");
                    return AuthResult.Failure(errorMessage);
                }
                catch
                {
                    // Si ce n'est pas du JSON, lire comme texte brut
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ ERROR (Text): {errorMessage}");
                    return AuthResult.Failure(string.IsNullOrEmpty(errorMessage) ? "Registration failed" : errorMessage);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ HTTP EXCEPTION:");
                Console.WriteLine($"   - Message: {ex.Message}");
                Console.WriteLine($"   - InnerException: {ex.InnerException?.Message}");
                Console.WriteLine($"   - StackTrace: {ex.StackTrace}");
                Console.WriteLine("=" .PadRight(60, '='));
                return AuthResult.Failure($"Erreur réseau : {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GENERAL EXCEPTION:");
                Console.WriteLine($"   - Type: {ex.GetType().Name}");
                Console.WriteLine($"   - Message: {ex.Message}");
                Console.WriteLine($"   - InnerException: {ex.InnerException?.Message}");
                Console.WriteLine($"   - StackTrace: {ex.StackTrace}");
                Console.WriteLine("=" .PadRight(60, '='));
                return AuthResult.Failure($"Erreur inattendue : {ex.Message}");
            }
        }

        public async Task<AuthResult> LoginAsync(string email, string password, string redirectUrl = "/")
        {
            Console.WriteLine("=" .PadRight(60, '='));
            Console.WriteLine("🔐 LoginAsync - START");
            Console.WriteLine("=" .PadRight(60, '='));
            
            try
            {
                Console.WriteLine($"📧 Email: {email}");
                Console.WriteLine($"🔒 Password length: {password?.Length ?? 0}");
                Console.WriteLine($"🔀 Redirect URL: {redirectUrl}");
                
                var request = new LoginRequest
                {
                    UserName = email,
                    Password = password!,
                    RedirectUrl = redirectUrl
                };
                
                Console.WriteLine($"📦 Request object created");
                Console.WriteLine($"🌐 Calling API: Account/Login");
                
                var response = await _httpClient.PostAsJsonAsync("Account/Login", request);
                
                Console.WriteLine($"📥 Response received");
                Console.WriteLine($"   - Status Code: {response.StatusCode} ({(int)response.StatusCode})");
                Console.WriteLine($"   - Is Success: {response.IsSuccessStatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var successContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✅ SUCCESS - Response content: {successContent}");
                    Console.WriteLine("=" .PadRight(60, '='));
                    return AuthResult.Success();
                }

                var errorMessage = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ ERROR - Response content: {errorMessage}");
                Console.WriteLine("=" .PadRight(60, '='));
                return AuthResult.Failure(errorMessage);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ HTTP EXCEPTION: {ex.Message}");
                Console.WriteLine("=" .PadRight(60, '='));
                return AuthResult.Failure($"Erreur réseau : {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GENERAL EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine("=" .PadRight(60, '='));
                return AuthResult.Failure($"Erreur inattendue : {ex.Message}");
            }
        }

        public async Task<bool> LogoutAsync()
        {
            Console.WriteLine("🚪 LogoutAsync called");
            try
            {
                var response = await _httpClient.PostAsync("Account/Logout", null);
                Console.WriteLine($"📥 Logout response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Logout exception: {ex.Message}");
                return false;
            }
        }

        // Classes internes pour les requêtes
        private class RegisterRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        private class ErrorResponse
        {
            public string error { get; set; } = string.Empty;
        }

        private class LoginRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string RedirectUrl { get; set; } = "/";
        }
    }

    // Classe pour les résultats d'authentification
    public class AuthResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        private AuthResult(bool isSuccess, string errorMessage = "")
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static AuthResult Success()
        {
            Console.WriteLine("✅ AuthResult.Success created");
            return new AuthResult(true);
        }
        
        public static AuthResult Failure(string errorMessage)
        {
            Console.WriteLine($"❌ AuthResult.Failure created: {errorMessage}");
            return new AuthResult(false, errorMessage);
        }
    }
}