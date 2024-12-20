﻿using ServiceStack.DataAnnotations;

namespace Topup.Balance.Models.Enums;

[EnumAsInt]
public enum SettlementStatus
{
    Init = 0,
    Done = 1,
    Error = 2,
    Deleted = 3,
    Rollback = 4,
}