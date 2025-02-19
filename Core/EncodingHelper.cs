// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

using System;
using System.Collections.Generic;
using System.Text;

namespace sachssoft.SimpleSerialTool
{
    internal static class EncodingHelper
    {
        internal static string RemoveWhitespace(string s)
        {
            return s.Replace(" ", "");
        }

        internal static string ConvertFromBinToChar(string bin)
        {
            bin = RemoveWhitespace(bin);

            var data = GetBytesFromBinaryString(bin);

            return Encoding.ASCII.GetString(data);
        }

        internal static string ConvertFromBinToHex(string bin)
        {
            bin = RemoveWhitespace(bin);

            var data = GetBytesFromBinaryString(bin);
            var output = string.Empty;
            int n = 0;

            foreach (var b in data)
            {
                output += b.ToString("X2");
                n++;

                if (n % 1 == 0)
                {
                    output += " ";
                }
            }

            return output;
        }

        internal static string ConvertFromHexToChar(string hex)
        {
            hex = RemoveWhitespace(hex);

            var data = GetBytesFromHexString(hex);

            return Encoding.ASCII.GetString(data);
        }

        internal static string ConvertFromHexToBin(string hex)
        {
            hex = RemoveWhitespace(hex);

            var data = GetBytesFromHexString(hex);
            var output = string.Empty;

            foreach (var b in data)
            {
                output += Convert.ToString(b, 2).PadLeft(8, '0');
                output += " ";
            }

            if (output.Length > 0)
            {
                output = output.Substring(0, output.Length - 1);
            }

            return output;
        }

        internal static string ConvertFromCharToBin(string hex)
        {
            string output = "";

            foreach (var c in hex)
            {
                output += Convert.ToString(c, 2).PadLeft(8, '0');
                output += " ";
            }

            if (output.Length > 0)
            {
                output = output.Substring(0, output.Length - 1);
            }

            return output;
        }

        internal static string ConvertFromCharToHex(string hex)
        {
            string output = "";
            int n = 0;

            foreach (var c in hex)
            {
                output += Convert.ToByte(c).ToString("X2");
                n++;

                if (n % 1 == 0)
                {
                    output += " ";
                }
            }

            return output;
        }

        private static byte[] GetBytesFromBinaryString(string binary)
        {
            var list = new List<byte>();

            if (binary.Length % 8 != 0)
            {
                var rest = 8 - binary.Length % 8;

                for (int i = 0; i < rest; i++)
                {
                    binary += "0";
                }
            }

            for (int i = 0; i < binary.Length; i += 8)
            {
                var t = binary.Substring(i, 8);
                list.Add(Convert.ToByte(t, 2));
            }

            return list.ToArray();
        }

        private static byte[] GetBytesFromHexString(string hex)
        {
            var list = new List<byte>();

            if (hex.Length % 2 != 0)
            {
                var rest = 2 - hex.Length % 2;

                for (int i = 0; i < rest; i++)
                {
                    hex += "0";
                }
            }

            for (int i = 0; i < hex.Length; i += 2)
            {
                var t = hex.Substring(i, 2);
                list.Add(Convert.ToByte(t, 16));
            }

            return list.ToArray();
        }
    }
}
