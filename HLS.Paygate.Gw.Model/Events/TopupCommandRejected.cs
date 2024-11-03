﻿using System;

namespace HLS.Paygate.Gw.Model.Events;

public interface TopupCommandRejected
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}

public interface TopupGameCommandRejected
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}