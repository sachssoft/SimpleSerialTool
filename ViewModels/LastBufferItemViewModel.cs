// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using ReactiveUI;

namespace sachssoft.SimpleSerialTool
{
    public class LastBufferItemViewModel : ReactiveObject
    {
        private EncodingMode _encoding_mode;
        private string? _text;
        private bool _is_display_set = false;
        private string? _display;

        public EncodingMode EncodingMode
        {
            get => _encoding_mode;
            init => _encoding_mode = value;
        }

        public string? Text
        {
            get => _text;
            init => _text = value;
        }

        public string? Display
        {
            get
            {
                if (!_is_display_set)
                {
                    switch (_encoding_mode)
                    {
                        case EncodingMode.Char:
                            _display = _text ?? string.Empty;
                            string[] characters_to_replace = [@"\t", @"\n", @"\r", " "];

                            foreach (string s in characters_to_replace)
                            {
                                _display = _display.Replace(s, "");
                            }
                            break;
                        case EncodingMode.Bin:
                            _display = EncodingHelper.ConvertFromCharToBin(_text ?? string.Empty);
                            break;
                        case EncodingMode.Hex:
                            _display = EncodingHelper.ConvertFromCharToHex(_text ?? string.Empty);
                            break;
                    }

                    _is_display_set = true;
                }

                return _display;
            }
        }
    }
}
