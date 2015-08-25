Imports System.Net.Sockets
Imports System.Net

Public Class ZBClass

    Public header(4) As Byte
    Public command As UInt16
    Public alphacommand(8) As Byte
    Public serial As UInt32
    Public sid As UInt32
    Public label_base(16) As Byte
    Public my_ip As UInt32
    Public my_port As UInt32
    Public reserved1 As UInt32
    Public reserved2 As UInt32
    Public param1 As UInt32
    Public param2 As UInt32
    Public param3 As UInt32
    Public param4 As UInt32
    Public my_count As UInt16
    Public your_count As UInt16
    Public command_text(96) As Byte

    Public Sub SetData(ByVal data() As Byte)

        If (data Is Nothing) Then Exit Sub
        If (data.Length < 70) Then Exit Sub

        Array.Copy(data, 0, header, 0, 4)

        Array.Reverse(data, 4, 2)
        command = BitConverter.ToUInt16(data, 4)

        Array.Copy(data, 6, alphacommand, 0, 8)

        'Dim s As String = System.Text.Encoding.Default.GetString(alphacommand)

        Array.Reverse(data, 14, 4)
        serial = BitConverter.ToUInt32(data, 14)

        Array.Reverse(data, 18, 4)
        sid = BitConverter.ToUInt32(data, 18)

        Array.Copy(data, 22, label_base, 0, 16)

        Array.Reverse(data, 38, 4)
        my_ip = BitConverter.ToUInt32(data, 38)

        Array.Reverse(data, 42, 4)
        reserved1 = BitConverter.ToUInt32(data, 42)

        Array.Reverse(data, 46, 4)
        reserved2 = BitConverter.ToUInt32(data, 46)

        Array.Reverse(data, 50, 4)
        param1 = BitConverter.ToUInt32(data, 50)

        Array.Reverse(data, 54, 4)
        param2 = BitConverter.ToUInt32(data, 54)

        Array.Reverse(data, 58, 4)
        param3 = BitConverter.ToUInt32(data, 58)

        Array.Reverse(data, 62, 4)
        param4 = BitConverter.ToUInt32(data, 62)

        Array.Reverse(data, 66, 2)
        my_count = BitConverter.ToUInt16(data, 66)

        Array.Reverse(data, 68, 2)
        your_count = BitConverter.ToUInt16(data, 68)

        ' Sur un paquet de type étendu, on extrait en plus la commande
        If (data.Length = 166) Then
            Array.Copy(data, 70, command_text, 0, 96)
            ' s = System.Text.Encoding.Default.GetString(command_text)
        End If
    End Sub

    Private Sub CopyBytes(ByRef Data() As Byte, ByVal val As UInt32, ByRef iCur As Integer)
        Dim temp() As Byte
        temp = BitConverter.GetBytes(val)
        Array.Reverse(temp)
        Array.Copy(temp, 0, Data, iCur, 4)
        iCur = iCur + 4
    End Sub

    Private Sub CopyBytes(ByRef Data() As Byte, ByVal val As UInt16, ByRef iCur As Integer)
        Dim temp() As Byte
        temp = BitConverter.GetBytes(val)
        Array.Reverse(temp)
        Array.Copy(temp, 0, Data, iCur, 2)
        iCur = iCur + 2
    End Sub

    Private Sub CopyBytes(ByRef Data() As Byte, ByVal val() As Byte, ByVal iSize As Integer, ByRef iCur As Integer)
        Dim i As Integer

        For i = 0 To iSize - 1
            If (i < val.Length) Then
                Data(iCur) = val(i)
            Else
                Data(iCur) = 0
            End If
            iCur = iCur + 1
        Next
    End Sub


    Public Function GetBytes() As Byte()
        Dim data(69) As Byte
        Dim iCur As Integer = 0

        CopyBytes(data, header, 4, iCur)
        CopyBytes(data, command, iCur)
        CopyBytes(data, alphacommand, 8, iCur)
        CopyBytes(data, serial, iCur)
        CopyBytes(data, sid, iCur)
        CopyBytes(data, label_base, 16, iCur)

        CopyBytes(data, my_ip, iCur)
        CopyBytes(data, reserved1, iCur)
        CopyBytes(data, reserved2, iCur)

        CopyBytes(data, param1, iCur)
        CopyBytes(data, param2, iCur)
        CopyBytes(data, param3, iCur)
        CopyBytes(data, param4, iCur)

        CopyBytes(data, my_count, iCur)
        CopyBytes(data, your_count, iCur)

        If (command_text(0) <> 0) Then
            ReDim Preserve data(165)
            CopyBytes(data, command_text, 96, iCur)
        End If

        GetBytes = data
    End Function

    Function GetBytesFromString(ByVal sSrc As String) As Byte()
        Dim arr(sSrc.Length) As Byte
        Dim i As Integer

        For i = 0 To sSrc.Length - 1
            arr(i) = Convert.ChangeType(sSrc(i), TypeCode.Byte)
        Next

        GetBytesFromString = arr

    End Function

    Public Sub SetServerPort(ByVal dwPort As UInt32)
        my_port = dwPort
    End Sub

    Public Function InitZapi(ByVal sZibaseIP As String, ByVal sLocalIP As String) As String
        Dim ZBS As New ZBClass
        Dim IpAddr As IPAddress
        Dim sZibaseName As String = ""

        ZBS.header = GetBytesFromString("ZSIG")
        ZBS.command = 13
        ZBS.alphacommand = GetBytesFromString("ZapiInit")
        ZBS.label_base = GetBytesFromString("")

        ZBS.serial = 0

        ' If (sAddr = "10.40.1.255") Then
        '       IpAdd(i) = IPAddress.Parse("192.168.1.16")
        ' End If

        IpAddr = IPAddress.Parse(sLocalIP)

        Dim temp() As Byte = IpAddr.GetAddressBytes()
        Array.Reverse(temp)
        ZBS.param1 = BitConverter.ToUInt32(temp, 0)
        ZBS.param2 = my_port
        ZBS.param3 = 0
        ZBS.param4 = 0

        Dim data() As Byte = Nothing
        UDPDataTransmit(ZBS.GetBytes(), data, sZibaseIP, 49999)

        'Detection d'une nouvelle zibase
        If Not data Is Nothing Then
            If data.Length >= 70 Then
                Dim ZBSrcv As New ZBClass
                ZBSrcv.SetData(data)
                sZibaseName = System.Text.Encoding.Default.GetString(ZBSrcv.label_base)
                Dim iPos As Integer = InStr(sZibaseName, Chr(0))
                If iPos > 0 Then sZibaseName = Left(sZibaseName, iPos - 1)
            End If
        End If

        InitZapi = sZibaseName
    End Function

    ' Cette fonction permet de parcourrir les différents réseaux incluant le PC et de transmettre un ordre d'activation de l'API Zapi
    Public Sub BrowseForZibase()
        Dim i As Integer

        ' On liste les adresses IP du PC
        Dim ipEnter As IPHostEntry = Dns.GetHostEntry(Dns.GetHostName())
        Dim IpAdd() As IPAddress = ipEnter.AddressList

        Dim sLocalIP As String
        Dim sBroadcastIP As String

        For i = 0 To IpAdd.GetUpperBound(0)

            If IpAdd(i).AddressFamily = AddressFamily.InterNetwork Then

                sLocalIP = IpAdd(i).GetAddressBytes(0) & "." & IpAdd(i).GetAddressBytes(1) & "." & IpAdd(i).GetAddressBytes(2) & "." & IpAdd(i).GetAddressBytes(3)
                sBroadcastIP = IpAdd(i).GetAddressBytes(0) & "." & IpAdd(i).GetAddressBytes(1) & "." & IpAdd(i).GetAddressBytes(2) & ".255"

                InitZapi(sBroadcastIP, sLocalIP)
            End If
        Next
    End Sub

    Public Function UDPDataTransmit( _
             ByVal sBuff() As Byte, ByRef rBuff() As Byte, _
             ByVal IP As String, ByVal Port As Integer) As Integer 'Returns # bytes received

        Dim retstat As Integer
        Dim Sck As Sockets.Socket
        Dim Due As DateTime
        Dim Encrp As IPEndPoint
        Try
            Sck = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000)
            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 2000)

            Encrp = New IPEndPoint(IPAddress.Parse(IP), Port)

            retstat = Sck.SendTo(sBuff, 0, sBuff.Length, SocketFlags.None, Encrp)

            If retstat > 0 Then
                Due = Now.AddMilliseconds(2000) '10 second time-out

                Do While Sck.Available = 0 AndAlso Now < Due
                Loop

                If Sck.Available = 0 Then
                    'timed-out
                    retstat = -3
                    Return retstat
                End If

                ReDim rBuff(Sck.Available - 1)

                retstat = Sck.Receive(rBuff, 0, Sck.Available, SocketFlags.None)

            Else
                retstat = -1 ' fail on send
            End If
        Catch ex As Exception
            'General Exception received--add code here to analyze the exception. A messagebox would be one idea.
            retstat = -2
        Finally
            Sck.Close() 'Always close the socket when done.
        End Try
        Return retstat
    End Function



End Class
