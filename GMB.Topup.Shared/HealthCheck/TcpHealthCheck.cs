﻿using System.Net.Sockets;

namespace GMB.Topup.Shared.HealthCheck;

public class TcpHealthCheck
{
    private readonly TcpListener _listener;

    public TcpHealthCheck(TcpListener listener)
    {
        _listener = listener;
    }
}