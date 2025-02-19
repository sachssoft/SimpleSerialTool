// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace sachssoft.SimpleSerialTool
{
    public partial class App : Application
    {
        public const string URL_GITHUB = "https://github.com/sachssoft";
        public const string URL_WEBSITE = "https://www.sachssoft.com/simpleserialtool";
        public const string URL_DOCUMENTATION = URL_WEBSITE + "/documentation";
        public const string URL_BUG_REPORT = URL_WEBSITE + "/bug-report";
        public const string URL_LICENSE_INFORMATION = URL_GITHUB +"/SimpleSerialTool/blob/main/LICENSE";
        public const string URL_REPOSITORY = URL_GITHUB + "/SimpleSerialTool";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }


    }
}