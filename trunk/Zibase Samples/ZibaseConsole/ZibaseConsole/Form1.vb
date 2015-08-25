' La dll ZibaseDll.dll est développée par la société APITRONIC revendeur de la Zibase et de ses accessoires : http://www.planete-domotique.com
' L'utilisation de cette dll est totalement gratuite pour une utilisation personnelle et/ou commerciale.
' La distribution de la dll avec votre projet implique la livraison du fichier readme.txt contenant une présentation de la dll 
' ainsi qu'un lien vers notre boutique. 
Imports ZibaseDll

Public Class Form1
    Dim WithEvents zba As ZiBase = New ZiBase

    Private Sub btnExec_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnExec.Click
        zba.ExecScript(TextBox1.Text)
    End Sub

    Private Sub btnRun_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnRun.Click
        zba.RunScenario(TextBox2.Text)
    End Sub

    Private Sub zba_NewSensorDetected(ByVal seInfo As ZibaseDll.ZiBase.SensorInfo) Handles zba.NewSensorDetected
        ListBox2.Items.Add("NEW:" & seInfo.sID & " / " & seInfo.sType & " : " & seInfo.sValue)
        ListBox2.TopIndex = ListBox2.Items.Count - 1
    End Sub

    Private Sub zba_NewZibaseDetected(ByVal zbInfo As ZibaseDll.ZiBase.ZibaseInfo) Handles zba.NewZibaseDetected
        ListBox2.Items.Add("NZB:" & zbInfo.sLabelBase & " / " & zbInfo.lIpAddress)
        ListBox2.TopIndex = ListBox2.Items.Count - 1
    End Sub

    Private Sub zba_UpdateSensorInfo(ByVal seInfo As ZibaseDll.ZiBase.SensorInfo) Handles zba.UpdateSensorInfo
        ListBox2.Items.Add("UPD:" & seInfo.sID & " / " & seInfo.sType & " : " & seInfo.sValue)
        ListBox2.TopIndex = ListBox2.Items.Count - 1
    End Sub

    Private Sub zba_WriteMessage(ByVal sMsg As String, ByVal level As Integer) Handles zba.WriteMessage
        ListBox1.Items.Add(sMsg)
        ListBox1.TopIndex = ListBox1.Items.Count - 1
    End Sub

    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

        For i = 0 To 15
            cmbHouse.Items.Add(Chr(65 + i))
            cmbCode.Items.Add(i + 1)
        Next

        cmbProtocol.Items.Add("VISONIC433")
        cmbProtocol.Items.Add("CHACON V2")
        cmbProtocol.Items.Add("DOMIA (CHACON V1)")
        cmbProtocol.Items.Add("X10")
        cmbProtocol.Items.Add("RFS10")

        For i = 0 To 100
            cmbDim.Items.Add(i)
        Next

        cmbPlateform.Items.Add("Zibase.net")
        cmbPlateform.Items.Add("not used")
        cmbPlateform.Items.Add("Planete-zb.net")
        cmbPlateform.Items.Add("Domadoo-zb.net")
        cmbPlateform.Items.Add("Robopolis-zb.net")

        cmbDim.SelectedIndex = 0
        cmbProtocol.SelectedIndex = 1
        cmbHouse.SelectedIndex = 0
        cmbCode.SelectedIndex = 0
        cmbPlateform.SelectedIndex = 0

        For i = 0 To 19
            cmbVarList.Items.Add("V" & i)
        Next
        cmbVarList.SelectedIndex = 0


        For i = 1 To 16
            cmbCalendarList.Items.Add("Calendrier " & i)
        Next
        cmbCalendarList.SelectedIndex = 0

        zba.StartZB()
        '
        'zba.StartZB(False)
        'zba.AddZibase("192.168.1.44", "192.168.1.45")
    End Sub

    Private Sub form1_FormClosing(ByVal sender As System.Object, ByVal e As System.ComponentModel.CancelEventArgs) Handles Me.FormClosing
        zba.StopZB()
    End Sub

    Private Sub btnClear_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnClear.Click
        ListBox1.Items.Clear()
        ListBox2.Items.Clear()
    End Sub

    Private Function GetSelectedProtocol() As ZiBase.Protocol
        Select Case cmbProtocol.SelectedIndex
            Case 0
                GetSelectedProtocol = ZiBase.Protocol.PROTOCOL_VISONIC433
            Case 1
                GetSelectedProtocol = ZiBase.Protocol.PROTOCOL_CHACON
            Case 2
                GetSelectedProtocol = ZiBase.Protocol.PROTOCOL_DOMIA
            Case 3
                GetSelectedProtocol = ZiBase.Protocol.PROTOCOL_X10
            Case 4
                GetSelectedProtocol = ZiBase.Protocol.PROTOCOL_RFS10

        End Select
    End Function

    Private Sub btnON_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnON.Click
        zba.SendCommand(cmbHouse.SelectedItem & cmbCode.SelectedItem, ZiBase.State.STATE_ON, 0, GetSelectedProtocol(), 1)
    End Sub

    Private Sub btnOFF_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOFF.Click
        zba.SendCommand(cmbHouse.SelectedItem & cmbCode.SelectedItem, ZiBase.State.STATE_OFF, 0, GetSelectedProtocol(), 1)
    End Sub

    Private Sub btnDIM_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnDIM.Click
        zba.SendCommand(cmbHouse.SelectedItem & cmbCode.SelectedItem, ZiBase.State.STATE_DIM, cmbDim.SelectedIndex, GetSelectedProtocol(), 1)
    End Sub

    Private Sub btnRead_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnRead.Click
        Dim sei As ZiBase.SensorInfo

        sei = zba.GetSensorInfo(TextBox4.Text, TextBox3.Text)

        MsgBox("Valeur pour le capteur " & sei.sID & "(" & sei.sType & ") = " & sei.sValue)
    End Sub

    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        Close()
    End Sub

    Private Sub PictureBox1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles PictureBox1.Click
        Process.Start("http://www.zodianet.com")
    End Sub

    Private Sub PictureBox2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles PictureBox2.Click
        Process.Start("http://www.planete-domotique.com")
    End Sub

    Private Sub btnWriteVar_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnWriteVar.Click
        zba.SetVar(cmbVarList.SelectedIndex, TextBox6.Text)
    End Sub

    Private Sub btnReadVar_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnReadVar.Click
        TextBox6.Text = zba.GetVar(cmbVarList.SelectedIndex)
    End Sub

    Private Sub btnWriteCalendar_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnWriteCalendar.Click
        Dim sDay As String = ""
        Dim sHour As String = ""

        ' On construit les chaines correspondant au calendrier
        If (CheckBoxDay1.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay2.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay3.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay4.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay5.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay6.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"
        If (CheckBoxDay7.Checked) Then sDay = sDay & "1" Else sDay = sDay & "0"

        If (CheckBox1.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox2.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox3.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox4.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox5.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox6.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox7.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox8.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox9.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox10.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox11.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox12.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox13.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox14.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox15.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox16.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox17.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox18.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox19.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox20.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox21.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox22.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox23.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"
        If (CheckBox24.Checked) Then sHour = sHour & "1" Else sHour = sHour & "0"

        zba.SetCalendar(cmbVarList.SelectedIndex, zba.GetCalendarFromString(sDay, sHour))
    End Sub

    Private Sub btnReadCalendar_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnReadCalendar.Click
        Dim sCal As String
        Dim arr As String()

        Dim dw As Int32

        dw = zba.GetCalendarFromString("0110111", "011011000111101100101110")

        sCal = zba.GetCalendarAsString(cmbVarList.SelectedIndex)
        arr = Split(sCal, ";")

        ' Gestion des jours
        If Len(arr(0)) = 7 Then
            CheckBoxDay1.Checked = (arr(0)(0) = "1")
            CheckBoxDay2.Checked = (arr(0)(1) = "1")
            CheckBoxDay3.Checked = (arr(0)(2) = "1")
            CheckBoxDay4.Checked = (arr(0)(3) = "1")
            CheckBoxDay5.Checked = (arr(0)(4) = "1")
            CheckBoxDay6.Checked = (arr(0)(5) = "1")
            CheckBoxDay7.Checked = (arr(0)(6) = "1")
        End If

        ' Gestion des heures
        If Len(arr(1)) = 24 Then
            CheckBox1.Checked = (arr(1)(0) = "1")
            CheckBox2.Checked = (arr(1)(1) = "1")
            CheckBox3.Checked = (arr(1)(2) = "1")
            CheckBox4.Checked = (arr(1)(3) = "1")
            CheckBox5.Checked = (arr(1)(4) = "1")
            CheckBox6.Checked = (arr(1)(5) = "1")
            CheckBox7.Checked = (arr(1)(6) = "1")
            CheckBox8.Checked = (arr(1)(7) = "1")
            CheckBox9.Checked = (arr(1)(8) = "1")
            CheckBox10.Checked = (arr(1)(9) = "1")
            CheckBox11.Checked = (arr(1)(10) = "1")
            CheckBox12.Checked = (arr(1)(11) = "1")
            CheckBox13.Checked = (arr(1)(12) = "1")
            CheckBox14.Checked = (arr(1)(13) = "1")
            CheckBox15.Checked = (arr(1)(14) = "1")
            CheckBox16.Checked = (arr(1)(15) = "1")
            CheckBox17.Checked = (arr(1)(16) = "1")
            CheckBox18.Checked = (arr(1)(17) = "1")
            CheckBox19.Checked = (arr(1)(18) = "1")
            CheckBox20.Checked = (arr(1)(19) = "1")
            CheckBox21.Checked = (arr(1)(20) = "1")
            CheckBox22.Checked = (arr(1)(21) = "1")
            CheckBox23.Checked = (arr(1)(22) = "1")
            CheckBox24.Checked = (arr(1)(23) = "1")
        End If
    End Sub

    Private Sub btnTempHygro_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnTempHygro.Click
        zba.SetVirtualProbeValue(1, ZiBase.VirtualProbeType.TEMP_HUM_SENSOR, 120, 41, 0)
    End Sub

    Private Sub btnEnergie_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnEnergie.Click
        zba.SetVirtualProbeValue(1, ZiBase.VirtualProbeType.POWER_SENSOR, 850, 10, 0)
    End Sub

    Private Sub btnTemp_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnTemp.Click
        zba.SetVirtualProbeValue(1, ZiBase.VirtualProbeType.TEMP_SENSOR, 250, 0, 0)
    End Sub

    Private Sub btnWatter_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnWatter.Click
        zba.SetVirtualProbeValue(1, ZiBase.VirtualProbeType.WATER_SENSOR, 250, 50, 0)
    End Sub

    Private Sub btnPlateform_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnPlateform.Click
        Dim arr() As String
        Dim dwPIn As UInt32
        Dim dwPOut As UInt32
        arr = txtPlateformPwd.Text.Split("/")

        If (arr.Length = 2) Then
            dwPIn = Convert.ToUInt32(arr(0))
            dwPOut = Convert.ToUInt32(arr(1))
        Else
            dwPIn = Convert.ToUInt32(arr(0))
            dwPOut = 0
        End If

        zba.SetPlatform(cmbPlateform.SelectedIndex, dwPIn, dwPOut)
    End Sub
End Class
