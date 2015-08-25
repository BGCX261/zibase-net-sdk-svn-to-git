//#define LOG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;


namespace ZibaseDll
{
    ///''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    // Changelog - Dll de communication Zibase
    //
    ///
    //' v1.0 - 05/2010 - Version initiale
    //' v1.1 - 09/2010 - Ajout de type de données (xse, vs...)
    //' v1.2 - 12/2010 - Possibilité de choisir les paramètres d'association d'une base
    //'                - Correction d'un bug sur les codes unités >10 dans Send Command
    //'                - Gestion des variables, calendriers et état X10
    //'                - Meilleur détection des zibases dès le démarrage
    //'                - Gestion des virtuals probe !!!!!
    //'                - Gestion de la liste des scénarios et des capteurs depuis la plateforme
    // v1.3 
    //                 - GetCalendar(int) returned GetVar(int) => Fixed
    //                 - lev field is now correct (with comma)
    //                 - The sdk caller does not need to implement ISynchronizedInvoke anymore
    //                 - New protocols  PROTOCOL_X2D868_INTER_SHUTTER,PROTOCOL_XDD868_PILOT_WIRE,PROTOCOL_XDD868_BOILER_AC

    public class ZiBase
    {
        public struct SensorInfo
        {
            public string sHSName;
            public string sName;
            public string sType;
            public string sID;
            public long dwValue;
            public string sValue;
            public string sHTMLValue;
            public string sDevice;
            public DateTime sDate;
        }
        public struct ZibaseInfo
        {
            public string sLabelBase;
            public long lIpAddress;
            public string sToken;

            public String GetIPAsString()
            {
                string ip = string.Empty;
                for (int i = 0; i < 4; i++)
                {
                    int num = (int)(lIpAddress / Math.Pow(256, (3 - i)));
                    lIpAddress = lIpAddress - (long)(num * Math.Pow(256, (3 - i)));
                    if (i == 0)
                        ip = num.ToString();
                    else
                        ip = ip + "." + num.ToString();
                }
                return ip;
            }
        }
        #region Delegates

        public delegate void NewSensorDetectedEventHandler(SensorInfo seInfo);

        public delegate void NewZibaseDetectedEventHandler(ZibaseInfo zbInfo);

        public delegate void UpdateSensorInfoEventHandler(SensorInfo seInfo);

        public delegate void WriteMessageEventHandler(string sMsg, int level);

        #endregion

        #region Protocol enum

        public enum Protocol
        {
            PROTOCOL_BROADCAST = 0,
            PROTOCOL_VISONIC433 = 1,
            PROTOCOL_VISONIC868 = 2,
            PROTOCOL_CHACON = 3,
            PROTOCOL_DOMIA = 4,
            PROTOCOL_X10 = 5,
            PROTOCOL_ZWAVE = 6,
            PROTOCOL_RFS10 = 7,
            PROTOCOL_X2D433 = 8,
            PROTOCOL_X2D868 = 9,
            PROTOCOL_X2D868_INTER_SHUTTER = 10,
            PROTOCOL_XDD868_PILOT_WIRE,
            PROTOCOL_XDD868_BOILER_AC
        }

        #endregion

        #region State enum

        public enum State
        {
            STATE_OFF = 0,
            STATE_ON = 1,
            STATE_DIM = 3,
            STATE_ASSOC = 7
        }

        #endregion

        #region VirtualProbeType enum

        public enum VirtualProbeType
        {
            TEMP_SENSOR = 0,
            TEMP_HUM_SENSOR = 1,
            POWER_SENSOR = 2,
            WATER_SENSOR = 3
        }

        #endregion

        #region ZibasePlateform enum

        public enum ZibasePlateform
        {
            ZODIANET = 0,
            RESERVED = 1,
            PLANETE_DOMOTIQUE = 2,
            DOMADOO = 3,
            ROBOPOLIS = 4
        }

        #endregion

        public const int MSG_INFO = 0;
        public const int MSG_DEBUG = 1;
        public const int MSG_DEBUG_NOLOG = 2;
        public const int MSG_WARNING = 3;

        public const int MSG_ERROR = 4;
        private const int CMD_READ_VAR = 0;
        private const int CMD_TYPE_WRITE_VAR = 1;
        private const int CMD_READ_CAL = 2;
        private const int CMD_WRITE_CAL = 3;

        private const int CMD_READ_X10 = 4;
        private const int DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE = 17;

        private const int DOMO_EVENT_ACTION_OWL_SIGNAL_32B_SENSOR_CODE = 20;
        private readonly Dictionary<String, SensorInfo> _SensorList = new Dictionary<String, SensorInfo>();
        private readonly IPEndPoint m_Server = new IPEndPoint(IPAddress.Any, 0);
        private readonly ZBClass m_Zbs = new ZBClass();
        private readonly Dictionary<String, ZibaseInfo> m_ZibaseList = new Dictionary<String, ZibaseInfo>();
        private bool m_AutoSearch = true;
        private bool m_EndThread;
        private Thread m_ThreadSearch;
        private Thread m_ThreadZibase;

