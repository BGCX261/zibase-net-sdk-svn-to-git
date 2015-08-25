using System;
using System.Net.Sockets;
using System.Net;

namespace ZibaseDll
{


    public class ZBClass
    {

        public byte[] header = new byte[5];
        public UInt16 command;
        public byte[] alphacommand = new byte[9];
        public UInt32 serial;
        public UInt32 sid;
        public byte[] label_base = new byte[17];
        public UInt32 my_ip;
        public UInt32 my_port;
        public UInt32 reserved1;
        public UInt32 reserved2;
        public UInt32 param1;
        public UInt32 param2;
        public UInt32 param3;
        public UInt32 param4;
        public UInt16 my_count;
        public UInt16 your_count;

        public byte[] command_text = new byte[97];

        public void SetData(byte[] data)
        {
            if ((data == null))
                return;
            if ((data.Length < 70))
                return;

            Array.Copy(data, 0, header, 0, 4);

            Array.Reverse(data, 4, 2);
            command = BitConverter.ToUInt16(data, 4);

            Array.Copy(data, 6, alphacommand, 0, 8);

            //Dim s As String = System.Text.Encoding.Default.GetString(alphacommand)

            Array.Reverse(data, 14, 4);
            serial = BitConverter.ToUInt32(data, 14);

            Array.Reverse(data, 18, 4);
            sid = BitConverter.ToUInt32(data, 18);

            Array.Copy(data, 22, label_base, 0, 16);

            Array.Reverse(data, 38, 4);
            my_ip = BitConverter.ToUInt32(data, 38);

            Array.Reverse(data, 42, 4);
            reserved1 = BitConverter.ToUInt32(data, 42);

            Array.Reverse(data, 46, 4);
            reserved2 = BitConverter.ToUInt32(data, 46);

            Array.Reverse(data, 50, 4);
            param1 = BitConverter.ToUInt32(data, 50);

            Array.Reverse(data, 54, 4);
            param2 = BitConverter.ToUInt32(data, 54);

            Array.Reverse(data, 58, 4);
            param3 = BitConverter.ToUInt32(data, 58);

            Array.Reverse(data, 62, 4);
            param4 = BitConverter.ToUInt32(data, 62);

            Array.Reverse(data, 66, 2);
            my_count = BitConverter.ToUInt16(data, 66);

            Array.Reverse(data, 68, 2);
            your_count = BitConverter.ToUInt16(data, 68);

            // Sur un paquet de type étendu, on extrait en plus la commande
            if ((data.Length == 166))
            {
                Array.Copy(data, 70, command_text, 0, 96);
                // s = System.Text.Encoding.Default.GetString(command_text)
            }
        }

        private void CopyBytes(ref byte[] Data, UInt32 val, ref int iCur)
        {
            byte[] temp = null;
            temp = BitConverter.GetBytes(val);
            Array.Reverse(temp);
            Array.Copy(temp, 0, Data, iCur, 4);
            iCur = iCur + 4;
        }

        private void CopyBytes(ref byte[] Data, UInt16 val, ref int iCur)
        {
            byte[] temp = null;
            temp = BitConverter.GetBytes(val);
            Array.Reverse(temp);
            Array.Copy(temp, 0, Data, iCur, 2);
            iCur = iCur + 2;
        }

        private void CopyBytes(ref byte[] Data, byte[] val, int iSize, ref int iCur)
        {
            int i = 0;

            for (i = 0; i <= iSize - 1; i++)
            {
                if ((i < val.Length))
                {
                    Data[iCur] = val[i];
                }
                else
                {
                    Data[iCur] = 0;
                }
                iCur = iCur + 1;
            }
        }


        public byte[] GetBytes()
        {
            byte[] data = new byte[70];
            int iCur = 0;

            CopyBytes(ref data, header, 4, ref iCur);
            CopyBytes(ref data, command, ref iCur);
            CopyBytes(ref data, alphacommand, 8, ref iCur);
            CopyBytes(ref data, serial, ref iCur);
            CopyBytes(ref data, sid, ref iCur);
            CopyBytes(ref data, label_base, 16, ref iCur);

            CopyBytes(ref data, my_ip, ref iCur);
            CopyBytes(ref data, reserved1, ref iCur);
            CopyBytes(ref data, reserved2, ref iCur);

            CopyBytes(ref data, param1, ref iCur);
            CopyBytes(ref data, param2, ref iCur);
            CopyBytes(ref data, param3, ref iCur);
            CopyBytes(ref data, param4, ref iCur);

            CopyBytes(ref data, my_count, ref iCur);
            CopyBytes(ref data, your_count, ref iCur);

            if ((command_text[0] != 0))
            {
                Array.Resize(ref data, 166);
                CopyBytes(ref data, command_text, 96, ref iCur);
            }

            return data;
        }

