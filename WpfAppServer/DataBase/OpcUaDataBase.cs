using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Opc.Ua;

namespace WpfAppServer.DataBase
{
    public class OpcUaDataBase
    {
        // 数据库文件路径
        private static readonly string DbPath = $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HistoryData.db")}";
        private static readonly object _dbLock = new object(); // 数据库锁

        /// <summary>
        /// 1. 初始化数据库（建表）
        /// </summary>
        public static void Initialize()
        {
            lock (_dbLock)
            {
                using (var connection = new SQLiteConnection(DbPath))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                         CREATE TABLE IF NOT EXISTS HistoryData (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        NodeId TEXT NOT NULL,
                        Timestamp INTEGER NOT NULL,
                        Value REAL NOT NULL
                            )";
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// 2. 插入一条历史数据
        /// </summary>
        public static void Insert(double value, DateTime timestamp, string nodeId)
        {
            lock (_dbLock)
            {
                using (var connection = new SQLiteConnection(DbPath))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO HistoryData (NodeId, Timestamp, Value) VALUES (@nodeId, @ts, @val)";
                        command.Parameters.AddWithValue("@nodeId", nodeId);
                        command.Parameters.AddWithValue("@ts", timestamp.Ticks);
                        command.Parameters.AddWithValue("@val", value);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// 3. 根据时间范围查询历史数据
        /// </summary>
        public static List<DataValue> Query(DateTime start, DateTime end, string nodeId)
        {
            var result = new List<DataValue>();
            lock (_dbLock)
            {
                using (var connection = new SQLiteConnection(DbPath))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT Timestamp, Value FROM HistoryData WHERE NodeId = @nodeId AND Timestamp >= @start AND Timestamp <= @end ORDER BY Timestamp ASC";
                        command.Parameters.AddWithValue("@nodeId", nodeId);
                        command.Parameters.AddWithValue("@start", start.Ticks);
                        command.Parameters.AddWithValue("@end", end.Ticks);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                long ticks = reader.GetInt64(0);
                                double val = reader.GetDouble(1);

                                // 还原为 OPC UA 的 DataValue 对象
                                result.Add(new DataValue(
                                    new Variant(val),
                                    StatusCodes.Good,
                                    new DateTime(ticks)
                                ));
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
