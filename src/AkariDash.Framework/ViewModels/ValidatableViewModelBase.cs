using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AkariDash.Framework.ViewModels;

/// <summary>
/// Base class for view models that expose validation via
/// <see cref="INotifyDataErrorInfo"/> (provided by <see cref="ObservableValidator"/>).
/// Combine with <see cref="ValidationAttribute"/>s and <c>[NotifyDataErrorFor]</c>.
/// </summary>
public abstract partial class ValidatableViewModelBase : ObservableValidator
{
    protected ValidatableViewModelBase()
    {
        ErrorsChanged += OnErrorsChanged;
    }

    /// <summary>Inverse of <see cref="ObservableValidator.HasErrors"/>; handy for gating submit buttons.</summary>
    public bool CanSubmit { get; private set; }

    /// <summary>Validates the whole model and returns true when valid.</summary>
    public bool ValidateAll()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>Validates a single property.</summary>
    public void ValidateProperty(string propertyName)
    {
        var value = GetType().GetProperty(propertyName)?.GetValue(this);
        ValidateProperty(value, propertyName);
    }

    /// <summary>Returns the first error message for a property, or null when valid.</summary>
    public string? GetError(string propertyName)
    {
        var errors = GetErrors(propertyName);
        foreach (var error in errors)
        {
            if (error is ValidationResult { ErrorMessage: not null } validationResult)
            {
                return validationResult.ErrorMessage;
            }
        }

        var first = errors.OfType<object>().FirstOrDefault();
        return first?.ToString();
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        CanSubmit = !HasErrors;

        // Keep the {PropertyName}Error binding targets in sync with validation state.
        if (e.PropertyName is not null)
        {
            OnPropertyChanged($"{e.PropertyName}Error");
        }
    }
}
