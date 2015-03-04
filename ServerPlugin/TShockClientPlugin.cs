using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Text;
using ClientApi.Networking;
using Terraria;
using TerrariaApi.Server;

namespace ServerPlugin
{
	[ApiVersion(1,17)]
	public class TShockClientPlugin : TerrariaPlugin
	{
		public override string Author
		{
			get { return "Nyx Studios"; }
		}

		public override string Name
		{
			get { return "Custom Client Api"; }
		}

		public TShockClientPlugin(Main game) : base(game)
		{
		}

		public override void Initialize()
		{
			ServerApi.Hooks.NetGetData.Register(this, OnGetData, -1);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
			}
			base.Dispose(disposing);
		}

		private void OnGetData(GetDataEventArgs args)
		{
			if (args.MsgID != (PacketTypes) 67)
			{
				return;
			}

			args.Handled = true;
			using (var stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1))
			{
				byte packetId = stream.ReadInt8();
				byte[] buffer = stream.ReadBytes(args.Length - 2);

				var packet = PacketFactory.Instance.CreatePacket((PacketId) packetId, buffer);
				Console.WriteLine("Received custom packet {0}", (PacketId)packetId);
			}
		}
	}
}
