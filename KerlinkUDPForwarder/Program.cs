using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KerlinkUDPForwarder
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Start receiving UDP from Kerlink gateway..");
            Task loop = new Task(new Action(Loop));
            loop.Start();
            Console.ReadLine();
        }

        static void Loop()
        {
            UdpClient udpServer = new UdpClient(8888);

            while (true)
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 8888);
                var data = udpServer.Receive(ref remoteEP); // listen on port 8888

                var datastr = System.Text.Encoding.Default.GetString(data);
                Console.WriteLine("receive data from " + remoteEP.ToString());
                Console.WriteLine("data: " + datastr);
                var obj = JsonConvert.DeserializeObject<RootObject>(datastr);
                if (obj != null)
                {
                    byte[] databyte = Convert.FromBase64String(obj.rx.userdata.payload);
                    string decodedString = Encoding.UTF8.GetString(databyte);
                    var originalValue = Unpack(decodedString);
                    Console.WriteLine("unpack :" + originalValue);
                    var sensorValue = JsonConvert.DeserializeObject<SensorData>(originalValue);
                    sensorValue.Tanggal = DateTime.Now;
                    //call power bi api
                    SendToPowerBI(sensorValue);
                    //send data to gateway
                    {
                        Transmitter.ObjMoteTx objtx = new Transmitter.ObjMoteTx();
                        objtx.tx = new Transmitter.Tx();
                        objtx.tx.moteeui = "00000000AAABBBEE";
                        objtx.tx.txmsgid = "000000000001";
                        objtx.tx.trycount = 5;
                        objtx.tx.txsynch = false;
                        objtx.tx.ackreq = false;
                        //string to hex str, hex str to base64 string
                        objtx.tx.userdata = new Transmitter.Userdata() { payload = "Njg2NTZjNmM2ZjIwNjM2ZjZkNzA3NTc0NjU3Mg==", port = 5 };
                        var jsonStr = JsonConvert.SerializeObject(objtx);
                        byte[] bytes = Encoding.ASCII.GetBytes(jsonStr);
                        udpServer.Send(bytes, bytes.Length, remoteEP);

                    }
                    Thread.Sleep(5000);
                }



            }


        }
        private static HttpClient _client;

        public static HttpClient client
        {
            get
            {
                if (_client == null) _client = new HttpClient();
                return _client;
            }

        }


        static async void SendToPowerBI(SensorData data)
        {
            var url = "https://api.powerbi.com/beta/e4a5cd36-e58f-4f98-8a1a-7a8e545fc65a/datasets/c3152879-fd74-4ba6-94aa-2b9b7111005f/rows?key=AUL%2FVTsGwsmJGzP28v7ah5EInDrjg7rTXx4b1IarBiTTcuB62zXzkG8QGoCZuJwyICzydqAT6ieTzGxsMXMETQ%3D%3D";
            var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            var res = await client.PostAsync(url, content, CancellationToken.None);
            if (res.IsSuccessStatusCode)
            {
                Console.WriteLine("data sent to power bi - " + DateTime.Now);
            }
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



        public static string Unpack(string input)
        {

            byte[] b = new byte[input.Length / 2];



            for (int i = 0; i < input.Length; i += 2)
            {

                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));

            }

            return new string(Encoding.UTF8.GetChars(b));

        }


    }
    public class Motetx
    {
        public int freq { get; set; }
        public string modu { get; set; }
        public string datr { get; set; }
        public string codr { get; set; }
    }

    public class Userdata
    {
        public int seqno { get; set; }
        public int port { get; set; }
        public string payload { get; set; }
        public Motetx motetx { get; set; }
    }

    public class Gwrx
    {
        public string time { get; set; }
        public int chan { get; set; }
        public int rfch { get; set; }
        public int rssi { get; set; }
        public double lsnr { get; set; }
    }

    public class Rx
    {
        public string moteeui { get; set; }
        public Userdata userdata { get; set; }
        public List<Gwrx> gwrx { get; set; }
    }

    public class RootObject
    {
        public Rx rx { get; set; }
    }

    public class SensorData
    {
        public DateTime Tanggal { get; set; }
        public double Humid { get; set; }
        public double Light { get; set; }
        public double Temp { get; set; }
    }

}

 