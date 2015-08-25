Imports System.Media
Imports System.IO
Imports System.Net
Imports ZibaseDll

Public Class frmMain
    Dim WithEvents zba As ZiBase = New ZiBase

    Private Sub PlayTTS(ByVal s As String)
        Dim i As Integer

        ' On regarde les données à remplacer (variable)
        For i = 0 To 19
            If s.IndexOf("%V" & i & "%") >= 0 Then
                s = s.Replace("%V" & i & "%", zba.GetVar(i))
            End If
            If s.IndexOf("%V" & i & "#%") >= 0 Then
                s = s.Replace("%V" & i & "#%", zba.GetVar(i) / 10)
            End If
        Next

        AxVLCPlugin21.playlist.clear()
        i = AxVLCPlugin21.playlist.add("http://translate.google.com/translate_tts?tl=fr&ie=UTF-8&q=" & s)
        AxVLCPlugin21.playlist.playItem(i)
    End Sub


    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        PlayTTS(TextBox1.Text)
    End Sub
    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        PlayTTS(TextBox2.Text)
    End Sub
    Private Sub Button3_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button3.Click
        PlayTTS(TextBox3.Text)
    End Sub
    Private Sub Button4_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button4.Click
        PlayTTS(TextBox4.Text)
    End Sub
    Private Sub Button5_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button5.Click
        PlayTTS(TextBox5.Text)
    End Sub
    Private Sub Button6_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button6.Click
        PlayTTS(TextBox6.Text)
    End Sub
    Private Sub Button7_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button7.Click
        PlayTTS(TextBox7.Text)
    End Sub
    Private Sub Button8_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button8.Click
        PlayTTS(TextBox8.Text)
    End Sub
    Private Sub Button9_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button9.Click
        PlayTTS(TextBox9.Text)
    End Sub
    Private Sub Button10_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button10.Click
        PlayTTS(TextBox10.Text)
    End Sub
    Private Sub Button11_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button11.Click
        PlayTTS(TextBox11.Text)
    End Sub
    Private Sub Button12_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button12.Click
        PlayTTS(TextBox12.Text)
    End Sub
    Private Sub Button13_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button13.Click
        PlayTTS(TextBox13.Text)
    End Sub
    Private Sub Button14_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button14.Click
        PlayTTS(TextBox14.Text)
    End Sub
    Private Sub Button15_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button15.Click
        PlayTTS(TextBox15.Text)
    End Sub

    Private Sub frmMain_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Disposed
        zba.StopZB()

    End Sub

    Private Sub frmMain_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        For i = 0 To 19
            ComboVar.Items.Add("V" & i)
        Next
        ComboVar.SelectedIndex = 0

        zba.StartZB(True)
    End Sub

    Private Sub Timer1_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Timer1.Tick
        Dim val As Int32

        val = zba.GetVar(ComboVar.SelectedIndex)

        If (val <> 0) Then
            Select Case (val)
                Case 1
                    PlayTTS(TextBox1.Text)
                Case 2
                    PlayTTS(TextBox2.Text)
                Case 3
                    PlayTTS(TextBox3.Text)
                Case 4
                    PlayTTS(TextBox4.Text)
                Case 5
                    PlayTTS(TextBox5.Text)
                Case 6
                    PlayTTS(TextBox6.Text)
                Case 7
                    PlayTTS(TextBox7.Text)
                Case 8
                    PlayTTS(TextBox8.Text)
                Case 9
                    PlayTTS(TextBox9.Text)
                Case 10
                    PlayTTS(TextBox10.Text)
                Case 11
                    PlayTTS(TextBox11.Text)
                Case 12
                    PlayTTS(TextBox12.Text)
                Case 13
                    PlayTTS(TextBox13.Text)
                Case 14
                    PlayTTS(TextBox14.Text)
                Case 15
                    PlayTTS(TextBox15.Text)


            End Select

            zba.SetVar(ComboVar.SelectedIndex, 0)
        End If
    End Sub

    Private Sub zba_NewZibaseDetected(ByVal zbInfo As ZibaseDll.ZiBase.ZibaseInfo) Handles zba.NewZibaseDetected
        MsgBox("Zibase détecté : " + zbInfo.sLabelBase)
    End Sub
End Class
