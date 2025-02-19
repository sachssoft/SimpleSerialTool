// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvaloniaHex;
using AvaloniaHex.Document;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace sachssoft.SimpleSerialTool
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>, IActivatableView
    {
        public MainWindow()
        {
            InitializeComponent();

            Icon = new WindowIcon(new MemoryStream(Resource.AppIcon));

            this.WhenActivated(d =>
            {
                d.Invoke(ViewModel!.NewWindow.RegisterHandler(NewWindowInteractionRegistering));
                d.Invoke(ViewModel!.AboutDialog.RegisterHandler(AboutDialogInteractionRegistering));
                d.Invoke(ViewModel.CopyToClipboard.RegisterHandler(CopyToClipboardInteractionRegistering));
                d.Invoke(ViewModel.ShowBrowser.RegisterHandler(ShowBrowserInteractionRegistering));

                // Buffer
                SendText.KeyDown += SendText_KeyDown;
                SendText.TextChanged += SendText_TextChanged;

                // Console
                SerialHexView.Document = new DynamicBinaryDocument();
                SerialHexView.Selection.RangeChanged += HexView_Selection_RangeChanged;
                ViewModel.Context = new SerialContext(ViewModel, SerialHexView, PlainText);
                ViewModel.WhenAnyValue(x => x.View)
                         .Subscribe(x =>
                         {
                             SerialHexView.IsVisible = x != Views.PlainText;
                             ViewSplitter.IsVisible = x == Views.Both;
                             PlainText.IsVisible = x != Views.Hexdecimal;

                             if (!SerialHexView.IsVisible)
                             {
                                 Grid.SetRow(PlainText, 2);
                                 Grid.SetRowSpan(PlainText, 3);
                             }
                             else if (!PlainText.IsVisible)
                             {
                                 Grid.SetRowSpan(SerialHexView, 3);
                             }
                             else
                             {
                                 Grid.SetRowSpan(SerialHexView, 1);
                                 Grid.SetRow(PlainText, 4);
                                 Grid.SetRowSpan(PlainText, 1);
                             }
                         });

                // Port
                ViewModel.Update();
                var ports = ViewModel.ConnectedPorts ?? [];
                PortList.ItemsSource = ports;
                PortList.SelectedIndex = ports.Any() ? 0 : -1;
                this.Bind(ViewModel, x => x.Port, y => (string?)y.PortList.SelectedItem);
                ViewModel.WhenAnyValue(x => x.ConnectedPorts)
                         .Subscribe(x =>
                         {
                             if (x != null && x.Any())
                             {
                                 PortList.ItemsSource = x;

                                 if (!string.IsNullOrEmpty(ViewModel.Port))
                                 {
                                     PortList.SelectedItem = ViewModel.Port;
                                 }
                                 else
                                 {
                                     PortList.SelectedIndex = 0;
                                 }

                             }
                             else
                             {
                                 PortList.SelectedIndex = -1;
                             }
                         });

                ViewModel.WhenAnyValue(x => x.Port)
                         .Subscribe(x =>
                         {
                             PortList.SelectedItem = x;
                         });

                // Baud
                BaudList.ItemsSource = ViewModel.Bauds;
                BaudList.SelectedItem = ViewModel.Baud;
                this.Bind(ViewModel, x => x.Baud, y => (int)y.BaudList.SelectedItem!);

                // Data Size
                DataSizeList.ItemsSource = ViewModel.DataSizes;
                DataSizeList.SelectedItem = ViewModel.DataSize;
                this.Bind(ViewModel, x => x.DataSize, y => (int)y.DataSizeList.SelectedItem!);

                // Stop Bits
                StopBitsList.ItemsSource = ViewModel.AllowedStopBits.Select(x => new ComboBoxItem()
                {
                    DataContext = x,
                    Content = (x == StopBits.OnePointFive) ? "1.5" : ((int)x).ToString()
                }).OrderBy(x => x.Content!.ToString());
                StopBitsList.SelectedIndex = 0;
                ViewModel.WhenAnyValue(x => x.StopBits)
                         .Subscribe(x =>
                         {
                             for (int i = 0; i < StopBitsList.Items.Count; i++)
                             {
                                 var item = (ComboBoxItem)StopBitsList.Items[i]!;
                                 if ((StopBits)item.DataContext! == x)
                                 {
                                     StopBitsList.SelectedIndex = i;
                                     return;
                                 }
                             }
                         });

                // Parity
                ParityList.ItemsSource = Enum.GetNames<Parity>();
                ParityList.SelectedItem = ViewModel.Parity.ToString();
                ViewModel.WhenAnyValue(x => x.Parity)
                         .Subscribe(x => Enum.Parse<Parity>((string)ParityList.SelectedItem));

                // Handshake
                HandshakeList.ItemsSource = Enum.GetNames<Handshake>();
                HandshakeList.SelectedItem = ViewModel.Handshake.ToString();
                ViewModel.WhenAnyValue(x => x.Handshake)
                         .Subscribe(x => Enum.Parse<Handshake>((string)HandshakeList.SelectedItem));

                // Linebreak
                LinebreakList.ItemsSource = Enum.GetNames<LinebreakKinds>();
                ViewModel.WhenAnyValue(x => x.LinebreakKind)
                         .Subscribe(x => LinebreakList.SelectedIndex = (int)x);
                this.WhenAnyValue(x => x.LinebreakList.SelectedIndex)
                         .Subscribe(x => ViewModel.LinebreakKind = (LinebreakKinds)x);

                // Encoding Mode
                ViewModel.WhenAnyValue(x => x.EncodingMode)
                         .Buffer(2, 1)
                         .Select(b => (Previous: b[0], Current: b[1]))
                         .Subscribe(x =>
                         {
                             var buffer = SendText.Text ?? string.Empty;

                             if (x.Previous == EncodingMode.Char && x.Current == EncodingMode.Bin)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromCharToBin(buffer);
                             }
                             else if (x.Previous == EncodingMode.Char && x.Current == EncodingMode.Hex)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromCharToHex(buffer);
                             }
                             else if (x.Previous == EncodingMode.Bin && x.Current == EncodingMode.Char)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromBinToChar(buffer);
                             }
                             else if (x.Previous == EncodingMode.Bin && x.Current == EncodingMode.Hex)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromBinToHex(buffer);
                             }
                             else if (x.Previous == EncodingMode.Hex && x.Current == EncodingMode.Char)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromHexToChar(buffer);
                             }
                             else if (x.Previous == EncodingMode.Hex && x.Current == EncodingMode.Bin)
                             {
                                 SendText.Text = EncodingHelper.ConvertFromHexToBin(buffer);
                             }
                             else
                             {
                                 SendText.Text = null;
                             }
                         });

                // Window
                this.WhenAnyValue(x => x.IsActive)
                    .Subscribe(x =>
                    {
                        ViewModel.Update();
                    });
            });
        }

        private void HexView_Selection_RangeChanged(object? sender, EventArgs e)
        {
            if (SerialHexView.Caret.PrimaryColumn is { } column)
            {
                try
                {
                    ViewModel!.SelectedBuffer = column.GetText(SerialHexView.Selection.Range);
                }
                catch
                {
                    ViewModel!.SelectedBuffer = null;
                }
            }
        }

        private void SendText_TextChanged(object? sender, TextChangedEventArgs e)
        {
            switch (ViewModel!.EncodingMode)
            {
                case EncodingMode.Bin:
                    var bin = EncodingHelper.RemoveWhitespace(SendText.Text ?? string.Empty);
                    var bin_output = "";

                    for (int i = 0; i < bin.Length; i++)
                    {
                        bin_output += bin[i];
                        if (i % 8 == 7)
                        {
                            if (i < bin.Length - 1)
                            {
                                bin_output += " ";
                            }
                        }
                    }
                    SendText.Text = bin_output;
                    e.Handled = true;

                    if (SendText.CaretIndex == SendText.Text.Length - 1)
                    {
                        SendText.CaretIndex = SendText.Text.Length;
                    }
                    return;
                case EncodingMode.Hex:
                    var hex = EncodingHelper.RemoveWhitespace(SendText.Text ?? string.Empty);
                    var hex_output = "";

                    for (int i = 0; i < hex.Length; i++)
                    {
                        hex_output += hex[i];
                        if (i % 2 == 1)
                        {
                            if (i < hex.Length - 1)
                            {
                                hex_output += " ";
                            }
                        }
                    }
                    SendText.Text = hex_output.ToUpperInvariant();
                    e.Handled = true;

                    if (SendText.CaretIndex == SendText.Text.Length - 1)
                    {
                        SendText.CaretIndex = SendText.Text.Length;
                    }
                    return;
                default:
                    return;
            }
        }

        private void SendText_KeyDown(object? sender, KeyEventArgs e)
        {
            // Ungültige Tasten werden anhand 'e.Handled = True' blockiert

            switch (ViewModel!.EncodingMode)
            {
                case EncodingMode.Bin:
                    string bin_valid_chars = "01";
                    e.Handled = !bin_valid_chars.Where(x => string.Equals(x.ToString(), e.KeySymbol)).Any();

                    if (SendText.CaretIndex % 9 == 8 && !e.Handled)
                    {
                        SendText.CaretIndex += 1;
                    }
                    return;
                case EncodingMode.Hex:

                    string hex_valid_chars = "0123456789ABCDEFabcdef";
                    e.Handled = !hex_valid_chars.Where(x => string.Equals(x.ToString(), e.KeySymbol)).Any();

                    if (SendText.CaretIndex % 3 == 2 && !e.Handled)
                    {
                        SendText.CaretIndex += 1;
                    }
                    return;
                default:
                    return;
            }
        }

        private void ShowBrowserInteractionRegistering(IInteractionContext<string, Unit> context)
        {
            Utils.ShowBrowser(context.Input);
            context.SetOutput(Unit.Default);
        }

        private void NewWindowInteractionRegistering(IInteractionContext<Unit, Unit> context)
        {
            var dlg = new MainWindow();
            dlg.Show();
            context.SetOutput(Unit.Default);
        }

        private async Task CopyToClipboardInteractionRegistering(IInteractionContext<string?, Unit> context)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            context.SetOutput(Unit.Default);

            if (clipboard != null && !string.IsNullOrEmpty(context.Input))
            {
                var data_object = new DataObject();
                data_object.Set(DataFormats.Text, context.Input);
                await clipboard.SetDataObjectAsync(data_object);
                return;
            }

            await Task.Yield();
        }

        private async Task AboutDialogInteractionRegistering(IInteractionContext<Unit, Unit> context)
        {
            var dlg = new AboutDialog();
            dlg.ViewModel = new AboutDialogViewModel();
            await dlg.ShowDialog(this);
            context.SetOutput(Unit.Default);
        }

        private class SerialContext : ISerialContext
        {
            private string _content = string.Empty;
            private string? _rx_buffer = null;
            private HexEditor _hex;
            private TextBox _plain;
            MainWindowViewModel _vm;
            // Lasse den Empfang im Hintergrund so lange laufen
            // Während des laufenden Vorganges können die Texte immer wieder empfangen werden
            private BackgroundWorker _rx_worker;

            public SerialContext(MainWindowViewModel vm, HexEditor hex, TextBox plain)
            {
                _vm = vm;
                _hex = hex;
                _plain = plain;
                _rx_worker = new BackgroundWorker();
                _rx_worker.DoWork += RxWorker_DoWork;
            }

            private void RxWorker_DoWork(object? sender, DoWorkEventArgs e)
            {
                while (_rx_buffer?.Length > 0)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!string.IsNullOrEmpty(_rx_buffer))
                        {
                            _content += _rx_buffer;
                            _vm.TerminalText = _content;
                            UpdateView();
                            _rx_buffer = null;
                        }
                    });
                }
            }

            public void Receive(string? value, SerialState state)
            {
                _rx_buffer = value;
                _rx_worker.RunWorkerAsync();
            }

            public void Reset()
            {
                _content = string.Empty;
                UpdateView();
            }

            public void Send(string? value, SerialState state)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _content += value;
                    UpdateView();
                }
            }

            private void UpdateView()
            {
                _hex.Document = new MemoryBinaryDocument(Encoding.ASCII.GetBytes(_content));
                _plain.Text = _content;
                _vm.TerminalText = _content;
            }
        }
    }
}