using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeeMyServer.Models
{
    public class MountInfo
    {
        public string FileSystem { get; set; }
        public string Size { get; set; }
        public string Used { get; set; }
        public string Avail { get; set; }
        public string UsePercentage { get; set; }
        public string MountedOn { get; set; }
        public decimal SectorsReadPerSecondOrigin { get; set; }
        public decimal SectorsWrittenPerSecondOrigin { get; set; }
        public string SectorsReadPerSecond { get; set; }
        public string SectorsWrittenPerSecond { get; set; }
        public long SectorsRead { get; set; }
        public long SectorsWritten { get; set; }
        public string SectorsReadBytes { get; set; }
        public string SectorsWrittenBytes { get; set; }
    }
}
