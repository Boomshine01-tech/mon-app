using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Models;
using SmartNest.Server.Services;

namespace SmartNest.Server.Controllers
{
    [Route("Account/[action]")]
    public partial class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<ApplicationRole> roleManager;
        private readonly IWebHostEnvironment env;
        private readonly IConfiguration configuration;
        private readonly Data.ApplicationDbContext context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            Data.ApplicationDbContext context,
            IWebHostEnvironment env,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🏗️ AccountController constructor called");
            Console.WriteLine("============================================================");

            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.env = env;
            this.configuration = configuration;
            this.context = context;
            this._logger = logger;

            Console.WriteLine("📦 Dependencies injected successfully");
            Console.WriteLine($"🌐 Environment: {env.EnvironmentName}");
            Console.WriteLine("============================================================");
        }

        private IActionResult RedirectWithError(string error, string? redirectUrl = null)
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("🔁 RedirectWithError called");
            Console.WriteLine($"❗ Error: {error}");
            Console.WriteLine($"➡️ RedirectUrl: {redirectUrl}");
            Console.WriteLine("------------------------------------------------------------");

            if (!string.IsNullOrEmpty(redirectUrl))
            {
                var final = $"~/Login?error={error}&redirectUrl={Uri.EscapeDataString(redirectUrl)}";
                Console.WriteLine($"🔀 Redirecting to: {final}");
                return Redirect(final);
            }
            else
            {
                var final = $"~/Login?error={error}";
                Console.WriteLine($"🔀 Redirecting to: {final}");
                return Redirect(final);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🔐 [GET] Login");
            Console.WriteLine($"➡️ Incoming returnUrl: {returnUrl}");
            Console.WriteLine("============================================================");

            if (returnUrl != "/" && !string.IsNullOrEmpty(returnUrl))
            {
                Console.WriteLine("🔄 Redirecting to login UI with redirectUrl");
                return Redirect($"~/Login?redirectUrl={Uri.EscapeDataString(returnUrl)}");
            }

            Console.WriteLine("➡️ Redirect to default login page");
            return Redirect("~/Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string userName, string password, string redirectUrl)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🔐 [POST] Login START");
            Console.WriteLine("============================================================");

            Console.WriteLine($"📧 UserName: {userName}");
            Console.WriteLine($"🔒 Password length: {password?.Length ?? 0}");
            Console.WriteLine($"➡️ RedirectUrl: {redirectUrl}");

            try
            {
                // ------------------------------------------------------------
                // DEV MODE - AUTO LOGIN
                // ------------------------------------------------------------
                if (env.EnvironmentName == "Development" && userName == "admin" && password == "admin")
                {
                    Console.WriteLine("🛠️ DEV MODE LOGIN TRIGGERED");

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, "admin"),
                        new Claim(ClaimTypes.Email, "admin")
                    };

                    Console.WriteLine("🔍 Loading roles...");
                    var allRoles = await roleManager.Roles.ToListAsync();
                    Console.WriteLine($"📦 Found {allRoles.Count} roles");

                    foreach (var role in allRoles)
                    {
                        Console.WriteLine($"   ➕ Adding role claim: {role.Name}");
                        claims.Add(new Claim(ClaimTypes.Role, role.Name!));
                    }

                    Console.WriteLine("🔐 Signing in with DEV admin...");
                    await signInManager.SignInWithClaimsAsync(
                        new ApplicationUser { UserName = userName, Email = userName },
                        isPersistent: false,
                        claims);

                    Console.WriteLine("✅ DEV login success");
                    Console.WriteLine("============================================================");
                    return Redirect($"~/{redirectUrl}");
                }

                // ------------------------------------------------------------
                // CHECK INPUTS
                // ------------------------------------------------------------
                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("❌ Username or password empty");
                    return RedirectWithError("Invalid user or password", redirectUrl);
                }

                Console.WriteLine("🔍 Searching user in database...");
                var user = await userManager.FindByNameAsync(userName);

                if (user == null)
                {
                    Console.WriteLine("❌ User not found");
                    return RedirectWithError("Invalid user or password", redirectUrl);
                }

                if (!user.EmailConfirmed)
                {
                    Console.WriteLine("❌ Email not confirmed");
                    return RedirectWithError("User email not confirmed", redirectUrl);
                }

                Console.WriteLine("🔐 Checking password...");
                var result = await signInManager.PasswordSignInAsync(userName, password, false, false);

                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Password correct");

                    var userId = user.Id;
                    Console.WriteLine($"🆔 User ID: {userId}");

                    // MQTT CONNECTION
                    Console.WriteLine("🔌 Connecting user to MQTT broker...");
                    try
                    {
                        var mqttService = HttpContext.RequestServices.GetRequiredService<MqttService>();
                        await mqttService.ConnectUserBroker(userId, "192.168.1.7", 1883);
                        Console.WriteLine("📡 MQTT connection success");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("⚠️ MQTT connection failed");
                        Console.WriteLine($"   Message: {ex.Message}");
                        Console.WriteLine($"   Stack: {ex.StackTrace}");
                    }

                    // TENANT VALIDATION
                    Console.WriteLine("🏢 Checking tenant restrictions...");
                    if (user.TenantId != null)
                    {
                        var tenant = await context.Tenants
                            .Where(t => t.Id == user.TenantId)
                            .FirstOrDefaultAsync();

                        if (tenant != null)
                        {
                            var hostMatches = tenant.Hosts
                                .Split(',')
                                .Any(h => h.Trim().Contains(HttpContext.Request.Host.Value!));

                            if (!hostMatches)
                            {
                                Console.WriteLine("❌ Host mismatch — kicking user out");
                                await signInManager.SignOutAsync();
                                return RedirectWithError("Invalid user or password", redirectUrl);
                            }
                        }
                    }

                    Console.WriteLine("🔁 Redirecting user after login...");
                    Console.WriteLine("============================================================");
                    return Redirect($"~/{redirectUrl}");
                }

                Console.WriteLine("❌ Login failed — wrong password");
                return RedirectWithError("Invalid user or password", redirectUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("💥 LOGIN EXCEPTION");
                Console.WriteLine($"❗ Type: {ex.GetType().Name}");
                Console.WriteLine($"❗ Message: {ex.Message}");
                Console.WriteLine($"❗ Stack: {ex.StackTrace}");
                Console.WriteLine("============================================================");
                return RedirectWithError($"An error occurred: {ex.Message}", redirectUrl);
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🔐 ChangePassword START");
            Console.WriteLine("============================================================");
            try
            {
                Console.WriteLine($"🔒 Old password length: {oldPassword?.Length ?? 0}");
                Console.WriteLine($"🔒 New password length: {newPassword?.Length ?? 0}");

                if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
                {
                    Console.WriteLine("❌ Invalid input: old or new password is empty");
                    return BadRequest("Invalid password");
                }

                var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Console.WriteLine($"🆔 Current user id from claim: {id}");
                if(id!=null)
                {
                    
                }
                var user = await userManager.FindByIdAsync(id!);

                if (user == null)
                {
                    Console.WriteLine("❌ User not found for given id");
                    return BadRequest("User not found");
                }

                Console.WriteLine("🔄 Attempting to change password via UserManager...");
                var result = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);

                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Password change succeeded");
                    return Ok();
                }

                var message = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
                Console.WriteLine($"❌ Password change failed: {message}");
                return BadRequest(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 ChangePassword EXCEPTION");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return BadRequest($"Exception: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("🔐 ChangePassword END");
                Console.WriteLine("============================================================");
            }
        }

        [HttpPost]
        public ApplicationAuthenticationState CurrentUser()
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("👤 CurrentUser called");
            Console.WriteLine($"   IsAuthenticated: {User.Identity!.IsAuthenticated}");
            Console.WriteLine($"   Name: {User.Identity.Name}");
            Console.WriteLine("============================================================");

            return new ApplicationAuthenticationState
            {
                IsAuthenticated = User.Identity.IsAuthenticated,
                Name = User.Identity.Name!,
                Claims = User.Claims.Select(c => new ApplicationClaim { Type = c.Type, Value = c.Value })
            };
        }

        public async Task<IActionResult> Logout()
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🚪 Logout START");
            Console.WriteLine("============================================================");

            try
            {
                await signInManager.SignOutAsync();
                Console.WriteLine("✅ SignOutAsync completed");
                return Redirect("~/");
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 Logout EXCEPTION");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return StatusCode(500, "Logout failed");
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("🚪 Logout END");
                Console.WriteLine("============================================================");
            }
        }

        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            Console.WriteLine("=" .PadRight(60, '='));
            Console.WriteLine("📝 REGISTRATION START");
            Console.WriteLine("=" .PadRight(60, '='));

            try
            {
                // AJOUT : Loguer immédiatement ce qui est reçu
                Console.WriteLine($"📦 Raw request received: {request != null}");
                // Test 1: Request null?
                if (request == null)
                {
                    Console.WriteLine("❌ TEST 1 FAILED: Request is NULL");
                    return BadRequest(new { error = "Request is null" });
                }
                Console.WriteLine("✅ TEST 1 PASSED: Request not null");
                Console.WriteLine($"📧 Request.UserName: {request.UserName}");
                Console.WriteLine($"🔒 Request.Password length: {request.Password?.Length ?? 0}");

                // Test 2: UserName empty?
                if (string.IsNullOrWhiteSpace(request.UserName))
                {
                    Console.WriteLine("❌ TEST 2 FAILED: UserName is empty");
                    return BadRequest(new { error = "UserName is required" });
                }
                Console.WriteLine("✅ TEST 2 PASSED: UserName provided");

                // Test 3: Password empty?
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    Console.WriteLine("❌ TEST 3 FAILED: Password is empty");
                    return BadRequest(new { error = "Password is required" });
                }
                Console.WriteLine("✅ TEST 3 PASSED: Password provided");

                // Test 4: User already exists?
                Console.WriteLine("🔍 TEST 4: Checking if user exists by email...");
                var existingUser = await userManager.FindByEmailAsync(request.UserName);
                if (existingUser != null)
                {
                    Console.WriteLine($"❌ TEST 4 FAILED: User {request.UserName} already exists (Id: {existingUser.Id})");
                    return BadRequest(new { error = $"User {request.UserName} already exists" });
                }
                Console.WriteLine("✅ TEST 4 PASSED: No existing user found");

                // Test 5: Create user
                Console.WriteLine("🔍 TEST 5: Creating new user...");
                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.UserName,
                    EmailConfirmed = true // Gardé comme dans l'original
                };

                Console.WriteLine("🔐 Calling userManager.CreateAsync...");
                var result = await userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    Console.WriteLine($"❌ TEST 5 FAILED: CreateAsync failed: {errors}");
                    return BadRequest(new { error = errors });
                }

                Console.WriteLine($"✅ TEST 5 PASSED: User created with ID = {user.Id}");

                // Test 6: Add to role
                Console.WriteLine("🔍 TEST 6: Adding user to role 'User'...");
                try
                {
                    var roleExists = await roleManager.RoleExistsAsync("User");
                    Console.WriteLine($"   Role 'User' exists: {roleExists}");
                    if (!roleExists)
                    {
                        Console.WriteLine("   ➕ Creating role 'User'...");
                        var roleCreateResult = await roleManager.CreateAsync(new ApplicationRole { Name = "User" });
                        Console.WriteLine($"   ➕ Role create success: {roleCreateResult.Succeeded}");
                        if (!roleCreateResult.Succeeded)
                        {
                            Console.WriteLine($"   ⚠️ Role creation errors: {string.Join(", ", roleCreateResult.Errors.Select(e => e.Description))}");
                        }
                    }

                    var roleResult = await userManager.AddToRoleAsync(user, "User");
                    if (roleResult.Succeeded)
                    {
                        Console.WriteLine("✅ TEST 6 PASSED: User added to 'User' role");
                    }
                    else
                    {
                        var roleErrors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                        Console.WriteLine($"⚠️ TEST 6 WARNING: Could not add to role: {roleErrors}");
                    }
                }
                catch (Exception roleEx)
                {
                    Console.WriteLine($"⚠️ TEST 6 WARNING: Exception while adding to role: {roleEx.Message}");
                    Console.WriteLine($"   Stack: {roleEx.StackTrace}");
                }

                Console.WriteLine("=" .PadRight(60, '='));
                Console.WriteLine("🎉 REGISTRATION SUCCESS");
                Console.WriteLine("=" .PadRight(60, '='));

                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("=" .PadRight(60, '='));
                Console.WriteLine("💥 REGISTRATION EXCEPTION");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                Console.WriteLine("=" .PadRight(60, '='));
                return BadRequest(new { error = $"Exception: {ex.Message}" });
            }
            finally
            {
                Console.WriteLine("🧾 Register method finished");
                Console.WriteLine("=" .PadRight(60, '='));
            }
        }

        public class RegisterRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("✉️ ConfirmEmail START");
            Console.WriteLine($"   userId: {userId}");
            Console.WriteLine($"   code length: {code?.Length ?? 0}");
            Console.WriteLine("============================================================");

            try
            {
                var user = await userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    Console.WriteLine("❌ ConfirmEmail: User not found");
                    return RedirectWithError("Invalid user");
                }

                Console.WriteLine("🔐 Calling ConfirmEmailAsync...");
                var result = await userManager.ConfirmEmailAsync(user, code!);

                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Email confirmed");
                    return Redirect("~/Login?info=Your registration has been confirmed");
                }

                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ ConfirmEmail failed: {errors}");
                return RedirectWithError("Invalid user or confirmation code");
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 ConfirmEmail EXCEPTION");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return RedirectWithError($"An error occurred: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("✉️ ConfirmEmail END");
                Console.WriteLine("============================================================");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string userName)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🔁 ResetPassword START");
            Console.WriteLine($"   userName: {userName}");
            Console.WriteLine("============================================================");

            if (string.IsNullOrEmpty(userName))
            {
                Console.WriteLine("❌ ResetPassword: Invalid user name (empty)");
                return BadRequest("Invalid user name.");
            }

            var user = await userManager.FindByNameAsync(userName);

            if (user == null)
            {
                Console.WriteLine("❌ ResetPassword: User not found");
                return BadRequest("Invalid user name.");
            }

            try
            {
                Console.WriteLine("🔐 Generating password reset token...");
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                Console.WriteLine($"   Token generated (length: {code?.Length ?? 0})");

                var callbackUrl = Url.Action("ConfirmPasswordReset", "Account",
                    new { userId = user.Id, code },
                    protocol: Request.Scheme);

                Console.WriteLine($"🔗 Callback URL created: {callbackUrl}");

                var body = string.Format(@"<p>Please click the following link to reset your password:</p><p><a href=""{0}"">{0}</a></p>", callbackUrl);

                Console.WriteLine("✉️ Sending reset email...");
                await SendEmailAsync(user.Email!, "Confirm your password reset", body);
                Console.WriteLine("✅ Reset email sent");

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 ResetPassword EXCEPTION");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return BadRequest(ex.Message);
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("🔁 ResetPassword END");
                Console.WriteLine("============================================================");
            }
        }

        public async Task<IActionResult> ConfirmPasswordReset(string userId, string code)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("🔐 ConfirmPasswordReset START");
            Console.WriteLine($"   userId: {userId}");
            Console.WriteLine($"   code length: {code?.Length ?? 0}");
            Console.WriteLine("============================================================");

            var user = await userManager.FindByIdAsync(userId);

            if (user == null)
            {
                Console.WriteLine("❌ ConfirmPasswordReset: User not found");
                return Redirect("~/Login?error=Invalid user");
            }

            try
            {
                var password = GenerateRandomPassword();
                Console.WriteLine($"🔐 Generated random password (length: {password.Length})");

                var result = await userManager.ResetPasswordAsync(user, code!, password);

                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Password reset succeeded - sending new password by email");
                    await SendEmailAsync(user.Email!, "New password",
                        $"<p>Your new password is: <strong>{password}</strong></p><p>Please change it after login.</p>");

                    Console.WriteLine("✅ Email with new password sent");
                    return Redirect("~/Login?info=Password reset successful. You will receive an email with your new password.");
                }

                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ ConfirmPasswordReset failed: {errors}");
                return Redirect("~/Login?error=Invalid user or confirmation code");
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 ConfirmPasswordReset EXCEPTION");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return Redirect("~/Login?error=An error occurred");
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("🔐 ConfirmPasswordReset END");
                Console.WriteLine("============================================================");
            }
        }

        private static string GenerateRandomPassword()
        {
            Console.WriteLine("🔁 GenerateRandomPassword START");

            var options = new PasswordOptions
            {
                RequiredLength = 8,
                RequiredUniqueChars = 4,
                RequireDigit = true,
                RequireLowercase = true,
                RequireNonAlphanumeric = true,
                RequireUppercase = true
            };

            var randomChars = new[] {
                "ABCDEFGHJKLMNOPQRSTUVWXYZ",
                "abcdefghijkmnopqrstuvwxyz",
                "0123456789",
                "!@$?_-"
            };

            var rand = new Random(Environment.TickCount);
            var chars = new List<char>();

            if (options.RequireUppercase)
            {
                chars.Insert(rand.Next(0, chars.Count + 1),
                    randomChars[0][rand.Next(0, randomChars[0].Length)]);
            }

            if (options.RequireLowercase)
            {
                chars.Insert(rand.Next(0, chars.Count + 1),
                    randomChars[1][rand.Next(0, randomChars[1].Length)]);
            }

            if (options.RequireDigit)
            {
                chars.Insert(rand.Next(0, chars.Count + 1),
                    randomChars[2][rand.Next(0, randomChars[2].Length)]);
            }

            if (options.RequireNonAlphanumeric)
            {
                chars.Insert(rand.Next(0, chars.Count + 1),
                    randomChars[3][rand.Next(0, randomChars[3].Length)]);
            }

            for (int i = chars.Count; i < options.RequiredLength || chars.Distinct().Count() < options.RequiredUniqueChars; i++)
            {
                string rcs = randomChars[rand.Next(0, randomChars.Length)];
                chars.Insert(rand.Next(0, chars.Count + 1), rcs[rand.Next(0, rcs.Length)]);
            }

            var password = new string(chars.ToArray());
            Console.WriteLine($"🔐 Generated password: (hidden) length={password.Length}");
            Console.WriteLine("🔁 GenerateRandomPassword END");
            return password;
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("✉️ SendEmailAsync START");
            Console.WriteLine($"   To: {to}");
            Console.WriteLine($"   Subject: {subject}");
            Console.WriteLine($"   Body length: {body?.Length ?? 0}");
            Console.WriteLine("============================================================");

            try
            {
                var mailMessage = new System.Net.Mail.MailMessage();
                var from = configuration.GetValue<string>("Smtp:User");
                var host = configuration.GetValue<string>("Smtp:Host");
                var port = configuration.GetValue<int>("Smtp:Port");
                var ssl = configuration.GetValue<bool>("Smtp:Ssl");
                var user = configuration.GetValue<string>("Smtp:User");
                var pass = configuration.GetValue<string>("Smtp:Password");

                Console.WriteLine($"🔧 SMTP Config => Host: {host}, Port: {port}, SSL: {ssl}, From: {from}");

                mailMessage.From = new System.Net.Mail.MailAddress(from!);
                mailMessage.Body = body;
                mailMessage.Subject = subject;
                mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                mailMessage.SubjectEncoding = System.Text.Encoding.UTF8;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(to);

                using (var client = new System.Net.Mail.SmtpClient(host))
                {
                    client.UseDefaultCredentials = false;
                    client.EnableSsl = ssl;
                    client.Port = port;
                    client.Credentials = new System.Net.NetworkCredential(user, pass);

                    Console.WriteLine("📡 Sending email via SMTP client...");
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine("✅ Email sent successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("💥 SendEmailAsync EXCEPTION");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                // Ne pas propager l'exception afin de ne pas casser le flow d'API
            }
            finally
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("✉️ SendEmailAsync END");
                Console.WriteLine("============================================================");
            }
        }
    }
}
