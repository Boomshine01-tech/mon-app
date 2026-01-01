using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using Radzen.Blazor;

namespace SmartNest.Client.Pages
{
    public partial class Chicks
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        protected DialogService DialogService { get; set; } = default!;

        [Inject]
        protected TooltipService TooltipService { get; set; } = default!;
 
        [Inject]
        protected ContextMenuService ContextMenuService { get; set; } = default!;

        
        [Inject]
        public postgresService postgresService { get; set; } = default!;

        protected IEnumerable<SmartNest.Server.Models.postgres.Chick> chicks = new List<SmartNest.Server.Models.postgres.Chick>();

        protected RadzenDataGrid<SmartNest.Server.Models.postgres.Chick> grid0 = default!;
        protected int count;

        [Inject]
        protected SecurityService Security { get; set; } = default!;

        protected async Task Grid0LoadData(LoadDataArgs args)
        {
            try
            {
                var result = await postgresService.GetChicks(filter: $"{args.Filter}", orderby: $"{args.OrderBy}", top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null);
                chicks = result.Value.AsODataEnumerable();
                count = result.Count;
            }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load Chicks" });
            }
        }    

        

        

       
    }
}