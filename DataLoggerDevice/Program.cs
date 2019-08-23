using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
//using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.Glide.UI;
using GHI.Glide;
using GHI.Glide.Display;
using System.Text;
using System.IO.Ports;
using Microsoft.SPOT.Hardware;
using GHI.Processor;
using GHI.SQLite;
using GHI.Glide.Geom;
namespace DataLoggerDevice
{
    public partial class Program
    {
        static bool RelayState = false;
        static int Counter = 0;
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
            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.Form1));

            txtTime = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtTime");
            GvData = (GHI.Glide.UI.DataGrid)window.GetChildByName("GvData");
            BtnReset = (GHI.Glide.UI.Button)window.GetChildByName("BtnReset");
            txtMessage = (GHI.Glide.UI.TextBlock)window.GetChildByName("TxtMessage");
            Glide.MainWindow = window;

            //setup grid
            //create grid column
            GvData.AddColumn(new DataGridColumn("Time", 200));
            GvData.AddColumn(new DataGridColumn("Relay", 200));
            GvData.AddColumn(new DataGridColumn("Light", 200));
            GvData.AddColumn(new DataGridColumn("Moisture", 200));

            // Create a database in memory,
            // file system is possible however!
            myDatabase = new GHI.SQLite.Database();
            myDatabase.ExecuteNonQuery("CREATE Table Sensor" +
            " (Time TEXT, Relay DOUBLE,Light DOUBLE,Moisture DOUBLE)");
            //reset database n display
            BtnReset.TapEvent += (object sender) =>
            {
                Counter = 0;
                myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                GvData.Clear();
                GvData.Invalidate();
            };

            Glide.MainWindow = window;
            relayX1.TurnOff();
            UART = new SimpleSerial(GHI.Pins.FEZRaptor.Socket4.SerialPortName, 57600);
            UART.ReadTimeout = 0;
            UART.DataReceived += UART_DataReceived;
            Debug.Print("57600");
            Debug.Print("RN2483 Test");
            PrintToLcd("RN2483 Test");
            OutputPort reset = new OutputPort(GHI.Pins.FEZRaptor.Socket4.Pin6, false);
            OutputPort reset2 = new OutputPort(GHI.Pins.FEZRaptor.Socket4.Pin3, false);

            reset.Write(true);
            reset2.Write(true);

            Thread.Sleep(100);
            reset.Write(false);
            reset2.Write(false);

            Thread.Sleep(100);
            reset.Write(true);
            reset2.Write(true);

            Thread.Sleep(100);

            waitForResponse();

            sendCmd("sys factoryRESET");
            sendCmd("sys get hweui");
            sendCmd("mac get deveui");
            Thread.Sleep(3000);
            // For TTN
            sendCmd("mac set devaddr AAABBBEE");  // Set own address
            Thread.Sleep(3000);
            sendCmd("mac set appskey 2B7E151628AED2A6ABF7158809CF4F3D");
            Thread.Sleep(3000);

            sendCmd("mac set nwkskey 2B7E151628AED2A6ABF7158809CF4F3D");
            Thread.Sleep(3000);

            sendCmd("mac set adr off");
            Thread.Sleep(3000);

            sendCmd("mac set rx2 3 868400000");//869525000
            Thread.Sleep(3000);

            sendCmd("mac join abp");
            Thread.Sleep(3000);
            sendCmd("mac get status");
            sendCmd("mac get devaddr");
            Thread.Sleep(2000);

            Thread th1 = new Thread(new ThreadStart(Loop));
            th1.Start();

        }
        private static string[] _dataInLora;
        private static string rx;


        void UART_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            _dataInLora = UART.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                rx = _dataInLora[index];
                //if error
                if (_dataInLora[index].Length > 5)
                {

                    //if receive data
                    if (rx.Substring(0, 6) == "mac_rx")
                    {
                        string hex = _dataInLora[index].Substring(9);

                        //update display
                        
                        byte[] data = StringToByteArrayFastest(hex);
                        string decoded = new String(UTF8Encoding.UTF8.GetChars(data));
                        Debug.Print("decoded:" + decoded);
                        txtMessage.Text = decoded;//Unpack(hex);
                        /*
                        var state =new string( new char[] { decoded[decoded.Length - 1] });
                        var relaystate = int.Parse(state);
                        RelayState = relaystate == 1 ? true : false;
                        if (RelayState) 
                            relayX1.TurnOn();
                        else
                            relayX1.TurnOff();
                        */
                        txtMessage.Invalidate();
                        window.Invalidate();
                    }
                }
            }
            Debug.Print(rx);
        }

        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
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
                    Relay = RelayState ? 1 : 0,
                    Light = lightSense.ReadProportion(),
                    Moisture = moisture.ReadMoisture()
                };
                var jsonStr = Json.NETMF.JsonSerializer.SerializeObject(data);
                Debug.Print("kirim :" + jsonStr);
                PrintToLcd("send count: " + counter);
                sendData(jsonStr);
                Thread.Sleep(5000);
                byte[] rx_data = new byte[20];

                if (UART.CanRead)
                {
                    var count = UART.Read(rx_data, 0, rx_data.Length);
                    if (count > 0)
                    {
                        Debug.Print("count:" + count);
                        var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                        Debug.Print("read:" + hasil);

                        //mac_rx 2 AABBCC
                    }
                }
                var TimeStr = DateTime.Now.ToString("dd/MM/yy HH:mm");
                //insert to db
                var item = new DataGridItem(new object[] { TimeStr, RelayState ? 1 : 0, data.Light, data.Moisture });
                //add data to grid
                GvData.AddItem(item);
                Counter++;

                GvData.Invalidate();


                //add rows to table
                myDatabase.ExecuteNonQuery("INSERT INTO Sensor (Time, Relay, Light, Moisture)" +
                " VALUES ('" + TimeStr + "' , " + data.Relay + "," + data.Light + "," + data.Moisture + ")");
                window.Invalidate();
                if (Counter > 10)
                {
                    //reset
                    Counter = 0;
                    myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                    GvData.Clear();
                    GvData.Invalidate();
                }

                Thread.Sleep(2000);
            }

        }
        SimpleSerial UART = null;

        void PrintToLcd(string Message)
        {
            //update display
            txtTime.Text = DateTime.Now.ToString("dd/MMM/yyyy HH:mm:ss");
            txtMessage.Text = "Data Transmitted Successfully.";
            txtTime.Invalidate();
            txtMessage.Invalidate();
            window.Invalidate();
        }



        void sendCmd(string cmd)
        {
            byte[] rx_data = new byte[20];
            Debug.Print(cmd);
            Debug.Print("\n");
            // flush all data
            UART.Flush();
            // send some data
            var tx_data = Encoding.UTF8.GetBytes(cmd);
            UART.Write(tx_data, 0, tx_data.Length);
            tx_data = Encoding.UTF8.GetBytes("\r\n");
            UART.Write(tx_data, 0, tx_data.Length);
            Thread.Sleep(100);
            while (!UART.IsOpen)
            {
                UART.Open();
                Thread.Sleep(100);
            }
            if (UART.CanRead)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count cmd:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read cmd:" + hasil);
                }
            }
        }

        void waitForResponse()
        {
            byte[] rx_data = new byte[20];

            while (!UART.IsOpen)
            {
                UART.Open();
                Thread.Sleep(100);
            }
            if (UART.CanRead)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count res:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read res:" + hasil);
                }

            }
        }
        public static string Unpack(string input)
        {
            byte[] b = new byte[input.Length / 2];

            for (int i = 0; i < input.Length; i += 2)
            {
                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));
            }
            return new string(Encoding.UTF8.GetChars(b));
        }
        public static int FromHex(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (int)(digit - '0');
            }

            if ('a' <= digit && digit <= 'f')
                return (int)(digit - 'a' + 10);

            if ('A' <= digit && digit <= 'F')
                return (int)(digit - 'A' + 10);

            throw new ArgumentException("digit");
        }

        char getHexHi(char ch)
        {
            int nibbleInt = ch >> 4;
            char nibble = (char)nibbleInt;
            int res = (nibble > 9) ? nibble + 'A' - 10 : nibble + '0';
            return (char)res;
        }
        char getHexLo(char ch)
        {
            int nibbleInt = ch & 0x0f;
            char nibble = (char)nibbleInt;
            int res = (nibble > 9) ? nibble + 'A' - 10 : nibble + '0';
            return (char)res;
        }

        void sendData(string msg)
        {
            byte[] rx_data = new byte[20];
            char[] data = msg.ToCharArray();
            Debug.Print("mac tx uncnf 1 ");
            var tx_data = Encoding.UTF8.GetBytes("mac tx uncnf 1 ");
            UART.Write(tx_data, 0, tx_data.Length);

            // Write data as hex characters
            foreach (char ptr in data)
            {
                tx_data = Encoding.UTF8.GetBytes(new string(new char[] { getHexHi(ptr) }));
                UART.Write(tx_data, 0, tx_data.Length);
                tx_data = Encoding.UTF8.GetBytes(new string(new char[] { getHexLo(ptr) }));
                UART.Write(tx_data, 0, tx_data.Length);


                Debug.Print(new string(new char[] { getHexHi(ptr) }));
                Debug.Print(new string(new char[] { getHexLo(ptr) }));
            }
            tx_data = Encoding.UTF8.GetBytes("\r\n");
            UART.Write(tx_data, 0, tx_data.Length);
            Debug.Print("\n");
            Thread.Sleep(5000);

            if (UART.CanRead)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count after:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read after:" + hasil);
                }
            }
        }
    }

    public class SensorData
    {
        public int Relay { get; set; }
        public double Light { get; set; }
        public double Moisture { get; set; }


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
}