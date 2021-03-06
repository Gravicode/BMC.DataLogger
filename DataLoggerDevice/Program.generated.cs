//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DataLoggerDevice {
    using Gadgeteer;
    using GTM = Gadgeteer.Modules;
    
    
    public partial class Program : Gadgeteer.Program {
        
        /// <summary>The SD Card module using socket 9 of the mainboard.</summary>
        private Gadgeteer.Modules.GHIElectronics.SDCard sdCard;
        
        /// <summary>The Display NHVN module using sockets 15, 16 and 17 of the mainboard.</summary>
        private Gadgeteer.Modules.GHIElectronics.DisplayNHVN displayNHVN;
        
        /// <summary>The Moisture module using socket 18 of the mainboard.</summary>
        private Gadgeteer.Modules.GHIElectronics.Moisture moisture;
        
        /// <summary>The Relay X1 module using socket 12 of the mainboard.</summary>
        private Gadgeteer.Modules.GHIElectronics.RelayX1 relayX1;
        
        /// <summary>The LightSense module using socket 2 of the mainboard.</summary>
        private Gadgeteer.Modules.GHIElectronics.LightSense lightSense;
        
        /// <summary>This property provides access to the Mainboard API. This is normally not necessary for an end user program.</summary>
        protected new static GHIElectronics.Gadgeteer.FEZRaptor Mainboard {
            get {
                return ((GHIElectronics.Gadgeteer.FEZRaptor)(Gadgeteer.Program.Mainboard));
            }
            set {
                Gadgeteer.Program.Mainboard = value;
            }
        }
        
        /// <summary>This method runs automatically when the device is powered, and calls ProgramStarted.</summary>
        public static void Main() {
            // Important to initialize the Mainboard first
            Program.Mainboard = new GHIElectronics.Gadgeteer.FEZRaptor();
            Program p = new Program();
            p.InitializeModules();
            p.ProgramStarted();
            // Starts Dispatcher
            p.Run();
        }
        
        private void InitializeModules() {
            this.sdCard = new GTM.GHIElectronics.SDCard(9);
            this.displayNHVN = new GTM.GHIElectronics.DisplayNHVN(15, 16, 17, Socket.Unused, Socket.Unused);
            this.moisture = new GTM.GHIElectronics.Moisture(18);
            this.relayX1 = new GTM.GHIElectronics.RelayX1(12);
            this.lightSense = new GTM.GHIElectronics.LightSense(2);
        }
    }
}
