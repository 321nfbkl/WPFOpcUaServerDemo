using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfAppServer.DataBase;
using WpfAppServer.Service;

namespace WpfAppServer
{
    /// <summary>
    /// 构建数据模型(地址空间 Address Space )
    /// OPC UA 的地址空间就像一棵倒挂的树（或者像文件资源管理器）。根节点是 Objects（i=85）。
    /// 这段代码的核心任务就是：在 Objects 下面创建一个叫 MyData 的文件夹，并在里面放一个叫 Temperature 的变量。
    /// </summary>
    public class MyNodeManager : CustomNodeManager2
    {
        /// <summary>
        /// OPC UA 是全球联网的，为了防止你的 Temperature 和别人公司的 Temperature 重名，必须分配一个唯一的命名空间。
        /// 服务器会自动给这个 URI 分配一个数字索引（比如 ns=2）。你的所有自定义节点都会带上这个前缀（如 ns=2;s=Temperature）。
        /// </summary>
        public static readonly string NamespaceUri = "http://cathyLuo.com/opcua/server";
        BaseDataVariableState temperatureNode;
        BaseDataVariableState humidityNode;
        BaseDataVariableState pressureNode;
        BaseDataVariableState distanceNode;
        BaseDataVariableState alarmNode;
        private ModbusRTUService _modbusService;
        private ModbusTcpService _modbusTcpService; 

        public MyNodeManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration, NamespaceUri) { }

        /// <summary>
        /// 重写创建地址空间
        /// </summary>
        /// <param name="externalReferences"></param>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                OpcUaDataBase.Initialize();

                // 1. 处理根文件夹到 Objects 节点的外部引用
                IList<IReference> references;
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                // 2. 创建根文件夹（使用 null 作为父节点，避免自动挂载）
                FolderState rootFolder = new FolderState(null);
                rootFolder.SymbolicName = "MyData";
                rootFolder.ReferenceTypeId = ReferenceTypeIds.Organizes;
                rootFolder.TypeDefinitionId = ObjectTypeIds.FolderType;
                rootFolder.NodeId = new NodeId("MyData", NamespaceIndex);
                rootFolder.BrowseName = new QualifiedName("MyData", NamespaceIndex);
                rootFolder.DisplayName = new LocalizedText("MyData");
                rootFolder.WriteMask = AttributeWriteMask.None;
                rootFolder.UserWriteMask = AttributeWriteMask.None;
                rootFolder.EventNotifier = EventNotifiers.None;

