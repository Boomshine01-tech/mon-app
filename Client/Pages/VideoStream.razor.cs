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
    public partial class VideoStream
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }  = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; }  = default!;

        [Inject]
        protected DialogService DialogService { get; set; } = default!;

        [Inject]
        protected TooltipService TooltipService { get; set; } = default!;

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; } = default!;



        [Inject]
        protected SecurityService Security { get; set; } = default!;
    }
}