﻿using BitcoinCharts.Models;
using BitcoinCharts.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCharts {
    public partial class BitcoinChartsClient {
        private Socket _socket;
        private Subject<Trade> _trades = new Subject<Trade>();
        private Subject<byte[]> _packets = new Subject<byte[]>();

        public BitcoinChartsClient() {
            _packets.Subscribe(ProcessPackets);
        }

        public IObservable<Trade> Trades { get { return _trades; } }

        public Task<bool> ConnectAsync(Action<IConnectConfigurator> configure){
            var c = new ConnectConfigurator();
            configure(c);
            _socket = c.Build();

            var tcs = new TaskCompletionSource<bool>(_socket);

            var settings = c.Settings;
            _socket.BeginConnect(settings.Address, settings.Port, EndConnect, tcs);
            
            return tcs.Task;
        }

        private void Listen(Message message, AsyncCallback callback) {
            message.Socket.BeginReceive(message.Buffer, message.Count, message.Buffer.Length - message.Count, SocketFlags.None, callback, message);
        }

        private void EndConnect(IAsyncResult ar) {
            var tcs= (TaskCompletionSource<bool>)ar.AsyncState;
            tcs.SetResult(true);

            var socket = (Socket)tcs.Task.AsyncState;

            var message = new Message {
                Buffer = new byte[socket.ReceiveBufferSize],
                Socket=socket,
            };
            Listen(message,EndReadMessage);
        }

        private void EndReadMessage(IAsyncResult ar) {
            var message = (Message)ar.AsyncState;
            var buffer = message.Buffer;
            int count = message.Socket.EndReceive(ar);

            if(count <= 0) {
                return;
            }

            message.Count += count;
            if(message.Count > message.Buffer.Length) {
                return;
            }

            for(int i = message.Count - 1; i >= 1; i--) {
                if(buffer[i] == 0x0a && buffer[i - 1] == 0x0d) {
                    count = i + 1;
                    message.Count = message.Count - count;
                    break;
                }
            }

            var packet = new byte[count];
            Buffer.BlockCopy(buffer, 0, packet, 0, count);
            _packets.OnNext(packet);

            if(message.Count != 0) {
                Buffer.BlockCopy(message.Buffer, count, message.Buffer, 0, message.Count);
            }

            Listen(message, EndReadMessage);
        }

        private void ProcessPackets(byte[] packet) {
            using(var reader = new StreamReader(new MemoryStream(packet))) {
                var line = default(string);
                while(null != (line = reader.ReadLine())) {
                    var trade = JsonConvert.DeserializeObject<Trade>(line);
                    _trades.OnNext(trade);            
                }
            }
        }

        internal class ConnectSettings {
            public string Address { get; set; }
            public int Port { get; set; }

            public int ReceiveBufferSize { get; set; }
        }

        public interface IConnectConfigurator {
            IConnectConfigurator Address(string value);
            IConnectConfigurator Port(int value);

            IConnectConfigurator ReceiveBufferSize(int value);
        }

        internal class ConnectConfigurator : IConnectConfigurator {
            private ConnectSettings _settings = new ConnectSettings {
                ReceiveBufferSize = 8 * 1024
            };

            internal ConnectSettings Settings {
                get { return _settings; }
            }

            public IConnectConfigurator Address(string value) {
                _settings.Address = value;
                return this;
            }

            public IConnectConfigurator Port(int value) {
                _settings.Port = value;
                return this;
            }

            public IConnectConfigurator ReceiveBufferSize(int value) {
                _settings.ReceiveBufferSize = value;
                return this;
            }

            public Socket Build() {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveBufferSize = _settings.ReceiveBufferSize;
                return socket;                
            }
        }
    }

    public static partial class Extensions {
        public static Task<bool> ConnectAsync(this BitcoinChartsClient client) {
            return client.ConnectAsync(x => x
                .Address("api.bitcoincharts.com")
                .Port(27007)
            );
        }
    }
}