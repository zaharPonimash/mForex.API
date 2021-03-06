﻿using ProtoBuf;

namespace mForex.API.Packets
{
    [ProtoContract]
    public class LoginRequestPacket : APINetworkPacket
    {
        [ProtoMember(1, IsRequired = true)]
        public int RequestId { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public int Login { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public string Password { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public int MajorProtocolVersion { get; set; }

        [ProtoMember(5, IsRequired = true)]        
        public int MinorProtocolVersion { get; set; }

        public LoginRequestPacket()
            : base(APINetworkPacketType.LoginRequest)
        { }

        public LoginRequestPacket(int reqId, int login, string password, int majorVersion, int minorVersion)
            : base(APINetworkPacketType.LoginRequest)
        {
            this.RequestId = reqId;
            this.Login = login;
            this.Password = password;
            this.MajorProtocolVersion = majorVersion;
            this.MinorProtocolVersion = minorVersion;
        }
    }

    [ProtoContract]
    public class LoginResponsePacket : APINetworkPacket
    {
        [ProtoMember(1, IsRequired = true)]
        public int RequestId { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public int Login { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public LoginStatus LoginStatus { get; set; }

        public LoginResponsePacket()
            : base(APINetworkPacketType.LoginResponse)
        {
        }
    }

    [ProtoContract]
    public class LogoutRequestPacket : APINetworkPacket
    {
        public LogoutRequestPacket()
            : base(APINetworkPacketType.LogoutRequest)
        {
        }
    }
}
