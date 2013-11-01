using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitcoinCharts;
using System.Reactive.Linq;

namespace Sandbox {
    class Program {
        static void Main(string[] args) {
            var client = new BitcoinChartsClient();

            client.GetTradesAsync()
                .Result.ToObservable()
                .Subscribe(x => Console.WriteLine(x));

            client.Trades.Subscribe(x => Console.WriteLine(x));

            client.ConnectAsync();

            Console.ReadLine();
        }
    }
}
