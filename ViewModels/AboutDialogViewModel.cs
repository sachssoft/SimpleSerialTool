// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using System.Reflection;

namespace sachssoft.SimpleSerialTool
{
    public class AboutDialogViewModel : ReactiveObject, IActivatableView
    {
        public ReactiveCommand<Unit, Unit> LicenseUrlCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> PageVisitUrlCommand { get; private set; }

        public AboutDialogViewModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            ProductTitle = $"{fvi.ProductName} {fvi.ProductMajorPart}.{fvi.ProductMinorPart}";
            ProductVersion = $"{fvi.ProductMajorPart}.{fvi.ProductMinorPart}.{fvi.ProductBuildPart}";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
            ProjectInfo = "This project is open source and licensed under the ";
            LicenseUrl = "MIT License.";
            HintAboutPage = "For more information and the source code, please visit ";
            PageVisitUrl = "the repository on GitHub.";

            LicenseUrlCommand = ReactiveCommand.Create(() => Utils.ShowBrowser(App.URL_LICENSE_INFORMATION));

            PageVisitUrlCommand = ReactiveCommand.Create(() => Utils.ShowBrowser(App.URL_REPOSITORY));
        }

        public string? ProductTitle
        {
            get;
        }

        public string? ProductVersion
        {
            get;
        }

        public string? Copyright
        {
            get;
        }

        public string? Description
        {
            get;
        }

        public string? ProjectInfo
        {
            get;
        }
        public string? LicenseUrl
        {
            get;
        }

        public string? HintAboutPage
        {
            get;
        }

        public string? PageVisitUrl
        {
            get;
        }
    }
}
