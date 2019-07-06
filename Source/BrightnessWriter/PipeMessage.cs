using NamedPipeWrapper.Serialization;
using System;

namespace BrightnessWriter
{
	public class PipeMessage
	{
		public int Index { get; }
		public int Value { get; }

		public PipeMessage(int index, int value)
		{
			Index = index;
			Value = value;
		}
	}

	public class PipeMessageSerializer : ICustomSerializer<PipeMessage>
	{
		public byte[] Serialize(PipeMessage obj)
		{
			var index = BitConverter.GetBytes(obj.Index);
			var value = BitConverter.GetBytes(obj.Value);

			var buffer = new byte[sizeof(int) * 2];
			Array.Copy(index, 0, buffer, 0, sizeof(int));
			Array.Copy(value, 0, buffer, sizeof(int), sizeof(int));

			return buffer;
		}

		public PipeMessage Deserialize(byte[] data)
		{
			var index = BitConverter.ToInt32(data, 0);
			var value = BitConverter.ToInt32(data, sizeof(int));
			return new PipeMessage(index, value);
		}
	}
}
