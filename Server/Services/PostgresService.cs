using System;
using System.Data;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;

using SmartNest.Server.Data;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server
{
    public partial class postgresService
    {
        ApplicationDbContext Context
        {
           get
           {
             return this.context;
           }
        }

        private readonly ApplicationDbContext context;
        private readonly NavigationManager navigationManager;

        public postgresService(ApplicationDbContext context, NavigationManager navigationManager)
        {
            this.context = context;
            this.navigationManager = navigationManager;
        }

        public void Reset() => Context.ChangeTracker.Entries().Where(e => e.Entity != null).ToList().ForEach(e => e.State = EntityState.Detached);

        public void ApplyQuery<T>(ref IQueryable<T> items, Query? query = null)
        {
            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    if (query.FilterParameters != null)
                    {
                        items = items.Where(query.Filter, query.FilterParameters);
                    }
                    else
                    {
                        items = items.Where(query.Filter);
                    }
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                {
                    items = items.OrderBy(query.OrderBy);
                }

                if (query.Skip.HasValue)
                {
                    items = items.Skip(query.Skip.Value);
                }

                if (query.Top.HasValue)
                {
                    items = items.Take(query.Top.Value);
                }
            }
        }

        #region Devices Methods

        public async Task ExportDevicesToExcel(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/devices/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/devices/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportDevicesToCSV(Query? query = null, string? fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/postgres/devices/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/postgres/devices/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnDevicesRead(ref IQueryable<device> items);

        public async Task<IQueryable<device>> GetDevices(Query? query = null)
        {
            var items = Context.Devices.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Expand))
                {
                    var propertiesToExpand = query.Expand.Split(',');
                    foreach(var p in propertiesToExpand)
                    {
                        items = items.Include(p.Trim());
                    }
                }

                ApplyQuery(ref items, query);
            }

            OnDevicesRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnDeviceGet(device item);
        partial void OnGetDeviceById(ref IQueryable<device> items);

        public async Task<device> GetDeviceById(string id)
        {
            var items = Context.Devices
                              .AsNoTracking()
                              .Where(i => i.DeviceId == id);

            OnGetDeviceById(ref items);

            var itemToReturn = items.FirstOrDefault();

            OnDeviceGet(itemToReturn!);

            return await Task.FromResult(itemToReturn!);
        }

        partial void OnDeviceCreated(device item);
        partial void OnAfterDeviceCreated(device item);

        public async Task<device> CreateDevice(device device)
        {
            OnDeviceCreated(device);

            var existingItem = Context.Devices
                              .Where(i => i.DeviceId == device.DeviceId)
                              .FirstOrDefault();

            if (existingItem != null)
            {
               throw new Exception("Item already available");
            }            

            try
            {
                Context.Devices.Add(device);
                Context.SaveChanges();
            }
            catch
            {
                Context.Entry(device).State = EntityState.Detached;
                throw;
            }

            OnAfterDeviceCreated(device);

            return device;
        }

        public async Task<device> CancelDeviceChanges(device item)
        {
            var entityToCancel = Context.Entry(item);
            if (entityToCancel.State == EntityState.Modified)
            {
              entityToCancel.CurrentValues.SetValues(entityToCancel.OriginalValues);
              entityToCancel.State = EntityState.Unchanged;
            }

            return item;
        }

        partial void OnDeviceUpdated(device item);
        partial void OnAfterDeviceUpdated(device item);

        public async Task<device> UpdateDevice(string id, device device)
        {
            OnDeviceUpdated(device);

            var itemToUpdate = Context.Devices
                              .Where(i => i.DeviceId == device.DeviceId)
                              .FirstOrDefault();

            if (itemToUpdate == null)
            {
               throw new Exception("Item no longer available");
            }
                
            var entryToUpdate = Context.Entry(itemToUpdate);
            entryToUpdate.CurrentValues.SetValues(device);
            entryToUpdate.State = EntityState.Modified;

            Context.SaveChanges();

            OnAfterDeviceUpdated(device);

            return device;
        }

        partial void OnDeviceDeleted(device item);
        partial void OnAfterDeviceDeleted(device item);

        public async Task<device> DeleteDevice(string id)
        {
            var itemToDelete = Context.Devices
                              .Where(i => i.DeviceId == id)
                              .FirstOrDefault();

            if (itemToDelete == null)
            {
               throw new Exception("Item no longer available");
            }

            OnDeviceDeleted(itemToDelete);

            Context.Devices.Remove(itemToDelete);

            try
            {
                Context.SaveChanges();
            }
            catch
            {
                Context.Entry(itemToDelete).State = EntityState.Unchanged;
                throw;
            }

            OnAfterDeviceDeleted(itemToDelete);

            return itemToDelete;
        }

        #endregion

      // Dans la classe postgresService

        public async Task<IQueryable<Chick>> Getchicks(Query? query = null)
        {
            var items = Context.Chicks.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Expand))
                {
                    var propertiesToExpand = query.Expand.Split(',');
                    foreach(var p in propertiesToExpand)
                    {
                        items = items.Include(p.Trim());
                    }
                }

                ApplyQuery(ref items, query);
            }

            return await Task.FromResult(items);
        }

        public async Task<IQueryable<Notification>> GetNotifications(Query? query = null)
        {
            var items = Context.Notifications.AsQueryable();
            items = items.Include(i => i.UserId);

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Expand))
                {
                    var propertiesToExpand = query.Expand.Split(',');
                    foreach(var p in propertiesToExpand)
                    {
                        items = items.Include(p.Trim());
                    }
                }

                ApplyQuery(ref items, query);
            }

            return await Task.FromResult(items);
        }

        public async Task<IQueryable<Sensordatum>> GetSensordata(Query? query = null)
        {
            var items = Context.SensorData.AsQueryable();
            

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Expand))
                {
                    var propertiesToExpand = query.Expand.Split(',');
                    foreach(var p in propertiesToExpand)
                    {
                        items = items.Include(p.Trim());
                    }
                }

                ApplyQuery(ref items, query);
            }

            return await Task.FromResult(items);
        }

          
    }
}