using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    public class IPTableEntry
    {
        private const string prefix = "IPTableEntry | ";

        public string ParentDevice { get; set; }
        public int ID { get; set; }
        public int DeviceID { get; set; }
        public string RoomID {  get; set; }
        public string ModelName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }

        public int Port { get; }

        public IPTableEntry(string ipAddress, int id, string type, int port, string model = "", string description = "", int deviceID = 0, string roomID = "") 
        {
            Log.Debug($"{prefix} Creating IP Table Entry: {id} -> {ipAddress} @ {port} [{model} {description} {deviceID} {roomID}]");
            
            this.ParentDevice = ipAddress; 
            
            this.ID = id;
            this.Port = port;
            this.Type = type.ToUpper();
            this.ModelName = model.ToUpper();
            this.Description = description;
            this.DeviceID = deviceID;
            this.RoomID = roomID;
        }

        public override string ToString()
        {
            return $"IP Table Entry: {this.ID} -> {this.ParentDevice} @ {this.Port} [{this.ModelName}] | {this.Description} / Room ID: {this.RoomID} / Device ID: {this.DeviceID}";
        }
    }
}
