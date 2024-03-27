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
        public string DriveLetter { get; set; }
        public string FriendlyName { get; set; }
        public string FileSystemType { get; set; }
        public string DriveType { get; set; }
        public string HealthStatus { get; set; }
        public string OperationalStatus { get; set; }
        public string SizeRemaining { get; set; }
    }
}
