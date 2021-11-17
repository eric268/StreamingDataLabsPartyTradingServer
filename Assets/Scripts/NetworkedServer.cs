using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    List<SharingRoom> sharingRooms;
   

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        sharingRooms = new List<SharingRoom>();

    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                PlayerDisconnected(recConnectionID);
                break;
        }

    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');
        int signifier =int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.JoinSharingRoom)
        {
            string roomName = csv[1];
            bool hasBeenFound = false;
            for (int i = 0; i < sharingRooms.Count;i++)
            {
                if (sharingRooms[i].name == roomName)
                {
                    hasBeenFound = true;
                    if (sharingRooms[i].connectionID[0] == id)
                    {
                        Debug.Log("Player already in room");
                        break;
                    }
                    sharingRooms[i].connectionID.Add(id);
                    Debug.Log("Room Joined");
                    break;
                }
            }
            if (!hasBeenFound)
            {
                Debug.Log("Room Created");
                sharingRooms.Add(new SharingRoom(roomName, id));
            }
        }
        else if (signifier == ClientToServerSignifiers.PartyDataSendTransferStart)
        {
            SharingRoom sr = FindSharingRoomWithConnectionID(id);
            sr.transferData = new List<string>();

        }
        else if (signifier == ClientToServerSignifiers.PartyDataTransfer)
        {
            SharingRoom sr = FindSharingRoomWithConnectionID(id);
            sr.transferData.Add(msg);
        }
        else if (signifier == ClientToServerSignifiers.PartyDataTransferEnd)
        {
            SharingRoom sr = FindSharingRoomWithConnectionID(id);
            int otherPlayerID = -1;
            if (sr.connectionID.Count > 1)
            {
                otherPlayerID = sr.connectionID[1];
            }
            else 
                return;

            SendMessageToClient(ServerToClientSignifiers.ServerSendPartyDataTransferStart + "", otherPlayerID);
            foreach (string d in sr.transferData)
            {
                SendMessageToClient(ServerToClientSignifiers.ServerSendingPartyData + "," + d, otherPlayerID);
            }
            SendMessageToClient(ServerToClientSignifiers.ServerSendPartyDataTransferEnd + "", otherPlayerID);
        }
    }

    public void PlayerDisconnected(int id)
    {
        SharingRoom foundSR = FindSharingRoomWithConnectionID(id);

        if (foundSR != null)
        {
            foundSR.connectionID.Remove(id);
            Debug.Log("Removing player from room");
            if (foundSR.connectionID.Count == 0)
            {
                sharingRooms.Remove(foundSR);
                Debug.Log("Removing room from list");
            }

        }
    }

    public SharingRoom FindSharingRoomWithConnectionID(int id)
    {
        foreach(SharingRoom sr in sharingRooms)
        {
            foreach(int pID in sr.connectionID)
            {
                if (pID == id)
                    return sr;
            }
        }
        return null;
    }

}

public class SharingRoom
{
    public string name;
    public List<int> connectionID;
    public List<string> transferData;

    public SharingRoom(string n, int creatorID)
    {
        name = n;
        connectionID = new List<int>();
        connectionID.Add(creatorID);
    }

}

static public class ClientToServerSignifiers
{
    public const int JoinSharingRoom = 1;
    public const int PartyDataSendTransferStart = 101;
    public const int PartyDataTransfer = 102;
    public const int PartyDataTransferEnd = 103;
}

static public class ServerToClientSignifiers
{
    public const int ServerSendPartyDataTransferStart = 101;
    public const int ServerSendingPartyData = 102;
    public const int ServerSendingPartyDataTransferEnd = 103;
}