using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Web;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;
using Radzen;

namespace SmartNest.Client
{
    /// <summary>
    /// Service OData pour l'accès aux données PostgreSQL.
    /// ⚠️ IMPORTANT : Ce service est READ-ONLY pour les devices.
    /// Pour contrôler les appareils, utilisez IDeviceService qui gère MQTT + SignalR.
    /// </summary>
    public partial class postgresService
    {
        private readonly HttpClient httpClient;
        private readonly Uri baseUri;
        private readonly NavigationManager navigationManager;

        public postgresService(NavigationManager navigationManager, HttpClient httpClient, IConfiguration configuration)
        {
            this.httpClient = httpClient;
            this.navigationManager = navigationManager;
            this.baseUri = new Uri($"{navigationManager.BaseUri}odata/postgres/");
        }

        #region SensorDataService Methods

        public async Task<IEnumerable<SmartNest.Server.Models.postgres.Sensordatum>> GetSensorData()
        {
            var result = await GetSensordata(orderby: "Timestamp desc");
            return result.Value;
        }

        public async Task DeleteSensorData(int id)
        {
            await DeleteSensordatum(id);
        }

        public async Task<double> GetAverageTemperature()
        {
            var data = await GetSensorData();
            var filtered = data.Where(x => x.temperature != 0);
            return filtered.Any() ? filtered.Average(x => x.temperature) : 0;
        }

        public async Task<double> GetAverageHumidity()
        {
            var data = await GetSensorData();
            var filtered = data.Where(x => x.humidity != 0);
            return filtered.Any() ? filtered.Average(x => x.humidity) : 0;
        }

        public async Task<double> GetAverageDust()
        {
            var data = await GetSensorData();
            var filtered = data.Where(x => x.dust != 0);
            return filtered.Any() ? filtered.Average(x => x.dust) : 0;
        }

        public async Task<IEnumerable<SmartNest.Server.Models.postgres.Sensordatum>> GetLatestSensorData(int count = 10)
        {
            var result = await GetSensordata(orderby: "Timestamp desc", top: count);
            return result.Value;
        }

        public async Task<SmartNest.Server.Models.postgres.Sensordatum> CreateSensorData(double temperature, double humidity, double dust)
        {
            var sensorData = new SmartNest.Server.Models.postgres.Sensordatum
            {
                temperature = temperature,
                humidity = humidity,
                dust = dust,
                timestamp = DateTime.Now
            };

            return await CreateSensordatum(sensorData);
        }

        #endregion

        #region Chicks Methods

