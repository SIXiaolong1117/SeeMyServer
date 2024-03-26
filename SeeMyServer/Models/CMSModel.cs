using System.ComponentModel;

namespace SeeMyServer.Models
{
    public class CMSModel : INotifyPropertyChanged
    {
        private string _cpuUsage;
        private string _memUsage;
        private string _netReceived;
        private string _netSent;
        private string _hostName;
        private string _upTime;

        public int Id { get; set; }
        public string Name { get; set; }
        public string HostIP { get; set; }
        public string HostPort { get; set; }
        public string SSHUser { get; set; }
        public string SSHPasswd { get; set; }
        public string SSHKey { get; set; }
        public string OSType { get; set; }

        public string CPUUsage
        {
            get { return _cpuUsage; }
            set
            {
                if (_cpuUsage != value)
                {
                    _cpuUsage = value;
                    OnPropertyChanged(nameof(CPUUsage));
                }
            }
        }
        public string MEMUsage
        {
            get { return _memUsage; }
            set
            {
                if (_memUsage != value)
                {
                    _memUsage = value;
                    OnPropertyChanged(nameof(MEMUsage));
                }
            }
        }

        public string NETReceived
        {
            get { return _netReceived; }
            set
            {
                if (_netReceived != value)
                {
                    _netReceived = value;
                    OnPropertyChanged(nameof(NETReceived));
                }
            }
        }

        public string NETSent
        {
            get { return _netSent; }
            set
            {
                if (_netSent != value)
                {
                    _netSent = value;
                    OnPropertyChanged(nameof(NETSent));
                }
            }
        }

        public string HostName
        {
            get { return _hostName; }
            set
            {
                if (_hostName != value)
                {
                    _hostName = value;
                    OnPropertyChanged(nameof(HostName));
                }
            }
        }

        public string UpTime
        {
            get { return _upTime; }
            set
            {
                if (_upTime != value)
                {
                    _upTime = value;
                    OnPropertyChanged(nameof(UpTime));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
