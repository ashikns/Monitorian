using NamedPipeWrapper;

namespace BrightnessWriter
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 2) { return; }

			var client = new NamedPipeClient<PipeMessage>("Monitorian", new PipeMessageSerializer());
			client.Start();

			if (client.WaitForConnection(1000) && int.TryParse(args[0], out var index) && int.TryParse(args[1], out var value))
			{
				if (value < 0 || value > 100) { return; }
				client.PushMessage(new PipeMessage(index, value));
			}

			client.Finish();
			client.WaitForDisconnection();
		}
	}
}
