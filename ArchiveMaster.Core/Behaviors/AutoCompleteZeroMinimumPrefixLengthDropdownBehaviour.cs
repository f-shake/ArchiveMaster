using System.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using System.Diagnostics;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Serilog;


namespace ArchiveMaster.Behaviors;

public class AutoCompleteZeroMinimumPrefixLengthDropdownBehaviour : Behavior<AutoCompleteBox>
{
    private Button dropDownButton;

    protected override void OnAttached()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.DropDownOpening += DropDownOpening;
            AssociatedObject.GotFocus += OnGotFocus;
            AssociatedObject.PointerReleased += PointerReleased;
            AssociatedObject.GetObservable(AutoCompleteBox.ItemsSourceProperty).Subscribe(p =>
            {
                dropDownButton?.IsVisible = p != null && p.Cast<object>().Any();
            });
            AssociatedObject.TemplateApplied += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Invoke(CreateDropdownButton);
            };
        }

        base.OnAttached();
    }
    

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp -= OnKeyUp;
            AssociatedObject.DropDownOpening -= DropDownOpening;
            AssociatedObject.GotFocus -= OnGotFocus;
            AssociatedObject.PointerReleased -= PointerReleased;
        }

        base.OnDetaching();
    }

    private void CreateDropdownButton()
    {
        if (AssociatedObject != null)
        {
            var prop = AssociatedObject.GetType().GetProperty("TextBox",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tb = (TextBox?)prop?.GetValue(AssociatedObject);
            if (tb is not null && tb.InnerRightContent is not Button)
            {
                dropDownButton = new Button()
                {
                    Content = new FluentIcon() { Icon = Icon.CaretDown, IconVariant = IconVariant.Filled },
                    ClickMode = ClickMode.Press,
                    Classes = { "Icon" }
                };
                dropDownButton.Click += (s, e) =>
                {
                    AssociatedObject.Text = string.Empty;
                    ShowDropdown();
                };

                tb.InnerRightContent = dropDownButton;
            }
        }
    }

    private void DropDownOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var prop = AssociatedObject?.GetType().GetProperty("TextBox",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var tb = (TextBox?)prop?.GetValue(AssociatedObject);
        if (tb is not null && tb.IsReadOnly)
        {
            e.Cancel = true;
            return;
        }
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (AssociatedObject != null)
        {
            CreateDropdownButton();
        }
    }

    //have to use KeyUp as AutoCompleteBox eats some of the KeyDown events
    private void OnKeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if ((e.Key == Avalonia.Input.Key.Down || e.Key == Avalonia.Input.Key.F4))
        {
            if (string.IsNullOrEmpty(AssociatedObject?.Text))
            {
                ShowDropdown();
            }
        }
    }

    private void PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (string.IsNullOrEmpty(AssociatedObject?.Text))
        {
            ShowDropdown();
        }
    }

    private void ShowDropdown()
    {
        if (AssociatedObject is not null && !AssociatedObject.IsDropDownOpen)
        {
            typeof(AutoCompleteBox)
                .GetMethod("PopulateDropDown",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(AssociatedObject, new object[] { AssociatedObject, EventArgs.Empty });
            typeof(AutoCompleteBox)
                .GetMethod("OpeningDropDown",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(AssociatedObject, new object[] { false });

            if (!AssociatedObject.IsDropDownOpen)
            {
                //We *must* set the field and not the property as we need to avoid the changed event being raised (which prevents the dropdown opening).
                var ipc = typeof(AutoCompleteBox).GetField("_ignorePropertyChange",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if ((bool)ipc?.GetValue(AssociatedObject) == false)
                    ipc?.SetValue(AssociatedObject, true);

                AssociatedObject.SetCurrentValue<bool>(AutoCompleteBox.IsDropDownOpenProperty, true);
            }
        }
    }
}