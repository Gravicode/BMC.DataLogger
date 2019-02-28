using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.SQLite;
using GHI.Processor;
using Microsoft.SPOT.Hardware;
using GHI.Glide;
using GHI.Glide.UI;
using GHI.Glide.Geom;

using System.IO;


using GHI.IO;
using GHI.IO.Storage;
using Microsoft.SPOT.IO;
using System.Text;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;

namespace DataLoggerDevice2
{
    public partial class Program
    {
        static string DataTopic = "bmc/logger/data";

        static FTPServer ftp;
        static string FileNameLog;
        static int Delay = 2000;
        static int Counter = 0;
        EthernetNetwork net;
        GT.SocketInterfaces.AnalogInput[] InputSensors;
        DiskStorage storage;
        MqttAgent Messaging;
        //UI
        GHI.Glide.UI.TextBlock txtTime = null;
        GHI.Glide.UI.DataGrid GvData = null;
        GHI.Glide.UI.Button BtnReset = null;
        GHI.Glide.Display.Window window = null;
        GHI.Glide.UI.TextBlock txtMessage = null;
        //database
        Database myDatabase = null;
        void ProgramStarted()
        {
            InputSensors = new GT.SocketInterfaces.AnalogInput[9];
            InputSensors[0] = breakout.CreateAnalogInput(GT.Socket.Pin.Three);
            InputSensors[1] = breakout.CreateAnalogInput(GT.Socket.Pin.Four);
            InputSensors[2] = breakout.CreateAnalogInput(GT.Socket.Pin.Five);

            InputSensors[3] = breakout2.CreateAnalogInput(GT.Socket.Pin.Three);
            InputSensors[4] = breakout2.CreateAnalogInput(GT.Socket.Pin.Four);
            InputSensors[5] = breakout2.CreateAnalogInput(GT.Socket.Pin.Five);

            InputSensors[6] = breakout3.CreateAnalogInput(GT.Socket.Pin.Three);
            InputSensors[7] = breakout3.CreateAnalogInput(GT.Socket.Pin.Four);
            InputSensors[8] = breakout3.CreateAnalogInput(GT.Socket.Pin.Five);
            
            
            //7" Displays
            Display.Width = 800;
            Display.Height = 480;
            Display.OutputEnableIsFixed = false;
            Display.OutputEnablePolarity = true;
            Display.PixelPolarity = false;
            Display.PixelClockRateKHz = 30000;
            Display.HorizontalSyncPolarity = false;
            Display.HorizontalSyncPulseWidth = 48;
            Display.HorizontalBackPorch = 88;
            Display.HorizontalFrontPorch = 40;
            Display.VerticalSyncPolarity = false;
            Display.VerticalSyncPulseWidth = 3;
            Display.VerticalBackPorch = 32;
            Display.VerticalFrontPorch = 13;
            Display.Type = Display.DisplayType.Lcd;
            if (Display.Save())      // Reboot required?
            {
                PowerState.RebootDevice(false);
            }
            //set up touch screen
            CapacitiveTouchController.Initialize(GHI.Pins.FEZRaptor.Socket14.Pin3);

         
            GlideTouch.Initialize();
            //set glide
            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.MainForm));

            txtTime = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtTime");
            GvData = (GHI.Glide.UI.DataGrid)window.GetChildByName("GvData");
            BtnReset = (GHI.Glide.UI.Button)window.GetChildByName("BtnReset");
            txtMessage = (GHI.Glide.UI.TextBlock)window.GetChildByName("TxtMessage");
            Glide.MainWindow = window;
            
            //setup grid
            //create grid column
            GvData.AddColumn(new DataGridColumn("Time", 200));
            GvData.AddColumn(new DataGridColumn("A0", 200));
            GvData.AddColumn(new DataGridColumn("A1", 200));
            GvData.AddColumn(new DataGridColumn("A2", 200));
            GvData.AddColumn(new DataGridColumn("A3", 200));
            GvData.AddColumn(new DataGridColumn("A4", 200));
            GvData.AddColumn(new DataGridColumn("A5", 200));
            GvData.AddColumn(new DataGridColumn("A6", 200));
            GvData.AddColumn(new DataGridColumn("A7", 200));
            GvData.AddColumn(new DataGridColumn("A8", 200));

