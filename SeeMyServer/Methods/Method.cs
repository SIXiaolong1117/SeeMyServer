using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;

namespace SeeMyServer.Methods
{
    public class Method
    {
        // SSH执行
        public static string SendSSHCommand(string sshCommand, string sshHost, string sshPort, string sshUser, string sshPasswd, string sshKey, string privateKeyIsOpen)
        {
            sshHost = DomainToIp(sshHost, "IPv4").ToString();
            try
            {
                bool usePrivateKey = string.Equals(privateKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                SshClient sshClient = InitializeSshClient(sshHost, int.Parse(sshPort), sshUser, sshPasswd, sshKey, usePrivateKey);

                if (sshClient != null)
                {
                    return ExecuteSshCommand(sshClient, sshCommand);
                }
                else
                {
                    //return "SSH 客户端初始化失败。";
                    return "0";
                }
            }
            catch (Exception ex)
            {
                //return "SSH 操作失败：" + ex.Message;
                return "0";
            }
        }
        // SSH初始化
        private static SshClient InitializeSshClient(string sshHost, int sshPort, string sshUser, string sshPasswd, string sshKey, bool usePrivateKey)
        {
            try
            {
                if (usePrivateKey)
                {
                    PrivateKeyFile privateKeyFile = new PrivateKeyFile(sshKey);
                    ConnectionInfo connectionInfo = new ConnectionInfo(sshHost, sshPort, sshUser, new PrivateKeyAuthenticationMethod(sshUser, new PrivateKeyFile[] { privateKeyFile }));
                    return new SshClient(connectionInfo);
                }
                else
                {
                    return new SshClient(sshHost, sshPort, sshUser, sshPasswd);
                }
            }
            catch
            {
                return null;
            }
        }
        // SSH返回
        private static string ExecuteSshCommand(SshClient sshClient, string sshCommand)
        {
            try
            {
                sshClient.Connect();

                if (sshClient.IsConnected)
                {
                    SshCommand SSHCommand = sshClient.RunCommand(sshCommand);

                    if (!string.IsNullOrEmpty(SSHCommand.Error))
                    {
                        //return "错误：" + SSHCommand.Error;
                        return "0";
                    }
                    else
                    {
                        return SSHCommand.Result;
                    }
                }
                //return "SSH 命令执行失败。";
                return "0";
            }
            finally
            {
                sshClient.Disconnect();
                sshClient.Dispose();
            }
        }
        // 获取域名对应的IP
        public static IPAddress DomainToIp(string domain, string ipVersion)
        {
            IPAddress ipAddress;
            if (IPAddress.TryParse(domain, out ipAddress))
            {
                // 是IP
                if ((ipVersion == "IPv4" && ipAddress.AddressFamily == AddressFamily.InterNetwork) ||
                    (ipVersion == "IPv6" && ipAddress.AddressFamily == AddressFamily.InterNetworkV6))
                {
                    return ipAddress;
                }
                else
                {
                    throw new ArgumentException("IP version mismatch");
                }
            }
            else
            {
                // 是域名或其他输入
                IPAddress[] addressList = Dns.GetHostEntry(domain).AddressList;

                if (ipVersion == "IPv4")
                {
                    foreach (IPAddress address in addressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return address;
                        }
                    }
                }
                else if (ipVersion == "IPv6")
                {
                    foreach (IPAddress address in addressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            return address;
                        }
                    }
                }

                throw new ArgumentException("No matching IP address found");
            }
        }
    }
}
