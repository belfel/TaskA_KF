using TMPro;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using Unity.Services.Authentication;

public class RelayFrontend : MonoBehaviour
{
    public string serverAddress = "127.0.0.1";
    public TMP_InputField joinCode;
    public ushort serverPort = 8000;

    public bool dedicatedServer;

    ConnectionState m_State;
    HostServer m_HostServerSystem;
    ConnectingPlayer m_HostClientSystem;

    enum ConnectionState
    {
        Unknown,
        SetupHost,
        SetupClient,
        JoinGame,
        JoinLocalGame,
    }

    public void StartDedicatedServer()
    {
        var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");

        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(serverPort);
        {
            using var networkDriverQuery = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            networkDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(endpoint);
        }

        SceneManager.LoadSceneAsync("SampleScene");
    }

    private void Start()
    {
        if (dedicatedServer)
        {
            StartDedicatedServer();
            return;
        }
    }

    public void Update()
    {
        switch (m_State)
        {
            case ConnectionState.SetupHost:
                {
                    HostServer();
                    m_State = ConnectionState.SetupClient;
                    goto case ConnectionState.SetupClient;
                }
            case ConnectionState.SetupClient:
                {
                    var isServerHostedLocally = m_HostServerSystem?.RelayServerData.Endpoint.IsValid;
                    var enteredJoinCode = !string.IsNullOrEmpty(joinCode.text);
                    if (isServerHostedLocally.GetValueOrDefault())
                    {
                        SetupClient();
                        m_HostClientSystem.GetJoinCodeFromHost();
                        m_State = ConnectionState.JoinLocalGame;
                        goto case ConnectionState.JoinLocalGame;
                    }

                    if (enteredJoinCode)
                    {
                        JoinAsClient();
                        m_State = ConnectionState.JoinGame;
                        goto case ConnectionState.JoinGame;
                    }
                    break;
                }
            case ConnectionState.JoinGame:
                {
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        ConnectToRelayServer();
                        m_State = ConnectionState.Unknown;
                    }
                    break;
                }
            case ConnectionState.JoinLocalGame:
                {
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        SetupRelayHostedServerAndConnect();
                        m_State = ConnectionState.Unknown;
                    }
                    break;
                }
            case ConnectionState.Unknown:
            default: return;
        }
    }

    public void Host()
    {
        m_State = ConnectionState.SetupHost;
    }

    public void JoinByCode()
    {
        m_State = ConnectionState.SetupClient;
    }

    void HostServer()
    {
        var world = World.All[0];
        m_HostServerSystem = world.GetOrCreateSystemManaged<HostServer>();
        var enableRelayServerEntity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<EnableRelayServer>());
        world.EntityManager.AddComponent<EnableRelayServer>(enableRelayServerEntity);

        var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
        simGroup.AddSystemToUpdateList(m_HostServerSystem);
    }

    void SetupClient()
    {
        var world = World.All[0];
        m_HostClientSystem = world.GetOrCreateSystemManaged<ConnectingPlayer>();
        var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
        simGroup.AddSystemToUpdateList(m_HostClientSystem);
    }

    private void JoinAsClient()
    {
        SetupClient();
        var world = World.All[0];
        var enableRelayServerEntity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<EnableRelayServer>());
        world.EntityManager.AddComponent<EnableRelayServer>(enableRelayServerEntity);

        m_HostClientSystem.JoinUsingCode(joinCode.text);
    }

    void SetupRelayHostedServerAndConnect()
    {
        if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
        {
            UnityEngine.Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
            return;
        }

        var world = World.All[0];
        var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;
        var relayServerData = world.GetExistingSystemManaged<HostServer>().RelayServerData;
        var joinCode = world.GetExistingSystemManaged<HostServer>().JoinCode;

        var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
        NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData);
        var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
        NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

        DestroyLocalSimulationWorld();
        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = server;

        SceneManager.LoadSceneAsync("SampleScene");

        Debug.Log(joinCode);

        var networkStreamEntity = server.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestListen>());
        server.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestListen");
        server.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4 });

        networkStreamEntity = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
        client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
        client.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });
    }

    void ConnectToRelayServer()
    {
        var world = World.All[0];
        var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;

        var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
        NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), relayClientData);
        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
        NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

        DestroyLocalSimulationWorld();
        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = client;

        SceneManager.LoadSceneAsync("SampleScene");

        var networkStreamEntity = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
        client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
        client.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });
    }

    protected void DestroyLocalSimulationWorld()
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }
    }

    public void ConnectToServer()
    {
        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
        DestroyLocalSimulationWorld();
        
        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = client;
        SceneManager.LoadSceneAsync("SampleScene");

        var ep = NetworkEndpoint.Parse(serverAddress, serverPort);
        {
            using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
        }
    }
}
