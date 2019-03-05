using System;
using Microsoft.SPOT;
using System.IO;
using System.Text;
using System.Collections;

namespace DataLoggerDevice2
{
    public class LogHelper
    {
        Gadgeteer.Modules.GHIElectronics.SDCard usbHost;
        public LogHelper(Gadgeteer.Modules.GHIElectronics.SDCard usbHost1)
        {
            this.usbHost = usbHost1;
        }

        public bool SaveSetting(SettingData data)
        {
            try
            {
                var PathStr = "\\SD\\Setting";
                var LogName = "config.json";

                if (usbHost.IsCardInserted && usbHost.IsCardMounted)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        var JsonStr = Json.NETMF.JsonSerializer.SerializeObject(data);
                        ms.Write(Encoding.UTF8.GetBytes(JsonStr), 0, JsonStr.Length);
                        Debug.Print(usbHost.StorageDevice.RootDirectory);
                        usbHost.StorageDevice.CreateDirectory(PathStr);
                        usbHost.StorageDevice.WriteFile(PathStr + "\\" + LogName, ms.ToArray());
                    
                    }
                    return true;
                }
            }
            catch (Exception ex) { Debug.Print(ex.ToString()); }
            return false;
        }

        public SettingData ReadSetting()
        {
            try{
                var PathStr = "\\SD\\Setting";
                var LogName = "config.json";
                if (usbHost.IsCardMounted && usbHost.IsCardInserted)
                {
                    if (IsFileExist(PathStr, LogName))
                    {
                        var JsonStr = string.Empty;

                        using (FileStream stream = new FileStream(PathStr + "\\" + LogName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (TextReader reader = new StreamReader(stream))
                        {
                           JsonStr = reader.ReadToEnd();
                        }
                        var item = Json.NETMF.JsonSerializer.DeserializeString(JsonStr) as Hashtable;
                        var data = new SettingData()
                        {
                            DeviceID = item["DeviceID"].ToString(),
                            MqttHost = item["MqttHost"].ToString(),
                            MqttPassword = item["MqttPassword"].ToString(),
                            MqttUserName = item["MqttUserName"].ToString(),
                            MqttTopic = item["MqttTopic"].ToString()
                        };
                        return data;
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            var JsonStr = Json.NETMF.JsonSerializer.SerializeObject(SettingData.GetDefaultValue());
                            ms.Write(Encoding.UTF8.GetBytes(JsonStr), 0, JsonStr.Length);
                            Debug.Print(usbHost.StorageDevice.RootDirectory);
                            usbHost.StorageDevice.CreateDirectory(PathStr);
                            usbHost.StorageDevice.WriteFile(PathStr + "\\" + LogName, ms.ToArray());
                            return SettingData.GetDefaultValue();
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.Print(ex.ToString()); }
            return null;
        }

        public void WriteLogs(string Message)
        {
            try
            {
                var MessageStr = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + " => " + Message;
                var PathStr = "\\SD\\Logs";
                var LogName = "log_" + DateTime.Now.ToString("dd_MMM_yyyy") + ".txt";
                if (usbHost.IsCardInserted && usbHost.IsCardMounted)
                {
                    if (IsFileExist(PathStr, LogName))
                    {
                        /*
                        using (MemoryStream ms = new MemoryStream())
                        {
                            var ExistingData =usbHost.MassStorageDevice.ReadFile(PathStr + "\\" + LogName);
                            ms.Write(ExistingData ,0, ExistingData.Length);
                            var newText = "New Line!\r\n";
                            ms.Write(Encoding.UTF8.GetBytes(newText), 0, newText.Length);
                            usbHost.MassStorageDevice.WriteFile(PathStr + "\\" + LogName, ms.ToArray());
                       
                        }*/

                        using (FileStream stream = new FileStream(PathStr + "\\" + LogName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        using (TextWriter writer = new StreamWriter(stream))
                        {
                            writer.WriteLine(MessageStr);
                            writer.Flush();
                            writer.Close();
                        }

                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            ms.Write(Encoding.UTF8.GetBytes(MessageStr + "\r\n"), 0, MessageStr.Length + 2);
                            Debug.Print(usbHost.StorageDevice.RootDirectory);
                            usbHost.StorageDevice.CreateDirectory(PathStr);
                            usbHost.StorageDevice.WriteFile(PathStr + "\\" + LogName, ms.ToArray());
                            /*
                            var files = usbHost.MassStorageDevice.ListFiles(PathStr);
                            foreach (var item in files)
                            {
                                Debug.Print(item);
                            }*/
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.Print(ex.ToString()); }
        }

        bool IsFileExist(string Path, string FileName)
        {
            try
            {
                if (usbHost.IsCardMounted && usbHost.IsCardInserted)
                {
                    var files = usbHost.StorageDevice.ListFiles(Path);
                    foreach (var item in files)
                    {
                        var fname = System.IO.Path.GetFileName(item);

                        if (fname.ToLower() == FileName.ToLower())
                        {
                            return true;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.Print(ex.ToString());
            }
            return false;
        }
    }
}