        public event WriteMessageEventHandler WriteMessage;

        public event UpdateSensorInfoEventHandler UpdateSensorInfo;

        public event NewZibaseDetectedEventHandler NewZibaseDetected;

        public event NewSensorDetectedEventHandler NewSensorDetected;


        //private String LogFilePath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location),"ZibaseLog.txt");
        private String LogFilePath = Path.Combine( "c:\\","ZibaseLog.txt");
        private void LOG(String log)
        {
#if LOG
            File.AppendAllText(LogFilePath, log + "\r\n");
#endif
        }

        public void StartZB(bool bAutoSearch = true, UInt32 dwPort = 0)
        {
            m_AutoSearch = bAutoSearch;

            m_Server.Port = (int) dwPort;
            m_ThreadSearch = new Thread(ThreadSearch);
            m_ThreadZibase = new Thread(ThreadZibase);
            m_ThreadZibase.Start();
        }

        public void StopZB()
        {
            m_EndThread = true;
        }


        public void RestartZibaseSearch()
        {
            if (WriteMessage != null)
            {
                WriteMessage("Search for Zibase", MSG_DEBUG);
            }
            m_Zbs.BrowseForZibase();
        }

        public SensorInfo GetSensorInfo(string sID, string sType)
        {
            SensorInfo functionReturnValue = default(SensorInfo);
            if ((_SensorList.Keys.Contains(sID + sType)))
            {
                functionReturnValue = _SensorList[sID + sType];
            }
            return functionReturnValue;
        }

        public void SetServerPort(int Port)
        {
            m_Server.Port = Port;
            m_Zbs.SetServerPort((uint) Port);
        }

        public void AddZibase(string sZibaseIP, string sLocalIP)
        {
            string sNewZB = null;

            sNewZB = m_Zbs.InitZapi(sZibaseIP, sLocalIP);

            if ((!string.IsNullOrEmpty(sNewZB)))
            {
                IPAddress IpAddr = null;

                IpAddr = IPAddress.Parse(sZibaseIP);
                byte[] temp = IpAddr.GetAddressBytes();
                Array.Reverse(temp);

                AddZibaseToCollection(sNewZB, BitConverter.ToUInt32(temp, 0));
            }
        }


        private void ThreadSearch()
        {
            // Effectue une activation de l'api Zibase sur toute les Zibases du réseau
            if (WriteMessage != null)
                WriteMessage("Search for Zibase", MSG_DEBUG);
            m_Zbs.BrowseForZibase();
        }

        private void ThreadZibase()
        {
            Socket Sck = null;

            try
            {
                if (WriteMessage != null)
                    WriteMessage("Start running thread", MSG_DEBUG);

                Thread.Sleep(1000);

                Sck = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                if ((m_Server.Port == 0))
                {
                    m_Server.Port = 17100;
                }

                for (int i = 0; i <= 50; i++)
                {
                    try
                    {
                        Sck.Bind(m_Server);

                        break; // TODO: might not be correct. Was : Exit For
                    }
                    catch (Exception ex)
                    {
                        // Exception indiquant un port déjà utilisé
                        if (((SocketException) ex).SocketErrorCode == SocketError.AddressAlreadyInUse)
                        {
                            if (WriteMessage != null)
                                WriteMessage(
                                    "IP Address and Port already in use. Try next port : " + (m_Server.Port + 1),
                                    MSG_DEBUG);
                        }

                        m_Server.Port = m_Server.Port + 1;
                    }
                }

                m_Zbs.SetServerPort((uint) m_Server.Port);

                Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 200);

                if ((m_AutoSearch))
                {
                    m_ThreadSearch.Start();
                }

                while (!m_EndThread)
                {
                    try
                    {
                        if ((Sck.Available > 0))
                        {
                            var rBuff = new byte[Sck.Available + 1];
                            Sck.Receive(rBuff);

                            AfterReceive(rBuff);
                        }
                    }
                    catch (Exception)
                    {
                    }

                    Thread.Sleep(5);
                }
            }
            catch (Exception ex)
            {
                //General Exception received--add code here to analyze the exception. A messagebox would be one idea.
            }

            if ((Sck != null))
                Sck.Close();
            //Always close the socket when done.
        }

