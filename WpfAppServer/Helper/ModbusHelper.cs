using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAppServer.Helper
{
    internal class ModbusHelper
    {
        /// <summary>
        /// CRC校验,ModbusRTU要用到,ModbusTCP用不到
        /// </summary>
        /// <param name="bytes">被校验的CRC字节组</param>
        /// <param name="count">校验字节组的前count个字节</param>
        /// <returns>返回16位CRC校验码，byte数组第一个值为CRC高8位，第二个值为CRC低8位</returns>
        public static byte[] CRC_RTU(byte[] bytes, int count)
        {
            byte[] bytesCRC = new byte[2];
            UInt16 tempCRC = 0xFFFF;
            byte highByte, lowByte;
            for (int i = 0; i < count; i++)
            {
                tempCRC ^= bytes[i];//异或
                for (int j = 0; j < 8; j++)
                {
                    if (tempCRC % 2 == 1)//判断移出位是否为1
                    {
                        tempCRC = (ushort)(tempCRC >> 1);
                        tempCRC ^= 0xA001;
                    }
                    else
                    {
                        tempCRC = (ushort)(tempCRC >> 1);
                    }
                }
            }
            highByte = (byte)((tempCRC >> 8));
            lowByte = (byte)(tempCRC & 0xff);
            byte crcH = lowByte;//将高字节与低字节交换，则得出最终16位的CRC校验码，CRC高8位
            byte crcL = highByte;//将高字节与低字节交换，则得出最终16位的CRC校验码，CRC低8位
            bytesCRC[0] = crcH;
            bytesCRC[1] = crcL;
            return bytesCRC;
        }
        /// <summary>
        /// 将读到的寄存器值转化为32位整型量
        /// </summary>
        /// <param name="startAddress">寄存器起始地址</param>
        /// <param name="bytesRead">读到的字节组中分离出来的数据字节组</param>
        /// <returns>返回的32位整型量列表</returns>
        public static Dictionary<uint, Int32> GetInt32List(uint startAddress, byte[] bytesData)
        {
            var address = startAddress;
            var listInt32 = new Dictionary<uint, Int32>();
            //4个字节计算一次
            for (int i = 0; i < bytesData.Length; i += 4)
            {
                Int32 data = (bytesData[i + 2] << 24) + (bytesData[i + 3] << 16) + (bytesData[i] << 8) + bytesData[i + 1];
                listInt32.Add(address, data);
                address += 2;//每两个16位寄存器组成一个32位整数，所以每次+2
            }
            return listInt32;
        }
        /// <summary>
        /// 将读到的寄存器值转化为16位整型量列表
        /// </summary>
        /// <param name="startAddress">寄存器起始地址</param>
        /// <param name="bytesData">读到的字节组中分离出来的数据字节组</param>
        /// <returns>返回的16位整型量列表</returns>
        public static Dictionary<uint, Int16> GetInt16List(uint startAddress, byte[] bytesData)
        {
            var address = startAddress;
            var listInt16 = new Dictionary<uint, Int16>();
            //2个字节计算一次
            for (int i = 0; i < bytesData.Length; i += 2)
            {
                Int16 data = (short)((bytesData[i] << 8) + bytesData[i + 1]);
                listInt16.Add(address, data);
                address++;
            }
            return listInt16;
        }
        public static Dictionary<uint, bool> GetCoilsList(uint startAddress, byte[] bytesData)
        {
            var address = startAddress;
            var listBool = new Dictionary<uint, bool>();
            //2个字节计算一次
            for (int i = 0; i < bytesData.Length; i++)
            {
                byte data = bytesData[i];
                for (int k = 0; k < 8; k++)
                {
                    bool b = ((data >> k) % 2) == 1 ? true : false;
                    listBool.Add(address, b);
                    address++;
                }
            }
            return listBool;
        }
        public static List<byte> GetBytesFromCoils(List<bool> coils)
        {
            var bytes = new List<byte>();
            var tempCoils = coils.ToArray().ToList();
            var coilsCount = coils.Count;
            var rest = coilsCount % 8;
            if (rest != 0)
            {
                var cout2 = 8 - rest;
                for (int i = 0; i < cout2; i++)
                {
                    tempCoils.Add(false);
                }
            }
            for (int i = 0; i < tempCoils.Count; i += 8)
            {
                byte byte1 = 0;
                for (int j = 0; j < 8; j++)
                {
                    var index = i + j;
                    var value = tempCoils[index] ? 1 : 0;
                    byte1 += (byte)(value << j);
                }
                bytes.Add(byte1);
            }
            return bytes;
        }
        /// <summary>
        /// 从读到的字节组中获取数据字节组
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] GetValuesFromReadBytes_RTU(byte[] bytes)
        {
            var bytesClone = (bytes.Clone() as byte[]).ToList();
            bytesClone.RemoveRange(0, 3);//去掉前三个：从机地址、功能码、字节数
            var bytesCount = bytesClone.Count;
            bytesClone.RemoveRange(bytesCount - 2, 2);//去掉最后两个字节，2字节CRC校验码
            return bytesClone.ToArray();
        }

        public static Dictionary<uint, float> GetFloatList(uint startAddress, byte[] bytesData)
        {
            var address = startAddress;
            var listFloat = new Dictionary<uint, float>();
            //4个字节计算一次
            for (int i = 0; i < bytesData.Length; i += 4)
            {
                var bytes = new List<byte>();
                bytes.Add(bytesData[i + 1]);
                bytes.Add(bytesData[i]);
                bytes.Add(bytesData[i + 3]);
                bytes.Add(bytesData[i + 2]);
                var data = BitConverter.ToSingle(bytes.ToArray(), 0);
                listFloat.Add(address, data);
                address += 2;//每两个16位寄存器组成一个32位整数，所以每次+2
            }
            return listFloat;
        }

        public static byte[] ReadBytes_RTU(SerialPort serialPort, int readCount)
        {
            DateTime startTime = DateTime.Now;
            Thread.Sleep(100);
            while (true)
            {
                if (serialPort.ReadTimeout > 0)
                {
                    if (DateTime.Now.Subtract(startTime).TotalMilliseconds > serialPort.ReadTimeout)
                    {
                        //超时后清空接收缓存
                        serialPort.DiscardOutBuffer();
                        serialPort.DiscardInBuffer();//读完后清空接收缓存
                        Thread.Sleep(100);
                        return new byte[] { };
                    }
                }
                else
                {
                    if (DateTime.Now.Subtract(startTime).TotalSeconds > 3)
                    {
                        //超时后清空接收缓存
                        serialPort.DiscardOutBuffer();
                        serialPort.DiscardInBuffer();//读完后清空接收缓存
                        Thread.Sleep(100);
                        return new byte[] { };
                    }
                }
                if (serialPort.BytesToRead == readCount)//加此判断，是为了保证数据长度正确
                {
                    break;
                }
            }
            //有数据，并且数据长度正确
            var bytes = new byte[serialPort.BytesToRead];
            serialPort.Read(bytes, 0, bytes.Length);
            serialPort.DiscardOutBuffer();
            serialPort.DiscardInBuffer();//读完后清空接收缓存
            Thread.Sleep(100);
            return bytes;
        }
    }
}
