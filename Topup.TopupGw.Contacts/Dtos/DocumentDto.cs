using System;

namespace Topup.TopupGw.Contacts.Dtos;

public abstract class DocumentDto
{
    public Guid Id { get; set; }
    public DateTime AddedAtUtc { get; set; }
}