        // Permet d'extraire une valeur de la chaine renvoyé par la Zibase
        private string GetValue(string sStr, string sName)
        {
            string functionReturnValue = null;

            if (sStr.Contains("<" + sName + ">"))
            {
                int iStart = sStr.IndexOf("<" + sName + ">") + Strings.Len("<" + sName + ">");
                int iLen = sStr.IndexOf("</" + sName + ">") - iStart;
                functionReturnValue = sStr.Substring(iStart, iLen);
            }
            else
            {
                functionReturnValue = "";
            }
            return functionReturnValue;
        }


        private void AddZibaseToCollection(string sZibaseName, long lIpAddress)
        {
            var seInfo = new SensorInfo();

            if ((string.IsNullOrEmpty(sZibaseName)))
            {
                sZibaseName = "Unknown";
            }

            if ((!m_ZibaseList.Keys.Contains(sZibaseName)))
            {
                var zb = new ZibaseInfo {sLabelBase = sZibaseName, lIpAddress = lIpAddress};
                m_ZibaseList.Add(sZibaseName, zb);
                if (NewZibaseDetected != null)
                    NewZibaseDetected(zb);
                if (WriteMessage != null)
                    WriteMessage("New Zibase Detected : " + sZibaseName, MSG_INFO);

                // Creation d'un sensor virtuel pour la detection de l'availability de la Zibase
                seInfo.sName = "Zibase State";
                seInfo.sID = sZibaseName;
                seInfo.sType = "lnk";
                seInfo.sValue = "Online";
                seInfo.sHTMLValue = "Online";
                seInfo.dwValue = 2;

                if ((_SensorList.Keys.Contains(sZibaseName + "lnk")))
                {
                    seInfo.sHSName = _SensorList[sZibaseName + "lnk"].sHSName;
                    seInfo.sDevice = _SensorList[sZibaseName + "lnk"].sDevice;

                    _SensorList[sZibaseName + "lnk"] = seInfo;
                }
                else
                {
                    _SensorList.Add(sZibaseName + "lnk", seInfo);
                    if (NewSensorDetected != null)
                        NewSensorDetected(seInfo);
                }
                if (UpdateSensorInfo != null)
                    UpdateSensorInfo(seInfo);
            }
        }

