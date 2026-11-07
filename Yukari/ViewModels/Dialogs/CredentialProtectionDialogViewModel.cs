using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Enums;
using Yukari.Services.Security;
using Yukari.Services.Sources;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Dialogs;

public partial class CredentialProtectionDialogViewModel : ObservableObject
{
    private readonly ICredentialProtector _credentialProtector;
    private readonly ISourceService _sourceService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    [NotifyPropertyChangedFor(nameof(DialogDescription))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowCurrentPassword))]
    [NotifyPropertyChangedFor(nameof(ShowNewPassword))]
    public partial CredentialProtectionMode Mode { get; set; }

    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool ShowCurrentPassword =>
        Mode
            is CredentialProtectionMode.Unlock
                or CredentialProtectionMode.ChangePassword
                or CredentialProtectionMode.Disable;

    public bool ShowNewPassword =>
        Mode is CredentialProtectionMode.Enable or CredentialProtectionMode.ChangePassword;

    public string DialogTitle =>
        _localizationService.GetString(
            Mode switch
            {
                CredentialProtectionMode.Unlock => "CredentialProtectionUnlockTitle",
                CredentialProtectionMode.Enable => "CredentialProtectionEnableTitle",
                CredentialProtectionMode.ChangePassword => "CredentialProtectionChangeTitle",
                CredentialProtectionMode.Disable => "CredentialProtectionDisableTitle",
                _ => "CredentialProtectionUnlockTitle",
            }
        );

    public string DialogDescription =>
        _localizationService.GetString(
            Mode switch
            {
                CredentialProtectionMode.Unlock => "CredentialProtectionUnlockDescription",
                CredentialProtectionMode.Enable => "CredentialProtectionEnableDescription",
                CredentialProtectionMode.ChangePassword => "CredentialProtectionChangeDescription",
                CredentialProtectionMode.Disable => "CredentialProtectionDisableDescription",
                _ => "CredentialProtectionUnlockDescription",
            }
        );

    public string PrimaryButtonText =>
        _localizationService.GetString(
            Mode switch
            {
                CredentialProtectionMode.Unlock => "CredentialProtectionUnlockPrimaryButton",
                CredentialProtectionMode.Enable => "CredentialProtectionEnablePrimaryButton",
                CredentialProtectionMode.ChangePassword =>
                    "CredentialProtectionChangePrimaryButton",
                CredentialProtectionMode.Disable => "CredentialProtectionDisablePrimaryButton",
                _ => "CredentialProtectionUnlockPrimaryButton",
            }
        );

    public CredentialProtectionDialogViewModel(
        ICredentialProtector credentialProtector,
        ISourceService sourceService,
        INotificationService notificationService,
        ILocalizationService localizationService
    )
    {
        _credentialProtector = credentialProtector;
        _sourceService = sourceService;
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public void Initialize(CredentialProtectionMode mode)
    {
        Mode = mode;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
    }

    public async Task<bool> SubmitAsync()
    {
        ErrorMessage = null;

        switch (Mode)
        {
            case CredentialProtectionMode.Unlock:
            {
                if (!_credentialProtector.TryUnlock(CurrentPassword))
                {
                    ErrorMessage = _localizationService.GetString(
                        "ErrorIncorrectCredentialPassword"
                    );
                    return false;
                }

                await _sourceService.ApplySettingsToAllLoadedSourcesAsync();
                _notificationService.ShowSuccess(
                    _localizationService.GetString("SuccessCredentialsUnlocked")
                );
                return true;
            }

            case CredentialProtectionMode.Enable:
            {
                if (string.IsNullOrEmpty(NewPassword))
                {
                    ErrorMessage = _localizationService.GetString("ErrorCredentialPasswordEmpty");
                    return false;
                }

                if (NewPassword != ConfirmPassword)
                {
                    ErrorMessage = _localizationService.GetString(
                        "ErrorCredentialPasswordMismatch"
                    );
                    return false;
                }

                await _credentialProtector.EnableEntropyAsync(NewPassword);
                _notificationService.ShowSuccess(
                    _localizationService.GetString("SuccessCredentialProtectionEnabled")
                );
                return true;
            }

            case CredentialProtectionMode.ChangePassword:
            {
                if (string.IsNullOrEmpty(NewPassword))
                {
                    ErrorMessage = _localizationService.GetString("ErrorCredentialPasswordEmpty");
                    return false;
                }

                if (NewPassword != ConfirmPassword)
                {
                    ErrorMessage = _localizationService.GetString(
                        "ErrorCredentialPasswordMismatch"
                    );
                    return false;
                }

                var changed = await _credentialProtector.ChangeEntropyPasswordAsync(
                    CurrentPassword,
                    NewPassword
                );
                if (!changed)
                {
                    ErrorMessage = _localizationService.GetString(
                        "ErrorIncorrectCredentialPassword"
                    );
                    return false;
                }

                _notificationService.ShowSuccess(
                    _localizationService.GetString("SuccessCredentialPasswordChanged")
                );
                return true;
            }

            case CredentialProtectionMode.Disable:
            {
                var disabled = await _credentialProtector.DisableEntropyAsync(CurrentPassword);
                if (!disabled)
                {
                    ErrorMessage = _localizationService.GetString(
                        "ErrorIncorrectCredentialPassword"
                    );
                    return false;
                }

                _notificationService.ShowSuccess(
                    _localizationService.GetString("SuccessCredentialProtectionDisabled")
                );
                return true;
            }

            default:
                return false;
        }
    }
}