        public byte[] GetBytesFromString(string sSrc)
        {
            byte[] arr = new byte[sSrc.Length + 1];
            int i = 0;

            for (i = 0; i <= sSrc.Length - 1; i++)
            {
                arr[i] = (Byte) Convert.ChangeType(sSrc[i], TypeCode.Byte);
            }

            return arr;

        }

        public void SetServerPort(UInt32 dwPort)
        {
            my_port = dwPort;
        }

        public string InitZapi(string sZibaseIP, string sLocalIP)
        {
            ZBClass ZBS = new ZBClass();
            IPAddress IpAddr = null;
            string sZibaseName = "";

            ZBS.header = GetBytesFromString("ZSIG");
            ZBS.command = 13;
            ZBS.alphacommand = GetBytesFromString("ZapiInit");
            ZBS.label_base = GetBytesFromString("");

            ZBS.serial = 0;

            // If (sAddr = "10.40.1.255") Then
            //       IpAdd(i) = IPAddress.Parse("192.168.1.16")
            // End If

            IpAddr = IPAddress.Parse(sLocalIP);

            byte[] temp = IpAddr.GetAddressBytes();
            Array.Reverse(temp);
            ZBS.param1 = BitConverter.ToUInt32(temp, 0);
            ZBS.param2 = my_port;
            ZBS.param3 = 0;
            ZBS.param4 = 0;

            byte[] data = null;
            UDPDataTransmit(ZBS.GetBytes(), ref data, sZibaseIP, 49999);

            //Detection d'une nouvelle zibase
            if ((data != null))
            {
                if (data.Length >= 70)
                {
                    ZBClass ZBSrcv = new ZBClass();
                    ZBSrcv.SetData(data);
                    sZibaseName = System.Text.Encoding.Default.GetString(ZBSrcv.label_base);
                    int iPos = Strings.InStr(sZibaseName, Strings.Chr(0));
                    if (iPos > 0)
                        sZibaseName = sZibaseName.Substring(iPos - 1); // Strings.Left(sZibaseName, iPos - 1);
                }
            }

            return sZibaseName;
        }

        // Cette fonction permet de parcourrir les différents réseaux incluant le PC et de transmettre un ordre d'activation de l'API Zapi
        public void BrowseForZibase()
        {
            int i = 0;

            // On liste les adresses IP du PC
            IPHostEntry ipEnter = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress[] IpAdd = ipEnter.AddressList;


            for (i = 0; i <= IpAdd.GetUpperBound(0); i++)
            {

                if (IpAdd[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    string sLocalIP = IpAdd[i].GetAddressBytes()[0] + "." + IpAdd[i].GetAddressBytes()[1] + "." + IpAdd[i].GetAddressBytes()[2] + "." + IpAdd[i].GetAddressBytes()[3];
                    string sBroadcastIP = IpAdd[i].GetAddressBytes()[0] + "." + IpAdd[i].GetAddressBytes()[1] + "." + IpAdd[i].GetAddressBytes()[2] + ".255";

                    InitZapi(sBroadcastIP, sLocalIP);
                }
            }
        }

        public int UDPDataTransmit(byte[] sBuff, ref byte[] rBuff, string IP, int Port)
        {
            //Returns # bytes received

            int retstat = 0;
            System.Net.Sockets.Socket Sck = null;
            DateTime Due = default(DateTime);
            try
            {
                Sck = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
                Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 2000);

                IPEndPoint Encrp = new IPEndPoint(IPAddress.Parse(IP), Port);

                retstat = Sck.SendTo(sBuff, 0, sBuff.Length, SocketFlags.None, Encrp);

                if (retstat > 0)
                {
                    Due = DateTime.Now.AddMilliseconds(2000);
                    //10 second time-out

                    while (Sck.Available == 0 && DateTime.Now < Due)
                    {
                    }

                    if (Sck.Available == 0)
                    {
                        //timed-out
                        retstat = -3;
                        return retstat;
                    }

                    rBuff = new byte[Sck.Available];

                    retstat = Sck.Receive(rBuff, 0, Sck.Available, SocketFlags.None);

                }
                else
                {
                    retstat = -1;
                    // fail on send
                }
            }
            catch (Exception)
            {
                //General Exception received--add code here to analyze the exception. A messagebox would be one idea.
                retstat = -2;
            }
            finally
            {
                Sck.Close();
                //Always close the socket when done.
            }
            return retstat;
        }



    }

}
