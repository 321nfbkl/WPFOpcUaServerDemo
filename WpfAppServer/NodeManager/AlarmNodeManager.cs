using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Generic;

namespace WpfAppServer
{
    public class AlarmNodeManager : CustomNodeManager2
    {
        // 定义报警专属的命名空间
        public static readonly string AlarmNamespaceUri = "http://cathyLuo.com/opcua/alarms";

        public AlarmNodeManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration, AlarmNamespaceUri) { }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                // 1. 处理外部引用（挂载到 Objects 根节点）
                IList<IReference> references;
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                // 2. 创建 Alarms 文件夹
                FolderState rootFolder = new FolderState(null);
                rootFolder.SymbolicName = "Alarms";
                rootFolder.ReferenceTypeId = ReferenceTypeIds.Organizes;
                rootFolder.TypeDefinitionId = ObjectTypeIds.FolderType;
                rootFolder.NodeId = new NodeId("Alarms", NamespaceIndex);
                rootFolder.BrowseName = new QualifiedName("Alarms", NamespaceIndex);
                rootFolder.DisplayName = new LocalizedText("Alarms");

                rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, rootFolder.NodeId));
                AddPredefinedNode(SystemContext, rootFolder);

                // 3. 创建报警状态变量（Bool类型）
                var alarmStatusNode = new BaseDataVariableState(null);
                alarmStatusNode.NodeId = new NodeId("IsAlarmActive", NamespaceIndex);
                alarmStatusNode.BrowseName = new QualifiedName("IsAlarmActive", NamespaceIndex);
                alarmStatusNode.DisplayName = new LocalizedText("IsAlarmActive");
                alarmStatusNode.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
                alarmStatusNode.DataType = DataTypeIds.Boolean; // 注意这里是 Boolean
                alarmStatusNode.Value = false;
                alarmStatusNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
                alarmStatusNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
                alarmStatusNode.Historizing = false;

                rootFolder.AddChild(alarmStatusNode);
                AddPredefinedNode(SystemContext, alarmStatusNode);
            }
        }
    }
}