
Public Class Systray
    Inherits ApplicationContext

    Private WithEvents Tray As NotifyIcon
    Private WithEvents MainMenu As ContextMenuStrip
    Private WithEvents mnuDisplayForm As ToolStripMenuItem
    Private WithEvents mnuSep1 As ToolStripSeparator
    Private WithEvents mnuExit As ToolStripMenuItem

    Dim ow As New OneWireModule

    Public Sub New()
        'Initialize the menus
        mnuDisplayForm = New ToolStripMenuItem("A propos...")
        mnuSep1 = New ToolStripSeparator()
        mnuExit = New ToolStripMenuItem("Quitter")
        MainMenu = New ContextMenuStrip
        MainMenu.Items.AddRange(New ToolStripItem() {mnuDisplayForm, mnuSep1, mnuExit})

        'Initialize the tray
        Tray = New NotifyIcon
        Tray.Icon = My.Resources.ZBtray
        Tray.ContextMenuStrip = MainMenu
        Tray.Text = "Formless tray application"

        ' Démarrage de la gestion OneWire
        ow.StartOneWire()

        'Display
        Tray.Visible = True
    End Sub

    Private Sub Systray_ThreadExit(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles Me.ThreadExit
        'Guarantees that the icon will not linger.
        Tray.Visible = False
    End Sub

    Private Sub mnuDisplayForm_Click(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles mnuDisplayForm.Click
        ShowDialog()
    End Sub

    Private Sub mnuExit_Click(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles mnuExit.Click
        ExitApplication()
    End Sub

    Private Sub Tray_DoubleClick(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles Tray.DoubleClick
        ShowDialog()
    End Sub

    Private PF As frmMain

    Public Sub ExitApplication()
        'Perform any clean-up here
        'Then exit the application
        ow.StopOneWire()

        Application.Exit()
    End Sub

    Public Sub ShowDialog()
        If PF IsNot Nothing AndAlso Not PF.IsDisposed Then Exit Sub

        Dim CloseApp As Boolean = False

        PF = New frmMain
        PF.ShowDialog()
        CloseApp = (PF.DialogResult = DialogResult.Abort)
        PF = Nothing

        If CloseApp Then Application.Exit()
    End Sub

End Class
