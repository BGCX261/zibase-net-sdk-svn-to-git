Imports System
Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Channels.Http
Imports System.Runtime.Remoting.Channels

Imports CookComputing.XmlRpc

'Use only ONE of these Main methods.
Public Module LaunchApp

    Public Sub Main()
        'Turn visual styles back on
        Application.EnableVisualStyles()

        Dim props As IDictionary = New Hashtable()
        props("name") = "ZibaseHttpChannel"
        props("port") = 17110
        Dim channel As New HttpChannel(props, Nothing, New XmlRpcServerFormatterSinkProvider())

        ChannelServices.RegisterChannel(channel, False)

        'RemotingConfiguration.Configure("StateNameServer.exe.config", false);
        RemotingConfiguration.RegisterWellKnownServiceType(GetType(ZibaseFuncServer), "zibase", WellKnownObjectMode.Singleton)

        'Run the application using AppContext
        Application.Run(New Systray)
    End Sub

End Module
