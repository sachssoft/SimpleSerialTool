// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;

namespace sachssoft.SimpleSerialTool
{
    public class MainWindowViewModel : ReactiveObject, IActivatableViewModel
    {
        private string? _port;
        private int _baud;
        private int _data_size;
        private Parity _parity;
        private Handshake _handshake;
        private bool _is_connected;
        private string? _selected_buffer;
        private LinebreakKinds _linebreak_kind = LinebreakKinds.System;
        private readonly Interaction<Unit, Unit> _new_window;
        private readonly Interaction<Unit, Unit> _about_dialog;
        private readonly Interaction<string?, Unit> _copy_to_clipboard;
        private readonly Interaction<string, Unit> _show_browser;
        private SerialPort? _serial = null;
        private StopBits _stop_bits;
        private IEnumerable<string>? _connected_ports;
        private IEnumerable<int> _bauds;
        private IEnumerable<int> _data_sizes;
        private IEnumerable<StopBits> _allowed_stop_bits;
        private bool _received_in_hex;
        private bool _is_rts_enabled;
        private bool _is_dtr_enabled;
        private int _read_timeout = 1000;
        private int _write_timeout = 1000;
        private bool _is_console_text_wrapped;
        private bool _signal_cd;
        private bool _signal_ri;
        private bool _signal_dsr;
        private bool _signal_cts;
        private EncodingMode _encoding_mode;
        private Views _view = Views.Both;
        //private bool _is_plain_text_view_visible = true;
        private bool _has_offset_in_hex_view = true;
        private bool _has_binary_in_hex_view;
        private bool _has_plain_text_in_hex_view = true;
        private ISerialContext? _context = null;
        private string? _terminal_text;
        private ObservableCollection<LastBufferItemViewModel> _last_buffer_items;
        private int _last_buffer_item_index = -1;

        public ViewModelActivator Activator => new();

        public Interaction<Unit, Unit> NewWindow => _new_window;
        public Interaction<Unit, Unit> AboutDialog => _about_dialog;
        public Interaction<string, Unit> ShowBrowser => _show_browser;
        public Interaction<string?, Unit> CopyToClipboard => _copy_to_clipboard;

        public IObservable<bool> ConnectionCondition { get; private set; }
        public IObservable<bool> DisconnectionCondition { get; private set; }

        public ReactiveCommand<Unit, Unit> NewWindowCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> ExitCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> CopyConsoleCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> CopySelectionCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> ClearCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> AboutCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> BugReportCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> DocumentationCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> DisconnectCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; private set; }
        public ReactiveCommand<string?, Unit> SendCommand { get; private set; }
        public ReactiveCommand<LastBufferItemViewModel, Unit> SendLastCommand { get; private set; }
        public ReactiveCommand<LinebreakKinds, Unit> LinebreakKindSwitchCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> ReceivedInHexCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> RTSCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> DTRCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> WrapConsoleTextCommand { get; private set; }
        public ReactiveCommand<EncodingMode, Unit> EncodingModeCommand { get; private set; }
        public ReactiveCommand<Views, Unit> ViewCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> BinaryInHexViewCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> OffsetInHexViewCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> PlainTextInHexViewCommand { get; private set; }

