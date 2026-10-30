using Modbus.Device;
using System;
using System.Net.Sockets;

namespace WpfAppServer.Service
{
    public class ModbusTcpService : IDisposable
    {
        private TcpClient _tcpClient;
        private ModbusIpMaster _ipMaster;
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly byte _unitId;
        private readonly object _lockObj = new object();

        public ModbusTcpService(string ipAddress, int port = 502, byte unitId = 1)
        {
            _ipAddress = ipAddress;
            _port = port;
            _unitId = unitId;
        }

        /// <summary>
        /// 连接设备（会自动处理重连）
        /// </summary>
        private void EnsureConnected()
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                _tcpClient?.Close();
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = 1000;
                _tcpClient.Connect(_ipAddress, _port);

                _ipMaster = ModbusIpMaster.CreateIp(_tcpClient);
                _ipMaster.Transport.ReadTimeout = 1000;
                _ipMaster.Transport.WriteTimeout = 1000;
                _ipMaster.Transport.Retries = 2;
            }
        }

        /// <summary>
        /// 读取温湿度
        /// </summary>
        public bool ReadTempAndHum(out double temp, out double humidity)
        {
            temp = 0;
            humidity = 0;

            lock (_lockObj)
            {
                try
                {
                    EnsureConnected();

                    // 直接调用库方法，读取起始地址 0 开始的 2 个寄存器
                    // 假设 寄存器0：温度，寄存器1：湿度
                    ushort[] values = _ipMaster.ReadHoldingRegisters(_unitId, 0, 2);

                    if (values == null || values.Length < 2) return false;

                    // 解析温度和湿度
                    temp = ProcessTemp(values[0]);
                    humidity = ProcessHumidity(values[1]);

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Modbus TCP] 读取失败: {ex.Message}");
                    _tcpClient?.Close(); // 读取失败，强制断开，下次自动重连
                    return false;
                }
            }
        }

        // 解析温度（处理负数）
        private double ProcessTemp(ushort rawTemp)
        {
            // 直接强转 short 可以完美处理 0x8000 ~ 0xFFFF 的负数情况
            short signedTemp = (short)rawTemp;
            return signedTemp / 10.0;
        }

        // 解析湿度
        private double ProcessHumidity(ushort rawHum)
        {
            return rawHum / 10.0;
        }

        public void Dispose()
        {
            _ipMaster?.Dispose();
            _tcpClient?.Close();
        }
    }
}