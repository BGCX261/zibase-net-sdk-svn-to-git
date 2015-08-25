Imports CookComputing.XmlRpc
Imports ZibaseDll

Module ZBInterface
    Public WithEvents zba As ZiBase = New ZiBase

    Public Class ZibaseFuncServer
        Inherits MarshalByRefObject


        <XmlRpcMethod("SendCommand")> _
        Public Function SendCommand(ByVal sAddress As String, ByVal iState As Integer, ByVal iDim As Integer, ByVal iProtocol As Integer, ByVal iNbBurst As Integer) As Integer
            zba.SendCommand(sAddress, CType(iState, ZiBase.State), iDim, CType(iProtocol, ZiBase.Protocol), iNbBurst)
            SendCommand = 1
        End Function

        <XmlRpcMethod("RunScenario")> _
        Public Function RunScenario(ByVal sName As String) As Integer
            zba.RunScenario(sName)
            RunScenario = 1
        End Function

        <XmlRpcMethod("ExecScript")> _
        Public Function ExecScript(ByVal sName As String) As Integer
            zba.ExecScript(sName)
            ExecScript = 1
        End Function

        <XmlRpcMethod("GetVar")> _
        Public Function GetVar(ByVal dwNumVar As Integer) As Integer
            GetVar = zba.GetVar(dwNumVar)
        End Function

        <XmlRpcMethod("GetX10State")> _
        Public Function GetX10State(ByVal house As Char, ByVal unit As Byte) As Boolean
            GetX10State = zba.GetX10State(house, unit)
        End Function

        <XmlRpcMethod("SetVar")> _
        Public Function SetVar(ByVal dwNumVar As Integer, ByVal dwVal As Integer) As Integer
            zba.SetVar(dwNumVar, dwVal)
            SetVar = 1
        End Function

        <XmlRpcMethod("GetScenarioList")> _
        Public Function GetScenarioList(ByVal sZibaseName As String, ByVal sZibaseToken As String) As String
            zba.SetZibaseToken(sZibaseName, sZibaseToken)
            GetScenarioList = zba.GetScenarioList(sZibaseName)
        End Function

        <XmlRpcMethod("GetDevicesList")> _
        Public Function GetDevicesList(ByVal sZibaseName As String, ByVal sZibaseToken As String) As String
            zba.SetZibaseToken(sZibaseName, sZibaseToken)
            GetDevicesList = zba.GetDevicesList(sZibaseName)
        End Function

        <XmlRpcMethod("GetSensorVal")> _
        Public Function GetSensorVal(ByVal sSensor As String, ByVal sType As String, ByVal iDivider As Integer) As Double
            Dim seInfo As New ZiBase.SensorInfo

            seInfo = zba.GetSensorInfo(sSensor, sType)

            If iDivider <> 0 Then
                GetSensorVal = seInfo.dwValue / iDivider
            Else
                GetSensorVal = seInfo.dwValue
            End If


        End Function

    End Class
End Module
