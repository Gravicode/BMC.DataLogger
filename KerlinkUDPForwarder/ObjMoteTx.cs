using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KerlinkUDPForwarder.Transmitter
{
public class ObjMoteTx
    {
        public Tx tx { get; set; }
    }

    public class Tx
    {
        public string moteeui { get; set; }
        public string txmsgid { get; set; }
        public int trycount { get; set; }
        public bool txsynch { get; set; }
        public bool ackreq { get; set; }
        public Userdata userdata { get; set; }
    }

    public class Userdata
    {
        public int port { get; set; }
        public string payload { get; set; }
    }

}
