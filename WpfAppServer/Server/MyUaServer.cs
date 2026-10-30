using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Generic;

namespace WpfAppServer
{
    public class MyUaServer : StandardServer
    {
        // 1.5.378.134 允许无参构造
        public MyUaServer() : base() { }

        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            // 1. 把所有的工厂装进一个列表里 (面向接口编程)
            var factories = new List<INodeManagerFactory>
            {
                new MyNodeManagerFactory(),     // 环境数据工厂
                new AlarmNodeManagerFactory()   // 报警数据工厂
            };

            // 2. 通过工厂来创建管理器
            // 2. 遍历工厂，统一生产 NodeManager
            var nodeManagers = new List<INodeManager>();
            foreach (var factory in factories)
            {
                // 不管你是哪个工厂，我都调用 Create 方法
                var manager = factory.Create(server, configuration);
                nodeManagers.Add(manager);
            }
            return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
        }
    }
}