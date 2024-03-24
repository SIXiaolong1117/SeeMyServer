using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SeeMyServer.Models
{
    public class CMSModel : INotifyPropertyChanged
    {
        private string _cpuUsage;
        private string _memUsage;

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
