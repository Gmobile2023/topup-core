using System;

namespace HLS.Paygate.TopupGw.Contacts.Dtos;

public abstract class DocumentDto
{
    public Guid Id { get; set; }
    public DateTime AddedAtUtc { get; set; }
}