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
    }
}
