using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

#if ENABLE_WINMD_SUPPORT
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.Foundation;
#else
using System.IO.Ports;
#endif

public abstract class GatewaySerial : MonoBehaviour
{
    public List<string> datas = new();

    public int BAUD = 115200;
    public int COM = 5;

    private Thread thread;


#if ENABLE_WINMD_SUPPORT
    private SerialDevice device;
#else
    private SerialPort stream;
#endif

    private void Start()
    {
        try
        {
            ConnectToDevice();
        }
        catch (SystemException e)
        {
            Debug.LogError("Couldn't find the correct serial port on COM" + COM + " BAUD " + BAUD);
            Debug.LogError(e);
        }
    }


    //public string data = "";
    private void Update()
    {
        /*if (Input.GetKeyDown(KeyCode.P))
        {
            dataSaver?.SaveAction(datas[0]);
            AnalyzeData(datas[0]);
            datas.RemoveAt(0);
        }
        return;*/
        while (datas.Count > 0)
        {
            AnalyzeData(datas[0]);
            datas.RemoveAt(0);
        }
    }

#if ENABLE_WINMD_SUPPORT
    private async void ConnectToDevice()
    {
        // We need to find the correct COM port
        string aqs = SerialDevice.GetDeviceSelector("COM" + COM);
        var dis = await DeviceInformation.FindAllAsync(aqs);
        device = await SerialDevice.FromIdAsync(dis[0].Id);
        device.BaudRate = (uint)BAUD;
        device.ReadTimeout = TimeSpan.FromMilliseconds(0);

        Debug.Log("Port found on COM" + COM + " BAUD " + BAUD);
        thread = new Thread(CollectData) { Name = "Thread COM " + COM + ", BAUD " + BAUD };
        thread.Start();

        // TO MAKE THIS WORK YOU NEED TO ADD THIS PIECE OF CODE IN THE BUILD
        // by adding 
        // <Capabilities>
        //   <DeviceCapability Name="serialcommunication"/>
        // </Capabilities>
        // in package.appxmanifest
    }
#else
    private void ConnectToDevice()
    {
        stream = new SerialPort("COM" + COM, BAUD);
        stream.Open();
        Debug.Log("Port found on COM" + COM + " BAUD " + BAUD);
        thread = new Thread(CollectData) { Name = "Thread COM " + COM + ", BAUD " + BAUD };
        thread.Start();
    }
#endif

#if ENABLE_WINMD_SUPPORT
    private async void CollectData()
    {
        DataReader stream = new DataReader(device.InputStream);
        stream.InputStreamOptions = InputStreamOptions.Partial;
        while(true) {
            String receivedString = "";
            char receivedSymbol = ' ';
            while (true)
            {
                try
                {
                    await stream.LoadAsync(1);
                    receivedSymbol = (char) stream.ReadByte();
                    if(receivedSymbol == '\n') 
                        break;
                    else receivedString += receivedSymbol;
                }
                catch {}
            }
            datas.Add(receivedString);
        }
    }
#else
    private void CollectData()
    {
        while (true)
        {
            String receivedString = "";
            char receivedSymbol;
            while ((receivedSymbol = (char)stream.ReadByte()) != '\n')
            {
                receivedString += receivedSymbol;
            }
            datas.Add(receivedString);
            //stream.DiscardOutBuffer();
            //stream.DiscardInBuffer();
        }
    }
#endif

    // Close the serial port when the application is closed
    private void OnDestroy()
    {
#if ENABLE_WINMD_SUPPORT
        device.Dispose();
#else
        stream.Close();
#endif
        if (thread != null)
            thread.Abort();
    }

    protected abstract void AnalyzeData(String receivedString);
}