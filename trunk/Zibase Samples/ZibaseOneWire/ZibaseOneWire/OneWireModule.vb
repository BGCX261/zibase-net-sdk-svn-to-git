Imports com.dalsemi.onewire
Imports System.Threading
Imports ZibaseDll

Public Class OneWireModule
    Dim bEndThread As Boolean
    Dim zb As New ZiBase

    Private Sub BackgroundProcess()
        Dim owd_enum As java.util.Enumeration
        Dim owd As com.dalsemi.onewire.container.OneWireContainer
        Dim tc As com.dalsemi.onewire.container.TemperatureContainer

        ' Creation de la variable associé à l'adaptateur 1Wire.
        ' Pour l'instant, c'est l'adaptateur par défaut.
        Dim adapter As com.dalsemi.onewire.adapter.DSPortAdapter
        Dim deviceFound As Boolean
        Dim OneWireAddress As String
        Dim state As Object
        Dim bAdapterExist As Boolean
        Dim sAdapterName As String
        Dim sExistingDevice As String
        Dim wSensorID As UInt16
        Dim LastSend As New Hashtable

        bEndThread = False

        Do While Not bEndThread

            ' Lecture des informations sur l'adaptateur
            Try
                adapter = com.dalsemi.onewire.OneWireAccessProvider.getDefaultAdapter
                bAdapterExist = adapter.adapterDetected
            Catch pEx As Exception
                sAdapterName = "Not detected"
                bAdapterExist = False
            End Try

            If (bAdapterExist) Then

                sAdapterName = adapter.getAdapterName & " (" & adapter.getAdapterVersion & ")"

                Try
                    adapter.beginExclusive(True)
                    adapter.setSearchAllDevices()
                    adapter.targetAllFamilies()
                    adapter.setSpeed(com.dalsemi.onewire.adapter.DSPortAdapter.SPEED_REGULAR)

                    ' Enumération de tous les devices du bus
                    owd_enum = adapter.getAllDeviceContainers
                    deviceFound = 0
                    sExistingDevice = ""

                    'On fait une première boucle qui contiendra tous les devices détectés
                    While owd_enum.hasMoreElements
                        owd = owd_enum.nextElement

                        sExistingDevice = sExistingDevice & owd.getName & ";" & owd.getAddressAsString & ";"
                    End While

                    ' Ensuite, on fait une lecture des périphériques de la liste des devices 

                    owd_enum = adapter.getAllDeviceContainers
                    While owd_enum.hasMoreElements
                        owd = owd_enum.nextElement

                        OneWireAddress = owd.getAddressAsString

                        Dim s As String = owd.getName

                        Try
                            ' Gestion des composants 1Wire du type Température (DS18B20 par exemple)
                            If TypeOf owd Is com.dalsemi.onewire.container.TemperatureContainer Then
                                deviceFound = 1

                                ' cast the OneWireContainer to TemperatureContainer
                                tc = DirectCast(owd, com.dalsemi.onewire.container.TemperatureContainer)

                                ' read the device
                                state = tc.readDevice
                                tc.doTemperatureConvert(state)

                                state = tc.readDevice
                                tc.doTemperatureConvert(state)

                                ' On transmet la valeur à la Zibase
                                wSensorID = Convert.ToUInt16(Mid(OneWireAddress, 1, 4), 16)

                                If (LastSend.Contains("K" & Hex(wSensorID))) Then
                                    Dim wCurDate As Date
                                    wCurDate = LastSend.Item("K" & Hex(wSensorID))
                                    Dim tsTimeSpan As TimeSpan
                                    tsTimeSpan = Now().Subtract(wCurDate)

                                    If (tsTimeSpan.TotalSeconds > 60) Then
                                        zb.SetVirtualProbeValue(wSensorID, ZiBase.VirtualProbeType.TEMP_HUM_SENSOR, Convert.ToInt16(tc.getTemperature(state) * 10), 0, 0)
                                        LastSend.Item("K" & Hex(wSensorID)) = Now()

                                    End If
                                Else
                                    LastSend.Add("K" & Hex(wSensorID), Now())
                                    zb.SetVirtualProbeValue(wSensorID, ZiBase.VirtualProbeType.TEMP_HUM_SENSOR, Convert.ToInt16(tc.getTemperature(state) * 10), 0, 0)
                                End If
                            End If
                        Catch pEx As Exception
                            MsgBox("Erreur lors de la lecture d'un module Temperature (" & OneWireAddress & ")")
                        End Try

                    End While
                    adapter.endExclusive()
                    adapter.freePort()
                Catch pEx As Exception
                    MsgBox("Erreur d'accès au contrôleur 1-Wire")
                End Try

            End If

            System.Threading.Thread.Sleep(100)
        Loop
    End Sub

    Public Sub StopOneWire()
        bEndThread = True

        zb.StopZB()

    End Sub

    Public Sub StartOneWire()
        zb.StartZB()

        Dim t As Thread
        t = New Thread(AddressOf Me.BackgroundProcess)
        t.Start()
    End Sub
End Class
