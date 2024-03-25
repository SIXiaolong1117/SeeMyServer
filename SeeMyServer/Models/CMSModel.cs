using System.ComponentModel;

namespace SeeMyServer.Models
{
    public class CMSModel : INotifyPropertyChanged
    {
        private string _cpuUsage;
        private string _memUsage;
        private string _netReceived;
        private string _netSent;

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
