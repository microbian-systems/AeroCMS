namespace Aero.Cms.Shared.Pages.Manager.Users;

using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

public partial class Users : ComponentBase
{
    [Inject] protected IUsersHttpClient UsersApi { get; set; } = default!;

    [Inject] protected DialogService DialogService { get; set; } = default!;

    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    protected RadzenDataGrid<UserSummary>? _grid;
    protected IEnumerable<UserSummary> _users = [];
    protected int _count;
    protected bool _isLoading;
    protected string _searchText = string.Empty;

    protected async Task LoadData(LoadDataArgs args)
    {
        _isLoading = true;

        var result = await UsersApi.GetAllAsync(args.Skip ?? 0, args.Top ?? 10, _searchText);

        switch (result)
        {
            case Result<PagedResult<UserSummary>, AeroError>.Ok ok:
                _users = ok.Value.Items;
                _count = (int)ok.Value.TotalCount;
                break;
            case Result<PagedResult<UserSummary>, AeroError>.Failure fail:
                _users = [];
                _count = 0;
                NotifyError("Users failed to load", fail.Error);
                break;
        }

        _isLoading = false;
    }

    protected async Task OnSearchChanged(string text)
    {
        _searchText = text;

        if (_grid is not null)
        {
            await _grid.FirstPage(true);
        }
    }

    protected async Task ReloadUsersAsync()
    {
        if (_grid is not null)
        {
            await _grid.Reload();
        }
    }

    protected void OnRowClick(DataGridRowMouseEventArgs<UserSummary> args)
    {
        Navigation.NavigateTo($"/manager/users/{args.Data.Id}");
    }

    protected async Task DeleteUserAsync(UserSummary user)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete {GetDisplayName(user)}? This removes the CRM user account and cannot be undone.",
            "Delete User",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        var result = await UsersApi.DeleteAsync(user.Id);

        switch (result)
        {
            case Result<bool, AeroError>.Ok:
                NotificationService.Notify(NotificationSeverity.Success, "User deleted", GetDisplayName(user));
                await ReloadUsersAsync();
                break;
            case Result<bool, AeroError>.Failure fail:
                NotifyError("Delete failed", fail.Error);
                break;
        }
    }

    protected void NotifyError(string title, AeroError error)
    {
        NotificationService.Notify(NotificationSeverity.Error, title, FormatError(error), duration: 5000);
    }

    protected static string FormatError(AeroError error)
    {
        return error switch
        {
            AeroError.Error generic => generic.msg,
            AeroError.Validation validation => string.Join("; ", validation.Errors),
            AeroError.HttpRequest http => http.msg ?? $"HTTP request failed with status {(int)http.code}.",
            AeroError.NotFound notFound => notFound.msg,
            AeroError.BadRequest badRequest => badRequest.msg,
            AeroError.InvalidRequest invalidRequest => invalidRequest.msg,
            AeroError.Conflict conflict => conflict.msg,
            AeroError.Forbidden forbidden => forbidden.msg,
            AeroError.Unauthorized unauthorized => unauthorized.msg,
            _ => error.ToString()
        };
    }

    protected static string GetDisplayName(UserSummary user)
    {
        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
    }

    protected static string GetInitials(string displayName, string userName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? userName : displayName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return "??";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }

        return name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }
}
