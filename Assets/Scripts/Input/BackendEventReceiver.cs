using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    public sealed class BackendEventReceiver : MonoBehaviour
    {
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private string websocketUrl = "ws://127.0.0.1:8000/ws/unity";
        [SerializeField] private float reconnectDelay = 3f;

        private KiForgeEventBus eventBus;
        private ClientWebSocket socket;
        private CancellationTokenSource cts;
        private readonly Queue<string> messageQueue = new Queue<string>();
        private readonly object queueLock = new object();

        public bool IsConnected { get; private set; }

        public void Initialize(KiForgeEventBus bus)
        {
            eventBus = bus;
        }

        private void Start()
        {
            if (connectOnStart)
                ConnectAsync();
        }

        private void Update()
        {
            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                    DispatchMessage(messageQueue.Dequeue());
            }
        }

        private async void ConnectAsync()
        {
            cts = new CancellationTokenSource();
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    socket = new ClientWebSocket();
                    await socket.ConnectAsync(new Uri(websocketUrl), cts.Token);
                    IsConnected = true;
                    Debug.Log($"[KiForge] WebSocket connected to {websocketUrl}");
                    await ReceiveLoopAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[KiForge] WebSocket: {e.Message} — retrying in {reconnectDelay}s");
                }
                finally
                {
                    IsConnected = false;
                    socket?.Dispose();
                    socket = null;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            while (socket.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var json = sb.ToString();
                lock (queueLock)
                    messageQueue.Enqueue(json);
            }
        }

        public async Task SendJsonAsync(string json)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                cts?.Token ?? CancellationToken.None);
        }

        private void DispatchMessage(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<BackendMessage>(json);
                if (msg == null || string.IsNullOrEmpty(msg.type)) return;

                switch (msg.type)
                {
                    case KiForgeEventNames.ChargeStart:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.ChargeStart, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.ChargeUpdate:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.ChargeUpdate, msg.GetOrigin(), msg.GetAim(), msg.Charge, msg.HoldSeconds));
                        break;
                    case KiForgeEventNames.BlastRelease:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.BlastRelease, msg.GetOrigin(), msg.GetAim(), msg.Charge, msg.HoldSeconds));
                        break;
                    case KiForgeEventNames.ShieldStart:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.ShieldStart, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.ShieldEnd:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.ShieldEnd, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.SlashLeft:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.SlashLeft, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.SlashRight:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.SlashRight, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.Ultimate:
                        eventBus?.PublishGesture(GestureEvent.Create(GestureEventType.Ultimate, msg.GetOrigin(), msg.GetAim()));
                        break;
                    case KiForgeEventNames.PoseUpdate:
                        eventBus?.PublishPose(new PoseEvent
                        {
                            wrist = msg.GetWrist(),
                            bodyCenter = msg.GetBodyCenter(),
                            aim = msg.GetAim(),
                            confidence = msg.Confidence,
                            timestamp = msg.timestamp
                        });
                        break;
                    default:
                        Debug.Log($"[KiForge] Unhandled message type: {msg.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[KiForge] Parse error: {e.Message}\n{json}");
            }
        }

        public void InjectGesture(GestureEvent gesture) => eventBus?.PublishGesture(gesture);
        public void InjectPose(PoseEvent pose) => eventBus?.PublishPose(pose);

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
            socket?.Dispose();
        }

        [Serializable]
        private sealed class Vec2Msg
        {
            public float x;
            public float y;
        }

        // Mirrors the backend NormalizedEvent.payload object. All combat/pose fields
        // are nested here — the backend wraps every websocket frame as
        // { "type", "timestamp", "payload": { ... } }.
        [Serializable]
        private sealed class Payload
        {
            public float charge;
            public float holdSeconds;
            public float emgIntensity;
            public float confidence;
            public Vec2Msg origin;
            public Vec2Msg aim;
            public Vec2Msg wrist;
            public Vec2Msg bodyCenter;
            public Vec2Msg target;
        }

        [Serializable]
        private sealed class BackendMessage
        {
            public string type;
            public double timestamp;
            public Payload payload;

            private static Vector2 ToVec(Vec2Msg v, Vector2 fallback) =>
                v != null ? new Vector2(v.x, v.y) : fallback;

            public float Charge => payload?.charge ?? 0f;
            public float HoldSeconds => payload?.holdSeconds ?? 0f;
            public float Confidence => payload?.confidence ?? 0f;

            public Vector2 GetOrigin() => ToVec(payload?.origin, Vector2.zero);
            public Vector2 GetAim() => ToVec(payload?.aim, Vector2.right);
            public Vector2 GetWrist() => ToVec(payload?.wrist, Vector2.zero);
            public Vector2 GetBodyCenter() => ToVec(payload?.bodyCenter, Vector2.zero);
        }
    }
}
