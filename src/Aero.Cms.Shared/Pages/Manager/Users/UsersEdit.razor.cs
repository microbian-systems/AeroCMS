namespace Aero.Cms.Shared.Pages.Manager.Users;

using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

public partial class UsersEdit : ComponentBase
{
    [Parameter] public long? Id { get; set; }

    [Inject] protected IUsersHttpClient UsersApi { get; set; } = default!;

    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    protected bool IsLoading { get; set; }
    protected bool IsSaving { get; set; }
    protected string UserName { get; set; } = string.Empty;
    protected string Email { get; set; } = string.Empty;
    protected string DisplayName { get; set; } = string.Empty;
    protected string Password { get; set; } = string.Empty;
    protected string RoleDraft { get; set; } = string.Empty;
    protected bool IsEnabled { get; set; } = true;
    protected DateTime? CreatedAt { get; set; }
    protected DateTime? LastLoginAt { get; set; }
    protected List<string> Roles { get; } = [];

    protected bool IsNew => Id is null or 0;
    protected string PageTitle => IsNew ? L["New User"] : $"{L["Edit"]} {DisplayNameOrUserName}";
    protected string SaveButtonText => IsSaving ? L["Saving..."] : IsNew ? L["Create User"] : L["Save User"];
    protected string StatusDescription => IsEnabled ? L["This user can sign in."] : L["This user is disabled."];
    protected string CreatedAtText => CreatedAt?.ToLocalTime().ToString("MMM d, yyyy h:mm tt") ?? "-";
    protected string LastLoginText => LastLoginAt?.ToLocalTime().ToString("MMM d, yyyy h:mm tt") ?? L["Never"];

    private string DisplayNameOrUserName
        => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : UserName;

    protected override async Task OnParametersSetAsync()
    {
        if (IsNew)
        {
            IsEnabled = true;
            return;
        }

        IsLoading = true;
        try
        {
            var result = await UsersApi.GetByIdAsync(Id!.Value);
            if (result is Result<UserDetail, AeroError>.Ok ok)
            {
                LoadUser(ok.Value);
            }
            else if (result is Result<UserDetail, AeroError>.Failure fail)
            {
                NotificationService.Notify(NotificationSeverity.Error, FormatError(fail.Error), duration: 4000);
                Navigation.NavigateTo("/manager/users");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task SaveAsync()
    {
        if (!Validate())
            return;

        IsSaving = true;
        try
        {
            if (IsNew)
            {
                var create = new CreateUserRequest(
                    UserName.Trim(),
                    Email.Trim(),
                    DisplayName.Trim(),
                    Password,
                    Roles.ToList());

                var result = await UsersApi.CreateAsync(create);
                if (result is Result<UserDetail, AeroError>.Ok ok)
                {
                    NotificationService.Notify(NotificationSeverity.Success, $"User '{ok.Value.DisplayName}' created");
                    Navigation.NavigateTo("/manager/users");
                }
                else if (result is Result<UserDetail, AeroError>.Failure fail)
                {
                    NotificationService.Notify(NotificationSeverity.Error, FormatError(fail.Error), duration: 4000);
                }
            }
            else
            {
                var update = new UpdateUserRequest(
                    Email.Trim(),
                    DisplayName.Trim(),
                    IsEnabled,
                    Roles.ToList());

                var result = await UsersApi.UpdateAsync(Id!.Value, update);
                if (result is Result<UserDetail, AeroError>.Ok ok)
                {
                    NotificationService.Notify(NotificationSeverity.Success, $"User '{ok.Value.DisplayName}' updated");
                    Navigation.NavigateTo("/manager/users");
                }
                else if (result is Result<UserDetail, AeroError>.Failure fail)
                {
                    NotificationService.Notify(NotificationSeverity.Error, FormatError(fail.Error), duration: 4000);
                }
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected void AddRole()
    {
        var role = RoleDraft.Trim();
        if (string.IsNullOrWhiteSpace(role))
            return;

        if (!Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            Roles.Add(role);
        }

        RoleDraft = string.Empty;
    }

    protected void RemoveRole(string role)
        => Roles.RemoveAll(existing => string.Equals(existing, role, StringComparison.OrdinalIgnoreCase));

    protected void ToggleEnabled(ChangeEventArgs args)
    {
        IsEnabled = args.Value is bool value && value;
    }

    private void LoadUser(UserDetail user)
    {
        UserName = user.UserName;
        Email = user.Email;
        DisplayName = user.DisplayName;
        IsEnabled = user.IsEnabled;
        CreatedAt = user.CreatedAt;
        LastLoginAt = user.LastLoginAt;
        Roles.Clear();
        Roles.AddRange(user.Roles.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Display name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Email is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Username is required");
            return false;
        }

        if (IsNew && string.IsNullOrWhiteSpace(Password))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Temporary password is required");
            return false;
        }

        return true;
    }

    private static string FormatError(AeroError error)
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
}
