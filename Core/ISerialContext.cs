// MIT License - Copyright (c) 2025 Tobias Sachs
// See LICENSE file for details.

namespace sachssoft.SimpleSerialTool
{
    public interface ISerialContext
    {
        void Send(string? buffer, SerialState state);

        void Receive(string? buffer, SerialState state);

        void Reset();
    }
}
