using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Specialized;

namespace WpfAppServer
{
    public class MyNodeManagerFactory : INodeManagerFactory
    {
        // 严格按照源码接口实现：属性名 NamespacesUris，类型 StringCollection
        public Opc.Ua.StringCollection NamespacesUris
        {
            get
            {
                var collection = new Opc.Ua.StringCollection();
                collection.Add(MyNodeManager.NamespaceUri);
                return collection;
            }
        }

        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            return new MyNodeManager(server, configuration);
        }
    }
}
