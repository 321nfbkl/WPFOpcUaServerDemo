using Opc.Ua;
using Opc.Ua.Server;

namespace WpfAppServer
{
    public class AlarmNodeManagerFactory : INodeManagerFactory
    {
        public Opc.Ua.StringCollection NamespacesUris
        {
            get
            {
                var collection = new Opc.Ua.StringCollection();
                collection.Add(AlarmNodeManager.AlarmNamespaceUri);
                return collection;
            }
        }

        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            // 这里是精髓：如果未来 AlarmNodeManager 需要额外的参数（如发邮件的服务），就在这里注入
            return new AlarmNodeManager(server, configuration);
        }
    }
}