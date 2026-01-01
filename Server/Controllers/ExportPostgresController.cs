using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

using SmartNest.Server.Data;
using SmartNest.Server; 

namespace SmartNest.Server.Controllers
{
    public partial class ExportpostgresController : ExportController
    {
        private readonly ApplicationDbContext context;
        private readonly postgresService service;

        public ExportpostgresController(ApplicationDbContext context, postgresService service)
        {
            this.service = service;
            this.context = context;
        }

  
        [HttpGet("/export/postgres/devices/csv")]
        [HttpGet("/export/postgres/devices/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportDevicesToCSV(string? fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetDevices(), Request.Query), fileName);
        }

        [HttpGet("/export/postgres/devices/excel")]
        [HttpGet("/export/postgres/devices/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportDevicesToExcel(string? fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetDevices(), Request.Query), fileName);
        }

        [HttpGet("/export/postgres/notifications/csv")]
        [HttpGet("/export/postgres/notifications/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportNotificationsToCSV(string? fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetNotifications(), Request.Query), fileName);
        }

        [HttpGet("/export/postgres/notifications/excel")]
        [HttpGet("/export/postgres/notifications/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportNotificationsToExcel(string? fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetNotifications(), Request.Query), fileName);
        }

        [HttpGet("/export/postgres/sensordata/csv")]
        [HttpGet("/export/postgres/sensordata/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportSensordataToCSV(string? fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetSensordata(), Request.Query), fileName);
        }

        [HttpGet("/export/postgres/sensordata/excel")]
        [HttpGet("/export/postgres/sensordata/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportSensordataToExcel(string? fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetSensordata(), Request.Query), fileName);
        }

       
    }
}