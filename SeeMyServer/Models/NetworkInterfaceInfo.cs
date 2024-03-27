using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeeMyServer.Models
{
    public class NetworkInterfaceInfo
    {
        public string Name { get; set; }
        public string LinkEncap { get; set; }
        public string HWAddr { get; set; }
        public string InetAddr { get; set; }
        public string Bcast { get; set; }
        public string Mask { get; set; }
        public string Inet6Addr { get; set; }
        public string Scope { get; set; }
        public string Status { get; set; }
        public string MTU { get; set; }
        public string Metric { get; set; }
        public string RXPackets { get; set; }
        public string RXErrors { get; set; }
        public string RXDropped { get; set; }
        public string RXOverruns { get; set; }
        public string RXFrame { get; set; }
        public string TXPackets { get; set; }
        public string TXErrors { get; set; }
        public string TXDropped { get; set; }
        public string TXOverruns { get; set; }
        public string TXCarrier { get; set; }
        public string Collisions { get; set; }
        public string TXQueueLen { get; set; }
        public string RXBytes { get; set; }
        public string TXBytes { get; set; }
    }
}
