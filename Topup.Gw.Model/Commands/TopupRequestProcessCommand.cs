﻿using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Commands;

public interface TopupRequestProcessCommand : ICommand
{
    SaleRequestDto SaleRequest { get; }
}
// public interface TopupFulfillRequestCommand : ICommand
// {
//     SaleRequestDto SaleRequest { get; }
// }