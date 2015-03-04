using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClientApi.Utils;

namespace ClientApi.Networking.Packets
{
	[PacketId(PacketId.NpcLimit)]
	public class NpcLimit : IPacket
	{
		public int Limit { get; set; }

		public NpcLimit(int limit)
		{
			Limit = limit;
		}

		public void Write(Stream s)
		{
			s.WriteInt32(Limit);
		}

		public void Read(Stream s)
		{
			Limit = s.ReadInt32();
		}
	}
}
