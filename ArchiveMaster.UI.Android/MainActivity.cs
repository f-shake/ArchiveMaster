using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using ArchiveMaster.Platforms;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Java.Interop;
using Java.Lang;
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Android.Views.View;
using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;
using ArchiveMaster.Views;
using FileHelper = ArchiveMaster.Helpers.FileHelper;

namespace ArchiveMaster.UI.Android;

[Activity(
    Label = "文件归档大师",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
                           | ConfigChanges.ScreenSize
                           | ConfigChanges.UiMode
                           | ConfigChanges.SmallestScreenSize
                           | ConfigChanges.ScreenLayout
                           | ConfigChanges.Keyboard
                           | ConfigChanges.KeyboardHidden
                           | ConfigChanges.Navigation
)]
public class MainActivity : AvaloniaMainActivity<App>, IPermissionService, IBackCommandService,
    IViewPadding
{
    private const int REQUEST_MANAGE_EXTERNAL_STORAGE = 1024;
    private const int REQUEST_READ_AND_WRITE_EXTERNAL_STORAGE = 1025;

    private Func<bool> backAction;

    public void CheckPermissions()
    {
        if ((int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.R)
        {
            if (!Environment.IsExternalStorageManager)
            {
                Toast.MakeText(this, "本应用需要访问存储器权限", ToastLength.Short).Show();

                Intent intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Uri.Parse("package:" + PackageName));
                StartActivityForResult(intent, REQUEST_MANAGE_EXTERNAL_STORAGE);
            }
        }
    }

    public double GetBottom()
    {
        var density = Resources.DisplayMetrics.ScaledDensity;
        return WindowManager.CurrentWindowMetrics.WindowInsets.GetInsets(WindowInsets.Type.NavigationBars()).Bottom /
               density;
    }

    public double GetNavBarHeight()
    {
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)30)
        {
            return WindowManager.CurrentWindowMetrics.WindowInsets.GetInsets(WindowInsets.Type.NavigationBars()).Bottom;
        }

        var orientation = Resources.Configuration.Orientation;
        int resourceId =
            Resources.GetIdentifier(
                orientation == global::Android.Content.Res.Orientation.Portrait
                    ? "navigation_bar_height"
                    : "navigation_bar_height_landscape", "dimen", "android");
        if (resourceId > 0)
        {
            return Resources.GetDimensionPixelSize(resourceId);
        }

        return 0;
    }

    public double GetTop()
    {
        var density = Resources.DisplayMetrics.ScaledDensity;
        return WindowManager.CurrentWindowMetrics.WindowInsets.GetInsets(WindowInsets.Type.StatusBars()).Top / density;
    }

    public override void OnBackPressed()
    {
        bool? result = backAction?.Invoke();
        if (result != true)
        {
            base.OnBackPressed();
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    public void RegisterBackCommand(Func<bool> backAction)
    {
        this.backAction = backAction;
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        //throw new NotImplementedException();
        Initializer.ServiceInitializing += (sender, e) =>
        {
            e.Services.AddSingleton<IPermissionService>(this);
            e.Services.AddSingleton<IBackCommandService>(this);
            e.Services.AddSingleton<IViewPadding>(this);
        };

        FileHelper.AndroidExternalFilesDir = GetExternalFilesDir();

        return base.CustomizeAppBuilder(builder);
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == REQUEST_MANAGE_EXTERNAL_STORAGE)
        {
            if (Environment.IsExternalStorageManager)
            {
                // 权限已授予
                Toast.MakeText(this, "权限已授予", ToastLength.Short).Show();
                RequestPermissions([Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage],
                    REQUEST_READ_AND_WRITE_EXTERNAL_STORAGE);
            }
            else
            {
                // 权限未授予
                Toast.MakeText(this, "权限未授予，无此权限本应用无法运行，请授予", ToastLength.Short).Show();
                CheckPermissions();
            }
        }
        else
        {
        }
    }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
        Window.ClearFlags(WindowManagerFlags.TranslucentStatus);
        Window.SetStatusBarColor(Color.Transparent);
    }

    private string GetExternalFilesDir()
    {
        var dir = GetExternalFilesDir(string.Empty);
        return dir.AbsolutePath.Split(["Android"], StringSplitOptions.None)[0];
    }
}