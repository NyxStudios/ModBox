using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClientApi.Utils;

namespace ClientApi.Networking.Packets
{
	[PacketId(PacketId.ClientVersion)]
	public class ClientVersion : IPacket
	{
		public string Version;

		public void Read(Stream s)
		{
			Version = s.ReadString();
		}

		public void Write(Stream s)
		{
			if (Version != null)
				s.WriteString(Version);
		}
	}
}
