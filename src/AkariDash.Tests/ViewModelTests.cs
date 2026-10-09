using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AkariDash.Framework.ViewModels;
using Xunit;

namespace AkariDash.Tests;

public class ViewModelTests
{
    [Fact]
    public void ViewModelBase_title_raises_property_changed()
    {
        var viewModel = new TestViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Title = "Hello";

        Assert.Equal("Hello", viewModel.Title);
        Assert.Contains(nameof(viewModel.Title), changed);
    }

    [Fact]
    public void ValidatableViewModelBase_reports_errors_and_can_submit()
    {
        var viewModel = new TestValidatableViewModel { Name = "Ada" };
        Assert.False(viewModel.HasErrors);

        viewModel.Name = "";
        viewModel.ValidateAll();

        Assert.True(viewModel.HasErrors);
        Assert.False(viewModel.CanSubmit);
        Assert.Equal("Name is required.", viewModel.GetError(nameof(viewModel.Name)));

        viewModel.Name = "Ada";
        viewModel.ValidateAll();

        Assert.False(viewModel.HasErrors);
        Assert.True(viewModel.CanSubmit);
        Assert.Null(viewModel.GetError(nameof(viewModel.Name)));
    }

    [Fact]
    public void ValidatableViewModelBase_enforces_max_length()
    {
        var viewModel = new TestValidatableViewModel { Name = "Ada" };

        viewModel.Name = "This name is far too long";
        viewModel.ValidateProperty(nameof(viewModel.Name));

        Assert.True(viewModel.HasErrors);
        Assert.Equal("Name must be 10 characters or fewer.", viewModel.GetError(nameof(viewModel.Name)));
    }

    [Fact]
    public void ValidatableViewModelBase_validate_all()
    {
        var viewModel = new TestValidatableViewModel { Name = "Ada" };

        Assert.True(viewModel.ValidateAll());
    }

    [Fact]
    public void ValidatableViewModelBase_validate_all_fails_when_invalid()
    {
        var viewModel = new TestValidatableViewModel { Name = "" };

        Assert.False(viewModel.ValidateAll());
        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void ValidatableViewModelBase_raises_errors_and_error_property_changed()
    {
        var viewModel = new TestValidatableViewModel { Name = "Ada" };
        var propertyChanged = new List<string?>();
        var errorsChanged = new List<string?>();
        viewModel.PropertyChanged += (_, e) => propertyChanged.Add(e.PropertyName);
        viewModel.ErrorsChanged += (_, e) => errorsChanged.Add(e.PropertyName);

        viewModel.Name = "";
        viewModel.ValidateProperty(nameof(viewModel.Name));

        Assert.Contains(nameof(viewModel.Name), errorsChanged);
        Assert.Contains($"{nameof(viewModel.Name)}Error", propertyChanged);
    }

    [Fact]
    public void ValidatableViewModelBase_validate_single_property()
    {
        var viewModel = new TestValidatableViewModel();

        viewModel.ValidateProperty(nameof(viewModel.Name));

        Assert.True(viewModel.HasErrors);
        Assert.Equal("Name is required.", viewModel.GetError(nameof(viewModel.Name)));
    }
}

internal sealed class TestViewModel : ViewModelBase
{
}

internal partial class TestValidatableViewModel : ValidatableViewModelBase
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(10, ErrorMessage = "Name must be 10 characters or fewer.")]
    public partial string Name { get; set; } = string.Empty;
}
