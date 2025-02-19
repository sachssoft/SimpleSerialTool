// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using System.IO;

namespace sachssoft.SimpleSerialTool
{
    public partial class AboutDialog : ReactiveWindow<AboutDialogViewModel>
    {
        public AboutDialog()
        {
            InitializeComponent();

            IconView.Source = new Bitmap(new MemoryStream(Resource.AppIcon));
            CloseButton.Click += (s, e) => Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.PhysicalKey == PhysicalKey.Escape)
            {
                Close();
            }
        }
    }
}