            // Create a database in memory,
            // file system is possible however!
            myDatabase = new GHI.SQLite.Database();
            myDatabase.ExecuteNonQuery("CREATE Table Sensor" +
            " (Time TEXT, A0 DOUBLE,A1 DOUBLE,A2 DOUBLE,A3 DOUBLE,A4 DOUBLE,A5 DOUBLE,A6 DOUBLE,A7 DOUBLE,A8 DOUBLE)");
            //reset database n display
            BtnReset.TapEvent += (object sender) =>
            {
                Counter = 0;
                myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                GvData.Clear();
                GvData.Invalidate();
            };

            Glide.MainWindow = window;

            storage = new DiskStorage(sdCard);
            net = new EthernetNetwork(ethernetENC28);
           
            net.NetworkConnected+=(ip)=>{
                PrintToLcd("network connected:"+ip);
                Thread.Sleep(1000);
                Messaging = new MqttAgent("110.35.82.86", "loradev_mqtt", "test123", "BMC_Logger", DataTopic);
                if (Messaging.IsReady) PrintToLcd("mqtt is ready");
                Thread.Sleep(1000);
                // get Internet Time using NTP
                NTPTime("time.windows.com", -420);
                PrintToLcd("time is updated:" + DateTime.Now.ToString());
                Debug.Print("Init FTP");
                StartFTP(ip);
            };
           

