using System;
using System.Collections.Generic;
using System.IO.Ports;
using Modbus.Device;
using WpfAppServer.Helper;

namespace WpfAppServer.Service
{

    public class ModbusRTUService : IDisposable
    {
        private SerialPort _serialPort;
        private IModbusMaster _master;
        private object m_lockObj = new object();
        private readonly byte _slaveId;
        private readonly ushort _startAddress;
        private readonly double _divisor;// 换算系数

        public ModbusRTUService(string portName, int baudRate, byte slaveId, ushort startAddress, double divisor = 1.0)
        {
            _slaveId = slaveId;
            _startAddress = startAddress;
            _divisor = divisor;

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _serialPort.Open();
        }


        public Dictionary<uint, short> ReadInt16s(uint startAddress, byte count)
        {
            Dictionary<uint, short> valueList = new Dictionary<uint, short>();
            if (count > 100)
            {
                count = 100;
            }
            var tryCount = 3;
            var readCount = 1 + 1 + 1 + 2 * count + 2;//1字节从机地址，1字节功能码，1字节字节数，2*n个字节寄存器值，2字节crc校验码
            lock (m_lockObj)
            {
                try
                {
                    byte functionCode = 4;
                    byte[] bytes = new byte[8];
                    bytes[0] = _slaveId;//从站地址
                    bytes[1] = functionCode;//功能码

                    bytes[2] = (byte)(startAddress >> 8);//寄存器起始地址高字节
                    bytes[3] = (byte)(startAddress & 0xff);//寄存器起始地址低字节

                    bytes[4] = 0;//寄存器读取数量高字节
                    bytes[5] = count;//寄存器读取数量低字节,此处读取100个寄存器

                    var crcBytes = ModbusHelper.CRC_RTU(bytes, 6);
                    bytes[6] = crcBytes[0];
                    bytes[7] = crcBytes[1];
                    //失败的情况下，尝试读取3次
                    var bytesRead = new byte[] { };
                    for (int i = 0; i < tryCount; i++)
                    {
                        _serialPort.Write(bytes, 0, bytes.Length);
                        bytesRead = ModbusHelper.ReadBytes_RTU(_serialPort, readCount);
                        if (bytesRead.Length > 0)
                        {
                            break;
                        }
                    }
                    if (bytesRead.Length > 0)
                    {
                        var bytesValue = ModbusHelper.GetValuesFromReadBytes_RTU(bytesRead);
                        valueList = ModbusHelper.GetInt16List(startAddress, bytesValue);
                    }
                    else
                    {
                        var warnMsg = nameof(this.ReadInt16s) + $" address:{startAddress},count:{count},{_serialPort.ReadTimeout / 1000}秒内未读到数据";
                    }

                }
                catch (Exception ex)
                {
                    var errorMsg = nameof(this.ReadInt16s) + ex.Message;
                }
                return valueList;
            }
        }

        public void Close()
        {
            _serialPort?.Close();
        }

        public void Dispose()
        {
            _serialPort?.Dispose();
        }
    }
}
