using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.IO;
using Microsoft.SPOT.Net.NetworkInformation;


 

namespace DataLoggerDevice2
{
    public class FTPServer
    {

        #region Structures

        public struct UserPermissions
        {
            public bool CanMakeDir;
            public bool CanRemoveDir;
            public bool CanUpload;
            public bool CanDownload;
            public bool CanDelete;
            public bool CanRename;
            public UserPermissions(bool CanMakeDir, bool CanRemoveDir, bool CanUpload, bool CanDownload, bool CanDelete, bool CanRename)
            {
                this.CanMakeDir = CanMakeDir;
                this.CanRemoveDir = CanRemoveDir;
                this.CanUpload = CanUpload;
                this.CanDownload = CanDownload;
                this.CanDelete = CanDelete;
                this.CanRename = CanRename;
            }
        }

        public struct UserData
        {
            public string Username;
            public string Password;
            public string StartDirectory;
            public UserPermissions Permissions;
            public UserData(string Username, string Password, string StartDirectory, UserPermissions Permissions)
            {
                this.Username = Username;
                this.Password = Password;
                this.StartDirectory = StartDirectory;
                this.Permissions = Permissions;
            }
        }

        #endregion Structures

        #region Variables

        private ArrayList _users = new ArrayList();     // Collection of allowed users
        private bool _anon = false;                     // Allow anonymous users when true
        private string _title;                          // Server Title

        private string _IP;
        private int _Port = 21;
        private bool _cont = false;
        private string _root = string.Empty;            // Root directory for user
        private string _dir = string.Empty;             // Current directory for user
        private UserPermissions _permissions;           // Current user permissions
        private UserPermissions _anonPerms;             // Permissions for anonymous users
        private string _anonRoot;                       // Root Directory for anonymous users

        private bool _debug = false;                    // Echos debug info when true

        #endregion

        #region Constructor / Deconstructor

        public FTPServer(string IP, int Port = 21, string ServerName = "AQ13")
        {
            _IP = IP;
            _Port = Port;
            _title = ServerName;

            _anonPerms = new UserPermissions(false, false, false, true, false, false);
        }

        #endregion

        #region Properties

        public bool AllowAnonymous
        {
            get { return _anon; }
            set { _anon = value; }
        }

        public UserPermissions AnonymousPermissions
        {
            get { return _anonPerms; }
            set { _anonPerms = value; }
        }

        public string AnonymousRoot
        {
            get { return _anonRoot; }
            set { _anonRoot = value; }
        }

        public bool DebugMode
        {
            get { return _debug; }
            set { _debug = value; }
        }

        #endregion

        #region Public Methods

        public void AddLogin(UserData User)
        {
            User.Username = User.Username.ToLower();
            _users.Add(User);
        }

        public void AddLogin(string Username, string Password, string StartDirectory, UserPermissions Permissions)
        {
            _users.Add(new UserData(Username.ToLower(), Password, StartDirectory, Permissions));
        }