            Thread th1 = new Thread(new ThreadStart(Loop));
            th1.Start();

        }
      





        private static void OnTap(object sender)
        {
            Debug.Print("Button tapped.");
        }
        void Loop()
        {
            int counter = 0;
            while (true)
            {
                counter++;
                var data = new SensorData()
                {
                    A0= InputSensors[0].ReadVoltage(),
                    A1 = InputSensors[1].ReadVoltage(),
                    A2 = InputSensors[2].ReadVoltage(),
                    A3 = InputSensors[3].ReadVoltage(),
                    A4 = InputSensors[4].ReadVoltage(),
                    A5 = InputSensors[5].ReadVoltage(),
                    A6 = InputSensors[6].ReadVoltage(),
                    A7 = InputSensors[7].ReadVoltage(),
                    A8 = InputSensors[8].ReadVoltage(),
                };
                var jsonStr = Json.NETMF.JsonSerializer.SerializeObject(data);
                SendToMqtt(DataTopic,jsonStr);
                Debug.Print("kirim :" + jsonStr);
                PrintToLcd("send count: " + counter);
                var TimeStr = DateTime.Now.ToString("dd/MM/yy HH:mm");
                //send data to mqtt or save data to sdcard
                FileNameLog = "LOG_" + DateTime.Now.ToString("yyyyMMdd")+".csv";
                var Msg = TimeStr + "," + data.A0 + "," + data.A1 + "," + data.A2 + "," + data.A3 + "," + data.A4 + "," + data.A5 + ", " + data.A6 + "," + data.A7 + "," + data.A8;
                storage.WriteData(FileNameLog,"LOGS",Msg);
               
                //insert to db
                var item = new DataGridItem(new object[] { TimeStr, data.A0, data.A1, data.A2, data.A3, data.A4, data.A5, data.A6, data.A7, data.A8 });
                //add data to grid
                GvData.AddItem(item);
                Counter++;

                GvData.Invalidate();


                //add rows to table
                myDatabase.ExecuteNonQuery("INSERT INTO Sensor (Time,A0,A1,A2,A3,A4,A5,A6,A7,A8)" +
                " VALUES ('" + TimeStr + "' , " + data.A0 + "," + data.A1 + ", "  + data.A2 + "," + data.A3 + ", " + data.A4 + ","  + data.A5 + ", " + data.A6 + ","  + data.A7 + "," + data.A8 + ")");
                window.Invalidate();
                if (Counter > 10)
                {
                    //reset
                    Counter = 0;
                    myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                    GvData.Clear();
                    GvData.Invalidate();
                    
                }

                Thread.Sleep(Delay);
            }

        }

        void StartFTP(string IP)
        {
            ftp = new FTPServer(IP);
            ftp.AddLogin("bmc", "123qweasd", "\\SD\\", new FTPServer.UserPermissions(true, true, true, true, true, true));
            ftp.AllowAnonymous = true;
            ftp.AnonymousRoot = "\\SD\\LOGS\\";
            ftp.DebugMode = true;
            ftp.Start();
        }

        void SendToMqtt(string Topic, string Message){
            if(Messaging!=null && Messaging.IsReady){
                Messaging.PublishMessage(Topic,Message);
            }
        }

        void PrintToLcd(string Message)
        {
            //update display
            txtTime.Text = DateTime.Now.ToString("dd/MMM/yyyy HH:mm:ss");
            txtMessage.Text = Message;
            txtTime.Invalidate();
            txtMessage.Invalidate();
            window.Invalidate();
        }

        public bool NTPTime(string TimeServer, int GmtOffset = 0)
        {
            Socket s = null;
            try
            {
                EndPoint rep = new IPEndPoint(Dns.GetHostEntry(TimeServer).AddressList[0], 123);
                s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var ntpData = new byte[48];
                Array.Clear(ntpData, 0, 48);
                ntpData[0] = 0x1B; // Set protocol version
                s.SendTo(ntpData, rep); // Send Request   
                if (s.Poll(30 * 1000 * 1000, SelectMode.SelectRead)) // Waiting an answer for 30s, if nothing: timeout
                {
                    s.ReceiveFrom(ntpData, ref rep); // Receive Time
                    byte offsetTransmitTime = 40;
                    ulong intpart = 0;
                    ulong fractpart = 0;
                    for (var i = 0; i <= 3; i++) intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];
                    for (var i = 4; i <= 7; i++) fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];
                    var milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);
                    s.Close();
                    var dateTime = new DateTime(1900, 1, 1) +
                                   TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);

                    Utility.SetLocalTime(dateTime.AddMinutes(GmtOffset));

                    return true;
                }
                s.Close();
            }
            catch
            {
                try
                {
                    s.Close();
                }
                catch
                {
                }
            }
            return false;
        }

    }

    public class SensorData
    {
        public double A0 { get; set; }
        public double A1 { get; set; }
        public double A2 { get; set; }
        public double A3 { get; set; }
        public double A4 { get; set; }
        public double A5 { get; set; }
        public double A6 { get; set; }
        public double A7 { get; set; }
        public double A8 { get; set; }

    }

    //driver for touch screen
    public class CapacitiveTouchController
    {
        private InterruptPort touchInterrupt;
        private I2CDevice i2cBus;
        private I2CDevice.I2CTransaction[] transactions;
        private byte[] addressBuffer;
        private byte[] resultBuffer;

        private static CapacitiveTouchController _this;

        public static void Initialize(Cpu.Pin PortId)
        {
            if (_this == null)
                _this = new CapacitiveTouchController(PortId);
        }

        private CapacitiveTouchController()
        {
        }

        private CapacitiveTouchController(Cpu.Pin portId)
        {
            transactions = new I2CDevice.I2CTransaction[2];
            resultBuffer = new byte[1];
            addressBuffer = new byte[1];
            i2cBus = new I2CDevice(new I2CDevice.Configuration(0x38, 400));
            touchInterrupt = new InterruptPort(portId, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeBoth);
            touchInterrupt.OnInterrupt += (a, b, c) => this.OnTouchEvent();
        }

        private void OnTouchEvent()
        {
            for (var i = 0; i < 5; i++)
            {
                var first = this.ReadRegister((byte)(3 + i * 6));
                var x = ((first & 0x0F) << 8) + this.ReadRegister((byte)(4 + i * 6));
                var y = ((this.ReadRegister((byte)(5 + i * 6)) & 0x0F) << 8) + this.ReadRegister((byte)(6 + i * 6));

                if (x == 4095 && y == 4095)
                    break;

                if (((first & 0xC0) >> 6) == 1)
                    GlideTouch.RaiseTouchUpEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
                else
                    GlideTouch.RaiseTouchDownEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
            }
        }

        private byte ReadRegister(byte address)
        {
            this.addressBuffer[0] = address;

            this.transactions[0] = I2CDevice.CreateWriteTransaction(this.addressBuffer);
            this.transactions[1] = I2CDevice.CreateReadTransaction(this.resultBuffer);

            this.i2cBus.Execute(this.transactions, 1000);

            return this.resultBuffer[0];
        }
    }

    public class DiskStorage
    {
        public enum FileType
        {
            CSV, JSON, XML
        };
        // ...
        // assume SD Card is inserted
        // Create a new storage device
      	// NETMF only allows one SD card
      	// to be supported at the same time.

        bool fs_ready;
        GTM.GHIElectronics.SDCard sd_card;
        public DiskStorage(GTM.GHIElectronics.SDCard sd)
        {
            sd_card = sd;

        }

        public void WriteData(string FileName, string Folder, string DataStr, FileType FType=FileType.CSV)
        {
            if (sd_card.IsCardInserted)
            {   
                // this is a non-blocking call 
                // it fires the RemovableMedia.Insert event after 
                // the mount is finished. 
                //sd_card.Mount();

                // Assume one storage device is available, access it through 
                // NETMF and display the available files and folders:
                
                if (VolumeInfo.GetVolumes()[0].IsFormatted)
                {
                    string rootDirectory =
                        VolumeInfo.GetVolumes()[0].RootDirectory;
                    var newDir = rootDirectory + "\\" + Folder;

                    if (!Directory.Exists(newDir))
                    {
                        Directory.CreateDirectory(newDir);
                    }
                    var FullPath = newDir +"\\"+ FileName;
                    if (File.Exists(FullPath))
                    {
                        FileStream FileHandle = new FileStream(FullPath, FileMode.Append);
                        byte[] data = Encoding.UTF8.GetBytes(DataStr);
                        FileHandle.Write(data, 0, data.Length);
                        FileHandle.Close();
                    }
                    else
                    {
                        FileStream FileHandle = new FileStream(FullPath, FileMode.Create);
                        byte[] data = Encoding.UTF8.GetBytes(DataStr);
                        FileHandle.Write(data, 0, data.Length);
                        FileHandle.Close();
                    }
                    Debug.Print("Data is written to disk.");
                }
                else
                {
                    Debug.Print("Storage is not formatted. " +
                        "Format on PC with FAT32/FAT16 first!");
                }
                // Unmount when done
                //sd_card.Unmount();
            }

           
        }
    }

    public class EthernetNetwork
    {
        public delegate void NetworkConnectedHandler(string IPAddress);
        public event NetworkConnectedHandler NetworkConnected;

        public string IpAddress { get; set; }
        bool IsConnected = false;
        NetworkInterface netif;
        public EthernetNetwork(EthernetENC28 network)
        {
            
            network.UseDHCP();
            netif = network.NetworkInterface.NetworkInterface;
            netif.EnableDhcp();
            netif.EnableDynamicDns();
            Thread th = new Thread(new ThreadStart(Connect));
            th.Start();
        }

        void Connect()
        {
            while (netif.IPAddress == "0.0.0.0")
            {
                Debug.Print("Waiting for DHCP");
                Thread.Sleep(250);
            }
            IpAddress = netif.IPAddress;
            IsConnected = true;
            if (NetworkConnected != null)
            {
                NetworkConnected.Invoke(IpAddress);
            }
        }
    }

    public class MqttAgent
    {
        public bool IsReady { get; set; }
        public delegate void DataReceivedHandler(string Topic,string Message);
        public event DataReceivedHandler DataReceived;
        MqttClient MqttClient;

        public MqttAgent(string IPBrokerAddress, string ClientUser, string ClientPass, string clientId, string DataTopic)
        {
            IsReady=false;
            SetupMqtt(IPBrokerAddress, ClientUser, ClientPass, clientId, DataTopic);
        }

        public void PublishMessage(string Topic, string Message)
        {
            MqttClient.Publish(Topic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }

        void SetupMqtt(string IPBrokerAddress, string ClientUser,string ClientPass,string clientId, string DataTopic)
        {
            try
            {
                //string IPBrokerAddress = "110.35.82.86"; //ConfigurationManager.AppSettings["MqttHost"];
                //string ClientUser = "loradev_mqtt"; //ConfigurationManager.AppSettings["MqttUser"];
                //string ClientPass = "test123";//ConfigurationManager.AppSettings["MqttPass"];

                MqttClient = new MqttClient(IPAddress.Parse( IPBrokerAddress));

                // register a callback-function (we have to implement, see below) which is called by the library when a message was received
                MqttClient.Subscribe(new string[] { DataTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                MqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

                // use a unique id as client id, each time we start the application
                //var clientId = "bmc-gateway-2";//Guid.NewGuid().ToString();

                MqttClient.Connect(clientId, ClientUser, ClientPass);
                Debug.Print("MQTT is connected");
                IsReady = true;
            }
            catch(Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        } // this code runs when a message was received
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = new string(Encoding.UTF8.GetChars(e.Message));
            if (DataReceived != null)
            {
                DataReceived.Invoke(e.Topic, ReceivedMessage);
               
            }
            Debug.Print("Data is received");
        }
    }
}
