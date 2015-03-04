using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using ClientApi.Networking;

namespace ClientApi.Hooks
{
	public class GetDataEventArgs : HandledEventArgs
	{
		public byte Packet { get; private set; }
		public byte[] Buffer { get; private set; }

		public GetDataEventArgs(byte packet, byte[] buffer)
		{
			Packet = packet;
			Buffer = buffer;
		}
	}

	public class SendDataEventArgs : HandledEventArgs
	{
		public byte Packet { get; private set; }
		public byte[] Buffer { get; private set; }

		public SendDataEventArgs(byte packet, byte[] buffer)
		{
			Packet = packet;
			Buffer = buffer;
		}
	}

	public class GetCustomPacketEventArgs : HandledEventArgs
	{
		public IPacket Packet { get; set; }

		public GetCustomPacketEventArgs(IPacket packet)
		{
			Packet = packet;
		}
	}

	public class DataHooks
	{
		public delegate void GetDataD(GetDataEventArgs args);
		public static event GetDataD GetData;

		public delegate void SendDataD(SendDataEventArgs args);
		public static event SendDataD SendData;

		public delegate void GetCustomPacketD(GetCustomPacketEventArgs args);
		public static event GetCustomPacketD GetCustomPacket;

		public static bool OnGetData(GetDataEventArgs args)
		{
			if (GetData == null)
			{
				return false;
			}

			GetData(args);
			return args.Handled;
		}

		public static bool OnSendData(SendDataEventArgs args)
		{
			if (SendData == null)
			{
				return false;
			}

			SendData(args);
			return args.Handled;
		}

		public static bool OnGetCustomPacket(IPacket packet)
		{
			if (GetCustomPacket == null)
			{
				return false;
			}

			GetCustomPacketEventArgs args = new GetCustomPacketEventArgs(packet);
			GetCustomPacket(args);
			return args.Handled;
		}
	}
}
