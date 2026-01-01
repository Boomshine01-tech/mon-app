using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartNest.Server.Models;

namespace SmartNest.Client.Services
{
    public interface IChickMonitoringService
    {
        Task<List<ChickData>> GetChicksData();
        Task UpdateHealthStatus(string chickId, string healthStatus);
        Task UpdateAgeAndWeight(string chickId, int age);
    }

    public class ChickMonitoringService : IChickMonitoringService
    {
        private readonly HttpClient _http;

        public ChickMonitoringService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ChickData>> GetChicksData()
        {
            try
            {
                var response = await _http.GetAsync("api/chicks");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ChickData>>() ?? new List<ChickData>();
                }
                return new List<ChickData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur récupération données: {ex.Message}");
                return new List<ChickData>();
            }
        }

        public async Task UpdateHealthStatus(string chickId, string healthStatus)
        {
            try
            {
                var request = new { ChickId = chickId, HealthStatus = healthStatus };
                await _http.PostAsJsonAsync("api/chicks/health", request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur mise à jour santé: {ex.Message}");
            }
        }

        public async Task UpdateAgeAndWeight(string chickId, int age)
        {
            try
            {
                var request = new { ChickId = chickId, Age = age };
                await _http.PostAsJsonAsync("api/chicks/metrics", request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur mise à jour métriques: {ex.Message}");
            }
        }
    }
}