        // Traitement des messages envoyés dans 
        private void AfterReceive(byte[] _data)
        {
            try
            {
                var seInfo = new SensorInfo();


                if (_data.Length >= 70 & _data[5] == 3)
                {
                    // On récupére d'abord les info générales sur le message
                    m_Zbs.SetData(_data);

                    string sZibaseName = Encoding.Default.GetString(m_Zbs.label_base);
                    int iPos = Strings.InStr(sZibaseName, Strings.Chr(0));
                    if (iPos > 0)
                        sZibaseName = sZibaseName.Substring(0,iPos - 1); // Strings.Left(sZibaseName, iPos - 1);

                    AddZibaseToCollection(sZibaseName, m_Zbs.my_ip);

                    string s = "";
                    int i = 0;
                    for (i = 70; i <= _data.Length - 2; i++)
                    {
                        s = s + Strings.Chr(_data[i]);
                    }

                    if (WriteMessage != null)
                        WriteMessage(sZibaseName + ":" + s, MSG_DEBUG);

                    LOG(s);

                    //s = "Received radio ID (<rf>433Mhz</rf> Noise=<noise>2564</noise> Level=<lev>4.6</lev>/5 <dev>Oregon THWR288A-THN132N</dev> Ch=<ch>1</ch> T=<tem>+15.4</tem>°C (+59.7°F) Batt=<bat>Ok</bat>): <id>OS3930910721</id>";
                    if ((s.Substring(0, 17).ToUpper() == "RECEIVED RADIO ID"))
                    {
                        seInfo.sID = GetValue(s, "id");
                        seInfo.sName = GetValue(s, "dev");
                        seInfo.sDate = DateTime.Now;

                        #region Remote Control

                        if ((Strings.Asc(seInfo.sID[0]) >= Strings.Asc('A') &&
                             Strings.Asc(seInfo.sID[0]) <= Strings.Asc('P') && (Char.IsNumber(seInfo.sID, 1))
                            /*| LPL ?? Information.IsNumeric(seInfo.sID.Substring(1).Replace("_OFF", ""))))*/))
                        {
                            seInfo.sName = "Remote Control";

                            // Traitement de la donnée reçu
                            // On parcours la liste des passerelles
                            for (i = 0; i <= 15; i++)
                            {
                                seInfo.sDevice = Strings.Chr(65 + i) + seInfo.sID.Substring(1);
                                seInfo.sValue = "";

                                switch (seInfo.sID.IndexOf("_OFF"))
                                {
                                    case -1:
                                        seInfo.dwValue = 2;
                                        seInfo.sValue = "On";
                                        break;
                                    default:
                                        seInfo.dwValue = 3;
                                        seInfo.sValue = "Off";
                                        break;
                                }

                                seInfo.sID = seInfo.sID.Replace("_OFF", "");

                                if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                            }

                            // On remet l'id d'origine version Zibase
                            // seInfo.sID = GetValue(s, "id").Replace("_OFF", "")
                        }

                        #endregion

                        // On modifie l'id pour Chacon et Visonic pour qui correspondent à l'actionneur (ON et OFF)
                        if ((seInfo.sID.Substring(0, 2) == "CS"))
                        {
                            seInfo.sID = "CS" + ((Convert.ToInt32(seInfo.sID.Substring(2), System.Globalization.CultureInfo.InvariantCulture) & ~0x10));
                            seInfo.sName = "Chacon";
                        }

                        if ((seInfo.sID.Substring(0, 2) == "VS"))
                        {
                            seInfo.sID = "VS" + ((Convert.ToInt32(seInfo.sID.Substring(2), System.Globalization.CultureInfo.InvariantCulture)) & ~0xf);
                            seInfo.sName = "Visonic";
                        }

                        if ((seInfo.sID.Substring(0, 2) == "DX"))
                        {
                            seInfo.sID = "DX" + seInfo.sID.Substring(2);
                            seInfo.sName = "X2D";
                        }

                        if ((seInfo.sID.Substring(0, 2) == "WS"))
                        {
                            LOG(seInfo.sID);
                            seInfo.sID = "WS" + ((Convert.ToInt32(seInfo.sID.Substring(2), System.Globalization.CultureInfo.InvariantCulture)) & ~0xf);
                            seInfo.sName = "OWL";
                        }

                        string sId = null;
                        string sValue = null;
                        string sType = null;

                        #region XS Security Device

                        if ((seInfo.sID.Substring(0, 2) == "XS"))
                        {
                            sValue = ((Convert.ToInt64(seInfo.sID.Substring(2), System.Globalization.CultureInfo.InvariantCulture)) & 0xff).ToString();
                            seInfo.sID = "XS" + ((Convert.ToInt64(seInfo.sID.Substring(2))) & ~0xff);
                            seInfo.sName = "X10 Secured";

                            sType = "xse";
                            seInfo.sType = sType;
                            seInfo.sDevice = "";

                            // Declaration d'une variable de type état
                            if ((!string.IsNullOrEmpty(sValue)))
                            {
                                seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture);
                                switch (seInfo.dwValue)
                                {
                                    case 0x20:
                                    case 0x30:
                                        seInfo.sValue = "ALERT";
                                        seInfo.sHTMLValue = "ALERT";
                                        break;
                                    case 0x21:
                                    case 0x31:
                                        seInfo.sValue = "NORMAL";
                                        seInfo.sHTMLValue = "NORMAL";
                                        break;
                                    case 0x40:
                                        seInfo.sValue = "ARM AWAY (max)";
                                        seInfo.sHTMLValue = "ARM AWAY (max)";
                                        break;
                                    case 0x41:
                                    case 0x61:
                                        seInfo.sValue = "DISARM";
                                        seInfo.sHTMLValue = "DISARM";
                                        break;
                                    case 0x42:
                                        seInfo.sValue = "SEC. LIGHT ON";
                                        seInfo.sHTMLValue = "SEC. LIGHT ON";
                                        break;
                                    case 0x43:
                                        seInfo.sValue = "SEC. LIGHT OFF";
                                        seInfo.sHTMLValue = "SEC. LIGHT OFF";
                                        break;
                                    case 0x44:
                                        seInfo.sValue = "PANIC";
                                        seInfo.sHTMLValue = "PANIC";
                                        break;
                                    case 0x50:
                                        seInfo.sValue = "ARM HOME";
                                        seInfo.sHTMLValue = "ARM HOME";
                                        break;
                                    case 0x60:
                                        seInfo.sValue = "ARM";
                                        seInfo.sHTMLValue = "ARM";
                                        break;
                                    case 0x62:
                                        seInfo.sValue = "LIGHTS ON";
                                        seInfo.sHTMLValue = "LIGHTS ON";
                                        break;
                                    case 0x63:
                                        seInfo.sValue = "LIGHTS OFF";
                                        seInfo.sHTMLValue = "LIGHTS OFF";
                                        break;
                                    case 0x70:
                                        seInfo.sValue = "ARM HOME (min)";
                                        seInfo.sHTMLValue = "ARM HOME (min)";
                                        break;
                                }

                                sId = seInfo.sID;

                                if ((_SensorList.Keys.Contains(sId + sType)))
                                {
                                    seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                    seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                    _SensorList[sId + sType] = seInfo;
                                }
                                else
                                {
                                    _SensorList.Add(sId + sType, seInfo);
                                }

                                if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                            }
                        }

                        #endregion

                        #region sta

                        sId = seInfo.sID;

                        sType = "sta";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type état
                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.sValue = sValue;
                            seInfo.sHTMLValue = sValue;

                            seInfo.dwValue = sValue == "ON" ? 2 : 3;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region lev

                        sType = "lev";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type strength level
                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = (int) (Convert.ToDouble(sValue,System.Globalization.CultureInfo.InvariantCulture)*10);
                            seInfo.sValue = (seInfo.dwValue/10.0) + "/5";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null)
                                UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region temc

                        sType = "temc";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type consigne de température
                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture);
                            seInfo.sValue = seInfo.dwValue + "°C";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null)
                                UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region kwh

