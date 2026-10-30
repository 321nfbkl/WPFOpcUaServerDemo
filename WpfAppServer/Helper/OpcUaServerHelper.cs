using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.Configuration; // 必须引入
using System.Threading.Tasks;

namespace WpfAppServer
{
    public class OpcUaServerHelper
    {
        private ApplicationInstance _application;
        private MyUaServer _server;

        public event Action<string> OnLog;
        public event Action<string> OnError;

        public bool IsRunning => _application != null;

        public async Task StartServerAsync()
        {
            if (IsRunning)
            {
                OnLog?.Invoke("服务器已经在运行中，无需重复启动。");
                return;
            }

            try
            {
                // 1. 从 App.config 读取配置 (带默认值兜底)
                string appName = ConfigurationManager.AppSettings["OpcUa.ApplicationName"] ?? "MyWpfUaServer";
                string endpointUrl = ConfigurationManager.AppSettings["OpcUa.EndpointUrl"] ?? "opc.tcp://localhost:4840/MyWpfUaServer";

                string certPath = ConfigurationManager.AppSettings["OpcUa.Cert.Path"] ?? "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\MachineDefault";
                string certSubject = ConfigurationManager.AppSettings["OpcUa.Cert.Subject"] ?? "CN=MyWpfUaServer";
                string issuerPath = ConfigurationManager.AppSettings["OpcUa.Cert.TrustedIssuerPath"] ?? "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\UA Certificate Authorities";
                string peerPath = ConfigurationManager.AppSettings["OpcUa.Cert.TrustedPeerPath"] ?? "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\UA Applications";
                string rejectedPath = ConfigurationManager.AppSettings["OpcUa.Cert.RejectedPath"] ?? "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\RejectedCertificates";

                bool autoAccept = true;
                if (bool.TryParse(ConfigurationManager.AppSettings["OpcUa.Cert.AutoAccept"], out bool parsedAutoAccept))
                {
                    autoAccept = parsedAutoAccept;
                }

                // 2. 构建配置
                var config = new ApplicationConfiguration()
                {
                    ApplicationName = appName,
                    ApplicationType = ApplicationType.Server,
                    TransportQuotas = new TransportQuotas
                    {
                        OperationTimeout = 60000,
                        MaxStringLength = 1048576,
                        MaxByteStringLength = 1048576,
                        MaxArrayLength = 65535,
                        MaxMessageSize = 4194304,
                        MaxBufferSize = 65535,
                        ChannelLifetime = 300000,
                        SecurityTokenLifetime = 3600000
                    },
                    ServerConfiguration = new ServerConfiguration
                    {
                        SecurityPolicies = new ServerSecurityPolicyCollection
                        {
                            new ServerSecurityPolicy { SecurityMode = MessageSecurityMode.None, SecurityPolicyUri = SecurityPolicies.None }
                        },
                        UserTokenPolicies = new UserTokenPolicyCollection
                        {
                            new UserTokenPolicy(UserTokenType.Anonymous)
                        }
                    },
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = certPath,
                            SubjectName = certSubject
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = issuerPath
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = peerPath
                        },
                        RejectedCertificateStore = new CertificateStoreIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = rejectedPath
                        },
                        AutoAcceptUntrustedCertificates = autoAccept
                    }
                };

                // 3. 注入端点地址
                config.ServerConfiguration.BaseAddresses.Add(endpointUrl);

                await config.Validate(ApplicationType.Server);

                _application = new ApplicationInstance(config);

                bool haveAppCertificate = await _application.CheckApplicationInstanceCertificatesAsync(false, 0);
                if (!haveAppCertificate)
                {
                    throw new Exception("创建应用程序证书失败。");
                }

                _server = new MyUaServer();
                await _application.Start(_server);

                OnLog?.Invoke($"OPC UA 服务器已启动！\n端点: {endpointUrl}");
            }
            catch (Exception ex)
            {
                _application = null;
                _server = null;
                OnError?.Invoke($"服务器启动失败: {ex.GetBaseException().Message}");
            }
        }

        public void StopServer()
        {
            if (_application != null)
            {
                try
                {
                    _application.Stop();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"停止服务器时发生异常: {ex.Message}");
                }
                finally
                {
                    _application = null;
                    _server = null;
                    OnLog?.Invoke("OPC UA 服务器已停止。");
                }
            }
            else
            {
                OnLog?.Invoke("服务器当前未运行。");
            }
        }
    }
}