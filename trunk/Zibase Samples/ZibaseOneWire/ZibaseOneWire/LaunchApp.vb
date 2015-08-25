Imports System
Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Channels

'Use only ONE of these Main methods.
Public Module LaunchApp

    Public Sub Main()
        'Turn visual styles back on
        Application.EnableVisualStyles()

        'Run the application using AppContext
        Application.Run(New Systray)
    End Sub

End Module
