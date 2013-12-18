using BitcoinCharts.Models;
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
using System.Reactive.Linq;

namespace BitcoinCharts {
    public partial class BitcoinChartsClient {
        private static readonly int ReceiveBufferSize = 8096;
        private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private ConnectSettings _settings;
        private SocketAsyncEventArgs _receive;
        private Subject<Trade> _trades = new Subject<Trade>();
        private Subject<byte[]> _packets = new Subject<byte[]>();
        private IDisposable _subscription;
        private Message _message = new Message { Buffer = new byte[ReceiveBufferSize] };

        private static TResult Configure<TSource, TResult>(Action<TSource> configure) where TResult : TSource, new() {
            var result = new TResult();
            configure(result);
            return result;
        }

        public void Connect(Action<IConnectConfigurator> configure) {
            var c = Configure<IConnectConfigurator, ConnectConfigurator>(configure);
            _settings = c.Build();            
        }

        private void  Connect(ConnectSettings settings) {
            var completed = default(EventHandler<SocketAsyncEventArgs>);

            var saea = new SocketAsyncEventArgs {
                RemoteEndPoint = new DnsEndPoint(_settings.Address, _settings.Port),
                UserToken = _socket
            };

            saea.Completed += completed = (s, e) => {
                e.Completed -= completed;
                if(e.SocketError == SocketError.Success) {
                    _receive = _receive ?? (_receive = CreateSocketAsyncEventArgs());
                    Receive(_receive);
                } else {
                    Connect(settings);
                }
            };

            if(false == _socket.ConnectAsync(saea)) {
                completed(this, saea);
            }
        }

        private SocketAsyncEventArgs CreateSocketAsyncEventArgs() {
            var buffer = new byte[ReceiveBufferSize];
            var message = new Message {
                Buffer = buffer,
                Count = 0,
            };
            var saea = new SocketAsyncEventArgs();
            saea.SetBuffer(buffer, 0, buffer.Length);
            saea.Completed += ProcessCompleted;
            saea.UserToken = message;
            return saea;
        }

        private void Receive(SocketAsyncEventArgs e) {
            var message = e.UserToken as Message;
            e.SetBuffer(message.Count, message.Buffer.Length - message.Count);
            _socket.ReceiveAsync(e);
        }

        private void ProcessCompleted(object sender, SocketAsyncEventArgs e) {
            ProcessCompleted(e);
        }

        private void ProcessCompleted(SocketAsyncEventArgs e) {
            switch(e.LastOperation) {
                case SocketAsyncOperation.Connect:
                    ProcessConnect(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Disconnect:
                    ProcessDisconnect(e);
                    break;
                default:                    
                    break;
            }
        }

        private void ProcessConnect(SocketAsyncEventArgs e) {
            if(e.SocketError == SocketError.Success) {
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e) {
            Console.WriteLine((new { SocketError = e.SocketError, BytesTransfered = e.BytesTransferred }.ToString()));
            if(e.SocketError == SocketError.Success && e.BytesTransferred > 0) {
                var buffer = e.Buffer;
                var count = e.BytesTransferred;
                var message = e.UserToken as Message;

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
                Receive(e);
            } else if(e.SocketError == SocketError.ConnectionReset) {
                Connect(_settings);
            }
        }

        private void ProcessDisconnect(SocketAsyncEventArgs e) {
            if(e.SocketError == SocketError.Success) {
                Connect(_settings);
            }
        }

        public IObservable<Trade> Trades(Action<ITradeConfigurator> configure) {
            var c = new TradeConfigurator(_trades);
            configure(c);
            return c.Build();
        }
        
        private static void ProcessPackets(byte[] packet, IObserver<Trade> trades) {
            using(var reader = new StreamReader(new MemoryStream(packet))) {
                var line = default(string);
                while(null != (line = reader.ReadLine())) {
                    var trade = JsonConvert.DeserializeObject<Trade>(line);
                    trades.OnNext(trade);            
                }
            }
        }

        public interface ITradeConfigurator {
            ITradeConfigurator Symbol(string value);
        }

        internal class TradeConfigurator : ITradeConfigurator {
            private IObservable<Trade> _source;
            private string _symbol;

            public TradeConfigurator(IObservable<Trade> source) {
                _source = source;
            }

            public ITradeConfigurator Symbol(string value) {
                _symbol = value;
                return this;
            }

            public IObservable<Trade> Build() {
                return _source.Where(x => _symbol == "*" || x.Symbol.Equals(_symbol, StringComparison.InvariantCultureIgnoreCase));
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

            public ConnectSettings Build() {
                return _settings;
            }
        }
    }

    public static partial class Extensions {
        public static void Connect(this BitcoinChartsClient client) {
            client.Connect(x => x
                    .Address("api.bitcoincharts.com")
                    .Port(27007)
            );
        }
        public static IObservable<Trade> Trades(this BitcoinChartsClient client) {
            return client.Trades(x => x.Symbol("*"));
        }
    }
}