        public void Start()
        {
            if (_users.Count == 0 && !_anon)
                throw new Exception("Cannot start server with no users and anonymous connections disallowed.");

            _cont = true;

            // Create the socket            
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket clientSocket = null;
            Socket activeSocket = null;

            byte[] b;
            string strLine;
            string[] strParams;
            string strUser = string.Empty;
            string sTempPath = string.Empty;

            // Bind the listening socket to the port
            IPAddress hostIP = IPAddress.Parse(_IP);
            IPEndPoint ep = new IPEndPoint(hostIP, _Port);
            listenSocket.Bind(ep);

            // Start listening
            listenSocket.Listen(1);

            // Main thread loop
            while (_cont)
            {
                while (_cont)
                {
                    try
                    {
                        DebugLine("Listening for connection");
                        clientSocket = listenSocket.Accept();
                        DebugLine("Accepted a connection from " + clientSocket.RemoteEndPoint.ToString());
                        byte[] messageBytes = Encoding.UTF8.GetBytes("220 Welcome to " + _title + "\n");
                        clientSocket.Send(messageBytes);
                        break;
                    }
                    catch (Exception e)
                    {
                        DebugLine("ERROR: " + e.Message);
                    }
                }

                try
                {
                    bool KeepAlive = true;
                    while (KeepAlive && _cont)
                    {
                        if (clientSocket.Available > 0)
                        {
                            b = new byte[clientSocket.Available];
                            clientSocket.Receive(b);

                            try
                            {

                                strLine = new string(UTF8Encoding.UTF8.GetChars(b));
                                if (strLine.Substring(strLine.Length - 1) == "\n")
                                    strLine = strLine.Substring(0, strLine.Length - 1);
                                if (strLine.Substring(strLine.Length - 1) == "\r")
                                    strLine = strLine.Substring(0, strLine.Length - 1);
                                DebugLine("<< " + strLine);
                                strParams = strLine.Split(' ');
                                switch (strParams[0])
                                {
                                    case "USER":
                                        // Get username
                                        strUser = FTP_USER(clientSocket, strParams[1]);
                                        break;
                                    case "PASS":
                                        FTP_PASS(clientSocket, strUser, strParams[1]);
                                        break;
                                    case "PORT":
                                        activeSocket = FTP_PORT(clientSocket, strParams[1]);
                                        break;
                                    case "NLST":
                                        FTP_NLST(clientSocket, activeSocket);
                                        break;
                                    case "CDUP":
                                        FTP_CWD(clientSocket, "..");
                                        break;
                                    case "CWD":
                                        FTP_CWD(clientSocket, strLine.Substring(strParams[0].Length + 1));
                                        break;
                                    case "RETR":
                                        FTP_RETR(clientSocket, activeSocket, strParams[1]);
                                        break;
                                    case "STOR":
                                        FTP_STOR(clientSocket, activeSocket, strParams[1]);
                                        break;
                                    case "DELE":
                                        FTP_DELE(clientSocket, strParams[1]);
                                        break;
                                    case "RNFR":
                                        sTempPath = FTP_RNFR(clientSocket, strParams[1]);
                                        break;
                                    case "RNTO":
                                        FTP_RNTO(clientSocket, strParams[1], sTempPath);
                                        sTempPath = string.Empty;
                                        break;
                                    case "PWD":
                                    case "XPWD":
                                        FTP_PWD(clientSocket);
                                        break;
                                    case "XMKD":
                                    case "MKD":
                                        FTP_MKD(clientSocket, strParams[1]);
                                        break;
                                    case "XRMD":
                                    case "RMD":
                                        FTP_RMD(clientSocket, strParams[1]);
                                        break;
                                    case "SIZE":
                                        FTP_SIZE(clientSocket, strParams[1]);
                                        break;
                                    case "NOOP":
                                        FTP_NOOP(clientSocket);
                                        break;
                                    case "QUIT":
                                        FTP_QUIT(clientSocket);
                                        KeepAlive = false;
                                        break;
                                    default:
                                        clientSocket.Send(Encoding.UTF8.GetBytes("500 Command not implemented.\n"));
                                        break;
                                }
                            }
                            catch (Exception e2)
                            {
                                DebugLine("ERROR: " + e2.Message);
                                clientSocket.Send(Encoding.UTF8.GetBytes("490 Internal server error.\n"));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Socket probably closed; move along
                    clientSocket = null;
                    activeSocket = null;
                    strUser = string.Empty;
                    sTempPath = string.Empty;
                }
            }
        }

        public void Stop()
        {
            _cont = false;
        }

        #endregion

        #region Private Methods

        private bool AuthUser(string Username, string Password)
        {
            // Usernames are not case sensitive
            Username = Username.ToLower();

            // Check Anonymous first
            if (Username == "anonymous" && _anon)
            {
                _permissions = _anonPerms;
                _root = NormalizeDirectory(_anonRoot);
                _dir = _root;
                DebugLine("Anonymous user logged in");
                DebugLine("Directory is now " + _dir);
                return true;
            }

            // Find user
            UserData ud;
            for (int i = 0; i < _users.Count; i++)
            {
                ud = (UserData)_users[i];
                if (ud.Username == Username)
                {
                    if (ud.Password == Password)
                    {
                        _permissions = ud.Permissions;
                        _root = NormalizeDirectory(ud.StartDirectory);
                        _dir = _root;
                        DebugLine("User '" + Username + " logged in");
                        DebugLine("Directory is now " + _dir);
                        return true;
                    }
                    else
                    {
                        DebugLine("User '" + Username + " login failed; incorrect password");
                        return false;
                    }
                }
            }

            DebugLine("Requested user not found");

            return false;
        }

        private void DebugLine(string msg)
        {
            if (_debug)
                Debug.Print("[" + DateTime.Now.ToString() + "] " + msg);
        }

        private void FinalizeVolumes()
        {
            VolumeInfo[] vi = VolumeInfo.GetVolumes();
            for (int i = 0; i < vi.Length; i++)
                vi[i].FlushAll();
        }

        private string FixPath(string path)
        {
            return path.Substring(_dir.Length);
        }

        private string NormalizeFile(string path)
        {
            int i;
            string dir = _dir.Substring(0, _dir.Length - 1);

            try
            {
                while (true)
                {
                    i = path.IndexOf("..");
                    if (i < 0)
                        break;

                    dir = dir.Substring(0, dir.LastIndexOf("\\"));
                    path = path.Substring(i + 2);

                    if (path == string.Empty)
                        break;

                    if (path.Substring(0, 1) == "\\")
                        path = path.Substring(1);
                }

                return NormalizeDirectory(dir) + path;

            }
            catch (Exception)
            {
                return "";
            }
        }

        private string NormalizeDirectory(string path)
        {
            if (path.Substring(path.Length - 1) == "\\")
                return path;
            return path + "\\";
        }

        private string RemoveRoot(string path)
        {
            return path.Substring(_root.Length);
        }

        private bool ValidateDirectory(string newPath)
        {
            int i;
            string dir = _dir.Substring(0, _dir.Length - 1);

            if (newPath == string.Empty)
                return false;

            try
            {
                if (newPath.Substring(0, 1) != "\\")
                {
                    while (true)
                    {
                        i = newPath.IndexOf("..");
                        if (i < 0)
                            break;

                        dir = dir.Substring(0, dir.LastIndexOf("\\"));
                        newPath = newPath.Substring(i + 2);

                        if (newPath == string.Empty)
                            break;

                        if (newPath.Substring(0, 1) == "\\")
                            newPath = newPath.Substring(1);
                    }

                    dir = NormalizeDirectory(dir);
                }
                else
                {
                    dir = NormalizeDirectory(newPath);
                    newPath = string.Empty;
                }

                if (newPath != string.Empty)
                    dir += NormalizeDirectory(newPath);

                if (dir.Length < _root.Length)
                    return false;

                if (dir.Substring(0, _root.Length) != _root)
                    return false;

                if (!Directory.Exists(dir))
                    return false;

                _dir = dir;
                DebugLine("Directory is now " + _dir);
                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Command Responses

        private void FTP_CWD(Socket clientSocket, string Param)
        {
            if (ValidateDirectory(Param))
                clientSocket.Send(Encoding.UTF8.GetBytes("250 CWD command successful. \\" + RemoveRoot(_dir) + " is current directory.\n"));
            else
                clientSocket.Send(Encoding.UTF8.GetBytes("501 CWD failed.\n"));
        }

        private void FTP_DELE(Socket clientSocket, string Param)
        {
            if (!_permissions.CanDelete)
                clientSocket.Send(Encoding.UTF8.GetBytes("500 DELE not allowed for this user\n"));
            else
            {
                string sFile = NormalizeFile(Param);
                DebugLine("?? " + sFile);
                if (!File.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 " + FixPath(sFile) + " does not exist!\n"));
                else
                {
                    try
                    {
                        File.Delete(sFile);
                        FinalizeVolumes();
                        clientSocket.Send(Encoding.UTF8.GetBytes("250 file deleted\n"));
                    }
                    catch (Exception)
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("550 could not delete " + FixPath(sFile) + "\n"));
                    }
                }
            }
        }

        private void FTP_MKD(Socket clientSocket, string Param)
        {
            if (!_permissions.CanMakeDir)
                clientSocket.Send(Encoding.UTF8.GetBytes("500 MKD not allowed for this user\n"));
            else
            {
                string sFile = _dir + NormalizeDirectory(Param);
                if (Directory.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 " + FixPath(sFile) + " already exists!\n"));
                else
                {
                    try
                    {
                        Directory.CreateDirectory(sFile);
                        FinalizeVolumes();
                        clientSocket.Send(Encoding.UTF8.GetBytes("257 directory created\n"));
                    }
                    catch (Exception)
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("550 could not create " + FixPath(sFile) + "\n"));
                    }
                }
            }
        }

        private void FTP_NLST(Socket clientSocket, Socket activeSocket)
        {
            clientSocket.Send(Encoding.UTF8.GetBytes("150 Connecting to port.\n"));

            string sRes = string.Empty;

            string[] s = Directory.GetDirectories(_dir);
            for (int i = 0; i < s.Length; i++)
                sRes += "." + FixPath(s[i]) + "\n";

            s = Directory.GetFiles(_dir);
            for (int i = 0; i < s.Length; i++)
                sRes += FixPath(s[i]) + "\n";

            activeSocket.Send(Encoding.UTF8.GetBytes(sRes));
            activeSocket.Close();

            clientSocket.Send(Encoding.UTF8.GetBytes("226 Transfer complete.\n"));
        }

        private void FTP_NOOP(Socket clientSocket)
        {
            clientSocket.Send(Encoding.UTF8.GetBytes("200 OK\n"));
        }

        private void FTP_PASS(Socket clientSocket, string User, string Param)
        {
            if (AuthUser(User, Param))
                clientSocket.Send(Encoding.UTF8.GetBytes("230 User logged in, proceed.\n"));
            else
                clientSocket.Send(Encoding.UTF8.GetBytes("530 Login authentication failed.\n"));
        }

        private Socket FTP_PORT(Socket clientSocket, string Param)
        {
            try
            {
                string[] addr = Param.Split(',');
                string IP = addr[0] + "." + addr[1] + "." + addr[2] + "." + addr[3];
                int Port = int.Parse(addr[4]) * 256 + int.Parse(addr[5]);
                DebugLine("## Connect to " + IP + " on port " + Port);

                Socket activeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ahostIP = IPAddress.Parse(IP);
                IPEndPoint aep = new IPEndPoint(ahostIP, Port);
                activeSocket.Connect(aep);

                clientSocket.Send(Encoding.UTF8.GetBytes("200 Port Command Successfull.\n"));
                return activeSocket;
            }
            catch (Exception)
            {
                clientSocket.Send(Encoding.UTF8.GetBytes("450 Port Command Failed!\n"));
                return null;
            }
        }

        private void FTP_PWD(Socket clientSocket)
        {
            clientSocket.Send(Encoding.UTF8.GetBytes("257 the current directory is \\" + RemoveRoot(_dir) + "\n"));
        }

        private void FTP_QUIT(Socket clientSocket)
        {
            clientSocket.Send(Encoding.UTF8.GetBytes("221 Goodbye from " + _title + ".\n"));
            clientSocket.Close();
        }

        private void FTP_RETR(Socket clientSocket, Socket activeSocket, string Param)
        {
            byte[] b = new byte[2048];
            FileStream iFile;
            long lRemain;

            if (!_permissions.CanDownload)
                clientSocket.Send(Encoding.UTF8.GetBytes("500 RETR not allowed for this user\n"));
            else
            {
                string sFile = NormalizeFile(Param);
                DebugLine("?? " + sFile);
                if (!File.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("500 " + RemoveRoot(sFile) + " does not exist\n"));
                else
                {
                    try
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("150 opening data connection for " + FixPath(sFile) + "\n"));

                        // Send File in Chunks
                        iFile = new FileStream(sFile, FileMode.Open, FileAccess.Read);
                        lRemain = iFile.Length;

                        while (lRemain > 0)
                        {
                            if (lRemain < 2048)
                                b = new byte[lRemain];

                            DebugLine("## Sending " + b.Length + " bytes; " + lRemain + " remain...");
                            iFile.Read(b, 0, b.Length);
                            activeSocket.Send(b);
                            lRemain -= b.Length;
                        }

                        clientSocket.Send(Encoding.UTF8.GetBytes("226 Transfer complete.\n"));
                    }
                    catch (Exception)
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("456 Transfer failed!\n"));
                    }
                }
            }