        public MainWindowViewModel()
        {
            _new_window = new Interaction<Unit, Unit>();
            _about_dialog = new Interaction<Unit, Unit>();
            _copy_to_clipboard = new Interaction<string?, Unit>();
            _show_browser = new Interaction<string, Unit>();

            _last_buffer_items = new();

            ConnectionCondition = this.WhenAnyValue(x => x.IsConnected);
            DisconnectionCondition = this.WhenAnyValue(x => x.IsConnected)
                                         .Select(x => !x);

            NewWindowCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await NewWindow.Handle(Unit.Default);
            });

            ExitCommand = ReactiveCommand.Create(() =>
            {
                if (IsConnected)
                {
                    _serial?.Close();
                }

                Environment.Exit(0);
            });

            CopyConsoleCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await CopyToClipboard.Handle(TerminalText);
            }, canExecute: this.WhenAnyValue(x => x.TerminalText)
                               .Select(x => !string.IsNullOrEmpty(x)));

            CopySelectionCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await CopyToClipboard.Handle(SelectedBuffer);
            }, canExecute: this.WhenAnyValue(x => x.SelectedBuffer)
                               .Select(x => x != null && x.Length > 0));

            ClearCommand = ReactiveCommand.Create(() =>
            {
                Context?.Reset();
            }, canExecute: this.WhenAnyValue(x => x.TerminalText)
                               .Select(x => !string.IsNullOrEmpty(x)));

            AboutCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await AboutDialog.Handle(Unit.Default);
            });

            BugReportCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await ShowBrowser.Handle(App.URL_BUG_REPORT);
            });

            DocumentationCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await ShowBrowser.Handle(App.URL_DOCUMENTATION);
            });

            ConnectCommand = ReactiveCommand.Create(() =>
            {
                _serial = new SerialPort()
                {
                    BaudRate = Baud,
                    DataBits = DataSize,
                    Handshake = Handshake,
                    Parity = Parity,
                    StopBits = StopBits.One,
                    PortName = Port,
                    RtsEnable = IsRTSEnabled,
                    DtrEnable = IsDTREnabled,
                    ReadTimeout = ReadTimeout,
                    WriteTimeout = WriteTimeout
                };
                _serial.DataReceived += Serial_DataReceived;
                _serial.ErrorReceived += Serial_ErrorReceived;
                _serial.PinChanged += Serial_PinChanged;

                bool error = false;

                try
                {
                    _serial.Open();
                    IsConnected = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Context?.Receive($"Access is denied to the port '{Port}'", SerialState.Error);
                    error = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    Context?.Receive($"One or more of the properties are invalid.", SerialState.Error);
                    error = true;
                }
                catch (IOException)
                {
                    Context?.Receive($"The state of this port '{Port}' is invalid.", SerialState.Error);
                    error = true;
                }
                finally
                {
                    if (error)
                    {
                        _serial?.Close();
                    }
                }
            });

            DisconnectCommand = ReactiveCommand.Create(() =>
            {
                if (_serial != null)
                {
                    _serial.DataReceived -= Serial_DataReceived;
                    _serial.ErrorReceived -= Serial_ErrorReceived;
                    _serial.PinChanged -= Serial_PinChanged;
                    _serial.Close();
                    _serial.Dispose();
                    _serial = null;
                }

                IsConnected = false;
            });

            UpdateCommand = ReactiveCommand.Create(() =>
            {
                Update();
            });

            SendCommand = ReactiveCommand.Create<string?>((param) =>
            {
                var buffer = EncodingMode switch
                {
                    EncodingMode.Bin => EncodingHelper.ConvertFromBinToChar(param ?? string.Empty),
                    EncodingMode.Hex => EncodingHelper.ConvertFromHexToChar(param ?? string.Empty),
                    _ => param
                };

                _serial!.WriteLine(buffer);
                var text = buffer + GetLineBreak();
                Context?.Send(text, SerialState.Successful);

                // Mehrdeutige Buffer werden nicht in die Liste aufgenommen
                if (!LastBufferItems.Where(x => x.EncodingMode == EncodingMode && x.Text == text).Any())
                {
                    LastBufferItems.Add(new LastBufferItemViewModel()
                    {
                        Text = text,
                        EncodingMode = EncodingMode
                    });

                    LastBufferItemIndex = LastBufferItems.Count - 1;
                }

            }, canExecute: ConnectionCondition);

            SendLastCommand = ReactiveCommand.Create<LastBufferItemViewModel>((param) =>
            {
                var buffer = param.EncodingMode switch
                {
                    EncodingMode.Bin => EncodingHelper.ConvertFromBinToChar(param.Text ?? string.Empty),
                    EncodingMode.Hex => EncodingHelper.ConvertFromHexToChar(param.Text ?? string.Empty),
                    _ => param.Text
                };

                _serial!.WriteLine(buffer);
                var text = buffer + GetLineBreak();
                Context?.Send(text, SerialState.Successful);

            }, canExecute: this.WhenAnyValue(x => x.LastBufferItems.Count, x => x.LastBufferItemIndex, x => x.IsConnected)
                               .Select(x => x.Item1 > 0 && x.Item2 < x.Item1 && x.Item3));

            LinebreakKindSwitchCommand = ReactiveCommand.Create<LinebreakKinds>((param) =>
            {
                LinebreakKind = param;
            });

            ReceivedInHexCommand = ReactiveCommand.Create(() =>
            {
                ReceivedInHex = !ReceivedInHex;
            });

            RTSCommand = ReactiveCommand.Create(() =>
            {
                IsRTSEnabled = !IsRTSEnabled;
            }, canExecute: DisconnectionCondition);

            DTRCommand = ReactiveCommand.Create(() =>
            {
                IsDTREnabled = !IsDTREnabled;
            }, canExecute: DisconnectionCondition);

            WrapConsoleTextCommand = ReactiveCommand.Create(() =>
            {
                IsConsoleTextWrapped = !IsConsoleTextWrapped;
            });

            EncodingModeCommand = ReactiveCommand.Create<EncodingMode>((param) =>
            {
                EncodingMode = param;
            });

            ViewCommand = ReactiveCommand.Create<Views>((param) =>
            {
                View = param;
            });

            OffsetInHexViewCommand = ReactiveCommand.Create(() =>
            {
                HasOffsetInHexView = !HasOffsetInHexView;
            });

            BinaryInHexViewCommand = ReactiveCommand.Create(() =>
            {
                HasBinaryInHexView = !HasBinaryInHexView;
            });

            PlainTextInHexViewCommand = ReactiveCommand.Create(() =>
            {
                HasPlainTextInHexView = !HasPlainTextInHexView;
            });

            _baud = 9600;
            _bauds = [
                50,
                75,
                110,
                134,
                200,
                300,
                600,
                1200,
                1800,
                2400,
                4800,
                9600,
                19200,
                28800,
                38400,
                57600,
                76800,
                115200,
                230400,
                460800,
                5676000,
                921600
            ];

            _data_size = 8;
            _data_sizes = [7, 8];

            _allowed_stop_bits = Enum.GetNames<StopBits>()
                                     .SkipWhile(x => x == nameof(StopBits.None))
                                     .Select(x => Enum.Parse<StopBits>(x));
            _stop_bits = StopBits.One;

            // Fensterbezeichnung
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Title = $"{fvi.ProductName} {fvi.ProductMajorPart}.{fvi.ProductMinorPart}";
        }

        private void Serial_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            SignalCD = (e.EventType & SerialPinChange.CDChanged) != 0;
            SignalCTS = (e.EventType & SerialPinChange.CtsChanged) != 0;
            SignalDSR = (e.EventType & SerialPinChange.DsrChanged) != 0;
            SignalRI = (e.EventType & SerialPinChange.Ring) != 0;
        }

        private void Serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            switch (e.EventType)
            {
                case SerialError.RXParity:
                    break;
                case SerialError.Frame:
                    break;
                case SerialError.Overrun:
                    break;
                case SerialError.RXOver:
                    break;
                case SerialError.TXFull:
                    break;
            }
        }

        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var buffer = _serial!.ReadExisting();
            Context?.Receive(buffer + GetLineBreak(), SerialState.Successful);
        }

        private string GetLineBreak()
        {
            return _linebreak_kind switch
            {
                LinebreakKinds.None => "",
                LinebreakKinds.CR => "\r",
                LinebreakKinds.LF => "\n",
                LinebreakKinds.CR_LF => "\r\n",
                LinebreakKinds.LF_CR => "\n\r",
                _ => Environment.NewLine,
            };
        }

        public void Update()
        {
            ConnectedPorts = SerialPort.GetPortNames()
                                       .Order();

            if (ConnectedPorts.Any())
            {
                if (!ConnectedPorts.Contains(Port))
                {
                    Port = ConnectedPorts.First();
                }
            }
            else
            {
                Port = null;
            }
        }

        public string Title
        {
            get;
        }

        public ObservableCollection<LastBufferItemViewModel> LastBufferItems
        {
            get => _last_buffer_items;
        }

        public int LastBufferItemIndex
        {
            get => _last_buffer_item_index;
            private set => this.RaiseAndSetIfChanged(ref _last_buffer_item_index, value);
        }

        public ISerialContext? Context
        {
            get => _context;
            set => this.RaiseAndSetIfChanged(ref _context, value);
        }

        public IEnumerable<string>? ConnectedPorts
        {
            get => _connected_ports;
            private set => this.RaiseAndSetIfChanged(ref _connected_ports, value);
        }

        public string? Port
        {
            get => _port;
            set => this.RaiseAndSetIfChanged(ref _port, value);
        }

        public IEnumerable<int> Bauds
        {
            get => _bauds;
        }

        public int Baud
        {
            get => _baud;
            set => this.RaiseAndSetIfChanged(ref _baud, value);
        }

        public int DataSize
        {
            get => _data_size;
            set => this.RaiseAndSetIfChanged(ref _data_size, value);
        }

        public IEnumerable<int> DataSizes
        {
            get => _data_sizes;
        }

        public Parity Parity
        {
            get => _parity;
            set => this.RaiseAndSetIfChanged(ref _parity, value);
        }

        public StopBits StopBits
        {
            get => _stop_bits;
            set => this.RaiseAndSetIfChanged(ref _stop_bits, value);
        }

        public IEnumerable<StopBits> AllowedStopBits
        {
            get => _allowed_stop_bits;
        }

        public Handshake Handshake
        {
            get => _handshake;
            set => this.RaiseAndSetIfChanged(ref _handshake, value);
        }

        public bool IsConnected
        {
            get => _is_connected;
            private set => this.RaiseAndSetIfChanged(ref _is_connected, value);
        }

        public string? SelectedBuffer
        {
            get => _selected_buffer;
            set => this.RaiseAndSetIfChanged(ref _selected_buffer, value);
        }

        public string? TerminalText
        {
            get => _terminal_text;
            set => this.RaiseAndSetIfChanged(ref _terminal_text, value);
        }

        public LinebreakKinds LinebreakKind
        {
            get => _linebreak_kind;
            set => this.RaiseAndSetIfChanged(ref _linebreak_kind, value);
        }

        public EncodingMode EncodingMode
        {
            get => _encoding_mode;
            set => this.RaiseAndSetIfChanged(ref _encoding_mode, value);
        }

        public bool ReceivedInHex
        {
            get => _received_in_hex;
            private set => this.RaiseAndSetIfChanged(ref _received_in_hex, value);
        }

        public bool IsRTSEnabled
        {
            get => _is_rts_enabled;
            private set => this.RaiseAndSetIfChanged(ref _is_rts_enabled, value);
        }

        public bool IsDTREnabled
        {
            get => _is_dtr_enabled;
            private set => this.RaiseAndSetIfChanged(ref _is_dtr_enabled, value);
        }

        public bool IsConsoleTextWrapped
        {
            get => _is_console_text_wrapped;
            private set => this.RaiseAndSetIfChanged(ref _is_console_text_wrapped, value);
        }

        public int ReadTimeout
        {
            get => _read_timeout;
            set => this.RaiseAndSetIfChanged(ref _read_timeout, value);
        }

        public int WriteTimeout
        {
            get => _write_timeout;
            set => this.RaiseAndSetIfChanged(ref _write_timeout, value);
        }

        public bool SignalCD
        {
            get => _signal_cd;
            private set => this.RaiseAndSetIfChanged(ref _signal_cd, value);
        }

        public bool SignalRI
        {
            get => _signal_ri;
            private set => this.RaiseAndSetIfChanged(ref _signal_ri, value);
        }

        public bool SignalDSR
        {
            get => _signal_dsr;
            private set => this.RaiseAndSetIfChanged(ref _signal_dsr, value);
        }

        public bool SignalCTS
        {
            get => _signal_cts;
            private set => this.RaiseAndSetIfChanged(ref _signal_cts, value);
        }

        public Views View
        {
            get => _view;
            private set => this.RaiseAndSetIfChanged(ref _view, value);
        }

        public bool HasOffsetInHexView
        {
            get => _has_offset_in_hex_view;
            set => this.RaiseAndSetIfChanged(ref _has_offset_in_hex_view, value);
        }

        public bool HasBinaryInHexView
        {
            get => _has_binary_in_hex_view;
            set => this.RaiseAndSetIfChanged(ref _has_binary_in_hex_view, value);
        }

        public bool HasPlainTextInHexView
        {
            get => _has_plain_text_in_hex_view;
            set => this.RaiseAndSetIfChanged(ref _has_plain_text_in_hex_view, value);
        }
    }
}
