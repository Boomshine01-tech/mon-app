using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using Radzen.Blazor;

namespace SmartNest.Client.Shared
{
    public partial class CulturePicker
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
        protected NotificationService NotificationService { get; set; } = default!;

        protected string culture = string.Empty; 

        [Inject]
        protected SecurityService Security { get; set; } = default!;

        protected override void OnInitialized()
        {
            culture = CultureInfo.CurrentCulture.Name;
        }

        protected void ChangeCulture()
        {
            var redirect = new Uri(NavigationManager.Uri).GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped);

            var query = $"?culture={Uri.EscapeDataString(culture)}&redirectUri={redirect}";

            NavigationManager.NavigateTo("Culture/SetCulture" + query, forceLoad: true);
        }
    }
}