            // Close active socket no matter what
            activeSocket.Close();
        }

        private void FTP_RMD(Socket clientSocket, string Param)
        {
            if (!_permissions.CanRemoveDir)
                clientSocket.Send(Encoding.UTF8.GetBytes("500 RMD not allowed for this user\n"));
            else
            {
                string sFile = _dir + NormalizeDirectory(Param);
                if (!Directory.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 " + FixPath(sFile) + " does not exist\n"));
                else
                {
                    try
                    {
                        Directory.Delete(sFile, true);
                        FinalizeVolumes();
                        clientSocket.Send(Encoding.UTF8.GetBytes("250 directory removed\n"));
                    }
                    catch (Exception)
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("550 could not delete " + FixPath(sFile) + "\n"));
                    }
                }
            }
        }

        private string FTP_RNFR(Socket clientSocket, string Param)
        {
            if (!_permissions.CanRename)
            {
                clientSocket.Send(Encoding.UTF8.GetBytes("500 RNFR not allowed for this user\n"));
                return string.Empty;
            }
            else
            {
                string sFile = NormalizeFile(Param);
                DebugLine("?? " + sFile);
                if (!File.Exists(sFile) && !Directory.Exists(sFile))
                {
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 \\" + RemoveRoot(sFile) + " does not exist!\n"));
                    return string.Empty;
                }
                else
                {
                    clientSocket.Send(Encoding.UTF8.GetBytes("350 \\" + RemoveRoot(sFile) + " exists; ready for destination name\n"));
                    return sFile;
                }
            }
        }

        private void FTP_RNTO(Socket clientSocket, string Param, string RenameFrom)
        {
            if (RenameFrom == string.Empty)
                clientSocket.Send(Encoding.UTF8.GetBytes("550 RNTO invalid at this point\n"));
            else
            {
                string sFile = NormalizeFile(Param);


                if (File.Exists(sFile) || Directory.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 Destination already exists\n"));
                else
                {

                    if (File.Exists(RenameFrom))
                    {
                        try
                        {
                            // Rename File
                            File.Move(RenameFrom, sFile);
                            clientSocket.Send(Encoding.UTF8.GetBytes("250 renamed " + FixPath(RenameFrom) + " to " + FixPath(sFile) + "\n"));
                        }
                        catch (Exception)
                        {
                            clientSocket.Send(Encoding.UTF8.GetBytes("550 Could not rename\n"));
                        }
                    }
                    else
                    {
                        try
                        {
                            // Rename Directory
                            Directory.Move(RenameFrom, sFile);
                            clientSocket.Send(Encoding.UTF8.GetBytes("250 renamed \\" + RemoveRoot(RenameFrom) + " to \\" + RemoveRoot(sFile) + "\n"));
                        }
                        catch (Exception)
                        {
                            clientSocket.Send(Encoding.UTF8.GetBytes("550 Could not rename\n"));
                        }
                    }
                }
            }
        }

        private void FTP_SIZE(Socket clientSocket, string Param)
        {
            string sTmp = NormalizeDirectory(Param);
            if (!File.Exists(sTmp))
                clientSocket.Send(Encoding.UTF8.GetBytes("550 " + FixPath(sTmp) + " does not exist\n"));
            else
                clientSocket.Send(Encoding.UTF8.GetBytes("213  " + new FileInfo(sTmp).Length + "\n"));
        }

        private void FTP_STOR(Socket clientSocket, Socket activeSocket, string Param)
        {
            byte[] b;
            FileStream iFile = null;

            if (!_permissions.CanUpload)
                clientSocket.Send(Encoding.UTF8.GetBytes("500 STOR not allowed for this user\n"));
            else
            {
                string sFile = NormalizeFile(Param);
                DebugLine("?? " + sFile);
                if (File.Exists(sFile))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 \\" + RemoveRoot(sFile) + " already exists!\n"));
                else if (!Directory.Exists(Path.GetDirectoryName(sFile)))
                    clientSocket.Send(Encoding.UTF8.GetBytes("550 invalid path\n"));
                else
                {
                    try
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes("150 opening data connection for " + FixPath(sFile) + "\n"));

                        iFile = new FileStream(sFile, FileMode.CreateNew, FileAccess.Write);
                        Thread.Sleep(200);
                        while (activeSocket.Available > 0)
                        {
                            b = new byte[(activeSocket.Available > 2048) ? 2048 : activeSocket.Available];
                            DebugLine("Receiving " + b.Length + " bytes...");
                            activeSocket.Receive(b);
                            iFile.Write(b, 0, b.Length);
                            Thread.Sleep(100);
                        }
                        iFile.Close();
                        FinalizeVolumes();

                        clientSocket.Send(Encoding.UTF8.GetBytes("226 Transfer complete.\n"));
                    }
                    catch (Exception)
                    {
                        if (iFile != null)
                            iFile.Close();
                        clientSocket.Send(Encoding.UTF8.GetBytes("456 Transfer failed!\n"));
                    }
                }
            }

            // Close active socket no matter what
            activeSocket.Close();
        }

        private string FTP_USER(Socket clientSocket, string Param)
        {
            if (Param.ToLower() == "anonymous" && _anon)
                clientSocket.Send(Encoding.UTF8.GetBytes("331 Anonymous access allowed, send identity (email name) as password.\n"));
            else
                clientSocket.Send(Encoding.UTF8.GetBytes("331 User name okay, need password.\n"));

            return Param;
        }

        #endregion

    }
}