                        sType = "kwh";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            LOG(DateTime.Now + " KWh :" + sValue);
                            seInfo.dwValue =
                                (long) (Convert.ToDouble(sValue, CultureInfo.InvariantCulture)*100);
                            seInfo.sValue = (seInfo.dwValue/100.0) + " kWh";
                            seInfo.sHTMLValue = sValue;
                            LOG(DateTime.Now + " Trace1");

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region kw

                        sType = "kw";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue =
                                (long) (Convert.ToDouble(sValue, CultureInfo.InvariantCulture)*100);
                            seInfo.sValue = (seInfo.dwValue/100.0) + " kW";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region tra

                        sType = "tra";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture) * 100;
                            seInfo.sValue = seInfo.dwValue + " mm";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region cra

                        sType = "cra";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture) * 100;
                            seInfo.sValue = seInfo.dwValue + " mm/h";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region awi

                        sType = "awi";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue =
                                (long) (Convert.ToDouble(sValue, CultureInfo.InvariantCulture)*100);
                            seInfo.sValue = seInfo.dwValue + " m/s";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region drt

                        sType = "drt";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture) * 100;
                            seInfo.sValue = seInfo.dwValue + " °";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region uvl

                        sType = "uvl";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture) * 100;
                            seInfo.sValue = seInfo.dwValue + "";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region bat

                        sType = "bat";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type battery
                        if ((!string.IsNullOrEmpty(sValue) & sValue != "?"))
                        {
                            seInfo.sValue = sValue;
                            seInfo.sHTMLValue = sValue;

                            seInfo.dwValue = sValue == "Low" ? 0 : 1;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region tem

                        // Gestion du type de sonde
                        sType = "tem";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type temperature
                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue =
                                (long) (Convert.ToDouble(sValue, CultureInfo.InvariantCulture)*100);
                            seInfo.sValue = String.Format("{0:0.0} °C", seInfo.dwValue/100.0); // "#.#") + "°C";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion

                        #region hum

                        sType = "hum";
                        seInfo.sType = sType;
                        seInfo.sDevice = "";
                        sValue = GetValue(s, sType);

                        // Declaration d'une variable de type humidity
                        if ((!string.IsNullOrEmpty(sValue)))
                        {
                            seInfo.dwValue = Convert.ToInt32(sValue, System.Globalization.CultureInfo.InvariantCulture);
                            seInfo.sValue = seInfo.dwValue + "%";
                            seInfo.sHTMLValue = sValue;

                            if ((_SensorList.Keys.Contains(sId + sType)))
                            {
                                seInfo.sHSName = _SensorList[sId + sType].sHSName;
                                seInfo.sDevice = _SensorList[sId + sType].sDevice;

                                _SensorList[sId + sType] = seInfo;
                            }
                            else
                            {
                                _SensorList.Add(sId + sType, seInfo);

                                if (NewSensorDetected != null) NewSensorDetected(seInfo);
                            }

                            if (UpdateSensorInfo != null) UpdateSensorInfo(seInfo);
                        }

                        #endregion
                    }
                }
            }
            catch(Exception ex)
            {
                LOG(ex.Message);
            }
        }

        public void SendCommand(string sAddress, State iState, int iDim = 0,
                                Protocol iProtocol = Protocol.PROTOCOL_CHACON, int iNbBurst = 1)
        {
            SendCommand("", sAddress, iState, iDim, iProtocol, iNbBurst);
        }

        public void SendCommand(string sZibaseName, string sAddress, State iState, int iDim = 0,
                                Protocol iProtocol = Protocol.PROTOCOL_CHACON, int iNbBurst = 1)
        {
            if ((Strings.Len(sAddress) < 2))
                return;

            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("SendX10");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;
            ZBS.param1 = 0;

            if (iState == State.STATE_DIM & iDim == 0)
            {
                iState = State.STATE_OFF;
            }

            switch (iState)
            {
                case State.STATE_OFF:
                    ZBS.param2 = 0;
                    break;
                case State.STATE_ON:
                    ZBS.param2 = 1;
                    break;
                case State.STATE_DIM:
                    ZBS.param2 = 3;
                    break;
            }

            // DEFAULT BROADCAST (RF X10, CHACON, DOMIA) : 0
            // VISONIC433:   1,          ( frequency : device RF LOW, 310...418Mhz band))
            // VISONIC868:   2,          (  frequency :  device RF HIGH, 868 Mhz Band)
            // CHACON (32B) (ChaconV2) :  3
            // DOMIA (24B) ( =Chacon V1 + low cost shit-devices):    4
            // RF X10 :    5
            ZBS.param2 = (uint) (ZBS.param2 | (((int) iProtocol & 0xff) << 8));

            // Dim
            if ((iState == State.STATE_DIM))
            {
                ZBS.param2 = (uint) (ZBS.param2 | ((iDim & 0xff) << 16));
            }

            if ((iNbBurst != 1))
            {
                ZBS.param2 = (uint) (ZBS.param2 | ((iNbBurst & 0xff) << 24));
            }

            string sHouse = Strings.Mid(sAddress, 1, 1);
            string sCode = Strings.Mid(sAddress, 2);

            ZBS.param3 = (uint) Convert.ToInt32(sCode) - 1;
            ZBS.param4 = (uint) Strings.Asc(sHouse[0]) - 65;

            SendToZibase(sZibaseName, ZBS);
        }

        public void ExecScript(string sScript)
        {
            ExecScript("", sScript);
        }

        public void RunScenario(string sZibaseName, string sName)
        {
            ExecScript(sZibaseName, "lm [" + sName + "]");
        }

        public void RunScenario(string sName)
        {
            RunScenario("", sName);
        }

        public void RunScenario(string sZibaseName, int iNum)
        {
            ExecScript(sZibaseName, "lm " + iNum);
        }

        public void RunScenario(int iNum)
        {
            RunScenario("", iNum);
        }

        public void ExecScript(string sZibaseName, string sScript)
        {
            sScript = "cmd:" + sScript;

            if ((Strings.Len(sScript) > 96))
                return;

            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 16;
            ZBS.alphacommand = ZBS.GetBytesFromString("SendCmd");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.command_text = ZBS.GetBytesFromString(sScript);

            ZBS.serial = 0;
            ZBS.param1 = 0;
            ZBS.param2 = 0;
            ZBS.param3 = 0;
            ZBS.param4 = 0;
            SendToZibase(sZibaseName, ZBS);
        }


        public UInt32 GetVar(UInt32 dwNumVar)
        {
            return GetVar("", dwNumVar);
        }

        public UInt32 GetVar(string sZibaseName, UInt32 dwNumVar)
        {
            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("GetVar");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 5;
            ZBS.param2 = 0;
            ZBS.param3 = CMD_READ_VAR;
            ZBS.param4 = dwNumVar;

            return SendToZibase(sZibaseName, ZBS);
        }

        public bool GetX10State(char house, byte unit)
        {
            return GetX10State("", house, unit);
        }

        public bool GetX10State(string sZibaseName, char house, byte unit)
        {
            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("GetX10");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 5;
            ZBS.param2 = 0;
            ZBS.param3 = CMD_READ_X10;
            ZBS.param4 = (uint) ((Strings.Asc(house) - Strings.Asc('A')) << 8) | unit;

            return SendToZibase(sZibaseName, ZBS) == 1;
        }


        public UInt32 GetCalendar(UInt32 dwNumCal)
        {
            return GetCalendar("", dwNumCal);
        }


        public uint SendToZibase(String ZibaseName, ZBClass SendBuffer)
        {
            byte[] dataRcv = null;
            var ZibaseReceiveBuf = new ZBClass();
            IEnumerable<ZibaseInfo> q = (from c in m_ZibaseList
                                         where c.Value.sLabelBase == ZibaseName || String.IsNullOrEmpty(ZibaseName)
                                         select c.Value);

            if (q.Any())
            {
                ZibaseInfo zInfo = q.First();
                String ZibaseIP = zInfo.GetIPAsString();
                m_Zbs.UDPDataTransmit(SendBuffer.GetBytes(), ref dataRcv, ZibaseIP, 49999);
                ZibaseReceiveBuf.SetData(dataRcv);
                return ZibaseReceiveBuf.param1;
            }
            return 0;
        }

        public UInt32 GetCalendar(string sZibaseName, UInt32 dwNumCal)
        {
            var ZBS = new ZBClass();


            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("GetCal");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 5;
            ZBS.param2 = 0;
            ZBS.param3 = CMD_READ_CAL;
            ZBS.param4 = dwNumCal;

            return SendToZibase(sZibaseName, ZBS);
        }

        public void SetVar(UInt32 dwNumVar, UInt32 dwVal)
        {
            SetVar("", dwNumVar, dwVal);
        }

        public void SetVar(string sZibaseName, UInt32 dwNumVar, UInt32 dwVal)
        {
            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("SetVar");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 5;
            ZBS.param2 = dwVal;
            ZBS.param3 = CMD_TYPE_WRITE_VAR;
            ZBS.param4 = dwNumVar;

            SendToZibase(sZibaseName, ZBS);
        }

        public void SetCalendar(UInt32 dwNumCal, UInt32 dwVal)
        {
            SetCalendar("", dwNumCal, dwVal);
        }

        public void SetCalendar(string sZibaseName, UInt32 dwNumCal, UInt32 dwVal)
        {
            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("SetCal");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 5;
            ZBS.param2 = dwVal;
            ZBS.param3 = CMD_WRITE_CAL;
            ZBS.param4 = dwNumCal;

            SendToZibase(sZibaseName, ZBS);
        }

        public string GetCalendarAsString(UInt32 dwNumCal)
        {
            return GetCalendarAsString("", dwNumCal);
        }

        public string GetCalendarAsString(string sZibaseName, UInt32 dwNumCal)
        {
            UInt32 val = default(UInt32);
            string sHour = null;
            string sDay = null;

            val = GetCalendar(sZibaseName, dwNumCal);

            sHour = "";
            sDay = "";

            for (int i = 0; i <= 30; i++)
            {
                if ((i <= 23))
                {
                    if ((val & 1) == 1)
                    {
                        sHour = sHour + "1";
                    }
                    else
                    {
                        sHour = sHour + "0";
                    }
                }
                else
                {
                    if ((val & 1) == 1)
                    {
                        sDay = sDay + "1";
                    }
                    else
                    {
                        sDay = sDay + "0";
                    }
                }

                val = val >> 1;
            }

            return sDay + ";" + sHour;
        }

        public UInt32 GetCalendarFromString(string sDay, string sHour)
        {
            UInt32 val = default(UInt32);

            // On compléte les variables pour être sur du nombre de donneés
            sDay = sDay + "0000000";
            sHour = sHour + "000000000000000000000000";

            val = 0;

            for (int i = 6; i >= 0; i += -1)
            {
                if ((sDay.ElementAt(i) == '1'))
                    val = val | 1;
                val = val << 1;
            }
            for (int i = 23; i >= 0; i += -1)
            {
                if ((sHour.ElementAt(i) == '1'))
                    val = val | 1;
                if ((i != 0))
                    val = val << 1;
            }

            return val;
        }

        public void SetVirtualProbeValue(UInt32 dwSensorID, VirtualProbeType SensorType, UInt32 dwValue1,
                                         UInt32 dwValue2, UInt32 dwLowBat)
        {
            SetVirtualProbeValue("", (ushort) dwSensorID, SensorType, dwValue1, dwValue2, dwLowBat);
        }

        public void SetVirtualProbeValue(string sZibaseName, UInt16 wSensorID, VirtualProbeType SensorType,
                                         UInt32 dwValue1, UInt32 dwValue2, UInt32 dwLowBat)
        {
            var ZBS = new ZBClass();
            //ZBClass ZBSrcv = new ZBClass();
            int iSensorType = 0;
            UInt32 dwSensorID = default(UInt32);

            switch (SensorType)
            {
                    // Simule un OWL
                case VirtualProbeType.POWER_SENSOR:
                    iSensorType = DOMO_EVENT_ACTION_OWL_SIGNAL_32B_SENSOR_CODE;
                    dwSensorID = (uint) 0x2 << 16 | wSensorID;

                    break;
                    // Simule une THGR228
                case VirtualProbeType.TEMP_HUM_SENSOR:
                    iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE;
                    dwSensorID = (uint) (0x1a2d << 16) | wSensorID;

                    break;
                    // Simule une THN132
                case VirtualProbeType.TEMP_SENSOR:
                    iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE;
                    dwSensorID = (uint) (0x1 << 16) | wSensorID;

                    break;
                    // Simule un pluviometre
                case VirtualProbeType.WATER_SENSOR:
                    iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE;
                    dwSensorID = (uint) (0x2a19 << 16) | wSensorID;
                    break;
            }


            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("VProbe");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 6;
            ZBS.param2 = dwSensorID;
            ZBS.param3 = dwValue1 | (dwValue2 << 16) | (dwLowBat << 24);
            ZBS.param4 = (uint) iSensorType;

            SendToZibase(sZibaseName, ZBS);
        }

        public void SetPlatform(UInt32 dwPlatform, UInt32 dwPasswordIn, UInt32 dwPasswordOut)
        {
            SetPlatform("", dwPlatform, dwPasswordIn, dwPasswordOut);
        }

        public void SetPlatform(string sZibaseName, UInt32 dwPlatform, UInt32 dwPasswordIn, UInt32 dwPasswordOut)
        {
            var ZBS = new ZBClass();

            ZBS.header = ZBS.GetBytesFromString("ZSIG");
            ZBS.command = 11;
            ZBS.alphacommand = ZBS.GetBytesFromString("SetPlatform");
            ZBS.label_base = ZBS.GetBytesFromString("");

            ZBS.serial = 0;

            ZBS.param1 = 7;
            ZBS.param2 = dwPasswordIn;
            ZBS.param3 = dwPasswordOut;
            ZBS.param4 = dwPlatform;

            SendToZibase(sZibaseName, ZBS);
        }


        // Permet d'associer un token à une zibase. Ce token sera ensuite utilisé pour récupérer des données depuis la plateforme zodianet (liste des scénarios par exemple)
        public void SetZibaseToken(string sZibaseName, string sToken)
        {
            if ((m_ZibaseList.Keys.Contains(sZibaseName)))
            {
                var zb = new ZibaseInfo();

                zb = m_ZibaseList[sZibaseName];
                zb.sToken = sToken;
                m_ZibaseList[sZibaseName] = zb;
            }
        }

        public string GetScenarioList(string sZibaseName)
        {
            string functionReturnValue = null;
            if ((m_ZibaseList.Keys.Contains(sZibaseName)))
            {
                var zb = new ZibaseInfo();

                zb = m_ZibaseList[sZibaseName];

                if ((zb.sToken == null))
                {
                    functionReturnValue = "Token must be defined";
                }
                else
                {
                    // On charge la liste des scènarios depuis la plateforme zodianet
                    var xDoc = new XmlDocument();

                    xDoc.Load("http://www.zibase.net/m/get_xml.php?device=" + sZibaseName + "&token=" + zb.sToken);

                    XmlNodeList scenario = xDoc.GetElementsByTagName("m");
                    string sSceList = null;

                    sSceList = "";

                    {
                        string s = null;
                        string sce_name = null;

                        foreach (XmlNode node in scenario)
                        {
                            s = node.Attributes.GetNamedItem("id").Value;
                            sce_name = node.ChildNodes.Item(0).InnerText;

                            if ((!string.IsNullOrEmpty(sSceList)))
                                sSceList = sSceList + "|";
                            sSceList = sSceList + sce_name + ";" + s;
                        }

                        functionReturnValue = sSceList;
                    }
                }
            }
            else
            {
                functionReturnValue = "Zibase not found";
            }
            return functionReturnValue;
        }

        public string GetDevicesList(string sZibaseName)
        {
            string functionReturnValue = null;
            if ((m_ZibaseList.Keys.Contains(sZibaseName)))
            {
                var zb = new ZibaseInfo();

                zb = m_ZibaseList[sZibaseName];

                if ((zb.sToken == null))
                {
                    functionReturnValue = "Token must be defined";
                }
                else
                {
                    // On charge la liste des scènarios depuis la plateforme zodianet
                    var xDoc = new XmlDocument();

                    xDoc.Load("http://www.zibase.net/m/get_xml.php?device=" + sZibaseName + "&token=" + zb.sToken);

                    XmlNodeList sensors = xDoc.GetElementsByTagName("e");
                    string sSensorsList = null;

                    sSensorsList = "";
                    string stype = null;
                    string sid = null;
                    string sce_name = null;

                    foreach (XmlNode node in sensors)
                    {
                        stype = node.Attributes.GetNamedItem("t").Value;
                        sid = node.Attributes.GetNamedItem("c").Value;
                        sce_name = node.ChildNodes.Item(0).InnerText;

                        if ((!string.IsNullOrEmpty(sSensorsList)))
                            sSensorsList = sSensorsList + "|";
                        sSensorsList = sSensorsList + sce_name + ";" + stype + ";" + sid;
                    }

                    functionReturnValue = sSensorsList;
                }
            }
            else
            {
                functionReturnValue = "Zibase not found";
            }
            return functionReturnValue;
        }
    }

    // loic.ploumen 
    // created only for VB to C# conversion purpose
    // TODO : remove it :)
    public static class Strings
    {
        public static int Len(String s)
        {
            return s.Length;
        }

        public static int InStr(String s, String s1)
        {
            return s.IndexOf(s1);
        }

        public static int InStr(String s, char c)
        {
            return s.IndexOf(c);
        }

        public static char Chr(int v)
        {
            return (char) v;
        }

        public static int Asc(char c)
        {
            return c;
        }

        public static string Mid(string s, int a, int b)
        {
            string temp = s.Substring(a - 1, b);
            return temp;
        }

        public static string Mid(string s, int a)
        {
            string temp = s.Substring(a - 1);
            return temp;
        }
    }
}