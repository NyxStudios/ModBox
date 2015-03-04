using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClientApi.Networking;
using ClientApi.Hooks;
using Terraria;

namespace MonoTerraria.Api
{
	class GetDataHandlers
	{
		private delegate bool GetDataHandlerDelegate(IPacket packet);
		private Dictionary<PacketId, GetDataHandlerDelegate> customHandlers = new Dictionary<PacketId, GetDataHandlerDelegate>();

		public GetDataHandlers()
		{
			customHandlers.Add(PacketId.NpcLimit, NpcLimitHandler);
			DataHooks.GetData += HandleGetData;
			DataHooks.GetCustomPacket += HandleCustomData;
		}

		public void HandleGetData(GetDataEventArgs args)
		{
			return;
		}

		private void HandleCustomData(ClientApi.Hooks.GetCustomPacketEventArgs args)
		{
			var id = PacketFactory.Instance.GetPacketId(args.Packet);
			//Console.WriteLine("Recieved Packet: {0}", id);
			GetDataHandlerDelegate handler;
			if (customHandlers.TryGetValue(id, out handler))
				args.Handled = handler(args.Packet);
		}

		private bool NpcLimitHandler(IPacket packet)
		{
			var pck = packet as ClientApi.Networking.Packets.NpcLimit;
			NPC.MaxNPCs = pck.Limit;
			for (int i = 0; i < NPC.MaxNPCs; i++)
			{
				Main.npc[i] = new NPC();
				Main.npc[i].whoAmI = i;
			}
			return true;
		}
	}
}
