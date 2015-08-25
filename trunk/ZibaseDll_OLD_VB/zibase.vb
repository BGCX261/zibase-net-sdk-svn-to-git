'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
' Changelog - Dll de communication Zibase
'
''''
'' v1.0 - 05/2010 - Version initiale
'' v1.1 - 09/2010 - Ajout de type de données (xse, vs...)
'' v1.2 - 12/2010 - Possibilité de choisir les paramètres d'association d'une base
''                - Correction d'un bug sur les codes unités >10 dans Send Command
''                - Gestion des variables, calendriers et état X10
''                - Meilleur détection des zibases dès le démarrage
''                - Gestion des virtuals probe !!!!!
''                - Gestion de la liste des scénarios et des capteurs depuis la plateforme

Imports System.Threading
Imports System.Net
Imports System.Net.Sockets
Imports System.ComponentModel
Imports System.Xml
Imports System.Net.NetworkInformation

Public Class ZiBase
    Public Structure SensorInfo
        Dim sHSName As String
        Dim sName As String
        Dim sType As String
        Dim sID As String
        Dim dwValue As Long
        Dim sValue As String
        Dim sHTMLValue As String
        Dim sDevice As String
        Public sDate As Date
    End Structure

    Public Structure ZibaseInfo
        Dim sLabelBase As String
        Dim lIpAddress As Long
        Dim sToken As String
    End Structure

    Private _ThreadZibase As Thread
    Private _ThreadSearch As Thread
    Private _Server As New IPEndPoint(IPAddress.Any, 0)
    Private _BytesReceived As Integer = 0
    Private _EndThread As Boolean = False
    Private _AutoSearch As Boolean = True
    ' Private _DisableUIEvent As Boolean = False
    Private _ZBS As New ZBClass

    Private _SensorList As New Hashtable
    Private _ZibaseList As New Hashtable

    Public Const MSG_INFO As Integer = 0
    Public Const MSG_DEBUG As Integer = 1
    Public Const MSG_DEBUG_NOLOG As Integer = 2
    Public Const MSG_WARNING As Integer = 3
    Public Const MSG_ERROR As Integer = 4

    Private Const CMD_READ_VAR As Integer = 0
    Private Const CMD_TYPE_WRITE_VAR As Integer = 1
    Private Const CMD_READ_CAL As Integer = 2
    Private Const CMD_WRITE_CAL As Integer = 3
    Private Const CMD_READ_X10 As Integer = 4

    Private Const DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE = 17
    Private Const DOMO_EVENT_ACTION_OWL_SIGNAL_32B_SENSOR_CODE = 20


    Public Event WriteMessage(ByVal sMsg As String, ByVal level As Integer)
    Public Event UpdateSensorInfo(ByVal seInfo As SensorInfo)
    Public Event NewZibaseDetected(ByVal zbInfo As ZibaseInfo)
    Public Event NewSensorDetected(ByVal seInfo As SensorInfo)

    Public Enum State
        STATE_OFF = 0
        STATE_ON = 1
        STATE_DIM = 3
        STATE_ASSOC = 7
    End Enum

    Public Enum Protocol
        PROTOCOL_BROADCAST = 0
        PROTOCOL_VISONIC433 = 1
        PROTOCOL_VISONIC868 = 2
        PROTOCOL_CHACON = 3
        PROTOCOL_DOMIA = 4
        PROTOCOL_X10 = 5
        PROTOCOL_ZWAVE = 6
        PROTOCOL_RFS10 = 7
        PROTOCOL_X2D433 = 8
        PROTOCOL_X2D868 = 9
    End Enum

    Public Enum VirtualProbeType
        TEMP_SENSOR = 0
        TEMP_HUM_SENSOR = 1
        POWER_SENSOR = 2
        WATER_SENSOR = 3
    End Enum

    Public Enum ZibasePlateform
        ZODIANET = 0
        RESERVED = 1
        PLANETE_DOMOTIQUE = 2
        DOMADOO = 3
        ROBOPOLIS = 4
    End Enum

    Shared Sub Raise(ByVal [event] As [Delegate], ByVal data As Object())
        If [event] IsNot Nothing Then
            For Each C In [event].GetInvocationList
                Dim T = CType(C.Target, ISynchronizeInvoke)
                If T IsNot Nothing AndAlso T.InvokeRequired Then T.BeginInvoke(C, data) Else C.DynamicInvoke(data)
            Next
        End If
    End Sub

    'Public Sub DisableUIEvent(ByVal bDisable As Boolean)
    '    _DisableUIEvent = bDisable
    'End Sub

    Public Sub StartZB(Optional ByVal bAutoSearch As Boolean = True, Optional ByVal dwPort As UInt32 = 0)
        Try
            _AutoSearch = bAutoSearch

            _Server.Port = dwPort
            'If (dwPort <> 0) Then
            'Else
            '    _Server.Port = GetFreeUDPPort()
            'End If

            _ThreadSearch = New Thread(AddressOf ThreadSearch)
            _ThreadZibase = New Thread(AddressOf ThreadZibase)
            _ThreadZibase.Start()
        Catch ex As Exception
            Throw ex
        End Try
    End Sub

    Public Sub StopZB()
        _EndThread = True
    End Sub


    Public Sub RestartZibaseSearch()
        RaiseEvent WriteMessage("Search for Zibase", MSG_DEBUG)
        _ZBS.BrowseForZibase()
    End Sub

    Public Function GetSensorInfo(ByVal sID As String, ByVal sType As String) As SensorInfo
        If (_SensorList.Contains(sID & sType)) Then
            GetSensorInfo = _SensorList.Item(sID & sType)
        Else
            GetSensorInfo = Nothing
        End If

    End Function

    Public Sub SetServerPort(ByVal Port As Integer)
        _Server.Port = Port
        _ZBS.SetServerPort(Port)
    End Sub

    Public Sub AddZibase(ByVal sZibaseIP As String, ByVal sLocalIP As String)
        Dim sNewZB As String

        sNewZB = _ZBS.InitZapi(sZibaseIP, sLocalIP)

        If (sNewZB <> "") Then
            Dim IpAddr As IPAddress

            IpAddr = IPAddress.Parse(sZibaseIP)
            Dim temp() As Byte = IpAddr.GetAddressBytes()
            Array.Reverse(temp)

            AddZibaseToCollection(sNewZB, BitConverter.ToUInt32(temp, 0))
        End If
    End Sub

    Private Sub ThreadSearch()

        ' Effectue une activation de l'api Zibase sur toute les Zibases du réseau
        Raise(WriteMessageEvent, New Object() {"Search for Zibase", MSG_DEBUG})
        _ZBS.BrowseForZibase()
    End Sub

    Private Sub ThreadZibase()
        Dim retstat As Integer
        Dim Sck As Sockets.Socket = Nothing
        Dim rBuff() As Byte

        Try
            Raise(WriteMessageEvent, New Object() {"Start running thread", MSG_DEBUG})

            Thread.Sleep(1000)

            Sck = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

            If (_Server.Port = 0) Then
                _Server.Port = 17100
            End If

            For i = 0 To 50
                Try
                    Sck.Bind(_Server)

                    Exit For
                Catch ex As Exception
                    ' Exception indiquant un port déjà utilisé
                    If CType(ex, SocketException).SocketErrorCode = SocketError.AddressAlreadyInUse Then
                        Raise(WriteMessageEvent, New Object() {"IP Address and Port already in use. Try next port : " & (_Server.Port + 1), MSG_DEBUG})

                    End If

                    _Server.Port = _Server.Port + 1
                End Try
            Next

            _ZBS.SetServerPort(_Server.Port)

            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 200)

            If (_AutoSearch) Then
                _ThreadSearch.Start()
            End If

            While Not _EndThread
                Try
                    If (Sck.Available > 0) Then
                        ReDim rBuff(Sck.Available)
                        retstat = Sck.Receive(rBuff)

                        AfterReceive(rBuff)

                    End If
                Catch ex As Exception
                End Try

                Thread.Sleep(5)
            End While

        Catch ex As Exception
            'General Exception received--add code here to analyze the exception. A messagebox would be one idea.
            retstat = -2
        End Try

        If Not Sck Is Nothing Then Sck.Close() 'Always close the socket when done.

    End Sub

    ' Permet d'extraire une valeur de la chaine renvoyé par la Zibase
    Private Function GetValue(ByVal sStr As String, ByVal sName As String) As String
        Dim iStart As Integer
        Dim iLen As Integer

        If sStr.Contains("<" & sName & ">") Then
            iStart = sStr.IndexOf("<" & sName & ">") + Len("<" & sName & ">")
            iLen = sStr.IndexOf("</" & sName & ">") - iStart
            GetValue = sStr.Substring(iStart, iLen)
        Else
            GetValue = ""
        End If
    End Function

    Private Function GetLabelType(ByVal type As String) As String
        If type = "bat" Then
            GetLabelType = "Battery"
        ElseIf type = "lev" Then
            GetLabelType = "Recpt. Level"
        ElseIf type = "tem" Then
            GetLabelType = "Temperature"
        ElseIf type = "hum" Then
            GetLabelType = "Humidity"
        ElseIf type = "lnk" Then
            GetLabelType = "Link"
        ElseIf type = "sta" Then
            GetLabelType = "Status switch"
        ElseIf type = "temc" Then
            GetLabelType = "Temperature Setpoint"
        ElseIf type = "kwh" Then
            GetLabelType = "Total Energy"
        ElseIf type = "kw" Then
            GetLabelType = "Energy"
        ElseIf type = "tra" Then
            GetLabelType = "Total Rain"
        ElseIf type = "cra" Then
            GetLabelType = "Current Rain"
        ElseIf type = "awi" Then
            GetLabelType = "Average Wind"
        ElseIf type = "drt" Then
            GetLabelType = "Wind Direction"
        ElseIf type = "uvl" Then
            GetLabelType = "UV Level"
        Else
            GetLabelType = "Unknown"
        End If

    End Function


    Private Sub AddZibaseToCollection(ByVal sZibaseName As String, ByVal lIpAddress As Long)
        Dim seInfo As New SensorInfo

        If (sZibaseName = "") Then
            sZibaseName = "Unknown"
        End If

        If (Not _ZibaseList.Contains(sZibaseName)) Then
            Dim zb As New ZibaseInfo
            zb.sLabelBase = sZibaseName
            zb.lIpAddress = lIpAddress
            _ZibaseList.Add(sZibaseName, zb)

            Raise(NewZibaseDetectedEvent, New Object() {zb})
            Raise(WriteMessageEvent, New Object() {"New Zibase Detected : " & sZibaseName, MSG_INFO})

            ' Creation d'un sensor virtuel pour la detection de l'availability de la Zibase
            seInfo.sName = "Zibase State"
            seInfo.sID = sZibaseName
            seInfo.sType = "lnk"
            seInfo.sValue = "Online"
            seInfo.sHTMLValue = "Online"
            seInfo.dwValue = 2

            If (_SensorList.Contains(sZibaseName & "lnk")) Then
                seInfo.sHSName = _SensorList.Item(sZibaseName & "lnk").sHSName
                seInfo.sDevice = _SensorList.Item(sZibaseName & "lnk").sDevice

                _SensorList.Item(sZibaseName & "lnk") = seInfo
            Else
                _SensorList.Add(sZibaseName & "lnk", seInfo)

                Raise(NewSensorDetectedEvent, New Object() {seInfo})
            End If

            Raise(UpdateSensorInfoEvent, New Object() {seInfo})
        End If

    End Sub

    ' Traitement des messages envoyés dans 
    Private Sub AfterReceive(ByVal _data() As Byte)
        Dim s As String
        Dim i As Integer
        Dim sZibaseName As String
        Dim seInfo As New SensorInfo

        If _data.Length >= 70 And _data(5) = 3 Then

            ' On récupére d'abord les info générales sur le message
            _ZBS.SetData(_data)

            sZibaseName = System.Text.Encoding.Default.GetString(_ZBS.label_base)
            Dim iPos As Integer = InStr(sZibaseName, Chr(0))
            If iPos > 0 Then sZibaseName = Left(sZibaseName, iPos - 1)

            AddZibaseToCollection(sZibaseName, _ZBS.my_ip)

            s = ""
            For i = 70 To _data.Length - 2
                s = s + Chr(_data(i))
            Next

            Raise(WriteMessageEvent, New Object() {sZibaseName & ":" & s, MSG_DEBUG})

            Dim sId As String
            Dim sValue As String
            Dim sType As String

            If (s.Substring(0, 17).ToUpper() = "RECEIVED RADIO ID") Then

                seInfo.sID = GetValue(s, "id")
                seInfo.sName = GetValue(s, "dev")
                seInfo.sDate = Date.Now


                If (Asc(seInfo.sID(0)) >= Asc("A") And Asc(seInfo.sID(0)) <= Asc("P") _
                    And (IsNumeric(seInfo.sID.Substring(1)) Or IsNumeric(seInfo.sID.Substring(1).Replace("_OFF", "")))) Then

                    seInfo.sName = "Remote Control"

                    ' Traitement de la donnée reçu
                    ' On parcours la liste des passerelles
                    For i = 0 To 15
                        seInfo.sDevice = Chr(65 + i) & seInfo.sID.Substring(1)
                        seInfo.sValue = ""

                        If seInfo.sID.IndexOf("_OFF") = -1 Then
                            seInfo.dwValue = 2
                            seInfo.sValue = "On"
                        Else
                            seInfo.dwValue = 3
                            seInfo.sValue = "Off"
                        End If

                        seInfo.sID = seInfo.sID.Replace("_OFF", "")

                        Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                    Next

                    ' On remet l'id d'origine version Zibase
                    ' seInfo.sID = GetValue(s, "id").Replace("_OFF", "")

                End If

                ' On modifie l'id pour Chacon et Visonic pour qui correspondent à l'actionneur (ON et OFF)
                If (seInfo.sID.Substring(0, 2) = "CS") Then
                    seInfo.sID = "CS" & ((seInfo.sID.Substring(2)) And Not &H10)
                    seInfo.sName = "Chacon"
                End If

                If (seInfo.sID.Substring(0, 2) = "VS") Then
                    seInfo.sID = "VS" & ((seInfo.sID.Substring(2)) And Not &HF)
                    seInfo.sName = "Visonic"
                End If

                If (seInfo.sID.Substring(0, 2) = "DX") Then
                    seInfo.sID = "DX" & seInfo.sID.Substring(2)
                    seInfo.sName = "X2D"
                End If

                If (seInfo.sID.Substring(0, 2) = "WS") Then
                    seInfo.sID = "WS" & ((seInfo.sID.Substring(2)) And Not &HF)
                    seInfo.sName = "OWL"
                End If

                If (seInfo.sID.Substring(0, 2) = "XS") Then
                    sValue = (seInfo.sID.Substring(2)) And &HFF
                    seInfo.sID = "XS" & ((seInfo.sID.Substring(2)) And Not &HFF)
                    seInfo.sName = "X10 Secured"

                    sType = "xse"
                    seInfo.sType = sType
                    seInfo.sDevice = ""

                    ' Declaration d'une variable de type état
                    If (sValue <> "") Then
                        seInfo.dwValue = CInt(sValue)
                        Select Case seInfo.dwValue
                            Case &H20, &H30
                                seInfo.sValue = "ALERT"
                                seInfo.sHTMLValue = "ALERT"
                            Case &H21, &H31
                                seInfo.sValue = "NORMAL"
                                seInfo.sHTMLValue = "NORMAL"
                            Case &H40
                                seInfo.sValue = "ARM AWAY (max)"
                                seInfo.sHTMLValue = "ARM AWAY (max)"
                            Case &H41, &H61
                                seInfo.sValue = "DISARM"
                                seInfo.sHTMLValue = "DISARM"
                            Case &H42
                                seInfo.sValue = "SEC. LIGHT ON"
                                seInfo.sHTMLValue = "SEC. LIGHT ON"
                            Case &H43
                                seInfo.sValue = "SEC. LIGHT OFF"
                                seInfo.sHTMLValue = "SEC. LIGHT OFF"
                            Case &H44
                                seInfo.sValue = "PANIC"
                                seInfo.sHTMLValue = "PANIC"
                            Case &H50
                                seInfo.sValue = "ARM HOME"
                                seInfo.sHTMLValue = "ARM HOME"
                            Case &H60
                                seInfo.sValue = "ARM"
                                seInfo.sHTMLValue = "ARM"
                            Case &H62
                                seInfo.sValue = "LIGHTS ON"
                                seInfo.sHTMLValue = "LIGHTS ON"
                            Case &H63
                                seInfo.sValue = "LIGHTS OFF"
                                seInfo.sHTMLValue = "LIGHTS OFF"
                            Case &H70
                                seInfo.sValue = "ARM HOME (min)"
                                seInfo.sHTMLValue = "ARM HOME (min)"
                        End Select

                        sId = seInfo.sID

                        If (_SensorList.Contains(sId & sType)) Then
                            seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                            seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                            _SensorList.Item(sId & sType) = seInfo
                        Else
                            _SensorList.Add(sId & sType, seInfo)
                        End If

                        Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                    End If
                End If

                sId = seInfo.sID

                sType = "sta"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type état
                If (sValue <> "") Then
                    seInfo.sValue = sValue
                    seInfo.sHTMLValue = sValue

                    If sValue = "ON" Then
                        seInfo.dwValue = 2
                    Else
                        seInfo.dwValue = 3
                    End If

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "lev"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type strength level
                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue)
                    seInfo.sValue = seInfo.dwValue & "/5"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If


                sType = "temc"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type consigne de température
                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue)
                    seInfo.sValue = seInfo.dwValue & "°C"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If


                sType = "kwh"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CLng(CDbl(sValue) * 100)
                    seInfo.sValue = seInfo.dwValue & " kWh"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "kw"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CLng(CDbl(sValue) * 100)
                    seInfo.sValue = seInfo.dwValue & " kW"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "tra"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue) * 100
                    seInfo.sValue = seInfo.dwValue & " mm"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "cra"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue) * 100
                    seInfo.sValue = seInfo.dwValue & " mm/h"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "awi"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CLng(CDbl(sValue) * 100)
                    seInfo.sValue = seInfo.dwValue & " m/s"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "drt"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue) * 100
                    seInfo.sValue = seInfo.dwValue & " °"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "uvl"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                If (sValue <> "") Then
                    seInfo.dwValue = CInt(sValue) * 100
                    seInfo.sValue = seInfo.dwValue & ""
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If


                sType = "bat"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type battery
                If (sValue <> "" And sValue <> "?") Then
                    seInfo.sValue = sValue
                    seInfo.sHTMLValue = sValue

                    If sValue = "Low" Then
                        seInfo.dwValue = 0
                    Else
                        seInfo.dwValue = 1
                    End If

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If


                ' Gestion du type de sonde
                sType = "tem"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type temperature
                If (sValue <> "") Then
                    seInfo.dwValue = CLng(Val(sValue) * 100)
                    seInfo.sValue = Format(seInfo.dwValue / 100, "#.#") & "°C"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

                sType = "hum"
                seInfo.sType = sType
                seInfo.sDevice = ""
                sValue = GetValue(s, sType)

                ' Declaration d'une variable de type humidity
                If (sValue <> "") Then
                    seInfo.dwValue = CInt(Val(sValue))
                    seInfo.sValue = seInfo.dwValue & "%"
                    seInfo.sHTMLValue = sValue

                    If (_SensorList.Contains(sId & sType)) Then
                        seInfo.sHSName = _SensorList.Item(sId & sType).sHSName
                        seInfo.sDevice = _SensorList.Item(sId & sType).sDevice

                        _SensorList.Item(sId & sType) = seInfo
                    Else
                        _SensorList.Add(sId & sType, seInfo)

                        Raise(NewSensorDetectedEvent, New Object() {seInfo})
                    End If

                    Raise(UpdateSensorInfoEvent, New Object() {seInfo})
                End If

            End If
        End If
    End Sub

    Public Sub SendCommand(ByVal sAddress As String, ByVal iState As State, Optional ByVal iDim As Integer = 0, Optional ByVal iProtocol As Protocol = Protocol.PROTOCOL_CHACON, Optional ByVal iNbBurst As Integer = 1)
        SendCommand("", sAddress, iState, iDim, iProtocol, iNbBurst)
    End Sub

    Public Sub SendCommand(ByVal sZibaseName As String, ByVal sAddress As String, ByVal iState As State, Optional ByVal iDim As Integer = 0, Optional ByVal iProtocol As Protocol = Protocol.PROTOCOL_CHACON, Optional ByVal iNbBurst As Integer = 1)
        If (Len(sAddress) < 2) Then Exit Sub

        Dim ZBS As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("SendX10")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0
        ZBS.param1 = 0

        If iState = State.STATE_DIM And iDim = 0 Then
            iState = State.STATE_OFF
        End If

        Select Case iState
            Case State.STATE_OFF
                ZBS.param2 = 0
            Case State.STATE_ON
                ZBS.param2 = 1
            Case State.STATE_DIM
                ZBS.param2 = 3
        End Select

        ' DEFAULT BROADCAST (RF X10, CHACON, DOMIA) : 0
        ' VISONIC433:   1,          ( frequency : device RF LOW, 310...418Mhz band))
        ' VISONIC868:   2,          (  frequency :  device RF HIGH, 868 Mhz Band)
        ' CHACON (32B) (ChaconV2) :  3
        ' DOMIA (24B) ( =Chacon V1 + low cost shit-devices):    4
        ' RF X10 :    5
        ZBS.param2 = ZBS.param2 Or ((iProtocol And &HFF) << 8)

        ' Dim
        If (iState = State.STATE_DIM) Then
            ZBS.param2 = ZBS.param2 Or ((iDim And &HFF) << 16)
        End If

        If (iNbBurst <> 1) Then
            ZBS.param2 = ZBS.param2 Or ((iNbBurst And &HFF) << 24)
        End If

        Dim sHouse As String = Mid(sAddress, 1, 1)
        Dim sCode As String = Mid(sAddress, 2)

        ZBS.param3 = Val(sCode) - 1
        ZBS.param4 = Asc(sHouse) - 65

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)
            End If
        Next
    End Sub

    Public Sub ExecScript(ByVal sScript As String)
        ExecScript("", sScript)
    End Sub

    Public Sub RunScenario(ByVal sZibaseName As String, ByVal sName As String)
        ExecScript(sZibaseName, "lm [" & sName & "]")
    End Sub

    Public Sub RunScenario(ByVal sName As String)
        RunScenario("", sName)
    End Sub

    Public Sub RunScenario(ByVal sZibaseName As String, ByVal iNum As Integer)
        ExecScript(sZibaseName, "lm " & iNum)
    End Sub

    Public Sub RunScenario(ByVal iNum As Integer)
        RunScenario("", iNum)
    End Sub

    Public Sub ExecScript(ByVal sZibaseName As String, ByVal sScript As String)
        sScript = "cmd:" & sScript

        If (Len(sScript) > 96) Then Exit Sub

        Dim ZBS As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 16
        ZBS.alphacommand = ZBS.GetBytesFromString("SendCmd")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.command_text = ZBS.GetBytesFromString(sScript)

        ZBS.serial = 0
        ZBS.param1 = 0
        ZBS.param2 = 0
        ZBS.param3 = 0
        ZBS.param4 = 0

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)
            End If
        Next

    End Sub


    Public Function GetVar(ByVal dwNumVar As UInt32) As UInt32
        GetVar = GetVar("", dwNumVar)
    End Function

    Public Function GetVar(ByVal sZibaseName As String, ByVal dwNumVar As UInt32) As UInt32
        Dim ZBS As New ZBClass
        Dim ZBSrcv As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("GetVar")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 5
        ZBS.param2 = 0
        ZBS.param3 = CMD_READ_VAR
        ZBS.param4 = dwNumVar

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

                ZBSrcv.SetData(dataRcv)

                GetVar = ZBSrcv.param1

                Exit For
            End If
        Next
    End Function

    Public Function GetX10State(ByVal house As Char, ByVal unit As Byte) As Boolean
        GetX10State = GetX10State("", house, unit)
    End Function

    Public Function GetX10State(ByVal sZibaseName As String, ByVal house As Char, ByVal unit As Byte) As Boolean
        Dim ZBS As New ZBClass
        Dim ZBSrcv As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("GetX10")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 5
        ZBS.param2 = 0
        ZBS.param3 = CMD_READ_X10
        ZBS.param4 = ((Asc(house) - Asc("A")) << 8) Or unit

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

                ZBSrcv.SetData(dataRcv)

                GetX10State = (ZBSrcv.param1 = 1)

                Exit For
            End If
        Next
    End Function


    Public Function GetCalendar(ByVal dwNumCal As UInt32) As UInt32
        GetCalendar = GetVar("", dwNumCal)
    End Function

    Public Function GetCalendar(ByVal sZibaseName As String, ByVal dwNumCal As UInt32) As UInt32
        Dim ZBS As New ZBClass
        Dim ZBSrcv As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("GetCal")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 5
        ZBS.param2 = 0
        ZBS.param3 = CMD_READ_CAL
        ZBS.param4 = dwNumCal

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

                ZBSrcv.SetData(dataRcv)

                GetCalendar = ZBSrcv.param1

                Exit For
            End If
        Next
    End Function

    Public Sub SetVar(ByVal dwNumVar As UInt32, ByVal dwVal As UInt32)
        SetVar("", dwNumVar, dwVal)
    End Sub

    Public Sub SetVar(ByVal sZibaseName As String, ByVal dwNumVar As UInt32, ByVal dwVal As UInt32)
        Dim ZBS As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("SetVar")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 5
        ZBS.param2 = dwVal
        ZBS.param3 = CMD_TYPE_WRITE_VAR
        ZBS.param4 = dwNumVar

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

            End If
        Next
    End Sub

    Public Sub SetCalendar(ByVal dwNumCal As UInt32, ByVal dwVal As UInt32)
        SetCalendar("", dwNumCal, dwVal)
    End Sub

    Public Sub SetCalendar(ByVal sZibaseName As String, ByVal dwNumCal As UInt32, ByVal dwVal As UInt32)
        Dim ZBS As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("SetCal")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 5
        ZBS.param2 = dwVal
        ZBS.param3 = CMD_WRITE_CAL
        ZBS.param4 = dwNumCal

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

            End If
        Next
    End Sub

    Public Function GetCalendarAsString(ByVal dwNumCal As UInt32) As String
        GetCalendarAsString = GetCalendarAsString("", dwNumCal)
    End Function

    Public Function GetCalendarAsString(ByVal sZibaseName As String, ByVal dwNumCal As UInt32) As String
        Dim val As UInt32
        Dim sHour As String
        Dim sDay As String

        val = GetCalendar(sZibaseName, dwNumCal)

        sHour = ""
        sDay = ""

        For i = 0 To 30
            If (i <= 23) Then
                If (val And 1) Then
                    sHour = sHour & "1"
                Else
                    sHour = sHour & "0"
                End If
            Else
                If (val And 1) Then
                    sDay = sDay & "1"
                Else
                    sDay = sDay & "0"
                End If
            End If

            val = val >> 1
        Next

        GetCalendarAsString = sDay & ";" & sHour
    End Function

    Public Function GetCalendarFromString(ByVal sDay As String, ByVal sHour As String) As UInt32
        Dim val As UInt32

        ' On compléte les variables pour être sur du nombre de donneés
        sDay = sDay & "0000000"
        sHour = sHour & "000000000000000000000000"

        val = 0

        For i = 6 To 0 Step -1
            If (sDay.ElementAt(i) = "1") Then val = val Or 1
            val = val << 1
        Next
        For i = 23 To 0 Step -1
            If (sHour.ElementAt(i) = "1") Then val = val Or 1
            If (i <> 0) Then val = val << 1
        Next

        GetCalendarFromString = val
    End Function

    Public Sub SetVirtualProbeValue(ByVal dwSensorID As UInt32, ByVal SensorType As VirtualProbeType, ByVal dwValue1 As UInt32, ByVal dwValue2 As UInt32, ByVal dwLowBat As UInt32)
        SetVirtualProbeValue("", dwSensorID, SensorType, dwValue1, dwValue2, dwLowBat)
    End Sub

    Public Sub SetVirtualProbeValue(ByVal sZibaseName As String, ByVal wSensorID As UInt16, ByVal SensorType As VirtualProbeType, ByVal dwValue1 As UInt32, ByVal dwValue2 As UInt32, ByVal dwLowBat As UInt32)
        Dim ZBS As New ZBClass
        Dim ZBSrcv As New ZBClass
        Dim iSensorType As Integer
        Dim dwSensorID As UInt32

        Select Case SensorType
            ' Simule un OWL
            Case VirtualProbeType.POWER_SENSOR
                iSensorType = DOMO_EVENT_ACTION_OWL_SIGNAL_32B_SENSOR_CODE
                dwSensorID = &H2 << 16 Or wSensorID

                ' Simule une THGR228
            Case VirtualProbeType.TEMP_HUM_SENSOR
                iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE
                dwSensorID = (&H1A2D << 16) Or wSensorID

                ' Simule une THN132
            Case VirtualProbeType.TEMP_SENSOR
                iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE
                dwSensorID = (&H1 << 16) Or wSensorID

                ' Simule un pluviometre
            Case VirtualProbeType.WATER_SENSOR
                iSensorType = DOMO_EVENT_ACTION_OREGON_SIGNAL_32B_SENSOR_CODE
                dwSensorID = (&H2A19 << 16) Or wSensorID
        End Select


        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("VProbe")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 6
        ZBS.param2 = dwSensorID
        ZBS.param3 = dwValue1 Or (dwValue2 << 16) Or (dwLowBat << 24)
        ZBS.param4 = iSensorType

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)
            End If
        Next
    End Sub

    Public Sub SetPlatform(ByVal dwPlatform As UInt32, ByVal dwPasswordIn As UInt32, ByVal dwPasswordOut As UInt32)
        SetPlatform("", dwPlatform, dwPasswordIn, dwPasswordOut)
    End Sub

    Public Sub SetPlatform(ByVal sZibaseName As String, ByVal dwPlatform As UInt32, ByVal dwPasswordIn As UInt32, ByVal dwPasswordOut As UInt32)
        Dim ZBS As New ZBClass

        ZBS.header = ZBS.GetBytesFromString("ZSIG")
        ZBS.command = 11
        ZBS.alphacommand = ZBS.GetBytesFromString("SetPlatform")
        ZBS.label_base = ZBS.GetBytesFromString("")

        ZBS.serial = 0

        ZBS.param1 = 7
        ZBS.param2 = dwPasswordIn
        ZBS.param3 = dwPasswordOut
        ZBS.param4 = dwPlatform

        Dim dataRcv() As Byte = Nothing
        Dim dataSnd() As Byte = ZBS.GetBytes()

        Dim zbe As ZibaseInfo
        Dim item As Object
        Dim sAddr As String

        For Each item In _ZibaseList
            zbe = item.value
            If (zbe.sLabelBase = sZibaseName Or sZibaseName = "") Then
                sAddr = BitConverter.GetBytes(zbe.lIpAddress)(3) & "." & BitConverter.GetBytes(zbe.lIpAddress)(2) & "." & BitConverter.GetBytes(zbe.lIpAddress)(1) & "." & BitConverter.GetBytes(zbe.lIpAddress)(0)
                _ZBS.UDPDataTransmit(dataSnd, dataRcv, sAddr, 49999)

            End If
        Next
    End Sub


    ' Permet d'associer un token à une zibase. Ce token sera ensuite utilisé pour récupérer des données depuis la plateforme zodianet (liste des scénarios par exemple)
    Public Sub SetZibaseToken(ByVal sZibaseName As String, ByVal sToken As String)
        If (_ZibaseList.Contains(sZibaseName)) Then
            Dim zb As New ZibaseInfo

            zb = _ZibaseList.Item(sZibaseName)
            zb.sToken = sToken
            _ZibaseList.Item(sZibaseName) = zb
        End If

    End Sub

    Public Function GetScenarioList(ByVal sZibaseName As String) As String
        If (_ZibaseList.Contains(sZibaseName)) Then
            Dim zb As New ZibaseInfo

            zb = _ZibaseList.Item(sZibaseName)

            If (zb.sToken Is Nothing) Then
                GetScenarioList = "Token must be defined"
            Else
                ' On charge la liste des scènarios depuis la plateforme zodianet
                Dim xDoc As XmlDocument = New XmlDocument()

                xDoc.Load("http://www.zibase.net/m/get_xml.php?device=" & sZibaseName & "&token=" & zb.sToken)

                Dim scenario As XmlNodeList = xDoc.GetElementsByTagName("m")
                Dim sSceList As String

                sSceList = ""

                If (Not scenario Is Nothing) Then
                    Dim s As String
                    Dim sce_name As String

                    For Each node In scenario
                        s = node.Attributes.GetNamedItem("id").Value
                        sce_name = node.ChildNodes.Item(0).InnerText

                        If (sSceList <> "") Then sSceList = sSceList & "|"
                        sSceList = sSceList & sce_name & ";" & s
                    Next

                    GetScenarioList = sSceList
                Else
                    GetScenarioList = "Impossible to get list from Zodianet Platform"

                End If
            End If
        Else
            GetScenarioList = "Zibase not found"
        End If

    End Function

    Public Function GetDevicesList(ByVal sZibaseName As String) As String
        If (_ZibaseList.Contains(sZibaseName)) Then
            Dim zb As New ZibaseInfo

            zb = _ZibaseList.Item(sZibaseName)

            If (zb.sToken Is Nothing) Then
                GetDevicesList = "Token must be defined"
            Else
                ' On charge la liste des scènarios depuis la plateforme zodianet
                Dim xDoc As XmlDocument = New XmlDocument()

                xDoc.Load("http://www.zibase.net/m/get_xml.php?device=" & sZibaseName & "&token=" & zb.sToken)

                Dim sensors As XmlNodeList = xDoc.GetElementsByTagName("e")
                Dim sSensorsList As String

                sSensorsList = ""

                If (Not sensors Is Nothing) Then
                    Dim stype As String
                    Dim sid As String
                    Dim sce_name As String

                    For Each node In sensors
                        stype = node.Attributes.GetNamedItem("t").Value
                        sid = node.Attributes.GetNamedItem("c").Value
                        sce_name = node.ChildNodes.Item(0).InnerText

                        If (sSensorsList <> "") Then sSensorsList = sSensorsList & "|"
                        sSensorsList = sSensorsList & sce_name & ";" & stype & ";" & sid
                    Next

                    GetDevicesList = sSensorsList
                Else
                    GetDevicesList = "Impossible to get list from Zodianet Platform"

                End If
            End If
        Else
            GetDevicesList = "Zibase not found"
        End If

    End Function

End Class
