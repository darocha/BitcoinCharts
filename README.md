BitcoinCharts
=============

Uses Reactive Extensions to publish information to subscribers.

Here's a simple example to help get your started:
```c#
static void Main(string[] args) {
	var client = new BitcoinChartsClient();            
            
	client.Trades().Subscribe(x => Console.WriteLine(x));
            
	// Just want to see MtGox?
	client.Trades(x => x
		.Symbol("mtgoxUSD")
	).Subscribe(x => Console.WriteLine(x));

	// Connect to the telnet session
	client.ConnectAsync().Wait();            

	Console.ReadLine();
}
```