        public async System.Threading.Tasks.Task ExportChicksToExcel(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/chicks/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/chicks/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async System.Threading.Tasks.Task ExportChicksToCSV(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/chicks/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/chicks/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnGetChicks(HttpRequestMessage requestMessage);

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Chick>> GetChicks(Query? query = null)
        {
            return await GetChicks(filter: $"{query?.Filter}", orderby: $"{query?.OrderBy}", top: query?.Top, skip: query?.Skip, count: query?.Top != null && query?.Skip != null);
        }

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Chick>> GetChicks(string? filter = default(string), string? orderby = default(string), string? expand = default(string), int? top = default(int?), int? skip = default(int?), bool? count = default(bool?), string? format = default(string), string? select = default(string))
        {
            var uri = new Uri(baseUri, $"Chicks");
            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: filter, top: top, skip: skip, orderby: orderby, expand: expand, select: select, count: count);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetChicks(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Chick>>(response);
        }


        partial void OnGetChickById(HttpRequestMessage requestMessage);

        public async Task<SmartNest.Server.Models.postgres.Chick> GetChickById(string? expand = default(string), int id = default(int))
        {
            var uri = new Uri(baseUri, $"Chicks({id})");

            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: null, top: null, skip: null, orderby: null, expand: expand, select: null, count: null);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetChickById(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.Chick>(response);
        }

        partial void OnUpdateChick(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> UpdateChick(int id = default(int), SmartNest.Server.Models.postgres.Chick? chick = default(SmartNest.Server.Models.postgres.Chick))
        {
            var uri = new Uri(baseUri, $"Chicks({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(chick), Encoding.UTF8, "application/json");

            OnUpdateChick(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        #endregion

        #region Devices Methods (READ-ONLY - Use IDeviceService for control)

        /// <summary>
        /// ⚠️ READ-ONLY: Récupère les appareils depuis la base de données.
        /// Pour CONTRÔLER les appareils (ON/OFF), utilisez IDeviceService.ToggleDevice() 
        /// qui gère la communication MQTT + SignalR.
        /// </summary>
        public async System.Threading.Tasks.Task ExportDevicesToExcel(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/devices/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/devices/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async System.Threading.Tasks.Task ExportDevicesToCSV(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/devices/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/devices/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnGetDevices(HttpRequestMessage requestMessage);

        /// <summary>
        /// READ-ONLY: Lecture des appareils via OData.
        /// Ne modifie PAS l'état des appareils physiques.
        /// </summary>
        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.device>> GetDevices(Query? query = null)
        {
            return await GetDevices(filter: $"{query?.Filter}", orderby: $"{query?.OrderBy}", top: query?.Top, skip: query?.Skip, count: query?.Top != null && query?.Skip != null);
        }

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.device>> GetDevices(string? filter = default(string), string? orderby = default(string), string? expand = default(string), int? top = default(int?), int? skip = default(int?), bool? count = default(bool?), string? format = default(string), string? select = default(string))
        {
            var uri = new Uri(baseUri, $"Devices");
            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: filter, top: top, skip: skip, orderby: orderby, expand: expand, select: select, count: count);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetDevices(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.device>>(response);
        }

        /// <summary>
        /// ⚠️ DEPRECATED: N'utilisez PAS cette méthode pour créer des appareils qui seront contrôlés via MQTT.
        /// Utilisez plutôt l'API REST /api/devices avec MqttService configuré.
        /// </summary>
        [Obsolete("Use DeviceController API instead for MQTT-enabled devices")]
        partial void OnCreateDevice(HttpRequestMessage requestMessage);

        [Obsolete("Use DeviceController API instead for MQTT-enabled devices")]
        public async Task<SmartNest.Server.Models.postgres.device> CreateDevice(SmartNest.Server.Models.postgres.device? device = default(SmartNest.Server.Models.postgres.device))
        {
            var uri = new Uri(baseUri, $"Devices");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            OnCreateDevice(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.device>(response);
        }

        partial void OnDeleteDevice(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> DeleteDevice(string id)
        {
            var uri = new Uri(baseUri, $"Devices('{id}')");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

            OnDeleteDevice(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        partial void OnGetDeviceById(HttpRequestMessage requestMessage);

        /// <summary>
        /// READ-ONLY: Récupère un appareil par son ID.
        /// Pour l'état en temps réel, utilisez IDeviceService avec SignalR.
        /// </summary>
        public async Task<SmartNest.Server.Models.postgres.device> GetDeviceById(string? expand = default(string), string? id = default(string))
        {
            var uri = new Uri(baseUri, $"Devices('{id}')");

            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: null, top: null, skip: null, orderby: null, expand: expand, select: null, count: null);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetDeviceById(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.device>(response);
        }

        /// <summary>
        /// ⚠️ DEPRECATED: Cette méthode met à jour la base de données UNIQUEMENT.
        /// Elle ne communique PAS avec l'ESP32 via MQTT.
        /// Utilisez IDeviceService.ToggleDevice() pour un contrôle complet.
        /// </summary>
        [Obsolete("Use IDeviceService.ToggleDevice() for MQTT-enabled control")]
        partial void OnUpdateDevice(HttpRequestMessage requestMessage);

        [Obsolete("Use IDeviceService.ToggleDevice() for MQTT-enabled control")]
        public async Task<HttpResponseMessage> UpdateDevice(string id, SmartNest.Server.Models.postgres.device device)
        {
            var uri = new Uri(baseUri, $"Devices('{id}')");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            OnUpdateDevice(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        #endregion

        #region Notifications Methods

        public async System.Threading.Tasks.Task ExportNotificationsToExcel(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/notifications/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/notifications/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async System.Threading.Tasks.Task ExportNotificationsToCSV(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/notifications/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/notifications/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnGetNotifications(HttpRequestMessage requestMessage);

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Notification>> GetNotifications(Query? query = null)
        {
            return await GetNotifications(filter: $"{query?.Filter}", orderby: $"{query?.OrderBy}", top: query?.Top, skip: query?.Skip, count: query?.Top != null && query?.Skip != null);
        }

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Notification>> GetNotifications(string? filter = default(string), string? orderby = default(string), string? expand = default(string), int? top = default(int?), int? skip = default(int?), bool? count = default(bool?), string? format = default(string), string? select = default(string))
        {
            var uri = new Uri(baseUri, $"Notifications");
            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: filter, top: top, skip: skip, orderby: orderby, expand: expand, select: select, count: count);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetNotifications(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Notification>>(response);
        }

        partial void OnCreateNotification(HttpRequestMessage requestMessage);

        public async Task<SmartNest.Server.Models.postgres.Notification> CreateNotification(SmartNest.Server.Models.postgres.Notification? notification = default(SmartNest.Server.Models.postgres.Notification))
        {
            var uri = new Uri(baseUri, $"Notifications");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(notification), Encoding.UTF8, "application/json");

            OnCreateNotification(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.Notification>(response);
        }

        partial void OnDeleteNotification(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> DeleteNotification(string? id = default(string))
        {
            var uri = new Uri(baseUri, $"Notifications({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

            OnDeleteNotification(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        partial void OnGetNotificationById(HttpRequestMessage requestMessage);

        public async Task<SmartNest.Server.Models.postgres.Notification> GetNotificationById(string? expand = default(string), int id = default(int))
        {
            var uri = new Uri(baseUri, $"Notifications({id})");

            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: null, top: null, skip: null, orderby: null, expand: expand, select: null, count: null);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetNotificationById(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.Notification>(response);
        }

        partial void OnUpdateNotification(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> UpdateNotification(int id = default(int), SmartNest.Server.Models.postgres.Notification? notification = default(SmartNest.Server.Models.postgres.Notification))
        {
            var uri = new Uri(baseUri, $"Notifications({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(notification), Encoding.UTF8, "application/json");

            OnUpdateNotification(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        #endregion

        #region Sensordata Methods

        public async System.Threading.Tasks.Task ExportSensordataToExcel(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/sensordata/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/sensordata/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async System.Threading.Tasks.Task ExportSensordataToCSV(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/sensordata/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/sensordata/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnGetSensordata(HttpRequestMessage requestMessage);

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Sensordatum>> GetSensordata(Query? query = null)
        {
            return await GetSensordata(filter: $"{query?.Filter}", orderby: $"{query?.OrderBy}", top: query?.Top, skip: query?.Skip, count: query?.Top != null && query?.Skip != null);
        }

        public async Task<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Sensordatum>> GetSensordata(string? filter = default(string), string? orderby = default(string), string? expand = default(string), int? top = default(int?), int? skip = default(int?), bool? count = default(bool?), string? format = default(string), string? select = default(string))
        {
            var uri = new Uri(baseUri, $"Sensordata");
            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: filter, top: top, skip: skip, orderby: orderby, expand: expand, select: select, count: count);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetSensordata(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<Radzen.ODataServiceResult<SmartNest.Server.Models.postgres.Sensordatum>>(response);
        }

        partial void OnCreateSensordatum(HttpRequestMessage requestMessage);

        public async Task<SmartNest.Server.Models.postgres.Sensordatum> CreateSensordatum(SmartNest.Server.Models.postgres.Sensordatum? sensordatum = default(SmartNest.Server.Models.postgres.Sensordatum))
        {
            var uri = new Uri(baseUri, $"Sensordata");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(sensordatum), Encoding.UTF8, "application/json");

            OnCreateSensordatum(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.Sensordatum>(response);
        }

        partial void OnDeleteSensordatum(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> DeleteSensordatum(int id = default(int))
        {
            var uri = new Uri(baseUri, $"Sensordata({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

            OnDeleteSensordatum(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        partial void OnGetSensordatumById(HttpRequestMessage requestMessage);

        public async Task<SmartNest.Server.Models.postgres.Sensordatum> GetSensordatumById(string? expand = default(string), int id = default(int))
        {
            var uri = new Uri(baseUri, $"Sensordata({id})");

            uri = Radzen.ODataExtensions.GetODataUri(uri: uri, filter: null, top: null, skip: null, orderby: null, expand: expand, select: null, count: null);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            OnGetSensordatumById(httpRequestMessage);

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<SmartNest.Server.Models.postgres.Sensordatum>(response);
        }

        partial void OnUpdateSensordatum(HttpRequestMessage requestMessage);

        public async Task<HttpResponseMessage> UpdateSensordatum(int id = default(int), SmartNest.Server.Models.postgres.Sensordatum? sensordatum = default(SmartNest.Server.Models.postgres.Sensordatum))
        {
            var uri = new Uri(baseUri, $"Sensordata({id})");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri);

            httpRequestMessage.Content = new StringContent(Radzen.ODataJsonSerializer.Serialize(sensordatum), Encoding.UTF8, "application/json");

            OnUpdateSensordatum(httpRequestMessage);

            return await httpClient.SendAsync(httpRequestMessage);
        }

        #endregion

       
    }
}