                // 建立双向引用：MyData - Objects
                rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, rootFolder.NodeId));

                // 将文件夹加入地址空间
                AddPredefinedNode(SystemContext, rootFolder);

                // 3. 创建温度变量（关键修复：先 null 父节点创建，设好属性再挂载）
                temperatureNode = new BaseDataVariableState(null);
                temperatureNode.NodeId = new NodeId("Temperature", NamespaceIndex);// NodeId 是 ns=2;s=Temperature
                temperatureNode.BrowseName = new QualifiedName("Temperature", NamespaceIndex);
                temperatureNode.DisplayName = new LocalizedText("Temperature");
                temperatureNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                temperatureNode.DataType = DataTypeIds.Double;
                temperatureNode.Value = 25.0;
                //AccessLevel 决定了客户端能不能修改它。如果只给 CurrentRead，客户端尝试写入时就会报 BadNotWritable
                temperatureNode.AccessLevel = AccessLevels.CurrentReadOrWrite | AccessLevels.HistoryRead;
                temperatureNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite | AccessLevels.HistoryRead;
                temperatureNode.OnSimpleWriteValue = OnWriteTemperature;
                temperatureNode.OnSimpleReadValue = OnReadTemperature;
                temperatureNode.Historizing = true;// 告诉服务器：这个节点需要记录历史

                // 将变量加入文件夹和地址空间
                rootFolder.AddChild(temperatureNode);
                AddPredefinedNode(SystemContext, temperatureNode);
                System.Diagnostics.Debug.WriteLine($"[CreateAddressSpace] Temperature NodeId = {temperatureNode.NodeId}");

                humidityNode = new BaseDataVariableState(null);
                humidityNode.NodeId = new NodeId("Humidity", NamespaceIndex);
                humidityNode.BrowseName = new QualifiedName("Humidity", NamespaceIndex);
                humidityNode.DisplayName = new LocalizedText("Humidity");
                humidityNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                humidityNode.DataType = DataTypeIds.Double;
                humidityNode.Value = 50.0;
                humidityNode.AccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                humidityNode.UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                humidityNode.Historizing = true; // 如果你想存历史
                rootFolder.AddChild(humidityNode);
                AddPredefinedNode(SystemContext, humidityNode);

                //创建压力节点
                pressureNode = new BaseDataVariableState(null);
                pressureNode.NodeId = new NodeId("Pressure", NamespaceIndex);// NodeId 是 ns=2;s=Pressure
                pressureNode.BrowseName = new QualifiedName("Pressure", NamespaceIndex);
                pressureNode.DisplayName = new LocalizedText("Pressure");
                pressureNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                pressureNode.DataType = DataTypeIds.Double;
                pressureNode.Value = 101.3;
                //AccessLevel 决定了客户端能不能修改它。如果只给 CurrentRead，客户端尝试写入时就会报 BadNotWritable
                pressureNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
                pressureNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
                pressureNode.Historizing = false;

                rootFolder.AddChild(pressureNode);
                AddPredefinedNode(SystemContext, pressureNode);

                distanceNode = new BaseDataVariableState(null);
                distanceNode.NodeId = new NodeId("Distance", NamespaceIndex); // NodeId 是 ns=2;s=Alarms/IsAlarmActive
                distanceNode.BrowseName = new QualifiedName("Distance", NamespaceIndex);
                distanceNode.DisplayName = new LocalizedText("Distance");
                distanceNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                distanceNode.DataType = DataTypeIds.Double; // 注意：布尔类型
                distanceNode.Value = 80.1;
                distanceNode.AccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                distanceNode.UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                distanceNode.Historizing = true; // 开启历史
                // 挂载节点
                // 假设你有一个 alarmFolder，如果没有，可以用 rootFolder
                rootFolder.AddChild(distanceNode);
                AddPredefinedNode(SystemContext, distanceNode);

                // 创建报警变量（假设你之前建了一个 alarmRootFolder）
                alarmNode = new BaseDataVariableState(null);
                alarmNode.NodeId = new NodeId("Alarms/IsAlarmActive", NamespaceIndex); // NodeId 是 ns=2;s=Alarms/IsAlarmActive
                alarmNode.BrowseName = new QualifiedName("IsAlarmActive", NamespaceIndex);
                alarmNode.DisplayName = new LocalizedText("是否报警");
                alarmNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                alarmNode.DataType = DataTypeIds.Boolean; // 注意：布尔类型
                alarmNode.Value = false;
                alarmNode.AccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                alarmNode.UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.HistoryRead;
                alarmNode.Historizing = true; // 开启历史

                // 挂载节点
                // 假设你有一个 alarmFolder，如果没有，可以用 rootFolder
                rootFolder.AddChild(alarmNode);
                AddPredefinedNode(SystemContext, alarmNode);

                // 1. 创建方法节点
                var coolDownMethod = new MethodState(null);
                coolDownMethod.NodeId = new NodeId("CoolDown", NamespaceIndex);
                coolDownMethod.BrowseName = new QualifiedName("CoolDown", NamespaceIndex);
                coolDownMethod.DisplayName = new LocalizedText("一键降温");
                coolDownMethod.Executable = true;
                coolDownMethod.UserExecutable = true;
                // 2.使用 OnCallMethod 和 GenericMethodCalledEventHandler
                coolDownMethod.OnCallMethod = new GenericMethodCalledEventHandler(OnCoolDownCalled);
                // 3. 挂载到文件夹
                rootFolder.AddChild(coolDownMethod);
                AddPredefinedNode(SystemContext, coolDownMethod);
                try
                {
                    _modbusService = new ModbusRTUService("COM3", 9600, 1, 0, 10.0);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Modbus] 串口打开失败: {ex.Message}");
                }

                _modbusTcpService= new ModbusTcpService("192.168.9.88", 502, 1);

        StartSimulation();
            }
        }

        protected override void HistoryReadRawModified(
          ServerSystemContext context,
          ReadRawModifiedDetails details,
          TimestampsToReturn timestampsToReturn,
          IList<HistoryReadValueId> nodesToRead,
          IList<HistoryReadResult> results,
          IList<ServiceResult> errors,
          List<NodeHandle> nodesToProcess,
          IDictionary<NodeId, NodeState> cache)
        {
            for (int i = 0; i < nodesToRead.Count; i++)
            {
                var nodeId = nodesToRead[i].NodeId;
                var identifier = nodeId.Identifier?.ToString();

                if (identifier == "Temperature" || identifier == "Alarms/IsAlarmActive")
                {
                    var historyData = new HistoryData();
                    historyData.DataValues = new DataValueCollection();

                    // 从数据库查询，建议按节点标识过滤（需要修改数据库层）
                    var filtered = OpcUaDataBase.Query(DateTime.MinValue, DateTime.MaxValue, identifier);

                    if (details.NumValuesPerNode > 0 && filtered.Count > details.NumValuesPerNode)
                    {
                        filtered = filtered.Take((int)details.NumValuesPerNode).ToList();
                    }

                    if (filtered.Count > 0)
                    {
                        historyData.DataValues.AddRange(filtered);
                    }

                    results[i] = new HistoryReadResult
                    {
                        HistoryData = new ExtensionObject(historyData),
                        StatusCode = StatusCodes.Good
                    };
                    errors[i] = ServiceResult.Good;
                }
                else
                {
                    results[i] = new HistoryReadResult();
                    errors[i] = StatusCodes.BadNodeIdUnknown;
                }
            }
        }

        private ServiceResult OnCoolDownCalled(
      ISystemContext context,
      MethodState method,
      IList<object> inputArguments,
      IList<object> outputArguments)
        {
            // 你的业务逻辑
            System.Diagnostics.Debug.WriteLine("收到客户端的调用请求：执行一键降温");

            // 示例：修改温度变量
            temperatureNode.Value = 20.0;
            temperatureNode.ClearChangeMasks(SystemContext, true);

            return ServiceResult.Good;
        }

        private ServiceResult OnWriteTemperature(ISystemContext context, NodeState node, ref object value)
        {
            // value 是客户端发来的新值
            if (value is double newTemp)
            {
                if (newTemp > 100.0)
                {
                    // 返回错误码，客户端会立刻收到拒绝提示！
                    return StatusCodes.BadOutOfRange;
                }

                // 可以在这里写你的业务逻辑，比如同步给 PLC
                System.Diagnostics.Debug.WriteLine($"[拦截] 成功接收并写入温度: {newTemp}");
            }

            // 如果没问题，返回 Good，允许底层修改值
            return ServiceResult.Good;
        }

        private ServiceResult OnReadTemperature(ISystemContext context, NodeState node, ref object value)
        {
            // 比如在这里可以随时去真实的传感器读一个最新值
            //value = DateTime.Now.Second; // 假装返回一个动态变化的值
            return ServiceResult.Good;
        }


        // 在类里定义一个后台任务
        private void StartSimulation()
        {

            Task.Run(async () =>
            {
                var random = new Random();

                while (true)
                {
                    await Task.Delay(1000);

                    // ================= 1. 更新温度 (模拟) =================
                    //double newTemp = 20.0 + random.NextDouble() * 10.0;
                    //temperatureNode.Value = newTemp;
                    //temperatureNode.ClearChangeMasks(SystemContext, false);
                    //SaveHistory(newTemp, "Temperature"); // 存历史

                    // ================= 2. 更新压力 (模拟) =================
                    double newPressure = 100.0 + random.NextDouble() * 5.0;
                    pressureNode.Value = newPressure;
                    pressureNode.ClearChangeMasks(SystemContext, false);

                    // ================= 3. 更新距离 (真实 Modbus) =================
                    double currentDistance = 0;
                    bool isDistanceValid = false;

                    if (_modbusService != null)
                    {
                        var result = _modbusService.ReadInt16s(29, 1);
                        if (result != null && result.TryGetValue(29, out short rawValue))
                        {
                            currentDistance = rawValue / 10.0;
                            isDistanceValid = true;
                            System.Diagnostics.Debug.WriteLine($"[Modbus] 读到距离原始值: {rawValue}, 转换后: {currentDistance}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Modbus] 读距离失败，设备无响应");
                        }
                    }

                    if (_modbusTcpService != null)
                    {
                        if (_modbusTcpService.ReadTempAndHum(out double temp, out double hum))
                        {
                            temperatureNode.Value = temp;
                            temperatureNode.ClearChangeMasks(SystemContext, false);
                            SaveHistory(temp, "Temperature"); // 存历史
                            humidityNode.Value = hum; // 记得在 CreateAddressSpace 里加湿度节点
                            humidityNode.ClearChangeMasks(SystemContext, false);

                            //System.Diagnostics.Debug.WriteLine($"[TCP] 温度: {temp}, 湿度: {hum}");
                        }
                    }

                    if (isDistanceValid)
                    {
                        distanceNode.Value = currentDistance;
                        distanceNode.ClearChangeMasks(SystemContext, false);
                        SaveHistory(currentDistance, "Distance"); // 存历史
                    }

                    // ================= 4. 更新报警 =================
                    // 假设报警逻辑：温度 > 28 报警
                    bool isAlarm =(double)temperatureNode.Value > 28.0;
                    if (alarmNode.Value is bool currentAlarm && currentAlarm != isAlarm)
                    {
                        alarmNode.Value = isAlarm;
                        alarmNode.ClearChangeMasks(SystemContext, false);
                        System.Diagnostics.Debug.WriteLine($"[报警] 状态变更: {isAlarm}");
                        SaveHistory(isAlarm ? 1.0 : 0.0, "Alarms/IsAlarmActive"); // 存历史
                    }
                }
            });
        }

        // ✅ 修正后的保存历史方法（必须传入 nodeId，否则全存到一个节点上了）
        private void SaveHistory(double value, string nodeId)
        {
            OpcUaDataBase.Insert(value, DateTime.UtcNow, nodeId);
        }

        private void RecordHistory(bool isAlarm, double value)
        {
            OpcUaDataBase.Insert(isAlarm ? 1.0 : 0.0, DateTime.UtcNow, "Alarms/IsAlarmActive");
        }
    }
}